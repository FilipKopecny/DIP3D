using System.Globalization;
using System.Numerics;
using Overbryce.Core.Model;
using static Lumina.Kit;

namespace Lumina;

/// <summary>
/// The opening (0 .. 16 s): the whole planetary system seen from afar. The giant circles the sun, and
/// Lumina and a small moonlet circle the giant. Then a lander comes in from deep space, shoots past the
/// camera and heads for Lumina. Everything here is a miniature at about 1/8 of the story's scale: the
/// story's moon has radius 10, here it has 1.3.
/// <para>
/// The orbits are permanent rotations of nested groups. A group translates and then rotates about its
/// parent's origin, so a translated group that keeps rotating carries its children around a circle.
/// Its first child turns back by the same angle, so the giant's tilted axis and rings keep their
/// direction in space.
/// </para>
/// </summary>
internal sealed class Intro
{
    public const float Duration = 16f;

    /// <summary>0.5 s apart, 0.25 s around the fly-by, plus one key a frame before the cut to the story.</summary>
    public static IEnumerable<float> KeyTimes()
    {
        for (int i = 0; i < 18; i++) yield return i * 0.5f;          // 0 .. 8.5
        for (int i = 0; i < 12; i++) yield return 9 + i * 0.25f;     // 9 .. 11.75: the fly-by and the whip pan
        for (int i = 0; i < 8; i++) yield return 12 + i * 0.5f;      // 12 .. 15.5
        yield return Duration - 0.04f;                                // no frozen interval before the cut
    }

    // ------------------------------------------------------------------ the miniature system (sun at the origin)

    private const float SunR = 4f;
    private const float GiantOrbit = 30, GiantPeriod = 60, Giant0 = -80;   // degrees at t = 0
    private const float GiantR = 4.4f;
    private const float MoonOrbit = 12, MoonPeriod = 8, Moon0 = 200, MoonR = 1.3f;
    private const float MoonletOrbit = 9, MoonletPeriod = 5, Moonlet0 = 40;
    private const float InnerOrbit = 15, InnerPeriod = 22, Inner0 = 150;
    private const float TiltA = 14, TiltG = -24;

    /// <summary>The lander's scale here: Lumina is 1.3 instead of 10 units across.</summary>
    private const float LanderScale = MoonR / 10f;

    private static readonly Vector3 C0 = new(-20, 19, 44), C1 = new(-15, 13.5f, 35);

    private static Vector3 Cam(float t) => Lerp(C0, C1, Smooth(0, Duration, t));

    private static float GiantAngle(float t) => Giant0 + 360 * t / GiantPeriod;

    /// <summary>World positions of the giant and Lumina at time t, from the same hierarchy the scene uses.</summary>
    private static (Vector3 Giant, Vector3 Moon) Bodies(float t)
    {
        var orbit = new GroupElement("p1") { X = GiantOrbit, Beta = GiantAngle(t) };
        var keep = new GroupElement("p2") { Beta = -GiantAngle(t) };
        var tilt = new GroupElement("p3") { Alpha = TiltA, Gamma = TiltG };
        var moon = new GroupElement("p4") { X = MoonOrbit, Beta = Moon0 + 360 * t / MoonPeriod };
        orbit.Add(keep); keep.Add(tilt); tilt.Add(moon);
        return (World(orbit), World(moon));
    }

    // ------------------------------------------------------------------ the lander's flight

    private const float Appear = 6f, Pass = 10.2f;

    private static readonly Vector3 Up = Vector3.UnitY;

    /// <summary>Where the lander comes from: to the right of and behind the camera's first view.</summary>
    private static readonly Vector3 DeepDir = DeepSpace();

    private static Vector3 DeepSpace()
    {
        var f = Vector3.Normalize(-C0);
        var fh = Vector3.Normalize(f with { Y = 0 });
        var right = new Vector3(-fh.Z, 0, fh.X);
        const float a = 115 * Deg;
        return Vector3.Normalize(right * MathF.Sin(a) + fh * MathF.Cos(a) + Up * 0.12f);
    }

    private static readonly Vector3 P0 = Cam(Appear) + DeepDir * 45;
    private static readonly Vector3 PPass = Cam(Pass) + Up * 0.42f + Vector3.Normalize(Vector3.Cross(DeepDir, Up)) * 0.3f;
    private static readonly Vector3 PEnd = EndPoint();
    private static readonly Vector3 VA = (PPass - P0) / (Pass - Appear);
    private static readonly Vector3 VPass = Vector3.Normalize(Vector3.Normalize(PPass - P0) + Vector3.Normalize(PEnd - PPass)) * 8.5f;
    private static readonly Vector3 VEnd = (PEnd - PPass) / (Duration - Pass) * 0.45f;

    private static Vector3 EndPoint()
    {
        var moon = Bodies(Duration).Moon;
        return moon + Vector3.Normalize(Cam(Duration) - moon) * (MoonR + 1.6f) + Up * 0.4f;
    }

    private static (Vector3 P, Vector3 V) Hermite(Vector3 p0, Vector3 v0, Vector3 p1, Vector3 v1, float t0, float t1, float t)
    {
        float dt = t1 - t0, u = (t - t0) / dt, u2 = u * u, u3 = u2 * u;
        var p = (2 * u3 - 3 * u2 + 1) * p0 + (u3 - 2 * u2 + u) * dt * v0 + (-2 * u3 + 3 * u2) * p1 + (u3 - u2) * dt * v1;
        var v = ((6 * u2 - 6 * u) * p0 + (3 * u2 - 4 * u + 1) * dt * v0 + (-6 * u2 + 6 * u) * p1 + (3 * u2 - 2 * u) * dt * v1) / dt;
        return (p, v);
    }

    /// <summary>Lander position and velocity: straight in from deep space, past the camera, on to Lumina.</summary>
    public static (Vector3 P, Vector3 V) LanderAt(float t)
    {
        if (t <= Appear) return (P0 + VA * (t - Appear), VA);
        if (t <= Pass) return Hermite(P0, VA, PPass, VPass, Appear, Pass, t);
        return Hermite(PPass, VPass, PEnd, VEnd, Pass, Duration, MathF.Min(t, Duration));
    }

    // ------------------------------------------------------------------ building a key state

    private readonly float t;
    private readonly Scene scene;

    private Intro(float t)
    {
        this.t = t;
        scene = new Scene("Uvod" + t.ToString("0.0#", CultureInfo.InvariantCulture)) { AniTime = t };
    }

    public static Scene Build(float t)
    {
        var intro = new Intro(t);
        intro.Compose();
        return intro.scene;
    }

    private void Compose()
    {
        var cam = Cam(t);
        Sky(cam);
        System();
        SunHalo(cam);
        Lander();
        Camera(cam);
    }

    private void Sky(Vector3 cam)
    {
        // the backdrop travels with the camera, so the system can be up to 95 units away (the far plane is at 100)
        var sky = Glow(new SphereElement("VObloha") { Radius = 96, QualityX = 48, QualityY = 24, Alpha = -90, Beta = 140 }, Gray(1f), tex: "Hmlovina.bmp");
        Place(sky, cam);
        scene.Add(sky);

        var stars = new GroupElement("VHviezdy");
        Place(stars, cam);
        for (int i = 0; i < 46; i++)
        {
            float z = Rnd(i, 211) * 2 - 1, phi = Rnd(i, 212) * MathF.PI * 2, rr = MathF.Sqrt(1 - z * z);
            var d = new Vector3(rr * MathF.Cos(phi), z, rr * MathF.Sin(phi));
            float size = 0.16f + 0.3f * MathF.Pow(Rnd(i, 213), 3);
            float twinkle = 0.7f + 0.28f * Wave(t, 1.1f + 2 * Rnd(i, 214), Rnd(i, 215));
            var tint = Rnd(i, 216) switch { < 0.3f => C(0.75f, 0.85f, 1f), < 0.5f => C(1f, 0.88f, 0.7f), _ => C(1f, 1f, 1f) };
            var star = Glow(new SphereElement($"VHviezda{i}") { Radius = size, QualityX = 6, QualityY = 4 }, Mul(tint, twinkle));
            Place(star, d * 90);
            stars.Add(star);
        }
        scene.Add(stars);
    }

    private void System()
    {
        // the sun and its light
        scene.Add(Glow(new SphereElement("VSlnko") { Radius = SunR, QualityX = 32, QualityY = 20 }, C(1f, 0.96f, 0.84f)));
        scene.Add(Light("VSvetloSlnko", Vector3.Zero, C(1f, 0.94f, 0.85f), ambient: C(0.05f, 0.05f, 0.08f)));

        // an inner rocky planet
        var inner = new GroupElement("VPlanetkaDraha")
        {
            X = InnerOrbit, Beta = Inner0, RotationPermanent = true, RotationAngleY = 360, RotationPeriod = InnerPeriod,
        };
        inner.Add(Mat(new SphereElement("VPlanetka") { Radius = 1.0, QualityX = 20, QualityY = 14, Alpha = -90 },
            C(1f, 0.85f, 0.7f), "Pieskovec.bmp", ambient: 0.25f, spec: 0.1f));
        scene.Add(inner);

        // the giant: orbit around the sun -> keep the axis fixed -> axial tilt
        var orbit = new GroupElement("VObrDraha")
        {
            X = GiantOrbit, Beta = Giant0, RotationPermanent = true, RotationAngleY = 360, RotationPeriod = GiantPeriod,
        };
        var keep = new GroupElement("VObrOs") { Beta = -Giant0, RotationPermanent = true, RotationAngleY = -360, RotationPeriod = GiantPeriod };
        var tilt = new GroupElement("VObrNaklon") { Alpha = TiltA, Gamma = TiltG };
        tilt.Add(Mat(new SphereElement("VObor")
        {
            Radius = GiantR, QualityX = 40, QualityY = 28, Alpha = -90,
            RotationPermanent = true, RotationAngleZ = 360, RotationPeriod = 20,
        }, Gray(0.95f), "Obor.bmp", ambient: 0.25f, emission: C(0.03f, 0.025f, 0.02f), spec: 0.08f, shine: 0.1f));
        tilt.Add(Mat(new DiskElement("VPrstence") { InnerRadius = GiantR * 13.5 / 11, OuterRadius = GiantR * 19 / 11, SweepAngle = 360, QualityX = 72, QualityY = 3, Alpha = -90 },
            C(0.95f, 0.9f, 0.85f), "Prstence.bmp", ambient: 0.45f, emission: C(0.05f, 0.045f, 0.04f), spec: 0.05f));

        // Lumina and the moonlet orbit the giant in the plane of its rings
        var moonOrbit = new GroupElement("VMesiacDraha")
        {
            X = MoonOrbit, Beta = Moon0, RotationPermanent = true, RotationAngleY = 360, RotationPeriod = MoonPeriod,
        };
        moonOrbit.Add(Mat(new SphereElement("VMesiac")
        {
            Radius = MoonR, QualityX = 28, QualityY = 18, Alpha = -90,
            RotationPermanent = true, RotationAngleZ = 360, RotationPeriod = 12,
        }, C(0.95f, 1f, 0.98f), "Povrch.bmp", ambient: 0.3f, emission: C(0.06f, 0.1f, 0.1f), spec: 0.1f, shine: 0.1f));
        tilt.Add(moonOrbit);

        var moonletOrbit = new GroupElement("VDrobecDraha")
        {
            X = MoonletOrbit, Beta = Moonlet0, RotationPermanent = true, RotationAngleY = 360, RotationPeriod = MoonletPeriod,
        };
        moonletOrbit.Add(Mat(new SphereElement("VDrobec") { Radius = 0.55, QualityX = 16, QualityY = 12 }, C(0.9f, 0.88f, 0.85f), "SivyKamen.bmp", ambient: 0.25f));
        tilt.Add(moonletOrbit);

        keep.Add(tilt);
        orbit.Add(keep);
        scene.Add(orbit);
    }

    private void SunHalo(Vector3 cam)
    {
        var toCam = Vector3.Normalize(cam);
        // six stacked translucent disks: a soft corona, brightest next to the sun
        for (int i = 0; i < 6; i++)
        {
            float r = SunR * (1.15f + 0.5f * i + 0.06f * i * i);
            var halo = Glow(new DiskElement($"VZiara{i}") { InnerRadius = 0, OuterRadius = r, SweepAngle = 360, QualityX = 40, QualityY = 1 },
                C(1f, 0.8f - 0.04f * i, 0.5f - 0.04f * i), 0.22f * MathF.Pow(0.68f, i));
            Place(halo, -toCam * (2.5f + i * 0.3f)); // behind the sun as seen from the camera
            PointZ(halo, toCam);
            scene.Add(halo);
        }
    }

    private void Lander()
    {
        var (p, v) = LanderAt(t);
        var d = Vector3.Normalize(v);

        // position -> heading -> pitch (the lander flies cabin first, +Y along the velocity) -> slow roll
        var pos = new GroupElement("VSonda");
        Place(pos, p);
        var heading = new GroupElement("VSondaSmer") { Beta = Unwrap("sonda", MathF.Atan2(d.X, d.Z) / Deg) };
        var pitch = new GroupElement("VSondaNaklon") { Alpha = MathF.Acos(Math.Clamp(d.Y, -1, 1)) / Deg };
        var roll = new GroupElement("VSondaRot") { RotationPermanent = true, RotationAngleY = 360, RotationPeriod = 9 };

        // the story's lander, folded for the flight, centred and shrunk
        var body = new GroupElement("VSondaTelo");
        Story.LanderBody(body, 0, lights: false);
        float flicker = Rnd((int)MathF.Round(t * 4), 9) - 0.5f;
        body.Add(Glow(new ConeElement("VPlamen") { BaseRadius = 0.36, TopRadius = 0.03, Height = 1.9f + 0.4f * flicker, QualityX = 14, QualityY = 3, Alpha = 90, Y = 0.25f },
            C(1f, 0.62f, 0.3f), 0.7f, "Plamen.bmp"));
        body.Add(Glow(new ConeElement("VPlamenJadro") { BaseRadius = 0.2, TopRadius = 0.01, Height = 1.0f + 0.2f * flicker, QualityX = 10, QualityY = 2, Alpha = 90, Y = 0.25f },
            C(1f, 0.94f, 0.75f), 0.85f, "Plamen.bmp"));
        Scale(body, LanderScale);
        body.Y = -1.6f * LanderScale;

        // a beacon on the cabin roof, big enough to be seen as a point from far away
        float blink = MathF.Max(0, Wave(t, 1f, 0.25f));
        var beacon = Glow(new SphereElement("VMajak") { Radius = 0.035, QualityX = 10, QualityY = 8 }, Mul(C(1f, 0.35f, 0.25f), 0.45f + 0.55f * blink));
        Place(beacon, new Vector3(0, (3.2f - 1.6f) * LanderScale, 0));

        // the engine's glow: a soft spark that stays visible when the lander is only a few pixels big
        var spark = new GroupElement("VIskra");
        Place(spark, new Vector3(0, -1.9f * LanderScale, 0));
        float[] sparkR = [0.07f, 0.14f, 0.26f], sparkA = [0.85f, 0.4f, 0.16f];
        float far = Math.Clamp(Vector3.Distance(p, Cam(t)) / 25f, 0.12f, 1f); // small when it is close to the camera
        for (int i = 0; i < sparkR.Length; i++)
            spark.Add(Glow(new SphereElement($"VIskra{i}") { Radius = sparkR[i] * far * (1 + 0.15f * flicker), QualityX = 12, QualityY = 8 }, C(1f, 0.7f - 0.12f * i, 0.35f), sparkA[i]));

        roll.Add(body);
        roll.Add(beacon);
        roll.Add(spark);
        pitch.Add(roll);
        heading.Add(pitch);
        pos.Add(heading);
        scene.Add(pos);
    }

    private void Camera(Vector3 cam)
    {
        var giant = Bodies(t).Giant;
        Vector3 Dir(Vector3 target) => Vector3.Normalize(target - cam);
        Vector3 LanderDir(float time) => Vector3.Normalize(LanderAt(time).P - Cam(time));

        // look at the system, turn to the lander coming in, hold while it shoots past, follow it to Lumina
        var system = Dir(giant * 0.4f);
        Vector3 dir;
        if (t < 7.2f)
            dir = DirLerp(system, LanderDir(t), Smooth(5.4f, 7.2f, t));
        else if (t < 9.75f)
            dir = LanderDir(t);
        else
        {
            // a quick whip pan right after the pass, catching the lander while it is still big. It swings
            // the way the lander went (judged from where it is a second later), not the other way round.
            var hold = LanderDir(9.75f);
            var follow = DirLerp(LanderDir(t), Dir(Bodies(t).Moon), 0.3f * Smooth(12.4f, 15f, t));
            float towards = MathF.Atan2(LanderDir(11.2f).X, LanderDir(11.2f).Z);
            dir = DirLerp(hold, follow, Smooth(10.25f, 11f, t), towards);
        }

        var camera = new CameraElement("VKamera") { FieldOfView = 50 };
        Place(camera, cam);
        Aim(camera, cam + dir);
        scene.Add(camera);
        scene.SetActiveCamera(camera);
    }

    /// <summary>
    /// Interpolates a direction by heading and elevation, turning the short way round, or, when
    /// <paramref name="via"/> is given, the way that passes closest to that heading.
    /// </summary>
    private static Vector3 DirLerp(Vector3 a, Vector3 b, float w, float? via = null)
    {
        float ya = MathF.Atan2(a.X, a.Z), yb = MathF.Atan2(b.X, b.Z);
        float reference = via is { } v ? Wrap(v - ya) : 0;
        float dy = Wrap(yb - ya - reference) + reference;
        float yaw = ya + dy * w;
        float pitch = Lerp(MathF.Asin(Math.Clamp(a.Y, -1, 1)), MathF.Asin(Math.Clamp(b.Y, -1, 1)), w);
        return new Vector3(MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Cos(yaw) * MathF.Cos(pitch));
    }

    /// <summary>An angle in radians brought into (-π, π].</summary>
    private static float Wrap(float a)
    {
        while (a > MathF.PI) a -= 2 * MathF.PI;
        while (a <= -MathF.PI) a += 2 * MathF.PI;
        return a;
    }
}
