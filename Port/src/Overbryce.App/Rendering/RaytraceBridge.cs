using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Overbryce.Core.Model;
using Overbryce.Raytrace;

namespace Overbryce.App.Rendering;

/// <summary>
/// Connects the editor to the ray tracer: the camera as the OpenGL views set it up, the frame
/// evaluated within the open shutter for motion blur, and the textures as the OpenGL views load them.
/// One instance keeps the shared geometry and the loaded textures between the frames of an export.
/// </summary>
internal sealed class RaytraceBridge(Func<string, string?> resolveTexture)
{
    private readonly Raytracer _tracer = new();
    private readonly Dictionary<string, TextureImage?> _textures = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Compiles one frame for tracing (on the thread that owns the animation).</summary>
    /// <param name="scene">The state to show (a key state or an evaluated frame).</param>
    /// <param name="time">The animation time of <paramref name="scene"/>, if it is a frame (enables motion blur).</param>
    public RaytraceJob Prepare(Animation animation, Scene? scene, float? time, ViewMode mode, EditorCamera editor,
        int width, int height, bool alphaBlend, bool classic, RaytraceSettings settings, uint seed)
    {
        scene ??= new Scene();
        var steps = new List<FrameStep>();
        if (time is float t && !classic && settings.Shutter > 0 && animation.Scenes.Count > 1)
        {
            float frame = 1f / Math.Clamp(animation.Fps, 1, 128);
            int n = Math.Clamp(settings.BlurSteps, 2, 16);
            for (int k = 0; k < n; k++)
            {
                float tk = t + (k / (float)(n - 1) - 0.5f) * settings.Shutter * frame;
                var s = animation.Evaluate(tk) ?? scene;
                steps.Add(new FrameStep(s, Camera(s, mode, editor)));
            }
        }
        else
        {
            steps.Add(new FrameStep(scene, Camera(scene, mode, editor)));
        }

        var options = new RaytraceOptions
        {
            Settings = settings,
            Modern = animation.Modern,
            Classic = classic,
            AlphaBlend = alphaBlend,
            Textures = Texture,
            Seed = seed,
        };
        return _tracer.Prepare(steps, width, height, options);
    }

    /// <summary>The camera of a view, exactly as <see cref="ViewSetup.Compute"/> sets it up for OpenGL.</summary>
    public static CameraSetup Camera(Scene? scene, ViewMode mode, EditorCamera editor)
    {
        if (mode == ViewMode.Camera)
        {
            if (scene?.ActiveCamera is { } cam)
                return new CameraSetup(Transforms.CameraView(cam), Math.Clamp(cam.FieldOfView, 1f, 179f), ViewSetup.Near, ViewSetup.Far,
                    cam.Aperture, cam.Focus);
            editor = new EditorCamera();
            editor.Reset(ViewMode.Front);
        }
        return new CameraSetup(Transforms.CameraView(editor.ToElement()), EditorCamera.FieldOfView, ViewSetup.Near,
            Math.Max(ViewSetup.Far, editor.Distance * 4));
    }

    private TextureImage? Texture(string name)
    {
        if (_textures.TryGetValue(name, out var cached)) return cached;
        TextureImage? image = null;
        try
        {
            if (resolveTexture(name) is { } path)
                image = new TextureImage(TextureCache.LoadPixels(path), TextureCache.Size);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or OutOfMemoryException or ExternalException)
        {
            image = null;
        }
        _textures[name] = image;
        return image;
    }

    /// <summary>A traced picture as a 24-bit bitmap (like the frames the OpenGL export writes).</summary>
    public static Bitmap ToBitmap(RaytraceImage image)
    {
        var bmp = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb);
        var data = bmp.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            var row = new byte[image.Width * 3];
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    int s = (y * image.Width + x) * 4;
                    row[x * 3] = image.Bgra[s];
                    row[x * 3 + 1] = image.Bgra[s + 1];
                    row[x * 3 + 2] = image.Bgra[s + 2];
                }
                Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, row.Length);
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }
        return bmp;
    }
}
