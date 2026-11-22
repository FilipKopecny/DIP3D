using System.Numerics;
using Overbryce.Core.Model;

namespace Overbryce.Core.Import;

/// <summary>
/// Smarter geometry: a mesh a rig (or a hook, a lattice ...) only moves as a whole gets that motion as its placement
/// instead of a polygon mesh per key state, and a plain box or rectangle becomes Overbryce's own shape.
/// </summary>
public sealed partial class BlenderSceneImporter
{
    private int _rigid;

    /// <summary>
    /// A deforming mesh whose every sampled shape is its rest shape turned and moved (a rigid rig: each vertex bound
    /// to one bone, all to the same one) moves by its placement instead: the turn and move are folded into it.
    /// </summary>
    private void FoldRigidDeformation(Node n)
    {
        var deform = n.Info.Deform;
        if (deform == null || n.Info.Mesh < 0 || n.Info.Mesh >= _file.Meshes.Count) return;
        int count = _file.Meshes[n.Info.Mesh].VertexCount;
        if (count < 3) return;
        var rest = _file.Floats(_file.Meshes[n.Info.Mesh].Positions, count * 3);
        var restCenter = Centroid(rest, count);
        double extent = 0;
        for (int i = 0; i < count; i++) extent = Math.Max(extent, (At(rest, i) - restCenter).Length);
        double tolerance = Math.Max(1e-5, 1e-4 * extent);

        var folds = new (Mat3 R, Vec3d T)?[_frames];
        for (int f = 0; f < _frames; f++)
        {
            if (f >= deform.Length || deform[f] < 0 || !n.Valid[f]) continue;
            var co = _file.Floats(deform[f], count * 3);
            var center = Centroid(co, count);
            // the best turn (Kabsch): the rotation part of the cross-covariance of the two shapes
            double m00 = 0, m01 = 0, m02 = 0, m10 = 0, m11 = 0, m12 = 0, m20 = 0, m21 = 0, m22 = 0;
            for (int i = 0; i < count; i++)
            {
                var q = At(co, i) - center;
                var p = At(rest, i) - restCenter;
                m00 += q.X * p.X; m01 += q.X * p.Y; m02 += q.X * p.Z;
                m10 += q.Y * p.X; m11 += q.Y * p.Y; m12 += q.Y * p.Z;
                m20 += q.Z * p.X; m21 += q.Z * p.Y; m22 += q.Z * p.Z;
            }
            var r = new Mat3(m00, m01, m02, m10, m11, m12, m20, m21, m22).Polar().Rotation;
            var t = center - r * restCenter;
            for (int i = 0; i < count; i++)
                if ((r * At(rest, i) + t - At(co, i)).Length > tolerance) return;   // it really changes shape
            folds[f] = (r, t);
        }

        for (int f = 0; f < _frames; f++)
        {
            if (folds[f] is not var (r, t)) continue;
            // world = A (R p + t) + T with A = turn * stretch: the placement A R, T + A t
            var a = n.Frame[f].R * n.Stretch[f];
            var (rotation, stretch) = (a * r).Polar();
            n.Frame[f] = new RigidFrame(rotation, n.Frame[f].T + a * t);
            n.Stretch[f] = stretch;
        }
        n.Info.Deform = null;
        _rigid++;
    }

    private static Vec3d At(float[] p, int i) => new(p[i * 3], p[i * 3 + 1], p[i * 3 + 2]);

    private static Vec3d Centroid(float[] p, int count)
    {
        var sum = Vec3d.Zero;
        for (int i = 0; i < count; i++) sum += At(p, i);
        return sum * (1.0 / count);
    }

    // ================================================================== primitives

    private readonly Dictionary<Out, Element?> _primitives = [];
    private int _boxes, _rectangles;

    /// <summary>
    /// Overbryce's own box or rectangle for a polygon mesh element that is exactly one: untextured, not changing shape,
    /// centred, its corners on the axes of its (stretched) space; null otherwise.
    /// </summary>
    private Element? Primitive(Out o)
    {
        if (_primitives.TryGetValue(o, out var known)) return known?.Clone();
        Element? shape = null;
        var n = o.Node;
        int material = o.Part!.Material;
        bool textured = material >= 0 && material < _file.Materials.Count && _file.Materials[material].Texture >= 0;
        if (_options.RecognizeShapes && !textured && !n.Deforms && !n.StretchVaries && n.Reference >= 0)
        {
            var mesh = MeshAt(n, o.Part, n.Reference);
            shape = BoxOrRectangle(mesh, o.Name);
            if (shape is BoxElement) _boxes++;
            else if (shape is RectangleElement) _rectangles++;
        }
        _primitives[o] = shape;
        return shape?.Clone();
    }

    internal static Element? BoxOrRectangle(PolyMeshData mesh, string name)
    {
        int triangles = mesh.Indices.Length / 3;
        if (triangles is not (12 or 2)) return null;
        var corners = new List<Vector3>();
        var (lo, hi) = mesh.Bounds;
        var size = hi - lo;
        float eps = 1e-4f * Math.Max(size.Length(), 1e-6f);
        if ((lo + hi).Length() > 2 * eps) return null;   // not centred
        for (int i = 0; i < mesh.VertexCount; i++)
        {
            var p = new Vector3(mesh.Positions[i * 3], mesh.Positions[i * 3 + 1], mesh.Positions[i * 3 + 2]);
            // every coordinate on one side or the other of the bounds
            for (int k = 0; k < 3; k++)
            {
                float v = k == 0 ? p.X : k == 1 ? p.Y : p.Z, l = k == 0 ? lo.X : k == 1 ? lo.Y : lo.Z, h = k == 0 ? hi.X : k == 1 ? hi.Y : hi.Z;
                if (Math.Abs(v - l) > eps && Math.Abs(v - h) > eps) return null;
            }
            if (!corners.Any(c => Vector3.Distance(c, p) < eps)) corners.Add(p);
        }
        if (triangles == 12 && corners.Count == 8 && size.X > eps && size.Y > eps && size.Z > eps)
            return new BoxElement(name) { SizeX = size.X, SizeY = size.Y, SizeZ = size.Z };
        // a rectangle lies in its XY plane facing +Z (as Blender's plane does)
        if (triangles == 2 && corners.Count == 4 && size.Z <= eps && size.X > eps && size.Y > eps)
        {
            for (int t = 0; t < 2; t++)
            {
                Vector3 V(int k) { int i = mesh.Indices[t * 3 + k]; return new(mesh.Positions[i * 3], mesh.Positions[i * 3 + 1], mesh.Positions[i * 3 + 2]); }
                if (Vector3.Cross(V(1) - V(0), V(2) - V(0)).Z <= 0) return null;
            }
            return new RectangleElement(name) { SizeX = size.X, SizeY = size.Y };
        }
        return null;
    }
}
