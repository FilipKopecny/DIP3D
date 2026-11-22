using System.Text;
using Overbryce.Core.Model;

namespace Overbryce.Core.IO;

/// <summary>
/// Reader/writer of the original <c>.ani</c> text format: a single stream of whitespace separated
/// tokens, <c>Animacia name fps Scena ... Koniec_sceny ... KoniecAnimacie</c>.
/// Files were written on Slovak Windows, so the default encoding is Windows-1250.
/// </summary>
public static class AniFile
{
    static AniFile()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static Encoding LegacyEncoding => Encoding.GetEncoding(1250);

    /// <summary>Reads an animation, with the vertices of its polygon meshes from the mesh file next to it.</summary>
    public static Animation Load(string path)
    {
        var animation = Parse(File.ReadAllText(path, LegacyEncoding));
        if (PolyMeshes(animation).Any(m => m.Data == null && m.MeshId != null))
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
            var candidates = new List<string>();
            if (animation.MeshFile != null) candidates.Add(Path.Combine(dir, Path.GetFileName(animation.MeshFile)));
            candidates.Add(Path.ChangeExtension(Path.GetFullPath(path), MeshStore.Extension));
            if (candidates.FirstOrDefault(File.Exists) is { } meshFile)
                ResolveMeshes(animation, MeshStore.Read(meshFile));
        }
        return animation;
    }

    /// <summary>
    /// Writes an animation; the vertices of its polygon meshes go to <c>name.meshes</c> next to it (only when it has
    /// any, so other files are written exactly as before).
    /// </summary>
    public static void Save(Animation animation, string path)
    {
        var meshes = PolyMeshes(animation).Select(m => m.Data).OfType<PolyMeshData>().ToList();
        if (meshes.Count > 0)
        {
            string meshFile = Path.GetFileNameWithoutExtension(path).Replace(' ', '_') + MeshStore.Extension;
            MeshStore.Write(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, meshFile), meshes);
            animation.MeshFile = meshFile;
        }
        File.WriteAllText(path, Serialize(animation), LegacyEncoding);
    }

    /// <summary>Every polygon mesh element of every key state.</summary>
    public static IEnumerable<PolyMeshElement> PolyMeshes(Animation animation) =>
        animation.Scenes.SelectMany(s => s.Descendants()).OfType<PolyMeshElement>();

    /// <summary>Gives the polygon meshes that refer to a mesh by id their data; returns the number still missing.</summary>
    public static int ResolveMeshes(Animation animation, IReadOnlyDictionary<string, PolyMeshData> meshes)
    {
        int missing = 0;
        foreach (var m in PolyMeshes(animation))
        {
            if (m.Data != null || m.MeshId == null) continue;
            if (meshes.TryGetValue(m.MeshId, out var data)) m.Data = data;
            else missing++;
        }
        return missing;
    }

    public static Animation Parse(string text)
    {
        var r = new AniTokenReader(text);
        string tag = r.Next();
        if (tag != AniTags.Animation)
            throw new AniFormatException($"Not an animation file (starts with '{tag}', expected '{AniTags.Animation}').");

        var animation = new Animation(r.Next()) { Fps = r.NextFloat() };
        while (true)
        {
            tag = r.Next();
            if (tag == AniTags.AnimationEnd) break;
            if (tag != AniTags.Scene)
                throw new AniFormatException($"Expected '{AniTags.Scene}' or '{AniTags.AnimationEnd}' but found '{tag}'.");
            var scene = new Scene();
            scene.ReadCommon(r);
            scene.ReadBody(r);
            animation.Scenes.Add(scene);
        }

        if (animation.Scenes.Count == 0)
            throw new AniFormatException("The animation contains no key state.");

        // Overbryce 2.1 settings follow the end tag, where older versions stop reading
        AniExtensions.Read(r, animation);
        return animation;
    }

    public static string Serialize(Animation animation)
    {
        var w = new AniTokenWriter();
        w.Write(AniTags.Animation);
        w.Write(animation.Name);
        w.Write(animation.Fps);
        foreach (var scene in animation.Scenes) scene.Write(w);
        w.Write(AniTags.AnimationEnd);
        return w + AniExtensions.Write(animation);
    }

    /// <summary>Reads child elements into <paramref name="group"/> until <paramref name="endTag"/>.</summary>
    internal static void ReadChildren(AniTokenReader r, GroupElement group, string endTag)
    {
        while (true)
        {
            string tag = r.Next();
            if (tag == endTag) return;
            Element element = CreateElement(tag)
                ?? throw new AniFormatException($"Unknown element type '{tag}' in '{group.Name}'.");
            element.ReadCommon(r);
            element.ReadBody(r);
            group.Add(element);
        }
    }

    private static Element? CreateElement(string tag) => tag switch
    {
        AniTags.Light => new LightElement(),
        AniTags.Sphere => new SphereElement(),
        AniTags.Camera => new CameraElement(),
        AniTags.Disk => new DiskElement(),
        AniTags.Cone => new ConeElement(),
        AniTags.Mesh => new MeshElement(),
        AniTags.Box => new BoxElement(),
        AniTags.Rectangle => new RectangleElement(),
        AniTags.Triangle => new TriangleElement(),
        AniTags.Torus => new TorusElement(),
        AniTags.Group => new GroupElement(),
        _ => null,
    };
}
