using System.ComponentModel;

namespace Overbryce.Core.Model;

/// <summary>How the ray tracer represents the round shapes.</summary>
public enum RaytraceGeometry
{
    /// <summary>The same triangles the OpenGL renderers draw (slices, stacks, loops, grid).</summary>
    Faceted,

    /// <summary>Exact spheres, cones, disks and tori; the Bezier mesh stays triangles.</summary>
    Smooth,
}

/// <summary>
/// Settings of the ray tracer (new in Overbryce 2.2), which renders exported frames with exact
/// reflections, refraction and soft shadows. They belong to the animation and are stored in the
/// extension block after the end of the original file format, like <see cref="ModernSettings"/>.
/// Exposure, bloom and the tone curve are shared with the modern renderer.
/// </summary>
public sealed class RaytraceSettings
{
    [Category("1 Quality"), DisplayName("Samples per pixel"), Description("Rays per pixel (1..4096). More samples give smoother edges, soft shadows, glossy reflections, depth of field and motion blur, at a proportional cost.")]
    public int Samples { get; set; } = 16;

    [Category("1 Quality"), DisplayName("Bounces"), Description("How many reflections and refractions a ray follows (0..32).")]
    public int Bounces { get; set; } = 6;

    [Category("1 Quality"), DisplayName("Geometry"), Description("Faceted: the same triangles as the OpenGL view. Smooth: exact spheres, cones, disks and tori.")]
    public RaytraceGeometry Geometry { get; set; } = RaytraceGeometry.Faceted;

    [Category("2 Light"), DisplayName("Shadows"), Description("Lights cast shadows (each light and element can opt out with 'Casts shadows'). A light with a radius casts soft shadows.")]
    public bool Shadows { get; set; } = true;

    [Category("2 Light"), DisplayName("Ambient occlusion"), Description("Creases and contacts get less ambient light, found by rays.")]
    public bool AmbientOcclusion { get; set; }

    [Category("2 Light"), DisplayName("Occlusion rays"), Description("Rays per sample for the ambient occlusion (1..64).")]
    public int AoRays { get; set; } = 4;

    [Category("2 Light"), DisplayName("Occlusion distance"), Description("How far the occlusion rays look, in scene units.")]
    public float AoDistance { get; set; } = 0.5f;

    [Category("3 Motion blur"), DisplayName("Shutter"), Description("How long the shutter is open, as a part of one frame (0 = no motion blur, 1 = the whole frame).")]
    public float Shutter { get; set; }

    [Category("3 Motion blur"), DisplayName("Steps"), Description("How many times within the shutter the animation is evaluated (2..16); fast rotations need more.")]
    public int BlurSteps { get; set; } = 3;

    /// <summary>Settings this version does not know (from a newer version); kept and written back unchanged.</summary>
    [Browsable(false)]
    public Dictionary<string, string> Unknown { get; private set; } = new(StringComparer.Ordinal);

    public RaytraceSettings Clone()
    {
        var copy = (RaytraceSettings)MemberwiseClone();
        copy.Unknown = new Dictionary<string, string>(Unknown, StringComparer.Ordinal);
        return copy;
    }

    /// <summary>Copies all values from <paramref name="other"/>.</summary>
    public void CopyFrom(RaytraceSettings other)
    {
        foreach (var p in typeof(RaytraceSettings).GetProperties())
            if (p.CanWrite && p.GetSetMethod() != null)
                p.SetValue(this, p.GetValue(other));
        Unknown = new Dictionary<string, string>(other.Unknown, StringComparer.Ordinal);
    }

    /// <summary>The values clamped to the documented ranges.</summary>
    public void Normalize()
    {
        Samples = Math.Clamp(Samples, 1, 4096);
        Bounces = Math.Clamp(Bounces, 0, 32);
        if (!Enum.IsDefined(Geometry)) Geometry = RaytraceGeometry.Faceted;
        AoRays = Math.Clamp(AoRays, 1, 64);
        AoDistance = Math.Clamp(AoDistance, 0.001f, 1000f);
        Shutter = Math.Clamp(Shutter, 0f, 1f);
        BlurSteps = Math.Clamp(BlurSteps, 2, 16);
    }
}
