using System.Numerics;
using Overbryce.Core;
using Overbryce.Core.Model;
using static Lumina.Kit;

namespace Lumina;

/// <summary>
/// The geography of Lumina: the ocean on the southern hemisphere, the terrain that covers the whole moon,
/// the route from the crystal grove to the shore, and the flora that grows in colonies all over the land.
/// <para>
/// The ocean is a translucent sphere at sea level. The terrain patches lie above it on land and dip below
/// it towards the ocean, so the coastline is where the two meet. Its shape comes from the ocean's
/// radius, which varies with the direction around the ocean's centre.
/// </para>
/// </summary>
internal static class Planet
{
    /// <summary>Radius of the sea surface (just below the walkable ground, so the land hides it).</summary>
    public const float Sea = 10.0f;

    /// <summary>Radius of the dark seabed sphere under the open ocean.</summary>
    public const float Seabed = 9.1f;

    /// <summary>How far the terrain falls below the ground radius under the open sea.</summary>
    private const float Depth = Ground - Seabed + 0.03f;

    public static readonly Vector3 OceanCenter = Vector3.Normalize(new Vector3(0.25f, -1f, 0.45f));
    private static readonly Vector3 E1 = Vector3.Normalize(Vector3.Cross(OceanCenter, Vector3.UnitZ));
    private static readonly Vector3 E2 = Vector3.Cross(OceanCenter, E1);

    // ================================================================== the ocean and the terrain height

    /// <summary>Angular radius of the ocean (degrees) in the direction of <paramref name="n"/> around its centre.</summary>
    public static float OceanRadius(Vector3 n)
    {
        float psi = MathF.Atan2(Vector3.Dot(n, E2), Vector3.Dot(n, E1));
        return 63 + 6 * MathF.Sin(3 * psi + 1f) + 3.5f * MathF.Sin(5 * psi + 2.3f) + 2 * MathF.Sin(9 * psi + 0.4f);
    }

    /// <summary>Signed angular distance (degrees) of <paramref name="n"/> from the coast: positive on land, negative at sea.</summary>
    public static float Shore(Vector3 n)
        => MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(n), OceanCenter), -1, 1)) / Deg - OceanRadius(n);

    /// <summary>The designed terrain radius: the ground on land, a beach falling to the sea floor.</summary>
    public static float TerrainRadius(Vector3 n) => Ground - Depth * Smooth(0.8f, -5f, Shore(n));

    // ------------------------------------------------------------------ cube-sphere terrain patches

    private const int Cells = 6;                                    // patches per cube face edge (15 degrees each)

    private static readonly (Vector3 N, Vector3 U, Vector3 V)[] Faces =
    [
        // U x V = N keeps every patch's normal pointing outwards
        (Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ), (-Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY),
        (Vector3.UnitY, Vector3.UnitZ, Vector3.UnitX), (-Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ),
        (Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY), (-Vector3.UnitZ, Vector3.UnitY, Vector3.UnitX),
    ];

    /// <summary>Bezier control points of every terrain patch [face, i, j][16], interpolating the designed terrain.</summary>
    private static readonly Vector3[,,][] Patches = BuildPatches();

    private static Vector3 FaceDir(int f, float x, float y)
    {
        var (n, u, v) = Faces[f];
        // equiangular cube mapping: even patch sizes
        return Vector3.Normalize(n + MathF.Tan(x * MathF.PI / 4) * u + MathF.Tan(y * MathF.PI / 4) * v);
    }

    private static Vector3[,,][] BuildPatches()
    {
        var all = new Vector3[6, Cells, Cells][];
        for (int f = 0; f < 6; f++)
        for (int i = 0; i < Cells; i++)
        for (int j = 0; j < Cells; j++)
        {
            var q = new Vector3[16];
            for (int a = 0; a < 4; a++)
            for (int b = 0; b < 4; b++)
            {
                var d = FaceDir(f, -1 + 2 * (i + a / 3f) / Cells, -1 + 2 * (j + b / 3f) / Cells);
                q[b * 4 + a] = d * TerrainRadius(d);
            }
            all[f, i, j] = FitBezier(q);
        }
        return all;
    }

    /// <summary>Control points of the bicubic Bezier patch that passes through the 4 x 4 points <paramref name="q"/> at u, v = 0, 1/3, 2/3, 1.</summary>
    internal static Vector3[] FitBezier(Vector3[] q)
    {
        static (Vector3, Vector3, Vector3, Vector3) Fit(Vector3 q0, Vector3 q1, Vector3 q2, Vector3 q3)
            => (q0, (-5 * q0 + 18 * q1 - 9 * q2 + 2 * q3) / 6, (2 * q0 - 9 * q1 + 18 * q2 - 5 * q3) / 6, q3);

        var rows = new Vector3[16];
        for (int b = 0; b < 4; b++)
            (rows[b * 4], rows[b * 4 + 1], rows[b * 4 + 2], rows[b * 4 + 3]) = Fit(q[b * 4], q[b * 4 + 1], q[b * 4 + 2], q[b * 4 + 3]);
        var p = new Vector3[16];
        for (int a = 0; a < 4; a++)
            (p[a], p[4 + a], p[8 + a], p[12 + a]) = Fit(rows[a], rows[4 + a], rows[8 + a], rows[12 + a]);
        return p;
    }

    private static Vector3 EvalBezier(Vector3[] p, float u, float v)
    {
        Span<float> bu = [Cube(1 - u), 3 * u * Sq(1 - u), 3 * u * u * (1 - u), Cube(u)];
        Span<float> bv = [Cube(1 - v), 3 * v * Sq(1 - v), 3 * v * v * (1 - v), Cube(v)];
        var r = Vector3.Zero;
        for (int b = 0; b < 4; b++)
        for (int a = 0; a < 4; a++)
            r += p[b * 4 + a] * (bu[a] * bv[b]);
        return r;
    }

    /// <summary>Radius of the terrain surface as rendered, in the direction <paramref name="n"/>.</summary>
    public static float SurfaceRadius(Vector3 n)
    {
        n = Vector3.Normalize(n);
        int f = 0;
        float best = -2;
        for (int k = 0; k < 6; k++)
        {
            float d = Vector3.Dot(n, Faces[k].N);
            if (d > best) (best, f) = (d, k);
        }
        var (fn, fu, fv) = Faces[f];
        float x = MathF.Atan(Vector3.Dot(n, fu) / best) / (MathF.PI / 4), y = MathF.Atan(Vector3.Dot(n, fv) / best) / (MathF.PI / 4);
        float cx = (x + 1) / 2 * Cells, cy = (y + 1) / 2 * Cells;
        int i = Math.Clamp((int)cx, 0, Cells - 1), j = Math.Clamp((int)cy, 0, Cells - 1);
        return EvalBezier(Patches[f, i, j], cx - i, cy - j).Length();
    }

    /// <summary>
    /// The moon: the seabed sphere, the terrain patches and the sea. The sea is translucent, so
    /// <see cref="AddSea"/> is called later, after the opaque things.
    /// </summary>
    public static void AddTerrain(Scene scene)
    {
        // new names: nothing here is interpolated against the original moon of the close-ups
        scene.Add(Mat(new SphereElement("MesiacDno") { Radius = Seabed, QualityX = 48, QualityY = 32 },
            C(0.42f, 0.55f, 0.75f), "Povrch.bmp", ambient: 0.45f, emission: C(0.01f, 0.03f, 0.06f), spec: 0.05f, shine: 0.1f));

        for (int f = 0; f < 6; f++)
        for (int i = 0; i < Cells; i++)
        for (int j = 0; j < Cells; j++)
        {
            var p = Patches[f, i, j];
            if (p.All(c => c.Length() < Seabed + 0.02f)) continue;   // open sea: the seabed sphere is enough
            var mesh = Mesh($"Povrch{f}{i}{j}", 4, 4, (a, b) => p[b * 4 + a], grid: 8);
            scene.Add(Mat(mesh, C(0.86f, 0.93f, 0.9f), "Mach.bmp", ambient: 0.5f, emission: C(0.05f, 0.08f, 0.08f), spec: 0.08f, shine: 0.1f));
            var center = EvalBezier(p, 0.5f, 0.5f);
            Cullable[mesh.Name] = (center, MathF.Max((p[0] - center).Length(), (p[15] - center).Length()) + 0.3f);
        }
    }

    public static void AddSea(Scene scene, float t)
    {
        var sea = Mat(new SphereElement("More")
        {
            Radius = Sea, QualityX = 72, QualityY = 36,
            RotationPermanent = true, RotationAngleZ = 360, RotationPeriod = 900,
        }, C(0.3f, 0.62f, 0.82f, 0.8f), "Ocean.bmp", ambient: 0.45f, emission: C(0.03f, 0.1f, 0.15f), spec: 1f, shine: 0.85f);
        PointZ(sea, E2);                      // the texture's poles lie on land, far from the ocean
        scene.Add(sea);
    }

    // ================================================================== the route from the grove to the sea

    /// <summary>Start of the journey: the creature's place beyond the heart crystal.</summary>
    public static readonly Vector3 RouteStart = SurfaceNormal(31.4f, 7.5f);
    private static readonly Vector3 RouteF = Vector3.Normalize(OceanCenter - Vector3.Dot(OceanCenter, RouteStart) * RouteStart);
    private static readonly Vector3 RouteL = Vector3.Cross(RouteStart, RouteF);

    /// <summary>A point <paramref name="along"/> degrees from the start towards the ocean, <paramref name="side"/> degrees to the side.</summary>
    public static Vector3 RouteFrame(float along, float side)
    {
        var p = RouteStart * MathF.Cos(along * Deg) + RouteF * MathF.Sin(along * Deg);
        return Vector3.Normalize(p * MathF.Cos(side * Deg) + RouteL * MathF.Sin(side * Deg));
    }

    /// <summary>Waypoints (along, side) of the walk: a mushroom forest, a tree grove, a crystal field, the shore.</summary>
    private static readonly (float Along, float Side)[] Waypoints =
        [(0, 0), (3, 1.5f), (8, 3), (13, 1), (19, -2.5f), (25, -3), (31, -0.5f), (37, 2.5f), (43, 2), (49, -0.5f)];

    /// <summary>Angle along the route of the dry top of the beach (where the walk ends) and of the wading point.</summary>
    public static readonly float BeachAlong = FindBeach(0.25f), WadeAlong = FindBeach(-3.2f);

    private static float FindBeach(float shore)
    {
        float along = Waypoints[^1].Along;
        while (Shore(RouteFrame(along, Waypoints[^1].Side)) > shore) along += 0.05f;
        return along;
    }

    /// <summary>The route's waypoints: through the biomes, onto the beach, into the water.</summary>
    public static readonly Vector3[] RoutePoints =
    [
        .. Waypoints.Select(w => RouteFrame(w.Along, w.Side)),
        RouteFrame(BeachAlong, Waypoints[^1].Side),
        RouteFrame(WadeAlong, Waypoints[^1].Side),
        RouteFrame(WadeAlong + 3, Waypoints[^1].Side),
    ];

    public static readonly SpherePath RoutePath = new(RoutePoints);

    /// <summary>Length of the walk to the top of the beach (units on the ground).</summary>
    public static readonly float BeachDistance = RoutePath.DistanceAlong(RouteFrame(BeachAlong, Waypoints[^1].Side));

    /// <summary>Length of the walk to the wading point.</summary>
    public static readonly float WadeDistance = RoutePath.DistanceAlong(RouteFrame(WadeAlong, Waypoints[^1].Side));

    /// <summary>Length of the whole route, into the water.</summary>
    public static float RouteLength => RoutePath.Length;

    /// <summary>The point (unit vector) at distance <paramref name="d"/> along the route, and its direction of travel.</summary>
    public static (Vector3 N, Vector3 Dir) Route(float d) => RoutePath.At(d);

    /// <summary>Distance (units) from <paramref name="n"/> (a unit vector) to the route (between distances <paramref name="from"/> and <paramref name="to"/>).</summary>
    public static float DistanceToRoute(Vector3 n, float from = 0, float to = float.MaxValue) => RoutePath.DistanceTo(n, from, to);

    /// <summary>The moon's (a, s) angles of a unit vector, see <see cref="Kit.SurfaceNormal"/>.</summary>
    public static (float A, float S) Angles(Vector3 n)
    {
        n = Vector3.Normalize(n);
        return (MathF.Atan2(n.Z, n.Y) / Deg, MathF.Asin(Math.Clamp(n.X, -1, 1)) / Deg);
    }

    /// <summary>Heading (degrees about the anchor's +Y, 0 = +Z) of the direction <paramref name="dir"/> in the anchor frame at <paramref name="n"/>.</summary>
    public static float Heading(Vector3 n, Vector3 dir)
    {
        var (a, s) = Angles(n);
        var alongA = new Vector3(0, -MathF.Sin(a * Deg), MathF.Cos(a * Deg));
        var alongS = new Vector3(MathF.Cos(s * Deg), -MathF.Sin(s * Deg) * MathF.Cos(a * Deg), -MathF.Sin(s * Deg) * MathF.Sin(a * Deg));
        return MathF.Atan2(Vector3.Dot(dir, alongS), Vector3.Dot(dir, alongA)) / Deg;
    }

    // ================================================================== flora colonies

    internal enum Kind { Tree, Fungus, SmallFungi, Crystals, Fern, Flowers, Rock, Toadstools }

    internal sealed record Member(Kind Kind, float X, float Z, float Size, float Yaw, int Seed, Rgba Tint);

    internal sealed class Colony
    {
        public required int Id;
        public required float A, S;
        public required Vector3 Normal;
        public readonly List<Member> Members = [];
        public Vector3 Center;
        public float Radius;
        public bool HasCrystals => Members.Any(m => m.Kind == Kind.Crystals);
    }

    /// <summary>Bounding spheres (world) of the scenery that may be left out of key states where no camera can see it, by top-level name.</summary>
    public static readonly Dictionary<string, (Vector3 Center, float Radius)> Cullable = new();

    private static readonly Rgba[] FungusTints =
        [C(1f, 0.45f, 0.85f), C(1f, 0.62f, 0.3f), C(0.35f, 0.85f, 0.85f), C(0.7f, 0.45f, 1f), C(1f, 0.45f, 0.6f), C(0.75f, 0.95f, 0.4f)];

    private static readonly Rgba[] CrystalTints = [C(0.8f, 0.5f, 1f), C(0.5f, 0.9f, 1f), C(1f, 0.55f, 0.8f), C(0.55f, 0.7f, 1f)];

    private sealed record Blob(Vector3 Center, float Sigma, float Weight);

    private static readonly Blob[] TreeBlobs, FungusBlobs, CrystalBlobs;

    static Planet()
    {
        // biome centres: a few along the route (so the walk crosses each kind of land), the rest at random
        TreeBlobs = [new(RouteFrame(26, -6), 11, 1.3f), .. RandomBlobs(8, 900, 12, 20)];
        FungusBlobs = [new(RouteFrame(10, 6), 10, 1.3f), .. RandomBlobs(9, 910, 10, 18)];
        CrystalBlobs = [new(RouteFrame(41, 5), 9, 1.2f), .. RandomBlobs(6, 920, 8, 14)];
    }

    private static IEnumerable<Blob> RandomBlobs(int count, int seed, float minSigma, float maxSigma)
    {
        for (int k = 0, found = 0; found < count && k < 500; k++)
        {
            var n = RandomDir(k, seed);
            if (Shore(n) < 6) continue;
            found++;
            yield return new Blob(n, Lerp(minSigma, maxSigma, Rnd(k, seed + 3)), 0.7f + 0.5f * Rnd(k, seed + 4));
        }
    }

    private static Vector3 RandomDir(int k, int seed)
    {
        float z = Rnd(k, seed) * 2 - 1, phi = Rnd(k, seed + 1) * MathF.PI * 2, r = MathF.Sqrt(1 - z * z);
        return new Vector3(r * MathF.Cos(phi), z, r * MathF.Sin(phi));
    }

    private static float Field(Blob[] blobs, Vector3 n)
    {
        float sum = 0;
        foreach (var b in blobs)
        {
            float ang = MathF.Acos(Math.Clamp(Vector3.Dot(b.Center, n), -1, 1)) / Deg;
            sum += b.Weight * MathF.Exp(-Sq(ang / b.Sigma));
        }
        return sum;
    }

    /// <summary>Places where nothing new may grow: the 1998-style scenes of the first part, and plants already there.</summary>
    internal static readonly List<(Vector3 P, float R)> Keepout = [];

    /// <summary>Line-of-sight checks: (camera, subject) pairs sampled from the whole film; plants in the way are not planted.</summary>
    internal static readonly List<(Vector3 From, Vector3 To)> Sightlines = [];

    private static List<Colony>? _colonies;

    public static IReadOnlyList<Colony> Colonies => _colonies ?? throw new InvalidOperationException("Call Planet.GrowFlora first.");

    public static bool Grown => _colonies != null;

    /// <summary>Distributes the colonies. Call after <see cref="Keepout"/> and <see cref="Sightlines"/> are filled.</summary>
    public static void GrowFlora()
    {
        var colonies = new List<Colony>();
        var centers = new List<Vector3>();
        const int candidates = 3000;
        for (int k = 0; k < candidates; k++)
        {
            // a Fibonacci spiral visits the whole sphere evenly; the jitter hides its pattern
            float z = 1 - 2 * (k + 0.5f) / candidates, r = MathF.Sqrt(1 - z * z), phi = k * 2.39996323f;
            var n = Vector3.Normalize(new Vector3(r * MathF.Cos(phi), z, r * MathF.Sin(phi))
                                      + 0.02f * new Vector3(Rnd(k, 1) - 0.5f, Rnd(k, 2) - 0.5f, Rnd(k, 3) - 0.5f));
            if (Shore(n) < 1.2f) continue;
            float trees = Field(TreeBlobs, n), fungi = Field(FungusBlobs, n), crystals = Field(CrystalBlobs, n);
            float density = Math.Clamp(0.3f + trees + fungi + crystals, 0, 1.6f);
            if (Rnd(k, 7) > 0.35f + 0.6f * density) continue;
            float spacing = Lerp(3.0f, 1.9f, Clamp01(density / 1.2f)) / Ground;   // radians
            if (centers.Any(c => Vector3.Dot(c, n) > MathF.Cos(spacing))) continue;

            var (a, s) = Angles(n);
            var colony = new Colony { Id = colonies.Count, A = a, S = s, Normal = n };
            Populate(colony, k, density, trees, fungi, crystals);
            if (colony.Members.Count == 0) continue;
            Bound(colony);
            colonies.Add(colony);
            centers.Add(n);
        }
        _colonies = colonies;
        foreach (var c in colonies)
        {
            Cullable[$"Kol{c.Id}"] = (c.Center, c.Radius);
            if (c.HasCrystals) Cullable[$"KolK{c.Id}"] = (c.Center, c.Radius);
        }
        LayCarpets();
    }

    // ------------------------------------------------------------------ undergrowth carpets

    /// <summary>A patch of dense low undergrowth lying on the ground: Bezier control points, tint, bounding sphere.</summary>
    internal sealed record Carpet(int Id, Vector3[] Points, Rgba Tint);

    private static List<Carpet> _carpets = [];

    public static IReadOnlyList<Carpet> Carpets => _carpets;

    /// <summary>
    /// Covers the land with overlapping carpets of undergrowth, so the moon looks overgrown also between
    /// the colonies. Their tint follows the biomes: teal under trees, magenta among fungi, blue near crystals.
    /// </summary>
    private static void LayCarpets()
    {
        var carpets = new List<Carpet>();
        const int n = 240;
        const float half = 1.9f;
        for (int k = 0; k < n; k++)
        {
            float z = 1 - 2 * (k + 0.5f) / n, r = MathF.Sqrt(1 - z * z), phi = k * 2.39996323f + 0.7f;
            var c = Vector3.Normalize(new Vector3(r * MathF.Cos(phi), z, r * MathF.Sin(phi))
                                      + 0.05f * new Vector3(Rnd(k, 31) - 0.5f, Rnd(k, 32) - 0.5f, Rnd(k, 33) - 0.5f));
            if (Shore(c) < 7) continue;                                      // a strip of bare moss along the coast
            if (DistanceToRoute(c) < 1.2f) continue;                         // the trail stays visible
            if (Keepout.Any(kp => kp.R > 1.5f && Vector3.Distance(kp.P, c * Ground) < kp.R + 0.8f)) continue;

            var e1 = Vector3.Normalize(Vector3.Cross(c, MathF.Abs(c.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX));
            var e2 = Vector3.Cross(c, e1);
            float turn = Rnd(k, 34) * MathF.PI;
            (e1, e2) = (e1 * MathF.Cos(turn) + e2 * MathF.Sin(turn), e2 * MathF.Cos(turn) - e1 * MathF.Sin(turn));
            float lift = 0.035f + 0.012f * (k % 3), size = half * (0.85f + 0.3f * Rnd(k, 35));
            var q = new Vector3[16];
            for (int b = 0; b < 4; b++)
            for (int a = 0; a < 4; a++)
            {
                var d = Vector3.Normalize(c * Ground + e1 * (size * (-1 + 2 * a / 3f)) + e2 * (size * (-1 + 2 * b / 3f)));
                q[b * 4 + a] = d * (SurfaceRadius(d) + lift);
            }

            float trees = Field(TreeBlobs, c), fungi = Field(FungusBlobs, c), crystals = Field(CrystalBlobs, c), plain = 0.35f;
            float sum = trees + fungi + crystals + plain, vary = 0.9f + 0.2f * Rnd(k, 36);
            var tint = C(
                (0.55f * plain + 0.3f * trees + 0.95f * fungi + 0.55f * crystals) / sum * vary,
                (0.85f * plain + 0.82f * trees + 0.5f * fungi + 0.65f * crystals) / sum * vary,
                (0.6f * plain + 0.62f * trees + 0.88f * fungi + 1f * crystals) / sum * vary);
            var carpet = new Carpet(carpets.Count, FitBezier(q), tint);
            carpets.Add(carpet);
            Cullable[$"Podrast{carpet.Id}"] = (c * Ground, size * 1.45f);
        }
        _carpets = carpets;
    }

    private static void AddCarpets(Scene scene)
    {
        foreach (var c in Carpets)
        {
            var mesh = Mesh($"Podrast{c.Id}", 4, 4, (a, b) => c.Points[b * 4 + a], grid: 4);
            scene.Add(Mat(mesh, c.Tint, "Podrast.bmp", ambient: 0.55f, emission: Mul(c.Tint, 0.1f), spec: 0.05f, shine: 0.1f));
        }
    }

    private static void Populate(Colony c, int seed, float density, float trees, float fungi, float crystals)
    {
        int count = (int)MathF.Round(Lerp(2.5f, 8.5f, Clamp01(density / 1.3f)) + Rnd(seed, 11) * 1.5f);
        float sum = trees + fungi + crystals + 0.25f;
        var frame = Anchor("tmp", c.A, c.S);
        var placed = new List<(float X, float Z, float R)>();
        for (int m = 0, tries = 0; m < count && tries < 40; tries++)
        {
            int ms = seed * 64 + tries;
            float ang = Rnd(ms, 21) * MathF.PI * 2, rad = 2.2f * MathF.Sqrt(Rnd(ms, 22));
            float x = rad * MathF.Cos(ang), z = rad * MathF.Sin(ang);

            // what grows here: the biome fields decide, undergrowth fills in
            float pick = Rnd(ms, 23) * sum;
            Kind kind;
            if ((pick -= trees) < 0) kind = Kind.Tree;
            else if ((pick -= fungi) < 0) kind = Rnd(ms, 24) < 0.72f ? Kind.Fungus : Kind.SmallFungi;
            else if ((pick -= crystals) < 0) kind = Kind.Crystals;
            else kind = (Kind)((int)Kind.Fern + (int)(Rnd(ms, 25) * 4));

            float size = 0.75f + 0.5f * Rnd(ms, 26);
            float footprint = kind switch { Kind.Tree => 1.05f, Kind.Fungus => 0.75f * size, Kind.Crystals => 0.6f, _ => 0.4f };
            if (placed.Any(p => Sq(p.X - x) + Sq(p.Z - z) < Sq(p.R + footprint))) continue;

            var world = World(frame, new Vector3(x, G(x, z), z));
            var nrm = Vector3.Normalize(world);
            if (Shore(nrm) < (kind is Kind.Rock or Kind.Flowers ? 0.9f : 1.6f)) continue;
            float tall = kind switch { Kind.Tree => 4.2f, Kind.Fungus => 2.2f * size, Kind.Crystals => 1.3f, Kind.SmallFungi => 0.7f, _ => 0.6f };
            float corridor = tall > 1 ? 1.5f + footprint : 0.75f;
            if (DistanceToRoute(nrm) < corridor) continue;
            if (Keepout.Any(k => Vector3.Distance(k.P, world) < k.R + footprint)) continue;
            if (Blocks(world, nrm, footprint, tall)) continue;

            var tint = kind == Kind.Crystals ? CrystalTints[(int)(Rnd(ms, 27) * CrystalTints.Length)] : FungusTints[(int)(Rnd(ms, 27) * FungusTints.Length)];
            c.Members.Add(new Member(kind, x, z, size, Rnd(ms, 28) * 360, ms, tint));
            placed.Add((x, z, footprint));
            m++;
        }
    }

    /// <summary>True when a plant at <paramref name="foot"/> would stand in a sampled line of sight of the film.</summary>
    private static bool Blocks(Vector3 foot, Vector3 up, float radius, float height)
    {
        foreach (var (from, to) in Sightlines)
        {
            // the plant as a capsule from its foot to its top, tested against the segment camera -> subject
            if (SegmentDistance(from, to, foot, foot + up * height) < radius + 0.25f) return true;
        }
        return false;
    }

    private static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
    {
        // closest distance between two segments (Ericson, Real-Time Collision Detection 5.1.9)
        Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
        float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
        float s, t;
        if (a <= 1e-9f && e <= 1e-9f) return r.Length();
        if (a <= 1e-9f) { s = 0; t = Clamp01(f / e); }
        else
        {
            float c = Vector3.Dot(d1, r);
            if (e <= 1e-9f) { t = 0; s = Clamp01(-c / a); }
            else
            {
                float b = Vector3.Dot(d1, d2), denom = a * e - b * b;
                s = denom != 0 ? Clamp01((b * f - c * e) / denom) : 0;
                t = (b * s + f) / e;
                if (t < 0) { t = 0; s = Clamp01(-c / a); }
                else if (t > 1) { t = 1; s = Clamp01((b - c) / a); }
            }
        }
        return Vector3.Distance(p1 + d1 * s, p2 + d2 * t);
    }

    private static void Bound(Colony c)
    {
        var frame = Anchor("tmp", c.A, c.S);
        var top = World(frame, new Vector3(0, 2f, 0));
        c.Center = top;
        float radius = 0;
        foreach (var m in c.Members)
        {
            float tall = m.Kind switch { Kind.Tree => 4.3f, Kind.Fungus => 3f, Kind.Crystals => 1.4f, _ => 0.8f };
            radius = MathF.Max(radius, MathF.Sqrt(m.X * m.X + m.Z * m.Z) + 1.3f);
            radius = MathF.Max(radius, MathF.Abs(tall - 2f) + 0.5f);
        }
        c.Radius = MathF.Sqrt(radius * radius + 4);
    }

    // ------------------------------------------------------------------ building the colonies into a key state

    /// <summary>The opaque part of every colony (crystals come later, with the other translucent things).</summary>
    public static void AddFlora(Scene scene, float t)
    {
        AddCarpets(scene);
        foreach (var c in Colonies)
        {
            var anchor = Anchor($"Kol{c.Id}", c.A, c.S);
            for (int i = 0; i < c.Members.Count; i++)
            {
                var m = c.Members[i];
                string id = $"K{c.Id}m{i}";
                switch (m.Kind)
                {
                    case Kind.Tree: Tree(anchor, id, m, t); break;
                    case Kind.Fungus: Fungus(anchor, id, m, m.X, m.Z, m.Size, t, spores: true); break;
                    case Kind.SmallFungi:
                        for (int k = 0; k < 3; k++)
                        {
                            float ang = (m.Yaw + 120 * k) * Deg, r = 0.25f + 0.1f * k;
                            Fungus(anchor, id + "s" + k, m with { Tint = FungusTints[(m.Seed + k) % FungusTints.Length] },
                                m.X + r * MathF.Cos(ang), m.Z + r * MathF.Sin(ang), 0.22f + 0.08f * k, t, spores: false);
                        }
                        break;
                    case Kind.Fern: Fern(anchor, id, m); break;
                    case Kind.Flowers: Flowers(anchor, id, m); break;
                    case Kind.Rock: Rock(anchor, id, m); break;
                    case Kind.Toadstools: Toadstools(anchor, id, m); break;
                }
            }
            if (anchor.Children.Count > 0) scene.Add(anchor);
        }
    }

    /// <summary>The crystals of the colonies (translucent: drawn after the opaque scenery).</summary>
    public static void AddCrystals(Scene scene, float t)
    {
        foreach (var c in Colonies)
        {
            if (!c.HasCrystals) continue;
            var anchor = Anchor($"KolK{c.Id}", c.A, c.S);
            for (int i = 0; i < c.Members.Count; i++)
                if (c.Members[i].Kind == Kind.Crystals) Crystals(anchor, $"K{c.Id}m{i}", c.Members[i], t);
            scene.Add(anchor);
        }
    }

    /// <summary>Unit vector of the local surface normal at (x, z) of an anchor's tangent frame, in that frame.</summary>
    private static Vector3 LocalUp(float x, float z) => Vector3.Normalize(new Vector3(x, G(x, z) + Ground, z));

    private static Vector3 Foot(float x, float z) => new(x, G(x, z), z);

    private static void Tree(GroupElement parent, string id, Member m, float t)
    {
        var up = LocalUp(m.X, m.Z);
        var foot = Foot(m.X, m.Z) - up * 0.1f;
        float h = 2.1f + 1.2f * Rnd(m.Seed, 41);
        parent.Add(Place(PointZ(Mat(new ConeElement(id + "t") { BaseRadius = 0.2f, TopRadius = 0.055f, Height = h, QualityX = 8, QualityY = 1 },
            C(0.75f, 0.7f, 0.8f), "TmaveDrevo.bmp", ambient: 0.6f, emission: C(0.03f, 0.02f, 0.05f)), up), foot));
        int whorls = 2 + (int)(Rnd(m.Seed, 42) * 2);
        for (int k = 0; k < whorls; k++)
        {
            float at = h * (0.48f + 0.17f * k), radius = 1.05f - 0.22f * k;
            var disk = Mat(new DiskElement(id + "w" + k)
            {
                InnerRadius = 0.1, OuterRadius = radius, SweepAngle = 360, QualityX = 16, QualityY = 1,
                RotationPermanent = true, RotationAngleZ = (k + m.Seed) % 2 == 0 ? 360 : -360, RotationPeriod = 6 + 2 * k + 3 * Rnd(m.Seed, 43 + k),
            }, C(0.9f, 1f, 0.95f), "Praslen.bmp", ambient: 0.6f, emission: C(0.1f, 0.16f, 0.16f), spec: 0.2f, shine: 0.2f);
            PointZ(disk, up);
            disk.Gamma = m.Yaw + 51 * k;
            parent.Add(Place(disk, foot + up * at));
        }
        var fruit = Glow(new SphereElement(id + "f") { Radius = 0.12f, QualityX = 8, QualityY = 5 },
            Rnd(m.Seed, 44) < 0.5f ? C(0.95f, 0.98f, 0.55f) : C(0.6f, 0.95f, 1f));
        parent.Add(Place(fruit, foot + up * (h + 0.1f + 0.06f * Wave(t, 2.4f, Rnd(m.Seed, 45)))));
    }

    private static void Fungus(GroupElement parent, string id, Member m, float x, float z, float size, float t, bool spores)
    {
        var up = LocalUp(x, z);
        var foot = Foot(x, z) - up * 0.04f;
        float h = (spores ? 0.9f + 1.1f * Rnd(m.Seed, 51) : 1f) * size * (spores ? 1 : 1.6f);
        float cap = (spores ? 0.38f + 0.18f * Rnd(m.Seed, 52) : 0.42f) * h;
        float pulse = (spores ? 0.25f : 0.12f) + 0.08f * Wave(t, 3.2f, Rnd(m.Seed, 53));
        parent.Add(Place(PointZ(Mat(new ConeElement(id + "n") { BaseRadius = 0.1f * h, TopRadius = 0.065f * h, Height = h, QualityX = 7, QualityY = 1 },
            C(0.92f, 0.95f, 0.88f), "Stonka.bmp", ambient: 0.6f, emission: Mul(m.Tint, 0.08f)), up), foot));
        parent.Add(Place(PointZ(Mat(new ConeElement(id + "k") { BaseRadius = cap, TopRadius = 0.05f, Height = 0.45f * cap, QualityX = 12, QualityY = 1 },
            m.Tint, "Klobuk.bmp", ambient: 0.55f, emission: Mul(m.Tint, pulse), spec: 0.4f, shine: 0.3f), up), foot + up * (h - 0.06f)));
        if (spores)
            parent.Add(Place(PointZ(Mat(new DiskElement(id + "l") { InnerRadius = 0.03, OuterRadius = cap * 0.97f, SweepAngle = 360, QualityX = 12, QualityY = 1 },
                Mul(m.Tint, 0.9f), "Lupene.bmp", ambient: 0.6f, emission: Mul(m.Tint, 0.2f)), up), foot + up * (h - 0.05f)));
    }

    private static void Crystals(GroupElement parent, string id, Member m, float t)
    {
        var up = LocalUp(m.X, m.Z);
        var foot = Foot(m.X, m.Z) - up * 0.06f;
        float pulse = 0.3f + 0.1f * Wave(t, 3f, Rnd(m.Seed, 61));
        int count = 3 + (int)(Rnd(m.Seed, 62) * 3);
        // a frame around the local up: two perpendicular directions to lean the spikes
        var side = Vector3.Normalize(Vector3.Cross(up, Vector3.UnitX));
        var side2 = Vector3.Cross(up, side);
        for (int k = 0; k < count; k++)
        {
            float lean = (k == 0 ? 6 : 18 + 22 * Rnd(m.Seed, 63 + k)) * Deg, dir = (m.Yaw + k * 360f / count) * Deg;
            var axis = Vector3.Normalize(up * MathF.Cos(lean) + (side * MathF.Cos(dir) + side2 * MathF.Sin(dir)) * MathF.Sin(lean));
            float h = (k == 0 ? 1.1f : 0.5f + 0.45f * Rnd(m.Seed, 70 + k)) * m.Size, r = (k == 0 ? 0.16f : 0.09f) * m.Size;
            var color = k % 3 == 2 ? CrystalTints[(m.Seed + k) % CrystalTints.Length] : m.Tint;
            var spike = Glass(Mat(new ConeElement($"{id}c{k}") { BaseRadius = r, TopRadius = 0, Height = h, QualityX = 6, QualityY = 1 },
                WithAlpha(color, 0.8f), "KrystalLum.bmp", ambient: 0.4f, emission: Mul(color, pulse), spec: 1f, shine: 0.85f));
            parent.Add(Place(PointZ(spike, axis), foot));
        }
    }

    private static void Fern(GroupElement parent, string id, Member m)
    {
        var fern = new GroupElement(id);
        Kit.Place(fern, Foot(m.X, m.Z) with { Y = G(m.X, m.Z) - 0.02f });
        float size = 0.7f + 0.5f * m.Size;
        float[] rise = [0f, 0.5f, 0.72f, 0.55f], reach = [0f, 0.3f, 0.66f, 0.98f], width = [0.04f, 0.21f, 0.17f, 0.04f];
        for (int k = 0; k < 3; k++)
        {
            float len = size * (0.8f + 0.35f * Rnd(m.Seed, 81 + k));
            var frond = Mesh($"{id}L{k}", 4, 3, (i, j) => new Vector3(len * width[i] * (j - 1), len * rise[i] + (j == 1 ? 0.04f * len : 0), len * reach[i]), grid: 3);
            frond.Beta = m.Yaw + k * 120;
            fern.Add(Mat(frond, C(0.78f, 0.97f, 0.92f), "Paprad.bmp", ambient: 0.6f, emission: C(0.07f, 0.13f, 0.13f), spec: 0.1f));
        }
        parent.Add(fern);
    }

    private static void Flowers(GroupElement parent, string id, Member m)
    {
        float size = 0.32f + 0.15f * m.Size;
        for (int k = 0; k < 2; k++)
        {
            var leaf = Mat(new RectangleElement($"{id}{k}") { SizeX = size, SizeY = size * 1.1f, Beta = m.Yaw + k * 90 },
                C(0.95f, 0.95f, 1f), "Horec.bmp", ambient: 0.65f, emission: C(0.14f, 0.14f, 0.24f), spec: 0.05f);
            parent.Add(Place(leaf, Foot(m.X, m.Z) + new Vector3(0, size * 0.55f, 0)));
        }
    }

    private static void Toadstools(GroupElement parent, string id, Member m)
    {
        float size = 0.38f + 0.2f * m.Size;
        for (int k = 0; k < 2; k++)
        {
            var r = Mat(new RectangleElement($"{id}{k}") { SizeX = size, SizeY = size, Beta = m.Yaw + k * 90 },
                C(0.95f, 0.92f, 0.9f), "Hriby.bmp", ambient: 0.65f, emission: C(0.1f, 0.08f, 0.08f), spec: 0.05f);
            parent.Add(Place(r, Foot(m.X, m.Z) + new Vector3(0, size * 0.48f, 0)));
        }
    }

    private static void Rock(GroupElement parent, string id, Member m)
    {
        float size = 0.16f + 0.2f * m.Size * Rnd(m.Seed, 91);
        string tex = (m.Seed % 3) switch { 0 => "DrsnyKamen.bmp", 1 => "SivyKamen.bmp", _ => "Pieskovec.bmp" };
        var rock = Mat(new SphereElement(id) { Radius = size, QualityX = 7, QualityY = 5, Alpha = 30 * Rnd(m.Seed, 92), Beta = m.Yaw },
            C(0.8f, 0.78f, 0.82f), tex, ambient: 0.55f, spec: 0.1f);
        parent.Add(Place(rock, Foot(m.X, m.Z) - new Vector3(0, size * 0.35f, 0)));
    }

    private static T Place<T>(T e, Vector3 p) where T : Element
    {
        Kit.Place(e, p);
        return e;
    }

    private static float Sq(float x) => x * x;
    private static float Cube(float x) => x * x * x;
}


