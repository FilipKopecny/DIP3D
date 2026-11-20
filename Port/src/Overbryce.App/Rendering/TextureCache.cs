using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace Overbryce.App.Rendering;

/// <summary>
/// Loads element textures the way CPrvok::LoadTexture did: the bitmap is stretched to 128x128,
/// stored bottom row first, and every pure white texel gets alpha 0 (transparent when alpha
/// blending is enabled). GL textures are cached per resolved file path.
/// </summary>
internal sealed class TextureCache : IDisposable
{
    public const int Size = 128;

    private readonly Dictionary<string, int> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _missing = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Texture names that could not be resolved or decoded (for the status bar).</summary>
    public IReadOnlyCollection<string> Missing => _missing;

    /// <summary>Returns the GL texture for <paramref name="path"/> or 0 if it cannot be loaded.</summary>
    public int Get(string? path)
    {
        if (string.IsNullOrEmpty(path)) return 0;
        if (_textures.TryGetValue(path, out int id)) return id;
        if (_missing.Contains(path)) return 0;

        try
        {
            byte[] rgba = LoadPixels(path);
            id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, id);
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, Size, Size, 0,
                OpenTK.Graphics.OpenGL4.PixelFormat.Rgba, PixelType.UnsignedByte, rgba);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            _textures[path] = id;
            return id;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or OutOfMemoryException or ExternalException)
        {
            _missing.Add(path);
            return 0;
        }
    }

    private static byte[] LoadPixels(string path)
    {
        using var source = new Bitmap(path);
        using var scaled = new Bitmap(Size, Size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(scaled))
        {
            // nearest neighbour like LoadImage(LR_CREATEDIBSECTION) stretching; keeps pure white exact
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(source, new Rectangle(0, 0, Size, Size));
        }

        var data = scaled.LockBits(new Rectangle(0, 0, Size, Size), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var bgra = new byte[Size * Size * 4];
            for (int row = 0; row < Size; row++)
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + row * data.Stride, bgra, row * Size * 4, Size * 4);

            var rgba = new byte[bgra.Length];
            for (int row = 0; row < Size; row++)
            {
                int src = (Size - 1 - row) * Size * 4; // GL row 0 = bottom of the bitmap
                int dst = row * Size * 4;
                for (int x = 0; x < Size; x++, src += 4, dst += 4)
                {
                    byte b = bgra[src], gC = bgra[src + 1], r = bgra[src + 2];
                    rgba[dst] = r;
                    rgba[dst + 1] = gC;
                    rgba[dst + 2] = b;
                    rgba[dst + 3] = r == 255 && gC == 255 && b == 255 ? (byte)0 : (byte)255;
                }
            }
            return rgba;
        }
        finally
        {
            scaled.UnlockBits(data);
        }
    }

    /// <summary>Forget everything (e.g. after loading another animation).</summary>
    public void Clear()
    {
        foreach (int id in _textures.Values) GL.DeleteTexture(id);
        _textures.Clear();
        _missing.Clear();
    }

    public void Dispose() => Clear();
}
