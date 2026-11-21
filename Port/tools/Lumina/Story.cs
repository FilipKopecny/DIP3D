using System.Globalization;
using System.Numerics;
using Overbryce.Core;
using Overbryce.Core.Model;
using static Lumina.Kit;

namespace Lumina;

/// <summary>
/// "Lumina": a lander arrives at a small moon of a ringed gas giant; an astronaut steps out, hops through
/// glowing fungi under drifting jellyfish and manta-like fliers, touches a crystal and meets a local.
/// <para>
/// Every key state is built from functions of time. Overbryce then interpolates between the key
/// states with its time-aware Catmull-Rom spline. Element names are unique in the whole scene.
/// </para>
/// </summary>
internal sealed partial class Story
{
    public const float Duration = LandEnd + 0.04f;

    /// <summary>
    /// 1 s apart in the establishing shot and the happy hops, 0.5 s during the action and the journey, a
    /// little denser for the creature's leap into the sea. The last key state comes 0.04 s before the
    /// underwater world, so that the change is a clean cut.
    /// </summary>
    public static IEnumerable<float> KeyTimes()
    {
        for (int i = 0; i < 8; i++) yield return i;
        for (int i = 0; i < 62; i++) yield return 8 + i * 0.5f;
        for (int i = 39; i <= 43; i++) yield return i;
        for (int i = 87; i <= 141; i++)
        {
            float k = i * 0.5f;
            yield return k;
            if (k is 65.5f or 66f or 66.5f) yield return k + 0.25f;
            if (k is 51f or 55f) yield return k + 0.46f;   // the last frame before the flock changes its names: a clean cut

        }
        yield return LandEnd;
    }

    // ------------------------------------------------------------------ the map (angles on the moon in degrees)

    private const float SiteA = -22f;                 // landing site
    private const float GroveA = 28f;                 // crystal grove
    private const float CreatureA = 31.4f, CreatureS = 7.5f; // beyond the heart crystal, seen past it from the discovery camera

    private static readonly Vector3 SunDir = Vector3.Normalize(new Vector3(0.62f, 0.5f, 0.6f));
    private static readonly Vector3 GiantPos = Vector3.Normalize(new Vector3(-0.72f, 0.4f, -0.56f)) * 50;

    // lander geometry (site frame: +Y up, the door faces +Z towards the path)
    private const float Floor = 0.72f, Top = 2.25f, HingeZ = 1.17f, RampLen = 1.45f;
    private static readonly float RampAngle = SolveRamp();
    private static readonly float TipZ = HingeZ + RampLen * MathF.Cos(RampAngle);
    private static readonly float ExitZ = TipZ + 0.45f;
    private static readonly float ExitA = SiteA + MathF.Atan2(ExitZ, Ground + G(0, ExitZ)) / Deg;
    private const float HopsA = 30f;                  // distance covered by the four hops (they end ~1.1 units before the heart crystal)

    private readonly float t;
    private readonly int key;
    private readonly Scene scene;
    private readonly Pose pose;

    private GroupElement site = null!, lander = null!, grove = null!, creature = null!;
    private GroupElement? head, astro;
    private readonly List<Vector3> flock = [];   // the mantas following the pair, in world space

    private Story(float t, float offset)
    {
        this.t = t;
        key = (int)MathF.Round(t * 2);
        scene = new Scene("Stav" + (t + offset).ToString("0.0#", CultureInfo.InvariantCulture)) { AniTime = t + offset };
        pose = AstroPose(t);
    }

    /// <summary>The key state at story time <paramref name="t"/>, placed at <paramref name="t"/> + <paramref name="offset"/> in the animation.</summary>
    public static Scene Build(float t, float offset = 0)
    {
        var story = new Story(t, offset);
        story.Compose();
        return story.scene;
    }

    /// <summary>
    /// The whole moon is in view in the establishing shot and from the crane-out on: there it is the
    /// planet with the ocean and the flora all over it (<see cref="Planet"/>). The close-ups in between keep
    /// the original moon; the worlds change at camera cuts (8 s and 39 s).
    /// </summary>
    private bool NewWorld => t < 8 || t >= 39;

    private void Compose()
    {
        // opaque things first: Overbryce draws in list order without sorting
        Sky();
        Sun();
        Giant();
        if (NewWorld)
        {
            Planet.AddTerrain(scene);
            if (Planet.Grown) Planet.AddFlora(scene, t);
        }
        else
        {
            Moon();
        }
        Flora();
        Trees();
        Creature();
        // the astronaut first appears inside the lander once the door is half open (a new element pops in at its key state)
        if (t >= 15) Astronaut();
        Lander();
        // the circling flock until the cut to the journey, then the mantas that follow the pair
        if (t < 48.5f) Mantas();
        else Svorka();
        // translucent things last
        if (NewWorld)
        {
            Planet.AddSea(scene, t);
            Splash();
        }
        Grove();
        if (NewWorld && Planet.Grown) Planet.AddCrystals(scene, t);
        Jellyfish();
        LanderFlames();
        Cameras();
        SeaFog();
    }

    // ================================================================== what the generator needs to know about the film

    /// <summary>The active camera at story time <paramref name="t"/> and the things it shows (for planting and culling).</summary>
    internal static Culling.Shot Sample(float t, float offset)
    {
        var story = new Story(t, offset);
        story.Compose();
        var cam = story.scene.ActiveCamera!;
        var w = Transforms.World(cam);
        var subjects = new List<Vector3> { World(story.lander, new Vector3(0, 1.3f, 0)), World(story.creature, new Vector3(0, 0.35f, 0)) };
        if (story.astro != null)
        {
            subjects.Add(World(story.astro, new Vector3(0, 0.75f, 0)));
            subjects.Add(World(story.astro, new Vector3(0, 1.35f, 0)));
        }
        // the mantas sweeping low over him must not fly through a plant in front of the camera
        if (t is >= 48.5f and < 58.5f) subjects.AddRange(story.flock);
        return new Culling.Shot(t + offset, w.Translation, Vector3.Normalize(Vector3.TransformNormal(-Vector3.UnitZ, w)), cam.FieldOfView,
            0, Moon: true, NewWorld: story.NewWorld, Subjects: subjects);
    }

    /// <summary>Where new plants must not grow: the lander, the path, the grove, and the plants of the original film.</summary>
    internal static IEnumerable<(Vector3 P, float R)> Keepout()
    {
        yield return (SurfaceNormal(SiteA, 0) * Ground, 3.8f);
        for (float a = SiteA; a <= GroveA + 4; a += 1.5f)
            yield return (SurfaceNormal(a, 0.5f) * Ground, 2.2f);
        yield return (SurfaceNormal(GroveA, 0) * Ground, 3f);
        yield return (SurfaceNormal(CreatureA, CreatureS) * Ground, 1.6f);
        foreach (var m in Mushrooms) yield return (SurfaceNormal(m.A, m.S) * Ground, 1.8f);
        foreach (var tr in TreeSpecs) yield return (SurfaceNormal(tr.A, tr.S) * Ground, 1.6f);
        foreach (var (a, s) in ExtraUndergrowth) yield return (SurfaceNormal(a, s) * Ground, 1.3f);
        yield return (SurfaceNormal(7.5f, -4.4f) * Ground, 1f);
    }

    // ================================================================== sky

    private void Sky()
    {
        scene.Add(Glow(new SphereElement("Obloha") { Radius = 73, QualityX = 40, QualityY = 20, Alpha = -90 }, Gray(1f), tex: "Hmlovina.bmp"));

        var stars = new GroupElement("Hviezdy");
        for (int i = 0; i < 30; i++)
        {
            float z = Rnd(i, 201) * 2 - 1, phi = Rnd(i, 202) * MathF.PI * 2, rr = MathF.Sqrt(1 - z * z);
            var d = new Vector3(rr * MathF.Cos(phi), z, rr * MathF.Sin(phi));
            float size = 0.13f + 0.24f * MathF.Pow(Rnd(i, 203), 3);
            float twinkle = 0.72f + 0.26f * Wave(t, 1.3f + 2 * Rnd(i, 204), Rnd(i, 205));
            var tint = Rnd(i, 206) switch { < 0.3f => C(0.75f, 0.85f, 1f), < 0.5f => C(1f, 0.88f, 0.7f), _ => C(1f, 1f, 1f) };
            var star = Glow(new SphereElement($"Hviezda{i}") { Radius = size, QualityX = 6, QualityY = 4 }, Mul(tint, twinkle));
            Place(star, d * 70);
            stars.Add(star);
        }
        scene.Add(stars);
    }

    private void Sun()
    {
        var p = SunDir * 63;
        var sun = Glow(new SphereElement("Slnko") { Radius = 2.4, QualityX = 24, QualityY = 16 }, C(1f, 0.97f, 0.88f));
        Place(sun, p);
        scene.Add(sun);
        float[] radii = [3.6f, 5.2f, 7.5f];
        float[] alphas = [0.42f, 0.2f, 0.09f];
        for (int i = 0; i < radii.Length; i++)
        {
            var halo = Glow(new DiskElement($"Ziara{i}") { InnerRadius = 0, OuterRadius = radii[i], SweepAngle = 360, QualityX = 36, QualityY = 1 },
                C(1f, 0.78f, 0.48f), alphas[i]);
            Place(halo, p + SunDir * (1.5f + i * 0.4f)); // behind the sun, so its disc stays crisp
            PointZ(halo, -SunDir);
            scene.Add(halo);
        }
        scene.Add(Light("SvetloSlnko", p, C(1f, 0.95f, 0.86f), ambient: C(0.1f, 0.1f, 0.14f)));
    }

    private void Giant()
    {
        var system = new GroupElement("ObrSystem");
        Place(system, GiantPos);
        var tilt = new GroupElement("ObrNaklon") { Alpha = 14, Gamma = -24 };
        tilt.Add(Mat(new SphereElement("Obor")
        {
            Radius = 11, QualityX = 48, QualityY = 32, Alpha = -90,
            RotationPermanent = true, RotationAngleZ = 360, RotationPeriod = 150,
        }, Gray(0.95f), "Obor.bmp", ambient: 0.3f, emission: C(0.035f, 0.03f, 0.025f), spec: 0.08f, shine: 0.1f));
        tilt.Add(Mat(new DiskElement("Prstence") { InnerRadius = 13.5, OuterRadius = 19, SweepAngle = 360, QualityX = 90, QualityY = 3, Alpha = -90 },
            C(0.95f, 0.9f, 0.85f, 0.9f), "Prstence.bmp", ambient: 0.5f, emission: C(0.06f, 0.055f, 0.05f), spec: 0.05f));
        // a moonlet orbiting the giant: permanent orbit about the giant's (tilted) axis
        tilt.Add(Mat(new SphereElement("Drobec")
        {
            Radius = 1.4, QualityX = 20, QualityY = 14, X = 21.5f,
            OrbitPermanent = true, OrbitAngleY = 360, OrbitPeriod = 40,
        }, C(0.85f, 0.82f, 0.8f), "SivyKamen.bmp", ambient: 0.3f));
        system.Add(tilt);
        // warm light reflected by the giant
        system.Add(Light("SvetloObor", Vector3.Zero, C(0.24f, 0.17f, 0.12f)));
        scene.Add(system);
    }

    // ================================================================== the moon and its flora

    private void Moon()
    {
        scene.Add(Mat(new SphereElement("Mesiac") { Radius = R, QualityX = 72, QualityY = 48 },
            C(0.86f, 0.93f, 0.9f), "Povrch.bmp", ambient: 0.5f, emission: C(0.05f, 0.08f, 0.08f), spec: 0.08f, shine: 0.1f));

        // detailed mossy terrain over every part of the moon a camera sees: 8 x 5 bicubic patches hugging
        // the sphere; the inner control points are pushed out so that the surface stays at Ground radius
        const int cols = 8, rows = 5;
        const float aMin = -38, aSpan = 21, sMin = -44, sSpan = 132f / rows;
        const float stepA = aSpan / 3, stepS = sSpan / 3;
        static float Lift(float step) => (8 - 2 * MathF.Cos(1.5f * step * Deg)) / (6 * MathF.Cos(0.5f * step * Deg));
        float liftA = Lift(stepA), liftS = Lift(stepS);
        for (int pa = 0; pa < cols; pa++)
        for (int ps = 0; ps < rows; ps++)
        {
            float a0 = aMin + pa * aSpan, s0 = sMin + ps * sSpan;
            var patch = Mesh($"Teren{pa}{ps}", 4, 4, (i, j) =>
            {
                float k = (i is 1 or 2 ? liftA : 1) * (j is 1 or 2 ? liftS : 1);
                return SurfaceNormal(a0 + i * stepA, s0 + j * stepS) * Ground * k;
            }, grid: 14);
            scene.Add(Mat(patch, C(0.86f, 0.93f, 0.9f), "Mach.bmp", ambient: 0.5f, emission: C(0.05f, 0.08f, 0.08f), spec: 0.08f, shine: 0.1f));
        }
    }

    private sealed record MushroomSpec(float A, float S, float Height, float Cap, Rgba Tint, Rgba Spore, float Phase);

    private static readonly MushroomSpec[] Mushrooms =
    [
        new(-5, -7, 2.0f, 0.95f, C(1f, 0.45f, 0.85f), C(0.6f, 1f, 0.95f), 0.1f),
        new(5, 7.5f, 1.5f, 0.75f, C(1f, 0.62f, 0.3f), C(1f, 0.95f, 0.5f), 0.4f),
        new(12, -9, 2.7f, 1.3f, C(0.35f, 0.85f, 0.85f), C(1f, 0.6f, 0.95f), 0.7f),
        new(18.5f, 9.5f, 1.8f, 0.9f, C(0.7f, 0.45f, 1f), C(0.6f, 1f, 0.6f), 0.25f),
        new(-11, 15, 1.3f, 0.6f, C(1f, 0.45f, 0.6f), C(0.7f, 0.9f, 1f), 0.55f),
    ];

    private static readonly (float A, float S)[] ExtraUndergrowth = [(0, -14), (9, 15), (23, -6.5f), (-16, -10), (34, -9), (-2, 9)];

    /// <summary>0..1: the crystal flares up when the astronaut touches it.</summary>
    private float Flare => Window(t, 34.9f, 35.2f, 35.6f, 37.5f);

    /// <summary>0..1: the spores burst away from the mushrooms after the flare.</summary>
    private float Burst => Window(t, 35f, 36f, 37.5f, 40f);

    private void Flora()
    {
        for (int i = 0; i < Mushrooms.Length; i++)
        {
            var m = Mushrooms[i];
            var anchor = Anchor($"Biotop{i}", m.A, m.S);
            Mushroom(anchor, i, m);
            float away = MathF.Sign(m.S); // companions grow away from the path
            Fern(anchor, $"Paprad{i}", new Vector3(away * 0.9f, 0, 0.55f), 0.75f + 0.3f * Rnd(i, 301), 30 * i, Rnd(i, 302));
            Flower(anchor, $"Horec{i}", new Vector3(away * 0.45f - 0.35f, 0, -0.7f), 0.42f, 20 + 40 * i, Rnd(i, 303));
            if (i % 2 == 1)
                Billboard(anchor, $"Hriby{i}", "Hriby.bmp", new Vector3(away * 1.25f, 0, -0.2f), 0.5f, 60 * i);
            Rock(anchor, $"Kamen{i}", new Vector3(away * 1.5f, 0, 0.9f), 0.22f + 0.1f * Rnd(i, 304), (i % 3) switch { 0 => "DrsnyKamen.bmp", 1 => "SivyKamen.bmp", _ => "Pieskovec.bmp" });
            if (i == 2)
            {
                float glow = 0.75f + 0.25f * Wave(t, 3.2f, 0.7f) + 0.6f * Burst;
                anchor.Add(Light("SvetloHuby", new Vector3(0, 1.4f, 0), Mul(C(0.95f, 0.35f, 0.85f), glow), linear: 0.45f, quadratic: 0.06f));
            }
            scene.Add(anchor);
        }

        // more undergrowth along the path
        var extra = ExtraUndergrowth;
        for (int i = 0; i < extra.Length; i++)
        {
            var (a, s) = extra[i];
            var anchor = Anchor($"Porast{i}", a, s);
            float away = MathF.Sign(s);
            Fern(anchor, $"PorastP{i}", new Vector3(0, 0, 0), 0.9f + 0.4f * Rnd(i, 311), 50 * i, Rnd(i, 312));
            Flower(anchor, $"PorastH{i}", new Vector3(away * 0.6f, 0, 0.5f), 0.38f, 45 * i, Rnd(i, 313));
            if (i % 2 == 0)
                Rock(anchor, $"PorastK{i}", new Vector3(-away * 0.2f + 0.5f, 0, -0.7f), 0.18f + 0.08f * Rnd(i, 314), i % 4 == 0 ? "SivyKamen.bmp" : "DrsnyKamen.bmp");
            scene.Add(anchor);
        }

        // a slab with fossil fish from the 1998 texture set, next to the path
        var slab = Anchor("Skamenelina", 7.5f, -4.4f);
        slab.Add(Mat(new BoxElement("Ryby") { SizeX = 0.95f, SizeY = 0.12f, SizeZ = 0.68f, Y = 0.02f, Alpha = 5, Beta = 25, Gamma = -6 },
            C(0.95f, 0.92f, 0.85f), "Ryby.bmp", ambient: 0.55f, spec: 0.15f, shine: 0.2f));
        scene.Add(slab);
    }

    private void Mushroom(GroupElement anchor, int i, MushroomSpec m)
    {
        // the whole mushroom sways about its foot (a group rotates about its parent's origin)
        var body = new GroupElement($"Huba{i}") { Alpha = 2.5f * Wave(t, 5.5f, m.Phase), Gamma = 2.5f * Wave(t, 7f, m.Phase + 0.3f) };
        float breathe = 1 + 0.05f * Wave(t, 3.2f, m.Phase);
        body.Add(Mat(new ConeElement($"Stonka{i}") { BaseRadius = 0.11f * m.Height, TopRadius = 0.07f * m.Height, Height = m.Height, QualityX = 14, QualityY = 2, Alpha = -90 },
            C(0.92f, 0.95f, 0.88f), "Stonka.bmp", ambient: 0.6f, emission: Mul(m.Tint, 0.08f)));
        body.Add(Mat(new ConeElement($"Klobuk{i}") { BaseRadius = m.Cap * breathe, TopRadius = 0.06f, Height = 0.45f * m.Cap, QualityX = 26, QualityY = 4, Alpha = -90, Y = m.Height - 0.08f },
            m.Tint, "Klobuk.bmp", ambient: 0.55f, emission: Mul(m.Tint, 0.3f + 0.25f * Burst), spec: 0.4f, shine: 0.3f));
        body.Add(Mat(new DiskElement($"Lupene{i}") { InnerRadius = 0.04, OuterRadius = m.Cap * breathe * 0.97, SweepAngle = 360, QualityX = 26, QualityY = 2, Alpha = 90, Y = m.Height - 0.07f },
            Mul(m.Tint, 0.9f), "Lupene.bmp", ambient: 0.6f, emission: Mul(m.Tint, 0.2f)));

        // spores circling the cap: a spinning group inside the swaying mushroom
        var spores = new GroupElement($"Spory{i}") { Y = m.Height + 0.1f, RotationPermanent = true, RotationAngleY = 360, RotationPeriod = 5 + 3 * m.Phase };
        for (int k = 0; k < 3; k++)
        {
            float ang = k * MathF.PI * 2 / 3 + m.Phase;
            float rad = (m.Cap + 0.25f + 0.1f * k) * (1 + 1.1f * Burst);
            var spore = Glow(new SphereElement($"Spora{i}{k}") { Radius = 0.045f, QualityX = 10, QualityY = 7 }, m.Spore);
            Place(spore, new Vector3(MathF.Cos(ang) * rad, 0.18f * Wave(t, 2.6f, k * 0.33f + m.Phase) + 0.15f * k + 0.9f * Burst, MathF.Sin(ang) * rad));
            spores.Add(spore);
        }
        body.Add(spores);
        anchor.Add(body);
    }

    private void Fern(GroupElement parent, string name, Vector3 at, float size, float yaw, float phase)
    {
        var fern = new GroupElement(name);
        Place(fern, at with { Y = G(at.X, at.Z) - 0.02f });
        for (int k = 0; k < 3; k++)
        {
            float len = size * (0.8f + 0.35f * Rnd(k, name.Length * 31 + (int)(phase * 1000)));
            float[] rise = [0f, 0.5f, 0.72f, 0.55f], reach = [0f, 0.3f, 0.66f, 0.98f], width = [0.04f, 0.21f, 0.17f, 0.04f];
            var frond = Mesh($"{name}L{k}", 4, 3, (i, j) =>
                new Vector3(len * width[i] * (j - 1), len * rise[i] + (j == 1 ? 0.04f * len : 0), len * reach[i]), grid: 6);
            frond.Beta = yaw + k * 120;
            frond.Alpha = 5 * Wave(t, 3.6f, phase + k * 0.3f); // each frond sways about its base
            fern.Add(Mat(frond, C(0.78f, 0.97f, 0.92f), "Paprad.bmp", ambient: 0.6f, emission: C(0.07f, 0.13f, 0.13f), spec: 0.1f));
        }
        parent.Add(fern);
    }

    private void Flower(GroupElement parent, string name, Vector3 at, float size, float yaw, float phase)
    {
        // a cross of two rectangles with the 1998 gentian bitmap (white = transparent)
        float y = G(at.X, at.Z) + size * 0.55f;
        for (int k = 0; k < 2; k++)
        {
            var leaf = Mat(new RectangleElement($"{name}{k}") { SizeX = size, SizeY = size * 1.1f, Beta = yaw + k * 90, Alpha = 4 * Wave(t, 4.1f, phase) },
                C(0.95f, 0.95f, 1f), "Horec.bmp", ambient: 0.65f, emission: C(0.14f, 0.14f, 0.24f), spec: 0.05f);
            Place(leaf, at with { Y = y });
            parent.Add(leaf);
        }
    }

    private static void Billboard(GroupElement parent, string name, string tex, Vector3 at, float size, float yaw)
    {
        for (int k = 0; k < 2; k++)
        {
            var r = Mat(new RectangleElement($"{name}{k}") { SizeX = size, SizeY = size, Beta = yaw + k * 90 },
                C(0.95f, 0.92f, 0.9f), tex, ambient: 0.65f, emission: C(0.1f, 0.08f, 0.08f), spec: 0.05f);
            Place(r, at with { Y = G(at.X, at.Z) + size * 0.48f });
            parent.Add(r);
        }
    }

    private static void Rock(GroupElement parent, string name, Vector3 at, float size, string tex)
    {
        var rock = Mat(new SphereElement(name) { Radius = size, QualityX = 12, QualityY = 9, Alpha = 30 * Rnd(name.Length, 1), Beta = 80 * Rnd(name.Length, 2) },
            C(0.8f, 0.78f, 0.82f), tex, ambient: 0.55f, spec: 0.1f);
        Place(rock, at with { Y = G(at.X, at.Z) - size * 0.35f });
        parent.Add(rock);
    }

    private sealed record TreeSpec(float A, float S, float Height, float Tilt, float Phase);

    private static readonly TreeSpec[] TreeSpecs =
    [
        new(10, -21, 3.4f, 6, 0.1f), new(14, 25, 3.0f, -5, 0.5f), new(36, -17, 3.8f, 4, 0.8f), new(-24, 21, 2.8f, -6, 0.3f),
    ];

    private void Trees()
    {
        for (int i = 0; i < TreeSpecs.Length; i++)
        {
            var spec = TreeSpecs[i];
            var anchor = Anchor($"StromMiesto{i}", spec.A, spec.S);
            var tree = new GroupElement($"Strom{i}") { Gamma = spec.Tilt + 1.5f * Wave(t, 6f, spec.Phase), Alpha = 1.5f * Wave(t, 7.5f, spec.Phase) };
            tree.Add(Mat(new ConeElement($"Kmen{i}") { BaseRadius = 0.22f, TopRadius = 0.06f, Height = spec.Height, QualityX = 12, QualityY = 3, Alpha = -90, Y = -0.1f },
                C(0.75f, 0.7f, 0.8f), "TmaveDrevo.bmp", ambient: 0.6f, emission: C(0.03f, 0.02f, 0.05f)));
            // whorls of fern fronds floating around the trunk, spinning in alternate directions. They are
            // disks (planar texture), so the turning shows; a torus's sphere-mapped texture would not move
            // with it. A slim glossy hoop holds each whorl together.
            for (int k = 0; k < 3; k++)
            {
                float h = spec.Height * (0.45f + 0.17f * k) + 0.06f * Wave(t, 3f, spec.Phase + k * 0.25f);
                float radius = 1.15f - 0.25f * k, spin = k % 2 == 0 ? 360 : -360, period = 6 + 2 * k;
                tree.Add(Mat(new DiskElement($"Praslen{i}{k}")
                {
                    InnerRadius = 0.1, OuterRadius = radius, SweepAngle = 360, QualityX = 40, QualityY = 4, Alpha = -90, Y = h,
                    Gamma = 51 * k + 90 * i, RotationPermanent = true, RotationAngleZ = spin, RotationPeriod = period,
                }, C(0.9f, 1f, 0.95f), "Praslen.bmp", ambient: 0.6f, emission: C(0.1f, 0.16f, 0.16f), spec: 0.2f, shine: 0.2f));
                tree.Add(Mat(new TorusElement($"Prsten{i}{k}")
                {
                    TubeRadius = 0.028f, RingRadius = radius * 0.42f, QualityX = 8, QualityY = 24, Alpha = -90, Y = h,
                }, C(0.8f, 0.95f, 1f), "ModroRuzovyMramor.bmp", ambient: 0.5f, emission: C(0.2f, 0.25f, 0.35f), spec: 0.9f, shine: 0.6f));
            }
            var fruit = Glow(new SphereElement($"Plod{i}") { Radius = 0.2f, QualityX = 14, QualityY = 10 }, C(0.95f, 0.98f, 0.55f));
            Place(fruit, new Vector3(0, spec.Height + 0.05f + 0.08f * Wave(t, 2.4f, spec.Phase), 0));
            tree.Add(fruit);
            anchor.Add(tree);
            scene.Add(anchor);
        }
    }

    // ================================================================== the crystal grove (translucent)

    private void Grove()
    {
        grove = Anchor("Haj", GroveA, 0);
        float pulse = 0.32f + 0.1f * Wave(t, 3f);
        float flare = Flare;

        Crystal(grove, "Srdce", 0.2f, 1.3f, 5, 40, C(0.6f, 0.95f, 1f), pulse + 0.9f * flare);
        (float r, float h, float tilt, float dir)[] around = [(0.1f, 0.75f, 32, 30), (0.12f, 0.6f, 40, 150), (0.09f, 0.55f, 36, 260)];
        for (int i = 0; i < around.Length; i++)
            Crystal(grove, $"SrdceK{i}", around[i].r, around[i].h, around[i].tilt, around[i].dir, C(0.55f, 0.85f, 1f), pulse * 0.8f + 0.7f * flare);

        (Vector3 at, Rgba color, int count)[] clusters =
        [
            (new Vector3(-0.7f, 0, -1.7f), C(0.8f, 0.5f, 1f), 4),
            (new Vector3(-1.1f, 0, 1.0f), C(1f, 0.55f, 0.8f), 3),
            (new Vector3(0.25f, 0, 1.9f), C(0.5f, 0.9f, 1f), 3),
        ];
        for (int c = 0; c < clusters.Length; c++)
        {
            var (at, color, count) = clusters[c];
            var cluster = new GroupElement($"Druza{c}");
            Place(cluster, at with { Y = G(at.X, at.Z) - 0.05f });
            for (int k = 0; k < count; k++)
            {
                float h = 0.5f + 0.45f * Rnd(c * 7 + k, 401);
                Crystal(cluster, $"Druza{c}K{k}", 0.08f + 0.06f * Rnd(c * 7 + k, 402), h, 12 + 30 * Rnd(c * 7 + k, 403), k * 360f / count + 40 * c,
                    color, pulse * 0.9f + 0.5f * flare * (1 - 0.15f * c));
            }
            grove.Add(cluster);
        }

        // a small glowing pool (1998 water texture)
        var pool = Mat(new DiskElement("Jazierko") { InnerRadius = 0, OuterRadius = 0.7, SweepAngle = 360, QualityX = 30, QualityY = 2, Alpha = -90 },
            C(0.45f, 0.9f, 1f, 0.72f), "Voda.bmp", ambient: 0.5f, emission: C(0.1f, 0.35f, 0.42f), spec: 1f, shine: 0.9f);
        Place(pool, new Vector3(-1.35f, G(-1.35f, -0.45f) + 0.035f, -0.45f));
        grove.Add(pool);

        // the flare: a shockwave of light that leaves the heart crystal when it is touched, growing and fading out
        float wave = Clamp01((t - 35.2f) / 1.2f);
        float waveAlpha = t < 35.2f ? 0 : 0.45f * Sq(1 - wave);
        var halo = Glow(new SphereElement("SrdceZiara") { Radius = 0.3f + 1.6f * wave, QualityX = 24, QualityY = 16 }, C(0.6f, 0.97f, 1f), waveAlpha);
        Place(halo, new Vector3(0, 0.75f, 0));
        grove.Add(halo);

        grove.Add(Light("SvetloKrystal", new Vector3(0, 0.9f, 0.3f), Mul(C(0.35f, 0.85f, 1f), 0.8f + 2.6f * flare), linear: 0.35f, quadratic: 0.12f));
        scene.Add(grove);
    }

    /// <summary>A hexagonal crystal leaning <paramref name="tilt"/> degrees towards <paramref name="dir"/> (degrees about +Y).</summary>
    private static void Crystal(GroupElement parent, string name, float r, float h, float tilt, float dir, Rgba color, float glow)
    {
        // Rz tilts +Y towards +X, then Ry turns that direction to 'dir'; the group pivots at its parent's origin
        var g = new GroupElement(name) { Gamma = -tilt, Beta = dir - 90 };
        var look = WithAlpha(color, 0.8f);
        g.Add(Mat(new ConeElement(name + "H") { BaseRadius = r, TopRadius = r * 0.92f, Height = h, QualityX = 6, QualityY = 1, Alpha = -90 },
            look, "KrystalLum.bmp", ambient: 0.4f, emission: Mul(color, glow), spec: 1f, shine: 0.85f));
        g.Add(Mat(new ConeElement(name + "S") { BaseRadius = r * 0.92f, TopRadius = 0, Height = r * 1.9f, QualityX = 6, QualityY = 1, Alpha = -90, Y = h },
            look, "KrystalLum.bmp", ambient: 0.4f, emission: Mul(color, glow * 1.15f), spec: 1f, shine: 0.85f));
        parent.Add(g);
    }

    // ================================================================== fauna

    private void Jellyfish()
    {
        (float a0, float a1, float s, float h, float phase)[] specs = [(-3, 6, -2.5f, 2.6f, 0f), (8, 15, 3.5f, 3.2f, 0.33f), (16, 23, -3.8f, 2.25f, 0.66f)];
        for (int i = 0; i < specs.Length; i++)
        {
            var (a0, a1, s, h, phase) = specs[i];
            var anchor = Anchor($"MeduzaMiesto{i}", Lerp(a0, a1, t / 48f), s + 1.2f * Wave(t, 17f, phase));
            float pulse = Wave(t, 2f, phase + 0.25f); // -1..1, sampled at its extremes
            var body = new GroupElement($"Meduza{i}") { Y = h + 0.25f * Wave(t, 6f, phase), Gamma = 6 * Wave(t, 4.5f, phase), Alpha = 4 * Wave(t, 5.5f, phase) };
            body.Add(Mat(new SphereElement($"Zvon{i}") { Radius = 0.45f * (1 + 0.1f * pulse), QualityX = 22, QualityY = 14, Alpha = -90 },
                C(0.95f, 0.55f, 0.9f, 0.5f), "Meduza.bmp", ambient: 0.5f, emission: C(0.45f, 0.18f, 0.5f), spec: 0.9f, shine: 0.7f));
            body.Add(Glow(new SphereElement($"Jadro{i}") { Radius = 0.15f, QualityX = 12, QualityY = 8 }, C(1f, 0.75f, 0.95f), 0.9f));
            for (int k = 0; k < 4; k++)
            {
                float th = k * MathF.PI / 2 + 0.4f;
                float spread = 0.25f - 0.12f * pulse;
                var tentacle = Glow(new ConeElement($"Chapadlo{i}{k}") { BaseRadius = 0.035, TopRadius = 0.004, Height = 1.1f + 0.4f * Rnd(i * 4 + k, 501), QualityX = 6, QualityY = 3 },
                    C(0.9f, 0.5f, 0.95f), 0.6f);
                Place(tentacle, new Vector3(0.2f * MathF.Cos(th), -0.15f, 0.2f * MathF.Sin(th)));
                PointZ(tentacle, new Vector3(spread * MathF.Cos(th), -1, spread * MathF.Sin(th)));
                body.Add(tentacle);
            }
            anchor.Add(body);
            scene.Add(anchor);
        }
    }

    private void Mantas()
    {
        // the flock circles the moon: a tilted group, a spinning group inside it, one group per flier
        var flock = new GroupElement("Kridla") { Beta = -35, Alpha = 8 };
        var spin = new GroupElement("KridlaRot") { Gamma = 40, RotationPermanent = true, RotationAngleZ = 360, RotationPeriod = 34 };
        (float theta, float r, float z, float phase, float scale)[] specs = [(0, 13.3f, -0.6f, 0.25f, 1f), (11, 13.9f, 0.7f, 0.75f, 0.8f), (22, 12.9f, 0.1f, 0.25f, 0.9f)];
        for (int k = 0; k < specs.Length; k++)
        {
            var (theta, r, z, phase, scale) = specs[k];
            var orbit = new GroupElement($"MantaKolo{k}") { Gamma = theta };
            var pos = new GroupElement($"Manta{k}") { Y = r + 0.3f * Wave(t, 5f, phase), Z = z };
            var body = new GroupElement($"MantaTelo{k}") { Beta = -90, Gamma = 14 * Wave(t, 6f, phase), Alpha = 5 * Wave(t, 4f, phase) };
            MantaParts(body, $"MantaKr{k}", $"MantaChvost{k}", scale, Wave(t, 2f, phase)); // flap sampled at +-1 every second
            pos.Add(body);
            orbit.Add(pos);
            spin.Add(orbit);
        }
        flock.Add(spin);
        scene.Add(flock);
    }

    /// <summary>The ray of the depths: a manta's shape, larger.</summary>
    internal static void MantaRay(GroupElement body, float scale, float flap) => MantaParts(body, "RejnokKr", "RejnokChvost", scale, flap);

    /// <summary>A manta's wings and tail in its body group (it flies towards +Z); <paramref name="flap"/> is -1..1.</summary>
    private static void MantaParts(GroupElement body, string wingName, string tailName, float scale, float flap)
    {
        float[] chordZ = [0.55f, 0f, -0.45f], spanX = [1.35f, 0.65f, 0f, -0.65f, -1.35f], chord = [0.35f, 0.8f, 1f, 0.8f, 0.35f];
        var wing = Mesh(wingName, 3, 5, (i, j) =>
        {
            float y = (j is 0 or 4 ? 0.6f : j is 1 or 3 ? 0.22f : 0f) * flap + (j == 2 && i == 0 ? 0.08f : 0);
            return new Vector3(spanX[j], y, chordZ[i] * chord[j]) * scale;
        }, grid: 8);
        body.Add(Mat(wing, C(0.92f, 0.88f, 1f), "Manta.bmp", ambient: 0.6f, emission: C(0.13f, 0.1f, 0.24f), spec: 0.5f, shine: 0.4f));
        var tail = Glow(new ConeElement(tailName) { BaseRadius = 0.05f * scale, TopRadius = 0.004, Height = 1.0f * scale, QualityX = 6, QualityY = 2 },
            C(0.5f, 0.85f, 0.95f));
        Place(tail, new Vector3(0, 0, -0.42f * scale));
        PointZ(tail, new Vector3(0, -0.08f * flap, -1));
        body.Add(tail);
    }

    private void Creature()
    {
        // face the astronaut
        var astroWorld = SurfaceNormal(pose.A, pose.S) * Ground;
        var local = ToLocal(Anchor("TvorMiesto", CreatureA, CreatureS), astroWorld);
        float heading = MathF.Atan2(local.X, local.Z) / Deg;

        float pop = Smooth(30.5f, 31f, t) + 0.25f * Window(t, 30.5f, 30.9f, 30.95f, 31.45f);
        float hop = 0.5f * Window(t, 34.7f, 35.2f, 35.3f, 35.8f)
                    + (t is > 39.5f and < 43.5f ? 0.32f * MathF.Abs(MathF.Sin(MathF.PI * (t - 39.5f) / 2)) : 0);

        // from 43.5 it leads the way to the sea (see Journey.cs)
        float a = CreatureA, s = CreatureS, lift = 0, dive = 0;
        if (t > JourneyStart)
        {
            (a, s, lift, heading, float h, dive) = CreatureJourney(heading);
            hop += h;
        }
        heading = Unwrap("tvor", heading);
        var anchor = Anchor("TvorMiesto", a, s, lift);
        float y = Lerp(-1.05f, 0, pop) + hop;
        float happy = Smooth(36f, 37.5f, t);
        float squash = 1 + 0.06f * Wave(t, 2f, 0.25f) * (t < 39 ? 1 : 1.5f);
        float wobblePeriod = t < 39 ? 1f : 2f; // alternate at every key state
        float wobble = Wave(t, wobblePeriod, 0.25f);
        float startle = Window(t, 30.8f, 31.1f, 31.4f, 32f);

        creature = new GroupElement("Tvor") { Y = y, Beta = heading, Alpha = -12 * startle + 6 * happy * wobble + dive };
        CreatureBody(creature, squash, happy, wobble, -25 * startle + 15 * Smooth(31.2f, 32f, t) - 8 * happy + 24 * Beckon * Wave(t, 1f, 0.25f));
        anchor.Add(creature);
        scene.Add(anchor);
    }

    /// <summary>The creature's body, feet, mouth and eye stalks in its group (it faces +Z, its feet at y = 0).</summary>
    internal static void CreatureBody(GroupElement creature, float squash, float happy, float wobble, float stalkPitch, float glow = 0)
    {
        float r = 0.32f * squash;
        var bodySphere = Mat(new SphereElement("TvorTelo") { Radius = r, QualityX = 22, QualityY = 16, Alpha = -90 },
            C(0.95f, 0.75f, 1f), "FialovyMramor.bmp", ambient: 0.6f, emission: C(0.16f + 0.3f * glow, 0.06f + 0.2f * glow, 0.22f + 0.45f * glow), spec: 0.5f, shine: 0.4f);
        Place(bodySphere, new Vector3(0, r, 0));
        creature.Add(bodySphere);
        var belly = Mat(new SphereElement("TvorBrucho") { Radius = 0.2f * squash, QualityX = 16, QualityY = 12 },
            C(0.75f, 1f, 0.85f), "ZelenyMramor.bmp", ambient: 0.6f, emission: C(0.08f, 0.15f, 0.1f));
        Place(belly, new Vector3(0, r * 0.8f, r * 0.42f));
        creature.Add(belly);
        for (int side = -1; side <= 1; side += 2)
        {
            var foot = Mat(new SphereElement(side < 0 ? "TvorNohaP" : "TvorNohaL") { Radius = 0.1f, QualityX = 10, QualityY = 8 }, C(0.45f, 0.25f, 0.6f), ambient: 0.6f);
            Place(foot, new Vector3(side * 0.16f, 0.07f, 0.08f));
            creature.Add(foot);
        }
        float mouthY = r * 1.02f, dy = mouthY - r;
        var mouth = Mat(new DiskElement("TvorUsta") { InnerRadius = 0, OuterRadius = 0.09f + 0.05f * happy, SweepAngle = 180, QualityX = 12, QualityY = 1, Gamma = -90 },
            C(0.4f, 0.05f, 0.12f), ambient: 0.6f, emission: C(0.15f, 0.02f, 0.05f));
        Place(mouth, new Vector3(0, mouthY - 0.02f, MathF.Sqrt(r * r - dy * dy) + 0.06f));
        creature.Add(mouth);

        // eye stalks: a joint group per stalk (translation, then the rotating group)
        for (int side = -1; side <= 1; side += 2)
        {
            string n = side < 0 ? "P" : "L";
            var joint = new GroupElement($"TvorOko{n}Klb");
            Place(joint, new Vector3(side * 0.12f, 1.85f * r, 0));
            var stalk = new GroupElement($"TvorOko{n}")
            {
                Alpha = stalkPitch,
                Gamma = -side * (10 + 14 * happy * wobble * side),
            };
            stalk.Add(Mat(new ConeElement($"TvorStopka{n}") { BaseRadius = 0.028, TopRadius = 0.02, Height = 0.28, QualityX = 8, QualityY = 2, Alpha = -90 },
                C(0.7f, 0.45f, 0.9f), ambient: 0.6f, emission: C(0.1f, 0.04f, 0.14f)));
            var eye = Mat(new SphereElement($"TvorOcko{n}") { Radius = 0.085, QualityX = 14, QualityY = 10 }, C(0.96f, 0.96f, 0.92f), ambient: 0.7f,
                emission: Mul(C(0.25f, 0.25f, 0.22f), 1 + 2.5f * glow), spec: 0.8f, shine: 0.6f);
            Place(eye, new Vector3(0, 0.31f, 0));
            stalk.Add(eye);
            var pupil = Mat(new SphereElement($"TvorZrenicka{n}") { Radius = 0.042, QualityX = 10, QualityY = 8 }, C(0.05f, 0.05f, 0.1f), ambient: 0.5f, spec: 1f, shine: 0.9f);
            Place(pupil, new Vector3(0, 0.315f, 0.06f));
            stalk.Add(pupil);
            joint.Add(stalk);
            creature.Add(joint);
        }
    }

    // ================================================================== the astronaut

    internal sealed class Pose
    {
        public float A, S, H, Heading, Pitch;
        public float Drop, Lean, Twist, HeadYaw, HeadPitch;
        public float SwingL, AbdL = 12, BendL = -18, WaveL;
        public float SwingR, AbdR = -12, BendR = -18, WaveR;
        public float HipL, HipR, KneeL, KneeR;
        public float Lamp;

        /// <summary>The helmet lamp as a spot light: brightness at Lamp = 1, cone half-angle, linear attenuation.</summary>
        public float LampPower = 0.9f, LampCutoff = 28, LampExponent = 8, LampLinear = 0.15f;
    }

    private static float SolveRamp()
    {
        float lo = 0.1f, hi = 1.5f;
        for (int i = 0; i < 60; i++)
        {
            float mid = (lo + hi) / 2;
            float z = HingeZ + RampLen * MathF.Cos(mid), y = Floor - RampLen * MathF.Sin(mid);
            if (y > G(0, z)) lo = mid; else hi = mid;
        }
        return (lo + hi) / 2;
    }

    /// <summary>Position along the way out of the lander (site frame): inside, over the door sill, down the ramp, onto the ground.</summary>
    private static (float Y, float Z) ExitPath(float d)
    {
        const float start = 0.25f;
        float inside = HingeZ - start;
        if (d <= inside) return (Floor, start + d);
        d -= inside;
        if (d <= RampLen) return (Floor - d * MathF.Sin(RampAngle), HingeZ + d * MathF.Cos(RampAngle));
        d -= RampLen;
        float z = TipZ + d;
        return (G(0, z), z);
    }

    private static Pose AstroPose(float t)
    {
        var p = new Pose();

        // ---- out of the lander (15.5 .. 19.5): four slow steps, then a wave to the camera
        float total = (HingeZ - 0.25f) + RampLen + (ExitZ - TipZ);
        var (yt, zt) = ExitPath(total * Smooth(15.5f, 19.5f, t));
        float tilt = MathF.Atan2(zt, Ground + yt) / Deg;
        p.A = SiteA + tilt;
        p.H = MathF.Sqrt(Sq(Ground + yt) + zt * zt) - Ground;
        // stand upright on the lander's floor and ramp; on the ground the rig's up is the local vertical
        p.Pitch = -tilt * (1 - Smooth(TipZ, ExitZ, zt));
        float walk = Window(t, 15.5f, 16f, 19f, 19.5f);
        float phi = 2 * MathF.PI * (t - 15.5f) / 2f;
        Gait(p, phi, walk, 20);
        p.Lamp = Smooth(15.4f, 16f, t);

        // wave to the hatch camera
        p.Heading = 51 * Window(t, 19.4f, 19.9f, 20.8f, 21.5f);
        float wave1 = Window(t, 19.6f, 20f, 20.6f, 21f);
        p.AbdL += 128 * wave1;
        p.WaveL += 28 * Wave(t, 1f, 0.25f) * Window(t, 19.9f, 20.1f, 20.5f, 20.8f);
        p.HeadYaw += 10 * wave1;

        // ---- four low-gravity hops along the path (21.5 .. 29.5)
        if (t > 21.5f)
        {
            float u = MathF.Min((t - 21.5f) / 2f, 4f);
            int k = Math.Min((int)u, 3);
            float ph = u - k;
            float air = t < 29.5f ? MathF.Sin(MathF.PI * ph) : 0;
            p.A = ExitA + HopsA / 4 * (k + Smooth(0, 1, ph));
            p.H = 0.45f * air;
            p.S = 0.9f * MathF.Sin(MathF.PI * Clamp01((t - 21.5f) / 8f)); // a gentle curve around the mushrooms
            float crouch = 30 * Sq(1 - air) * Window(t, 21.3f, 21.6f, 29.4f, 30.2f);
            Crouch(p, crouch);
            p.HipL -= 14 * air; p.HipR -= 14 * air;
            p.KneeL += 24 * air; p.KneeR += 24 * air;
            p.Lean += 8 * Window(t, 21.3f, 21.8f, 29.3f, 30f);
            p.SwingL += -25 * air + 8 * (1 - air) * Window(t, 21.3f, 21.6f, 29.4f, 30f);
            p.SwingR = p.SwingL;
            p.AbdL += 22 * air; p.AbdR -= 22 * air;
            p.HeadPitch -= 6 * air;
        }

        // ---- the creature pops up (30.5); the astronaut looks at the crystals, then at it
        p.HeadPitch += 18 * Window(t, 29.8f, 30.4f, 31.8f, 32.4f);
        p.HeadYaw += 38 * Window(t, 30.6f, 31.2f, 31.9f, 32.5f);
        float startle = Window(t, 30.7f, 31f, 31.2f, 31.8f);
        p.Lean -= 10 * startle;
        p.AbdL += 30 * startle; p.AbdR -= 30 * startle;

        // ---- one step towards the heart crystal (32 .. 34)
        // ... to arm's length: his feet stop 0.9 units from its axis (the crystal is 0.2 thick)
        if (t > 32) p.A += 1.0f * Smooth(32, 34, t);
        Gait(p, 2 * MathF.PI * (t - 32) / 2f, Window(t, 32f, 32.4f, 33.6f, 34f), 10);

        // ---- crouch and touch it (35), recoil from the flare
        float reach = Window(t, 34f, 35f, 35.2f, 36.2f);
        Crouch(p, 28 * reach);
        p.Lean += 6 * reach - 12 * Window(t, 35f, 35.4f, 35.8f, 36.6f); // little extra lean, or the helmet hits the crystal
        p.AbdR += 31 * reach; // the right arm reaches in towards the middle, onto the crystal
        p.SwingR -= 76 * reach;
        p.BendR += 12 * reach;
        p.HeadPitch += 26 * Window(t, 33.6f, 34.4f, 35.4f, 36.2f);

        // ---- turn to the creature and wave (36.5 .. 39)
        // step back from the dazzling crystal, then turn to the creature
        p.A -= 1.6f * Smooth(35.6f, 36.6f, t);
        Gait(p, 2 * MathF.PI * (t - 35.6f) / 2f, Window(t, 35.6f, 35.8f, 36.4f, 36.6f), 10);
        p.Heading += Lerp(0, 35, Smooth(36.5f, 37.5f, t));
        p.HeadYaw += 10 * Window(t, 36.6f, 37.4f, 43.5f, 44.5f);
        float wave2 = Window(t, 37.4f, 37.8f, 38.8f, 39.3f); // with the left arm, the one nearer the meeting camera
        p.AbdL += 128 * wave2;
        p.WaveL += 28 * Wave(t, 1f, 0.25f) * Window(t, 37.7f, 38f, 38.5f, 38.8f);
        p.HeadPitch += 12 * Window(t, 37f, 37.5f, 43f, 44f);

        // ---- happy hops in place (39.5 .. 43.5, key states 1 s apart)
        if (t > 39.5f && t < 43.5f)
        {
            float air = MathF.Abs(MathF.Sin(MathF.PI * (t - 39.5f) / 2));
            p.H += 0.35f * air;
            Crouch(p, 26 * Sq(1 - air));
            p.AbdL += 50 * air; p.AbdR -= 50 * air;
        }

        // ---- the journey to the ocean (see Journey.cs)
        if (t > JourneyStart) Journey(p, t);
        return p;
    }

    /// <summary>Walking: legs and arms swing in opposite phase; <paramref name="amount"/> fades the gait in and out.</summary>
    private static void Gait(Pose p, float phi, float amount, float stride)
    {
        if (amount <= 0) return;
        float s = MathF.Sin(phi), c = MathF.Cos(phi);
        p.HipL -= stride * s * amount; p.HipR += stride * s * amount;
        p.KneeL += 28 * MathF.Max(0, c) * amount; p.KneeR += 28 * MathF.Max(0, -c) * amount;
        p.SwingL += 14 * s * amount; p.SwingR -= 14 * s * amount;
        p.Twist += 6 * s * amount; // shoulders counter-rotate against the hips
        p.H += 0.03f * MathF.Abs(s) * amount;
    }

    /// <summary>Bends hips and knees by k degrees (shins stay roughly vertical) and lowers the pelvis.</summary>
    private static void Crouch(Pose p, float k)
    {
        p.HipL -= k; p.HipR -= k;
        p.KneeL += 2 * k; p.KneeR += 2 * k;
        p.Drop += 0.39f * (1 - MathF.Cos(k * Deg));
        p.Lean += 0.55f * k;
    }

    private static float Sq(float x) => x * x;

    private void Astronaut()
    {
        var p = pose;
        var rig = Anchor("AstroRig", p.A, p.S);
        astro = new GroupElement("Astronaut") { Y = p.H, Alpha = p.Pitch, Beta = p.Heading };
        head = AstronautBody(astro, p, t);
        rig.Add(astro);
        scene.Add(rig);
    }

    /// <summary>The astronaut's body in its group (he faces +Z, feet at y = 0), posed by <paramref name="p"/>. Returns the head group.</summary>
    internal static GroupElement AstronautBody(GroupElement astro, Pose p, float t)
    {
        var suit = C(0.95f, 0.95f, 0.93f);
        T Suit<T>(T e) where T : Element => Mat(e, suit, "Skafander.bmp", ambient: 0.62f, emission: C(0.05f, 0.05f, 0.06f), spec: 0.35f, shine: 0.25f);
        T Metal<T>(T e, float v = 0.45f) where T : Element => Mat(e, Gray(v), ambient: 0.6f, spec: 0.8f, shine: 0.5f);

        var pelvis = new GroupElement("Panva") { Y = 0.44f - p.Drop };
        var torso = new GroupElement("Trup") { Alpha = p.Lean, Beta = p.Twist };
        torso.Add(Suit(new ConeElement("Hrud") { BaseRadius = 0.25, TopRadius = 0.24, Height = 0.36, QualityX = 18, QualityY = 2, Alpha = -90 }));
        torso.Add(Metal(new ConeElement("Opasok") { BaseRadius = 0.262, TopRadius = 0.262, Height = 0.05, QualityX = 18, QualityY = 1, Alpha = -90, Y = -0.01f }, 0.3f));
        torso.Add(Mat(new BoxElement("Ovladac") { SizeX = 0.2f, SizeY = 0.13f, SizeZ = 0.06f, Y = 0.2f, Z = 0.25f }, Gray(0.9f), "Ovladac.bmp", ambient: 0.6f, emission: C(0.25f, 0.25f, 0.25f)));
        torso.Add(Suit(new BoxElement("Batoh") { SizeX = 0.4f, SizeY = 0.42f, SizeZ = 0.18f, Y = 0.2f, Z = -0.3f }));

        var neck = new GroupElement("Krk") { Y = 0.36f };
        var head = new GroupElement("Hlava") { Alpha = p.HeadPitch, Beta = p.HeadYaw };
        var helmet = Suit(new SphereElement("Prilba") { Radius = 0.3, QualityX = 24, QualityY = 18, Alpha = -90 });
        Place(helmet, new Vector3(0, 0.24f, 0));
        head.Add(helmet);
        var visor = Mat(new SphereElement("Hledi") { Radius = 0.24, QualityX = 24, QualityY = 18, Alpha = -90 },
            C(1f, 0.85f, 0.55f), "Hledi.bmp", ambient: 0.5f, emission: C(0.18f, 0.12f, 0.05f), spec: 1f, shine: 0.9f);
        Place(visor, new Vector3(0, 0.25f, 0.1f));
        head.Add(visor);
        var lamp = Metal(new ConeElement("Lampa") { BaseRadius = 0.045, TopRadius = 0.05, Height = 0.1, QualityX = 10, QualityY = 1 }, 0.55f);
        Place(lamp, new Vector3(0.22f, 0.36f, 0.12f));
        head.Add(lamp);
        var lampGlass = Glow(new DiskElement("LampaSklo") { InnerRadius = 0, OuterRadius = 0.048, SweepAngle = 360, QualityX = 12, QualityY = 1 },
            Mul(C(1f, 0.95f, 0.8f), 0.2f + 0.78f * p.Lamp));
        Place(lampGlass, new Vector3(0.22f, 0.36f, 0.222f));
        head.Add(lampGlass);
        var antenna = Metal(new ConeElement("AntenaPrilby") { BaseRadius = 0.012, TopRadius = 0.008, Height = 0.22, QualityX = 6, QualityY = 1, Alpha = -90 });
        Place(antenna, new Vector3(-0.16f, 0.46f, -0.08f));
        head.Add(antenna);
        var blink = Glow(new SphereElement("AntenaSvetlo") { Radius = 0.03, QualityX = 8, QualityY = 6 }, Mul(C(1f, 0.25f, 0.2f), 0.35f + 0.6f * MathF.Max(0, Wave(t, 1f, 0.25f))));
        Place(blink, new Vector3(-0.16f, 0.69f, -0.08f));
        head.Add(blink);
        head.Add(Light("SvetloPrilba", LampPosition, Mul(C(1f, 0.95f, 0.82f), p.LampPower * p.Lamp), dir: LampDirection,
            cutoff: p.LampCutoff, exponent: p.LampExponent, linear: p.LampLinear));
        head.Add(new CameraElement("KamPrilba") { Y = 0.26f, Z = 0.36f, Beta = 180, Alpha = -6, FieldOfView = 62 });
        neck.Add(head);
        torso.Add(neck);

        Arm(torso, "L", +1, p.SwingL, p.AbdL, p.BendL, p.WaveL, Suit);
        Arm(torso, "P", -1, p.SwingR, p.AbdR, p.BendR, p.WaveR, Suit);
        pelvis.Add(torso);
        Leg(pelvis, "L", +1, p.HipL, p.KneeL, Suit);
        Leg(pelvis, "P", -1, p.HipR, p.KneeR, Suit);

        astro.Add(pelvis);
        return head;
    }

    /// <summary>Where the helmet lamp is in the head group, and where it shines.</summary>
    internal static readonly Vector3 LampPosition = new(0.22f, 0.36f, 0.25f), LampDirection = new(0, -0.18f, 1);

    // his left is +X (he faces +Z)
    private static void Arm(GroupElement torso, string n, int side, float swing, float abd, float bend, float wave, Func<Element, Element> suit)
    {
        var shoulder = new GroupElement($"Rameno{n}Klb");
        Place(shoulder, new Vector3(side * 0.31f, 0.3f, 0));
        var upper = new GroupElement($"Rameno{n}") { Alpha = swing, Gamma = abd };
        upper.Add(suit(new SphereElement($"Ramenko{n}") { Radius = 0.095, QualityX = 12, QualityY = 8 }));
        upper.Add(suit(new ConeElement($"Paza{n}") { BaseRadius = 0.08, TopRadius = 0.072, Height = 0.22, QualityX = 12, QualityY = 1, Alpha = 90 }));
        var elbow = new GroupElement($"Laket{n}Klb") { Y = -0.22f };
        var lower = new GroupElement($"Laket{n}") { Alpha = bend, Gamma = wave };
        lower.Add(suit(new ConeElement($"Predlaktie{n}") { BaseRadius = 0.072, TopRadius = 0.068, Height = 0.2, QualityX = 12, QualityY = 1, Alpha = 90 }));
        var glove = Mat(new SphereElement($"Rukavica{n}") { Radius = 0.075, QualityX = 12, QualityY = 8, Y = -0.24f }, C(0.95f, 0.55f, 0.18f), ambient: 0.6f, spec: 0.3f);
        lower.Add(glove);
        elbow.Add(lower);
        upper.Add(elbow);
        shoulder.Add(upper);
        torso.Add(shoulder);
    }

    private static void Leg(GroupElement pelvis, string n, int side, float hip, float knee, Func<Element, Element> suit)
    {
        var hipJoint = new GroupElement($"Bedro{n}Klb");
        Place(hipJoint, new Vector3(side * 0.12f, 0, 0));
        var thigh = new GroupElement($"Bedro{n}") { Alpha = hip };
        thigh.Add(suit(new ConeElement($"Stehno{n}") { BaseRadius = 0.1, TopRadius = 0.09, Height = 0.2, QualityX = 12, QualityY = 1, Alpha = 90 }));
        var kneeJoint = new GroupElement($"Koleno{n}Klb") { Y = -0.2f };
        var shin = new GroupElement($"Koleno{n}") { Alpha = knee };
        shin.Add(suit(new ConeElement($"Lytko{n}") { BaseRadius = 0.09, TopRadius = 0.085, Height = 0.19, QualityX = 12, QualityY = 1, Alpha = 90 }));
        shin.Add(Mat(new BoxElement($"Bota{n}") { SizeX = 0.14f, SizeY = 0.09f, SizeZ = 0.22f, Y = -0.2f, Z = 0.04f }, Gray(0.35f), ambient: 0.6f, spec: 0.3f));
        kneeJoint.Add(shin);
        thigh.Add(kneeJoint);
        hipJoint.Add(thigh);
        pelvis.Add(hipJoint);
    }

    // ================================================================== the lander

    /// <summary>Lander position in the landing-site frame: a long curved approach, then a soft vertical touchdown at 12.5 s.</summary>
    private static Vector3 LanderPos(float t)
    {
        float h = Smooth(0, 9.5f, t);
        float x = -16 * (1 - h) + 3.5f * MathF.Sin(MathF.PI * h);
        float z = -11 * (1 - h);
        float y = 24 * MathF.Pow(1 - Smooth(0, 12.5f, t), 1.6f);
        return new Vector3(x, y, z);
    }

    private (GroupElement Site, GroupElement Body, GroupElement Frame) LanderChain(string suffix)
    {
        var siteAnchor = Anchor("LanderMiesto" + suffix, SiteA, 0);
        var body = new GroupElement("Lander" + suffix);
        Place(body, LanderPos(t));
        float approach = 1 - Smooth(4f, 10f, t);
        var frame = new GroupElement("LanderTrup" + suffix)
        {
            Alpha = -14 * approach + 1.2f * Wave(t, 2.3f) * (1 - Smooth(11.5f, 12.5f, t)),
            Gamma = 9 * approach + 1.2f * Wave(t, 3.1f, 0.3f) * (1 - Smooth(11.5f, 12.5f, t)),
        };
        // touchdown: the spline stops exactly here instead of overshooting into the ground
        bool stop = MathF.Abs(t - 12.5f) < 1e-3f;
        body.MotionBoundary = stop;
        frame.MotionBoundary = stop;
        body.Add(frame);
        siteAnchor.Add(body);
        return (siteAnchor, body, frame);
    }

    private void Lander()
    {
        var (siteAnchor, body, frame) = LanderChain("");
        site = siteAnchor;
        lander = body;
        LanderBody(frame, t, lights: true);
        scene.Add(siteAnchor);
    }

    /// <summary>
    /// The lander's geometry in its own frame (feet on y = 0, door towards +Z) as it is at story time
    /// <paramref name="t"/>: legs, door, mast and dish move with the story. The intro reuses it at a smaller scale.
    /// </summary>
    internal static void LanderBody(GroupElement frame, float t, bool lights)
    {
        T Foil<T>(T e) where T : Element => Mat(e, C(1f, 0.9f, 0.75f), "Folia.bmp", ambient: 0.55f, emission: C(0.04f, 0.03f, 0.01f), spec: 0.9f, shine: 0.55f);
        T Panel<T>(T e) where T : Element => Mat(e, Gray(0.92f), "Panel.bmp", ambient: 0.55f, emission: C(0.03f, 0.03f, 0.04f), spec: 0.7f, shine: 0.45f);
        T Metal<T>(T e, float v) where T : Element => Mat(e, Gray(v), ambient: 0.55f, spec: 0.8f, shine: 0.5f);

        // descent stage: an octagonal drum with a flat face towards +Z (the door)
        frame.Add(Foil(new ConeElement("Telo") { BaseRadius = 1.25, TopRadius = 1.25, Height = Top - 0.7f, QualityX = 8, QualityY = 1, Alpha = -90, Y = 0.7f, Gamma = 22.5f }));
        frame.Add(Metal(new DiskElement("TeloSpodok") { InnerRadius = 0, OuterRadius = 1.25, SweepAngle = 360, QualityX = 8, QualityY = 1, Alpha = 90, Y = 0.7f, Gamma = 22.5f }, 0.35f));
        frame.Add(Panel(new DiskElement("TeloVrch") { InnerRadius = 0, OuterRadius = 1.25, SweepAngle = 360, QualityX = 8, QualityY = 1, Alpha = -90, Y = Top, Gamma = 22.5f }));
        // crew cabin
        frame.Add(Panel(new ConeElement("Kabina") { BaseRadius = 0.95, TopRadius = 0.62, Height = 0.85, QualityX = 12, QualityY = 1, Alpha = -90, Y = Top }));
        frame.Add(Panel(new DiskElement("KabinaVrch") { InnerRadius = 0, OuterRadius = 0.62, SweepAngle = 360, QualityX = 12, QualityY = 1, Alpha = -90, Y = Top + 0.85f }));
        const float wy = Top + 0.38f;
        foreach (var (name, at) in new[] { ("Okno1", new Vector3(0, wy, 0.79f)), ("Okno2", new Vector3(0.79f, wy, 0)), ("Okno3", new Vector3(-0.79f, wy, 0)) })
        {
            var window = Mat(new SphereElement(name) { Radius = 0.15, QualityX = 14, QualityY = 10 }, C(0.3f, 0.55f, 0.85f), ambient: 0.5f,
                emission: C(0.25f, 0.45f, 0.6f), spec: 1f, shine: 0.9f);
            Place(window, at);
            frame.Add(window);
        }
        frame.Add(Metal(new ConeElement("Tryska") { BaseRadius = 0.22, TopRadius = 0.42, Height = 0.45, QualityX = 16, QualityY = 2, Alpha = 90, Y = 0.7f }, 0.28f));
        // badges on the side faces (white = transparent)
        for (int side = -1; side <= 1; side += 2)
        {
            var badge = Mat(new RectangleElement(side > 0 ? "ZnakL" : "ZnakP") { SizeX = 0.75f, SizeY = 0.75f, Beta = side * 90 }, Gray(0.95f), "Znak.bmp",
                ambient: 0.6f, emission: C(0.1f, 0.1f, 0.12f), spec: 0.4f);
            Place(badge, new Vector3(side * 1.163f, (0.7f + Top) / 2, 0));
            frame.Add(badge);
        }

        // landing legs: hinge (translation) -> leg frame turned outwards and folded about its Z axis
        float deploy = 1 - Smooth(8.5f, 10.5f, t);
        for (int i = 0; i < 4; i++)
        {
            float th = (45 + 90 * i) * Deg;
            var hinge = new GroupElement($"NohaKlb{i}");
            Place(hinge, new Vector3(1.16f * MathF.Cos(th), 1.0f, 1.16f * MathF.Sin(th)));
            var leg = new GroupElement($"Noha{i}") { Beta = -th / Deg, Gamma = 100 * deploy };
            float footR = 2.15f;
            var foot = new Vector3(footR - 1.16f, G(footR * MathF.Cos(th), footR * MathF.Sin(th)) - 1.0f + 0.03f, 0);
            leg.Add(PointZ(Metal(new ConeElement($"Vzpera{i}") { BaseRadius = 0.05, TopRadius = 0.04, Height = foot.Length(), QualityX = 8, QualityY = 1 }, 0.6f), foot));
            var pad = Foil(new DiskElement($"Tanier{i}") { InnerRadius = 0, OuterRadius = 0.25, SweepAngle = 360, QualityX = 14, QualityY = 1, Alpha = 90 });
            Place(pad, foot);
            leg.Add(pad);
            var knob = Metal(new SphereElement($"Klbik{i}") { Radius = 0.07, QualityX = 10, QualityY = 6 }, 0.5f);
            Place(knob, foot + new Vector3(0, 0.04f, 0));
            leg.Add(knob);
            hinge.Add(leg);
            frame.Add(hinge);
        }

        // the door becomes a ramp: hinge at the sill, rotation about X
        float open = Smooth(14f, 15.5f, t);
        var opening = Mat(new RectangleElement("Otvor") { SizeX = 0.72f, SizeY = RampLen }, Gray(0.06f), ambient: 0.2f, emission: C(0.16f, 0.12f, 0.07f), spec: 0f);
        Place(opening, new Vector3(0, Floor + RampLen / 2, 1.158f));
        frame.Add(opening);
        var doorHinge = new GroupElement("DvereKlb");
        Place(doorHinge, new Vector3(0, Floor, HingeZ));
        var door = new GroupElement("DvereRot") { Alpha = (90 + RampAngle / Deg) * open };
        door.Add(Panel(new BoxElement("Dvere") { SizeX = 0.72f, SizeY = RampLen, SizeZ = 0.04f, Y = RampLen / 2, Z = 0.02f }));
        doorHinge.Add(door);
        frame.Add(doorHinge);

        // telescopic mast with a scanning dish: mast group -> spinning group -> tilted dish
        float mast = Lerp(0.02f, 0.7f, Smooth(13.5f, 15f, t));
        frame.Add(Metal(new ConeElement("Stoziar") { BaseRadius = 0.035, TopRadius = 0.025, Height = mast, QualityX = 8, QualityY = 1, Alpha = -90, Y = Top + 0.85f }, 0.7f));
        var dishJoint = new GroupElement("AntenaKlb") { Y = Top + 0.85f + mast };
        bool scanning = t >= 15f;
        var dishSpin = new GroupElement("Antena") { RotationPermanent = scanning, RotationAngleY = 360, RotationPeriod = 8 };
        var dish = new GroupElement("AntenaMisa") { Alpha = -35 * Smooth(14.5f, 15.5f, t) };
        dish.Add(Panel(new ConeElement("Misa") { BaseRadius = 0.05, TopRadius = 0.42, Height = 0.15, QualityX = 18, QualityY = 2, Alpha = -90 }));
        var feed = Glow(new SphereElement("AntenaHrot") { Radius = 0.04, QualityX = 8, QualityY = 6, Y = 0.22f }, C(0.4f, 1f, 0.6f));
        dish.Add(feed);
        dish.Add(Metal(new ConeElement("AntenaTyc") { BaseRadius = 0.012, TopRadius = 0.012, Height = 0.22, QualityX = 6, QualityY = 1, Alpha = -90 }, 0.7f));
        dishSpin.Add(dish);
        dishJoint.Add(dishSpin);
        frame.Add(dishJoint);

        // floodlight under the descent stage
        if (lights)
            frame.Add(Light("SvetloReflektor", new Vector3(0, 0.5f, 0.2f), Mul(C(1f, 0.93f, 0.8f), Smooth(5f, 7f, t) * (1 - 0.4f * Smooth(14f, 16f, t))),
                dir: new Vector3(0, -1, 0.35f), cutoff: 55, exponent: 5, linear: 0.08f));
    }

    private void LanderFlames()
    {
        var (siteAnchor, _, frame) = LanderChain("2");
        float on = 1 - Smooth(12.5f, 13.3f, t);
        float flicker = Rnd(key, 7) - 0.5f;
        float len = (0.9f + 0.6f * (1 - Smooth(5f, 10f, t)) + 0.3f * flicker) * on + 0.01f;
        frame.Add(Glow(new ConeElement("Plamen") { BaseRadius = 0.38, TopRadius = 0.03, Height = len, QualityX = 16, QualityY = 3, Alpha = 90, Y = 0.25f },
            C(1f, 0.62f, 0.3f), 0.7f * on, "Plamen.bmp"));
        frame.Add(Glow(new ConeElement("PlamenJadro") { BaseRadius = 0.2, TopRadius = 0.01, Height = 0.55f * len, QualityX = 12, QualityY = 2, Alpha = 90, Y = 0.25f },
            C(1f, 0.94f, 0.75f), 0.85f * on, "Plamen.bmp"));
        frame.Add(Light("SvetloMotor", new Vector3(0, -0.4f, 0), Mul(C(1f, 0.55f, 0.22f), on * (1.2f + 0.4f * flicker)), linear: 0.3f));

        // dust thrown up at touchdown (in the site frame, lying on the ground)
        float spread = Smooth(11.6f, 15f, t);
        var dust = Mat(new DiskElement("Prach") { InnerRadius = 0.5f + 3f * spread, OuterRadius = 1.3f + 4.2f * spread, SweepAngle = 360, QualityX = 40, QualityY = 3, Alpha = -90, Y = 0.06f + 0.25f * spread },
            C(0.78f, 0.74f, 0.82f, 0.55f * Window(t, 11.3f, 12.2f, 13f, 15.5f)), "Prach.bmp", ambient: 0.7f, emission: C(0.08f, 0.07f, 0.09f), spec: 0f);
        siteAnchor.Add(dust);
        scene.Add(siteAnchor);
    }

    // ================================================================== cameras

    private void Cameras()
    {
        if (t >= JourneyStart)
        {
            JourneyCameras();
            return;
        }
        var landerWorld = World(lander, new Vector3(0, 1.3f, 0));
        var chest = astro is null ? World(site, new Vector3(0, 1.5f, 0.5f)) : World(astro, new Vector3(0, 0.75f, 0));

        // 1. establishing shot (0 .. 8): the moon, the giant, the lander coming in
        var cam1 = new CameraElement("KamVesmir") { FieldOfView = 48 };
        Place(cam1, Lerp(new Vector3(19, 9, 14), new Vector3(9, 15.5f, 12), Smooth(0, 8.5f, t)));
        Aim(cam1, Lerp(Lerp(landerWorld, World(site), 0.35f), World(site, new Vector3(0, 2, 0)), Smooth(3, 9, t)));
        scene.Add(cam1);

        // 2. descent (8 .. 13): low on the ground, looking up at the lander
        var cam2 = new CameraElement("KamZostup") { FieldOfView = 52 };
        Place(cam2, Lerp(new Vector3(-4.8f, 0.45f, 4.6f), new Vector3(-4.2f, 0.9f, 5.0f), Smooth(8, 13, t)));
        Aim(cam2, Lerp(LanderPos(t) + new Vector3(0, 1.3f, 0), new Vector3(0, 1.1f, 0.5f), Smooth(11f, 13.5f, t)));
        site.Add(cam2);

        // 3. the hatch (13 .. 21): door, mast, the astronaut walking out and waving
        var cam3 = new CameraElement("KamDvere") { FieldOfView = 46 };
        Place(cam3, Lerp(new Vector3(3.6f, 1.6f, 5.2f), new Vector3(2.8f, 1.2f, 4.5f), Smooth(13, 21, t)));
        Aim(cam3, Lerp(new Vector3(0, 1.45f, 1.2f), ToLocal(site, chest), Smooth(15.3f, 16.8f, t)));
        site.Add(cam3);

        // 4. tracking (21 .. 29): rides along beside the hopping astronaut
        // moves at the hops' average speed, a little ahead of him, so we see him coming
        var trackRig = Anchor("SledRig", ExitA + HopsA * Clamp01((t - 21.5f) / 8f), 0);
        var cam4 = new CameraElement("KamSled") { FieldOfView = 46 };
        Place(cam4, new Vector3(3.4f, 1.9f, 1.6f));
        trackRig.Add(cam4);
        scene.Add(trackRig);
        Aim(cam4, ToLocal(trackRig, chest) + new Vector3(0, 0.1f, 0));

        // 5. through the helmet (29 .. 32): KamPrilba lives in the head group

        // 6. discovery (32 .. 39): the crystal, the astronaut and the creature
        var cam6 = new CameraElement("KamObjav") { FieldOfView = 44 };
        // from the side for the touch: the astronaut, the heart crystal, the creature beyond it
        Place(cam6, Lerp(new Vector3(-2.7f, 1.2f, 0.3f), new Vector3(-2.3f, 1.05f, 0.4f), Smooth(32, 36, t)));
        Aim(cam6, ToLocal(grove, chest) + new Vector3(0.25f, -0.05f, 0.2f));
        grove.Add(cam6);

        // 6b. the meeting (36 .. 39): cut across to the other side, a two-shot with the crystal behind them
        var cam6b = new CameraElement("KamStretnutie") { FieldOfView = 46 };
        Place(cam6b, Lerp(new Vector3(3.6f, 1.7f, -0.75f), new Vector3(3.15f, 1.45f, -0.5f), Smooth(36, 39, t)));
        var creatureLocal = ToLocal(grove, World(creature, new Vector3(0, 0.4f, 0)));
        Aim(cam6b, (ToLocal(grove, chest) + creatureLocal) / 2 + new Vector3(0, 0.05f, 0));
        grove.Add(cam6b);

        // 7. the crane-out (39 .. 48.5)
        var cam7 = Crane(chest);

        var helmet = (CameraElement?)head?.FindChild("KamPrilba");
        CameraElement active = t switch
        {
            < 8 => cam1,
            < 13 => cam2,
            < 21 => cam3,
            < 29 => cam4,
            < 32 => helmet!,
            < 36 => cam6,
            < 39 => cam6b,
            _ => cam7,
        };
        scene.SetActiveCamera(active);
    }

    /// <summary>Crane up and away until the whole moon is in view.</summary>
    private CameraElement Crane(Vector3 chest)
    {
        var cam7 = new CameraElement("KamFinale") { FieldOfView = Lerp(44, 52, Smooth(39, 47, t)) };
        var start = World(grove, new Vector3(-2.6f, 1.5f, 1.8f));
        // once the pair sets off (43.5), the crane swings round towards the ocean they are heading for
        float swing = Smooth(42.5f, 46.5f, t);
        var end = Lerp(Vector3.Normalize(new Vector3(0.5f, 0.85f, 0.65f)) * 24f, Planet.RouteFrame(33, 2) * 28f, swing);
        float k = Smooth(40f, 47.5f, t);
        var path = Lerp(start, end, k) + Vector3.Normalize(start) * 4f * MathF.Sin(MathF.PI * k);
        Place(cam7, path);
        Aim(cam7, Lerp(chest, Lerp(new Vector3(0, 3.5f, 0), Planet.RouteFrame(20, 0) * 1.2f, swing), Smooth(40.5f, 47f, t)));
        scene.Add(cam7);
        return cam7;
    }
}

