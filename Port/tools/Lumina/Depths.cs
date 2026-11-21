using System.Globalization;
using System.Numerics;
using Overbryce.Core;
using Overbryce.Core.Model;
using static Lumina.Kit;

namespace Lumina;

/// <summary>
/// Under Lumina's sea. The ocean is far deeper than the little moon could hold, so it is a world of its own
/// (flat, the surface at y = 0, the way leading along +X): a kelp shelf, a slope down into a canyon, the
/// dark, the helmet lamp, and at the canyon's end a portal in the rock. The story continues there another time.
/// </summary>
internal sealed class Depths
{
    public const float Duration = 34f;

    /// <summary>0.5 s apart, with a flicker of the lamp as it comes on.</summary>
    public static IEnumerable<float> KeyTimes()
    {
        for (int i = 0; i <= 68; i++)
        {
            float k = i * 0.5f;
            yield return k;
            if (k == 18.5f)
            {
                yield return 18.6f;
                yield return 18.7f;
                yield return 18.8f;
            }
        }
    }

    /// <summary>Bounding spheres of the scenery that may be left out of key states in which no camera sees it.</summary>
    public static readonly Dictionary<string, (Vector3 Center, float Radius)> Cullable = new();

    private readonly float t;
    private readonly Scene scene;
    private GroupElement head = null!, astro = null!, creature = null!;

    private Depths(float t, float offset)
    {
        this.t = t;
        scene = new Scene("Hlbka" + (t + offset).ToString("0.0#", CultureInfo.InvariantCulture)) { AniTime = t + offset };
    }

    public static Scene Build(float t, float offset)
    {
        var d = new Depths(t, offset);
        d.Compose();
        return d.scene;
    }

    /// <summary>The active camera at <paramref name="t"/> and how far it sees in the fog (for culling).</summary>
    public static Culling.Shot Sample(float t, float offset)
    {
        var d = new Depths(t, offset);
        d.Compose();
        var cam = d.scene.ActiveCamera!;
        var w = Transforms.World(cam);
        return new Culling.Shot(t + offset, w.Translation, Vector3.Normalize(Vector3.TransformNormal(-Vector3.UnitZ, w)), cam.FieldOfView,
            d.scene.FogEnd + 1, Moon: false, NewWorld: false, Subjects: []);
    }

    /// <summary>How much daylight still reaches the swimmers: 1 under the surface, almost nothing in the canyon.</summary>
    private float Daylight => 1 - 0.93f * Smooth(7f, 18.5f, t);

    private Rgba FogColour => Lerp(C(0.004f, 0.018f, 0.045f), C(0.06f, 0.3f, 0.4f), Daylight);

    private static Rgba Lerp(Rgba a, Rgba b, float k) => C(Kit.Lerp(a.R, b.R, k), Kit.Lerp(a.G, b.G, k), Kit.Lerp(a.B, b.B, k), Kit.Lerp(a.A, b.A, k));

    private void Compose()
    {
        // the water: fog thickening and darkening with depth, closing in at the very end
        scene.FogMode = FogMode.Linear;
        scene.FogColor = FogColour;
        float close = 1 - 0.4f * Smooth(32.3f, 34f, t);
        scene.FogStart = (0.5f + 1.5f * Daylight) * close;
        scene.FogEnd = Kit.Lerp(10, 18, Daylight) * close;

        // opaque first
        Backdrop();
        scene.Add(Light("SvetloHladina", new Vector3(12, 40, -6), Mul(C(0.75f, 0.95f, 1f), 0.1f + 0.9f * Daylight), ambient: Mul(C(0.08f, 0.16f, 0.18f), Daylight)));
        Terrain();
        PortalWall();
        Rocks();
        Starfish();
        Kelp();
        Anemones();
        Astronaut();
        Creature();
        Fish("Kridel", 14, t => Kit.Lerp(new Vector3(12.5f, -3.8f, -9), new Vector3(13, -4.3f, 10), Clamp01((t - 2) / 9)), 1.3f, glow: false, from: 1.5f, to: 11.5f);
        Fish("Svetlusky", 12, t => Kit.Lerp(new Vector3(23, -9.6f, 4), new Vector3(21, -9f, -4), Clamp01((t - 12) / 10)), 1f, glow: true, from: 11.5f, to: 22.5f);
        Ray();
        // translucent last
        Surface();
        SunRays();
        Jellyfish();
        Portal();
        Bubbles();
        Cameras();
        Beam();   // after the cameras: it needs to know where the active one is
    }

    private void Register(string name, Vector3 center, float radius) => Cullable[name] = (center, radius);

    // ================================================================== the place

    private void Backdrop()
    {
        // the clear colour is black; beyond the fog everything must be the fog's colour
        var sphere = Glow(new SphereElement("HlbkaPozadie") { Radius = 70, QualityX = 24, QualityY = 16 }, FogColour);
        Place(sphere, new Vector3(15, -6, 0));
        scene.Add(sphere);
    }

    private static float Floor(float x) => -4.2f - 6.8f * Smooth(5, 15, x) - 2.6f * Smooth(15, 32, x) + 1.6f * Smooth(31.5f, 34.5f, x);
    private static float HalfWidth(float x) => Kit.Lerp(10, 3.4f, Smooth(9, 19, x));

    /// <summary>Height of the sea floor: the shelf, the slope, and the canyon with its walls.</summary>
    public static float Height(float x, float z)
    {
        float w = HalfWidth(x);
        float wall = 8.5f * Smooth(8, 17, x) * Smooth(w, w + 3.2f, MathF.Abs(z));
        float bumps = 0.35f * MathF.Sin(x * 0.9f + 1.3f) * MathF.Sin(z * 0.7f) + 0.2f * MathF.Sin(x * 2.1f + z * 1.7f);
        return Floor(x) + wall + bumps;
    }

    private static Vector3 Ground(float x, float z) => new(x, Height(x, z), z);

    private const float Cell = 3f;
    private const int CellsX = 15, CellsZ = 10;
    private const float X0 = -9, Z0 = -15;

    private static readonly Vector3[,][] Patches = BuildPatches();

    private static Vector3[,][] BuildPatches()
    {
        var all = new Vector3[CellsX, CellsZ][];
        for (int i = 0; i < CellsX; i++)
        for (int j = 0; j < CellsZ; j++)
        {
            var q = new Vector3[16];
            for (int b = 0; b < 4; b++)
            for (int a = 0; a < 4; a++)
                q[b * 4 + a] = Ground(X0 + (i + b / 3f) * Cell, Z0 + (j + a / 3f) * Cell);   // u along z, v along x: the normal points up
            all[i, j] = Planet.FitBezier(q);
        }
        return all;
    }

    private void Terrain()
    {
        for (int i = 0; i < CellsX; i++)
        for (int j = 0; j < CellsZ; j++)
        {
            var p = Patches[i, j];
            float x = X0 + (i + 0.5f) * Cell, z = Z0 + (j + 0.5f) * Cell;
            bool rock = Height(x, z) - Floor(x) > 1.2f;
            string name = $"Dno{i}_{j}";
            var mesh = Mesh(name, 4, 4, (a, b) => p[b * 4 + a], grid: 7);
            scene.Add(Mat(mesh, rock ? C(0.7f, 0.78f, 0.9f) : C(0.75f, 0.85f, 0.9f), rock ? "Skala.bmp" : "Dno.bmp", ambient: 0.7f, spec: 0.05f, shine: 0.1f));
            float lo = p.Min(c => c.Y), hi = p.Max(c => c.Y);
            Register(name, new Vector3(x, (lo + hi) / 2, z), MathF.Sqrt(2 * Sq(Cell / 2) + Sq((hi - lo) / 2)) + 0.2f);
        }
    }

    /// <summary>The rock face that closes the canyon: the portal is set in it.</summary>
    private static readonly Vector3 PortalCenter = new(34.55f, -10.6f, 0);

    private void PortalWall()
    {
        // u along +z, v along +y, so the face looks towards -X; hollowed a little behind the portal
        var wall = Mesh("StenaKonca", 4, 4, (a, b) =>
        {
            float zn = -1 + 2 * a / 3f, yn = -1 + 2 * b / 3f;
            return new Vector3(34.9f + 0.6f * (1 - zn * zn) * (1 - yn * yn) + 0.3f * MathF.Sin(a * 2.1f + b), Kit.Lerp(-14.5f, -0.5f, b / 3f), 11 * zn);
        }, grid: 10);
        scene.Add(Mat(wall, C(0.65f, 0.7f, 0.85f), "Skala.bmp", ambient: 0.7f, spec: 0.05f, shine: 0.1f));
        Register("StenaKonca", new Vector3(35, -7.5f, 0), 13);
    }

    private void Rocks()
    {
        string[] tex = ["DrsnyKamen.bmp", "SivyKamen.bmp", "Pieskovec.bmp"];
        for (int i = 0; i < 16; i++)
        {
            float x = -6 + 39 * Rnd(i, 601), z = (Rnd(i, 602) - 0.5f) * (x > 16 ? 5.5f : 14);
            if (MathF.Abs(z) < 1.2f) z += MathF.Sign(z + 0.01f) * 1.4f;
            float r = 0.25f + 0.5f * Rnd(i, 603);
            var rock = Mat(new SphereElement($"Balvan{i}") { Radius = r, QualityX = 10, QualityY = 7, Alpha = 40 * Rnd(i, 604), Beta = 360 * Rnd(i, 605) },
                C(0.65f, 0.7f, 0.8f), tex[i % 3], ambient: 0.65f, spec: 0.1f);
            Place(rock, Ground(x, z) - new Vector3(0, r * 0.35f, 0));
            scene.Add(rock);
            Register(rock.Name, Pos(rock), r);
        }
    }

    private void Starfish()
    {
        for (int i = 0; i < 12; i++)
        {
            float x = -5 + 22 * Rnd(i, 611), z = (Rnd(i, 612) - 0.5f) * 10;
            float size = 0.3f + 0.25f * Rnd(i, 613);
            var star = Mat(new RectangleElement($"Hviezdica{i}") { SizeX = size, SizeY = size, Alpha = -90, Gamma = 360 * Rnd(i, 614) },
                C(1f, 0.9f, 0.9f), "Hviezdica.bmp", ambient: 0.75f, emission: C(0.1f, 0.04f, 0.04f), spec: 0.1f);
            Place(star, Ground(x, z) + new Vector3(0, 0.06f, 0));
            scene.Add(star);
            Register(star.Name, Pos(star), size);
        }
    }

    private void Kelp()
    {
        // a kelp forest on the shelf, swaying in a slow current along +Z
        for (int i = 0; i < 34; i++)
        {
            float x = -7 + 18 * Rnd(i, 621), z = (Rnd(i, 622) - 0.5f) * 18;
            if (MathF.Abs(z) < 1.6f) continue;                               // the swimmers' way stays open
            var foot = Ground(x, z);
            float h = 2.2f + 2.3f * Rnd(i, 623), phase = Rnd(i, 624);
            var group = new GroupElement($"Chaluha{i}");
            Place(group, foot);
            for (int k = 0; k < 2; k++)
            {
                float yaw = (Rnd(i, 625 + k) * 180 + 90 * k) * Deg, width = 0.22f + 0.1f * Rnd(i, 627 + k), len = h * (0.75f + 0.3f * k);
                var across = new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw)) * width;
                float sway = 0.45f * Wave(t, 4.5f, phase + 0.2f * k);
                var blade = Mesh($"Chaluha{i}L{k}", 3, 4, (a, b) =>
                {
                    float up = b / 3f;
                    var bend = new Vector3(0.15f * up, 0, 0.35f + sway) * up * up * len;
                    return across * (a - 1) * (b == 0 ? 0.3f : 1) + new Vector3(0, up * len, 0) + bend;
                }, grid: 5);
                group.Add(Mat(blade, C(0.85f, 0.95f, 0.8f), "Chaluha.bmp", ambient: 0.7f, emission: C(0.05f, 0.08f, 0.03f), spec: 0.2f, shine: 0.3f));
            }
            var bulb = Glow(new SphereElement($"Chaluha{i}B") { Radius = 0.09f, QualityX = 8, QualityY = 6 }, C(0.8f, 0.95f, 0.5f));
            Place(bulb, new Vector3(0.15f * h, h * 0.95f, (0.35f + 0.45f * Wave(t, 4.5f, phase)) * h));
            group.Add(bulb);
            scene.Add(group);
            Register(group.Name, foot + new Vector3(0, h / 2, 0), h * 0.75f + 0.5f);
        }
    }

    private void Anemones()
    {
        Rgba[] tints = [C(1f, 0.4f, 0.75f), C(0.35f, 0.95f, 0.9f), C(1f, 0.6f, 0.25f), C(0.7f, 0.5f, 1f)];
        for (int i = 0; i < 18; i++)
        {
            float x = 12 + 22 * Rnd(i, 631), side = Rnd(i, 632) < 0.5f ? -1 : 1, z = side * (1.4f + 3.6f * Rnd(i, 633));
            var foot = Ground(x, z);
            var tint = tints[i % tints.Length];
            float size = 0.6f + 0.6f * Rnd(i, 634), phase = Rnd(i, 635);
            float glow = 0.35f + 0.15f * Wave(t, 2.6f, phase);
            var group = new GroupElement($"Sasanka{i}");
            Place(group, foot - new Vector3(0, 0.05f, 0));
            group.Add(Mat(new ConeElement($"Sasanka{i}N") { BaseRadius = 0.16f * size, TopRadius = 0.12f * size, Height = 0.35f * size, QualityX = 10, QualityY = 1, Alpha = -90 },
                Mul(tint, 0.7f), "Stonka.bmp", ambient: 0.6f, emission: Mul(tint, 0.15f)));
            for (int k = 0; k < 8; k++)
            {
                float a = (k * 45 + 20 * phase) * Deg, sway = 0.25f * Wave(t, 3f, phase + k * 0.05f);
                var arm = Glow(new ConeElement($"Sasanka{i}C{k}") { BaseRadius = 0.035f * size, TopRadius = 0.006f, Height = 0.5f * size, QualityX = 5, QualityY = 1 },
                    Mul(tint, glow + 0.25f));
                Place(arm, new Vector3(MathF.Cos(a) * 0.1f * size, 0.33f * size, MathF.Sin(a) * 0.1f * size));
                PointZ(arm, new Vector3(MathF.Cos(a) * 0.55f + sway, 1, MathF.Sin(a) * 0.55f));
                group.Add(arm);
            }
            scene.Add(group);
            Register(group.Name, foot + new Vector3(0, 0.4f * size, 0), size);
        }
        // a pair of lights: the anemones light the canyon floor a little
        scene.Add(Light("SvetloSasanky", new Vector3(20, -10.6f, 1.5f), Mul(C(0.9f, 0.45f, 0.8f), 0.6f), linear: 0.35f, quadratic: 0.08f));
    }

    // ================================================================== the swimmers

    private static readonly Path3 HeroPath = new(
    [
        new(0.8f, -0.7f, 0.3f), new(3.5f, -1.9f, 0.6f), new(7.5f, -2.6f, -0.4f), new(11, -4.4f, 0.3f), new(14.5f, -7.6f, 0.2f),
        new(18.5f, -9.6f, -0.5f), new(23, -10.4f, 0.3f), new(27.5f, -11.1f, -0.2f), new(29.4f, -11f, 0), new(30.4f, -10.8f, 0),
    ]);

    private static readonly Path3 CreaturePath = new(
    [
        new(4f, -1.5f, -0.6f), new(6.5f, -2.3f, 0.2f), new(9.5f, -3f, 0.5f), new(13, -6f, -0.3f), new(16.5f, -8.7f, 0.4f),
        new(21, -10f, -0.2f), new(25.5f, -10.6f, 0.4f), new(30, -10.8f, 0.6f), new(32.8f, -10.3f, 1.6f), new(33.5f, -10f, 2.3f),
    ]);

    private const float Arrive = 28.5f;

    private Vector3 HeroAt(float t) => HeroPath.At(Travel(t, 0, Arrive, HeroPath.Length, 2.5f, 1.4f)).P;

    /// <summary>Distance along a path: starting at <paramref name="v0"/> (the momentum of the dive), cruising, slowing to a stop at <paramref name="t1"/>.</summary>
    private static float Travel(float t, float t0, float t1, float distance, float ramp, float v0 = 0)
    {
        // cruise speed v so that the start at v0, a cruise and a stop over 'ramp' seconds cover the distance
        float total = t1 - t0, u = Math.Clamp(t - t0, 0, total);
        float v = (distance - v0 * ramp / 2) / (total - ramp);
        if (u < ramp) return v0 * u + (v - v0) * u * u / (2 * ramp);
        float start = (v0 + v) * ramp / 2;
        if (u < total - ramp) return start + v * (u - ramp);
        float w = total - u;
        return distance - v * w * w / (2 * ramp);
    }

    private void Astronaut()
    {
        float d = Travel(t, 0, Arrive, HeroPath.Length, 2.5f, 1.4f);
        var (pos, dir) = HeroPath.At(d);
        float yaw = Unwrap("astroV", MathF.Atan2(dir.X, dir.Z) / Deg);
        float descent = MathF.Asin(Math.Clamp(-dir.Y, -1, 1)) / Deg;
        float arrived = Smooth(Arrive - 1.5f, Arrive + 1f, t);

        var p = new Story.Pose { Lamp = t < 18.55f ? 0 : MathF.Abs(t - 18.7f) < 0.01f ? 0.15f : 1, LampPower = 2.6f, LampCutoff = 38, LampExponent = 28, LampLinear = 0.06f };
        // breaststroke, two seconds a stroke (the key states catch it at four points)
        float phi = 2 * MathF.PI * t / 2, swim = 1 - arrived;
        p.SwingL = p.SwingR = Kit.Lerp(-20, -95 - 70 * MathF.Cos(phi), swim);
        p.AbdL = Kit.Lerp(12, 40 + 30 * MathF.Sin(phi), swim);
        p.AbdR = -p.AbdL;
        p.BendL = p.BendR = -18 - 20 * swim;
        float kick = MathF.Sin(2 * MathF.PI * (t + 0.25f));
        p.HipL = (-8 + 16 * kick) * swim; p.HipR = (-8 - 16 * kick) * swim;
        p.KneeL = p.KneeR = 18 * swim + 10;
        // looking ahead, then about with the lamp
        p.HeadPitch = -62 * swim - 8 + 10 * Window(t, 4, 5, 7.5f, 8.2f);
        p.HeadYaw = 12 * MathF.Sin(t * 0.9f) * Window(t, 4, 5, 7.5f, 8.2f)
                    + Kit.Lerp(-28, 22, Smooth(18.8f, 20.5f, t)) * Window(t, 18.6f, 18.9f, 20.6f, 21.5f)
                    + 24 * MathF.Sin((t - 21) * 1.1f) * Window(t, 20.6f, 21.5f, 24, 25);
        // in front of the portal: upright, and he reaches out to it
        float reach = Smooth(30.5f, 32.5f, t);
        p.SwingR -= 75 * reach; p.AbdR += 25 * reach;
        p.HeadPitch -= 10 * arrived;

        var rig = new GroupElement("AstroRigV");
        Place(rig, pos);
        var turn = new GroupElement("AstroSmer") { Beta = yaw };
        var tilt = new GroupElement("AstroSklon") { Alpha = Kit.Lerp(82 + 0.6f * descent, 18, arrived) + 4 * Wave(t, 2f, 0.25f) * swim };
        astro = new GroupElement("Astronaut") { Y = -0.8f };
        head = Story.AstronautBody(astro, p, t);
        tilt.Add(astro);
        turn.Add(tilt);
        rig.Add(turn);
        scene.Add(rig);
    }

    private void Creature()
    {
        float d = Travel(t, 0, Arrive - 1, CreaturePath.Length, 2.5f, 1.2f);
        var (pos, dir) = CreaturePath.At(d);
        // at the portal it turns back to him and nods towards the opening
        var toHero = HeroAt(t) - pos;
        float yaw = BlendAngle(MathF.Atan2(dir.X, dir.Z) / Deg, MathF.Atan2(toHero.X, toHero.Z) / Deg, Smooth(Arrive - 1.5f, Arrive, t));
        float arrived = Smooth(Arrive - 1.5f, Arrive, t);
        float dark = 1 - Daylight;

        var rig = new GroupElement("TvorRigV");
        Place(rig, pos);
        creature = new GroupElement("Tvor") { Y = -0.32f, Beta = Unwrap("tvorV", yaw), Alpha = 22 * (1 - arrived) + 4 * Wave(t, 1f, 0.25f) };
        float nod = Window(t, 29.5f, 30f, 33f, 33.6f) * Wave(t, 1f, 0.25f);
        Story.CreatureBody(creature, 1 + 0.08f * Wave(t, 1f, 0.25f) * (1 - arrived), 0.6f, Wave(t, 2f, 0.25f), Kit.Lerp(-38, 0, arrived) + 22 * nod, glow: 0.15f + 0.85f * dark);
        creature.Add(Light("SvetloTvor", new Vector3(0, 0.35f, 0), Mul(C(0.65f, 0.45f, 1f), 0.15f + 0.8f * dark), linear: 0.6f, quadratic: 0.25f));
        rig.Add(creature);
        scene.Add(rig);
    }

    private static float WrapDeg(float d) => d - 360 * MathF.Round(d / 360);

    private static float Sq(float x) => x * x;

    private static float BlendAngle(float from, float to, float k) => from + WrapDeg(to - from) * k;

    // ================================================================== fauna

    /// <summary>A school of fish swimming along <paramref name="center"/>, each one wiggling on its own.</summary>
    private void Fish(string name, int count, Func<float, Vector3> center, float spread, bool glow, float from, float to)
    {
        if (t < from || t > to) return;
        var c = center(t);
        var heading = center(t + 0.5f) - center(t - 0.5f);
        float yaw = MathF.Atan2(heading.X, heading.Z) / Deg;
        var school = new GroupElement(name);
        Place(school, c);
        for (int k = 0; k < count; k++)
        {
            var off = new Vector3(Rnd(k, 641) - 0.5f, (Rnd(k, 642) - 0.5f) * 0.6f, Rnd(k, 643) - 0.5f) * 2 * spread
                      + 0.15f * new Vector3(Wave(t, 3f, Rnd(k, 644)), Wave(t, 2.5f, Rnd(k, 645)), 0);
            var fish = new GroupElement($"{name}{k}") { Beta = yaw + 90 + 12 * Wave(t, 1f, 0.25f + 0.5f * (k % 2)) };
            Place(fish, off);
            float size = 0.3f + 0.15f * Rnd(k, 646);
            for (int s = 0; s < 2; s++)
            {
                var body = glow
                    ? Glow(new RectangleElement($"{name}{k}T{s}") { SizeX = size, SizeY = size * 0.55f, Alpha = 90 * s }, C(0.5f, 0.95f, 1f), 1f, "Rybka.bmp")
                    : Mat(new RectangleElement($"{name}{k}T{s}") { SizeX = size, SizeY = size * 0.55f, Alpha = 90 * s }, C(1f, 1f, 1f), "Rybka.bmp", ambient: 0.75f, emission: C(0.15f, 0.15f, 0.15f), spec: 0.6f, shine: 0.5f);
                fish.Add(body);
            }
            school.Add(fish);
        }
        scene.Add(school);
        Register(name, c, spread * 1.8f + 0.5f);
    }

    private void Ray()
    {
        // a big ray glides over the canyon
        if (t < 10.5f || t > 20) return;
        float u = Clamp01((t - 11) / 8.5f);
        var p = Kit.Lerp(new Vector3(10, -4.5f, -9), new Vector3(27, -6.5f, 7), u);
        var dir = new Vector3(17, -2, 16);
        var rig = new GroupElement("Rejnok");
        Place(rig, p + new Vector3(0, 0.4f * Wave(t, 5f, 0.1f), 0));
        var body = new GroupElement("RejnokTelo") { Beta = MathF.Atan2(dir.X, dir.Z) / Deg, Gamma = 10 * Wave(t, 6f, 0.3f), Alpha = 6 };
        var scaled = new GroupElement("RejnokMierka");
        Story.MantaRay(scaled, 2.1f, Wave(t, 3f, 0.25f) * 0.7f);
        body.Add(scaled);
        rig.Add(body);
        scene.Add(rig);
    }

    private void Jellyfish()
    {
        if (t < 16) return;
        for (int i = 0; i < 4; i++)
        {
            var p = new Vector3(24 + 2.5f * i + 0.6f * Wave(t, 11f, 0.25f * i), -7.5f + 0.9f * (i % 2) + 0.4f * Wave(t, 6f, 0.3f * i), (i % 2 == 0 ? -1.8f : 1.6f) + 0.5f * Wave(t, 9f, 0.2f * i));
            float pulse = Wave(t, 2f, 0.25f + 0.5f * (i % 2));
            var body = new GroupElement($"HlbMeduza{i}");
            Place(body, p);
            body.Add(Glow(new SphereElement($"HlbMeduzaZvon{i}") { Radius = 0.32f * (1 + 0.12f * pulse), QualityX = 16, QualityY = 10, Alpha = -90 },
                i % 2 == 0 ? C(0.5f, 0.85f, 1f) : C(0.95f, 0.5f, 0.95f), 0.45f, "Meduza.bmp"));
            for (int k = 0; k < 4; k++)
            {
                float th = k * MathF.PI / 2 + 0.3f, spread = 0.2f - 0.1f * pulse;
                var tentacle = Glow(new ConeElement($"HlbMeduzaCh{i}{k}") { BaseRadius = 0.025, TopRadius = 0.003, Height = 0.9f, QualityX = 5, QualityY = 2 }, C(0.8f, 0.7f, 1f), 0.6f);
                Place(tentacle, new Vector3(0.15f * MathF.Cos(th), -0.1f, 0.15f * MathF.Sin(th)));
                PointZ(tentacle, new Vector3(spread * MathF.Cos(th), -1, spread * MathF.Sin(th)));
                body.Add(tentacle);
            }
            scene.Add(body);
        }
    }

    // ================================================================== light and water (translucent)

    private void Surface()
    {
        // the underside of the sea surface, bright with the sky
        var surface = Mat(new DiskElement("Hladina") { InnerRadius = 0, OuterRadius = 45, SweepAngle = 360, QualityX = 48, QualityY = 3 },
            C(0.5f, 0.85f, 0.95f, 0.75f), "Ocean.bmp", ambient: 0.5f, emission: C(0.35f, 0.62f, 0.68f), spec: 0.5f, shine: 0.5f);
        Place(surface, new Vector3(12, 0, 0));
        PointZ(surface, -Vector3.UnitY);
        scene.Add(surface);
    }

    private void SunRays()
    {
        // shafts of light slanting down from the surface, fading as the swimmers go deeper
        if (t > 14) return;
        for (int i = 0; i < 8; i++)
        {
            float x = -3 + 2.4f * i + 1.2f * Rnd(i, 651), z = (Rnd(i, 652) - 0.5f) * 9;
            float len = 7 + 3 * Rnd(i, 653);
            var ray = Glow(new ConeElement($"SvetelnyLuc{i}") { BaseRadius = 0.25f + 0.2f * Rnd(i, 654), TopRadius = 0.9f + 0.6f * Rnd(i, 655), Height = len, QualityX = 10, QualityY = 1 },
                C(0.7f, 0.95f, 1f), (0.09f + 0.05f * Wave(t, 3.5f, Rnd(i, 656))) * (1 - Smooth(7, 13, t)), "Luc.bmp");
            Place(ray, new Vector3(x, -0.05f, z));
            PointZ(ray, new Vector3(0.3f + 0.05f * Wave(t, 6f, Rnd(i, 657)), -1, 0.12f));
            scene.Add(ray);
        }
    }

    private void Bubbles()
    {
        // from his helmet valve: each bubble rises on its own from where he was when it left
        for (int k = 0; k < 60; k++)
        {
            float born = 0.55f * k, age = t - born;
            if (age < 0 || age > 3.2f) continue;
            var from = HeroAt(born) + new Vector3(0, 0.25f, 0);
            var p = from + new Vector3(0.12f * MathF.Sin(age * 5 + k), 1.1f * age, 0.12f * MathF.Cos(age * 4 + k));
            var bubble = Glass(Mat(new SphereElement($"Bublina{k}") { Radius = 0.04f + 0.03f * Rnd(k, 661) + 0.01f * age, QualityX = 8, QualityY = 6 },
                C(0.85f, 0.97f, 1f, 0.45f), ambient: 0.8f, emission: C(0.25f, 0.35f, 0.4f), spec: 1f, shine: 0.9f), 1.33f);
            Place(bubble, p);
            scene.Add(bubble);
        }
    }

    private void Portal()
    {
        float wake = Smooth(22, 28, t), pulse = 0.85f + 0.15f * Wave(t, 2f, 0.25f);
        var group = new GroupElement("Portal");
        Place(group, PortalCenter);
        var ring = Mat(new TorusElement("PortalKruh") { TubeRadius = 0.34f, RingRadius = 1.75f, QualityX = 12, QualityY = 40 },
            C(0.75f, 0.75f, 0.85f), "DrsnyKamen.bmp", ambient: 0.7f, emission: Mul(C(0.25f, 0.15f, 0.35f), 0.3f + 0.7f * wake), spec: 0.3f, shine: 0.3f);
        PointZ(ring, -Vector3.UnitX);
        group.Add(ring);
        for (int k = 0; k < 9; k++)
        {
            float a = k * 40 * Deg;
            var rune = Glow(new SphereElement($"PortalRuna{k}") { Radius = 0.09f, QualityX = 8, QualityY = 6 },
                Mul(k % 2 == 0 ? C(0.4f, 0.95f, 1f) : C(0.85f, 0.5f, 1f), (0.2f + 0.8f * wake) * (0.7f + 0.3f * Wave(t, 1.5f, k / 9f))), glow: 2.5f);
            Place(rune, new Vector3(-0.32f, 1.75f * MathF.Sin(a), 1.75f * MathF.Cos(a)));
            group.Add(rune);
        }
        // the swirling opening: two discs turning against each other
        for (int k = 0; k < 2; k++)
        {
            var vortex = Glow(new DiskElement($"PortalVir{k}")
            {
                InnerRadius = 0, OuterRadius = 1.6f - 0.15f * k, SweepAngle = 360, QualityX = 40, QualityY = 4,
                RotationPermanent = true, RotationAngleZ = k == 0 ? 360 : -360, RotationPeriod = 7 + 3 * k,
            }, Mul(C(1f, 1f, 1f), (0.15f + 0.85f * wake) * pulse), k == 0 ? 0.95f : 0.45f, "Portal.bmp");
            Place(vortex, new Vector3(0.05f - 0.06f * k, 0, 0));
            PointZ(vortex, -Vector3.UnitX);
            group.Add(vortex);
        }
        group.Add(Light("SvetloPortal", new Vector3(-1.5f, 0, 0), Mul(C(0.6f, 0.55f, 1f), 1.8f * wake * pulse), linear: 0.25f, quadratic: 0.05f));
        scene.Add(group);
    }

    /// <summary>The helmet lamp's beam, a faint cone in the dark water (drawn last).</summary>
    private void Beam()
    {
        // only in the last shot, from well away: from inside the helmet the cone would hang in front of the lens, and
        // from just beside it (over the shoulder) its faint surface reads as a flat sheet; there the pool of light on
        // the seabed shows the lamp. It appears in the key state where the last shot starts.
        if (t < 28.5f) return;
        const float baseRadius = 0.05f, topRadius = 2.9f, height = 6.5f;
        var w = Transforms.World(head);
        var lamp = Vector3.Transform(Story.LampPosition, w);
        var dir = Vector3.Normalize(Vector3.TransformNormal(Story.LampDirection, w));

        // and never with the camera in or close to the cone
        var eye = Transforms.World(scene.ActiveCamera!).Translation - lamp;
        float along = Vector3.Dot(eye, dir), across = (eye - dir * along).Length();
        if (along > -0.5f && along < height + 0.5f && across < baseRadius + (topRadius - baseRadius) * Math.Clamp(along, 0, height) / height + 0.5f) return;

        float on = t < 18.55f ? 0 : MathF.Abs(t - 18.7f) < 0.01f ? 0.15f : 1;
        var beam = Glow(new ConeElement("LampaLuc") { BaseRadius = baseRadius, TopRadius = topRadius, Height = height, QualityX = 20, QualityY = 1 },
            C(1f, 0.95f, 0.8f), 0.2f * on, "Luc.bmp");
        Place(beam, lamp);
        PointZ(beam, dir);
        scene.Add(beam);
    }

    // ================================================================== cameras

    private void Cameras()
    {
        var hero = World(astro, new Vector3(0, 0.8f, 0));
        var critter = World(creature, new Vector3(0, 0.32f, 0));
        var helmet = (CameraElement)head.FindChild("KamPrilba")!;
        CameraElement active = helmet;

        CameraElement Add(string name, Vector3 eye, Vector3 target, float fov, float t0, float t1)
        {
            var cam = new CameraElement(name) { FieldOfView = fov };
            Place(cam, eye);
            Aim(cam, target);
            scene.Add(cam);
            if (t >= t0 && t < t1) active = cam;
            return cam;
        }

        // 0 .. 4 under the surface: he plunges in among the light shafts
        if (t <= 4.5f)
            Add("KamHladina", Kit.Lerp(new Vector3(5.6f, -3.3f, 3.4f), new Vector3(6.2f, -3.6f, 2.6f), Smooth(0, 4, t)), Kit.Lerp(hero, critter, 0.3f) + new Vector3(0, 0.5f, 0), 56, 0, 4);
        // 4 .. 8 first person over the kelp; the school crosses ahead
        // 8 .. 13 from the side and above: the slope down into the canyon
        if (t >= 7.5f && t <= 13.5f)
            Add("KamSvah", hero + new Vector3(1.5f, 3.4f, 7.2f), hero + new Vector3(3.5f, -1.2f, 0), 52, 8, 13);
        // 13 .. 17 in the canyon, low, ahead of them: anemones, the ray above
        if (t >= 12.5f && t <= 17.5f)
        {
            var eye = hero + new Vector3(3.8f, -0.9f, -1.7f);
            eye.Y = MathF.Max(eye.Y, Height(eye.X, eye.Z) + 0.6f);
            Add("KamKanon", eye, Kit.Lerp(hero, critter, 0.35f) + new Vector3(0, 0.5f, 0), 54, 13, 17);
        }
        // 17 .. 20 first person: the dark, and the lamp comes on
        // 20 .. 25 beside him: the beam sweeping the canyon
        if (t >= 19.5f && t <= 25.5f)
            Add("KamLuc", hero + new Vector3(1.1f, 0.9f, -2.7f), hero + new Vector3(0.7f, -0.1f, 0.1f), 58, 20, 25);
        // 25 .. 28.5 first person: the portal ahead
        // 28.5 .. 34 the portal: behind and above them, slowly closing in
        if (t >= 28)
            Add("KamPortal", Kit.Lerp(new Vector3(28.2f, -9f, 2.4f), new Vector3(29.6f, -9.8f, 1.4f), Smooth(28.5f, 34, t)), Kit.Lerp(hero, PortalCenter, 0.62f), 50, 28.5f, 99);

        scene.SetActiveCamera(active);
    }
}

/// <summary>A smooth path in space through waypoints (Catmull-Rom), measured along its length.</summary>
internal sealed class Path3
{
    private readonly Vector3[] line;
    private readonly float[] dist;

    public Path3(IReadOnlyList<Vector3> pts)
    {
        var l = new List<Vector3>();
        for (int k = 0; k < pts.Count - 1; k++)
        {
            var p0 = pts[Math.Max(k - 1, 0)];
            var p1 = pts[k];
            var p2 = pts[k + 1];
            var p3 = pts[Math.Min(k + 2, pts.Count - 1)];
            for (int s = 0; s < 30; s++)
            {
                float u = s / 30f;
                l.Add(0.5f * (2 * p1 + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (-p0 + 3 * p1 - 3 * p2 + p3) * u * u * u));
            }
        }
        l.Add(pts[^1]);
        line = [.. l];
        dist = new float[line.Length];
        for (int i = 1; i < line.Length; i++) dist[i] = dist[i - 1] + Vector3.Distance(line[i - 1], line[i]);
    }

    public float Length => dist[^1];

    public (Vector3 P, Vector3 Dir) At(float d)
    {
        d = Math.Clamp(d, 0, dist[^1] - 1e-4f);
        int i = Array.BinarySearch(dist, d);
        if (i < 0) i = ~i - 1;
        i = Math.Clamp(i, 0, line.Length - 2);
        float u = (d - dist[i]) / MathF.Max(dist[i + 1] - dist[i], 1e-6f);
        return (Vector3.Lerp(line[i], line[i + 1], u), Vector3.Normalize(line[i + 1] - line[i]));
    }
}


