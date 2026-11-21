using System.Numerics;
using Overbryce.Core;
using Overbryce.Core.Model;
using static Lumina.Kit;

namespace Lumina;

/// <summary>
/// The extension of the story: as the crane-out begins, the creature sets off towards the ocean and the
/// astronaut follows it, with a flock of mantas above them. At the shore the creature leaps into the sea
/// and calls him; he wades in and dives (the underwater part is <see cref="Depths"/>).
/// </summary>
internal sealed partial class Story
{
    /// <summary>Story time when the pair sets off: the crane-out has just begun.</summary>
    private const float JourneyStart = 43.5f;

    /// <summary>The last key state on land; the underwater world follows.</summary>
    public const float LandEnd = 70.96f;

    private const float CreatureGo = 43.9f, CreatureStop = 64.4f;
    private const float AstroGo = 44.5f, AstroStop = 65.1f;
    private const float Jump = 65.4f;                 // the creature leaps into the sea
    private const float Wade = 68.2f, Dive = 70.1f;    // the astronaut wades in, then dives

    private static SpherePath CreaturePath => Planet.RoutePath;

    private static SpherePath? _astroPath;

    /// <summary>The astronaut's way: round the heart crystal to where the creature stood, then the creature's route.</summary>
    private static SpherePath AstroPath => _astroPath ??= new SpherePath(
    [
        SurfaceNormal(AstroPose(JourneyStart).A, AstroPose(JourneyStart).S),
        SurfaceNormal(GroveA - 0.4f, 5.2f),
        .. Planet.RoutePoints,
    ]);

    /// <summary>How far the astronaut walks before he is where the creature started.</summary>
    private static float AstroLead => AstroPath.DistanceAlong(Planet.RouteStart);

    /// <summary>Distance covered between <paramref name="t0"/> and <paramref name="t1"/>: speeding up and slowing down over <paramref name="ramp"/> seconds.</summary>
    private static float Travel(float t, float t0, float t1, float distance, float ramp)
    {
        float total = t1 - t0, v = distance / (total - ramp), u = Math.Clamp(t - t0, 0, total);
        if (u < ramp) return v * u * u / (2 * ramp);
        if (u > total - ramp) return distance - v * Sq(total - u) / (2 * ramp);
        return v * (u - ramp / 2);
    }

    private static float CreatureDistance(float t)
        => Travel(t, CreatureGo, CreatureStop, Planet.BeachDistance + 0.25f, 1.2f)
           + 0.95f * Clamp01((t - Jump) / 0.8f)
           + 2.2f * Smooth(68.2f, 69.6f, t);   // off into the sea, so it is gone when he dives

    private static float AstroDistance(float t)
        => Travel(t, AstroGo, AstroStop, AstroLead + Planet.BeachDistance - 0.6f, 1.2f)
           + (Planet.WadeDistance - Planet.BeachDistance + 0.8f) * Smooth(Wade, Dive + 0.3f, t)
           + 0.7f * Smooth(Dive, LandEnd + 0.4f, t);

    private static float WrapDeg(float d) => d - 360 * MathF.Round(d / 360);

    private static float BlendAngle(float from, float to, float k) => from + WrapDeg(to - from) * k;

    // ================================================================== the astronaut

    private static void Journey(Pose p, float t)
    {
        var (n, dir) = AstroPath.At(AstroDistance(t));
        (p.A, p.S) = Planet.Angles(n);
        p.H += Planet.SurfaceRadius(n) - Ground;
        p.Heading = Unwrap("astroYaw", BlendAngle(p.Heading, Planet.Heading(n, dir), Smooth(JourneyStart, 44.6f, t)));

        // a long, springy low-gravity stride (one step a second, sampled at its extremes by the key states)
        float phi = 2 * MathF.PI * (t - JourneyStart) / 2f;
        float walk = Window(t, AstroGo - 0.2f, AstroGo + 0.6f, AstroStop - 0.7f, AstroStop)
                     + 0.8f * Window(t, Wade - 0.1f, Wade + 0.4f, Dive - 0.2f, Dive + 0.2f);
        Gait(p, phi, walk, 24);
        p.H += 0.09f * MathF.Abs(MathF.Sin(phi)) * walk;
        p.SwingL += 8 * MathF.Sin(phi) * walk; p.SwingR -= 8 * MathF.Sin(phi) * walk;
        p.Lean += 6 * walk;

        // first person: through the fungi, looking at the creature ahead and around
        float fp1 = Window(t, 48.2f, 48.6f, 51.3f, 51.8f);
        p.HeadPitch += 7 * fp1;
        p.HeadYaw += Lerp(-20, 18, Smooth(48.5f, 51.5f, t)) * fp1;
        // seen over his shoulder: he looks up as the mantas sweep over him
        float up = Window(t, 56.1f, 56.6f, 57.4f, 58f);
        p.HeadPitch -= 24 * up;
        p.HeadYaw -= 10 * up;

        // at the shore: he watches the leap, then waves back at the creature
        p.HeadPitch += 16 * Window(t, 65.2f, 65.8f, 66.6f, 67.2f);
        float wave = Window(t, 66.8f, 67.2f, 67.9f, 68.3f);
        p.AbdL += 128 * wave;
        p.WaveL += 28 * Wave(t, 1f, 0.25f) * Window(t, 67.1f, 67.3f, 67.7f, 67.9f);

        // wading: arms out for balance, eyes on the water
        float wading = Window(t, Wade, Wade + 0.6f, Dive - 0.1f, Dive + 0.3f);
        p.AbdL += 28 * wading; p.AbdR -= 28 * wading;
        p.WaveL -= 15 * wading; p.WaveR += 15 * wading;   // hands skimming the water
        p.HeadPitch += 14 * Window(t, Wade, Wade + 0.6f, Dive, Dive + 0.4f);

        // the dive: he tips forward, arms stretched over his head, and looks ahead under the water
        float dive = Smooth(Dive, Dive + 0.8f, t);
        p.Pitch += 78 * dive;
        p.H += 0.15f * dive;
        p.SwingL -= 150 * dive; p.SwingR -= 150 * dive;
        p.AbdL -= 20 * dive; p.AbdR += 20 * dive;
        p.HeadPitch -= 55 * dive;
    }

    // ================================================================== the creature

    private (float A, float S, float Lift, float Heading, float Hop, float Pitch) CreatureJourney(float face)
    {
        var (n, dir) = CreaturePath.At(CreatureDistance(t));
        n = CreatureAt(t);
        var (a, s) = Planet.Angles(n);
        float ground = Planet.SurfaceRadius(n) - Ground;
        float yaw = BlendAngle(face, Planet.Heading(n, dir), Smooth(JourneyStart, CreatureGo + 0.3f, t));
        yaw += 180 * (Smooth(66.3f, 66.9f, t) - Smooth(68.15f, 68.65f, t));   // turns back to the shore to call him
        // and swims off where it goes: out to sea and aside, out of his way
        if (t > 68.2f)
        {
            var v = CreatureAt(t + 0.1f) - CreatureAt(t - 0.1f);
            yaw = BlendAngle(yaw, Planet.Heading(n, v - n * Vector3.Dot(n, v)), Smooth(68.2f, 68.6f, t));
        }

        float walking = Window(t, CreatureGo - 0.3f, CreatureGo + 0.2f, CreatureStop - 0.3f, CreatureStop + 0.1f);
        float hop = 0.26f * MathF.Abs(MathF.Sin(MathF.PI * (t - 44f))) * walking;
        float lift = ground, pitch = 0;
        if (t > Jump)
        {
            // the leap: an arc into the sea; under, up again to call him (half out of the water), and away
            float u = Clamp01((t - Jump) / 0.8f);
            float sea = Planet.Sea - Ground;
            // ... and it dives right under, eyes and all, before he wades in after it: the sea is shallower than it is
            // tall, so it goes down to the seabed and tips nose down, which keeps even its eye stalks under the water
            float target = sea - 0.62f + 0.3f * Smooth(66.4f, 66.9f, t) - 1.6f * Smooth(68.2f, 68.7f, t)
                           + 0.035f * Wave(t, 1f, 0.25f) * Window(t, 66.9f, 67.2f, 68f, 68.3f);
            lift = MathF.Max(Lerp(ground, target, u) + 0.7f * MathF.Sin(MathF.PI * u), ground + 0.02f);
            pitch = 35 * MathF.Sin(MathF.PI * u) + 75 * Smooth(68.2f, 68.65f, t);
            hop = 0;
        }
        return (a, s, lift, yaw, hop, pitch);
    }

    /// <summary>
    /// Where the creature is: along its route, and at the end aside from it, so that when the astronaut wades in and
    /// dives it is well out of his view rather than right in front of him.
    /// </summary>
    private static Vector3 CreatureAt(float time)
        => CreaturePath.Beside(CreatureDistance(time), 3.6f * Smooth(68.2f, 69.5f, time));

    /// <summary>0..1 while the creature, half out of the water, nods its eyes to call him in.</summary>
    private float Beckon => Window(t, 66.8f, 67.1f, 67.9f, 68.2f);

    // ================================================================== the mantas follow them

    /// <summary>
    /// One manta of the flock, in three parts, one for each shot:
    /// <list type="bullet">
    /// <item>A, in the first person: it sweeps low over the astronaut from behind (<c>FromA</c>) and on ahead (<c>ToA</c>),
    /// both along the way and relative to him, at <c>SideA</c> beside it;</item>
    /// <item>B, the tracking shot: it glides in from the far sky (<c>FromB</c>) towards the camera and past the pair
    /// (<c>ToB</c>), given in the view of the camera (x right, y up, z depth), so that the flock is always in the picture,
    /// and seen coming on, with its wings spread;</item>
    /// <item>C, over his shoulder: it sweeps over him again from <c>FromC</c> to <c>ToC</c>, then rises and glides ahead;
    /// at the shore it comes back and gathers round him in a loose arc (at the angle <c>Ring</c> from the way ahead,
    /// <c>RingRadius</c> away and <c>RingHeight</c> up), its nose turned to him and its tail outwards.</item>
    /// </list>
    /// Each part has its own element names and starts at a cut, so the manta can start each sweep where it needs to.
    /// </summary>
    private readonly record struct Flier(float Scale, float Phase, int Order,
        float FromA, float ToA, float SideA, float LowA,
        Vector3 FromB, Vector3 ToB,
        float FromC, float ToC, float SideC, float SideD, float LowC, float HighC,
        float Ring, float RingRadius, float RingHeight);

    private static readonly Flier[] Fliers =
    [
        new(0.80f, 0.25f, 0, -4.5f, 4.5f, -0.5f, 2.4f, new(1.5f, 1.5f, 10f), new(-1.6f, 1.4f, 4.5f), -4.0f, 6.0f, -1.3f, -0.9f, 2.2f, 4.2f, 140, 3.0f, 2.0f),
        new(0.90f, 0.75f, 1, -5.5f, 3.6f, 0.6f, 2.5f, new(2.8f, 0.9f, 11f), new(-0.2f, 0.9f, 5.5f), -4.8f, 5.0f, 0.6f, 1.1f, 2.3f, 4.8f, 160, 3.6f, 2.6f),
        new(1.00f, 0.25f, 2, -6.5f, 6.0f, 0.1f, 2.4f, new(0.6f, 2.2f, 12f), new(-2.6f, 1.9f, 6.5f), -5.5f, 7.5f, 0.2f, 0.2f, 2.2f, 5.3f, 180, 3.2f, 2.2f),
        new(0.75f, 0.75f, 3, -7.0f, 3.2f, 0.9f, 2.6f, new(3.2f, 1.8f, 9f), new(0.4f, 1.6f, 4.0f), -6.3f, 4.5f, 1.1f, -0.3f, 2.4f, 5.6f, 200, 3.7f, 2.7f),
        new(0.70f, 0.25f, 4, -8.0f, 5.4f, -0.9f, 2.4f, new(-0.2f, 1.0f, 13f), new(-3.0f, 1.0f, 7.5f), -7.0f, 6.5f, -0.9f, 1.4f, 2.1f, 4.0f, 220, 3.1f, 1.9f),
    ];

    /// <summary>At the shore the flock gathers round him: from the first time to the second.</summary>
    private const float GatherStart = 60f, GatherEnd = 63f;

    /// <summary>How much higher than the straight way the mantas fly, halfway to the arc.</summary>
    private const float GatherArc = 2.6f;

    private static float Gathered(float time) => Smooth(GatherStart, GatherEnd, time);

    /// <summary>Manta <paramref name="f"/>'s place in the arc round the astronaut at the shore (over the water where he wades).</summary>
    private static Vector3 RingPoint(Flier f, float time)
    {
        var (n, dir) = AstroPath.At(AstroDistance(time));
        var left = Vector3.Cross(n, dir);
        float angle = (f.Ring + 10 * Wave(time, 9f, f.Phase)) * Deg;
        var at = Vector3.Normalize(n * Ground + (dir * MathF.Cos(angle) + left * MathF.Sin(angle)) * f.RingRadius);
        return at * (MathF.Max(Planet.SurfaceRadius(at), Planet.Sea) + f.RingHeight + 0.12f * Wave(time, 4f, f.Phase));
    }

    /// <summary>The cuts between the three parts of the flock's flight: to the tracking shot, and to the shot over the shoulder.</summary>
    private const float FlockB = 51.5f, FlockC = 55.5f;

    private static string FlockName(float time) => time < FlockB ? "Svorka" : time < FlockC ? "SvorkaB" : "SvorkaC";

    /// <summary>The tracking shot's camera, worked out without building the scene: its eye, and its right, up and forward axes.</summary>
    private static (Vector3 Eye, Vector3 Right, Vector3 Up, Vector3 Forward) TrackingView(float time)
    {
        float d = AstroDistance(time);
        var at = AstroPath.Beside(d + 1.4f, 2.8f);
        var eye = at * (Planet.SurfaceRadius(at) + 1.3f);
        var hero = AstroPath.At(d).N;
        var critter = CreaturePath.At(CreatureDistance(time)).N;
        var target = Lerp(hero * (Planet.SurfaceRadius(hero) + 0.75f), critter * (Planet.SurfaceRadius(critter) + 0.35f), 0.4f) + hero * 0.55f;
        var forward = Vector3.Normalize(target - eye);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.Normalize(eye)));
        return (eye, right, Vector3.Cross(right, forward), forward);
    }

    /// <summary>Where manta <paramref name="f"/> is at <paramref name="time"/>, in world space.</summary>
    private static Vector3 FlierAt(Flier f, float time)
    {
        float d = AstroDistance(time), rel, side, height;
        if (time >= FlockB && time < FlockC)
        {
            var (eye, right, up, forward) = TrackingView(time);
            var v = Lerp(f.FromB, f.ToB, Smooth(FlockB - 0.5f, FlockC + 0.5f, time))
                    + new Vector3(0.1f * Wave(time, 7f, f.Phase), 0.12f * Wave(time, 5f, f.Phase), 0);
            return eye + right * v.X + up * v.Y + forward * v.Z;
        }
        if (time < FlockB)
        {
            float t0 = 46.6f + 0.3f * f.Order, t1 = 50.8f + 0.3f * f.Order, u = Smooth(t0, t1, time);
            rel = Lerp(f.FromA, f.ToA, u) - 0.25f * MathF.Max(0, time - t1);
            side = f.SideA;
            height = f.LowA + 0.7f * Smooth(-1, -4, rel);   // higher while behind him
        }
        else
        {
            float t0 = 54.6f + 0.3f * f.Order, t1 = 58.4f + 0.3f * f.Order, u = Smooth(t0, t1, time);
            rel = Lerp(f.FromC, f.ToC, u) - 0.3f * MathF.Min(MathF.Max(0, time - t1), 6);
            side = Lerp(f.SideC, f.SideD, Smooth(t1 - 1, t1 + 2, time));
            // higher behind him and over the camera, low over him, then up again ahead
            height = f.LowC + 0.8f * Smooth(0.5f, -3, rel) + (f.HighC - f.LowC) * Smooth(5, 9, rel);
        }
        float dk = d + rel + 0.25f * Wave(time, 9f, f.Phase);
        var n = AstroPath.Beside(dk, side + 0.3f * Wave(time, 7f, f.Phase + 0.3f));
        var p = n * (Planet.SurfaceRadius(n) + height + 0.15f * Wave(time, 5f, f.Phase));
        if (time <= GatherStart) return p;
        // on the way to the arc it passes high over him (and over the camera in the shallows), not at head height
        float g = Gathered(time);
        var q = Lerp(p, RingPoint(f, time), g);
        return q + Vector3.Normalize(q) * GatherArc * MathF.Sin(MathF.PI * g);
    }

    /// <summary>
    /// Which way is up for manta <paramref name="f"/>: the ground below it, except in the tracking shot, where it is the
    /// camera's ground. On so small a moon the ground under a manta a few units away is tilted by tens of degrees, and a
    /// manta flying level over it looks rolled on edge from the camera.
    /// </summary>
    private static Vector3 FlierUp(Flier f, float time)
        => Vector3.Normalize(time >= FlockB && time < FlockC ? TrackingView(time).Eye : FlierAt(f, time));

    /// <summary>The heading of manta <paramref name="f"/>: along its flight (within the current part), or along the way when it barely moves.</summary>
    private static float FlierHeading(Flier f, float time)
    {
        float from = time < FlockB ? float.MinValue : time < FlockC ? FlockB : FlockC;
        float to = time < FlockB ? FlockB - 1e-3f : time < FlockC ? FlockC - 1e-3f : float.MaxValue;
        var n = FlierUp(f, time);
        var v = FlierAt(f, MathF.Min(time + 0.1f, to)) - FlierAt(f, MathF.Max(time - 0.1f, from));
        v -= n * Vector3.Dot(n, v);
        var (hero, dir) = AstroPath.At(AstroDistance(time));
        float flight = Planet.Heading(n, v + 0.12f * dir);
        if (time <= GatherStart) return flight;

        // gathered round him at the shore: nose to him, tail outwards
        var toHero = hero * Ground - FlierAt(f, time);
        toHero -= n * Vector3.Dot(n, toHero);
        float facing = Planet.Heading(n, toHero) + 6 * Wave(time, 5f, f.Phase + 0.4f);
        return BlendAngle(flight, facing, Smooth(GatherStart + 0.6f, GatherEnd + 0.4f, time));
    }

    private void Svorka()
    {
        string name = FlockName(t);
        float from = t < FlockB ? float.MinValue : t < FlockC ? FlockB : FlockC;
        float to = t < FlockB ? FlockB - 1e-3f : t < FlockC ? FlockC - 1e-3f : float.MaxValue;
        for (int k = 0; k < Fliers.Length; k++)
        {
            var f = Fliers[k];
            var p = FlierAt(f, t);
            var (a, s) = Planet.Angles(FlierUp(f, t));
            var anchor = Anchor($"{name}{k}", a, s, 0);
            var local = ToLocal(anchor, p);
            float heading = FlierHeading(f, t);
            float turn = WrapDeg(FlierHeading(f, MathF.Min(t + 0.25f, to)) - FlierHeading(f, MathF.Max(t - 0.25f, from)));   // bank into the turns
            var body = new GroupElement($"{name}Telo{k}")
            {
                Beta = Unwrap($"{name}{k}", heading) + 5 * Wave(t, 6f, f.Phase),
                Gamma = Math.Clamp(-0.6f * turn, -30, 30) + 8 * Wave(t, 6f, f.Phase + 0.2f),
                Alpha = 4 * Wave(t, 4f, f.Phase),
            };
            MantaParts(body, $"{name}Kr{k}", $"{name}Chvost{k}", f.Scale, Wave(t, 2f, f.Phase), rigidTail: false);
            SoftTail(body, $"{name}Chvost{k}", f.Scale, t, f.Phase);
            // the offset in a group of its own: a group translates before it rotates, so it must not bank
            var place = new GroupElement($"{name}Poz{k}") { X = local.X, Y = local.Y, Z = local.Z };
            place.Add(body);
            anchor.Add(place);
            scene.Add(anchor);
            flock.Add(p);
        }
    }

    // ================================================================== water (translucent, after the sea)

    private void Splash()
    {
        // rings spreading from where the creature went in
        if (t >= Jump && t <= 69.5f)
        {
            var (n, _) = CreaturePath.At(Planet.BeachDistance + 0.25f + 0.95f);
            var (a, s) = Planet.Angles(n);
            var anchor = Anchor("Spriech", a, s, Planet.Sea - Ground + 0.012f);
            for (int k = 0; k < 2; k++)
            {
                float start = Jump + 0.75f + 0.45f * k, u = Clamp01((t - start) / 1.8f);
                float alpha = t < start ? 0 : 0.7f * Sq(1 - u);
                anchor.Add(Mat(new DiskElement($"SpriechKruh{k}")
                {
                    InnerRadius = 0.1f + 1.1f * u, OuterRadius = 0.22f + 1.45f * u, SweepAngle = 360, QualityX = 36, QualityY = 1, Alpha = -90,
                }, C(0.85f, 0.97f, 1f, alpha), ambient: 0.7f, emission: C(0.35f, 0.45f, 0.5f), spec: 1f, shine: 0.8f));
            }
            scene.Add(anchor);
        }

        // ripples round the creature calling from the water, and round his waist as he wades
        if (t >= 66 && t <= 69.5f)
            Ripple("VlnkyTvor", CreatureAt(t), 0.33f, Window(t, 66.5f, 66.9f, 68.2f, 68.7f));
        if (t >= Wade)
            Ripple("Vlnky", AstroPath.At(AstroDistance(t)).N, 0.3f, Window(t, Wade + 0.3f, Wade + 0.8f, Dive + 0.2f, Dive + 0.8f));
    }

    private void Ripple(string name, Vector3 n, float inner, float alpha)
    {
        var (a, s) = Planet.Angles(n);
        var anchor = Anchor(name, a, s, Planet.Sea - Ground + 0.014f);
        float swell = 0.5f + 0.5f * Wave(t, 1f, 0.25f);
        anchor.Add(Mat(new DiskElement(name + "K") { InnerRadius = inner, OuterRadius = inner + 0.12f + 0.1f * swell, SweepAngle = 360, QualityX = 28, QualityY = 1, Alpha = -90 },
            C(0.85f, 0.97f, 1f, 0.55f * alpha), ambient: 0.7f, emission: C(0.3f, 0.4f, 0.45f), spec: 1f, shine: 0.8f));
        scene.Add(anchor);
    }

    /// <summary>The water closes over the camera in the last moments on land.</summary>
    private void SeaFog()
    {
        if (t < 70.4f) return;
        scene.FogMode = FogMode.Linear;
        scene.FogColor = C(0.04f, 0.26f, 0.36f);
        float under = Smooth(70.4f, LandEnd, t);
        scene.FogStart = Lerp(90, 0.3f, under);
        scene.FogEnd = Lerp(160, 7, under);
    }

    // ================================================================== cameras of the journey

    /// <summary>
    /// A camera at world point <paramref name="eye"/> looking at <paramref name="target"/>, held upright: it sits in a
    /// rig standing on the moon, so its "up" is the local vertical rather than the world's Y axis.
    /// </summary>
    private CameraElement RigCamera(string name, Vector3 eye, Vector3 target, float fov)
    {
        var (a, s) = Planet.Angles(eye);
        var rig = Anchor(name + "Rig", a, s, eye.Length() - Ground);
        var cam = new CameraElement(name) { FieldOfView = fov };
        rig.Add(cam);
        Aim(cam, ToLocal(rig, target));
        scene.Add(rig);
        return cam;
    }

    /// <summary>For checking the timing: how far ahead of the astronaut the creature is, at a few moments.</summary>
    internal static string Gaps() => string.Join(", ", new[] { 45f, 49.5f, 53.5f, 57f, 60f, 64f }.Select(t =>
        $"{t}: {Vector3.Distance(AstroPath.At(AstroDistance(t)).N, CreaturePath.At(CreatureDistance(t)).N) * Ground:0.0}"));

    private void JourneyCameras()
    {
        var chest = World(astro!, new Vector3(0, 0.75f, 0));
        var critter = World(creature, new Vector3(0, 0.35f, 0));
        var helmet = (CameraElement)head!.FindChild("KamPrilba")!;
        float d = AstroDistance(t);
        CameraElement active = helmet;

        // the crane-out, until the cut to the first person view
        if (t < 49) { var crane = Crane(chest); if (t < 48.5f) active = crane; }

        // 48.5 .. 51.5 first person, through the fungi: the mantas sweep over him and on ahead

        // 51.5 .. 55.5 tracking beside them through the trees, the mantas gliding low beyond them
        if (t >= 51 && t <= 56)
        {
            var at = AstroPath.Beside(d + 1.4f, 2.8f);
            var cam = RigCamera("KamCesta", at * (Planet.SurfaceRadius(at) + 1.3f), Lerp(chest, critter, 0.4f) + Vector3.Normalize(chest) * 0.55f, 50);
            cam.Gamma = Lerp(-24, -14, Smooth(51.5f, 55.5f, t));   // a Dutch tilt: the one shot that leans, for the rush through the trees
            if (t >= FlockB && t < FlockC) active = cam;
        }

        // 55.5 .. 58.5 over his shoulder: the mantas come from behind, low over him, and fly on along the way
        if (t >= 55 && t <= 59)
        {
            var at = AstroPath.Beside(d - 1.25f, -0.5f);
            var ahead = AstroPath.At(d + 4.5f).N;
            var cam = RigCamera("KamRameno", at * (Planet.SurfaceRadius(at) + 1.8f), ahead * (Planet.SurfaceRadius(ahead) + 1.1f), 56);
            if (t >= FlockC && t < 58.5f) active = cam;
        }

        // 58.5 .. 61.5 high behind them over the crystal field: the ocean ahead
        if (t >= 58 && t <= 62)
        {
            var at = AstroPath.Beside(d - 2.3f, -1.5f);
            var cam = RigCamera("KamPole", at * (Ground + 3.1f), Lerp(chest, AstroPath.At(d + 5.5f).N * Ground, 0.45f), 52);
            if (t >= 58.5f && t < 61.5f) active = cam;
        }

        // 61.5 .. 65.5 from the shallows, looking up the beach as they come out of the jungle
        if (t >= 61 && t <= 66)
        {
            var at = CreaturePath.Beside(Planet.WadeDistance + 1.3f, 1.4f + 0.3f * Smooth(61.5f, 65.5f, t));
            var cam = RigCamera("KamBreh", at * (Planet.Sea + 0.45f), Lerp(chest, critter, 0.5f) + Vector3.Normalize(chest) * 0.15f, 48);
            if (t >= 61.5f && t < 65.5f) active = cam;
        }

        // 65.5 .. 69 the invitation: from the side, along the waterline
        if (t >= 65 && t <= 69.5f)
        {
            var at = CreaturePath.Beside(Planet.BeachDistance + 0.7f, Lerp(-2.7f, -2.2f, Smooth(65.5f, 69f, t)));
            var cam = RigCamera("KamPozvanie", at * (Planet.Sea + 0.8f), Lerp(chest, critter, 0.5f), 46);
            if (t >= 65.5f && t < 69f) active = cam;
        }

        // 69 .. the cut: first person, wading in and diving
        scene.SetActiveCamera(active);
    }
}



