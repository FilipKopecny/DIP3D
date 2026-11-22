using System.Numerics;
using Overbryce.Core.Model;

namespace Overbryce.App.Rendering;

/// <summary>How the current state is looked at (original View menu: Front / Top / Side / Camera).</summary>
internal enum ViewMode
{
    Camera,
    Front,
    Side,
    Top,
}

/// <summary>
/// The editor cameras of the original (CScena::predna/bocna/horna: 40 units from the origin on +Z,
/// +X and +Y). In the port they can additionally be orbited with the mouse and zoomed with the wheel.
/// </summary>
internal sealed class EditorCamera
{
    public const float DefaultDistance = 40f;
    public const float FieldOfView = 35f;

    /// <summary>Rotation about X in degrees (original alfa).</summary>
    public float Pitch { get; set; }

    /// <summary>Rotation about Y in degrees (original beta).</summary>
    public float Yaw { get; set; }

    public float Distance { get; set; } = DefaultDistance;

    public void Reset(ViewMode mode)
    {
        Distance = DefaultDistance;
        (Pitch, Yaw) = mode switch
        {
            ViewMode.Side => (0f, 90f),
            ViewMode.Top => (-90f, 0f),
            _ => (0f, 0f),
        };
    }

    public void Orbit(float dYaw, float dPitch)
    {
        Yaw = (Yaw + dYaw) % 360f;
        Pitch = Math.Clamp(Pitch + dPitch, -90f, 90f);
    }

    public void Zoom(float factor) => Distance = Math.Clamp(Distance * factor, 1f, 500f);

    /// <summary>The camera as an element (positioned on a sphere around the origin, looking at it).</summary>
    public CameraElement ToElement()
    {
        float a = Pitch * MathF.PI / 180f, b = Yaw * MathF.PI / 180f;
        return new CameraElement("EditorCamera")
        {
            Alpha = Pitch,
            Beta = Yaw,
            X = Distance * MathF.Cos(a) * MathF.Sin(b),
            Y = -Distance * MathF.Sin(a),
            Z = Distance * MathF.Cos(a) * MathF.Cos(b),
            FieldOfView = FieldOfView,
        };
    }
}

internal static class ViewSetup
{
    public const float Near = CameraElement.DefaultNear;
    public const float Far = CameraElement.DefaultFar;

    /// <summary>A camera's clipping distances (0.1 and 100 unless set, as in the original).</summary>
    public static (float Near, float Far) Clip(CameraElement cam)
    {
        float near = Math.Max(1e-4f, cam.Near);
        return (near, Math.Max(near * 1.001f, cam.Far));
    }

    /// <summary>
    /// Computes the view and projection matrices. In camera mode the state's active camera is used
    /// exactly like CKamera::RenderDirect; when there is none the front editor camera is used and
    /// <paramref name="warning"/> is set.
    /// </summary>
    public static (Matrix4x4 View, Matrix4x4 Projection) Compute(Scene? scene, ViewMode mode, EditorCamera editor,
        int width, int height, out string? warning)
    {
        warning = null;
        float aspect = Math.Max(1, width) / (float)Math.Max(1, height);

        if (mode == ViewMode.Camera)
        {
            var cam = scene?.ActiveCamera;
            if (cam != null)
            {
                var (near, far) = Clip(cam);
                return (Transforms.CameraView(cam), Transforms.Perspective(Math.Clamp(cam.FieldOfView, 1f, 179f), aspect, near, far));
            }
            warning = "No active camera in this state - showing the front view.";
            var front = new EditorCamera();
            front.Reset(ViewMode.Front);
            editor = front;
        }

        var e = editor.ToElement();
        return (Transforms.CameraView(e), Transforms.Perspective(EditorCamera.FieldOfView, aspect, Near, Math.Max(Far, editor.Distance * 4)));
    }
}
