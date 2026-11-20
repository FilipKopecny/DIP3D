using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Numerics;

namespace Lumina;

/// <summary>
/// Procedural 128x128 textures for the Lumina scene. Overbryce stretches every texture to 128x128 and
/// makes pure white texels transparent (with alpha blending on), so colours are kept below 255 and
/// pure white is written only where a texel should be see-through.
/// </summary>
internal static class Textures
{
    private const int N = 128;

    /// <summary>Marker colour: written as pure white, i.e. transparent in Overbryce.</summary>
    private static readonly Vector3 Clear = new(-1, -1, -1);

    /// <summary>Original 1998 textures reused by the scene (copied next to the .ani file).</summary>
    public static readonly string[] Originals =
    [
        "Horec.bmp", "Hriby.bmp", "FialovyMramor.bmp", "TmaveDrevo.bmp", "DrsnyKamen.bmp", "SivyKamen.bmp",
        "Pieskovec.bmp", "Ryby.bmp", "Voda.bmp", "ModroRuzovyMramor.bmp", "ZelenyMramor.bmp",
    ];

    public static void Generate(string dir, string originals)
    {
        foreach (var name in Originals)
            File.Copy(Path.Combine(originals, name), Path.Combine(dir, name), overwrite: true);

        Paint(dir, "Hmlovina.bmp", Nebula);
        Paint(dir, "Obor.bmp", GasGiant);
        Paint(dir, "Prstence.bmp", Rings);
        Paint(dir, "Povrch.bmp", MoonSurface);
        Paint(dir, "Mach.bmp", Moss);
        Paint(dir, "Skafander.bmp", Suit);
        Paint(dir, "Hledi.bmp", Visor);
        Paint(dir, "Panel.bmp", Panel);
        Paint(dir, "Klobuk.bmp", MushroomCap);
        Paint(dir, "Lupene.bmp", Gills);
        Paint(dir, "Stonka.bmp", Stem);
        Paint(dir, "KrystalLum.bmp", Crystal);
        Paint(dir, "Manta.bmp", MantaWing);
        Paint(dir, "Meduza.bmp", Jelly);
        Paint(dir, "Paprad.bmp", Frond);
        Paint(dir, "Praslen.bmp", Whorl);
        Paint(dir, "Prach.bmp", Dust);
        Paint(dir, "Plamen.bmp", Flame);
        Paint(dir, "Ovladac.bmp", ChestPanel);
        Foil(Path.Combine(originals, "Papier.bmp"), Path.Combine(dir, "Folia.bmp"));
        Emblem(Path.Combine(dir, "Znak.bmp"));
    }

    // ------------------------------------------------------------------ painters (u, v in [0,1), v = 0 is the top row)

    private static Vector3 Nebula(float u, float v)
    {
        float n1 = Fbm(u, v, 3, 5, 11), n2 = Fbm(u, v, 4, 5, 23), n3 = Fbm(u, v, 6, 4, 37);
        float poles = Kit.Smooth(0.02f, 0.3f, v) * (1 - Kit.Smooth(0.7f, 0.98f, v));
        var c = new Vector3(0.012f, 0.012f, 0.035f);
        c += new Vector3(0.36f, 0.12f, 0.55f) * MathF.Pow(n1, 2.6f) * 1.9f * poles;
        c += new Vector3(0.05f, 0.42f, 0.5f) * MathF.Pow(n2, 3.2f) * 1.7f * poles;
        c += new Vector3(0.75f, 0.25f, 0.45f) * MathF.Pow(MathF.Max(0, n3 - 0.35f), 2f) * 2.2f * poles;
        // faint dust lanes
        c *= 0.55f + 0.45f * Kit.Smooth(0.25f, 0.6f, Fbm(u, v, 8, 3, 51));
        return c;
    }

    private static Vector3 GasGiant(float u, float v)
    {
        float warp = (Fbm(u, v, 4, 5, 3) - 0.5f) * 0.06f + (Fbm(u, v, 12, 3, 7) - 0.5f) * 0.02f;
        float b = v + warp;
        Vector3[] palette =
        [
            new(0.93f, 0.86f, 0.70f), new(0.84f, 0.64f, 0.42f), new(0.70f, 0.40f, 0.24f),
            new(0.92f, 0.80f, 0.62f), new(0.60f, 0.62f, 0.62f), new(0.86f, 0.70f, 0.50f),
        ];
        float x = b * 14f + 2f * Fbm(u, b, 1, 3, 9);
        int i = (int)MathF.Floor(x);
        float f = Kit.Smooth(0.3f, 0.7f, x - i);
        var c = Vector3.Lerp(palette[Mod(i, palette.Length)], palette[Mod(i + 1, palette.Length)], f);
        c *= 0.88f + 0.24f * Fbm(u, v, 16, 3, 19);
        // great storm
        float dx = Wrap(u - 0.32f) / 0.09f, dy = (v - 0.63f) / 0.035f;
        float r = MathF.Sqrt(dx * dx + dy * dy);
        if (r < 1.3f)
        {
            float s = 1 - Kit.Smooth(0.6f, 1.3f, r);
            var storm = Vector3.Lerp(new Vector3(0.72f, 0.3f, 0.2f), new Vector3(0.9f, 0.55f, 0.4f), 0.5f + 0.5f * MathF.Sin(r * 9 + MathF.Atan2(dy, dx) * 2));
            c = Vector3.Lerp(c, storm, s);
        }
        // darker, bluish poles
        float pole = 1 - Kit.Smooth(0.0f, 0.18f, MathF.Min(v, 1 - v));
        return Vector3.Lerp(c, new Vector3(0.42f, 0.48f, 0.56f), pole * 0.8f);
    }

    private static Vector3 Rings(float u, float v)
    {
        float dx = (u - 0.5f) * 2, dy = (v - 0.5f) * 2;
        float r = MathF.Sqrt(dx * dx + dy * dy);
        const float inner = 13.5f / 19f;
        if (r < inner || r > 0.995f) return Clear;
        if (r > 0.865f && r < 0.885f) return Clear;           // the big gap
        float x = (r - inner) / (1 - inner);
        float bands = 0.6f + 0.4f * MathF.Sin(x * 70) * MathF.Sin(x * 23 + 1);
        if (bands < 0.28f) return Clear;                       // thin gaps
        var c = Vector3.Lerp(new Vector3(0.78f, 0.7f, 0.58f), new Vector3(0.6f, 0.62f, 0.7f), x);
        return c * (0.55f + 0.45f * bands);
    }

    private static Vector3 MoonSurface(float u, float v)
    {
        // the same palette as the moss on the terrain patches, so the seam between them stays quiet;
        // the sphere's texture wraps once around the moon, hence the higher frequencies along u
        float n = Fbm(u, v, 8, 5, 5), lich = Fbm(u, v, 6, 4, 77), fine = Fbm(u, v, 24, 3, 15);
        var c = Vector3.Lerp(new Vector3(0.1f, 0.33f, 0.3f), new Vector3(0.24f, 0.52f, 0.42f), n);
        c = Vector3.Lerp(c, new Vector3(0.42f, 0.26f, 0.5f), Kit.Smooth(0.52f, 0.66f, lich) * 0.85f);
        c *= 0.8f + 0.4f * fine;
        // shallow dark craters
        for (int k = 0; k < 18; k++)
        {
            float cx = Kit.Rnd(k, 1), cy = 0.15f + 0.7f * Kit.Rnd(k, 2), cr = 0.012f + 0.03f * Kit.Rnd(k, 3);
            float d = MathF.Sqrt(Sq(Wrap(u - cx)) + Sq(2 * (v - cy))) / cr;
            if (d < 1) c *= 0.72f + 0.28f * d * d;
        }
        return c;
    }

    private static Vector3 Moss(float u, float v)
    {
        float n = Fbm(u, v, 4, 5, 101), fine = Fbm(u, v, 16, 3, 103), lich = Fbm(u, v, 3, 4, 107);
        var c = Vector3.Lerp(new Vector3(0.1f, 0.33f, 0.3f), new Vector3(0.24f, 0.52f, 0.42f), n);
        c = Vector3.Lerp(c, new Vector3(0.42f, 0.26f, 0.5f), Kit.Smooth(0.55f, 0.68f, lich) * 0.85f);
        c *= 0.75f + 0.5f * fine;
        // pebbles and glowing specks
        float h = Hash((int)(u * N), (int)(v * N), 9);
        if (h > 0.985f) c = new Vector3(0.55f, 0.95f, 0.9f);
        else if (h > 0.955f) c *= 1.35f;
        else if (h < 0.02f) c *= 0.55f;
        return c;
    }

    private static Vector3 Suit(float u, float v)
    {
        float n = Fbm(u, v, 16, 3, 61);
        var c = new Vector3(0.9f, 0.89f, 0.86f) * (0.93f + 0.1f * n);
        float sx = Frac(u * 4), sy = Frac(v * 5);
        if (sx < 0.04f || sy < 0.04f) c *= 0.8f;                // quilting seams
        if (v > 0.44f && v < 0.52f) c = new Vector3(0.95f, 0.45f, 0.12f) * (0.9f + 0.1f * n); // orange band
        return c;
    }

    private static Vector3 Visor(float u, float v)
    {
        // latitude gradient: dark sky above, a warm horizon, ground glow below
        var sky = new Vector3(0.08f, 0.06f, 0.16f);
        var horizon = new Vector3(0.98f, 0.72f, 0.3f);
        var ground = new Vector3(0.25f, 0.42f, 0.38f);
        var c = v < 0.5f ? Vector3.Lerp(sky, horizon, Kit.Smooth(0.15f, 0.5f, v)) : Vector3.Lerp(horizon, ground, Kit.Smooth(0.5f, 0.75f, v));
        c *= 0.85f + 0.15f * Fbm(u, v, 6, 2, 71);
        float streak = MathF.Exp(-Sq((Wrap(u - 0.3f) + (v - 0.35f) * 0.4f) / 0.03f));
        return c + new Vector3(0.6f, 0.55f, 0.5f) * streak;
    }

    private static Vector3 Panel(float u, float v)
    {
        float px = Frac(u * 4), py = Frac(v * 4);
        int cell = (int)(u * 4) + 4 * (int)(v * 4);
        var c = new Vector3(0.72f, 0.74f, 0.78f) * (0.85f + 0.15f * Kit.Rnd(cell, 5)) * (0.94f + 0.08f * Fbm(u, v, 12, 2, 41));
        if (px < 0.04f || py < 0.04f) c *= 0.55f;
        if ((MathF.Abs(px - 0.12f) < 0.03f || MathF.Abs(px - 0.88f) < 0.03f) && (MathF.Abs(py - 0.12f) < 0.03f || MathF.Abs(py - 0.88f) < 0.03f))
            c *= 0.6f;                                          // rivets
        return c;
    }

    private static Vector3 MushroomCap(float u, float v)
    {
        // u around the cone, v = 0 at the tip (top row), v = 1 at the rim
        var c = Vector3.Lerp(new Vector3(0.95f, 0.9f, 0.95f) * 0.75f, new Vector3(0.45f, 0.4f, 0.5f), Kit.Smooth(0.1f, 1f, v));
        c *= 0.85f + 0.2f * Fbm(u, v, 8, 3, 31);
        for (int k = 0; k < 22; k++)
        {
            float cx = Kit.Rnd(k, 31), cy = 0.15f + 0.75f * Kit.Rnd(k, 32), cr = 0.035f + 0.04f * Kit.Rnd(k, 33);
            float d = MathF.Sqrt(Sq(Wrap(u - cx)) + Sq((v - cy) * 0.6f)) / cr;
            if (d < 1) return Vector3.Lerp(new Vector3(0.98f, 0.98f, 0.9f), new Vector3(0.8f, 0.95f, 0.98f), d) * 0.98f;
        }
        if (v > 0.93f) c *= 0.6f;                               // rim
        return c;
    }

    private static Vector3 Gills(float u, float v)
    {
        float dx = u - 0.5f, dy = v - 0.5f;
        float r = MathF.Sqrt(dx * dx + dy * dy) * 2, a = MathF.Atan2(dy, dx);
        if (r > 0.995f) return Clear;
        float lines = 0.5f + 0.5f * MathF.Cos(a * 48);
        var c = Vector3.Lerp(new Vector3(0.35f, 0.25f, 0.4f), new Vector3(0.85f, 0.78f, 0.9f), lines * Kit.Smooth(0.15f, 0.6f, r));
        return r < 0.12f ? new Vector3(0.5f, 0.45f, 0.45f) : c;
    }

    private static Vector3 Stem(float u, float v)
    {
        float n = Fbm(u * 6, v * 0.5f, 4, 3, 81);
        return new Vector3(0.85f, 0.88f, 0.8f) * (0.75f + 0.3f * n) * (0.9f + 0.1f * MathF.Sin(u * 80));
    }

    private static Vector3 Crystal(float u, float v)
    {
        float facet = Frac(u * 6);
        float shade = 0.7f + 0.3f * MathF.Sin(facet * MathF.PI);
        var c = new Vector3(0.82f, 0.92f, 0.97f) * shade;
        c *= 0.85f + 0.2f * Fbm(u, v, 2, 3, 91);
        float streak = MathF.Exp(-Sq((facet - 0.3f - 0.2f * v) / 0.04f));
        c += new Vector3(0.15f, 0.08f, 0.0f) * streak;
        if (Hash((int)(u * N), (int)(v * N), 93) > 0.99f) c = new Vector3(0.97f, 0.97f, 0.97f);
        return Vector3.Min(c, new Vector3(0.97f));
    }

    private static Vector3 MantaWing(float u, float v)
    {
        // u across the span (0.5 = body), v along the chord (0 = leading edge)
        float span = MathF.Abs(u - 0.5f) * 2;
        var c = Vector3.Lerp(new Vector3(0.18f, 0.12f, 0.38f), new Vector3(0.38f, 0.2f, 0.55f), span);
        c *= 0.85f + 0.25f * Fbm(u, v, 8, 3, 111);
        float spots = MathF.Sin(span * 22) * MathF.Sin(v * 12);
        if (spots > 0.8f) c = new Vector3(0.45f, 0.95f, 0.92f);
        if (v < 0.08f) c = Vector3.Lerp(c, new Vector3(0.9f, 0.6f, 0.95f), 0.6f);
        return c;
    }

    private static Vector3 Jelly(float u, float v)
    {
        float stripes = 0.5f + 0.5f * MathF.Cos(u * MathF.PI * 2 * 8);
        var c = Vector3.Lerp(new Vector3(0.75f, 0.45f, 0.9f), new Vector3(0.98f, 0.75f, 0.95f), stripes * 0.7f);
        c *= 0.8f + 0.25f * Kit.Smooth(0.2f, 0.7f, v);
        return Vector3.Min(c, new Vector3(0.97f));
    }

    private static Vector3 Frond(float u, float v)
    {
        // the frond grows from the bottom row (v = 1) to the top (v = 0); u = 0.5 is the stalk
        float along = 1 - v, across = u - 0.5f;
        if (along < 0.03f || along > 0.97f) return Clear;
        var col = Vector3.Lerp(new Vector3(0.15f, 0.55f, 0.45f), new Vector3(0.5f, 0.3f, 0.7f), along);
        if (MathF.Abs(across) < 0.018f) return col * 0.7f;
        float width = 0.42f * MathF.Sin(MathF.PI * MathF.Min(1, along * 1.1f)) * (1 - 0.35f * along);
        if (MathF.Abs(across) > width) return Clear;
        // leaflets: diagonal teeth
        float t = Frac(along * 11 + MathF.Abs(across) * 2.2f);
        if (t > 0.78f) return Clear;
        float vein = MathF.Exp(-Sq((t - 0.38f) / 0.06f));
        return col * (0.75f + 0.25f * Fbm(u, v, 8, 2, 121)) + new Vector3(0.25f, 0.45f, 0.4f) * vein;
    }

    /// <summary>
    /// A whorl of seven curved fern fronds for the spinning rings on the trees (a disk's planar mapping:
    /// the centre of the texture is the centre of the disk). The fronds differ in length and colour, so
    /// the turning is easy to follow; everything between them is transparent.
    /// </summary>
    private static Vector3 Whorl(float u, float v)
    {
        const int fronds = 7;
        const float r0 = 0.2f;
        float x = (u - 0.5f) * 2, y = (v - 0.5f) * 2, r = MathF.Sqrt(x * x + y * y);
        if (r < r0 || r > 0.99f) return Clear;

        // a pinwheel: each frond sweeps back as it grows outwards
        float phi = MathF.Atan2(y, x) + 1.5f * (r - r0);
        float sector = phi * fronds / (2 * MathF.PI);
        int k = (int)MathF.Round(sector);
        int leaf = ((k % fronds) + fronds) % fronds;
        float length = 0.72f + 0.27f * Kit.Rnd(leaf, 141);              // fronds of different lengths
        float along = (r - r0) / (length - r0);                          // 0 at the base, 1 at the tip
        if (along > 1) return Clear;
        float across = (sector - k) * 2 * MathF.PI / fronds * r;         // distance from the midrib
        float width = 0.13f * MathF.Sin(MathF.PI * MathF.Min(1, along * 1.08f)) * (1 - 0.3f * along) + 0.008f;
        if (MathF.Abs(across) > width) return Clear;

        var baseCol = leaf % 2 == 0 ? new Vector3(0.12f, 0.58f, 0.5f) : new Vector3(0.2f, 0.5f, 0.62f);
        var tipCol = leaf == 0 ? new Vector3(0.95f, 0.85f, 0.35f) : new Vector3(0.55f, 0.32f, 0.72f); // one golden frond
        var col = Vector3.Lerp(baseCol, tipCol, Kit.Smooth(0.35f, 1f, along));
        float n = MathF.Abs(across) / width;                              // 0 on the midrib, 1 on the edge
        if (n < 0.14f) return col * 0.65f + new Vector3(0.1f, 0.12f, 0.1f);
        // leaflets: chevron teeth along the frond
        float t = Frac(along * 9 + n * 0.9f);
        if (t > 0.74f) return Clear;
        float vein = MathF.Exp(-Sq((t - 0.36f) / 0.07f));
        return Vector3.Min(col * (0.8f + 0.25f * Fbm(u, v, 8, 2, 143)) + new Vector3(0.2f, 0.35f, 0.3f) * vein, new Vector3(0.97f));
    }

    private static Vector3 Dust(float u, float v)
    {
        float dx = (u - 0.5f) * 2, dy = (v - 0.5f) * 2, r = MathF.Sqrt(dx * dx + dy * dy);
        if (r > 0.99f || r < 0.3f) return Clear;
        float n = Fbm(u, v, 8, 4, 131);
        if (n < 0.38f) return Clear;
        return new Vector3(0.85f, 0.8f, 0.86f) * (0.7f + 0.3f * n);
    }

    private static Vector3 Flame(float u, float v)
    {
        // v = 1 (bottom row) is the nozzle, v = 0 the tip
        float h = 1 - v;
        float n = Fbm(u * 3, h, 4, 3, 141);
        var hot = new Vector3(0.98f, 0.95f, 0.78f);
        var warm = new Vector3(0.98f, 0.62f, 0.2f);
        var cool = new Vector3(0.75f, 0.22f, 0.35f);
        var c = h < 0.5f ? Vector3.Lerp(hot, warm, h * 2) : Vector3.Lerp(warm, cool, (h - 0.5f) * 2);
        return Vector3.Min(c * (0.8f + 0.35f * n), new Vector3(0.98f));
    }

    private static Vector3 ChestPanel(float u, float v)
    {
        var c = new Vector3(0.18f, 0.2f, 0.25f);
        Vector3[] leds = [new(0.95f, 0.2f, 0.2f), new(0.2f, 0.9f, 0.3f), new(0.25f, 0.6f, 0.98f), new(0.98f, 0.8f, 0.2f)];
        for (int k = 0; k < 4; k++)
        {
            float d = MathF.Sqrt(Sq(u - (0.2f + 0.2f * k)) + Sq(v - 0.35f));
            if (d < 0.07f) return leds[k];
        }
        if (v > 0.58f && v < 0.8f && u > 0.15f && u < 0.85f) return new Vector3(0.1f, 0.35f, 0.4f) * (0.8f + 0.4f * MathF.Sin(u * 40));
        return c;
    }

    // ------------------------------------------------------------------ derived from bitmaps

    /// <summary>Crinkled gold foil made from the original crumpled-paper texture.</summary>
    private static void Foil(string source, string target)
    {
        using var src = new Bitmap(source);
        using var dst = new Bitmap(N, N, PixelFormat.Format24bppRgb);
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            var p = src.GetPixel(x * src.Width / N, y * src.Height / N);
            float l = (0.3f * p.R + 0.59f * p.G + 0.11f * p.B) / 255f;
            l = Math.Clamp((l - 0.35f) * 1.6f + 0.55f, 0, 1);
            dst.SetPixel(x, y, Color.FromArgb(Byte(l * 1.0f), Byte(l * 0.78f), Byte(l * 0.32f)));
        }
        dst.Save(target, ImageFormat.Bmp);
    }

    /// <summary>Expedition badge for the lander (white = transparent).</summary>
    private static void Emblem(string target)
    {
        using var bmp = new Bitmap(N, N, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.None;
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            g.Clear(Color.White);
            using var navy = new SolidBrush(Color.FromArgb(20, 30, 90));
            using var gold = new SolidBrush(Color.FromArgb(240, 190, 60));
            using var teal = new SolidBrush(Color.FromArgb(60, 200, 190));
            g.FillEllipse(gold, 8, 4, 112, 112);
            g.FillEllipse(navy, 13, 9, 102, 102);
            g.FillEllipse(teal, 30, 30, 50, 50);         // the moon
            g.FillEllipse(navy, 44, 22, 50, 50);         // crescent cut
            var star = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                float r = i % 2 == 0 ? 15 : 6, a = -MathF.PI / 2 + i * MathF.PI / 5;
                star[i] = new PointF(84 + r * MathF.Cos(a), 44 + r * MathF.Sin(a));
            }
            g.FillPolygon(gold, star);
            using var font = new Font("Arial", 15, FontStyle.Bold, GraphicsUnit.Pixel);
            var fmt = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString("LUMINA I", font, gold, 64, 86, fmt);
        }
        // anti-aliasing is off, so the background stays exactly white (transparent)
        bmp.Save(target, ImageFormat.Bmp);
    }

    // ------------------------------------------------------------------ infrastructure

    private static void Paint(string dir, string name, Func<float, float, Vector3> f)
    {
        using var bmp = new Bitmap(N, N, PixelFormat.Format24bppRgb);
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            var c = f((x + 0.5f) / N, (y + 0.5f) / N);
            bmp.SetPixel(x, y, c == Clear
                ? Color.White
                : Color.FromArgb(Byte(c.X, 254), Byte(c.Y, 254), Byte(c.Z, 254)));
        }
        bmp.Save(Path.Combine(dir, name), ImageFormat.Bmp);
    }

    private static int Byte(float v, int max = 255) => Math.Clamp((int)MathF.Round(v * 255), 0, max);
    private static float Sq(float x) => x * x;
    private static float Frac(float x) => x - MathF.Floor(x);
    private static int Mod(int a, int n) => ((a % n) + n) % n;

    /// <summary>Signed horizontal distance on a texture that wraps around (-0.5..0.5).</summary>
    private static float Wrap(float d) => d - MathF.Round(d);

    private static float Hash(int x, int y, int seed) => Kit.Rnd(x * 7919 + y, seed);

    /// <summary>Value noise with period <paramref name="p"/> in both directions (tileable).</summary>
    private static float Noise(float x, float y, int p, int seed)
    {
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        float h00 = Hash(Mod(x0, p), Mod(y0, p), seed), h10 = Hash(Mod(x0 + 1, p), Mod(y0, p), seed);
        float h01 = Hash(Mod(x0, p), Mod(y0 + 1, p), seed), h11 = Hash(Mod(x0 + 1, p), Mod(y0 + 1, p), seed);
        return Kit.Lerp(Kit.Lerp(h00, h10, fx), Kit.Lerp(h01, h11, fx), fy);
    }

    /// <summary>Tileable fractal noise in [0, 1] for u, v in [0, 1).</summary>
    private static float Fbm(float u, float v, int baseFreq, int octaves, int seed)
    {
        float sum = 0, amp = 0.5f, norm = 0;
        int f = baseFreq;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * Noise(u * f, v * f, f, seed + o * 17);
            norm += amp;
            amp *= 0.5f;
            f *= 2;
        }
        return sum / norm;
    }
}
