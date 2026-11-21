using System.Numerics;
using System.Runtime.CompilerServices;
using Overbryce.Core.Geometry;

namespace Overbryce.Raytrace;

/// <summary>The surface at a hit, in the shape's own (object) space.</summary>
internal struct SurfacePoint
{
    /// <summary>Position.</summary>
    public Vector3 P;

    /// <summary>Shading normal (interpolated like OpenGL's; not normalised).</summary>
    public Vector3 N;

    /// <summary>Normal of the real surface (the facet), on the same side as <see cref="N"/>.</summary>
    public Vector3 Ng;

    /// <summary>Texture coordinates.</summary>
    public Vector2 Uv;

    /// <summary>Where shadow rays start: the position lifted off the facets onto the smooth surface the normals describe.</summary>
    public Vector3 ShadowP;
}

/// <summary>Decides whether a candidate hit counts (transparent texels do not).</summary>
internal abstract class HitFilter
{
    public abstract bool Accept(Shape shape, int prim, Vector2 bary, in Vector3 o, in Vector3 d, float t);
}

/// <summary>Sees every hit along a ray segment, in no particular order; returns true to stop.</summary>
internal abstract class HitVisitor
{
    public abstract bool Visit(Shape shape, int prim, Vector2 bary, in Vector3 o, in Vector3 d, float t);
}

/// <summary>A shape in its own space. Rays are not normalised; t is measured in units of the direction.</summary>
internal abstract class Shape
{
    public Bounds Bounds;

    /// <summary>Closed (sphere, box, torus): glass bends the light where the ray enters and where it leaves.</summary>
    public abstract bool Solid { get; }

    /// <summary>Approximate memory use in bytes (for the geometry cache).</summary>
    public virtual long Bytes => 256;

    /// <summary>Finds the nearest accepted hit in (tMin, tMax); on success tMax becomes its t.</summary>
    public abstract bool Closest(in Vector3 o, in Vector3 d, float tMin, ref float tMax, ref int prim, ref Vector2 bary, HitFilter? filter);

    /// <summary>Calls the visitor for each hit in (tMin, tMax); returns true when the visitor stopped.</summary>
    public abstract bool Visit(in Vector3 o, in Vector3 d, float tMin, float tMax, HitVisitor visitor);

    public abstract void Surface(in Vector3 o, in Vector3 d, float t, int prim, Vector2 bary, out SurfacePoint sp);

    /// <summary>The order in which OpenGL draws the primitive within the element (0 for exact shapes).</summary>
    public virtual int DrawIndex(int prim) => 0;
}

/// <summary>Triangles from the shared tessellation, with their own BVH.</summary>
internal sealed class TriangleMesh : Shape
{
    private struct Tri
    {
        public Vector3 V0, E1, E2;
        public int A, B, C;
        public int Order; // position in the tessellation, which is OpenGL's drawing order
    }

    private readonly Tri[] _tris;
    private readonly BvhNode[] _nodes;
    private readonly Vector3[] _p;
    private readonly Vector3[] _n;
    private readonly Vector2[] _uv;
    private readonly bool _solid;

    public TriangleMesh(ReadOnlySpan<float> data, IEnumerable<DrawRange> ranges, bool solid)
    {
        _solid = solid;
        const int F = GeometryBuilder.FloatsPerVertex;
        int count = data.Length / F;
        _p = new Vector3[count];
        _n = new Vector3[count];
        _uv = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            int o = i * F;
            _p[i] = new Vector3(data[o], data[o + 1], data[o + 2]);
            _n[i] = new Vector3(data[o + 3], data[o + 4], data[o + 5]);
            _uv[i] = new Vector2(data[o + 6], data[o + 7]);
        }

        var list = new List<Tri>();
        var p = _p;
        GeometryBuilder.Triangles(ranges, (a, b, c) =>
        {
            var e1 = p[b] - p[a];
            var e2 = p[c] - p[a];
            var cross = Vector3.Cross(e1, e2);
            if (!(cross.LengthSquared() > 1e-30f)) return; // degenerate (strip joins, poles)
            list.Add(new Tri { V0 = p[a], E1 = e1, E2 = e2, A = a, B = b, C = c, Order = list.Count });
        });

        var boxes = new Bounds[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            var b = Bounds.Empty;
            b.Grow(list[i].V0);
            b.Grow(list[i].V0 + list[i].E1);
            b.Grow(list[i].V0 + list[i].E2);
            boxes[i] = b;
        }
        var (nodes, order) = BvhBuilder.Build(boxes);
        _nodes = nodes;
        _tris = new Tri[list.Count];
        for (int i = 0; i < order.Length; i++) _tris[i] = list[order[i]];
        Bounds = list.Count > 0 ? new Bounds { Min = nodes[0].Min, Max = nodes[0].Max } : Bounds.Empty;
    }

    public int TriangleCount => _tris.Length;

    public override int DrawIndex(int prim) => _tris[prim].Order;

    public override bool Solid => _solid;

    public override long Bytes => _tris.LongLength * 52 + _nodes.LongLength * 32 + _p.LongLength * 32;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HitTri(in Tri tri, in Vector3 o, in Vector3 d, out float t, out float u, out float v)
    {
        var p = Vector3.Cross(d, tri.E2);
        float det = Vector3.Dot(tri.E1, p);
        t = u = v = 0;
        if (det == 0 || !float.IsFinite(det)) return false;
        float inv = 1f / det;
        var s = o - tri.V0;
        u = Vector3.Dot(s, p) * inv;
        if (u < 0 || u > 1) return false;
        var q = Vector3.Cross(s, tri.E1);
        v = Vector3.Dot(d, q) * inv;
        if (v < 0 || u + v > 1) return false;
        t = Vector3.Dot(tri.E2, q) * inv;
        return true;
    }

    public override bool Closest(in Vector3 o, in Vector3 d, float tMin, ref float tMax, ref int prim, ref Vector2 bary, HitFilter? filter)
    {
        if (_tris.Length == 0) return false;
        var inv = Bounds.Inverse(d);
        Span<int> stack = stackalloc int[128];
        Span<float> enter = stackalloc float[128];
        int sp = 0;
        if (!Bounds.Hit(_nodes[0].Min, _nodes[0].Max, o, inv, tMin, tMax, out float t0)) return false;
        stack[sp] = 0;
        enter[sp++] = t0;
        bool hit = false;
        while (sp > 0)
        {
            sp--;
            if (enter[sp] > tMax) continue;
            ref readonly var node = ref _nodes[stack[sp]];
            if (node.Count > 0)
            {
                for (int i = node.Start, end = node.Start + node.Count; i < end; i++)
                {
                    if (!HitTri(_tris[i], o, d, out float t, out float u, out float v) || t <= tMin || t >= tMax) continue;
                    if (filter != null && !filter.Accept(this, i, new Vector2(u, v), o, d, t)) continue;
                    tMax = t;
                    prim = i;
                    bary = new Vector2(u, v);
                    hit = true;
                }
                continue;
            }
            int l = node.Start, r = l + 1;
            bool hl = Bounds.Hit(_nodes[l].Min, _nodes[l].Max, o, inv, tMin, tMax, out float tl);
            bool hr = Bounds.Hit(_nodes[r].Min, _nodes[r].Max, o, inv, tMin, tMax, out float tr);
            if (hl && hr)
            {
                // the nearer child is popped first
                if (tl <= tr) { stack[sp] = r; enter[sp++] = tr; stack[sp] = l; enter[sp++] = tl; }
                else { stack[sp] = l; enter[sp++] = tl; stack[sp] = r; enter[sp++] = tr; }
            }
            else if (hl) { stack[sp] = l; enter[sp++] = tl; }
            else if (hr) { stack[sp] = r; enter[sp++] = tr; }
        }
        return hit;
    }

    public override bool Visit(in Vector3 o, in Vector3 d, float tMin, float tMax, HitVisitor visitor)
    {
        if (_tris.Length == 0) return false;
        var inv = Bounds.Inverse(d);
        Span<int> stack = stackalloc int[128];
        int sp = 0;
        stack[sp++] = 0;
        while (sp > 0)
        {
            ref readonly var node = ref _nodes[stack[--sp]];
            if (!Bounds.Hit(node.Min, node.Max, o, inv, tMin, tMax, out _)) continue;
            if (node.Count > 0)
            {
                for (int i = node.Start, end = node.Start + node.Count; i < end; i++)
                {
                    if (!HitTri(_tris[i], o, d, out float t, out float u, out float v) || t <= tMin || t >= tMax) continue;
                    if (visitor.Visit(this, i, new Vector2(u, v), o, d, t)) return true;
                }
                continue;
            }
            if (sp + 2 > stack.Length) continue;
            stack[sp++] = node.Start;
            stack[sp++] = node.Start + 1;
        }
        return false;
    }

    public override void Surface(in Vector3 o, in Vector3 d, float t, int prim, Vector2 bary, out SurfacePoint sp)
    {
        ref readonly var tri = ref _tris[prim];
        float u = bary.X, v = bary.Y, w = 1 - u - v;
        sp.P = tri.V0 + tri.E1 * u + tri.E2 * v;
        sp.N = _n[tri.A] * w + _n[tri.B] * u + _n[tri.C] * v;
        sp.Uv = _uv[tri.A] * w + _uv[tri.B] * u + _uv[tri.C] * v;
        var ng = Vector3.Cross(tri.E1, tri.E2);
        if (!(sp.N.LengthSquared() > 1e-30f)) sp.N = ng;
        sp.Ng = Vector3.Dot(ng, sp.N) < 0 ? -ng : ng;

        // "Hacking the shadow terminator" (Hanika 2021): lift the point onto the tangent planes of the
        // vertices, so coarse facets do not shadow the smooth surface their normals describe
        var sum = Vector3.Zero;
        sum += Lift(sp.P, _p[tri.A], _n[tri.A]) * w;
        sum += Lift(sp.P, _p[tri.B], _n[tri.B]) * u;
        sum += Lift(sp.P, _p[tri.C], _n[tri.C]) * v;
        sp.ShadowP = sp.P + sum;
    }

    private static Vector3 Lift(Vector3 p, Vector3 vertex, Vector3 normal)
    {
        float len = normal.Length();
        if (!(len > 1e-20f)) return Vector3.Zero;
        var n = normal / len;
        float dot = Vector3.Dot(p - vertex, n);
        return dot < 0 ? -dot * n : Vector3.Zero;
    }
}

/// <summary>The unit sphere (gluSphere: the pole on +Z, texture as GLU maps it).</summary>
internal sealed class SphereShape : Shape
{
    public SphereShape() => Bounds = new Bounds { Min = new Vector3(-1), Max = new Vector3(1) };

    public override bool Solid => true;

    private static int Roots(in Vector3 o, in Vector3 d, out float t0, out float t1)
    {
        float a = Vector3.Dot(d, d), b = Vector3.Dot(o, d), c = Vector3.Dot(o, o) - 1;
        float disc = b * b - a * c;
        t0 = t1 = 0;
        if (disc < 0 || a == 0) return 0;
        float s = MathF.Sqrt(disc);
        // numerically stable form
        float q = b >= 0 ? -(b + s) : -(b - s);
        float r0 = q / a, r1 = q != 0 ? c / q : r0;
        t0 = MathF.Min(r0, r1);
        t1 = MathF.Max(r0, r1);
        return 2;
    }

    public override bool Closest(in Vector3 o, in Vector3 d, float tMin, ref float tMax, ref int prim, ref Vector2 bary, HitFilter? filter)
    {
        if (Roots(o, d, out float t0, out float t1) == 0) return false;
        foreach (float t in (ReadOnlySpan<float>)[t0, t1])
        {
            if (t <= tMin || t >= tMax) continue;
            if (filter != null && !filter.Accept(this, 0, default, o, d, t)) continue;
            tMax = t;
            prim = 0;
            return true;
        }
        return false;
    }

    public override bool Visit(in Vector3 o, in Vector3 d, float tMin, float tMax, HitVisitor visitor)
    {
        if (Roots(o, d, out float t0, out float t1) == 0) return false;
        if (t0 > tMin && t0 < tMax && visitor.Visit(this, 0, default, o, d, t0)) return true;
        return t1 > tMin && t1 < tMax && visitor.Visit(this, 0, default, o, d, t1);
    }

    public override void Surface(in Vector3 o, in Vector3 d, float t, int prim, Vector2 bary, out SurfacePoint sp)
    {
        var p = o + d * t;
        var n = p.LengthSquared() > 0 ? Vector3.Normalize(p) : Vector3.UnitZ;
        sp.P = n;
        sp.N = sp.Ng = n;
        sp.ShadowP = n;
        float theta = MathF.Atan2(-n.X, n.Y);
        if (theta < 0) theta += 2 * MathF.PI;
        sp.Uv = new Vector2(theta / (2 * MathF.PI), 1 - MathF.Acos(Math.Clamp(n.Z, -1f, 1f)) / MathF.PI);
    }
}

/// <summary>gluCylinder with base radius 1 at z = 0 and top radius <c>k</c> at z = 1 (no caps).</summary>
internal sealed class ConeShape : Shape
{
    private readonly float _m; // radius slope: r(z) = 1 + m z

    public ConeShape(float topRatio)
    {
        _m = topRatio - 1;
        float r = MathF.Max(1, MathF.Abs(topRatio));
        Bounds = new Bounds { Min = new Vector3(-r, -r, 0), Max = new Vector3(r, r, 1) };
    }

    public override bool Solid => false;

    private int Roots(in Vector3 o, in Vector3 d, Span<float> t)
    {
        float m = _m;
        float ro = 1 + m * o.Z;
        float a = d.X * d.X + d.Y * d.Y - m * m * d.Z * d.Z;
        float b = 2 * (o.X * d.X + o.Y * d.Y - m * d.Z * ro);
        float c = o.X * o.X + o.Y * o.Y - ro * ro;
        int n = 0;
        float scale = d.LengthSquared();
        if (MathF.Abs(a) <= 1e-9f * scale)
        {
            if (b != 0) t[n++] = -c / b;
        }
        else
        {
            float disc = b * b - 4 * a * c;
            if (disc < 0) return 0;
            float s = MathF.Sqrt(disc);
            float q = b >= 0 ? -0.5f * (b + s) : -0.5f * (b - s);
            float r0 = q / a, r1 = q != 0 ? c / q : r0;
            t[n++] = MathF.Min(r0, r1);
            t[n++] = MathF.Max(r0, r1);
        }
        // only the part between z = 0 and z = 1
        int k = 0;
        for (int i = 0; i < n; i++)
        {
            float z = o.Z + d.Z * t[i];
            if (z >= 0 && z <= 1) t[k++] = t[i];
        }
        return k;
    }

    public override bool Closest(in Vector3 o, in Vector3 d, float tMin, ref float tMax, ref int prim, ref Vector2 bary, HitFilter? filter)
    {
        Span<float> t = stackalloc float[2];
        int n = Roots(o, d, t);
        for (int i = 0; i < n; i++)
        {
            if (t[i] <= tMin || t[i] >= tMax) continue;
            if (filter != null && !filter.Accept(this, 0, default, o, d, t[i])) continue;
            tMax = t[i];
            prim = 0;
            return true;
        }
        return false;
    }

    public override bool Visit(in Vector3 o, in Vector3 d, float tMin, float tMax, HitVisitor visitor)
    {
        Span<float> t = stackalloc float[2];
        int n = Roots(o, d, t);
        for (int i = 0; i < n; i++)
            if (t[i] > tMin && t[i] < tMax && visitor.Visit(this, 0, default, o, d, t[i]))
                return true;
        return false;
    }

    public override void Surface(in Vector3 o, in Vector3 d, float t, int prim, Vector2 bary, out SurfacePoint sp)
    {
        var p = o + d * t;
        float a = MathF.Atan2(p.X, p.Y);
        if (a < 0) a += 2 * MathF.PI;
        // GLU's normal: (sin a, cos a, (base - top) / height)
        var n = new Vector3(MathF.Sin(a), MathF.Cos(a), -_m);
        sp.P = p;
        sp.N = sp.Ng = n;
        sp.ShadowP = p;
        sp.Uv = new Vector2(a / (2 * MathF.PI), p.Z);
    }
}

/// <summary>gluPartialDisk with outer radius 1 in the z = 0 plane; angles from +Y towards +X.</summary>
internal sealed class DiskShape : Shape
{
    private readonly float _rMin, _rMax, _sweep;

    public DiskShape(float innerRatio, float sweepDegrees)
    {
        _rMin = Math.Clamp(MathF.Min(innerRatio, 1), 0, float.MaxValue);
        _rMax = MathF.Max(innerRatio, 1);
        _sweep = Math.Clamp(sweepDegrees, -360, 360) * MathF.PI / 180;
        Bounds = new Bounds { Min = new Vector3(-_rMax, -_rMax, -1e-4f), Max = new Vector3(_rMax, _rMax, 1e-4f) };
    }

    public override bool Solid => false;

    private bool Hit(in Vector3 o, in Vector3 d, out float t)
    {
        t = 0;
        if (d.Z == 0) return false;
        t = -o.Z / d.Z;
        float x = o.X + d.X * t, y = o.Y + d.Y * t;
        float r2 = x * x + y * y;
        if (r2 < _rMin * _rMin || r2 > _rMax * _rMax) return false;
        if (MathF.Abs(_sweep) >= 2 * MathF.PI - 1e-6f) return true;
        float a = MathF.Atan2(x, y);
        if (_sweep > 0) return (a < 0 ? a + 2 * MathF.PI : a) <= _sweep;
        return (a > 0 ? a - 2 * MathF.PI : a) >= _sweep;
    }

    public override bool Closest(in Vector3 o, in Vector3 d, float tMin, ref float tMax, ref int prim, ref Vector2 bary, HitFilter? filter)
    {
        if (!Hit(o, d, out float t) || t <= tMin || t >= tMax) return false;
        if (filter != null && !filter.Accept(this, 0, default, o, d, t)) return false;
        tMax = t;
        prim = 0;
        return true;
    }

    public override bool Visit(in Vector3 o, in Vector3 d, float tMin, float tMax, HitVisitor visitor) =>
        Hit(o, d, out float t) && t > tMin && t < tMax && visitor.Visit(this, 0, default, o, d, t);

    public override void Surface(in Vector3 o, in Vector3 d, float t, int prim, Vector2 bary, out SurfacePoint sp)
    {
        var p = o + d * t;
        p.Z = 0;
        sp.P = p;
        sp.N = sp.Ng = Vector3.UnitZ;
        sp.ShadowP = p;
        sp.Uv = new Vector2(0.5f + p.X / 2, 0.5f + p.Y / 2);
    }
}

/// <summary>A torus around the Z axis with ring radius 1 and tube radius <c>r</c>, intersected by sphere tracing its exact distance field.</summary>
internal sealed class TorusShape : Shape
{
    private const int MaxSteps = 400;
    private readonly float _r;
    private readonly float _eps;

    public TorusShape(float tubeRatio)
    {
        _r = MathF.Abs(tubeRatio);
        float e = 1 + _r;
        Bounds = new Bounds { Min = new Vector3(-e, -e, -_r), Max = new Vector3(e, e, _r) };
        _eps = 2e-5f * e;
    }

    public override bool Solid => true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float Distance(Vector3 p)
    {
        float q = MathF.Sqrt(p.X * p.X + p.Y * p.Y) - 1;
        return MathF.Sqrt(q * q + p.Z * p.Z) - _r;
    }

    /// <summary>The next surface crossing after <paramref name="from"/> (in normalised units) up to <paramref name="to"/>.</summary>
    private bool March(in Vector3 o, in Vector3 dn, float from, float to, bool stepOff, out float hit)
    {
        hit = 0;
        float t = from;
        // step off a surface the ray starts on (not where it enters the bounding box: that can be a hit)
        if (stepOff && MathF.Abs(Distance(o + dn * t)) < _eps) t += 4 * _eps;
        for (int i = 0; i < MaxSteps && t <= to; i++)
        {
            float dist = MathF.Abs(Distance(o + dn * t));
            if (dist < _eps)
            {
                hit = t;
                return true;
            }
            t += MathF.Max(dist, _eps * 0.5f);
        }
        return false;
    }

    private bool Range(in Vector3 o, in Vector3 dn, float tMin, float tMax, out float from, out float to)
    {
        to = tMax;
        if (!Bounds.Hit(Bounds.Min, Bounds.Max, o, Bounds.Inverse(dn), tMin, tMax, out from)) return false;
        var inv = Bounds.Inverse(dn);
        var t0 = (Bounds.Min - o) * inv;
        var t1 = (Bounds.Max - o) * inv;
        var hi = Vector3.Max(t0, t1);
        to = MathF.Min(tMax, MathF.Min(hi.X, MathF.Min(hi.Y, hi.Z)));
        return from <= to;
    }

    public override bool Closest(in Vector3 o, in Vector3 d, float tMin, ref float tMax, ref int prim, ref Vector2 bary, HitFilter? filter)
    {
        float len = d.Length();
        if (!(len > 0)) return false;
        var dn = d / len;
        if (!Range(o, dn, tMin * len, tMax * len, out float from, out float to)) return false;
        float start = MathF.Max(from, tMin * len);
        bool stepOff = from <= tMin * len;
        while (March(o, dn, start, to, stepOff, out float hit))
        {
            stepOff = false;
            float t = hit / len;
            if (t > tMin && t < tMax && (filter == null || filter.Accept(this, 0, default, o, d, t)))
            {
                tMax = t;
                prim = 0;
                return true;
            }
            start = hit + 4 * _eps;
        }
        return false;
    }

    public override bool Visit(in Vector3 o, in Vector3 d, float tMin, float tMax, HitVisitor visitor)
    {
        float len = d.Length();
        if (!(len > 0)) return false;
        var dn = d / len;
        if (!Range(o, dn, tMin * len, tMax * len, out float from, out float to)) return false;
        float start = MathF.Max(from, tMin * len);
        for (int i = 0; i < 8 && March(o, dn, start, to, i == 0 && from <= tMin * len, out float hit); i++)
        {
            float t = hit / len;
            if (t > tMin && t < tMax && visitor.Visit(this, 0, default, o, d, t)) return true;
            start = hit + 4 * _eps;
        }
        return false;
    }

    public override void Surface(in Vector3 o, in Vector3 d, float t, int prim, Vector2 bary, out SurfacePoint sp)
    {
        var p = o + d * t;
        float len = MathF.Sqrt(p.X * p.X + p.Y * p.Y);
        float q = len - 1;
        var n = len > 1e-12f ? new Vector3(q * p.X / len, q * p.Y / len, p.Z) : new Vector3(0, 0, p.Z);
        if (!(n.LengthSquared() > 1e-30f)) n = Vector3.UnitZ;
        sp.P = p;
        sp.N = sp.Ng = n;
        sp.ShadowP = p;
        sp.Uv = default; // the torus is sphere mapped
    }
}
