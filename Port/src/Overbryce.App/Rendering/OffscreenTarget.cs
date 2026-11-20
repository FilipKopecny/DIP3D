using System.Drawing;
using System.Drawing.Imaging;
using OpenTK.Graphics.OpenGL4;

namespace Overbryce.App.Rendering;

/// <summary>
/// Multisampled framebuffer used to render animation frames at a fixed resolution for export
/// (the original copied the window contents with a VCL TImage and saved BMP files).
/// </summary>
internal sealed class OffscreenTarget : IDisposable
{
    private readonly int _msFbo, _msColor, _msDepth;
    private readonly int _fbo, _color;

    public int Width { get; }
    public int Height { get; }

    public OffscreenTarget(int width, int height, int samples)
    {
        Width = width;
        Height = height;
        GL.GetInteger(GetPName.MaxSamples, out int maxSamples);
        samples = Math.Clamp(samples, 0, maxSamples);

        _msFbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _msFbo);
        _msColor = GL.GenRenderbuffer();
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _msColor);
        GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, samples, RenderbufferStorage.Rgba8, width, height);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, _msColor);
        _msDepth = GL.GenRenderbuffer();
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _msDepth);
        GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, samples, RenderbufferStorage.DepthComponent24, width, height);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _msDepth);
        Check();

        _fbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        _color = GL.GenRenderbuffer();
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _color);
        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Rgba8, width, height);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, _color);
        Check();

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private static void Check()
    {
        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
            throw new InvalidOperationException("Framebuffer incomplete: " + status);
    }

    public void Bind() => GL.BindFramebuffer(FramebufferTarget.Framebuffer, _msFbo);

    /// <summary>Resolves the multisampled image and reads it back as a 24-bit bitmap.</summary>
    public Bitmap ReadBitmap()
    {
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _msFbo);
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _fbo);
        GL.BlitFramebuffer(0, 0, Width, Height, 0, 0, Width, Height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _fbo);

        var pixels = new byte[Width * Height * 4];
        GL.PixelStore(PixelStoreParameter.PackAlignment, 1);
        GL.ReadPixels(0, 0, Width, Height, OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, pixels);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        var bmp = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        var data = bmp.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        try
        {
            var row = new byte[data.Stride];
            for (int y = 0; y < Height; y++)
            {
                int src = (Height - 1 - y) * Width * 4; // GL origin is bottom-left
                for (int x = 0; x < Width; x++)
                {
                    row[x * 3] = pixels[src + x * 4];
                    row[x * 3 + 1] = pixels[src + x * 4 + 1];
                    row[x * 3 + 2] = pixels[src + x * 4 + 2];
                }
                System.Runtime.InteropServices.Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, data.Stride);
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }
        return bmp;
    }

    public void Dispose()
    {
        GL.DeleteFramebuffer(_msFbo);
        GL.DeleteFramebuffer(_fbo);
        GL.DeleteRenderbuffer(_msColor);
        GL.DeleteRenderbuffer(_msDepth);
        GL.DeleteRenderbuffer(_color);
    }
}
