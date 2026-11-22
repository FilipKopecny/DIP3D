namespace Overbryce.Core;

/// <summary>
/// Weights of one in-between frame of the non-uniform Catmull-Rom spline used by the original
/// animator (CAnimacia::Render).
/// <para>
/// For the segment between key states 2 and 3 with neighbours 1 and 4 the original computes
/// <c>h1 = p2 - (p2 - p1) * dt23/dt12</c> and <c>h4 = p3 + (p4 - p3) * dt23/dt34</c>, i.e. it rescales the
/// outer control points so that the velocity at a key state is the average of the velocities of
/// the two neighbouring intervals (C1 continuous in time even for unevenly spaced key states), and
/// then evaluates the uniform Catmull-Rom basis at parameter u.
/// </para>
/// <para>
/// <see cref="EaseStart"/> / <see cref="EaseEnd"/> implement "start / end of motion" (the
/// thesis' <i>začiatok / koniec pohybu</i>, the "Final" check box) and the start and end of the
/// whole animation: the velocity at that key state is zero, so the motion accelerates from rest
/// and decelerates to rest. The original tried to achieve this by doubling the key state, which
/// only halves the velocity; see docs/changes-from-1998.md.
/// </para>
/// </summary>
public readonly struct SplineWeights
{
    public float C1 { get; }
    public float C2 { get; }
    public float C3 { get; }
    public float C4 { get; }

    /// <summary>dt23 / dt12 (original: dt2312).</summary>
    public float Ratio2312 { get; }

    /// <summary>dt23 / dt34 (original: dt2334).</summary>
    public float Ratio2334 { get; }

    public float U { get; }

    /// <summary>Zero velocity at the start of the segment (u = 0).</summary>
    public bool EaseStart { get; }

    /// <summary>Zero velocity at the end of the segment (u = 1).</summary>
    public bool EaseEnd { get; }

    public SplineWeights(float u, float ratio2312, float ratio2334)
        : this(u, ratio2312, ratio2334, false, false)
    {
    }

    public SplineWeights(float u, float ratio2312, float ratio2334, bool easeStart, bool easeEnd)
    {
        U = u;
        Ratio2312 = ratio2312;
        Ratio2334 = ratio2334;
        EaseStart = easeStart;
        EaseEnd = easeEnd;
        float u2 = u * u, u3 = u2 * u;
        C1 = -0.5f * u3 + u2 - 0.5f * u;
        C2 = 1.5f * u3 - 2.5f * u2 + 1f;
        C3 = -1.5f * u3 + 2f * u2 + 0.5f * u;
        C4 = 0.5f * u3 - 0.5f * u2;
    }

    /// <summary>Builds the weights from the key times t1..t4 (t2 &lt; t3) and the segment parameter u.</summary>
    public static SplineWeights FromKeyTimes(float t1, float t2, float t3, float t4, float u)
    {
        float dt23 = t3 - t2;
        float dt12 = t2 - t1;
        float dt34 = t4 - t3;
        if (dt12 == 0) dt12 = dt23;
        if (dt34 == 0) dt34 = dt23;
        return new SplineWeights(u, dt23 / dt12, dt23 / dt34);
    }

    /// <summary>The same frame with the given ease flags (replacing the current ones).</summary>
    public SplineWeights WithEase(bool easeStart, bool easeEnd)
        => new(U, Ratio2312, Ratio2334, easeStart, easeEnd);

    /// <summary>Interpolates one scalar attribute: p2 at u=0, p3 at u=1.</summary>
    public float Interpolate(float p1, float p2, float p3, float p4)
    {
        // the Catmull-Rom velocity at u=0 is (p3 - h1)/2 and at u=1 it is (h4 - p2)/2
        float h1 = EaseStart ? p3 : p2 - (p2 - p1) * Ratio2312;
        float h4 = EaseEnd ? p2 : p3 + (p4 - p3) * Ratio2334;
        return h1 * C1 + p2 * C2 + p3 * C3 + h4 * C4;
    }

    public double Interpolate(double p1, double p2, double p3, double p4)
        => Interpolate((float)p1, (float)p2, (float)p3, (float)p4);

    /// <summary>
    /// The weights of p1..p4 in <see cref="Interpolate(float, float, float, float)"/>, which is linear in them: the
    /// result is <c>w1 p1 + w2 p2 + w3 p3 + w4 p4</c>. Used to blend whole arrays (the vertices of a polygon mesh)
    /// with one set of weights, and to turn such a blend into Blender shape keys.
    /// </summary>
    public (float W1, float W2, float W3, float W4) Coefficients()
    {
        float w1 = 0, w2 = C2, w3 = C3, w4 = 0;
        // h1 = p3 (ease) or (1 - r) p2 + r p1
        if (EaseStart) w3 += C1;
        else { w1 += Ratio2312 * C1; w2 += (1 - Ratio2312) * C1; }
        // h4 = p2 (ease) or (1 - s) p3 + s p4
        if (EaseEnd) w2 += C4;
        else { w3 += (1 - Ratio2334) * C4; w4 += Ratio2334 * C4; }
        return (w1, w2, w3, w4);
    }
}
