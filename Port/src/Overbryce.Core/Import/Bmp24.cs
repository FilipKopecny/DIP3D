namespace Overbryce.Core.Import;

/// <summary>The uncompressed 24-bit BMP files the Blender import writes, read and changed in place.</summary>
public static class Bmp24
{
    /// <summary>
    /// Changes the colour of every texel (values 0..1 in, 0..1 out), except pure white, which is transparent in
    /// Overbryce; no other texel becomes pure white.
    /// </summary>
    public static void Recolor(string path, Func<double, double, double, (double R, double G, double B)> color)
    {
        if (!File.Exists(path)) return;
        var data = File.ReadAllBytes(path);
        if (data.Length < 54 || data[0] != 'B' || data[1] != 'M') return;
        int offset = BitConverter.ToInt32(data, 10);
        int width = BitConverter.ToInt32(data, 18);
        int height = Math.Abs(BitConverter.ToInt32(data, 22));
        int bits = BitConverter.ToInt16(data, 28);
        int compression = BitConverter.ToInt32(data, 30);
        if (bits != 24 || compression != 0 || width <= 0) return;
        int row = (width * 3 + 3) & ~3;
        if (offset + (long)row * height > data.Length) return;
        var cache = new Dictionary<int, (byte, byte, byte)>();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = offset + y * row + x * 3;
                byte b = data[i], g = data[i + 1], r = data[i + 2];
                if (r == 255 && g == 255 && b == 255) continue;
                int key = r << 16 | g << 8 | b;
                if (!cache.TryGetValue(key, out var c))
                {
                    var (nr, ng, nb) = color(r / 255.0, g / 255.0, b / 255.0);
                    c = (ToByte(nr), ToByte(ng), ToByte(nb));
                    if (c is (255, 255, 255)) c = (254, 254, 254);
                    cache[key] = c;
                }
                (data[i + 2], data[i + 1], data[i]) = c;
            }
        File.WriteAllBytes(path, data);
    }

    private static byte ToByte(double v) => (byte)Math.Clamp((int)Math.Round(v * 255), 0, 255);
}
