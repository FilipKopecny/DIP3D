using System.ComponentModel;

namespace Overbryce.Core.Model;

/// <summary>
/// Settings of the modern renderer (new in Overbryce 2.1): HDR with bloom, shadows, ambient occlusion
/// and reflections. They belong to the animation and are stored in the extension block after the end
/// of the original file format, which the 1998 program and Overbryce 2.0 ignore. The classic renderer
/// (the 1998 look) does not use them.
/// </summary>
public sealed class ModernSettings
{
    [Category("1 General"), DisplayName("Open in modern mode"), Description("Show this animation with the modern renderer when it is opened (View > Modern rendering switches at any time).")]
    public bool Enabled { get; set; }

    [Category("1 General"), DisplayName("Exposure"), Description("Brightness of the whole picture before the highlights are compressed (1 = as in the classic renderer).")]
    public float Exposure { get; set; } = 1f;

    [Category("2 Bloom"), DisplayName("On"), Description("Bright parts of the picture glow.")]
    public bool Bloom { get; set; } = true;

    [Category("2 Bloom"), DisplayName("Strength"), Description("How strongly the glow is added (0..2).")]
    public float BloomStrength { get; set; } = 0.5f;

    [Category("2 Bloom"), DisplayName("Threshold"), Description("Brightness above which the picture glows (the classic renderer clamps at 1).")]
    public float BloomThreshold { get; set; } = 0.9f;

    [Category("3 Shadows"), DisplayName("On"), Description("Lights cast shadows (each light and element can opt out with 'Casts shadows').")]
    public bool Shadows { get; set; } = true;

    [Category("3 Shadows"), DisplayName("Softness"), Description("Width of the soft shadow edge (0 = hard).")]
    public float ShadowSoftness { get; set; } = 1f;

    [Category("3 Shadows"), DisplayName("Lights with shadows"), Description("At most this many lights cast shadows at a time (0..4): the brightest ones near the camera.")]
    public int ShadowLights { get; set; } = 4;

    [Category("3 Shadows"), DisplayName("Map size"), Description("Resolution of each face of a light's shadow map (256..2048).")]
    public int ShadowMapSize { get; set; } = 1024;

    [Category("4 Ambient occlusion"), DisplayName("On"), Description("Creases and contacts get darker (screen-space ambient occlusion).")]
    public bool AmbientOcclusion { get; set; } = true;

    [Category("4 Ambient occlusion"), DisplayName("Radius"), Description("How far around a point is searched for occluders, in scene units.")]
    public float AoRadius { get; set; } = 0.5f;

    [Category("4 Ambient occlusion"), DisplayName("Strength"), Description("How dark the occlusion gets (0..2).")]
    public float AoStrength { get; set; } = 1f;

    [Category("5 Reflections"), DisplayName("On"), Description("Elements with a reflectivity mirror their surroundings, glass (refractive index above 1) bends the light.")]
    public bool Reflections { get; set; } = true;

    [Category("5 Reflections"), DisplayName("Probes"), Description("At most this many reflecting elements get their own view of the surroundings per frame (1..8); the others share the nearest one.")]
    public int ReflectionProbes { get; set; } = 4;

    [Category("5 Reflections"), DisplayName("Probe size"), Description("Resolution of each face of a reflection probe (32..512).")]
    public int ProbeSize { get; set; } = 256;

    /// <summary>Settings this version does not know (from a newer version); kept and written back unchanged.</summary>
    [Browsable(false)]
    public Dictionary<string, string> Unknown { get; private set; } = new(StringComparer.Ordinal);

    public ModernSettings Clone()
    {
        var copy = (ModernSettings)MemberwiseClone();
        copy.Unknown = new Dictionary<string, string>(Unknown, StringComparer.Ordinal);
        return copy;
    }

    /// <summary>Copies all values from <paramref name="other"/>.</summary>
    public void CopyFrom(ModernSettings other)
    {
        foreach (var p in typeof(ModernSettings).GetProperties())
            if (p.CanWrite && p.GetSetMethod() != null)
                p.SetValue(this, p.GetValue(other));
        Unknown = new Dictionary<string, string>(other.Unknown, StringComparer.Ordinal);
    }

    /// <summary>The values clamped to the documented ranges.</summary>
    public void Normalize()
    {
        Exposure = Math.Clamp(Exposure, 0.05f, 20f);
        BloomStrength = Math.Clamp(BloomStrength, 0f, 4f);
        BloomThreshold = Math.Clamp(BloomThreshold, 0f, 10f);
        ShadowSoftness = Math.Clamp(ShadowSoftness, 0f, 10f);
        ShadowLights = Math.Clamp(ShadowLights, 0, 4);
        ShadowMapSize = Math.Clamp(ShadowMapSize, 256, 2048);
        AoRadius = Math.Clamp(AoRadius, 0.01f, 100f);
        AoStrength = Math.Clamp(AoStrength, 0f, 4f);
        ReflectionProbes = Math.Clamp(ReflectionProbes, 1, 8);
        ProbeSize = Math.Clamp(ProbeSize, 32, 512);
    }
}
