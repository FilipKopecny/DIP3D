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

    public static Animation Load(string path) => Parse(File.ReadAllText(path, LegacyEncoding));

    public static void Save(Animation animation, string path) => File.WriteAllText(path, Serialize(animation), LegacyEncoding);

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
        return w.ToString();
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
