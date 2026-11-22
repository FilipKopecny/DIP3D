using System.Numerics;
using Overbryce.Core.Geometry;
using Overbryce.Core.IO;
using Overbryce.Core.Model;

namespace Overbryce.Core.Tests;

/// <summary>The polygon mesh element (2.4): its data, file storage, interpolation and tessellation.</summary>
public class PolyMeshTests
{
    /// <summary>A unit cube with flat faces (24 vertices: each face has its own normals and texture coordinates).</summary>
    public static PolyMeshData Cube(float size = 1)
    {
        var positions = new List<float>();
        var uvs = new List<float>();
        var indices = new List<int>();
        Vector3[] normals = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
        foreach (var n in normals)
        {
            var u = MathF.Abs(n.Y) > 0.5f ? Vector3.UnitX : Vector3.Cross(Vector3.UnitY, n);
            var v = Vector3.Cross(n, u);
            int start = positions.Count / 3;
            foreach (var (a, b) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
            {
                var p = (n + a * u + b * v) * (size / 2);
                positions.AddRange([p.X, p.Y, p.Z]);
                uvs.AddRange([(a + 1) / 2, (b + 1) / 2]);
            }
            indices.AddRange([start, start + 1, start + 2, start, start + 2, start + 3]);
        }
        return PolyMeshData.Create(positions.ToArray(), uvs.ToArray(), indices.ToArray());
    }

    private static Animation TwoStates(PolyMeshData first, PolyMeshData second)
    {
        var a = new Animation("Poly");
        for (int i = 0; i < 2; i++)
        {
            var s = new Scene { AniTime = i * 2 };
            s.Add(new SphereElement("Ball"));
            var g = new GroupElement("G");
            g.Add(new PolyMeshElement("Cube", i == 0 ? first : second) { X = 1, Gamma = 30, Reflectivity = 0.5f, Texture = "wood.bmp" });
            s.Add(g);
            a.Scenes.Add(s);
        }
        return a;
    }

    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "overbryce-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Cube_normals_are_flat_and_the_cube_is_closed()
    {
        var cube = Cube();
        Assert.Equal(24, cube.VertexCount);
        Assert.Equal(12, cube.TriangleCount);
        Assert.True(cube.IsClosed);
        // the first face looks along +X
        Assert.Equal(1f, cube.Normals[0], 4);
        Assert.Equal(0f, cube.Normals[1], 4);
    }

    [Fact]
    public void Saves_to_a_mesh_file_and_loads_back()
    {
        string dir = TempDir();
        string path = Path.Combine(dir, "Poly test.ani");
        var cube = Cube();
        AniFile.Save(TwoStates(cube, cube), path);

        Assert.True(File.Exists(Path.Combine(dir, "Poly_test.meshes")));
        var loaded = AniFile.Load(path);
        var mesh = (PolyMeshElement)((GroupElement)loaded.Scenes[1].FindChild("G")!).FindChild("Cube")!;
        Assert.NotNull(mesh.Data);
        Assert.Equal(cube.Id, mesh.Data!.Id);
        Assert.Equal(0.5f, mesh.Reflectivity);
        Assert.Equal(30f, mesh.Gamma);
        Assert.Equal("wood.bmp", mesh.Texture);

        // the key states share one mesh again, and saving once more writes the same text
        var first = (PolyMeshElement)((GroupElement)loaded.Scenes[0].FindChild("G")!).FindChild("Cube")!;
        Assert.Same(first.Data, mesh.Data);
        Assert.Equal(File.ReadAllText(path), AniFile.Serialize(loaded));
    }

    [Fact]
    public void Older_versions_see_an_empty_group()
    {
        string text = AniFile.Serialize(TwoStates(Cube(), Cube()));
        // the original format ends at KoniecAnimacie: everything before it is what the 1998 program reads
        string original = text[..(text.IndexOf(AniTags.AnimationEnd, StringComparison.Ordinal) + AniTags.AnimationEnd.Length)];
        var old = AniFile.Parse(original);
        var placeholder = ((GroupElement)old.Scenes[0].FindChild("G")!).FindChild("Cube");
        Assert.IsType<GroupElement>(placeholder);
        Assert.Empty(((GroupElement)placeholder!).Children);
        Assert.Equal(1f, placeholder.X);
    }

    [Fact]
    public void A_missing_mesh_file_keeps_the_element_and_its_id()
    {
        var cube = Cube();
        var a = AniFile.Parse(AniFile.Serialize(TwoStates(cube, cube)));
        var mesh = (PolyMeshElement)((GroupElement)a.Scenes[0].FindChild("G")!).FindChild("Cube")!;
        Assert.Null(mesh.Data);
        Assert.Equal(cube.Id, mesh.MeshId);
        Assert.Contains("mesh=" + cube.Id, AniFile.Serialize(a));
        Assert.Equal(0, AniFile.ResolveMeshes(a, new Dictionary<string, PolyMeshData> { [cube.Id] = cube }));
        Assert.Same(cube, mesh.Data);
    }

    [Fact]
    public void Mesh_file_stores_shared_arrays_once()
    {
        string dir = TempDir();
        var cube = Cube();
        var moved = cube.WithPositions(cube.Positions.Select(p => p * 2).ToArray());
        string one = Path.Combine(dir, "one.meshes"), two = Path.Combine(dir, "two.meshes");
        MeshStore.Write(one, [cube]);
        MeshStore.Write(two, [cube, moved]);
        var read = MeshStore.Read(two);
        Assert.Equal(2, read.Count);
        Assert.Same(read[cube.Id].Indices, read[moved.Id].Indices);
        Assert.Same(read[cube.Id].Uvs, read[moved.Id].Uvs);
        Assert.Equal(moved.Positions, read[moved.Id].Positions);
    }

    [Fact]
    public void Coefficients_reproduce_the_spline()
    {
        foreach (var (s, e) in new[] { (false, false), (true, false), (false, true), (true, true) })
        {
            var w = SplineWeights.FromKeyTimes(0, 1, 3, 3.5f, 0.37f).WithEase(s, e);
            var (w1, w2, w3, w4) = w.Coefficients();
            float p1 = 2, p2 = -1, p3 = 5, p4 = 7;
            Assert.Equal(w.Interpolate(p1, p2, p3, p4), w1 * p1 + w2 * p2 + w3 * p3 + w4 * p4, 4);
        }
    }

    [Fact]
    public void Meshes_of_the_same_topology_morph_between_key_states()
    {
        var cube = Cube();
        var big = cube.WithPositions(cube.Positions.Select(p => p * 3).ToArray());
        var a = TwoStates(cube, big);
        var mid = (PolyMeshElement)((GroupElement)a.Evaluate(1)!.FindChild("G")!).FindChild("Cube")!;
        // the whole animation eases in and out, so half-way in time is half-way in shape
        Assert.Equal(cube.Positions[0] * 2, mid.Data!.Positions[0], 4);
        Assert.Same(cube.Indices, mid.Data.Indices);
        Assert.NotNull(mid.Data.BlendedFrom);
        Assert.Equal(1f, mid.Data.BlendedFrom!.Sum(p => p.Weight), 4);

        // a different vertex count cannot morph: the first key state's mesh stays
        var other = TwoStates(cube, PolyMeshData.Create([0, 0, 0, 1, 0, 0, 0, 1, 0], null, [0, 1, 2]));
        var kept = (PolyMeshElement)((GroupElement)other.Evaluate(1)!.FindChild("G")!).FindChild("Cube")!;
        Assert.Same(cube, kept.Data);
    }

    [Fact]
    public void Tessellation_gives_the_triangles_and_the_edges()
    {
        var g = new GeometryBuilder();
        var ranges = new List<DrawRange>();
        g.Shape(ranges, new PolyMeshElement("C", Cube()), false);
        Assert.Equal(36, g.VertexCount);
        Assert.Equal(PrimitiveMode.Triangles, ranges[0].Mode);

        g.Clear();
        ranges.Clear();
        g.Shape(ranges, new PolyMeshElement("C", Cube()), true);
        Assert.Equal(PrimitiveMode.Lines, ranges[0].Mode);
        Assert.Equal(6 * 5 * 2, g.VertexCount); // 4 sides and a diagonal per face, faces not shared (flat cube)
    }

    [Fact]
    public void Resize_and_mirroring_keep_the_faces_outside()
    {
        var cube = Cube();
        var mirrored = cube.Transform(Matrix4x4.CreateScale(-2, 1, 1));
        var n = PolyMeshData.ComputeNormals(mirrored.Positions, mirrored.Indices, null);
        // the +X face is now at -X and its normal still points outwards
        Assert.Equal(-1f, mirrored.Positions[0], 4);
        Assert.True(n[0] < -0.9f);
        Assert.True(mirrored.Normals[0] < -0.9f);

        var e = new PolyMeshElement("C", cube);
        e.Resize(2);
        Assert.Equal(1f, e.Data!.Positions[0], 4);
    }

    [Fact]
    public void Camera_clip_and_texture_size_round_trip()
    {
        var a = Animation.CreateNew();
        a.Scenes[0].Add(new CameraElement("Cam") { Near = 0.01f, Far = 5000, Active = true });
        a.Scenes[0].Add(new CameraElement("Plain"));
        a.TextureSize = 1024;
        var b = AniFile.Parse(AniFile.Serialize(a));
        var cam = (CameraElement)b.Scenes[0].FindChild("Cam")!;
        Assert.Equal(0.01f, cam.Near);
        Assert.Equal(5000f, cam.Far);
        Assert.Equal(CameraElement.DefaultFar, ((CameraElement)b.Scenes[0].FindChild("Plain")!).Far);
        Assert.Equal(1024, b.TextureSize);
        Assert.DoesNotContain("Textures", AniFile.Serialize(Animation.CreateNew()));
    }
}
