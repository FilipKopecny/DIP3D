using System.Numerics;
using Overbryce.Core;
using Overbryce.Core.Model;

namespace Overbryce.Raytrace.Tests;

/// <summary>Polygon meshes (2.4) in the ray tracer: the same picture as the primitive they copy.</summary>
public class PolyMeshRenderTests
{
    private const int Size = 48;

    /// <summary>The 24-vertex flat cube of a box of the given size, made like the Blender import makes meshes.</summary>
    private static PolyMeshData Cube(float s)
    {
        var p = new List<float>();
        var ix = new List<int>();
        Vector3[] normals = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
        foreach (var n in normals)
        {
            var u = MathF.Abs(n.Y) > 0.5f ? Vector3.UnitX : Vector3.Cross(Vector3.UnitY, n);
            var v = Vector3.Cross(n, u);
            int start = p.Count / 3;
            foreach (var (a, b) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
            {
                var q = (n + a * u + b * v) * (s / 2);
                p.AddRange([q.X, q.Y, q.Z]);
            }
            ix.AddRange([start, start + 1, start + 2, start, start + 2, start + 3]);
        }
        return PolyMeshData.Create(p.ToArray(), null, ix.ToArray());
    }

    private static RaytraceImage Render(Element shape)
    {
        var scene = new Scene();
        scene.Add(new LightElement("Lamp") { X = 3, Y = 4, Z = 6, Ambient = new Rgba(0, 0, 0, 1) });
        shape.Alpha = 25;
        shape.Beta = 35;
        scene.Add(shape);
        var camera = new CameraSetup(Matrix4x4.CreateLookAt(new Vector3(0, 0, 8), Vector3.Zero, Vector3.UnitY), 35, 0.1f, 100);
        var options = new RaytraceOptions
        {
            Settings = new RaytraceSettings { Samples = 1 },
            Modern = new ModernSettings { Bloom = false },
            Classic = true,
            Textures = _ => null,
        };
        return new Raytracer().Render([new FrameStep(scene, camera)], Size, Size, options);
    }

    [Fact]
    public void A_mesh_cube_looks_like_a_box()
    {
        var box = Render(new BoxElement("B") { SizeX = 2, SizeY = 2, SizeZ = 2 });
        var mesh = Render(new PolyMeshElement("M", Cube(2)));
        int lit = 0, differ = 0;
        for (int i = 0; i < box.Bgra.Length; i += 4)
        {
            if (box.Bgra[i + 1] > 0) lit++;
            if (Math.Abs(box.Bgra[i + 1] - mesh.Bgra[i + 1]) > 2) differ++;
        }
        Assert.True(lit > Size * Size / 8, "the box is in the picture");
        Assert.True(differ <= Size / 2, $"{differ} pixels differ"); // a few along the silhouette at most
    }
}
