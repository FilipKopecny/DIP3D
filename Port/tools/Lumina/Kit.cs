using System.Numerics;
using Overbryce.Core;
using Overbryce.Core.Model;

namespace Lumina;

/// <summary>Small helpers for building Overbryce elements: materials, placement on the moon, timing curves.</summary>
internal static class Kit
{
    /// <summary>Radius of the moon sphere (a little below the terrain patches, which approximate a sphere from inside).</summary>
    public const float R = 9.96f;

    /// <summary>Radius of the walkable surface (the detailed terrain patches lie just above the sphere).</summary>
    public const float Ground = 10.03f;

    public const float Deg = MathF.PI / 180f;

    // ------------------------------------------------------------------ colours and materials

    public static Rgba C(float r, float g, float b, float a = 1f) => new(r, g, b, a);
    public static Rgba Gray(float v, float a = 1f) => new(v, v, v, a);
    public static Rgba Mul(Rgba c, float k) => new(c.R * k, c.G * k, c.B * k, c.A);
    public static Rgba WithAlpha(Rgba c, float a) => new(c.R, c.G, c.B, a);
    public static readonly Rgba Black = new(0, 0, 0, 1);

    /// <summary>Lit material. <paramref name="ambient"/> scales the diffuse colour for the ambient term.</summary>
    public static T Mat<T>(T e, Rgba diffuse, string? tex = null, float ambient = 0.6f, Rgba? emission = null,
        float spec = 0.25f, float shine = 0.15f) where T : Element
    {
        e.Diffuse = diffuse;
        e.Ambient = new Rgba(diffuse.R * ambient, diffuse.G * ambient, diffuse.B * ambient, 1);
        e.Emission = emission ?? Black;
        e.Specular = Gray(spec);
        e.Shininess = shine;
        e.Texture = tex;
        return e;
    }

    /// <summary>Self-lit material: only the emission colour (times the texture) is visible.</summary>
    public static T Glow<T>(T e, Rgba color, float alpha = 1f, string? tex = null) where T : Element
    {
        e.Diffuse = new Rgba(0, 0, 0, alpha);
        e.Ambient = Black;
        e.Specular = Black;
        e.Emission = new Rgba(color.R, color.G, color.B, 1);
        e.Shininess = 0;
        e.Texture = tex;
        return e;
    }

    public static LightElement Light(string name, Vector3 pos, Rgba diffuse, Rgba? ambient = null, Vector3? dir = null,
        float cutoff = 180, float exponent = 0, float linear = 0, float quadratic = 0)
    {
        var l = new LightElement(name)
        {
            X = pos.X, Y = pos.Y, Z = pos.Z,
            Diffuse = diffuse,
            Specular = diffuse,
            Ambient = ambient ?? Black,
            Emission = Black,
            SpotCutoff = cutoff,
            SpotExponent = exponent,
            ConstantAttenuation = 1,
            LinearAttenuation = linear,
            QuadraticAttenuation = quadratic,
        };
        if (dir is { } d)
        {
            // for lights alpha/beta/gamma are the spot direction vector
            l.Alpha = d.X; l.Beta = d.Y; l.Gamma = d.Z;
        }
        return l;
    }

    // ------------------------------------------------------------------ placement

    /// <summary>
    /// A group standing on the moon at "along" angle <paramref name="a"/> and "side" angle <paramref name="s"/>
    /// (degrees). Its local +Y is the surface normal, +Z points along increasing a, +X along increasing s.
    /// A group rotates before it translates, so the translation along its own Y lifts it onto the surface.
    /// </summary>
    public static GroupElement Anchor(string name, float a, float s, float lift = 0f)
        => new(name) { Alpha = a, Gamma = -s, Y = Ground + lift };

    public static Vector3 SurfaceNormal(float a, float s) => new(
        MathF.Sin(s * Deg), MathF.Cos(s * Deg) * MathF.Cos(a * Deg), MathF.Cos(s * Deg) * MathF.Sin(a * Deg));

    /// <summary>Height of the curved ground below the point (x, z) of an anchor's tangent frame.</summary>
    public static float G(float x, float z) => MathF.Sqrt(MathF.Max(0, Ground * Ground - x * x - z * z)) - Ground;

    /// <summary>Turns an element whose axis is +Z (cone, disk, rectangle) so that the axis points along <paramref name="d"/>.</summary>
    public static T PointZ<T>(T e, Vector3 d) where T : Element
    {
        d = Vector3.Normalize(d);
        e.Beta = MathF.Asin(Math.Clamp(d.X, -1, 1)) / Deg;
        e.Alpha = MathF.Atan2(-d.Y, d.Z) / Deg;
        return e;
    }

    public static Vector3 Pos(Element e) => new(e.X, e.Y, e.Z);

    public static void Place(Element e, Vector3 p)
    {
        e.X = p.X; e.Y = p.Y; e.Z = p.Z;
    }

    /// <summary>World position of a point given in the local frame of <paramref name="e"/>.</summary>
    public static Vector3 World(Element e, Vector3 local = default) => Vector3.Transform(local, Transforms.World(e));

    /// <summary>World point expressed in the local frame of <paramref name="frame"/>.</summary>
    public static Vector3 ToLocal(GroupElement frame, Vector3 world)
    {
        Matrix4x4.Invert(Transforms.World(frame), out var inv);
        return Vector3.Transform(world, inv);
    }

    /// <summary>Scales an element (and a group's whole subtree) by <paramref name="k"/>: positions and shape sizes; angles stay.</summary>
    public static void Scale(Element e, float k)
    {
        e.X *= k; e.Y *= k; e.Z *= k;
        switch (e)
        {
            case SphereElement s: s.Radius *= k; break;
            case ConeElement c: c.BaseRadius *= k; c.TopRadius *= k; c.Height *= k; break;
            case DiskElement d: d.InnerRadius *= k; d.OuterRadius *= k; break;
            case BoxElement b: b.SizeX *= k; b.SizeY *= k; b.SizeZ *= k; break;
            case RectangleElement r: r.SizeX *= k; r.SizeY *= k; break;
            case TorusElement t: t.TubeRadius *= k; t.RingRadius *= k; break;
            case MeshElement m: for (int i = 0; i < m.Points.Length; i++) m.Points[i] *= k; break;
            case GroupElement g: foreach (var child in g.Children) Scale(child, k); break;
        }
    }

    // continuity of angles computed with atan2 (per name, the key states are built in time order)
    private static readonly Dictionary<string, float> LastAngle = new();

    public static float Unwrap(string key, float deg)
    {
        if (LastAngle.TryGetValue(key, out float prev))
        {
            while (deg - prev > 180) deg -= 360;
            while (deg - prev < -180) deg += 360;
        }
        LastAngle[key] = deg;
        return deg;
    }

    /// <summary>Aims a camera at a point given in the camera's parent frame (no roll).</summary>
    public static void Aim(CameraElement cam, Vector3 target)
    {
        var d = Vector3.Normalize(target - Pos(cam));
        cam.Alpha = MathF.Asin(Math.Clamp(d.Y, -1, 1)) / Deg;
        cam.Beta = Unwrap(cam.Name + ".beta", MathF.Atan2(-d.X, -d.Z) / Deg);
        cam.Gamma = 0;
    }

    /// <summary>Bezier mesh from a nu x nv grid of control points; u runs along i, v along j.</summary>
    public static MeshElement Mesh(string name, int nu, int nv, Func<int, int, Vector3> p, int grid = 8)
    {
        var pts = new float[nu * nv * 3];
        for (int j = 0; j < nv; j++)
        for (int i = 0; i < nu; i++)
        {
            var v = p(i, j);
            int k = (j * nu + i) * 3;
            pts[k] = v.X; pts[k + 1] = v.Y; pts[k + 2] = v.Z;
        }
        return new MeshElement(name, pts, nu, nv) { QualityX = grid, QualityY = grid };
    }

    // ------------------------------------------------------------------ timing

    public static float Clamp01(float x) => Math.Clamp(x, 0f, 1f);
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * t;

    /// <summary>Smoothstep from 0 at <paramref name="e0"/> to 1 at <paramref name="e1"/>.</summary>
    public static float Smooth(float e0, float e1, float x)
    {
        float t = Clamp01((x - e0) / (e1 - e0));
        return t * t * (3 - 2 * t);
    }

    public static float Ramp(float t, float t0, float t1, float a, float b) => Lerp(a, b, Smooth(t0, t1, t));

    /// <summary>0 before t0, rising to 1 at t1, falling from t2 to 0 at t3.</summary>
    public static float Window(float t, float t0, float t1, float t2, float t3) => Smooth(t0, t1, t) * (1 - Smooth(t2, t3, t));

    public static float Wave(float t, float period, float phase = 0) => MathF.Sin(2 * MathF.PI * (t / period + phase));

    /// <summary>Deterministic pseudo random number in [0, 1).</summary>
    public static float Rnd(int a, int b = 0)
    {
        uint h = (uint)(a * 374761393 + b * 668265263 + 1442695041);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFFFF) / 16777216f;
    }
}
