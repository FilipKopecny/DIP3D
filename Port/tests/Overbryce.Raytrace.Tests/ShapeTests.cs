using System.Numerics;
using Overbryce.Core.Geometry;

namespace Overbryce.Raytrace.Tests;

/// <summary>Ray intersections of the shapes, and the BVH against testing every triangle.</summary>
public class ShapeTests
{
    private static bool Hit(Shape shape, Vector3 o, Vector3 d, out float t)
    {
        t = float.PositiveInfinity;
        int prim = 0;
        var bary = Vector2.Zero;
        return shape.Closest(o, d, 1e-4f, ref t, ref prim, ref bary, null);
    }

    [Fact]
    public void Sphere_is_hit_at_its_surface()
    {
        var s = new SphereShape();
        Assert.True(Hit(s, new Vector3(0, 0, 5), new Vector3(0, 0, -1), out float t));
        Assert.Equal(4f, t, 4);
        Assert.True(Hit(s, new Vector3(0, 0, 5), new Vector3(0, 0, -2), out t)); // t in units of the direction
        Assert.Equal(2f, t, 4);
        Assert.True(Hit(s, Vector3.Zero, Vector3.UnitX, out t)); // from inside
        Assert.Equal(1f, t, 4);
        Assert.False(Hit(s, new Vector3(0, 1.01f, 5), new Vector3(0, 0, -1), out _));
    }

    [Fact]
    public void Cone_follows_its_radius_and_ends_at_its_height()
    {
        var c = new ConeShape(0.5f); // radius 1 at z = 0, 0.5 at z = 1
        Assert.True(Hit(c, new Vector3(5, 0, 0.5f), new Vector3(-1, 0, 0), out float t));
        Assert.Equal(5 - 0.75f, t, 4);
        Assert.False(Hit(c, new Vector3(5, 0, 1.2f), new Vector3(-1, 0, 0), out _));
        Assert.False(Hit(c, new Vector3(0, 0, 5), new Vector3(0, 0, -1), out _)); // open ends
    }

    [Fact]
    public void Disk_has_its_hole_and_sweep()
    {
        var ring = new DiskShape(0.5f, 360);
        Assert.False(Hit(ring, new Vector3(0.25f, 0, 1), new Vector3(0, 0, -1), out _));
        Assert.True(Hit(ring, new Vector3(0.75f, 0, 1), new Vector3(0, 0, -1), out float t));
        Assert.Equal(1f, t, 5);

        var quarter = new DiskShape(0, 90); // from +Y towards +X, as gluPartialDisk
        Assert.True(Hit(quarter, new Vector3(0.7f, 0.1f, 1), new Vector3(0, 0, -1), out _));
        Assert.False(Hit(quarter, new Vector3(-0.7f, 0.1f, 1), new Vector3(0, 0, -1), out _));
        Assert.False(Hit(quarter, new Vector3(0.1f, -0.7f, 1), new Vector3(0, 0, -1), out _));
    }

    [Fact]
    public void Torus_is_hit_on_the_tube_and_not_in_the_hole()
    {
        var torus = new TorusShape(0.25f);
        Assert.True(Hit(torus, new Vector3(0, 1, 5), new Vector3(0, 0, -1), out float t));
        Assert.Equal(4.75f, t, 3);
        Assert.False(Hit(torus, new Vector3(0, 0, 5), new Vector3(0, 0, -1), out _));
        Assert.True(Hit(torus, new Vector3(5, 0, 0), new Vector3(-1, 0, 0), out t)); // the outer side first
        Assert.Equal(5 - 1.25f, t, 3);
    }

    [Fact]
    public void Faceted_and_smooth_sphere_agree_closely()
    {
        var g = new GeometryBuilder();
        var ranges = new List<DrawRange>();
        g.Sphere(ranges, 1, 64, 32, false);
        var mesh = new TriangleMesh(g.Data, ranges, solid: true);
        var smooth = new SphereShape();
        var rng = new Rng(7);
        for (int i = 0; i < 200; i++)
        {
            var o = new Vector3(rng.Next() * 1.2f - 0.6f, rng.Next() * 1.2f - 0.6f, 3);
            bool a = Hit(mesh, o, -Vector3.UnitZ, out float ta);
            bool b = Hit(smooth, o, -Vector3.UnitZ, out float tb);
            Assert.True(a && b);
            Assert.InRange(ta - tb, 0, 0.02f); // the facets lie inside the sphere
        }
    }

    [Fact]
    public void Bvh_finds_the_same_hits_as_testing_every_triangle()
    {
        var g = new GeometryBuilder();
        var ranges = new List<DrawRange>();
        g.Torus(ranges, 0.3, 24, 1, 48, false);
        g.Box(ranges, 0.5, 2, 0.5, false);
        var mesh = new TriangleMesh(g.Data, ranges, solid: false);

        var data = g.Data.ToArray();
        Vector3 P(int v) => new(data[v * 8], data[v * 8 + 1], data[v * 8 + 2]);
        var tris = new List<(Vector3, Vector3, Vector3)>();
        GeometryBuilder.Triangles(ranges, (a, b, c) => tris.Add((P(a), P(b), P(c))));

        var rng = new Rng(11);
        int hits = 0;
        for (int i = 0; i < 2000; i++)
        {
            var o = new Vector3(rng.Next() * 6 - 3, rng.Next() * 6 - 3, rng.Next() * 6 - 3);
            var target = new Vector3(rng.Next() * 2 - 1, rng.Next() * 2 - 1, rng.Next() * 0.6f - 0.3f);
            var d = target - o;
            bool found = Hit(mesh, o, d, out float t);
            float best = float.PositiveInfinity;
            foreach (var (a, b, c) in tris)
                if (Brute(o, d, a, b, c, out float tt) && tt > 1e-4f && tt < best)
                    best = tt;
            Assert.Equal(float.IsFinite(best), found);
            if (found)
            {
                Assert.Equal(best, t, 4);
                hits++;
            }
        }
        Assert.True(hits > 300);
    }

    private static bool Brute(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
    {
        t = 0;
        var e1 = b - a;
        var e2 = c - a;
        var p = Vector3.Cross(d, e2);
        float det = Vector3.Dot(e1, p);
        if (det == 0) return false;
        float inv = 1f / det; // the same arithmetic as the mesh, so edge cases agree
        var s = o - a;
        float u = Vector3.Dot(s, p) * inv;
        if (u < 0 || u > 1) return false;
        var q = Vector3.Cross(s, e1);
        float v = Vector3.Dot(d, q) * inv;
        if (v < 0 || u + v > 1) return false;
        t = Vector3.Dot(e2, q) * inv;
        return true;
    }

    [Fact]
    public void Visit_sees_both_sides_of_a_closed_shape()
    {
        var hits = new List<float>();
        var visitor = new Collect(hits);
        new SphereShape().Visit(new Vector3(0, 0, 5), -Vector3.UnitZ, 0, 100, visitor);
        Assert.Equal(new[] { 4f, 6f }, hits.Order().Select(h => MathF.Round(h, 4)));
    }

    private sealed class Collect(List<float> hits) : HitVisitor
    {
        public override bool Visit(Shape shape, int prim, Vector2 bary, in Vector3 o, in Vector3 d, float t)
        {
            hits.Add(t);
            return false;
        }
    }
}
