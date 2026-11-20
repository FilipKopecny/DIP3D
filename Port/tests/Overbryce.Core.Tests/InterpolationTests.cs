using System.Numerics;
using Overbryce.Core.Model;

namespace Overbryce.Core.Tests;

public class InterpolationTests
{
    private static Animation Linear(bool boundary, params (float t, float x)[] keys)
    {
        var a = new Animation();
        foreach (var (t, x) in keys)
        {
            var s = new Scene { AniTime = t };
            s.Add(new SphereElement("Ball") { X = x, MotionBoundary = boundary });
            a.Scenes.Add(s);
        }
        return a;
    }

    private static float BallX(Scene s) => s.FindChild("Ball")!.X;

    [Fact]
    public void Passes_through_key_states()
    {
        var a = Linear(false, (0, 0), (1, 4), (3, -2), (4, 7));
        Assert.Equal(0, BallX(a.Evaluate(0)!), 5);
        Assert.Equal(4, BallX(a.Evaluate(1)!), 5);
        Assert.Equal(-2, BallX(a.Evaluate(3)!), 5);
        Assert.Equal(7, BallX(a.Evaluate(4)!), 5);
        Assert.Equal(-2, BallX(a.EvaluateSegment(1, 0.99999f)), 3);
    }

    [Fact]
    public void Reproduces_uniform_motion_between_inner_keys()
    {
        var a = Linear(false, (0, 0), (1, 1), (2, 2), (3, 3));
        for (float t = 1; t <= 2; t += 0.125f)
            Assert.Equal(t, BallX(a.Evaluate(t)!), 4);
    }

    [Fact]
    public void Non_uniform_key_spacing_keeps_constant_velocity()
    {
        // x = 2t sampled at uneven times: the dt-ratio rescaling must keep the motion linear
        var a = Linear(false, (0, 0), (1, 2), (4, 8), (5, 10));
        for (float t = 1; t <= 4; t += 0.25f)
            Assert.Equal(2 * t, BallX(a.Evaluate(t)!), 3);
    }

    private static float Velocity(Animation a, float t, float eps = 1e-3f)
        => (BallX(a.Evaluate(t + eps)!) - BallX(a.Evaluate(t)!)) / eps;

    [Fact]
    public void Stop_flag_gives_zero_velocity_at_the_key_state()
    {
        // thesis: "if start / end of motion is defined for a state, the velocity there is zero"
        // (the original only halved it)
        var stopping = Linear(true, (0, 0), (1, 1), (2, 2), (3, 3));
        Assert.InRange(Velocity(stopping, 1), -0.01f, 0.01f);
        Assert.InRange(Velocity(stopping, 2 - 1e-3f), -0.01f, 0.01f);

        var smooth = Linear(false, (0, 0), (1, 1), (2, 2), (3, 3));
        Assert.InRange(Velocity(smooth, 1), 0.99f, 1.01f);
    }

    [Fact]
    public void Animation_starts_and_ends_at_rest()
    {
        var a = Linear(false, (0, 0), (1, 1), (2, 2));
        Assert.InRange(Velocity(a, 0), -0.01f, 0.01f);
        Assert.InRange(Velocity(a, 2 - 1e-3f), -0.01f, 0.01f);
        Assert.Equal(1, BallX(a.Evaluate(1)!), 4);
    }

    [Fact]
    public void Stop_flag_is_honoured_inside_groups()
    {
        var a = new Animation();
        for (int i = 0; i < 4; i++)
        {
            var s = new Scene { AniTime = i };
            var g = new GroupElement("G");
            g.Add(new SphereElement("Ball") { X = i, MotionBoundary = i == 1 });
            s.Add(g);
            a.Scenes.Add(s);
        }
        float X(float t) => ((GroupElement)a.Evaluate(t)!.FindChild("G")!).FindChild("Ball")!.X;
        Assert.InRange((X(1.001f) - X(1)) / 0.001f, -0.01f, 0.01f);
    }

    [Fact]
    public void Missing_counterpart_does_not_pull_back_to_the_first_state()
    {
        // the ball exists in states 0..2 but not in state 3: the last interval it is in must not
        // use its state-0 position as the 4th control point (original bug) - it just stops at x=2
        var a = Linear(false, (0, 0), (1, 1), (2, 2), (3, 3));
        a.Scenes[3].Remove(a.Scenes[3].FindChild("Ball")!);
        a.Scenes[3].Add(new BoxElement("Other"));
        float before = BallX(a.Evaluate(1.999f)!);
        Assert.InRange(before, 1.99f, 2.0f);
        Assert.InRange(Velocity(a, 1.5f), 0.9f, 1.6f);
    }

    [Fact]
    public void Permanent_rotation_is_continuous_across_key_states()
    {
        // Disky.ani style: 180 deg per 0.8 s over 5 s intervals = 1125 deg, i.e. not whole turns;
        // the original restarted the rotation at each key state and jumped by 45 deg
        var a = new Animation();
        for (int i = 0; i < 3; i++)
        {
            var s = new Scene { AniTime = i * 5 };
            s.Add(new DiskElement("Disk") { RotationPermanent = true, RotationAngleX = 180, RotationPeriod = 0.8f });
            a.Scenes.Add(s);
        }
        float before = a.Evaluate(5 - 1e-3f)!.FindChild("Disk")!.Alpha;
        float after = a.Evaluate(5 + 1e-3f)!.FindChild("Disk")!.Alpha;
        Assert.InRange(after - before, 0f, 1f);
        Assert.Equal(180f * 7.5f / 0.8f, a.Evaluate(7.5f)!.FindChild("Disk")!.Alpha, 1);
    }

    [Fact]
    public void Last_frame_keeps_the_accumulated_permanent_motion()
    {
        var a = new Animation();
        for (int i = 0; i < 3; i++)
        {
            var s = new Scene { AniTime = i * 4 };
            var g = new GroupElement("G") { RotationPermanent = true, RotationAngleY = 30, RotationPeriod = 1 };
            g.Add(new SphereElement("Moon") { X = 5, OrbitPermanent = true, OrbitAngleY = 10, OrbitPeriod = 1 });
            s.Add(g);
            a.Scenes.Add(s);
        }
        var nearEnd = (GroupElement)a.Evaluate(8 - 1e-3f)!.FindChild("G")!;
        var end = (GroupElement)a.Evaluate(8)!.FindChild("G")!;
        Assert.Equal(240f, end.Beta, 1);
        Assert.InRange(end.Beta - nearEnd.Beta, 0f, 0.1f);
        var moon = end.FindChild("Moon")!;
        Assert.Equal(80f, moon.Beta, 1);
        Assert.Equal(5 * MathF.Cos(80 * MathF.PI / 180), moon.X, 3);
        Assert.Equal(nearEnd.FindChild("Moon")!.X, moon.X, 2);
    }

    [Fact]
    public void Permanent_orbit_rotates_about_origin()
    {
        var a = new Animation();
        for (int i = 0; i < 2; i++)
        {
            var s = new Scene { AniTime = i * 10 };
            s.Add(new SphereElement("Planet")
            {
                X = 5, OrbitPermanent = true, OrbitAngleY = 90, OrbitPeriod = 10, MotionBoundary = false,
            });
            a.Scenes.Add(s);
        }
        var mid = a.Evaluate(5)!.FindChild("Planet")!;
        // 5 s of a 90deg/10 s orbit = 45 deg about +Y: (5,0,0) -> (5cos45, 0, -5sin45)
        Assert.Equal(5 * MathF.Cos(MathF.PI / 4), mid.X, 3);
        Assert.Equal(-5 * MathF.Sin(MathF.PI / 4), mid.Z, 3);
        Assert.Equal(45, mid.Beta, 3);
    }

    [Fact]
    public void Group_children_are_matched_by_name()
    {
        var a = new Animation();
        for (int i = 0; i < 2; i++)
        {
            var s = new Scene { AniTime = i };
            var g = new GroupElement("G");
            g.Add(new BoxElement("B") { SizeX = 1 + i * 2 });
            s.Add(g);
            a.Scenes.Add(s);
        }
        var box = (BoxElement)((GroupElement)a.Evaluate(0.5f)!.FindChild("G")!).FindChild("B")!;
        Assert.Equal(2, box.SizeX, 3);
        // the key states themselves are untouched
        Assert.Equal(1, ((BoxElement)((GroupElement)a.Scenes[0].FindChild("G")!).FindChild("B")!).SizeX);
    }

    [Fact]
    public void Standalone_camera_view_matches_the_original()
    {
        // CKamera::RenderDirect: glRotate(-gama,Z); glRotate(-alfa,X); glRotate(-beta,Y); glTranslate(-p)
        var scene = new Scene();
        var cam = new CameraElement { X = 3, Y = -2, Z = 40, Alpha = -12, Beta = 30, Gamma = 7 };
        scene.Add(cam);
        const float d = MathF.PI / 180;
        var legacy = Matrix4x4.CreateTranslation(-3, 2, -40)
                     * Matrix4x4.CreateRotationY(-30 * d)
                     * Matrix4x4.CreateRotationX(12 * d)
                     * Matrix4x4.CreateRotationZ(-7 * d);
        Assert.Equal(Round(legacy), Round(Transforms.CameraView(cam)));
    }

    [Fact]
    public void Camera_in_moved_and_rotated_group_looks_along_its_world_axes()
    {
        var scene = new Scene();
        var group = new GroupElement("Head") { X = -16, Y = -1, Z = 17, Beta = 40, Alpha = 10 };
        var cam = new CameraElement { X = 1, Y = 2, Z = 3, Alpha = -12, Beta = 30, Gamma = 7 };
        group.Add(cam);
        scene.Add(group);
        var id = Transforms.World(cam) * Transforms.CameraView(cam);
        Assert.Equal(Matrix4x4.Identity, Round(id));
        // the camera sits where the group puts it
        var eye = Vector3.Transform(Vector3.Zero, Transforms.World(cam));
        var expected = Vector3.Transform(new Vector3(1, 2, 3), Transforms.GroupLocal(group));
        Assert.True(Vector3.Distance(eye, expected) < 1e-4f);
    }

    private static Matrix4x4 Round(Matrix4x4 m)
    {
        float r(float f) => MathF.Round(f, 4) + 0f;
        return new Matrix4x4(r(m.M11), r(m.M12), r(m.M13), r(m.M14), r(m.M21), r(m.M22), r(m.M23), r(m.M24),
            r(m.M31), r(m.M32), r(m.M33), r(m.M34), r(m.M41), r(m.M42), r(m.M43), r(m.M44));
    }
}
