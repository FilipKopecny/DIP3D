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
    public void No_key_state_uses_more_than_eight_lights()
    {
        foreach (var s in Lumina.Value.Scenes)
        {
            int lights = s.Descendants().OfType<LightElement>().Count();
            Assert.True(lights <= 8, $"{s.Name}: {lights} lights");
        }
    }

    [Fact]
    public void The_film_runs_through_the_intro_the_story_and_the_depths_in_order()
    {
        var a = Lumina.Value;
        Assert.True(a.EndTime > 120, $"ends at {a.EndTime}");
        Assert.All(a.Scenes.Zip(a.Scenes.Skip(1)), p => Assert.True(p.Second.AniTime > p.First.AniTime, p.Second.Name));
        // the cut into the depths: both key states at the cut stop every motion
        var cut = a.Scenes.Select((s, i) => (s, i)).First(x => x.s.Descendants().Any(e => e.Name == "HlbkaPozadie")).i;
        Assert.True(a.Scenes[cut].AniTime > 86);
        Assert.All(a.Scenes[cut - 1].Descendants().Concat(a.Scenes[cut].Descendants()), e => Assert.True(e.MotionBoundary, e.Name));
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
