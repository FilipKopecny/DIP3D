using System.Text.Json;
using System.Text.Json.Serialization;

namespace Overbryce.Core.Import;

/// <summary>
/// What overbryce_import.py writes from a Blender scene (the interchange file, format "overbryce-blender-scene"):
/// a JSON file with the scene's structure and a binary side file with its arrays, addressed by byte offset. Pure
/// data; <see cref="BlenderSceneImporter"/> turns it into an animation.
/// </summary>
public sealed class BlenderSceneFile
{
    public const string FormatName = "overbryce-blender-scene";
    public const int SupportedVersion = 1;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public string Format { get; set; } = "";
    public int Version { get; set; }
    public SceneInfo Scene { get; set; } = new();
    /// <summary>The sampled frames: whole ones, plus the moments of the key states of a scene exported by Overbryce.</summary>
    public double[] Frames { get; set; } = [];
    public List<ObjectInfo> Objects { get; set; } = [];
    public List<MeshInfo> Meshes { get; set; } = [];
    public List<MaterialInfo> Materials { get; set; } = [];
    public string[] MaterialTrack { get; set; } = [];
    public List<TextureInfo> Textures { get; set; } = [];
    public WorldInfo World { get; set; } = new();

    /// <summary>The active camera's name per sampled frame ("" when the scene has none).</summary>
    public string[] Cameras { get; set; } = [];

    /// <summary>Timeline markers that switch the camera: [frame, camera name].</summary>
    public JsonElement[][] Markers { get; set; } = [];

    public string Binary { get; set; } = "";

    /// <summary>What the script already knows it leaves out or approximates: "kind|text".</summary>
    public List<string> Notes { get; set; } = [];

    [JsonIgnore] public byte[] Blob { get; private set; } = [];

    [JsonIgnore] public int FrameCount => Frames.Length;

    /// <summary>The camera markers as (frame, camera name).</summary>
    public IEnumerable<(int Frame, string Camera)> CameraMarkers()
    {
        foreach (var m in Markers)
            if (m.Length == 2 && m[0].ValueKind == JsonValueKind.Number && m[1].ValueKind == JsonValueKind.String)
                yield return (m[0].GetInt32(), m[1].GetString()!);
    }

    /// <summary>Reads the JSON file and its binary side file.</summary>
    public static BlenderSceneFile Load(string jsonPath)
    {
        var file = Parse(File.ReadAllText(jsonPath), null);
        string bin = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(jsonPath))!, file.Binary);
        file.Blob = File.Exists(bin) ? File.ReadAllBytes(bin) : throw new FileNotFoundException("The binary part of the Blender scene is missing.", bin);
        return file;
    }

    /// <summary>Parses the JSON text with the given binary part (unit tests build both in memory).</summary>
    public static BlenderSceneFile Parse(string json, byte[]? blob)
    {
        var file = JsonSerializer.Deserialize<BlenderSceneFile>(json, Json) ?? throw new InvalidDataException("Empty Blender scene file.");
        if (file.Format != FormatName)
            throw new InvalidDataException($"Not a Blender scene for Overbryce (format \"{file.Format}\").");
        if (file.Version > SupportedVersion)
            throw new InvalidDataException($"The Blender scene file has version {file.Version}; this Overbryce reads up to {SupportedVersion}.");
        if (blob != null) file.Blob = blob;
        return file;
    }

    // ------------------------------------------------------------------ the binary part

    public float[] Floats(long offset, int count)
    {
        var a = new float[count];
        if (count > 0) Buffer.BlockCopy(Blob, checked((int)offset), a, 0, count * 4);
        return a;
    }

    public int[] Ints(long offset, int count)
    {
        var a = new int[count];
        if (count > 0) Buffer.BlockCopy(Blob, checked((int)offset), a, 0, count * 4);
        return a;
    }

    public byte[] Bytes(long offset, int count) => Blob.AsSpan(checked((int)offset), count).ToArray();

    // ------------------------------------------------------------------ the parts

    public sealed class SceneInfo
    {
        public string Name { get; set; } = "Scene";
        public string File { get; set; } = "";
        public string Blender { get; set; } = "";
        public double Fps { get; set; } = 24;
        public int FrameStart { get; set; } = 1;
        public int FrameEnd { get; set; } = 250;
        public int[] Resolution { get; set; } = [1920, 1080];
        public string Engine { get; set; } = "";
        public double UnitScale { get; set; } = 1;
        public string ViewTransform { get; set; } = "Standard";
        public double Exposure { get; set; }
        public bool MotionBlur { get; set; }
        public double Shutter { get; set; } = 0.5;
        public int CyclesSamples { get; set; }
        public int CyclesBounces { get; set; }
        public bool EeveeAo { get; set; }
        public bool EeveeBloom { get; set; }
        public Dictionary<string, JsonElement> Props { get; set; } = [];
    }

    public sealed class ObjectInfo
    {
        public string Name { get; set; } = "";

        /// <summary>Blender's object type: MESH, CURVE, FONT, META, SURFACE, EMPTY, LIGHT, CAMERA, ARMATURE ...</summary>
        public string Kind { get; set; } = "";

        /// <summary>Made by an instancer (geometry nodes, particles, a collection instance); its parent is the instancer.</summary>
        public bool Instance { get; set; }

        public int Parent { get; set; } = -1;
        public int Mesh { get; set; } = -1;

        /// <summary>Offset of the world matrices: 16 floats per frame, row by row (NaN where the object is missing).</summary>
        public long Matrices { get; set; }

        /// <summary>Offset of one byte per frame: rendered (1) or hidden (0).</summary>
        public long Visible { get; set; }

        /// <summary>Lights and cameras: offset of their values per frame (<see cref="DataWidth"/> floats each).</summary>
        public long Data { get; set; } = -1;
        public int DataWidth { get; set; }

        /// <summary>Deforming meshes: per frame the offset of the vertex positions (-1 where not sampled).</summary>
        public long[]? Deform { get; set; }

        /// <summary>Overbryce custom properties with numbers: name → [offset, width] of their values per frame.</summary>
        public Dictionary<string, long[]>? Props { get; set; }

        /// <summary>Overbryce custom properties with text (written by the Overbryce export).</summary>
        public Dictionary<string, string>? Overbryce { get; set; }

        public string? ParentBone { get; set; }
        public string[]? Bones { get; set; }
        public bool HiddenInstancer { get; set; }
    }

    public sealed class MeshInfo
    {
        /// <summary>Blender vertices: their positions (3 floats each, the object's local space) at <see cref="Positions"/>.</summary>
        public int VertexCount { get; set; }
        public long Positions { get; set; }

        /// <summary>The output vertices (Blender's corners with equal vertex, normal and uv merged).</summary>
        public int CornerCount { get; set; }
        public long CornerVertex { get; set; }
        public long CornerNormal { get; set; }
        public long CornerUv { get; set; } = -1;

        public int TriangleCount { get; set; }
        public long Triangles { get; set; }
        public long TriangleSlot { get; set; }

        /// <summary>Per material slot the index into <see cref="BlenderSceneFile.Materials"/> (-1 = none).</summary>
        public int[] Slots { get; set; } = [-1];
    }

    public sealed class MaterialInfo
    {
        public string Name { get; set; } = "";
        public string Shader { get; set; } = "";
        public double[] Base { get; set; } = [0.8, 0.8, 0.8];
        public double Alpha { get; set; } = 1;
        public double Metallic { get; set; }
        public double Roughness { get; set; } = 0.5;
        public double Ior { get; set; } = 1.45;
        public double Transmission { get; set; }
        public double[] Emission { get; set; } = [0, 0, 0];
        public double EmissionStrength { get; set; }
        public double Specular { get; set; } = 0.5;
        public int Texture { get; set; } = -1;
        public UvTransform? Uv { get; set; }
        public List<string> Procedural { get; set; } = [];
        public List<string> Unsupported { get; set; } = [];

        /// <summary>Animated materials: offset of their values per frame (in <see cref="BlenderSceneFile.MaterialTrack"/> order).</summary>
        public long Track { get; set; } = -1;
        public int TrackWidth { get; set; }
    }

    /// <summary>A Mapping node between the texture coordinates and the image.</summary>
    public sealed class UvTransform
    {
        public string Type { get; set; } = "POINT";
        public double[] Location { get; set; } = [0, 0];
        public double Rotation { get; set; }
        public double[] Scale { get; set; } = [1, 1];
    }

    public sealed class TextureInfo
    {
        public string File { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Alpha { get; set; }
        public string Image { get; set; } = "";
    }

    public sealed class WorldInfo
    {
        public double[] Color { get; set; } = [0, 0, 0];
        public double Strength { get; set; } = 1;
        public int Texture { get; set; } = -1;
        public string? Projection { get; set; }
        public VolumeInfo? Volume { get; set; }
        public bool Animated { get; set; }

        /// <summary>Animated worlds: offset of colour (3) and strength per frame.</summary>
        public long Track { get; set; } = -1;
    }

    public sealed class VolumeInfo
    {
        public double Density { get; set; }
        public double[] Color { get; set; } = [0.5, 0.5, 0.5];
    }
}
