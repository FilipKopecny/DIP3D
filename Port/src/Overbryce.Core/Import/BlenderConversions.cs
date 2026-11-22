namespace Overbryce.Core.Import;

/// <summary>
/// Blender's physical values turned into Overbryce's (OpenGL 1.1) ones. Blender renders light in linear values and
/// converts the picture to display values at the end; Overbryce adds the light of its lights in display values. A
/// surface of albedo a lit by E shows (a·E)^(1/2.2) = a′·E′ in Blender, so a colour becomes a′ = a^(1/2.2) and the
/// light that reaches a surface E′ = E^(1/2.2). Only the sum of several lights differs (Overbryce adds a little too
/// much where they overlap).
/// </summary>
public static class BlenderConversions
{
    public const double Gamma = 2.2;

    /// <summary>Overbryce's global ambient light (GL_LIGHT_MODEL_AMBIENT), which the world colour is expressed in.</summary>
    public const double GlobalAmbient = 0.2;

    /// <summary>A linear value as the display shows it.</summary>
    public static double Display(double linear) => linear <= 0 ? 0 : Math.Pow(linear, 1 / Gamma);

    public static (double R, double G, double B) Display(double[] rgb) =>
        (Display(rgb.Length > 0 ? rgb[0] : 0), Display(rgb.Length > 1 ? rgb[1] : 0), Display(rgb.Length > 2 ? rgb[2] : 0));

    /// <summary>
    /// Blender's AgX view transform (the default since Blender 4.0), approximated as in the widely used real-time fit:
    /// a slight desaturation matrix, a logarithmic encoding of -12.5..+4 stops, a sigmoid and the inverse matrix.
    /// The result is a display value.
    /// </summary>
    public static (double R, double G, double B) Agx(double r, double g, double b)
    {
        const double minEv = -12.47393, maxEv = 4.026069;
        double ir = 0.842479062253094 * r + 0.0784335999999992 * g + 0.0792237451477643 * b;
        double ig = 0.0423282422610123 * r + 0.878468636469772 * g + 0.0791661274605434 * b;
        double ib = 0.0423756549057051 * r + 0.0784336 * g + 0.879142973793104 * b;
        static double Curve(double x, double lo, double hi)
        {
            x = Math.Clamp(Math.Log2(Math.Max(x, 1e-10)), lo, hi);
            x = (x - lo) / (hi - lo);
            double x2 = x * x, x4 = x2 * x2;
            return 15.5 * x4 * x2 - 40.14 * x4 * x + 31.96 * x4 - 6.868 * x2 * x + 0.4298 * x2 + 0.1191 * x - 0.00232;
        }
        double a = Curve(ir, minEv, maxEv), c = Curve(ig, minEv, maxEv), e = Curve(ib, minEv, maxEv);
        double or = 1.19687900512017 * a - 0.0980208811401368 * c - 0.0990297440797205 * e;
        double og = -0.0528968517574562 * a + 1.15190312990417 * c - 0.0989611768448433 * e;
        double ob = -0.0529716355144438 * a - 0.0980434501171241 * c + 1.15107367264116 * e;
        return (Math.Clamp(or, 0, 1), Math.Clamp(og, 0, 1), Math.Clamp(ob, 0, 1));
    }

    /// <summary>
    /// The colour of a surface as the view transform shows it, relative to white: what a lit surface of this albedo
    /// looks like next to a white one (Standard: the display curve; AgX: also its desaturation and contrast).
    /// </summary>
    public static (double R, double G, double B) SurfaceColor(double[] rgb, bool agx, double light = 1)
    {
        if (!agx) return Display(rgb);
        double r = rgb.Length > 0 ? rgb[0] : 0, g = rgb.Length > 1 ? rgb[1] : 0, b = rgb.Length > 2 ? rgb[2] : 0;
        light = Math.Max(light, 1e-6);
        var (cr, cg, cb) = Agx(r * light, g * light, b * light);
        var (wr, wg, wb) = Agx(light, light, light);
        return (cr / wr, cg / wg, cb / wb);
    }

    /// <summary>How the view transform shows a grey value.</summary>
    public static double View(double linear, bool agx) => agx ? Agx(linear, linear, linear).G : Display(linear);

    /// <summary>The linear value of an sRGB display value (0..1), as Blender reads 8-bit images.</summary>
    public static double FromSrgb(double v) => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);

    /// <summary>The relative luminance of a linear colour.</summary>
    public static double Luminance(double r, double g, double b) => 0.2126 * r + 0.7152 * g + 0.0722 * b;

    /// <summary>How AgX shows white of value 1 (the plain display curve shows 1).</summary>
    public static double AgxWhite => Agx(1, 1, 1).G;

    /// <summary>A colour that is seen directly (the sky, a glowing surface) as the view transform shows it.</summary>
    public static (double R, double G, double B) SeenColor(double r, double g, double b, bool agx) =>
        agx ? Agx(r, g, b) : (Display(r), Display(g), Display(b));

    /// <summary>
    /// Irradiance factor k of a light of power P: a white diffuse surface at distance d shows P·k / d² (linear).
    /// Point and spot lights spread their power over the sphere (k = 1/(4π²)), an area light over the half space in
    /// front of it (k = 1/π²).
    /// </summary>
    public static double PointFactor => 1 / (4 * Math.PI * Math.PI);
    public static double AreaFactor => 1 / (Math.PI * Math.PI);

    /// <summary>
    /// OpenGL attenuation 1/(c + l·d + q·d²) that follows a light of radius <paramref name="radius"/> as the display shows it,
    /// (d² + R²)^(-1/2.2), between <paramref name="near"/> and <paramref name="far"/> (least relative error, all three ≥ 0).
    /// Without the display curve this would be c = R², q = 1; with it the fall-off is nearly linear.
    /// </summary>
    public static (double C, double L, double Q) FitAttenuation(double radius, double near, double far)
    {
        near = Math.Max(near, 1e-4);
        far = Math.Max(far, near * 1.5);
        const int n = 32;
        var d = new double[n];
        var g = new double[n];
        for (int i = 0; i < n; i++)
        {
            d[i] = near * Math.Pow(far / near, i / (double)(n - 1));
            g[i] = Math.Pow(d[i] * d[i] + radius * radius, -1 / Gamma);
        }
        // minimise Σ (g·(c + l d + q d²) - 1)² over the subsets of the three terms with non-negative solutions
        double best = double.MaxValue;
        (double, double, double) result = (1, 0, 0);
        for (int mask = 1; mask < 8; mask++)
        {
            var terms = Enumerable.Range(0, 3).Where(t => (mask & (1 << t)) != 0).ToArray();
            int k = terms.Length;
            var a = new double[k, k];
            var b = new double[k];
            for (int i = 0; i < n; i++)
            {
                var row = new double[k];
                for (int t = 0; t < k; t++) row[t] = g[i] * Math.Pow(d[i], terms[t]);
                for (int r = 0; r < k; r++)
                {
                    b[r] += row[r];
                    for (int c = 0; c < k; c++) a[r, c] += row[r] * row[c];
                }
            }
            var x = Solve(a, b);
            if (x == null || x.Any(v => v < 0)) continue;
            var coef = new double[3];
            for (int t = 0; t < k; t++) coef[terms[t]] = x[t];
            double err = 0;
            for (int i = 0; i < n; i++)
            {
                double e = g[i] * (coef[0] + coef[1] * d[i] + coef[2] * d[i] * d[i]) - 1;
                err += e * e;
            }
            if (err < best)
            {
                best = err;
                result = (coef[0], coef[1], coef[2]);
            }
        }
        return result;
    }

    /// <summary>Gaussian elimination with partial pivoting; null when singular.</summary>
    private static double[]? Solve(double[,] a, double[] b)
    {
        int n = b.Length;
        var m = (double[,])a.Clone();
        var v = (double[])b.Clone();
        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            for (int r = col + 1; r < n; r++)
                if (Math.Abs(m[r, col]) > Math.Abs(m[pivot, col])) pivot = r;
            if (Math.Abs(m[pivot, col]) < 1e-300) return null;
            if (pivot != col)
            {
                for (int c = 0; c < n; c++) (m[col, c], m[pivot, c]) = (m[pivot, c], m[col, c]);
                (v[col], v[pivot]) = (v[pivot], v[col]);
            }
            for (int r = 0; r < n; r++)
            {
                if (r == col) continue;
                double f = m[r, col] / m[col, col];
                if (f == 0) continue;
                for (int c = col; c < n; c++) m[r, c] -= f * m[col, c];
                v[r] -= f * v[col];
            }
        }
        var x = new double[n];
        for (int i = 0; i < n; i++) x[i] = v[i] / m[i, i];
        return x;
    }

    /// <summary>
    /// The OpenGL spot exponent of a Blender spot whose soft edge is <paramref name="blend"/> of its cone (the inverse
    /// of the export): cos^exponent falls to one half where Blender's soft edge begins to darken.
    /// </summary>
    public static double SpotExponent(double cutoffDegrees, double blend)
    {
        if (blend < 0.06) return 0;
        double half = cutoffDegrees * (1 - Math.Clamp(blend, 0, 1)) * Math.PI / 180;
        double cos = Math.Cos(half);
        if (cos >= 1 - 1e-9) return 128;
        return Math.Clamp(Math.Log(0.5) / Math.Log(cos), 0, 128);
    }

    /// <summary>Overbryce's shininess (0..1, ×128 = Phong exponent) of a GGX roughness: n = 2/α² - 2 with α = roughness².</summary>
    public static double Shininess(double roughness)
    {
        double alpha = Math.Max(roughness * roughness, 0.02);
        return Math.Clamp((2 / (alpha * alpha) - 2) / 128, 1 / 128.0, 1);
    }

    /// <summary>
    /// The Phong specular strength of a highlight as bright as Blender's at its peak: a dielectric reflects a few
    /// percent, but concentrated in a highlight that gets brighter the smoother the surface.
    /// </summary>
    public static double SpecularStrength(double roughness, double specularLevel, double metallic)
    {
        double alpha = Math.Max(roughness * roughness, 0.02);
        double dielectric = Math.Clamp(0.01 / (alpha * alpha) * specularLevel / 0.5, 0, 1);
        double metal = Math.Clamp(1 / (4 * alpha * alpha), 0, 1);
        return dielectric * (1 - metallic) + metal * metallic;
    }
}
