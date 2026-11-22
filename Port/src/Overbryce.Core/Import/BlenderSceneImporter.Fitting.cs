using System.Globalization;
using System.Numerics;
using Overbryce.Core.Model;

namespace Overbryce.Core.Import;

/// <summary>
/// Choosing the key states: as few sampled frames as Overbryce's spline needs to follow Blender's animation within
/// the tolerance. It starts with the frames that must be key states and adds the worst frame of every segment where
/// <see cref="Animation.EvaluateSegment"/> strays too far, until none does.
/// </summary>
public sealed partial class BlenderSceneImporter
{
    /// <summary>The largest colour difference (0..1) that counts as the same colour.</summary>
    private const double ColorTolerance = 0.01;

    private List<int> FitKeyFrames()
    {
        var keys = new SortedSet<int>(FindForcedKeys());
        int forced = keys.Count;
        var passed = new HashSet<(int, int, int, int)>();
        for (int round = 0; round < 200; round++)
        {
            var list = keys.ToList();
            var animation = new Animation("Fit");
            animation.Scenes.AddRange(list.Select(State));
            var inserts = new List<int>();
            for (int j = 0; j + 1 < list.Count; j++)
            {
                int a = list[j], b = list[j + 1];
                if (b - a < 2) continue;
                // a segment depends on the key states around it; when they did not change, it still passes
                var support = (j > 0 ? list[j - 1] : -1, a, b, j + 2 < list.Count ? list[j + 2] : -1);
                if (passed.Contains(support)) continue;
                double worst = 1;
                int at = -1;
                float ta = TimeOf(a), tb = TimeOf(b);
                for (int f = a + 1; f < b; f++)
                {
                    double e = Error(State(f), animation.EvaluateSegment(j, (TimeOf(f) - ta) / (tb - ta)));
                    if (e > worst) { worst = e; at = f; }
                }
                if (at >= 0) inserts.Add(at);
                else passed.Add(support);
            }
            if (inserts.Count == 0) break;
            foreach (int f in inserts) keys.Add(f);
        }
        _report.Note($"{keys.Count} key states for {_frames} frames ({forced} at cuts, jumps and changes of visibility; " +
                     $"tolerance {_options.Tolerance.ToString("0.####", CultureInfo.InvariantCulture)} of the scene's size).");
        return keys.ToList();
    }

    /// <summary>
    /// The frames that must be key states: the first and the last, where an element appears or disappears (an
    /// element shows from its key state to the next), where the active camera changes, and both frames of a jump
    /// (where an element stops, so the spline does not swing around it).
    /// </summary>
    private SortedSet<int> FindForcedKeys()
    {
        int last = _frames - 1;
        var keys = new SortedSet<int> { 0, last };
        var outputs = AllOutputs().ToList();
        foreach (var o in outputs)
        {
            bool before = Shown(o, 0);
            for (int f = 1; f <= last; f++)
            {
                bool now = Shown(o, f);
                if (now != before) keys.Add(f);
                before = now;
            }
        }
        for (int f = 1; f <= last; f++)
            if (ActiveCamera(f) != ActiveCamera(f - 1))
            {
                keys.Add(f - 1);
                keys.Add(f);
                _skyStops.Add(f - 1);
                _skyStops.Add(f);
            }
        int jumps = 0;
        foreach (var o in outputs.Where(o => !o.Inside))
        {
            var delta = new double[_frames];
            for (int f = 1; f <= last; f++)
                if (Shown(o, f) && Shown(o, f - 1))
                    delta[f] = (o.Position[f] - o.Position[f - 1]).Length / _radius + (o.Angles[f] - o.Angles[f - 1]).Length;
            for (int f = 1; f <= last; f++)
            {
                double around = Math.Max(f > 1 ? delta[f - 1] : 0, f < last ? delta[f + 1] : 0);
                if (delta[f] < 0.02 || delta[f] < 8 * around) continue;
                keys.Add(f - 1);
                keys.Add(f);
                Stop(o, f - 1);
                Stop(o, f);
                jumps++;
            }
        }
        if (jumps > 0) _report.Note($"{jumps} jump{(jumps == 1 ? "" : "s")} (an element moving from one frame to the next at once) became stops.");
        return keys;
    }

    private void Stop(Out o, int f)
    {
        if (!_stops.TryGetValue(f, out var set)) _stops[f] = set = [];
        set.Add(o);
    }

    /// <summary>Whether the element is in the key state of frame <paramref name="f"/>.</summary>
    private bool Shown(Out o, int f) => o.Kind == OutKind.Light ? ShownLights(f).Contains(o) : o.Present(f);

    // ================================================================== how far an in-between frame strays

    /// <summary>
    /// The largest difference between Blender's frame and Overbryce's in-between frame, in tolerances: above 1 the
    /// frame needs a key state. Positions count against the tolerance times the scene's size, colours against 0.01.
    /// </summary>
    private double Error(Scene truth, Scene evaluated) => Compare(truth, evaluated, Matrix4x4.Identity, Matrix4x4.Identity);

    private double Compare(GroupElement truth, GroupElement evaluated, Matrix4x4 truthWorld, Matrix4x4 evaluatedWorld)
    {
        double worst = 0;
        var other = new Dictionary<string, Element>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in evaluated.Children) other.TryAdd(c.Name, c);
        if (other.Count != truth.Children.Count) worst = 1e6;
        foreach (var t in truth.Children)
        {
            if (!other.TryGetValue(t.Name, out var e) || e.Type != t.Type) return 1e6;
            if (t.Name == _sky && t is SphereElement) continue;   // it follows the camera; its place does not show
            double err;
            if (t is GroupElement tg)
                err = Compare(tg, (GroupElement)e, Transforms.GroupLocal(tg) * truthWorld, Transforms.GroupLocal((GroupElement)e) * evaluatedWorld);
            else
                err = LeafError(t, e, truthWorld, evaluatedWorld);
            worst = Math.Max(worst, err);
        }
        return worst;
    }

    private double LeafError(Element t, Element e, Matrix4x4 truthParent, Matrix4x4 evaluatedParent)
    {
        double tolerance = Math.Max(_options.Tolerance * _radius, 1e-9);
        double worst = 0;
        switch (t)
        {
            case PolyMeshElement tm when e is PolyMeshElement em && tm.Data != null && em.Data != null:
            {
                var tw = Transforms.ElementLocal(tm) * truthParent;
                var ew = Transforms.ElementLocal(em) * evaluatedParent;
                var (lo, hi) = tm.Data.Bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = new Vector3((k & 1) != 0 ? hi.X : lo.X, (k & 2) != 0 ? hi.Y : lo.Y, (k & 4) != 0 ? hi.Z : lo.Z);
                    worst = Math.Max(worst, Vector3.Distance(Vector3.Transform(corner, tw), Vector3.Transform(corner, ew)) / tolerance);
                }
                if (!ReferenceEquals(tm.Data, em.Data) && tm.Data.VertexCount == em.Data.VertexCount)
                {
                    int step = Math.Max(1, tm.Data.VertexCount / 64);
                    for (int i = 0; i < tm.Data.VertexCount; i += step)
                    {
                        var a = new Vector3(tm.Data.Positions[i * 3], tm.Data.Positions[i * 3 + 1], tm.Data.Positions[i * 3 + 2]);
                        var b = new Vector3(em.Data.Positions[i * 3], em.Data.Positions[i * 3 + 1], em.Data.Positions[i * 3 + 2]);
                        worst = Math.Max(worst, Vector3.Distance(Vector3.Transform(a, tw), Vector3.Transform(b, ew)) / tolerance);
                    }
                }
                worst = Math.Max(worst, SurfaceError(t, e));
                break;
            }
            case LightElement tl when e is LightElement el:
            {
                var tp = Vector3.Transform(new Vector3(tl.X, tl.Y, tl.Z), truthParent);
                var ep = Vector3.Transform(new Vector3(el.X, el.Y, el.Z), evaluatedParent);
                worst = Math.Max(worst, Vector3.Distance(tp, ep) / tolerance);
                var td = Vector3.Normalize(new Vector3(tl.Alpha, tl.Beta, tl.Gamma));
                var ed = new Vector3(el.Alpha, el.Beta, el.Gamma);
                if (ed.LengthSquared() > 1e-12f) ed = Vector3.Normalize(ed);
                worst = Math.Max(worst, Vector3.Distance(td, ed) * _radius / tolerance);
                worst = Math.Max(worst, ColorError(tl.Diffuse, el.Diffuse));
                double d = _radius;
                double ta = 1 / Math.Max(tl.ConstantAttenuation + tl.LinearAttenuation * d + tl.QuadraticAttenuation * d * d, 1e-9);
                double ea = 1 / Math.Max(el.ConstantAttenuation + el.LinearAttenuation * d + el.QuadraticAttenuation * d * d, 1e-9);
                worst = Math.Max(worst, Math.Abs(ta - ea) / Math.Max(ta, 1e-9) / ColorTolerance);
                worst = Math.Max(worst, Math.Abs(tl.SpotCutoff - el.SpotCutoff) / 0.5);
                worst = Math.Max(worst, Math.Abs(tl.SpotExponent - el.SpotExponent) / Math.Max(0.5, 0.05 * tl.SpotExponent));
                break;
            }
            case CameraElement tc when e is CameraElement ec:
            {
                var tw = Transforms.CameraLocal(tc) * truthParent;
                var ew = Transforms.CameraLocal(ec) * evaluatedParent;
                // the camera's place, and where it looks: points a scene size in front, above and to the side
                foreach (var p in new[] { Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY, Vector3.UnitX })
                {
                    var q = p * (float)_radius;
                    worst = Math.Max(worst, Vector3.Distance(Vector3.Transform(q, tw), Vector3.Transform(q, ew)) / tolerance);
                }
                worst = Math.Max(worst, Math.Abs(tc.FieldOfView - ec.FieldOfView) / Math.Max(0.02, _options.Tolerance * tc.FieldOfView));
                worst = Math.Max(worst, Math.Abs(tc.Aperture - ec.Aperture) / tolerance);
                worst = Math.Max(worst, Math.Abs(tc.Focus - ec.Focus) / Math.Max(tolerance, 0.01 * tc.Focus));
                break;
            }
            default:
            {
                // a shape of its own (a box, a rectangle): its place and where its axes point, a scene size out
                var tw = Transforms.ElementLocal(t) * truthParent;
                var ew = Transforms.ElementLocal(e) * evaluatedParent;
                foreach (var p in new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
                {
                    var q = p * (float)_radius;
                    worst = Math.Max(worst, Vector3.Distance(Vector3.Transform(q, tw), Vector3.Transform(q, ew)) / tolerance);
                }
                worst = Math.Max(worst, SurfaceError(t, e));
                break;
            }
        }
        return worst;
    }

    private static double SurfaceError(Element t, Element e)
    {
        double worst = Math.Max(Math.Max(ColorError(t.Diffuse, e.Diffuse), ColorError(t.Ambient, e.Ambient)),
            Math.Max(ColorError(t.Specular, e.Specular), ColorError(t.Emission, e.Emission)));
        worst = Math.Max(worst, Math.Abs(t.Glow * t.Emission.R - e.Glow * e.Emission.R) / ColorTolerance);
        worst = Math.Max(worst, Math.Abs(t.Reflectivity - e.Reflectivity) / ColorTolerance);
        worst = Math.Max(worst, Math.Abs(t.Shininess - e.Shininess) / 0.02);
        return worst;
    }

    private static double ColorError(Rgba a, Rgba b) =>
        Math.Max(Math.Max(Math.Abs(a.R - b.R), Math.Abs(a.G - b.G)), Math.Max(Math.Abs(a.B - b.B), Math.Abs(a.A - b.A))) / ColorTolerance;
}
