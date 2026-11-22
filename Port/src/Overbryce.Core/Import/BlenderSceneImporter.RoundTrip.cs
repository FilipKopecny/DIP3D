using System.Text.Json;
using Overbryce.Core.IO;
using Overbryce.Core.Model;

namespace Overbryce.Core.Import;

/// <summary>
/// The round trip: a scene the Overbryce export made (and maybe edited in Blender) comes back as the elements it was
/// made from. The export tags every object with its element (custom property ob_tag: the element as .ani text, the
/// key states it is in and stops at) and keys what Blender has no place for in custom properties (ob_ext, ob_light,
/// ob_cam, ob_shape); the scene carries the animation's settings and key states (ob_settings) and the frames that show
/// them (ob_keytimes). The import takes the key states at those frames and rebuilds each tagged element from its tag,
/// with the place, colours and sizes Blender has at that moment. Objects added in Blender come in as usual; a tagged
/// shape whose mesh was edited or which was scaled in Blender becomes a polygon mesh.
/// </summary>
public sealed partial class BlenderSceneImporter
{
    private sealed class Tag
    {
        public string Name { get; init; } = "";
        public int Order { get; init; }
        public HashSet<int> States { get; init; } = [];
        public HashSet<int> Stops { get; init; } = [];
        public int Verts { get; init; } = -1;

        /// <summary>A texture that changes between key states: its name in each (empty: none).</summary>
        public Dictionary<int, string>? Textures { get; init; }

        /// <summary>"pos" (the offset empty of a group) or "aim" (a spot light's target): helpers, not elements.</summary>
        public string? Role { get; init; }

        public Element? Template { get; init; }
    }

    private Animation? _skeleton;
    private int[] _keyFrameIndex = [];
    private readonly Dictionary<Node, Tag> _tags = [];
    private readonly Dictionary<Node, List<Node>> _restoredChildren = [];
    private readonly Dictionary<Node, Node> _posOf = [];
    private readonly List<Node> _restoredRoots = [];
    private int _becameMeshes;

    private bool Restoring => _skeleton != null;

    private const double RadToDeg = 180 / Math.PI;

    /// <summary>Reads the tags of a scene exported by Overbryce (nothing when there are none).</summary>
    private void ReadTags()
    {
        foreach (var n in _nodes)
            if (!n.Info.Instance && n.Info.Overbryce != null && n.Info.Overbryce.TryGetValue("ob_tag", out var text) && ParseTag(text) is { } tag)
                _tags[n] = tag;
        if (_tags.Count == 0) return;

        var props = _file.Scene.Props;
        if (!props.TryGetValue("ob_settings", out var settings) || settings.ValueKind != JsonValueKind.String ||
            !props.TryGetValue("ob_keytimes", out var times) || times.ValueKind != JsonValueKind.Array)
        {
            _report.Note("The scene has objects exported by Overbryce, but not the animation they belong to: imported like any Blender scene.");
            _tags.Clear();
            return;
        }
        Animation skeleton;
        try
        {
            skeleton = AniFile.Parse(settings.GetString()!);
        }
        catch (AniFormatException ex)
        {
            _report.Note("The Overbryce settings of the scene cannot be read (" + ex.Message + "): imported like any Blender scene.");
            _tags.Clear();
            return;
        }
        var keyTimes = times.EnumerateArray().Where(t => t.ValueKind == JsonValueKind.Number).Select(t => t.GetDouble()).ToArray();
        if (keyTimes.Length != skeleton.Scenes.Count || _file.Frames.Length == 0)
        {
            _report.Note("The key states of the Overbryce export do not match the scene: imported like any Blender scene.");
            _tags.Clear();
            return;
        }
        _keyFrameIndex = keyTimes.Select(NearestFrame).ToArray();
        _skeleton = skeleton;

        // an element whose object was moved out of its group in Blender is imported like a Blender object
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var n in _tags.Keys.ToList())
                if (_tags[n].Role == null && n.Parent != null && LogicalParent(n) == null)
                {
                    _tags.Remove(n);
                    changed = true;
                }
        }
        foreach (var (n, tag) in _tags)
        {
            if (tag.Role == "pos" && n.Parent != null) _posOf[n.Parent] = n;
            if (tag.Role != null) continue;
            if (LogicalParent(n) is { } parent)
            {
                if (!_restoredChildren.TryGetValue(parent, out var list)) _restoredChildren[parent] = list = [];
                list.Add(n);
            }
            else _restoredRoots.Add(n);
        }
        _restoredRoots.Sort((a, b) => _tags[a].Order.CompareTo(_tags[b].Order));
        foreach (var list in _restoredChildren.Values) list.Sort((a, b) => _tags[a].Order.CompareTo(_tags[b].Order));
        _report.Note($"A scene exported by Overbryce: {_tags.Values.Count(t => t.Role == null)} elements are restored from their tags at the {keyTimes.Length} key states of the export.");
    }

    private static Tag? ParseTag(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var r = doc.RootElement;
            if (r.TryGetProperty("role", out var role)) return new Tag { Role = role.GetString() ?? "" };
            var ani = r.GetProperty("ani").GetString();
            var template = ani == null ? null : AniFile.Parse(ani).Scenes[0].Children.FirstOrDefault();
            if (template == null) return null;
            template.Parent?.Remove(template);
            return new Tag
            {
                Name = r.GetProperty("name").GetString() ?? template.Name,
                Order = r.TryGetProperty("order", out var o) ? o.GetInt32() : 0,
                States = r.TryGetProperty("states", out var s) ? s.EnumerateArray().Select(x => x.GetInt32()).ToHashSet() : [],
                Stops = r.TryGetProperty("stops", out var st) ? st.EnumerateArray().Select(x => x.GetInt32()).ToHashSet() : [],
                Verts = r.TryGetProperty("verts", out var v) ? v.GetInt32() : -1,
                Textures = r.TryGetProperty("textures", out var tx) && tx.ValueKind == JsonValueKind.Object
                    ? tx.EnumerateObject().Where(p => int.TryParse(p.Name, out _)).ToDictionary(p => int.Parse(p.Name, System.Globalization.CultureInfo.InvariantCulture), p => p.Value.GetString() ?? "")
                    : null,
                Template = template,
            };
        }
        catch (Exception ex) when (ex is JsonException or AniFormatException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>The tagged group an element belongs to (through the group's offset empty), or null at the top.</summary>
    private Node? LogicalParent(Node n)
    {
        var p = n.Parent;
        if (p != null && _tags.TryGetValue(p, out var pt) && pt.Role == "pos") p = p.Parent;
        return p != null && _tags.TryGetValue(p, out var t) && t.Role == null && t.Template is GroupElement ? p : null;
    }

    private int NearestFrame(double frame)
    {
        int best = 0;
        for (int i = 1; i < _file.Frames.Length; i++)
            if (Math.Abs(_file.Frames[i] - frame) < Math.Abs(_file.Frames[best] - frame)) best = i;
        return best;
    }

    /// <summary>A custom property's values in sampled frame <paramref name="f"/> (null when the object has none).</summary>
    private float[]? Prop(Node n, string name, int f)
    {
        if (n.Info.Props == null || !n.Info.Props.TryGetValue(name, out var at) || at.Length < 2) return null;
        int width = (int)at[1];
        return _file.Floats(at[0] + (long)f * width * 4, width);
    }

    // ================================================================== the restored animation

    private Animation RestoredAnimation()
    {
        var a = _skeleton!;
        if (_options.Name != null) a.Name = _options.Name;
        bool others = AllOutputs().Any();
        for (int k = 0; k < a.Scenes.Count; k++)
        {
            var scene = a.Scenes[k];
            int f = _keyFrameIndex[k];
            foreach (var root in _restoredRoots)
                if (Restore(root, k, f) is { } e) scene.Add(e);
            if (others)
                foreach (var c in BuildState(f).Children.ToList()) scene.Add(c);
        }
        CorrectPermanentMotion(a);
        if (others)
            _report.Note($"{AllOutputs().Count()} elements made from objects added in Blender follow the key states of the export.");
        if (_becameMeshes > 0)
            _report.Approximate($"{_becameMeshes} restored shapes (per key state) were scaled or edited in Blender: they became polygon meshes");
        int lights = a.Scenes.Max(s => s.Lights.Count());
        if (lights > LightElement.MaxPerScene)
            _report.Drop($"{lights - LightElement.MaxPerScene} of {lights} lights in some key states (Overbryce has 8)");
        return a;
    }

    private Element? Restore(Node n, int k, int f)
    {
        var tag = _tags[n];
        if (!tag.States.Contains(k) || tag.Template == null) return null;
        var e = tag.Template.Clone();
        e.Name = tag.Name;
        var local = Prop(n, "_local", f);
        if (local is { Length: >= 6 })
        {
            e.X = local[0]; e.Y = local[1]; e.Z = local[2];
            e.Alpha = (float)(local[3] * RadToDeg); e.Beta = (float)(local[4] * RadToDeg); e.Gamma = (float)(local[5] * RadToDeg);
        }
        switch (e)
        {
            case GroupElement g:
                // a group turns, then moves in its turned frame: its offset is on the inner empty
                if (_posOf.TryGetValue(n, out var pos) && Prop(pos, "_local", f) is { Length: >= 3 } inner)
                {
                    g.X = inner[0]; g.Y = inner[1]; g.Z = inner[2];
                }
                if (_restoredChildren.TryGetValue(n, out var children))
                    foreach (var c in children)
                        if (Restore(c, k, f) is { } child) g.Add(child);
                break;
            case LightElement l:
                l.Alpha = l.Beta = l.Gamma = 0;
                if (Prop(n, "ob_light", f) is { Length: >= 22 } v)
                {
                    l.Alpha = v[0]; l.Beta = v[1]; l.Gamma = v[2];
                    l.Diffuse = new Rgba(v[3], v[4], v[5], v[6]);
                    l.Ambient = new Rgba(v[7], v[8], v[9], v[10]);
                    l.Specular = new Rgba(v[11], v[12], v[13], v[14]);
                    l.ConstantAttenuation = v[15]; l.LinearAttenuation = v[16]; l.QuadraticAttenuation = v[17];
                    l.SpotCutoff = v[18]; l.SpotExponent = v[19]; l.LightRadius = v[20]; l.Enabled = v[21] > 0.5f;
                }
                else
                {
                    var t = (LightElement)tag.Template;
                    l.Alpha = t.Alpha; l.Beta = t.Beta; l.Gamma = t.Gamma;
                }
                break;
            case CameraElement c:
                if (Prop(n, "ob_cam", f) is { Length: >= 5 } cam)
                {
                    c.FieldOfView = cam[0]; c.Aperture = cam[1]; c.Focus = cam[2]; c.Near = cam[3]; c.Far = cam[4];
                }
                c.Active = IsActiveCamera(n, f);
                break;
            default:
                e = RestoreSurface(n, tag, e, local, k, f);
                break;
        }
        e.MotionBoundary = tag.Stops.Contains(k);
        return e;
    }

    private Element RestoreSurface(Node n, Tag tag, Element e, float[]? local, int k, int f)
    {
        var dif = Prop(n, "ob_dif", f);
        var alpha = Prop(n, "ob_alpha", f);
        var emi = Prop(n, "ob_emi", f);
        var amb = Prop(n, "ob_amb", f);
        var spec = Prop(n, "ob_spec", f);
        var shin = Prop(n, "ob_shin", f);
        var ext = Prop(n, "ob_ext", f);
        bool hasExt = ext is { Length: >= 8 };
        if (dif is { Length: >= 3 }) e.Diffuse = new Rgba(dif[0], dif[1], dif[2], alpha is { Length: >= 1 } ? alpha[0] : e.Diffuse.A);
        if (emi is { Length: >= 3 }) e.Emission = new Rgba(emi[0], emi[1], emi[2], hasExt ? ext![0] : e.Emission.A);
        if (amb is { Length: >= 3 }) e.Ambient = new Rgba(amb[0], amb[1], amb[2], hasExt ? ext![1] : e.Ambient.A);
        if (spec is { Length: >= 3 }) e.Specular = new Rgba(spec[0], spec[1], spec[2], hasExt ? ext![2] : e.Specular.A);
        if (shin is { Length: >= 1 }) e.Shininess = shin[0];
        if (hasExt)
        {
            e.Reflectivity = ext![3]; e.RefractiveIndex = ext[4]; e.Glow = ext[5]; e.Roughness = ext[6]; e.Absorption = ext[7];
            if (ext.Length >= 9 && e is DiskElement disk) disk.SweepAngle = ext[8];
        }
        if (Prop(n, "ob_shape", f) is { } shape)
            for (int i = 0; i < shape.Length; i++) ShapeParameters.Set(e, i, shape[i]);

        // a texture that changes between key states keeps its name in each (the files stay where the original's were);
        // otherwise the Blender mesh decides (the import wrote its image as a BMP next to the animation)
        int material = n.Parts.Count > 0 ? n.Parts[0].Material : -1;
        int tex = material >= 0 && material < _file.Materials.Count ? _file.Materials[material].Texture : -1;
        if (tag.Textures != null && tag.Textures.TryGetValue(k, out var name)) e.Texture = name.Length > 0 ? name : null;
        else if (tex >= 0 && tex < _file.Textures.Count) e.Texture = _file.Textures[tex].File;

        bool scaled = local is { Length: >= 9 } && (Math.Abs(local[6] - 1) > 1e-4 || Math.Abs(local[7] - 1) > 1e-4 || Math.Abs(local[8] - 1) > 1e-4);
        bool edited = tag.Verts >= 0 && e.DrawStyle != DrawStyle.Line && n.Info.Mesh >= 0 && n.Info.Mesh < _file.Meshes.Count &&
                      _file.Meshes[n.Info.Mesh].VertexCount != tag.Verts;
        if ((e is PolyMeshElement || scaled || edited) && n.Parts.Count > 0 && n.Valid[f])
        {
            var mesh = new PolyMeshElement(tag.Name, MeshAt(n, n.Parts[0], f));
            mesh.CopyCommonFrom(e);
            if (e is not PolyMeshElement) _becameMeshes++;
            return mesh;
        }
        return e;
    }

    /// <summary>
    /// The camera the export's markers make active at a key state's frame (the markers sit on whole frames, the key
    /// state may lie just before one).
    /// </summary>
    private bool IsActiveCamera(Node n, int f)
    {
        var markers = _file.CameraMarkers().OrderBy(m => m.Frame).ToList();
        if (markers.Count == 0) return _file.Cameras.Length > f && _file.Cameras[f] == n.Info.Name;
        double frame = Math.Ceiling(_file.Frames[f] - 1e-3);
        string? active = markers.LastOrDefault(m => m.Frame <= frame + 1e-6).Camera ?? markers[0].Camera;
        return active == n.Info.Name;
    }

    /// <summary>
    /// Blender has the permanent rotation and orbit in the elements' animated values; the restored elements turn by
    /// themselves again, so what they turned by up to each key state comes off their key-state values.
    /// </summary>
    private static void CorrectPermanentMotion(Animation a)
    {
        var moving = new List<(int K, string[] Path, Element E, Vector Target)>();
        for (int k = 0; k < a.Scenes.Count; k++)
            foreach (var (path, e) in Paths(a.Scenes[k], []))
                if (e.RotationPermanent || e.OrbitPermanent)
                    moving.Add((k, path, e, new Vector(e.X, e.Y, e.Z, e.Alpha, e.Beta, e.Gamma)));
        if (moving.Count == 0 || a.Scenes.Count < 2) return;
        // a key state shows as itself plus the motion up to it, which depends neither on the neighbouring key states nor
        // on the other elements' values: all the elements of a key state are corrected from a few evaluations of it
        foreach (var state in moving.GroupBy(m => m.K))
        {
            int k = state.Key;
            var items = state.ToList();
            Element?[] ShownAll() { var s = StateAsShown(a, k); return items.Select(i => Find(s, i.Path)).ToArray(); }

            // the turn adds to the angles; without an orbit the position shows as it is
            var shown = ShownAll();
            for (int i = 0; i < items.Count; i++)
            {
                var (_, _, e, t) = items[i];
                if (shown[i] is not { } s) continue;
                e.Alpha += t.A - s.Alpha; e.Beta += t.B - s.Beta; e.Gamma += t.C - s.Gamma;
                if (!e.OrbitPermanent) { e.X += t.X - s.X; e.Y += t.Y - s.Y; e.Z += t.Z - s.Z; }
            }
            var orbits = Enumerable.Range(0, items.Count).Where(i => items[i].E.OrbitPermanent && shown[i] != null).ToList();
            if (orbits.Count == 0) continue;

            // the orbit turns the position: shown = o + M p, so p = M⁻¹ (target - o), M from three unit positions
            Vec3d[] Probe(Vec3d p)
            {
                foreach (int i in orbits) { var e = items[i].E; e.X = (float)p.X; e.Y = (float)p.Y; e.Z = (float)p.Z; }
                var s = ShownAll();
                return orbits.Select(i => s[i] is { } x ? new Vec3d(x.X, x.Y, x.Z) : p).ToArray();
            }
            var o = Probe(Vec3d.Zero);
            var px = Probe(new Vec3d(1, 0, 0));
            var py = Probe(new Vec3d(0, 1, 0));
            var pz = Probe(new Vec3d(0, 0, 1));
            for (int j = 0; j < orbits.Count; j++)
            {
                var (_, _, e, t) = items[orbits[j]];
                Vec3d cx = px[j] - o[j], cy = py[j] - o[j], cz = pz[j] - o[j];
                var m = new Mat3(cx.X, cy.X, cz.X, cx.Y, cy.Y, cz.Y, cx.Z, cy.Z, cz.Z);
                var want = new Vec3d(t.X, t.Y, t.Z);
                var p = m.TryInvert(out var inverse) ? inverse * (want - o[j]) : want;
                e.X = (float)p.X; e.Y = (float)p.Y; e.Z = (float)p.Z;
            }
        }
    }

    private readonly record struct Vector(float X, float Y, float Z, float A, float B, float C);

    /// <summary>Key state <paramref name="k"/> as the animation shows it (with the permanent motion up to it).</summary>
    private static Scene StateAsShown(Animation a, int k)
    {
        int last = a.Scenes.Count - 1;
        if (k < last && a.Scenes[k + 1].AniTime > a.Scenes[k].AniTime) return a.EvaluateSegment(k, 0);
        return k > 0 && a.Scenes[k].AniTime > a.Scenes[k - 1].AniTime ? a.EvaluateSegment(k - 1, 1) : a.EvaluateSegment(Math.Min(k, last - 1), 0);
    }

    private static IEnumerable<(string[] Path, Element E)> Paths(GroupElement g, string[] prefix)
    {
        foreach (var c in g.Children)
        {
            string[] path = [.. prefix, c.Name];
            yield return (path, c);
            if (c is GroupElement inner)
                foreach (var x in Paths(inner, path)) yield return x;
        }
    }

    private static Element? Find(GroupElement g, string[] path)
    {
        Element? e = g;
        foreach (var name in path) e = (e as GroupElement)?.FindChild(name);
        return e;
    }

    /// <summary>The names the restored elements take at the top of the key states (objects added in Blender avoid them).</summary>
    private HashSet<string> RestoredTopNames() =>
        _restoredRoots.Select(n => _tags[n].Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
