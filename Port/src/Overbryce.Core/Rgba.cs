using System.Globalization;

namespace Overbryce.Core;

/// <summary>
/// RGBA colour with float components in the 0..1 range (the original stored four separate
/// GLfloat members per material component, e.g. rDif, gDif, bDif, aDif).
/// </summary>
public struct Rgba : IEquatable<Rgba>
{
    public float R { get; set; }
    public float G { get; set; }
    public float B { get; set; }
    public float A { get; set; }

    public Rgba(float r, float g, float b, float a)
    {
        R = r; G = g; B = b; A = a;
    }

    public static Rgba Black => new(0, 0, 0, 1);
    public static Rgba White => new(1, 1, 1, 1);

    /// <summary>Component-wise Catmull-Rom step, see <see cref="SplineWeights.Interpolate(float, float, float, float)"/>.</summary>
    public static Rgba Interpolate(in SplineWeights w, Rgba p1, Rgba p2, Rgba p3, Rgba p4) => new(
        w.Interpolate(p1.R, p2.R, p3.R, p4.R),
        w.Interpolate(p1.G, p2.G, p3.G, p4.G),
        w.Interpolate(p1.B, p2.B, p3.B, p4.B),
        w.Interpolate(p1.A, p2.A, p3.A, p4.A));

    public readonly bool Equals(Rgba other) => R == other.R && G == other.G && B == other.B && A == other.A;
    public override readonly bool Equals(object? obj) => obj is Rgba other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(R, G, B, A);
    public static bool operator ==(Rgba a, Rgba b) => a.Equals(b);
    public static bool operator !=(Rgba a, Rgba b) => !a.Equals(b);

    public override readonly string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"{R:0.###}; {G:0.###}; {B:0.###}; {A:0.###}");
}
