using System.Globalization;
using System.Numerics;
using Overbryce.Core.Model;
using static Overbryce.Core.Import.BlenderConversions;

namespace Overbryce.Core.Import;

/// <summary>Which way is up in the Blender scene.</summary>
public enum ImportAxis
{
    /// <summary>Scenes exported by Overbryce are taken 1:1, all others are turned from Z up to Y up.</summary>
    Auto,

    /// <summary>Blender's coordinates as they are (as the Overbryce export writes them).</summary>
    YUp,

    /// <summary>Blender's Z up becomes Overbryce's Y up: (x, y, z) → (x, z, -y).</summary>
    ZUp,
}

public sealed class BlenderImportOptions
{
    public ImportAxis Axis { get; set; } = ImportAxis.Auto;

    /// <summary>Only the first frame: one key state.</summary>
    public bool Static { get; set; }

    /// <summary>The animation's name (default: the Blender scene's).</summary>
    public string? Name { get; set; }

    /// <summary>At most this many instances (geometry nodes, particles, collection instances) are imported.</summary>
    public int MaxInstances { get; set; } = 4000;

    /// <summary>
    /// How far the animation between key states may stray from Blender's, as a part of the scene's size (0.001: a
    /// millimetre in a scene of a metre). Key states are added where it strays further. 0: every frame is a key state.
    /// </summary>
    public double Tolerance { get; set; } = 0.001;

    /// <summary>Plain boxes and rectangles become Overbryce's own shapes (off: every mesh is a polygon mesh).</summary>
    public bool RecognizeShapes { get; set; } = true;
}

/// <summary>
/// Builds an Overbryce animation from a Blender scene read by overbryce_import.py (<see cref="BlenderSceneFile"/>).
/// <list type="bullet">
/// <item>Objects keep their hierarchy: an object with children (an empty, an armature, a mesh with children) becomes
/// a group, which holds the object's own geometry as an element.</item>
/// <item>Every placement is split into a rotation and a stretch: the rotation and position become the element's,
/// the stretch (scale and shear) is baked into its polygon mesh. A stretch that changes over time and every other
/// change of shape (modifiers, shape keys, soft rigs) morphs the mesh between key states.</item>
/// <item>Each material slot of an object is a polygon mesh of its own; the Principled BSDF becomes Phong colours plus
/// the modern values (reflectivity, refractive index, glow, roughness).</item>
/// <item>Lights and cameras sit directly in the key state, at their place in the world.</item>
/// </list>
/// </summary>
public sealed partial class BlenderSceneImporter
{
    private readonly BlenderSceneFile _file;
    private readonly BlenderImportOptions _options;
    private readonly ImportReport _report = new();
    private readonly int _frames;
    private readonly Mat3 _axis;
    private readonly Node[] _nodes;
    private readonly List<Out> _roots = [];
    private readonly List<Out> _lights = [];
    private readonly List<Out> _cameras = [];
    private readonly Dictionary<int, List<Part>> _parts = [];
    private readonly Dictionary<(Part, string), PolyMeshData> _shared = [];
    private Vec3d _center;
    private double _radius = 10;
    private (double R, double G, double B) _world;
    private readonly bool _agx;
    private string? _sky;

    private BlenderSceneImporter(BlenderSceneFile file, BlenderImportOptions options)
    {
        _file = file;
        _options = options;
        _agx = file.Scene.ViewTransform is "AgX" or "Filmic";
        _frames = Math.Max(1, file.FrameCount);
        bool overbryce = file.Objects.Any(o => o.Props?.ContainsKey("ob_dif") == true || o.Overbryce?.Count > 0);
        bool zUp = options.Axis == ImportAxis.ZUp || (options.Axis == ImportAxis.Auto && !overbryce);
        _axis = zUp ? Mat3.RotationX(-Math.PI / 2) : Mat3.Identity;
        _report.Note($"Blender scene \"{file.Scene.Name}\" ({file.Scene.Blender}), {_frames} frame{(_frames == 1 ? "" : "s")} sampled ({FirstFrame}..{LastFrame} at {file.Scene.Fps.ToString("0.##", CultureInfo.InvariantCulture)} fps).");
        _report.Note(zUp ? "Axes: Blender's Z up became Overbryce's Y up." : "Axes: taken 1:1 (a scene exported by Overbryce).");
        _nodes = file.Objects.Select((o, i) => new Node(i, o)).ToArray();
    }

    private double FirstFrame => _file.Frames.Length > 0 ? _file.Frames[0] : _file.Scene.FrameStart;
    private double LastFrame => _file.Frames.Length > 0 ? _file.Frames[^1] : FirstFrame;

    /// <summary>
    /// The imported animation, what was approximated or left out, and how the colours of the textures change to look
    /// as in Blender (null: they stay as they are).
    /// </summary>
    public sealed record Result(Animation Animation, ImportReport Report, Func<double, double, double, (double R, double G, double B)>? TextureColors);

    /// <summary>Imports the scene: the animation and what was approximated or left out.</summary>
    public static Result Import(BlenderSceneFile file, BlenderImportOptions? options = null)
    {
        var importer = new BlenderSceneImporter(file, options ?? new BlenderImportOptions());
        return importer.Run();
    }

    private Result Run()
    {
        foreach (var note in _file.Notes) _report.AddScriptNote(note);
        LinkNodes();
        if (!_options.Static) ReadTags();
        SampleFrames();
        BuildParts();
        DecideKept();
        ComputeBounds();
        Calibrate();
        _world = WorldAmbient();
        BuildOutputTree();
        SampleOutputs();

        Animation animation;
        if (Restoring)
            animation = RestoredAnimation();
        else
        {
            animation = new Animation(_options.Name ?? _file.Scene.Name);
            ApplySettings(animation);
            foreach (int f in KeyFrames())
                animation.Scenes.Add(State(f));
        }
        ReportSummary(animation);
        Func<double, double, double, (double, double, double)>? textures = null;
        if (_agx && !Restoring)
        {
            double light = _typical;
            textures = (r, g, b) => SurfaceColor([FromSrgb(r), FromSrgb(g), FromSrgb(b)], true, light);
        }
        return new Result(animation, _report, textures);
    }

    // ================================================================== light calibration

    private double _lightScale = 1;
    private double _typical = 1;

    /// <summary>
    /// Blender adds the light of all lights before the view transform, Overbryce adds each light's display value.
    /// Where lights overlap Overbryce gets too bright; where one light dominates it is right. This finds the single
    /// factor for all lights that matches Blender best on the scene's own surfaces (vertices of the first frame,
    /// white, unshadowed), and the typical light on them (for AgX, whose colours depend on the brightness).
    /// </summary>
    private void Calibrate()
    {
        var points = new List<(Vec3d P, Vec3d N)>();
        var geometry = _nodes.Where(n => n.Kept && n.Kind == NodeKind.Geometry && n.Present(0)).ToList();
        long total = geometry.Sum(n => (long)n.Parts.Sum(p => p.Local.VertexCount));
        int step = (int)Math.Max(1, total / 4000);
        long counter = 0;
        foreach (var n in geometry)
            foreach (var part in n.Parts)
            {
                var mesh = MeshAt(n, part, 0);
                var frame = n.Frame[0];
                for (int i = 0; i < mesh.VertexCount; i++)
                {
                    if (counter++ % step != 0) continue;
                    var p = new Vec3d(mesh.Positions[i * 3], mesh.Positions[i * 3 + 1], mesh.Positions[i * 3 + 2]);
                    var nn = new Vec3d(mesh.Normals[i * 3], mesh.Normals[i * 3 + 1], mesh.Normals[i * 3 + 2]);
                    points.Add((frame.R * p + frame.T, (frame.R * nn).Normalized));
                }
            }
        var lights = _nodes.Where(n => n.Kept && n.Kind == NodeKind.Light && n.Present(0) && n.Data != null && n.Info.DataWidth >= 13).ToList();
        if (points.Count == 0 || lights.Count == 0) return;

        var w = _file.World;
        double world = Luminance(w.Color[0], w.Color[1], w.Color[2]) * w.Strength;
        double ambient = View(world, _agx);
        double so = 0, oo = 0;
        var totals = new List<double>();
        foreach (var (p, normal) in points)
        {
            double linear = world, shown = 0;
            foreach (var n in lights)
            {
                var d = n.Data!;
                int o = 0;
                var type = (LightType)(int)d[o];
                double power = Luminance(d[o + 1], d[o + 2], d[o + 3]) * d[o + 4];
                var dir = (n.Frame[0].R * new Vec3d(0, 0, -1)).Normalized;
                double e, shownE;
                if (type == LightType.Sun)
                {
                    double cos = Math.Max(0, Vec3d.Dot(normal, -dir));
                    e = power / Math.PI * cos;
                    shownE = Display(power / Math.PI) * cos;
                }
                else
                {
                    var v = n.Frame[0].T - p;
                    double dist = v.Length;
                    if (dist < 1e-6) continue;
                    var l = v * (1 / dist);
                    double cos = Math.Max(0, Vec3d.Dot(normal, l));
                    double mask = 1, k = PointFactor;
                    double radius = d[o + 7];
                    if (type == LightType.Spot)
                        mask = Vec3d.Dot(-l, dir) >= Math.Cos(Math.Min(Math.PI / 2, d[o + 5] / 2)) ? 1 : 0;
                    else if (type == LightType.Area)
                    {
                        mask = Math.Max(0, Vec3d.Dot(-l, dir));
                        k = AreaFactor;
                        radius = Math.Sqrt(Math.Max(d[o + 8] * (d[o + 9] > 0 ? d[o + 9] : d[o + 8]), 0)) / 2;
                    }
                    double falloff = 1 / (dist * dist + radius * radius);
                    e = power * k * falloff * cos * mask;
                    shownE = Display(power * k) * Display(falloff) * cos * mask;
                }
                linear += e;
                shown += shownE;
            }
            totals.Add(linear);
            if (shown <= 0) continue;
            so += shown * (View(linear, _agx) - ambient);
            oo += shown * shown;
        }
        if (oo > 0) _lightScale = Math.Clamp(so / oo, 0.2, 2);
        totals.Sort();
        _typical = Math.Max(totals[totals.Count / 2], 1e-3);
        _report.Note($"Lights scaled by {_lightScale.ToString("0.00", CultureInfo.InvariantCulture)} to match Blender's brightness on the scene's surfaces (typical light {_typical.ToString("0.00", CultureInfo.InvariantCulture)}).");
    }

    /// <summary>The sampled frames (indices) that become key states.</summary>
    private IEnumerable<int> KeyFrames()
    {
        if (_options.Static || _frames == 1) return [0];
        if (_options.Tolerance <= 0) return Enumerable.Range(0, _frames);
        return FitKeyFrames();
    }

    private float TimeOf(int frameIndex) =>
        (float)((_file.Frames.Length > 0 ? _file.Frames[frameIndex] - _file.Frames[0] : 0) / Math.Max(_file.Scene.Fps, 1e-3));

    // ================================================================== the Blender objects

    private enum NodeKind { Empty, Geometry, Light, Camera }

    private sealed class Node(int index, BlenderSceneFile.ObjectInfo info)
    {
        public int Index { get; } = index;
        public BlenderSceneFile.ObjectInfo Info { get; } = info;
        public Node? Parent { get; set; }
        public List<Node> Children { get; } = [];
        public NodeKind Kind { get; set; }
        public bool Kept { get; set; }
        public RigidFrame[] Frame { get; set; } = [];
        public Mat3[] Stretch { get; set; } = [];
        public bool[] Valid { get; set; } = [];
        public bool[] Visible { get; set; } = [];
        public int Reference { get; set; } = -1;
        public bool StretchVaries { get; set; }
        public List<Part> Parts { get; set; } = [];
        public float[]? Data { get; set; }
        public Dictionary<(Part, int), PolyMeshData> Meshes { get; } = [];
        public bool Present(int f) => Valid[f] && Visible[f];
        public bool Deforms => Info.Deform != null;
    }

    /// <summary>One material slot of a Blender mesh: a polygon mesh in the object's own (unstretched) space.</summary>
    private sealed class Part(int slot, int material, PolyMeshData local, int[] source)
    {
        public int Slot { get; } = slot;
        public int Material { get; } = material;
        public PolyMeshData Local { get; } = local;

        /// <summary>The Blender vertex of each vertex (to follow the mesh's deformation).</summary>
        public int[] Source { get; } = source;
    }

    private void LinkNodes()
    {
        int instances = 0;
        foreach (var n in _nodes)
        {
            n.Kind = n.Info.Kind switch
            {
                "LIGHT" => NodeKind.Light,
                "CAMERA" => NodeKind.Camera,
                _ => n.Info.Mesh >= 0 ? NodeKind.Geometry : NodeKind.Empty,
            };
            if (n.Info.Instance && ++instances > _options.MaxInstances) continue;
            if (n.Info.Parent >= 0 && n.Info.Parent < _nodes.Length && n.Info.Parent != n.Index)
            {
                n.Parent = _nodes[n.Info.Parent];
                n.Parent.Children.Add(n);
            }
        }
        if (instances > _options.MaxInstances)
            _report.Drop($"{instances - _options.MaxInstances} of {instances} instances (more than {_options.MaxInstances})");
    }

    private void SampleFrames()
    {
        int max = _options.MaxInstances, instances = 0;
        foreach (var n in _nodes)
        {
            var m = _file.Floats(n.Info.Matrices, _frames * 16);
            var visible = _file.Bytes(n.Info.Visible, _frames);
            n.Frame = new RigidFrame[_frames];
            n.Stretch = new Mat3[_frames];
            n.Valid = new bool[_frames];
            n.Visible = new bool[_frames];
            bool skip = n.Info.Instance && ++instances > max;
            for (int f = 0; f < _frames; f++)
            {
                int o = f * 16;
                bool valid = !skip && float.IsFinite(m[o]) && float.IsFinite(m[o + 15]);
                n.Valid[f] = valid;
                n.Visible[f] = valid && visible[f] != 0;
                if (!valid) continue;
                var a = _axis * new Mat3(m[o], m[o + 1], m[o + 2], m[o + 4], m[o + 5], m[o + 6], m[o + 8], m[o + 9], m[o + 10]);
                var t = _axis * new Vec3d(m[o + 3], m[o + 7], m[o + 11]);
                var (r, s) = a.Polar();
                n.Frame[f] = new RigidFrame(r, t);
                n.Stretch[f] = s;
                if (n.Reference < 0 && n.Visible[f]) n.Reference = f;
            }
            FoldRigidDeformation(n);
            if (n.Reference < 0) n.Reference = Array.IndexOf(n.Valid, true);
            if (n.Reference >= 0)
            {
                var s0 = n.Stretch[n.Reference];
                double scale = Math.Max(1e-6, Math.Abs(s0.M00) + Math.Abs(s0.M11) + Math.Abs(s0.M22));
                for (int f = 0; f < _frames && !n.StretchVaries; f++)
                    if (n.Valid[f] && Mat3.Distance(n.Stretch[f], s0) > 1e-4 * scale) n.StretchVaries = true;
            }
            if (n.Info.Data >= 0 && n.Info.DataWidth > 0)
                n.Data = _file.Floats(n.Info.Data, _frames * n.Info.DataWidth);
        }
    }

    // ================================================================== meshes

    private void BuildParts()
    {
        foreach (var n in _nodes)
        {
            if (n.Kind != NodeKind.Geometry || n.Reference < 0) continue;
            if (!_parts.TryGetValue(n.Info.Mesh, out var parts))
            {
                parts = n.Info.Mesh < _file.Meshes.Count ? MakeParts(_file.Meshes[n.Info.Mesh]) : [];
                _parts[n.Info.Mesh] = parts;
            }
            n.Parts = parts;
            if (parts.Count == 0) n.Kind = NodeKind.Empty;
        }
    }

    /// <summary>Splits a Blender mesh by material slot into polygon meshes.</summary>
    private List<Part> MakeParts(BlenderSceneFile.MeshInfo m)
    {
        var positions = _file.Floats(m.Positions, m.VertexCount * 3);
        var cornerVertex = _file.Ints(m.CornerVertex, m.CornerCount);
        var cornerNormal = _file.Floats(m.CornerNormal, m.CornerCount * 3);
        var cornerUv = m.CornerUv >= 0 ? _file.Floats(m.CornerUv, m.CornerCount * 2) : null;
        var triangles = _file.Ints(m.Triangles, m.TriangleCount * 3);
        var slots = _file.Ints(m.TriangleSlot, m.TriangleCount);
        var parts = new List<Part>();
        int slotCount = Math.Max(1, m.Slots.Length);
        for (int slot = 0; slot < slotCount; slot++)
        {
            var map = new Dictionary<int, int>();
            var indices = new List<int>();
            for (int t = 0; t < m.TriangleCount; t++)
            {
                if (Math.Clamp(slots[t], 0, slotCount - 1) != slot) continue;
                for (int k = 0; k < 3; k++)
                {
                    int c = triangles[t * 3 + k];
                    if (!map.TryGetValue(c, out int v)) map[c] = v = map.Count;
                    indices.Add(v);
                }
            }
            if (indices.Count == 0) continue;
            int count = map.Count;
            var p = new float[count * 3];
            var nrm = new float[count * 3];
            var uv = cornerUv != null ? new float[count * 2] : null;
            var source = new int[count];
            var groups = new int[count];
            var groupIds = new Dictionary<(int, int, int, int), int>();
            int material = slot < m.Slots.Length ? m.Slots[slot] : -1;
            var uvTransform = material >= 0 && material < _file.Materials.Count ? _file.Materials[material].Uv : null;
            foreach (var (c, v) in map)
            {
                int sv = cornerVertex[c];
                source[v] = sv;
                for (int k = 0; k < 3; k++)
                {
                    p[v * 3 + k] = positions[sv * 3 + k];
                    nrm[v * 3 + k] = cornerNormal[c * 3 + k];
                }
                if (uv != null)
                {
                    var (u, w) = ApplyUv(uvTransform, cornerUv![c * 2], cornerUv[c * 2 + 1]);
                    uv[v * 2] = u;
                    uv[v * 2 + 1] = w;
                }
                // vertices of one Blender vertex with the same normal stay smooth when the mesh deforms
                var key = (sv, (int)MathF.Round(nrm[v * 3] * 1000), (int)MathF.Round(nrm[v * 3 + 1] * 1000), (int)MathF.Round(nrm[v * 3 + 2] * 1000));
                if (!groupIds.TryGetValue(key, out int g)) groupIds[key] = g = groupIds.Count;
                groups[v] = g;
            }
            parts.Add(new Part(slot, material, new PolyMeshData(p, nrm, uv, indices.ToArray(), groups), source));
        }
        return parts;
    }

    /// <summary>A Mapping node's transform of texture coordinates (Point: scale, rotate, move; Texture: the inverse).</summary>
    private static (float U, float V) ApplyUv(BlenderSceneFile.UvTransform? t, float u, float v)
    {
        if (t == null) return (u, v);
        double sx = t.Scale.Length > 0 ? t.Scale[0] : 1, sy = t.Scale.Length > 1 ? t.Scale[1] : 1;
        double lx = t.Location.Length > 0 ? t.Location[0] : 0, ly = t.Location.Length > 1 ? t.Location[1] : 0;
        double c = Math.Cos(t.Rotation), s = Math.Sin(t.Rotation);
        double x = u, y = v;
        switch (t.Type)
        {
            case "TEXTURE":
                x -= lx; y -= ly;
                (x, y) = (c * x + s * y, -s * x + c * y);
                x = sx != 0 ? x / sx : x;
                y = sy != 0 ? y / sy : y;
                break;
            case "VECTOR":
                x *= sx; y *= sy;
                (x, y) = (c * x - s * y, s * x + c * y);
                break;
            default:
                x *= sx; y *= sy;
                (x, y) = (c * x - s * y, s * x + c * y);
                x += lx; y += ly;
                break;
        }
        return ((float)x, (float)y);
    }

    /// <summary>A part of a node in frame <paramref name="f"/>: deformed and stretched like the Blender object.</summary>
    private PolyMeshData MeshAt(Node n, Part part, int f)
    {
        if (!n.Valid[f]) f = n.Reference;
        bool deformed = n.Deforms && n.Info.Deform!.Length > f && n.Info.Deform[f] >= 0;
        if (!deformed && !n.StretchVaries) f = n.Reference;
        if (n.Meshes.TryGetValue((part, f), out var cached)) return cached;
        var local = part.Local;
        if (deformed)
        {
            var co = _file.Floats(n.Info.Deform![f], _file.Meshes[n.Info.Mesh].VertexCount * 3);
            var p = new float[part.Source.Length * 3];
            for (int i = 0; i < part.Source.Length; i++)
            {
                int s = part.Source[i] * 3;
                p[i * 3] = co[s]; p[i * 3 + 1] = co[s + 1]; p[i * 3 + 2] = co[s + 2];
            }
            local = local.WithPositions(p);
        }
        var stretch = n.Stretch[f];
        PolyMeshData mesh;
        if (!deformed)
        {
            // objects with the same mesh and the same stretch share the polygon mesh (instances, linked copies)
            string key = string.Join(",", Enumerable.Range(0, 9).Select(i => Math.Round(stretch[i / 3, i % 3], 5).ToString(CultureInfo.InvariantCulture)));
            if (_shared.TryGetValue((part, key), out var found)) mesh = found;
            else _shared[(part, key)] = mesh = IsIdentity(stretch) ? local : local.Transform(stretch.ToNumerics());
        }
        else mesh = IsIdentity(stretch) ? local : local.Transform(stretch.ToNumerics());
        n.Meshes[(part, f)] = mesh;
        return mesh;
    }

    private static bool IsIdentity(Mat3 m) => Mat3.Distance(m, Mat3.Identity) < 1e-6;

    // ================================================================== what is kept

    private void DecideKept()
    {
        int emptyDropped = 0;
        bool Keep(Node n)
        {
            bool any = false;
            foreach (var c in n.Children) any |= Keep(c);
            if (n.Reference < 0)
            {
                n.Kept = false;
                return false;
            }
            // restored elements (and the helpers of the export) are not made from the Blender object
            if (_tags.ContainsKey(n))
            {
                n.Kept = false;
                return false;
            }
            n.Kept = any || n.Kind is NodeKind.Geometry or NodeKind.Light or NodeKind.Camera;
            if (!n.Kept && !n.Info.Instance && n.Info.Kind is "EMPTY" or "ARMATURE") emptyDropped++;
            return n.Kept;
        }
        foreach (var n in _nodes.Where(n => n.Parent == null)) Keep(n);
        if (emptyDropped > 0) _report.Note($"{emptyDropped} empties or armatures without anything under them were left out (they do not render).");
        foreach (var n in _nodes.Where(n => n.Reference < 0 && n.Kind == NodeKind.Geometry && !n.Info.Instance))
            _report.Drop($"{n.Info.Name}: never rendered in the sampled frames");
    }

    private void ComputeBounds()
    {
        var min = new Vec3d(double.MaxValue, double.MaxValue, double.MaxValue);
        var max = -min;
        bool any = false;
        // the space the scene takes over the whole animation: nine frames from the first to the last
        var frames = Enumerable.Range(0, 9).Select(i => i * (_frames - 1) / 8).Distinct().ToList();
        foreach (var n in _nodes)
        {
            if (!n.Kept || n.Kind != NodeKind.Geometry) continue;
            foreach (int f in frames.Where(f => n.Present(f)).DefaultIfEmpty(n.Reference))
                foreach (var part in n.Parts)
                {
                    var (lo, hi) = (n.Deforms ? MeshAt(n, part, f) : part.Local).Bounds;
                    var frame = n.Frame[f];
                    var s = n.Deforms ? Mat3.Identity : n.Stretch[f];
                    for (int k = 0; k < 8; k++)
                    {
                        var corner = new Vec3d((k & 1) != 0 ? hi.X : lo.X, (k & 2) != 0 ? hi.Y : lo.Y, (k & 4) != 0 ? hi.Z : lo.Z);
                        var w = frame.R * (s * corner) + frame.T;
                        if (!double.IsFinite(w.X + w.Y + w.Z)) continue;
                        min = new Vec3d(Math.Min(min.X, w.X), Math.Min(min.Y, w.Y), Math.Min(min.Z, w.Z));
                        max = new Vec3d(Math.Max(max.X, w.X), Math.Max(max.Y, w.Y), Math.Max(max.Z, w.Z));
                        any = true;
                    }
                }
        }
        if (!any) return;
        _center = (min + max) * 0.5;
        _radius = Math.Max((max - min).Length / 2, 1e-3);
    }

    // ================================================================== the Overbryce elements

    private enum OutKind { Group, Mesh, Light, Camera }

    /// <summary>An element of the key states: what it is made of and its values in every sampled frame.</summary>
    private sealed class Out(OutKind kind, Node node, Part? part, Out? parent)
    {
        public OutKind Kind { get; } = kind;
        public Node Node { get; } = node;
        public Part? Part { get; } = part;
        public Out? Parent { get; } = parent;
        public List<Out> Children { get; } = [];
        public string Name { get; set; } = "";

        /// <summary>The element sits in its node's group with no placement of its own.</summary>
        public bool Inside { get; init; }

        public Vec3d[] Position { get; set; } = [];
        public Vec3d[] Angles { get; set; } = [];
        public LightValues[]? Light { get; set; }

        public bool Present(int f) => Kind switch
        {
            OutKind.Group => Node.Valid[f] && Children.Any(c => c.Present(f)),
            OutKind.Camera => Node.Valid[f],
            _ => Node.Present(f),
        };
    }

    private void BuildOutputTree()
    {
        // an object whose parent is not imported (a restored element of an Overbryce export) sits at the top
        static bool KeptAbove(Node n)
        {
            for (var p = n.Parent; p != null; p = p.Parent)
                if (p.Kept) return true;
            return false;
        }
        foreach (var n in _nodes.Where(n => n.Kept && !KeptAbove(n))) Place(n, null, _roots);
        var top = _lights.Concat(_cameras).Concat(_roots).ToList();
        Name(top, Restoring ? RestoredTopNames() : null);
        var w = _file.World;
        if (!Restoring && w.Color.Length >= 3 && w.Color.Take(3).Max() * w.Strength > 1e-3)
        {
            _sky = "Sky";
            for (int i = 2; top.Any(o => string.Equals(o.Name, _sky, StringComparison.OrdinalIgnoreCase)); i++) _sky = "Sky_" + i;
        }
    }

    private void Place(Node n, Out? parent, List<Out> siblings)
    {
        var kids = n.Children.Where(c => c.Kept).ToList();
        switch (n.Kind)
        {
            case NodeKind.Light:
            case NodeKind.Camera:
            {
                var element = new Out(n.Kind == NodeKind.Light ? OutKind.Light : OutKind.Camera, n, null, null) { Name = n.Info.Name };
                (n.Kind == NodeKind.Light ? _lights : _cameras).Add(element);
                if (kids.Count == 0) return;
                var frame = new Out(OutKind.Group, n, null, parent) { Name = n.Info.Name };
                siblings.Add(frame);
                foreach (var c in kids) Place(c, frame, frame.Children);
                return;
            }
            case NodeKind.Geometry when kids.Count == 0 && n.Parts.Count == 1:
                siblings.Add(new Out(OutKind.Mesh, n, n.Parts[0], parent) { Name = n.Info.Name });
                return;
            default:
            {
                var group = new Out(OutKind.Group, n, null, parent) { Name = n.Info.Name };
                siblings.Add(group);
                if (n.Kind == NodeKind.Geometry)
                    foreach (var part in n.Parts)
                        group.Children.Add(new Out(OutKind.Mesh, n, part, group)
                        {
                            Name = n.Parts.Count == 1 ? n.Info.Name : PartName(n, part),
                            Inside = true,
                        });
                foreach (var c in kids) Place(c, group, group.Children);
                return;
            }
        }
    }

    private string PartName(Node n, Part part) =>
        part.Material >= 0 && part.Material < _file.Materials.Count ? _file.Materials[part.Material].Name : $"{n.Info.Name}_{part.Slot + 1}";

    /// <summary>Overbryce names: at most 20 characters without spaces, unique among the elements of a group.</summary>
    private static void Name(List<Out> siblings, HashSet<string>? taken = null)
    {
        var used = new HashSet<string>(taken ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var o in siblings)
        {
            string name = Element.SanitizeName(o.Name);
            for (int i = 2; !used.Add(name); i++)
            {
                string suffix = "_" + i.ToString(CultureInfo.InvariantCulture);
                string stem = Element.SanitizeName(o.Name);
                name = (stem.Length + suffix.Length > Element.MaxNameLength ? stem[..(Element.MaxNameLength - suffix.Length)] : stem) + suffix;
            }
            o.Name = name;
            Name(o.Children);
        }
    }

    private IEnumerable<Out> AllOutputs()
    {
        IEnumerable<Out> Walk(Out o) => o.Children.SelectMany(Walk).Prepend(o);
        return _lights.Concat(_cameras).Concat(_roots.SelectMany(Walk));
    }

    // ================================================================== values per frame

    private void SampleOutputs()
    {
        foreach (var o in AllOutputs())
        {
            o.Position = new Vec3d[_frames];
            o.Angles = new Vec3d[_frames];
            if (o.Kind == OutKind.Light) o.Light = new LightValues[_frames];
            var near = Vec3d.Zero;
            for (int f = 0; f < _frames; f++)
            {
                int g = o.Node.Valid[f] ? f : o.Node.Reference;
                var world = o.Node.Frame[g];
                switch (o.Kind)
                {
                    case OutKind.Light:
                        o.Position[f] = world.T;
                        o.Angles[f] = (world.R * new Vec3d(0, 0, -1)).Normalized;
                        o.Light![f] = LightAt(o.Node, g);
                        continue;
                    case OutKind.Camera:
                        o.Position[f] = world.T;
                        near = o.Angles[f] = world.R.EulerCamera(near);
                        continue;
                }
                if (o.Inside)
                {
                    o.Position[f] = Vec3d.Zero;
                    o.Angles[f] = Vec3d.Zero;
                    continue;
                }
                var parentFrame = o.Parent == null ? RigidFrame.Identity : FrameOf(o.Parent, f);
                var local = world.RelativeTo(parentFrame);
                near = o.Angles[f] = local.R.EulerXyz(near);
                // a group turns first and moves in its turned frame (CGrupa::Render), an element moves first
                o.Position[f] = o.Kind == OutKind.Group ? local.R.Transposed * local.T : local.T;
            }
        }
    }

    private static RigidFrame FrameOf(Out group, int f)
    {
        var n = group.Node;
        return n.Frame[n.Valid[f] ? f : n.Reference];
    }

    // ================================================================== lights

    /// <summary>A Blender light in Overbryce's terms.</summary>
    private readonly record struct LightValues(Rgba Color, double C, double L, double Q, double Cutoff, double Exponent,
        float Radius, bool Shadows, bool Distant);

    private enum LightType { Point = 0, Spot = 1, Sun = 2, Area = 3 }

    private LightValues LightAt(Node n, int f)
    {
        var d = n.Data;
        int w = n.Info.DataWidth;
        if (d == null || w < 13) return new LightValues(new Rgba(1, 1, 1, 1), 1, 0, 0, 180, 0, 0, true, false);
        int o = f * w;
        var type = (LightType)(int)d[o];
        double r = d[o + 1], g = d[o + 2], b = d[o + 3], power = d[o + 4];
        double spotSize = d[o + 5], blend = d[o + 6], softSize = d[o + 7], size = d[o + 8], sizeY = d[o + 9], angle = d[o + 10];
        bool shadows = d[o + 11] > 0.5f;
        double scale, c = 1, l = 0, q = 0, cutoff = 180, exponent = 0;
        float radius = 0;
        bool distant = false;
        if (type == LightType.Sun)
        {
            // irradiance S on a white diffuse surface shows S/π
            scale = Display(power / Math.PI);
            radius = (float)(DistantLight * _radius * Math.Tan(angle / 2));
            distant = true;
        }
        else
        {
            double k = type == LightType.Area ? AreaFactor : PointFactor;
            double rad = type == LightType.Area ? Math.Sqrt(Math.Max(size * (sizeY > 0 ? sizeY : size), 0)) / 2 : softSize;
            radius = (float)rad;
            scale = Display(power * k);
            var pos = n.Frame[f].T;
            double toCenter = (pos - _center).Length;
            (c, l, q) = FitAttenuation(rad, Math.Max(toCenter - _radius, 0.02 * _radius), toCenter + _radius);
            if (type == LightType.Spot)
            {
                cutoff = Math.Min(90, spotSize * 180 / Math.PI / 2);
                exponent = SpotExponent(cutoff, blend);
            }
            else if (type == LightType.Area)
            {
                cutoff = 90;
                exponent = 1;
            }
        }
        // several lights add up in display values in Overbryce: the calibration evens that out on the scene's surfaces
        scale *= _lightScale;
        var (cr, cg, cb) = SurfaceColor([r, g, b], _agx, _typical);
        var color = new Rgba((float)(cr * scale), (float)(cg * scale), (float)(cb * scale), 1);
        return new LightValues(color, c, l, q, cutoff, exponent, radius, shadows, distant);
    }

    /// <summary>A sun is placed this many scene radii from the scene's centre.</summary>
    private const double DistantLight = 20;

    /// <summary>How bright a light is at the scene's centre (to keep the eight that matter most).</summary>
    private double Importance(Out light, int f)
    {
        var v = light.Light![f];
        double peak = Math.Max(v.Color.R, Math.Max(v.Color.G, v.Color.B));
        if (v.Distant) return peak;
        double dist = (light.Position[f] - _center).Length;
        return peak / Math.Max(v.C + v.L * dist + v.Q * dist * dist, 1e-6);
    }

    // ================================================================== materials

    private (double R, double G, double B) WorldAmbient()
    {
        var w = _file.World;
        double s = w.Strength;
        return SeenColor(w.Color[0] * s, w.Color[1] * s, w.Color[2] * s, _agx);
    }

    /// <summary>A material's values in frame <paramref name="f"/> (animated materials have a value per frame).</summary>
    private Surface SurfaceOf(int material, int f)
    {
        if (material < 0 || material >= _file.Materials.Count)
            return Surface.From(new BlenderSceneFile.MaterialInfo { Base = [0.8, 0.8, 0.8] }, false, _world, _agx, _typical);
        var m = _file.Materials[material];
        if (m.Track >= 0 && m.TrackWidth > 0)
        {
            var row = _file.Floats(m.Track + (long)f * m.TrackWidth * 4, m.TrackWidth);
            m = WithTrack(m, row);
        }
        return Surface.From(m, m.Texture >= 0 && m.Texture < _file.Textures.Count, _world, _agx, _typical);
    }

    private BlenderSceneFile.MaterialInfo WithTrack(BlenderSceneFile.MaterialInfo m, float[] row)
    {
        var copy = new BlenderSceneFile.MaterialInfo
        {
            Name = m.Name, Shader = m.Shader, Base = (double[])m.Base.Clone(), Alpha = m.Alpha, Metallic = m.Metallic,
            Roughness = m.Roughness, Ior = m.Ior, Transmission = m.Transmission, Emission = (double[])m.Emission.Clone(),
            EmissionStrength = m.EmissionStrength, Specular = m.Specular, Texture = m.Texture, Uv = m.Uv,
        };
        int i = 0;
        foreach (var name in _file.MaterialTrack)
        {
            int width = name is "base" or "emission" ? 3 : 1;
            if (i + width > row.Length) break;
            switch (name)
            {
                case "base": copy.Base = [row[i], row[i + 1], row[i + 2]]; break;
                case "emission": copy.Emission = [row[i], row[i + 1], row[i + 2]]; break;
                case "alpha": copy.Alpha = row[i]; break;
                case "metallic": copy.Metallic = row[i]; break;
                case "roughness": copy.Roughness = row[i]; break;
                case "ior": copy.Ior = row[i]; break;
                case "transmission": copy.Transmission = row[i]; break;
                case "emission_strength": copy.EmissionStrength = row[i]; break;
                case "specular": copy.Specular = row[i]; break;
            }
            i += width;
        }
        return copy;
    }

    /// <summary>A Blender material in Overbryce's terms.</summary>
    private readonly record struct Surface(Rgba Diffuse, Rgba Ambient, Rgba Specular, Rgba Emission, float Shininess,
        float Reflectivity, float Ior, float Glow, float Roughness)
    {
        public static Surface From(BlenderSceneFile.MaterialInfo m, bool textured, (double R, double G, double B) world, bool agx, double light)
        {
            var (br, bg, bb) = SurfaceColor(m.Base, agx, light);
            if (textured) (br, bg, bb) = (1, 1, 1);
            double metallic = Math.Clamp(m.Metallic, 0, 1);
            double transmission = Math.Clamp(m.Transmission, 0, 1);
            float alpha = (float)Math.Clamp(m.Alpha * (1 - 0.9 * transmission), 0, 1);
            // a metal has no diffuse light of its own, but without reflections (the classic view) it would be black
            double diffuse = 1 - 0.6 * metallic;
            var dif = new Rgba((float)(br * diffuse), (float)(bg * diffuse), (float)(bb * diffuse), alpha);
            var amb = new Rgba((float)(dif.R * world.R / GlobalAmbient), (float)(dif.G * world.G / GlobalAmbient),
                (float)(dif.B * world.B / GlobalAmbient), alpha);
            double spec = SpecularStrength(m.Roughness, m.Specular, metallic);
            double sr = spec * (1 - metallic) + spec * metallic * br, sg = spec * (1 - metallic) + spec * metallic * bg,
                sb = spec * (1 - metallic) + spec * metallic * bb;
            // a glowing surface is seen directly: AgX compresses it, the plain display curve lets it go above white
            var (er, eg, eb) = SeenColor(m.Emission[0] * m.EmissionStrength, m.Emission[1] * m.EmissionStrength, m.Emission[2] * m.EmissionStrength, agx);
            double peak = Math.Max(er, Math.Max(eg, eb));
            float glow = 1;
            if (peak > 1)
            {
                glow = (float)peak;
                (er, eg, eb) = (er / peak, eg / peak, eb / peak);
            }
            // Overbryce's reflections are not tinted: a coloured metal reflects as much as its colour is bright
            float reflect = (float)Math.Clamp(metallic > 0.01 ? 0.8 * metallic * Luminance(br, bg, bb)
                : m.Roughness < 0.25 && transmission < 0.5 ? 0.05 : 0, 0, 1);
            float ior = transmission > 0.5 && m.Ior > 1 ? (float)m.Ior : 1;
            return new Surface(dif, amb, new Rgba((float)sr, (float)sg, (float)sb, alpha), new Rgba((float)er, (float)eg, (float)eb, alpha),
                (float)BlenderConversions.Shininess(m.Roughness), reflect, ior, glow, reflect > 0 || ior > 1 ? (float)Math.Clamp(m.Roughness, 0, 1) : 0);
        }

        public void ApplyTo(Element e)
        {
            e.Diffuse = Diffuse;
            e.Ambient = Ambient;
            e.Specular = Specular;
            e.Emission = Emission;
            e.Shininess = Shininess;
            e.Reflectivity = Reflectivity;
            e.RefractiveIndex = Ior;
            e.Glow = Glow;
            e.Roughness = Roughness;
        }
    }

    // ================================================================== key states

    private readonly Dictionary<int, Scene> _states = [];
    private readonly Dictionary<int, List<Out>> _shownLights = [];

    /// <summary>The key state of sampled frame <paramref name="f"/> (built once).</summary>
    private Scene State(int f)
    {
        if (!_states.TryGetValue(f, out var scene)) _states[f] = scene = BuildState(f);
        return scene;
    }

    /// <summary>The lights of frame <paramref name="f"/>: the eight brightest at the scene's centre, in their order.</summary>
    private List<Out> ShownLights(int f)
    {
        if (!_shownLights.TryGetValue(f, out var list))
            _shownLights[f] = list = _lights.Where(l => l.Present(f)).OrderByDescending(l => Importance(l, f)).Take(LightElement.MaxPerScene)
                .OrderBy(l => _lights.IndexOf(l)).ToList();
        return list;
    }

    private string? ActiveCamera(int f) =>
        _cameras.FirstOrDefault(c => c.Present(f) && !c.Node.Info.Instance && _file.Cameras.Length > f && c.Node.Info.Name == _file.Cameras[f])?.Name;

    private Scene BuildState(int f)
    {
        var scene = new Scene("F" + (_file.Frames.Length > 0 ? _file.Frames[f] : f).ToString(CultureInfo.InvariantCulture)) { AniTime = TimeOf(f) };
        // the lights take the first OpenGL light slots
        foreach (var light in ShownLights(f))
            scene.Add(Mark(MakeLight(light, f), light, f));
        string? active = ActiveCamera(f);
        CameraElement? view = null;
        foreach (var camera in _cameras.Where(c => c.Present(f)))
        {
            var c = MakeCamera(camera, f, camera.Name == active);
            if (c.Active) view = c;
            scene.Add(Mark(c, camera, f));
        }
        if (_sky != null)
        {
            var sky = MakeSky(view);
            sky.MotionBoundary = _skyStops.Contains(f);
            scene.Add(sky);
        }
        foreach (var root in _roots)
            if (Make(root, f) is { } e) scene.Add(e);
        ApplyFog(scene);
        return scene;
    }

    /// <summary>Elements that stop at a key state (a jump, see <see cref="FindForcedKeys"/>).</summary>
    private readonly Dictionary<int, HashSet<Out>> _stops = [];
    private readonly HashSet<int> _skyStops = [];

    private T Mark<T>(T e, Out o, int f) where T : Element
    {
        if (_stops.TryGetValue(f, out var set) && set.Contains(o)) e.MotionBoundary = true;
        return e;
    }

    /// <summary>
    /// The world's colour behind everything: a glowing sphere around the active camera, just inside its far clip
    /// (Overbryce clears the picture to black). It casts no shadows and takes no light.
    /// </summary>
    private SphereElement MakeSky(CameraElement? camera)
    {
        var w = _file.World;
        var (r, g, b) = SeenColor(w.Color[0] * w.Strength, w.Color[1] * w.Strength, w.Color[2] * w.Strength, _agx);
        double peak = Math.Max(r, Math.Max(g, b));
        float glow = peak > 1 ? (float)peak : 1;
        var black = new Rgba(0, 0, 0, 1);
        return new SphereElement(_sky!)
        {
            X = camera?.X ?? (float)_center.X, Y = camera?.Y ?? (float)_center.Y, Z = camera?.Z ?? (float)_center.Z,
            Radius = 0.9 * (camera?.Far ?? CameraElement.DefaultFar),
            QualityX = 32, QualityY = 16,
            Diffuse = black, Ambient = black, Specular = black,
            Emission = new Rgba((float)(r / glow), (float)(g / glow), (float)(b / glow), 1),
            Glow = glow,
            Shininess = 0,
            CastsShadows = false,
        };
    }

    private Element? Make(Out o, int f)
    {
        if (!o.Present(f)) return null;
        Element e;
        if (o.Kind == OutKind.Group)
        {
            var g = new GroupElement(o.Name);
            foreach (var c in o.Children)
                if (Make(c, f) is { } child) g.Add(child);
            e = g;
        }
        else
        {
            var mesh = Primitive(o) ?? new PolyMeshElement(o.Name, MeshAt(o.Node, o.Part!, f));
            SurfaceOf(o.Part!.Material, f).ApplyTo(mesh);
            int tex = o.Part.Material >= 0 && o.Part.Material < _file.Materials.Count ? _file.Materials[o.Part.Material].Texture : -1;
            if (tex >= 0 && tex < _file.Textures.Count) mesh.Texture = _file.Textures[tex].File;
            e = mesh;
        }
        Place(e, o.Position[f], o.Angles[f]);
        return Mark(e, o, f);
    }

    private static void Place(Element e, Vec3d position, Vec3d angles)
    {
        const double deg = 180 / Math.PI;
        e.X = (float)position.X; e.Y = (float)position.Y; e.Z = (float)position.Z;
        e.Alpha = (float)(angles.X * deg); e.Beta = (float)(angles.Y * deg); e.Gamma = (float)(angles.Z * deg);
    }

    private LightElement MakeLight(Out o, int f)
    {
        var v = o.Light![f];
        var p = o.Position[f];
        var dir = o.Angles[f];
        if (v.Distant) p = _center - dir * (DistantLight * _radius);
        var l = new LightElement(o.Name)
        {
            X = (float)p.X, Y = (float)p.Y, Z = (float)p.Z,
            Alpha = (float)dir.X, Beta = (float)dir.Y, Gamma = (float)dir.Z,
            Diffuse = v.Color,
            Specular = v.Color,
            Ambient = new Rgba(0, 0, 0, 1),
            Emission = new Rgba(0, 0, 0, 1),
            ConstantAttenuation = v.C,
            LinearAttenuation = v.L,
            QuadraticAttenuation = v.Q,
            SpotCutoff = v.Cutoff,
            SpotExponent = v.Exponent,
            LightRadius = v.Radius,
            CastsShadows = v.Shadows,
        };
        return l;
    }

    private CameraElement MakeCamera(Out o, int f, bool active)
    {
        var c = new CameraElement(o.Name) { Active = active };
        Place(c, o.Position[f], o.Angles[f]);
        var d = o.Node.Data;
        int w = o.Node.Info.DataWidth;
        int g = o.Node.Valid[f] ? f : o.Node.Reference;
        if (d != null && w >= 6)
        {
            int i = g * w;
            c.FieldOfView = Math.Clamp((float)(d[i] * 180 / Math.PI), 1f, 179f);
            c.Near = Math.Max(1e-4f, d[i + 1]);
            c.Far = Math.Max(c.Near * 1.001f, d[i + 2]);
            c.Focus = Math.Max(0.01f, d[i + 3]);
            c.Aperture = d[i + 5] > 0.5f ? Math.Max(0, d[i + 4]) : 0;
        }
        return c;
    }

    private void ApplyFog(Scene scene)
    {
        var v = _file.World.Volume;
        if (v == null || v.Density <= 0) return;
        var (r, g, b) = SurfaceColor(v.Color, _agx, _typical);
        scene.FogMode = FogMode.Exponential;
        scene.FogDensity = (float)Math.Clamp(v.Density, 0, 1);
        scene.FogColor = new Rgba((float)(r * _world.R), (float)(g * _world.G), (float)(b * _world.B), 1);
    }

    // ================================================================== settings and report

    private void ApplySettings(Animation a)
    {
        var s = _file.Scene;
        a.Fps = (float)Math.Clamp(s.Fps, 1, 128);
        a.Modern.Enabled = true;
        a.Modern.ClassicTextureLight = false;
        a.Modern.Bloom = false;
        a.Modern.Exposure = (float)Math.Clamp(Math.Pow(2, s.Exposure), 0.05, 20);
        a.Raytrace.Samples = Math.Clamp(s.CyclesSamples > 0 ? s.CyclesSamples : 16, 4, 64);
        a.Raytrace.Bounces = Math.Clamp(s.CyclesBounces > 0 ? s.CyclesBounces : 6, 1, 8);
        a.Raytrace.Shutter = s.MotionBlur ? (float)Math.Clamp(s.Shutter, 0, 1) : 0;
        int largest = _file.Textures.Count == 0 ? Animation.ClassicTextureSize : _file.Textures.Max(t => Math.Max(t.Width, t.Height));
        a.TextureSize = Math.Clamp((int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(largest, 1)), Animation.ClassicTextureSize, 1024);
        if (_agx)
            _report.Approximate($"The {s.ViewTransform} view transform: approximated by AgX's curve on colours and lights");
        else if (s.ViewTransform is not ("Standard" or "Raw"))
            _report.Approximate($"The {s.ViewTransform} view transform: colours are converted like Blender's Standard view");
        if (_sky != null) _report.Note($"The world colour is a glowing sphere \"{_sky}\" around the active camera.");
        if (_file.World.Texture >= 0)
            _report.Approximate("The world's environment image: its average colour lights the scene");
        if (_file.World.Volume is { Density: > 0 })
            _report.Approximate("The world's volume: exponential fog");
    }

    private void ReportSummary(Animation a)
    {
        // the materials of restored elements do not matter: those take their colours from the export's properties
        var converted = Restoring
            ? AllOutputs().Where(o => o.Part != null).Select(o => o.Part!.Material).ToHashSet()
            : Enumerable.Range(0, _file.Materials.Count).ToHashSet();
        foreach (var m in _file.Materials.Where((_, i) => converted.Contains(i)))
        {
            foreach (var p in m.Procedural) _report.Approximate($"Material {m.Name}: the {p} from nodes is replaced by its plain value");
            foreach (var u in m.Unsupported) _report.Drop($"Material {m.Name}: {u}");
        }
        foreach (var o in _cameras)
        {
            var d = o.Node.Data;
            int w = o.Node.Info.DataWidth;
            if (d == null || w < 10) continue;
            int i = Math.Max(o.Node.Reference, 0) * w;
            if (d[i + 6] >= 0.5f) _report.Approximate($"Camera {o.Node.Info.Name}: not a perspective camera; a perspective one is used");
            if (Math.Abs(d[i + 7]) > 1e-4 || Math.Abs(d[i + 8]) > 1e-4) _report.Drop($"Camera {o.Node.Info.Name}: lens shift");
        }
        foreach (var light in _lights)
        {
            var n = light.Node;
            if (n.Data == null || n.Info.DataWidth < 1) continue;
            var type = (LightType)(int)n.Data[Math.Max(n.Reference, 0) * n.Info.DataWidth];
            if (type == LightType.Sun) _report.Approximate($"Light {n.Info.Name}: a sun, as a point light {DistantLight} scene radii away");
            if (type == LightType.Area) _report.Approximate($"Light {n.Info.Name}: an area light, as a round spot of its size");
        }
        int over = 0;
        for (int f = 0; f < _frames; f++) over = Math.Max(over, _lights.Count(l => l.Present(f)));
        if (over > LightElement.MaxPerScene)
            _report.Drop($"{over - LightElement.MaxPerScene} of {over} lights in some frames (Overbryce has 8: the brightest are kept per key state)");
        if (_cameras.Count == 0) _report.Note("The scene has no camera.");

        if (_rigid > 0)
            _report.Note($"{_rigid} mesh{(_rigid == 1 ? "" : "es")} moved by a rig as a whole: the motion became {(_rigid == 1 ? "its" : "their")} placement.");
        if (_boxes + _rectangles > 0)
            _report.Note($"Recognised {_boxes} box{(_boxes == 1 ? "" : "es")} and {_rectangles} rectangle{(_rectangles == 1 ? "" : "s")} (Overbryce's own shapes instead of polygon meshes).");
        int meshes = a.Scenes.SelectMany(s => s.Descendants()).OfType<PolyMeshElement>().Select(p => p.Data).Distinct().Count();
        _report.Note($"{a.Scenes.Count} key state{(a.Scenes.Count == 1 ? "" : "s")}, {AllOutputs().Count() + _tags.Values.Count(t => t.Role == null)} elements ({_lights.Count} lights, {_cameras.Count} cameras), {meshes} polygon mesh{(meshes == 1 ? "" : "es")}, {_file.Textures.Count} texture{(_file.Textures.Count == 1 ? "" : "s")}.");
    }
}
