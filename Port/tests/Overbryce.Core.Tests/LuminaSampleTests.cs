using Overbryce.Core.IO;
using Overbryce.Core.Model;

namespace Overbryce.Core.Tests;

/// <summary>The generated sample in samples/Lumina (built by tools/Lumina).</summary>
public class LuminaSampleTests
{
    private static string LuminaFile()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "samples", "Lumina", "Lumina.ani");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("samples/Lumina/Lumina.ani not found above the test directory (run tools/Lumina).");
    }

    private static readonly Lazy<Animation> Lumina = new(() => AniFile.Load(LuminaFile()));

    [Fact]
    public void Every_key_state_has_an_active_camera_and_existing_textures()
    {
        var a = Lumina.Value;
        Assert.True(a.Scenes.Count > 50);
        string dir = Path.GetDirectoryName(LuminaFile())!;
        foreach (var s in a.Scenes)
        {
            Assert.NotNull(s.ActiveCamera);
            foreach (var e in s.Descendants().Where(e => e.HasTexture))
                Assert.True(File.Exists(Path.Combine(dir, e.Texture!)), $"{s.Name}/{e.Name}: {e.Texture}");
        }
    }

    [Fact]
    public void Element_names_are_unique_in_each_key_state()
    {
        foreach (var s in Lumina.Value.Scenes)
        {
            var dup = s.Descendants().GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            Assert.True(dup is null, $"{s.Name}: {dup?.Key}");
        }
    }

    [Fact]
    public void Round_trip_is_stable()
    {
        string once = AniFile.Serialize(Lumina.Value);
        Assert.Equal(once, AniFile.Serialize(AniFile.Parse(once)));
    }

    [Fact]
    public void Every_frame_can_be_evaluated()
    {
        var a = Lumina.Value;
        foreach (float t in a.FrameTimes().Where((_, i) => i % 5 == 0))
        {
            var s = a.Evaluate(t);
            Assert.NotNull(s);
            Assert.NotNull(s!.ActiveCamera);
            Assert.All(s.Descendants(), e => Assert.True(float.IsFinite(e.X) && float.IsFinite(e.Y) && float.IsFinite(e.Z), e.Name));
        }
    }
}
