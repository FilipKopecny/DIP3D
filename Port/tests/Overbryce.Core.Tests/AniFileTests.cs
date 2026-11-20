using Overbryce.Core.IO;
using Overbryce.Core.Model;

namespace Overbryce.Core.Tests;

public class AniFileTests
{
    internal static string ScenesDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "DIP", "SCENES");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("Original DIP/SCENES folder not found above the test directory.");
    }

    public static TheoryData<string> SampleFiles()
    {
        var data = new TheoryData<string>();
        foreach (var f in Directory.GetFiles(ScenesDir(), "*.ani").OrderBy(f => f))
            data.Add(Path.GetFileName(f));
        return data;
    }

    [Theory]
    [MemberData(nameof(SampleFiles))]
    public void Loads_original_sample(string file)
    {
        var a = AniFile.Load(Path.Combine(ScenesDir(), file));
        Assert.NotEmpty(a.Scenes);
        Assert.True(a.Fps > 0);
        Assert.All(a.Scenes, s => Assert.NotEmpty(s.Children));
    }

    [Theory]
    [MemberData(nameof(SampleFiles))]
    public void Round_trip_is_stable(string file)
    {
        var a = AniFile.Load(Path.Combine(ScenesDir(), file));
        string once = AniFile.Serialize(a);
        string twice = AniFile.Serialize(AniFile.Parse(once));
        Assert.Equal(once, twice);
    }

    [Theory]
    [MemberData(nameof(SampleFiles))]
    public void Every_frame_can_be_evaluated(string file)
    {
        var a = AniFile.Load(Path.Combine(ScenesDir(), file));
        foreach (float t in a.FrameTimes())
        {
            var s = a.Evaluate(t);
            Assert.NotNull(s);
            Assert.All(s!.Descendants(), e => Assert.True(float.IsFinite(e.X) && float.IsFinite(e.Y) && float.IsFinite(e.Z)));
        }
    }

    [Fact]
    public void Reads_known_values_from_Gule()
    {
        var a = AniFile.Load(Path.Combine(ScenesDir(), "Gule.ani"));
        Assert.Equal(25, a.Fps);
        Assert.Equal(3, a.Scenes.Count);
        Assert.Equal(new[] { 50f, 70f, 75f }, a.Scenes.Select(s => s.AniTime));

        var s0 = a.Scenes[0];
        var light = Assert.IsType<LightElement>(s0.Children[0]);
        Assert.Equal(2f, light.Z);
        Assert.Equal(180, light.SpotCutoff);

        var cam = Assert.IsType<CameraElement>(s0.Children[1]);
        Assert.True(cam.Active);
        Assert.Equal(35, cam.FieldOfView);
        Assert.Same(cam, s0.ActiveCamera);

        var group = Assert.IsType<GroupElement>(s0.FindChild("NovaGrupa"));
        Assert.Equal(9f, group.X);
        Assert.True(group.OrbitPermanent);
        Assert.Equal(180f, group.OrbitAngleY);
        Assert.Equal(5f, group.OrbitPeriod);
        var moon = Assert.IsType<SphereElement>(group.FindChild("mesiac")); // case-insensitive like stricmp
        Assert.Equal(0.2, moon.Radius, 5);
    }

    [Fact]
    public void Reads_textures_and_legacy_names()
    {
        var ha1 = AniFile.Load(Path.Combine(ScenesDir(), "ha1.ani"));
        Assert.Contains(ha1.Scenes[0].Descendants(), e => e.Texture == "Travnik.bmp");

        // Meshe.ani names a texture with size 0x0 - the original treated that as "no texture"
        var a = AniFile.Load(Path.Combine(ScenesDir(), "Meshe.ani"));
        Assert.DoesNotContain(a.Scenes[0].Descendants(), e => e.HasTexture);
        var mesh = a.Scenes[0].Descendants().OfType<MeshElement>().First();
        Assert.Equal(mesh.CountX * mesh.CountY * 3, mesh.Points.Length);
    }
}
