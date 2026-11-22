using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Overbryce.App.Rendering;
using Overbryce.Core.Geometry;
using Overbryce.Core.IO;
using Overbryce.Core.Model;

namespace Overbryce.App.Export;

/// <summary>
/// Exports an animation for Blender: a JSON description with a binary side file (meshes and animation keys), the
/// textures as PNG files and <c>overbryce_blender.py</c>, which builds a .blend file from them (run by Blender itself).
/// <para>
/// Between two key states every attribute follows a cubic of time (the non-uniform Catmull-Rom spline of
/// <see cref="Overbryce.Core.SplineWeights"/>, plus the linear permanent rotation), and a Blender Bezier key pair with
/// its handles at a third of the interval is exactly that cubic. So each segment is sampled at u = 0, 1/3, 2/3 and 1,
/// turned into one Bezier span, and checked at u = 1/2; where it is not a cubic (a permanent orbit, a clamped value, a
/// field of view turned into a focal length) the segment gets a key at every frame instead.
/// </para>
/// <para>
/// The scene graph is kept: a group becomes an empty for its rotation with a child empty for its offset (a group
/// translates before it rotates), an element a mesh object with the Euler order of its matrix. Shapes are tessellated
/// exactly like the renderer does; a changing size or control point becomes a shape key (the vertices are linear in
/// these parameters, so the result is exact too). Materials are shared per texture and read the element's colours from
/// animated custom properties of its object; fog and ambient light come from custom properties of the Blender scene.
/// </para>
/// </summary>
internal sealed class BlenderExporter
{
    private static readonly float[] SampleU = [0f, 1f / 3f, 0.5f, 2f / 3f, 1f];
    private const float Deg = MathF.PI / 180f;

    /// <summary>The camera sensor is fitted vertically, so its height and the field of view give the focal length.</summary>
    private const float SensorHeight = 24f;

    /// <summary>A jump at a key state: the value before it is keyed this many frames earlier.</summary>
    private const float JumpGap = 0.01f;

    private const string ScriptName = "overbryce_blender.py";

    /// <summary>The distance at which a spot light's brightness is right in EEVEE (about how far it lights the ground).</summary>
    private const double SpotReference = 3;

    private readonly Animation _animation;
    private readonly string _sourceDirectory;
    private readonly float _fps, _start;
    private readonly float[] _times;
    private readonly Dictionary<string, Track> _tracks = new();
    private readonly List<Track> _order = [];
    private readonly Track _scene;
    private readonly List<(int State, string? Camera)> _cameras = [];
    private readonly List<string> _warnings = [];

    public BlenderExporter(Animation animation, string sourceFile)
    {
        _animation = animation;
        _sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourceFile))!;
        _fps = Math.Clamp(animation.Fps, 1, 128);
        _start = animation.StartTime;
        _times = animation.Scenes.Select(s => s.AniTime).ToArray();
        _scene = new Track("", "Scene", null, Kind.Scene, animation.Scenes[0]) { Channels = SceneChannels };
    }

    public sealed record Result(string JsonPath, int Objects, int Meshes, int Curves, int Keys, int DenseSegments, IReadOnlyList<string> Warnings);

    // ================================================================== channels

    private enum Kind { Group, Mesh, Camera, Light, Scene }

    /// <summary>What a channel drives in Blender: the object, the inner (offset) empty of a group, the object's data,
    /// a light's aiming target, a custom property of the object or the scene, or a shape key (by parameter).</summary>
    private readonly record struct Channel(string Target, string Path, int Index);

    private static readonly Channel[] Transform =
    [
        new("obj", "location", 0), new("obj", "location", 1), new("obj", "location", 2),
        new("obj", "rotation_euler", 0), new("obj", "rotation_euler", 1), new("obj", "rotation_euler", 2),
    ];

    private static readonly Channel[] GroupChannels =
    [
        new("inner", "location", 0), new("inner", "location", 1), new("inner", "location", 2),
        new("obj", "rotation_euler", 0), new("obj", "rotation_euler", 1), new("obj", "rotation_euler", 2),
    ];

    private static readonly Channel[] MaterialChannels =
    [
        new("prop", "ob_dif", 0), new("prop", "ob_dif", 1), new("prop", "ob_dif", 2), new("prop", "ob_alpha", -1),
        new("prop", "ob_emi", 0), new("prop", "ob_emi", 1), new("prop", "ob_emi", 2),
        new("prop", "ob_amb", 0), new("prop", "ob_amb", 1), new("prop", "ob_amb", 2),
        new("prop", "ob_spec", 0), new("prop", "ob_spec", 1), new("prop", "ob_spec", 2), new("prop", "ob_shin", -1),
    ];

    private static readonly Channel[] CameraChannels = [.. Transform, new("data", "lens", -1)];

    private const int LightColor = 6, LightSpecular = 9, LightEnergy = 10;

    private static readonly Channel[] LightChannels =
    [
        new("obj", "location", 0), new("obj", "location", 1), new("obj", "location", 2),
        new("target", "location", 0), new("target", "location", 1), new("target", "location", 2),
        new("data", "color", 0), new("data", "color", 1), new("data", "color", 2),
        new("data", "specular_factor", -1), new("data", "energy", -1),
        new("data", "spot_size", -1), new("data", "spot_blend", -1),
    ];

    private static readonly Channel[] SceneChannels =
    [
        new("scene", "sc_amb", 0), new("scene", "sc_amb", 1), new("scene", "sc_amb", 2),
        new("scene", "sc_fogcol", 0), new("scene", "sc_fogcol", 1), new("scene", "sc_fogcol", 2),
        new("scene", "sc_fog", 0), new("scene", "sc_fog", 1), new("scene", "sc_fog", 2), new("scene", "sc_fogmode", -1),
    ];

    private static Kind KindOf(Element e) => e switch
    {
        Scene => Kind.Scene,
        GroupElement => Kind.Group,
        CameraElement => Kind.Camera,
        LightElement => Kind.Light,
        _ => Kind.Mesh,
    };

    private static Channel[] ChannelsOf(Element e) => KindOf(e) switch
    {
        Kind.Group => GroupChannels,
        Kind.Camera => CameraChannels,
        Kind.Light => LightChannels,
        _ => [.. Transform, .. MaterialChannels, .. Enumerable.Range(0, ShapeParams(e).Length).Select(i => new Channel("shape", "", i))],
    };

    /// <summary>The channel values of an element, already in Blender's units.</summary>
    private static float[] Values(Element e)
    {
        switch (e)
        {
            case GroupElement:
                return [e.X, e.Y, e.Z, e.Alpha * Deg, e.Beta * Deg, e.Gamma * Deg];
            case CameraElement c:
                float fov = Math.Clamp(c.FieldOfView, 1f, 179f);
                return [e.X, e.Y, e.Z, e.Alpha * Deg, e.Beta * Deg, e.Gamma * Deg, SensorHeight / 2 / MathF.Tan(fov * Deg / 2)];
            case LightElement l:
                float cut = (float)(l.SpotCutoff > 90 ? 90 : Math.Max(l.SpotCutoff, 0.5));
                return
                [
                    l.X, l.Y, l.Z, l.X + l.Alpha, l.Y + l.Beta, l.Z + l.Gamma,
                    l.Diffuse.R, l.Diffuse.G, l.Diffuse.B, MathF.Max(l.Specular.R, MathF.Max(l.Specular.G, l.Specular.B)),
                    l.Enabled ? 1 : 0, 2 * cut * Deg, SpotBlend(cut, (float)l.SpotExponent),
                ];
            default:
                return
                [
                    e.X, e.Y, e.Z, e.Alpha * Deg, e.Beta * Deg, e.Gamma * Deg,
                    e.Diffuse.R, e.Diffuse.G, e.Diffuse.B, e.Diffuse.A,
                    e.Emission.R, e.Emission.G, e.Emission.B,
                    e.Ambient.R, e.Ambient.G, e.Ambient.B,
                    e.Specular.R, e.Specular.G, e.Specular.B, Math.Clamp(e.Shininess, 0f, 1f),
                    .. ShapeParams(e),
                ];
        }
    }

    /// <summary>
    /// The soft edge of a Blender spot for an OpenGL spot exponent: the part of the cone beyond the angle where
    /// cos^exponent falls to one half.
    /// </summary>
    private static float SpotBlend(float cutoff, float exponent)
    {
        if (exponent <= 0) return 0.05f;
        float half = MathF.Acos(MathF.Pow(0.5f, 1f / exponent)) / Deg;
        return Math.Clamp(1 - half / cutoff, 0.05f, 1f);
    }

    /// <summary>The scene's own values: the ambient light (global ambient plus the lights' ambient) and the fog.</summary>
    private static float[] SceneValues(Scene s)
    {
        Vector3 ambient = new(0.2f);   // GL_LIGHT_MODEL_AMBIENT
        foreach (var light in s.Lights.Take(LightElement.MaxPerScene))
            if (light.Enabled) ambient += new Vector3(light.Ambient.R, light.Ambient.G, light.Ambient.B);
        return
        [
            ambient.X, ambient.Y, ambient.Z,
            s.FogColor.R, s.FogColor.G, s.FogColor.B,
            s.FogStart, s.FogEnd, s.FogDensity, (float)s.FogMode,
        ];
    }

    /// <summary>The size parameters a shape's vertices depend on linearly (they can become shape keys).</summary>
    private static float[] ShapeParams(Element e) => e switch
    {
        SphereElement s => [(float)s.Radius],
        ConeElement c => [(float)c.BaseRadius, (float)c.TopRadius, (float)c.Height],
        DiskElement d => [(float)d.InnerRadius, (float)d.OuterRadius],
        BoxElement b => [b.SizeX, b.SizeY, b.SizeZ],
        RectangleElement r => [r.SizeX, r.SizeY],
        TriangleElement t => [t.X1, t.Y1, t.X2, t.Y2],
        TorusElement t => [t.TubeRadius, t.RingRadius],
        MeshElement m => (float[])m.Points.Clone(),
        _ => [],
    };

    private static void SetShapeParam(Element e, int i, float v)
    {
        switch (e)
        {
            case SphereElement s: s.Radius = v; break;
            case ConeElement c when i == 0: c.BaseRadius = v; break;
            case ConeElement c when i == 1: c.TopRadius = v; break;
            case ConeElement c: c.Height = v; break;
            case DiskElement d when i == 0: d.InnerRadius = v; break;
            case DiskElement d: d.OuterRadius = v; break;
            case BoxElement b when i == 0: b.SizeX = v; break;
            case BoxElement b when i == 1: b.SizeY = v; break;
            case BoxElement b: b.SizeZ = v; break;
            case RectangleElement r when i == 0: r.SizeX = v; break;
            case RectangleElement r: r.SizeY = v; break;
            case TriangleElement t when i == 0: t.X1 = v; break;
            case TriangleElement t when i == 1: t.Y1 = v; break;
            case TriangleElement t when i == 2: t.X2 = v; break;
            case TriangleElement t: t.Y2 = v; break;
            case TorusElement t when i == 0: t.TubeRadius = v; break;
            case TorusElement t: t.RingRadius = v; break;
            case MeshElement m: m.Points[i] = v; break;
        }
    }

    // ================================================================== tracks

    /// <summary>One element of the animation (an element path) over all the key states it appears in.</summary>
    private sealed class Track(string path, string name, string? parent, Kind kind, Element first)
    {
        public string Path { get; } = path;
        public string Name { get; } = name;
        public string? ParentPath { get; } = parent;
        public Kind Kind { get; } = kind;

        /// <summary>The element as in the first state it appears in (type, texture, tessellation quality).</summary>
        public Element First { get; } = first;

        public required Channel[] Channels { get; init; }

        /// <summary>Per segment the element is present in: its values at <see cref="SampleU"/>.</summary>
        public SortedDictionary<int, float[][]> Samples { get; } = new();

        /// <summary>Segments that are not a cubic for some channel, with the values at every frame inside them.</summary>
        public Dictionary<int, List<(float Frame, float[] Values)>> Dense { get; } = new();

        /// <summary>Key states the element is present in.</summary>
        public SortedSet<int> States { get; } = new();

        /// <summary>The values in the last key state, when the element is there.</summary>
        public float[]? End { get; set; }

        /// <summary>Lights: the distance to the camera, sampled over the film (for the brightness EEVEE gets right).</summary>
        public List<float> CameraDistances { get; } = [];

        public int Index { get; set; } = -1;
    }

    private static IEnumerable<(string Path, string? Parent, Element E)> Walk(GroupElement group, string? parent)
    {
        foreach (var child in group.Children)
        {
            string path = parent == null ? child.Name : parent + "/" + child.Name;
            yield return (path, parent, child);
            if (child is GroupElement g)
                foreach (var inner in Walk(g, path)) yield return inner;
        }
    }

    private static string KeyOf(string path, Element e) => path + "|" + (int)e.Type;

    private Track TrackFor(string path, string? parent, Element e)
    {
        string key = KeyOf(path, e);
        if (_tracks.TryGetValue(key, out var track)) return track;
        track = new Track(path, e.Name, parent == null ? null : parent, KindOf(e), e.Clone()) { Channels = ChannelsOf(e) };
        _tracks[key] = track;
        _order.Add(track);
        return track;
    }

    // ================================================================== sampling

    private void Sample()
    {
        var scenes = _animation.Scenes;
        for (int k = 0; k < scenes.Count; k++)
            foreach (var (path, parent, e) in Walk(scenes[k], null))
                TrackFor(path, parent, e).States.Add(k);

        for (int k = 0; k + 1 < scenes.Count; k++)
        {
            if (_times[k + 1] - _times[k] <= 0) continue;
            var samples = SampleU.Select(u => _animation.EvaluateSegment(k, u)).ToArray();
            var maps = samples.Select(s => Walk(s, null).ToDictionary(x => KeyOf(x.Path, x.E), x => x.E)).ToArray();
            foreach (var (key, e) in maps[0])
            {
                var track = _tracks[key];
                var values = new float[SampleU.Length][];
                for (int i = 0; i < SampleU.Length; i++) values[i] = Values(maps[i][key]);
                track.Samples[k] = values;
            }
            _scene.Samples[k] = samples.Select(SceneValues).ToArray();
            _cameras.Add((k, CameraKey(samples[0])));
            LightDistances(samples[2]);
        }

        var end = _animation.Evaluate(_animation.EndTime)!;
        foreach (var (path, _, e) in Walk(end, null))
            _tracks[KeyOf(path, e)].End = Values(e);
        _scene.End = SceneValues(end);
        _cameras.Add((scenes.Count - 1, CameraKey(end)));

        // elements only in key states without a time of their own are never shown, but still get an object
        foreach (var track in _order)
            if (track.Samples.Count == 0 && track.End == null) track.End = Values(track.First);
    }

    private static string? CameraKey(Scene s)
    {
        var cam = s.ActiveCamera;
        if (cam == null) return null;
        var path = new List<string>();
        for (Element? e = cam; e != null && e is not Scene; e = e.Parent) path.Insert(0, e.Name);
        return KeyOf(string.Join("/", path), cam);
    }

    private void LightDistances(Scene s)
    {
        var cam = s.ActiveCamera;
        if (cam == null) return;
        var eye = Transforms.World(cam).Translation;
        foreach (var (path, _, e) in Walk(s, null))
            if (e is LightElement l)
            {
                var at = Vector3.Transform(new Vector3(l.X, l.Y, l.Z), Transforms.ParentChain(l));
                _tracks[KeyOf(path, e)].CameraDistances.Add(Vector3.Distance(at, eye));
            }
    }

    /// <summary>Marks the segments where some channel is not the cubic through the samples, and samples them at every frame.</summary>
    private int Densify()
    {
        var needed = new Dictionary<int, List<Track>>();
        foreach (var track in _order.Append(_scene))
            foreach (var (k, s) in track.Samples)
                if (!IsCubic(s))
                {
                    if (!needed.TryGetValue(k, out var list)) needed[k] = list = [];
                    list.Add(track);
                }

        foreach (var (k, tracks) in needed)
        {
            float t0 = _times[k], t1 = _times[k + 1];
            foreach (var track in tracks) track.Dense[k] = [];
            for (int f = (int)MathF.Floor(Frame(t0)) + 1; f < Frame(t1) - 1e-3f; f++)
            {
                float t = _start + f / _fps;
                var scene = _animation.EvaluateSegment(k, (t - t0) / (t1 - t0));
                var map = Walk(scene, null).ToDictionary(x => KeyOf(x.Path, x.E), x => x.E);
                foreach (var track in tracks)
                    track.Dense[k].Add((f, track.Kind == Kind.Scene ? SceneValues(scene) : Values(map[KeyOf(track.Path, track.First)])));
            }
        }
        return needed.Count;
    }

    private static bool IsCubic(float[][] s)
    {
        for (int c = 0; c < s[0].Length; c++)
        {
            var (p1, p2) = Handles(s[0][c], s[1][c], s[3][c], s[4][c]);
            float mid = (s[0][c] + 3 * p1 + 3 * p2 + s[4][c]) / 8;
            float scale = 1 + MathF.Max(MathF.Abs(s[0][c]), MathF.Abs(s[4][c]));
            if (MathF.Abs(mid - s[2][c]) > 2e-4f * scale) return false;
        }
        return true;
    }

    /// <summary>The inner control points of the cubic Bezier through f0 (u=0), f1 (1/3), f2 (2/3) and f3 (1).</summary>
    private static (float P1, float P2) Handles(float f0, float f1, float f2, float f3)
    {
        float a = 27 * f1 - 8 * f0 - f3, b = 27 * f2 - f0 - 8 * f3;
        return ((2 * a - b) / 18, (2 * b - a) / 18);
    }

    private float Frame(float time) => (time - _start) * _fps;

    // ================================================================== keys

    private readonly record struct Key(float Frame, float Value, float LX, float LY, float RX, float RY, int Interp);

    private const int Bezier = 0, Linear = 1, Constant = 2;

    /// <summary>The keys of channel <paramref name="c"/> of a track (null when the value never changes).</summary>
    private List<Key>? Keys(Track track, int c, Func<float, float> map)
    {
        float first = float.NaN;
        bool changes = false;
        void See(float v)
        {
            if (float.IsNaN(first)) first = v;
            else if (MathF.Abs(v - first) > 1e-6f * (1 + MathF.Abs(first))) changes = true;
        }
        foreach (var s in track.Samples.Values) foreach (var v in s) See(map(v[c]));
        foreach (var d in track.Dense.Values) foreach (var (_, v) in d) See(map(v[c]));
        if (track.End != null) See(map(track.End[c]));
        if (!changes) return null;

        var keys = new List<Key>();
        void Start(float frame, float value, int interp)
        {
            if (keys.Count > 0)
            {
                var last = keys[^1];
                if (MathF.Abs(last.Frame - frame) < 1e-4f)
                {
                    if (MathF.Abs(last.Value - value) <= 1e-5f * (1 + MathF.Abs(value)))
                    {
                        keys[^1] = last with { Interp = interp };
                        return;
                    }
                    // a jump at the key state: the value before it a moment earlier
                    keys[^1] = last with { Frame = last.Frame - JumpGap, LX = MathF.Min(last.LX, last.Frame - JumpGap), RX = last.Frame - JumpGap / 2 };
                }
                else keys[^1] = last with { Interp = Constant };
            }
            keys.Add(new Key(frame, value, frame - 1, value, frame + 1, value, interp));
        }

        foreach (var (k, s) in track.Samples)
        {
            float t0 = Frame(_times[k]), t1 = Frame(_times[k + 1]), d = t1 - t0;
            float f0 = map(s[0][c]), f3 = map(s[4][c]);
            if (track.Dense.TryGetValue(k, out var dense))
            {
                Start(t0, f0, Linear);
                foreach (var (frame, v) in dense)
                    keys.Add(new Key(frame, map(v[c]), frame - 0.3f, map(v[c]), frame + 0.3f, map(v[c]), Linear));
                keys.Add(new Key(t1, f3, t1 - 0.3f, f3, t1 + 1, f3, Linear));
            }
            else
            {
                var (p1, p2) = Handles(f0, map(s[1][c]), map(s[3][c]), f3);
                Start(t0, f0, Bezier);
                keys[^1] = keys[^1] with { RX = t0 + d / 3, RY = p1 };
                keys.Add(new Key(t1, f3, t1 - d / 3, p2, t1 + 1, f3, Bezier));
            }
        }
        if (track.End != null && track.States.Contains(_times.Length - 1))
        {
            float tEnd = Frame(_times[^1]);
            if (keys.Count == 0 || MathF.Abs(keys[^1].Frame - tEnd) > 1e-4f) Start(tEnd, map(track.End[c]), Constant);
        }
        if (keys.Count > 0) keys[^1] = keys[^1] with { Interp = Constant };
        return keys;
    }

    /// <summary>Frames where the element appears (true) or disappears (false).</summary>
    private List<(float Frame, bool Visible)> Visibility(Track track)
    {
        var changes = new List<(float, bool)>();
        bool? visible = null;
        for (int k = 0; k < _times.Length; k++)
        {
            // a key state with no time of its own (at a cut) is never shown
            if (k < _times.Length - 1 && _times[k + 1] - _times[k] <= 0) continue;
            bool present = track.States.Contains(k);
            if (present == visible) continue;
            changes.Add((Frame(_times[k]), present));
            visible = present;
        }
        if (changes.Count == 0) changes.Add((0, false));
        return changes;
    }

    // ================================================================== geometry

    private sealed record MeshOut(int Id, long Verts, int VertCount, long Tris, int TriCount, long Uvs, List<(int Param, long Verts)> Keys);

    private static (float[] Data, List<DrawRange> Ranges) Tessellate(Element e)
    {
        var g = new GeometryBuilder();
        var r = new List<DrawRange>();
        g.Shape(r, e, false);
        return (g.Data.ToArray(), r);
    }

    /// <summary>
    /// Writes the triangles of <paramref name="shape"/>, with a shape key for each of <paramref name="variants"/>
    /// (the shape with one parameter changed). Vertices at the same place with the same normal are merged, so that the
    /// mesh shades smooth like the original and stays sharp where the original's normals break.
    /// </summary>
    private MeshOut WriteMesh(BinaryWriter bin, int id, Element shape, List<(int Param, Element Variant)> variants)
    {
        const int F = GeometryBuilder.FloatsPerVertex;
        var (data, ranges) = Tessellate(shape);
        var others = variants.Select(v => Tessellate(v.Variant).Data).ToList();
        int raw = data.Length / F;
        if (others.Any(o => o.Length != data.Length)) throw new InvalidOperationException($"{shape.Name}: the shape keys do not match the shape.");

        var weld = new int[raw];
        var index = new Dictionary<(long, long, long, long, long, long, long), int>();
        var positions = new List<int>();
        for (int i = 0; i < raw; i++)
        {
            int o = i * F;
            var n = Vector3.Normalize(new Vector3(data[o + 3], data[o + 4], data[o + 5]));
            if (!float.IsFinite(n.X)) n = Vector3.Zero;
            long h = 17;
            foreach (var other in others)
                h = h * 31 + HashCode.Combine(Q(other[o], 1e5f), Q(other[o + 1], 1e5f), Q(other[o + 2], 1e5f));
            var key = (Q(data[o], 1e5f), Q(data[o + 1], 1e5f), Q(data[o + 2], 1e5f), Q(n.X, 1e3f), Q(n.Y, 1e3f), Q(n.Z, 1e3f), h);
            if (!index.TryGetValue(key, out int w))
            {
                w = positions.Count;
                index[key] = w;
                positions.Add(i);
            }
            weld[i] = w;
        }

        var tris = new List<(int A, int B, int C)>();
        void Tri(int a, int b, int c)
        {
            if (weld[a] == weld[b] || weld[b] == weld[c] || weld[a] == weld[c]) return;
            Vector3 P(int i) => new(data[i * F], data[i * F + 1], data[i * F + 2]);
            Vector3 N(int i) => new(data[i * F + 3], data[i * F + 4], data[i * F + 5]);
            var face = Vector3.Cross(P(b) - P(a), P(c) - P(a));
            if (face.LengthSquared() < 1e-20f && others.Count == 0) return;
            // wind each triangle so that its face normal agrees with the original's vertex normals
            if (Vector3.Dot(face, N(a) + N(b) + N(c)) < 0) (b, c) = (c, b);
            tris.Add((a, b, c));
        }
        GeometryBuilder.Triangles(ranges, Tri);

        long verts = bin.BaseStream.Position;
        foreach (int i in positions) { bin.Write(data[i * F]); bin.Write(data[i * F + 1]); bin.Write(data[i * F + 2]); }
        long triOffset = bin.BaseStream.Position;
        foreach (var (a, b, c) in tris) { bin.Write(weld[a]); bin.Write(weld[b]); bin.Write(weld[c]); }
        long uvs = bin.BaseStream.Position;
        foreach (var (a, b, c) in tris)
            foreach (int i in (int[])[a, b, c]) { bin.Write(data[i * F + 6]); bin.Write(data[i * F + 7]); }
        var keys = new List<(int, long)>();
        for (int v = 0; v < variants.Count; v++)
        {
            keys.Add((variants[v].Param, bin.BaseStream.Position));
            foreach (int i in positions) { bin.Write(others[v][i * F]); bin.Write(others[v][i * F + 1]); bin.Write(others[v][i * F + 2]); }
        }
        return new MeshOut(id, verts, positions.Count, triOffset, tris.Count, uvs, keys);
    }

    private static long Q(float v, float scale) => (long)MathF.Round(v * scale);

    // ================================================================== textures

    /// <summary>Converts a texture like the renderer loads it: 128 x 128 (or the animation's texture size), pure white texels transparent.</summary>
    private static void ConvertTexture(string source, string target, int size)
    {
        using var bitmap = new Bitmap(source);
        using var scaled = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = size > Animation.ClassicTextureSize && (bitmap.Width > size || bitmap.Height > size)
                ? InterpolationMode.HighQualityBilinear : InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(bitmap, new Rectangle(0, 0, size, size));
        }
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var c = scaled.GetPixel(x, y);
                if (c.R == 255 && c.G == 255 && c.B == 255) scaled.SetPixel(x, y, Color.FromArgb(0, 255, 255, 255));
            }
        scaled.Save(target, ImageFormat.Png);
    }

    private string? FindTexture(string name)
    {
        string file = Path.GetFileName(name);
        foreach (var candidate in new[] { name, Path.Combine(_sourceDirectory, file), Path.Combine(AppContext.BaseDirectory, "Scenes", file) })
            if (Path.IsPathRooted(candidate) && File.Exists(candidate)) return candidate;
        return null;
    }

    // ================================================================== export

    public Result Export(string outputDirectory, Action<string>? log = null)
    {
        var clock = Stopwatch.StartNew();
        Directory.CreateDirectory(outputDirectory);
        string name = _animation.Name;
        if (_animation.Scenes.Any(s => s.X != 0 || s.Y != 0 || s.Z != 0 || s.Alpha != 0 || s.Beta != 0 || s.Gamma != 0))
            _warnings.Add("Key states with a transform of their own: that transform is ignored.");

        Sample();
        log?.Invoke($"Sampled {_animation.Scenes.Count} key states: {_order.Count} elements ({clock.ElapsedMilliseconds} ms)");
        int dense = Densify();
        log?.Invoke($"{dense} segments are sampled at every frame ({clock.ElapsedMilliseconds} ms)");

        // textures
        var textures = new List<(string Name, string File)>();
        var textureIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Directory.CreateDirectory(Path.Combine(outputDirectory, "textures"));
        int TextureOf(Element e)
        {
            if (!e.HasTexture) return -1;
            string key = Path.GetFileName(e.Texture!);
            if (textureIndex.TryGetValue(key, out int i)) return i;
            string? source = FindTexture(e.Texture!);
            if (source == null)
            {
                _warnings.Add($"Texture {key} not found.");
                textureIndex[key] = -1;
                return -1;
            }
            string file = "textures/" + Path.GetFileNameWithoutExtension(key) + ".png";
            ConvertTexture(source, Path.Combine(outputDirectory, file), _animation.TextureSize);
            textureIndex[key] = textures.Count;
            textures.Add((key, file));
            return textures.Count - 1;
        }

        string binName = name + ".bin";
        using var binStream = File.Create(Path.Combine(outputDirectory, binName));
        using var bin = new BinaryWriter(binStream);
        string jsonPath = Path.Combine(outputDirectory, name + ".json");
        using var jsonStream = File.Create(jsonPath);
        using var json = new Utf8JsonWriter(jsonStream, new JsonWriterOptions { Indented = false });

        int curves = 0, keyCount = 0;
        var meshes = new List<MeshOut>();
        var meshCache = new Dictionary<string, MeshOut>();

        void WriteCurves(Track track, Func<int, Func<float, float>> mapFor, Func<int, bool>? skip = null)
        {
            json.WriteStartArray("init");
            float[] first = track.Samples.Count > 0 ? track.Samples.First().Value[0] : track.End ?? [];
            for (int c = 0; c < track.Channels.Length && c < first.Length; c++)
            {
                if (skip?.Invoke(c) == true) continue;
                var ch = track.Channels[c];
                json.WriteStartArray();
                json.WriteStringValue(ch.Target); json.WriteStringValue(ch.Path); json.WriteNumberValue(ch.Index);
                json.WriteNumberValue(Finite(mapFor(c)(first[c])));
                json.WriteEndArray();
            }
            json.WriteEndArray();
            json.WriteStartArray("curves");
            for (int c = 0; c < track.Channels.Length && c < first.Length; c++)
            {
                if (skip?.Invoke(c) == true) continue;
                var keys = Keys(track, c, mapFor(c));
                if (keys == null) continue;
                var ch = track.Channels[c];
                json.WriteStartArray();
                json.WriteStringValue(ch.Target); json.WriteStringValue(ch.Path); json.WriteNumberValue(ch.Index);
                json.WriteNumberValue(bin.BaseStream.Position); json.WriteNumberValue(keys.Count);
                json.WriteEndArray();
                foreach (var k in keys)
                {
                    bin.Write(k.Frame); bin.Write(Finite(k.Value)); bin.Write(k.LX); bin.Write(Finite(k.LY));
                    bin.Write(k.RX); bin.Write(Finite(k.RY)); bin.Write((float)k.Interp);
                }
                curves++;
                keyCount += keys.Count;
            }
            json.WriteEndArray();
        }

        static Func<float, float> Same(int _) => v => v;

        json.WriteStartObject();
        json.WriteString("format", "overbryce-blender");
        json.WriteNumber("version", 1);
        json.WriteString("name", name);
        json.WriteString("binary", binName);
        json.WriteNumber("fps", _fps);
        json.WriteNumber("frame_start", 0);
        json.WriteNumber("frame_end", (int)MathF.Round(Frame(_animation.EndTime)));
        json.WriteNumber("clip_start", ViewSetup.Near);
        json.WriteNumber("clip_end", ViewSetup.Far);
        json.WriteNumber("sensor_height", SensorHeight);

        json.WriteStartArray("objects");
        int objectCount = 0;
        foreach (var track in _order)
        {
            track.Index = objectCount++;
            var e = track.First;
            json.WriteStartObject();
            json.WriteString("name", track.Name);
            json.WriteString("kind", track.Kind.ToString().ToLowerInvariant());
            int parent = -1;
            if (track.ParentPath != null && _tracks.TryGetValue(track.ParentPath + "|" + (int)ElementType.Group, out var p)) parent = p.Index;
            json.WriteNumber("parent", parent);
            json.WriteStartArray("visibility");
            foreach (var (frame, visible) in Visibility(track)) { json.WriteNumberValue(frame); json.WriteNumberValue(visible ? 1 : 0); }
            json.WriteEndArray();

            switch (track.Kind)
            {
                case Kind.Group:
                {
                    bool offset = AllValues(track).Any(v => v[0] != 0 || v[1] != 0 || v[2] != 0);
                    json.WriteBoolean("inner", offset);
                    json.WriteString("rotation_mode", "ZYX");
                    WriteCurves(track, Same, c => !offset && c < 3);
                    break;
                }
                case Kind.Camera:
                    json.WriteString("rotation_mode", "ZXY");
                    var (near, far) = ViewSetup.Clip((CameraElement)e);
                    json.WriteStartArray("clip");
                    json.WriteNumberValue(near); json.WriteNumberValue(far);
                    json.WriteEndArray();
                    WriteCurves(track, Same);
                    break;
                case Kind.Light:
                {
                    var l = (LightElement)e;
                    var all = AllValues(track).ToList();
                    float m = all.Max(v => MathF.Max(v[LightColor], MathF.Max(v[LightColor + 1], v[LightColor + 2])));
                    m = MathF.Max(m, 1e-6f);
                    double c0 = Math.Max(l.ConstantAttenuation, 1e-3), l1 = l.LinearAttenuation, q2 = l.QuadraticAttenuation;
                    // Blender: pixel = albedo * power / (4 pi^2 (d^2 + R^2)) for a light of radius R with soft falloff
                    // (R = 0: the physical 1 / d^2); the original: albedo * colour / (c + l d + q d^2)
                    double k, radius = 0;
                    bool fades = l1 > 0 || q2 > 0, spot = l.SpotCutoff <= 90;
                    // where the original's light is half as bright as at the light
                    double half = !fades ? 0 : q2 > 0 ? (-l1 + Math.Sqrt(l1 * l1 + 4 * q2 * c0)) / (2 * q2) : c0 / l1;
                    if (fades && !spot)
                    {
                        // a point light that fades: K / (d^2 + R^2), exact without the linear term, else equal at d = 0
                        // and where it is half as bright (a spot gets no radius: EEVEE and Cycles shape a wide one differently)
                        radius = l1 == 0 ? Math.Sqrt(c0 / q2) : half;
                        k = l1 == 0 ? 1 / q2 : radius * radius / c0;
                    }
                    else
                    {
                        // a spot, or a light that does not fade: EEVEE is right at a reference distance (a spot: where it
                        // lights the ground, a light that does not fade: its usual distance from the camera); in Cycles a
                        // light node gives the original attenuation everywhere
                        double dref = fades ? Math.Min(half, SpotReference) : ReferenceDistance(track);
                        double attRef = 1 / (c0 + l1 * dref + q2 * dref * dref);
                        k = attRef * dref * dref;
                        json.WriteStartArray("falloff");
                        json.WriteNumberValue(c0); json.WriteNumberValue(l1); json.WriteNumberValue(q2); json.WriteNumberValue(k);
                        json.WriteEndArray();
                    }
                    float energy = (float)(4 * Math.PI * Math.PI * m * k);
                    json.WriteString("light", l.SpotCutoff > 90 ? "POINT" : "SPOT");
                    json.WriteNumber("radius", radius);
                    if (all.Any(v => v[LightEnergy] < 0.5f)) _warnings.Add($"Light {track.Name} is switched off in some key states.");
                    WriteCurves(track, c => c switch
                    {
                        >= LightColor and < LightColor + 3 => v => v / m,
                        LightSpecular => v => v / m,
                        LightEnergy => v => v * energy,
                        _ => v => v,
                    });
                    break;
                }
                default:
                {
                    json.WriteString("rotation_mode", "ZYX");
                    int tex = TextureOf(e);
                    json.WriteNumber("texture", tex);
                    json.WriteBoolean("spheremap", e is TorusElement);
                    json.WriteBoolean("wire", e.DrawStyle == DrawStyle.Line);
                    // see-through at some time: blended rather than dithered transparency in EEVEE
                    json.WriteBoolean("translucent", AllValues(track).Any(v => v[Transform.Length + 3] < 0.999f));
                    int shapeStart = Transform.Length + MaterialChannels.Length;
                    var values = AllValues(track).ToList();
                    float[] p0 = values[0][shapeStart..];
                    var ranges = new float[p0.Length];
                    for (int i = 0; i < p0.Length; i++)
                    {
                        float lo = values.Min(v => v[shapeStart + i]), hi = values.Max(v => v[shapeStart + i]);
                        ranges[i] = hi - lo > 1e-6f * (1 + MathF.Abs(p0[i])) ? MathF.Max(MathF.Abs(hi - p0[i]), MathF.Abs(lo - p0[i])) : 0;
                    }
                    var basis = e.Clone();
                    for (int i = 0; i < p0.Length; i++) SetShapeParam(basis, i, p0[i]);
                    var variants = new List<(int, Element)>();
                    for (int i = 0; i < p0.Length; i++)
                        if (ranges[i] > 0)
                        {
                            var v = basis.Clone();
                            SetShapeParam(v, i, p0[i] + ranges[i]);
                            variants.Add((i, v));
                        }
                    MeshOut mesh;
                    string cacheKey = variants.Count == 0 ? $"{e.Type}|{string.Join(",", p0)}|{Quality(e)}" : "";
                    if (variants.Count == 0 && meshCache.TryGetValue(cacheKey, out var cached)) mesh = cached;
                    else
                    {
                        mesh = WriteMesh(bin, meshes.Count, basis, variants);
                        meshes.Add(mesh);
                        if (variants.Count == 0) meshCache[cacheKey] = mesh;
                    }
                    json.WriteNumber("mesh", mesh.Id);
                    WriteCurves(track, c => c >= shapeStart ? (ranges[c - shapeStart] > 0 ? v => (v - p0[c - shapeStart]) / ranges[c - shapeStart] : v => 0) : v => v,
                        c => c >= shapeStart && ranges[c - shapeStart] == 0);
                    break;
                }
            }
            json.WriteEndObject();
        }
        json.WriteEndArray();

        json.WriteStartObject("scene");
        WriteCurves(_scene, Same);
        json.WriteEndObject();

        json.WriteStartArray("markers");
        string? current = "\0";
        foreach (var (state, camera) in _cameras)
        {
            if (camera == current) continue;
            current = camera;
            int index = camera != null && _tracks.TryGetValue(camera, out var cam) ? cam.Index : -1;
            json.WriteNumberValue(MathF.Ceiling(Frame(_times[state]) - 1e-3f));
            json.WriteNumberValue(index);
        }
        json.WriteEndArray();

        json.WriteStartArray("meshes");
        foreach (var m in meshes)
        {
            json.WriteStartObject();
            json.WriteNumber("verts", m.Verts); json.WriteNumber("vert_count", m.VertCount);
            json.WriteNumber("tris", m.Tris); json.WriteNumber("tri_count", m.TriCount);
            json.WriteNumber("uvs", m.Uvs);
            json.WriteStartArray("keys");
            foreach (var (param, offset) in m.Keys) { json.WriteNumberValue(param); json.WriteNumberValue(offset); }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        json.WriteEndArray();

        json.WriteStartArray("textures");
        foreach (var (texName, file) in textures) json.WriteStringValue(file);
        json.WriteEndArray();

        json.WriteStartArray("warnings");
        foreach (var w in _warnings.Distinct()) json.WriteStringValue(w);
        json.WriteEndArray();
        json.WriteEndObject();
        json.Flush();

        WriteScript(outputDirectory);
        log?.Invoke($"Wrote {jsonPath}: {objectCount} objects, {meshes.Count} meshes, {curves} animation curves with {keyCount} keys ({clock.ElapsedMilliseconds} ms)");
        return new Result(jsonPath, objectCount, meshes.Count, curves, keyCount, dense, _warnings.Distinct().ToList());
    }

    private static float Finite(float v) => float.IsFinite(v) ? v : 0;

    private static string Quality(Element e) => e switch
    {
        SphereElement s => $"{s.QualityX}x{s.QualityY}",
        ConeElement c => $"{c.QualityX}x{c.QualityY}",
        DiskElement d => $"{d.QualityX}x{d.QualityY}x{d.SweepAngle}",
        TorusElement t => $"{t.QualityX}x{t.QualityY}",
        MeshElement m => $"{m.CountX}x{m.CountY}x{m.QualityX}x{m.QualityY}",
        PolyMeshElement p => p.MeshId ?? "",
        _ => "",
    };

    private static IEnumerable<float[]> AllValues(Track track)
    {
        foreach (var s in track.Samples.Values) foreach (var v in s) yield return v;
        foreach (var d in track.Dense.Values) foreach (var (_, v) in d) yield return v;
        if (track.End != null) yield return track.End;
    }

    /// <summary>
    /// For a light that does not fade: its median distance from the camera, where EEVEE (which can only make it fade
    /// physically) gets its brightness right.
    /// </summary>
    private static float ReferenceDistance(Track track)
    {
        if (track.CameraDistances.Count == 0) return 10;
        var sorted = track.CameraDistances.Order().ToList();
        return MathF.Max(sorted[sorted.Count / 2], 0.05f);
    }

    private static void WriteScript(string outputDirectory)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ScriptName)
                           ?? throw new InvalidOperationException("The Blender script is missing from the program.");
        using var file = File.Create(Path.Combine(outputDirectory, ScriptName));
        stream.CopyTo(file);
    }

    // ================================================================== Blender

    /// <summary>Blender's executable: the BLENDER environment variable, or the newest one in Program Files.</summary>
    public static string? FindBlender()
    {
        if (Environment.GetEnvironmentVariable("BLENDER") is { } env && File.Exists(env)) return env;
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"C:\Program Files" })
        {
            string dir = Path.Combine(root, "Blender Foundation");
            if (!Directory.Exists(dir)) continue;
            var found = Directory.GetDirectories(dir)
                .Select(d => Path.Combine(d, "blender.exe"))
                .Where(File.Exists)
                .OrderByDescending(p => Version.TryParse(new string(Path.GetFileName(Path.GetDirectoryName(p))!.Where(ch => char.IsDigit(ch) || ch == '.').ToArray()), out var v) ? v : new Version(0, 0))
                .FirstOrDefault();
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>Runs Blender in the background to build the .blend file; returns its exit code.</summary>
    public static int BuildBlend(string blender, string jsonPath, string blendPath, Action<string>? log = null)
    {
        string script = Path.Combine(Path.GetDirectoryName(jsonPath)!, ScriptName);
        var info = new ProcessStartInfo(blender)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var arg in new[] { "-b", "--factory-startup", "--python", script, "--", jsonPath, blendPath }) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        string logPath = Path.ChangeExtension(blendPath, ".log");
        using var writer = new StreamWriter(logPath);
        void Line(string? s)
        {
            if (s == null) return;
            lock (writer) writer.WriteLine(s);
            if (s.StartsWith("Overbryce:", StringComparison.Ordinal) || s.Contains("Error", StringComparison.Ordinal)) log?.Invoke(s);
        }
        process.OutputDataReceived += (_, a) => Line(a.Data);
        process.ErrorDataReceived += (_, a) => Line(a.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        return process.ExitCode;
    }

    /// <summary>Command line: Overbryce.exe file.ani --blender folder [--no-blend].</summary>
    public static int Run(string aniPath, string outputDirectory, bool buildBlend, Action<string> log)
    {
        var animation = AniFile.Load(aniPath);
        if (animation.Scenes.Count == 0) { log("The animation has no key states."); return 1; }
        var result = new BlenderExporter(animation, aniPath).Export(outputDirectory, log);
        foreach (var w in result.Warnings) log("Warning: " + w);
        if (!buildBlend) return 0;
        var blender = FindBlender();
        if (blender == null)
        {
            log($"Blender was not found. Build the .blend file with: blender -b --python {ScriptName} -- {Path.GetFileName(result.JsonPath)} {animation.Name}.blend");
            return 0;
        }
        string blend = Path.Combine(outputDirectory, animation.Name + ".blend");
        log($"Building {blend} with {blender} ...");
        int code = BuildBlend(blender, result.JsonPath, blend, log);
        log(code == 0 && File.Exists(blend) ? $"Done: {blend}" : $"Blender failed (exit code {code}), see {Path.ChangeExtension(blend, ".log")}");
        return code;
    }
}
