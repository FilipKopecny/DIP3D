using System.Numerics;

namespace Overbryce.Raytrace;

/// <summary>
/// An element texture as the OpenGL renderers use it: square, RGBA, the bottom row first, pure
/// white texels with alpha 0. Sampled like GL_LINEAR with GL_REPEAT and no mipmaps.
/// </summary>
public sealed class TextureImage
{
    private readonly Vector4[] _texels;

    /// <param name="rgba">Size × Size × 4 bytes, the bottom row first.</param>
    public TextureImage(byte[] rgba, int size)
    {
        if (size <= 0 || rgba.Length != size * size * 4)
            throw new ArgumentException("The pixel data does not match the size.", nameof(rgba));
        Size = size;
        _texels = new Vector4[size * size];
        for (int i = 0; i < _texels.Length; i++)
        {
            _texels[i] = new Vector4(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]) / 255f;
            if (rgba[i * 4 + 3] < 255) HasTransparentTexels = true;
        }
    }

    public int Size { get; }

    /// <summary>Whether some texel has alpha below 1 (the white ones), so the texture can cut holes.</summary>
    public bool HasTransparentTexels { get; }

    /// <summary>The colour at texture coordinates (<paramref name="s"/>, <paramref name="t"/>).</summary>
    public Vector4 Sample(float s, float t)
    {
        if (!float.IsFinite(s) || !float.IsFinite(t)) return _texels[0];
        float x = s * Size - 0.5f, y = t * Size - 0.5f;
        float fx = MathF.Floor(x), fy = MathF.Floor(y);
        float ax = x - fx, ay = y - fy;
        int x0 = Wrap((long)fx), y0 = Wrap((long)fy);
        int x1 = x0 + 1 == Size ? 0 : x0 + 1, y1 = y0 + 1 == Size ? 0 : y0 + 1;
        var c00 = _texels[y0 * Size + x0];
        var c10 = _texels[y0 * Size + x1];
        var c01 = _texels[y1 * Size + x0];
        var c11 = _texels[y1 * Size + x1];
        return Vector4.Lerp(Vector4.Lerp(c00, c10, ax), Vector4.Lerp(c01, c11, ax), ay);
    }

    private int Wrap(long i)
    {
        long m = i % Size;
        return (int)(m < 0 ? m + Size : m);
    }
}
