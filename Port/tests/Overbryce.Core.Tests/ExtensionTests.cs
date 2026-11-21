using Overbryce.Core.IO;
using Overbryce.Core.Model;

namespace Overbryce.Core.Tests;

/// <summary>The Overbryce 2.1 extension block after <c>KoniecAnimacie</c> (modern rendering settings).</summary>
public class ExtensionTests
{
    private static Animation Sample()
    {
        var a = new Animation("Test") { Fps = 25 };
        for (int i = 0; i < 2; i++)
        {
            var s = new Scene { AniTime = i * 2 };
            s.Add(new LightElement("Lamp"));
            var g = new GroupElement("G");
            g.Add(new SphereElement("Chrome"));
            g.Add(new BoxElement("Glass"));
            s.Add(g);
            a.Scenes.Add(s);
        }
        return a;
    }

    private static Element Chrome(Scene s) => ((GroupElement)s.FindChild("G")!).FindChild("Chrome")!;
    private static Element Glass(Scene s) => ((GroupElement)s.FindChild("G")!).FindChild("Glass")!;

    private static Animation WithModernData()
    {
        var a = Sample();
        a.Modern.Enabled = true;
        a.Modern.BloomStrength = 0.75f;
        a.Modern.ShadowMapSize = 512;
        a.Modern.AmbientOcclusion = false;
        foreach (var s in a.Scenes)
        {
            s.FindChild("Lamp")!.CastsShadows = false;
            Chrome(s).Reflectivity = 0.8f;
            Glass(s).RefractiveIndex = 1.5f;
        }
        Chrome(a.Scenes[1]).Glow = 3f;
        return a;
    }

    [Fact]
    public void Files_without_modern_data_are_written_as_before()
    {
        string text = AniFile.Serialize(Sample());
        Assert.DoesNotContain(AniExtensions.Begin, text);
        Assert.EndsWith("KoniecAnimacie", text.TrimEnd());
    }

    [Fact]
    public void Modern_settings_and_element_values_round_trip()
    {
        var a = AniFile.Parse(AniFile.Serialize(WithModernData()));
        Assert.True(a.Modern.Enabled);
        Assert.Equal(0.75f, a.Modern.BloomStrength);
        Assert.Equal(512, a.Modern.ShadowMapSize);
        Assert.False(a.Modern.AmbientOcclusion);
        Assert.True(a.Modern.Bloom); // untouched values keep their defaults
        Assert.Equal(256, a.Modern.ProbeSize);

        foreach (var s in a.Scenes)
        {
            Assert.False(s.FindChild("Lamp")!.CastsShadows);
            Assert.Equal(0.8f, Chrome(s).Reflectivity);
            Assert.Equal(1.5f, Glass(s).RefractiveIndex);
            Assert.Equal(1f, Glass(s).Glow);
        }
        Assert.Equal(1f, Chrome(a.Scenes[0]).Glow);
        Assert.Equal(3f, Chrome(a.Scenes[1]).Glow);

        string once = AniFile.Serialize(a);
        Assert.Equal(once, AniFile.Serialize(AniFile.Parse(once)));
    }

    [Fact]
    public void The_text_before_the_block_is_the_original_format()
    {
        // what the 1998 program and Overbryce 2.0 read: everything up to KoniecAnimacie
        var modern = WithModernData();
        string text = AniFile.Serialize(modern);
        int block = text.IndexOf("\r\n" + AniExtensions.Begin, StringComparison.Ordinal);
        Assert.True(block > 0);
        string prefix = text[..block];

        var plain = WithModernData();
        plain.Modern.CopyFrom(new ModernSettings());
        foreach (var e in plain.Scenes.SelectMany(s => s.Descendants()))
        {
            e.Reflectivity = 0;
            e.RefractiveIndex = 1;
            e.Glow = 1;
            e.CastsShadows = true;
        }
        Assert.Equal(AniFile.Serialize(plain), prefix);

        var old = AniFile.Parse(prefix);
        Assert.False(old.Modern.Enabled);
        Assert.All(old.Scenes.SelectMany(s => s.Descendants()), e => Assert.Equal(0f, e.Reflectivity));
    }

    [Fact]
    public void Unknown_records_and_keys_from_a_newer_version_are_kept()
    {
        string text = AniFile.Serialize(Sample()) +
            "\r\nOverbryce2 7\r\n" +
            "Modern enabled=1 futureSetting=abc ;\r\n" +
            "Element 0 1.0 reflectivity=0.5 roughness=0.2 ;\r\n" +
            "Raytracer samples=64 bounces=4 ;\r\n" +
            "EndOverbryce2\r\n";
        var a = AniFile.Parse(text);
        Assert.True(a.Modern.Enabled);
        Assert.Equal("abc", a.Modern.Unknown["futureSetting"]);
        Assert.Equal(0.5f, Chrome(a.Scenes[0]).Reflectivity);
        Assert.Equal("0.2", Chrome(a.Scenes[0]).ExtensionData!["roughness"]);
        Assert.Equal("Raytracer samples=64 bounces=4", Assert.Single(a.UnknownExtensions));

        string again = AniFile.Serialize(a);
        Assert.Contains("futureSetting=abc", again);
        Assert.Contains("roughness=0.2", again);
        Assert.Contains("Raytracer samples=64 bounces=4 ;", again);
        var b = AniFile.Parse(again);
        Assert.Equal("0.2", Chrome(b.Scenes[0]).ExtensionData!["roughness"]);
        Assert.Equal(again, AniFile.Serialize(b));
    }

    [Fact]
    public void Extension_data_follows_the_element_when_cloned()
    {
        var e = new SphereElement("S") { Reflectivity = 0.3f, ExtensionData = new() { ["roughness"] = "0.1" } };
        var copy = e.Clone();
        Assert.Equal(0.3f, copy.Reflectivity);
        copy.ExtensionData!["roughness"] = "0.9";
        Assert.Equal("0.1", e.ExtensionData["roughness"]);
    }

    [Theory]
    [InlineData("Modern exposure=abc bloomStrength=NaN probes=x enabled=yes ;")]
    [InlineData("Element 9 0 reflectivity=1 ;")]          // no such key state
    [InlineData("Element 0 5.2 reflectivity=1 ;")]        // no such element
    [InlineData("Element 0 reflectivity=1 ;")]            // missing path
    [InlineData("Element x y ;")]
    [InlineData("; ; =1 noValue ;")]
    public void Bad_values_and_stale_records_fall_back_to_defaults(string record)
    {
        string text = AniFile.Serialize(Sample()) + $"\r\nOverbryce2 1\r\n{record}\r\nEndOverbryce2\r\n";
        var a = AniFile.Parse(text);
        Assert.False(a.Modern.Enabled);
        Assert.Equal(1f, a.Modern.Exposure);
        Assert.Equal(0.5f, a.Modern.BloomStrength);
        Assert.Equal(4, a.Modern.ReflectionProbes);
        Assert.All(a.Scenes.SelectMany(s => s.Descendants()), e =>
        {
            Assert.Equal(0f, e.Reflectivity);
            Assert.Null(e.ExtensionData);
        });
    }

    [Theory]
    [InlineData("\r\nOverbryce2 1\r\nModern enabled=1 bloomStrength=0.7")] // cut off in the middle
    [InlineData("\r\nOverbryce2")]
    [InlineData("\r\nsome junk the original also ignored\r\n")]
    [InlineData("")]
    public void A_damaged_or_missing_block_does_not_prevent_loading(string tail)
    {
        var a = AniFile.Parse(AniFile.Serialize(Sample()) + tail);
        Assert.Equal(2, a.Scenes.Count);
        Assert.Equal(0.5f, a.Modern.BloomStrength); // an unfinished record is not applied
    }

    [Fact]
    public void Out_of_range_settings_are_clamped()
    {
        string text = AniFile.Serialize(Sample()) +
            "\r\nOverbryce2 1\r\nModern shadowMapSize=100000 probes=0 exposure=-3 ;\r\nElement 0 1.0 reflectivity=7 ior=0.2 glow=-1 ;\r\nEndOverbryce2\r\n";
        var a = AniFile.Parse(text);
        Assert.Equal(2048, a.Modern.ShadowMapSize);
        Assert.Equal(1, a.Modern.ReflectionProbes);
        Assert.True(a.Modern.Exposure > 0);
        var chrome = Chrome(a.Scenes[0]);
        Assert.Equal(1f, chrome.Reflectivity);
        Assert.Equal(1f, chrome.RefractiveIndex);
        Assert.Equal(0f, chrome.Glow);
    }

    [Fact]
    public void Surface_values_are_interpolated_between_key_states()
    {
        var a = Sample();
        Chrome(a.Scenes[1]).Reflectivity = 1f;
        Glass(a.Scenes[1]).RefractiveIndex = 2f;
        Chrome(a.Scenes[1]).Glow = 3f;
        var mid = a.Evaluate(1)!;
        Assert.InRange(Chrome(mid).Reflectivity, 0.01f, 0.99f);
        Assert.InRange(Glass(mid).RefractiveIndex, 1.01f, 1.99f);
        Assert.InRange(Chrome(mid).Glow, 1.01f, 2.99f);
        Assert.Equal(1f, Chrome(a.Evaluate(2)!).Reflectivity, 4);
    }
}
