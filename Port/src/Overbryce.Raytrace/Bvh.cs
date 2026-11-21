using System.Numerics;
using System.Runtime.CompilerServices;

namespace Overbryce.Raytrace;

/// <summary>An axis-aligned box.</summary>
internal struct Bounds
{
    public Vector3 Min;
    public Vector3 Max;

    public static Bounds Empty => new() { Min = new Vector3(float.MaxValue), Max = new Vector3(float.MinValue) };

    public readonly bool IsEmpty => Min.X > Max.X;
    public readonly Vector3 Center => (Min + Max) * 0.5f;
    public readonly Vector3 Size => Max - Min;

    public void Grow(Vector3 p)
    {
        Min = Vector3.Min(Min, p);
        Max = Vector3.Max(Max, p);
    }

    public void Grow(in Bounds b)
    {
        Min = Vector3.Min(Min, b.Min);
        Max = Vector3.Max(Max, b.Max);
    }

    public readonly float Area()
    {
        if (IsEmpty) return 0;
        var d = Max - Min;
        return 2 * (d.X * d.Y + d.Y * d.Z + d.Z * d.X);
    }

    public readonly bool Contains(Vector3 p) =>
        p.X >= Min.X && p.Y >= Min.Y && p.Z >= Min.Z && p.X <= Max.X && p.Y <= Max.Y && p.Z <= Max.Z;

    /// <summary>The box around this box moved by <paramref name="m"/>.</summary>
    public readonly Bounds Transform(in Matrix4x4 m)
    {
        var r = Empty;
        if (IsEmpty) return r;
        for (int i = 0; i < 8; i++)
        {
            var c = new Vector3((i & 1) != 0 ? Max.X : Min.X, (i & 2) != 0 ? Max.Y : Min.Y, (i & 4) != 0 ? Max.Z : Min.Z);
            r.Grow(Vector3.Transform(c, m));
        }
        return r;
    }

    /// <summary>Slab test; <paramref name="invD"/> is 1 / direction.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Hit(in Vector3 min, in Vector3 max, in Vector3 o, in Vector3 invD, float tMin, float tMax, out float tEnter)
    {
        var t0 = (min - o) * invD;
        var t1 = (max - o) * invD;
        var lo = Vector3.Min(t0, t1);
        var hi = Vector3.Max(t0, t1);
        tEnter = MathF.Max(tMin, MathF.Max(lo.X, MathF.Max(lo.Y, lo.Z)));
        float tExit = MathF.Min(tMax, MathF.Min(hi.X, MathF.Min(hi.Y, hi.Z)));
        return tEnter <= tExit;
    }

    /// <summary>1 / d with zero components replaced by a tiny value of the same sign (no NaN in the slab test).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Inverse(Vector3 d)
    {
        const float tiny = 1e-20f;
        return new Vector3(
            1f / (MathF.Abs(d.X) > tiny ? d.X : (d.X >= 0 ? tiny : -tiny)),
            1f / (MathF.Abs(d.Y) > tiny ? d.Y : (d.Y >= 0 ? tiny : -tiny)),
            1f / (MathF.Abs(d.Z) > tiny ? d.Z : (d.Z >= 0 ? tiny : -tiny)));
    }
}

/// <summary>
/// A node of a bounding volume hierarchy: a leaf holds <see cref="Count"/> primitives from
/// <see cref="Start"/>; an inner node has its two children at <see cref="Start"/> and Start + 1.
/// </summary>
internal struct BvhNode
{
    public Vector3 Min;
    public int Start;
    public Vector3 Max;
    public int Count;
}

/// <summary>Builds a BVH with the surface area heuristic over binned centroids.</summary>
internal static class BvhBuilder
{
    private const int Bins = 16;

    /// <summary>Returns the nodes (root first) and the primitive order the leaves refer to.</summary>
    public static (BvhNode[] Nodes, int[] Order) Build(Bounds[] prims, int maxLeaf = 4)
    {
        int n = prims.Length;
        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        var centers = new Vector3[n];
        for (int i = 0; i < n; i++) centers[i] = prims[i].Center;

        var nodes = new List<BvhNode>(Math.Max(1, 2 * n / maxLeaf + 1)) { default };
        var stack = new Stack<(int Node, int Start, int Count)>();
        stack.Push((0, 0, n));

        Span<Bounds> binBounds = stackalloc Bounds[Bins];
        Span<int> binCount = stackalloc int[Bins];
        Span<float> rightArea = stackalloc float[Bins];
        Span<int> rightCount = stackalloc int[Bins];

        while (stack.Count > 0)
        {
            var (index, start, count) = stack.Pop();
            var box = Bounds.Empty;
            var cbox = Bounds.Empty;
            for (int i = start; i < start + count; i++)
            {
                box.Grow(prims[order[i]]);
                cbox.Grow(centers[order[i]]);
            }

            int split = -1, axis = -1;
            if (count > maxLeaf)
            {
                float best = box.Area() * count; // cost of a leaf
                var ext = cbox.Size;
                for (int a = 0; a < 3; a++)
                {
                    float lo = a == 0 ? cbox.Min.X : a == 1 ? cbox.Min.Y : cbox.Min.Z;
                    float len = a == 0 ? ext.X : a == 1 ? ext.Y : ext.Z;
                    if (len <= 1e-12f) continue;
                    float scale = Bins / len;
                    for (int b = 0; b < Bins; b++) { binBounds[b] = Bounds.Empty; binCount[b] = 0; }
                    for (int i = start; i < start + count; i++)
                    {
                        int b = Bin(centers[order[i]], a, lo, scale);
                        binBounds[b].Grow(prims[order[i]]);
                        binCount[b]++;
                    }
                    var acc = Bounds.Empty;
                    int accCount = 0;
                    for (int b = Bins - 1; b > 0; b--)
                    {
                        acc.Grow(binBounds[b]);
                        accCount += binCount[b];
                        rightArea[b] = acc.Area();
                        rightCount[b] = accCount;
                    }
                    acc = Bounds.Empty;
                    accCount = 0;
                    for (int b = 0; b < Bins - 1; b++)
                    {
                        acc.Grow(binBounds[b]);
                        accCount += binCount[b];
                        if (accCount == 0 || rightCount[b + 1] == 0) continue;
                        float cost = acc.Area() * accCount + rightArea[b + 1] * rightCount[b + 1];
                        if (cost < best)
                        {
                            best = cost;
                            axis = a;
                            split = b + 1;
                        }
                    }
                }
            }

            if (axis < 0)
            {
                // a leaf; split by count anyway when it is far too big (all centroids in one place)
                if (count > 4 * maxLeaf)
                {
                    int half = count / 2;
                    MakeInner(nodes, index, box, start, half, count, stack);
                    continue;
                }
                nodes[index] = new BvhNode { Min = box.Min, Max = box.Max, Start = start, Count = count };
                continue;
            }

            float lo2 = axis == 0 ? cbox.Min.X : axis == 1 ? cbox.Min.Y : cbox.Min.Z;
            float len2 = axis == 0 ? cbox.Size.X : axis == 1 ? cbox.Size.Y : cbox.Size.Z;
            float scale2 = Bins / len2;
            int left = start, right = start + count - 1;
            while (left <= right)
            {
                if (Bin(centers[order[left]], axis, lo2, scale2) < split)
                {
                    left++;
                }
                else
                {
                    (order[left], order[right]) = (order[right], order[left]);
                    right--;
                }
            }
            int leftCount = left - start;
            if (leftCount == 0 || leftCount == count) leftCount = count / 2;
            MakeInner(nodes, index, box, start, leftCount, count, stack);
        }
        return (nodes.ToArray(), order);
    }

    private static void MakeInner(List<BvhNode> nodes, int index, in Bounds box, int start, int leftCount, int count,
        Stack<(int, int, int)> stack)
    {
        int child = nodes.Count;
        nodes.Add(default);
        nodes.Add(default);
        nodes[index] = new BvhNode { Min = box.Min, Max = box.Max, Start = child, Count = 0 };
        stack.Push((child, start, leftCount));
        stack.Push((child + 1, start + leftCount, count - leftCount));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Bin(Vector3 c, int axis, float lo, float scale)
    {
        float v = axis == 0 ? c.X : axis == 1 ? c.Y : c.Z;
        return Math.Clamp((int)((v - lo) * scale), 0, Bins - 1);
    }
}

/// <summary>Deterministic random numbers (PCG) seeded from the pixel, the sample and the frame.</summary>
internal struct Rng
{
    private uint _state;

    public Rng(uint seed) => _state = Hash(seed);

    public Rng(int x, int y, int sample, uint frame) => _state = Hash(Hash(Hash((uint)x * 73856093u ^ (uint)y * 19349663u) ^ (uint)sample * 83492791u) ^ frame);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Hash(uint v)
    {
        uint state = v * 747796405u + 2891336453u;
        uint word = ((state >> (int)((state >> 28) + 4u)) ^ state) * 277803737u;
        return (word >> 22) ^ word;
    }

    /// <summary>Uniform in [0, 1).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Next()
    {
        _state = _state * 747796405u + 2891336453u;
        uint word = ((_state >> (int)((_state >> 28) + 4u)) ^ _state) * 277803737u;
        word = (word >> 22) ^ word;
        return (word >> 8) * (1f / 16777216f);
    }
}

internal static class Sampling
{
    /// <summary>Point in the unit disk (concentric mapping of the unit square).</summary>
    public static Vector2 Disk(float u, float v)
    {
        float a = 2 * u - 1, b = 2 * v - 1;
        if (a == 0 && b == 0) return Vector2.Zero;
        float r, phi;
        if (MathF.Abs(a) > MathF.Abs(b))
        {
            r = a;
            phi = MathF.PI / 4 * (b / a);
        }
        else
        {
            r = b;
            phi = MathF.PI / 2 - MathF.PI / 4 * (a / b);
        }
        return new Vector2(r * MathF.Cos(phi), r * MathF.Sin(phi));
    }

    /// <summary>A direction around <paramref name="n"/> with a cosine-weighted distribution.</summary>
    public static Vector3 CosineHemisphere(Vector3 n, float u, float v)
    {
        var d = Disk(u, v);
        float z = MathF.Sqrt(MathF.Max(0, 1 - d.LengthSquared()));
        Basis(n, out var t, out var b);
        return t * d.X + b * d.Y + n * z;
    }

    /// <summary>A uniformly distributed point in the unit ball.</summary>
    public static Vector3 Ball(ref Rng rng)
    {
        float z = 2 * rng.Next() - 1;
        float phi = 2 * MathF.PI * rng.Next();
        float r = MathF.Sqrt(MathF.Max(0, 1 - z * z));
        float len = MathF.Cbrt(rng.Next());
        return new Vector3(r * MathF.Cos(phi), r * MathF.Sin(phi), z) * len;
    }

    /// <summary>Two unit vectors perpendicular to <paramref name="n"/> and to each other.</summary>
    public static void Basis(Vector3 n, out Vector3 t, out Vector3 b)
    {
        // Duff et al., "Building an orthonormal basis, revisited"
        float sign = n.Z >= 0 ? 1f : -1f;
        float a = -1f / (sign + n.Z);
        float bb = n.X * n.Y * a;
        t = new Vector3(1f + sign * n.X * n.X * a, sign * bb, -sign * n.X);
        b = new Vector3(bb, sign + n.Y * n.Y * a, -n.Y);
    }

    /// <summary>The R2 low-discrepancy sequence (Roberts), shifted per pixel.</summary>
    public static Vector2 R2(int index, float shiftX, float shiftY)
    {
        const double g = 1.32471795724474602596;
        const double a1 = 1 / g, a2 = 1 / (g * g);
        double x = shiftX + a1 * index, y = shiftY + a2 * index;
        return new Vector2((float)(x - Math.Floor(x)), (float)(y - Math.Floor(y)));
    }
}
