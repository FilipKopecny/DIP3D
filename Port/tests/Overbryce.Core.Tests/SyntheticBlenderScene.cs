using System.Numerics;
using System.Text.Json;
using Overbryce.Core.Import;

namespace Overbryce.Core.Tests;

/// <summary>
/// Builds the interchange file overbryce_import.py writes (JSON plus the binary part) in memory, so the import can be
/// tested without Blender. Matrices are Blender's (column vectors, given as System.Numerics row-vector matrices and
/// stored transposed, row by row, as the script stores them).
/// </summary>
public sealed class SyntheticBlenderScene(int frames = 1)
{
    private readonly MemoryStream _blob = new();
    private readonly List<Dictionary<string, object?>> _objects = [];
    private readonly List<Dictionary<string, object?>> _meshes = [];
    private readonly List<Dictionary<string, object?>> _materials = [];
    public int Frames { get; } = frames;
    public string ViewTransform { get; set; } = "Standard";
    public double[] WorldColor { get; set; } = [0, 0, 0];
    public List<string> Cameras { get; } = [];

    private long Write(ReadOnlySpan<float> values)
    {
        long at = _blob.Position;
        foreach (float v in values) _blob.Write(BitConverter.GetBytes(v));
        return at;
    }

    private long Write(ReadOnlySpan<int> values)
    {
        long at = _blob.Position;
        foreach (int v in values) _blob.Write(BitConverter.GetBytes(v));
        return at;
    }

    private long Write(byte[] values)
    {
        long at = _blob.Position;
        _blob.Write(values);
        return at;
    }

    /// <summary>A Blender world matrix (column-vector convention) from a System.Numerics one (row vectors).</summary>
    public static float[] Rows(Matrix4x4 m) =>
    [
        m.M11, m.M21, m.M31, m.M41,
        m.M12, m.M22, m.M32, m.M42,
        m.M13, m.M23, m.M33, m.M43,
        m.M14, m.M24, m.M34, m.M44,
    ];

    public int AddMaterial(string name, double[] baseColor, double metallic = 0, double roughness = 0.5, double transmission = 0,
        double[]? emission = null, double emissionStrength = 0)
    {
        _materials.Add(new()
        {
            ["name"] = name, ["shader"] = "bsdf_principled", ["base"] = baseColor, ["alpha"] = 1.0, ["metallic"] = metallic,
            ["roughness"] = roughness, ["ior"] = 1.45, ["transmission"] = transmission, ["emission"] = emission ?? new double[] { 0, 0, 0 },
            ["emission_strength"] = emissionStrength, ["specular"] = 0.5, ["texture"] = -1,
        });
        return _materials.Count - 1;
    }

    /// <summary>A mesh made of triangles with flat normals: every corner is its own vertex.</summary>
    public int AddMesh(Vector3[] vertices, int[] triangles, int[]? triangleSlots = null, int[]? slotMaterials = null)
    {
        int nt = triangles.Length / 3;
        var cornerVertex = new int[triangles.Length];
        var normals = new float[triangles.Length * 3];
        var uvs = new float[triangles.Length * 2];
        for (int t = 0; t < nt; t++)
        {
            var a = vertices[triangles[t * 3]];
            var n = Vector3.Normalize(Vector3.Cross(vertices[triangles[t * 3 + 1]] - a, vertices[triangles[t * 3 + 2]] - a));
            for (int k = 0; k < 3; k++)
            {
                int c = t * 3 + k;
                cornerVertex[c] = triangles[c];
                normals[c * 3] = n.X; normals[c * 3 + 1] = n.Y; normals[c * 3 + 2] = n.Z;
                uvs[c * 2] = k == 1 ? 1 : 0; uvs[c * 2 + 1] = k == 2 ? 1 : 0;
            }
        }
        var corners = Enumerable.Range(0, triangles.Length).ToArray();
        var positions = vertices.SelectMany(v => new[] { v.X, v.Y, v.Z }).ToArray();
        _meshes.Add(new()
        {
            ["vertex_count"] = vertices.Length,
            ["positions"] = Write(positions),
            ["corner_count"] = triangles.Length,
            ["corner_vertex"] = Write(cornerVertex),
            ["corner_normal"] = Write(normals),
            ["corner_uv"] = Write(uvs),
            ["triangle_count"] = nt,
            ["triangles"] = Write(corners),
            ["triangle_slot"] = Write(triangleSlots ?? new int[nt]),
            ["slots"] = slotMaterials ?? [-1],
        });
        return _meshes.Count - 1;
    }

    /// <summary>A unit cube (-1..1) of 12 triangles.</summary>
    public int AddCube(int material = -1)
    {
        Vector3[] v =
        [
            new(-1, -1, -1), new(1, -1, -1), new(1, 1, -1), new(-1, 1, -1),
            new(-1, -1, 1), new(1, -1, 1), new(1, 1, 1), new(-1, 1, 1),
        ];
        int[] t =
        [
            0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4,
            2, 3, 7, 2, 7, 6, 1, 2, 6, 1, 6, 5, 3, 0, 4, 3, 4, 7,
        ];
        return AddMesh(v, t, null, [material]);
    }

    /// <summary>An object with the same world matrix and visibility in every frame (or one per frame).</summary>
    public int AddObject(string name, string kind, Matrix4x4[] world, int parent = -1, int mesh = -1, bool[]? visible = null,
        float[]? data = null, int dataWidth = 0)
    {
        var m = new float[Frames * 16];
        for (int f = 0; f < Frames; f++) Rows(world[Math.Min(f, world.Length - 1)]).CopyTo(m, f * 16);
        var vis = new byte[Frames];
        for (int f = 0; f < Frames; f++) vis[f] = (byte)(visible == null || visible[Math.Min(f, visible.Length - 1)] ? 1 : 0);
        var o = new Dictionary<string, object?>
        {
            ["name"] = name, ["kind"] = kind, ["instance"] = false, ["parent"] = parent, ["mesh"] = mesh,
            ["matrices"] = Write(m), ["visible"] = Write(vis),
        };
        if (data != null)
        {
            o["data"] = Write(data);
            o["data_width"] = dataWidth;
        }
        _objects.Add(o);
        return _objects.Count - 1;
    }

    public int AddObject(string name, string kind, Matrix4x4 world, int parent = -1, int mesh = -1) =>
        AddObject(name, kind, [world], parent, mesh);

    /// <summary>A point light: colour, power in watts, radius.</summary>
    public int AddPointLight(string name, Matrix4x4 world, float power, float radius = 0)
    {
        var d = new float[Frames * 13];
        for (int f = 0; f < Frames; f++)
            new float[] { 0, 1, 1, 1, power, 0, 0, radius, 0, 0, 0, 1, 1 }.CopyTo(d, f * 13);
        return AddObject(name, "LIGHT", [world], data: d, dataWidth: 13);
    }

    /// <summary>A camera: vertical field of view in degrees, clip start and end.</summary>
    public int AddCamera(string name, Matrix4x4 world, float fovDegrees, float near = 0.1f, float far = 100)
    {
        var d = new float[Frames * 10];
        for (int f = 0; f < Frames; f++)
            new float[] { fovDegrees * MathF.PI / 180, near, far, 10, 0, 0, 0, 0, 0, 1 }.CopyTo(d, f * 10);
        return AddObject(name, "CAMERA", [world], data: d, dataWidth: 10);
    }

    public BlenderSceneFile Build()
    {
        var json = new Dictionary<string, object?>
        {
            ["format"] = BlenderSceneFile.FormatName,
            ["version"] = 1,
            ["scene"] = new Dictionary<string, object?>
            {
                ["name"] = "Synthetic", ["fps"] = 24.0, ["frame_start"] = 1, ["frame_end"] = Frames, ["resolution"] = new[] { 960, 540 },
                ["view_transform"] = ViewTransform, ["engine"] = "CYCLES", ["cycles_samples"] = 32, ["cycles_bounces"] = 4,
            },
            ["frames"] = Enumerable.Range(1, Frames).ToArray(),
            ["objects"] = _objects,
            ["meshes"] = _meshes,
            ["materials"] = _materials,
            ["material_track"] = new[] { "base", "alpha", "metallic", "roughness", "ior", "transmission", "emission", "emission_strength", "specular" },
            ["textures"] = Array.Empty<object>(),
            ["world"] = new Dictionary<string, object?> { ["color"] = WorldColor, ["strength"] = 1.0, ["texture"] = -1 },
            ["cameras"] = Cameras.Count > 0 ? Cameras.ToArray() : Enumerable.Repeat("", Frames).ToArray(),
            ["markers"] = Array.Empty<object>(),
            ["binary"] = "synthetic.bin",
            ["notes"] = Array.Empty<string>(),
        };
        return BlenderSceneFile.Parse(JsonSerializer.Serialize(json), _blob.ToArray());
    }
}
