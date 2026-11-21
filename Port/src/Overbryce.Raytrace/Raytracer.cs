using System.Diagnostics;
using System.Numerics;
using Overbryce.Core.Model;

namespace Overbryce.Raytrace;

/// <summary>What and how to trace.</summary>
public sealed class RaytraceOptions
{
    public RaytraceSettings Settings { get; init; } = new();

    /// <summary>Exposure, bloom and the tone curve (shared with the modern renderer).</summary>
    public ModernSettings Modern { get; init; } = new();

    /// <summary>
    /// The 1998 look through the tracer: OpenGL lighting clamped to 0..1, no glow, shadows, reflections
    /// or refraction; only anti-aliasing and depth-correct transparency are added.
    /// </summary>
    public bool Classic { get; init; }

    /// <summary>Alpha blending as in the OpenGL views (white texels and alpha below 1 are see-through).</summary>
    public bool AlphaBlend { get; init; } = true;

    /// <summary>The texture of an element by its file name (null when it cannot be loaded).</summary>
    public Func<string, TextureImage?> Textures { get; init; } = _ => null;

    /// <summary>Varies the noise between frames (the frame number); the same seed gives the same picture.</summary>
    public uint Seed { get; init; }

    /// <summary>At most this many threads (0 = all processors).</summary>
    public int Threads { get; init; }
}

/// <summary>A finished picture: 32-bit BGRA, the top row first (as a Windows bitmap wants it).</summary>
public sealed record RaytraceImage(int Width, int Height, byte[] Bgra);

/// <summary>Numbers about a traced frame.</summary>
public sealed record RaytraceStats(int Elements, int Triangles, int Samples, TimeSpan Prepare, TimeSpan Trace);

/// <summary>
/// The offline ray tracer (new in Overbryce 2.2). Keeps the geometry of equal shapes between frames, so
/// one instance is meant to render the frames of one export.
/// </summary>
public sealed class Raytracer
{
    private readonly GeometryCache _cache = new();

    /// <summary>Compiles a frame; the job then traces samples into its film.</summary>
    /// <param name="steps">The animation evaluated within the open shutter (one step without motion blur).</param>
    public RaytraceJob Prepare(IReadOnlyList<FrameStep> steps, int width, int height, RaytraceOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        var watch = Stopwatch.StartNew();
        CompiledScene scene;
        lock (_cache) scene = SceneCompiler.Compile(steps, options, _cache);
        return new RaytraceJob(scene, width, height, options, watch.Elapsed);
    }

    /// <summary>Traces a whole frame with the number of samples of the settings.</summary>
    public RaytraceImage Render(IReadOnlyList<FrameStep> steps, int width, int height, RaytraceOptions options,
        IProgress<float>? progress = null, CancellationToken cancel = default)
    {
        var job = Prepare(steps, width, height, options);
        job.Trace(options.Settings.Samples, progress, cancel);
        return job.Resolve();
    }
}

/// <summary>One frame being traced. Samples can be added in passes (the editor preview refines the picture).</summary>
public sealed class RaytraceJob
{
    private const int Tile = 16;

    private readonly CompiledScene _scene;
    private readonly RaytraceOptions _options;
    private readonly Vector3[] _sum;
    private readonly TimeSpan _prepare;
    private TimeSpan _trace;

    internal RaytraceJob(CompiledScene scene, int width, int height, RaytraceOptions options, TimeSpan prepare)
    {
        _scene = scene;
        _options = options;
        Width = width;
        Height = height;
        _sum = new Vector3[width * height];
        _prepare = prepare;
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Samples per pixel traced so far.</summary>
    public int Samples { get; private set; }

    public RaytraceStats Stats => new(_scene.Instances.Length, _scene.TriangleCount, Samples, _prepare, _trace);

    /// <summary>Adds <paramref name="samples"/> samples to every pixel.</summary>
    public void Trace(int samples, IProgress<float>? progress = null, CancellationToken cancel = default)
    {
        if (samples <= 0) return;
        var watch = Stopwatch.StartNew();
        int tilesX = (Width + Tile - 1) / Tile, tilesY = (Height + Tile - 1) / Tile, tiles = tilesX * tilesY;
        int first = Samples, done = 0;
        var parallel = new ParallelOptions
        {
            CancellationToken = cancel,
            MaxDegreeOfParallelism = _options.Threads > 0 ? _options.Threads : -1,
        };
        Parallel.For(0, tiles, parallel, () => new TraceContext(_scene, _options), (tile, _, ctx) =>
        {
            int x0 = tile % tilesX * Tile, y0 = tile / tilesX * Tile;
            int x1 = Math.Min(Width, x0 + Tile), y1 = Math.Min(Height, y0 + Tile);
            for (int y = y0; y < y1; y++)
            {
                cancel.ThrowIfCancellationRequested();
                for (int x = x0; x < x1; x++)
                {
                    var sum = Vector3.Zero;
                    for (int s = first; s < first + samples; s++)
                        sum += ctx.Pixel(x, y, s, Width, Height);
                    _sum[y * Width + x] += sum;
                }
            }
            int n = Interlocked.Increment(ref done);
            progress?.Report((float)n / tiles);
            return ctx;
        }, _ => { });
        Samples += samples;
        _trace += watch.Elapsed;
    }

    /// <summary>The picture from the samples so far, with bloom, exposure and the tone curve (or clamped in the classic look).</summary>
    public RaytraceImage Resolve()
    {
        var hdr = new Vector3[_sum.Length];
        float inv = Samples > 0 ? 1f / Samples : 0;
        for (int i = 0; i < hdr.Length; i++) hdr[i] = _sum[i] * inv;
        if (!_options.Classic) PostProcess.Apply(hdr, Width, Height, _options.Modern);
        var bgra = new byte[Width * Height * 4];
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            var c = Vector3.Clamp(hdr[y * Width + x], Vector3.Zero, Vector3.One);
            int o = (y * Width + x) * 4;
            bgra[o] = (byte)MathF.Round(c.Z * 255);
            bgra[o + 1] = (byte)MathF.Round(c.Y * 255);
            bgra[o + 2] = (byte)MathF.Round(c.X * 255);
            bgra[o + 3] = 255;
        }
        return new RaytraceImage(Width, Height, bgra);
    }
}

/// <summary>The per-thread state of the tracer: random numbers, scratch visitors and the shading.</summary>
internal sealed class TraceContext
{
    private const int MaxLayers = 32;       // see-through surfaces one ray passes straight through
    private const float MinWeight = 0.004f; // contributions smaller than this are not traced

    private readonly CompiledScene _s;
    private readonly RaytraceOptions _o;
    private readonly RaytraceSettings _rs;
    private readonly AlphaFilter _alpha;
    private readonly ShadowVisitor _shadow;
    private readonly OcclusionVisitor _occlusion;
    private readonly float _tanY;
    private Rng _rng;
    private float _time;
    private Matrix4x4 _view;

    public TraceContext(CompiledScene scene, RaytraceOptions options)
    {
        _s = scene;
        _o = options;
        _rs = options.Settings;
        _alpha = new AlphaFilter(this);
        _shadow = new ShadowVisitor(this);
        _occlusion = new OcclusionVisitor(this);
        _tanY = MathF.Tan(Math.Clamp(scene.Camera.FieldOfView, 1f, 179f) * MathF.PI / 360);
    }

    public bool AlphaBlend => _o.AlphaBlend;

    // ------------------------------------------------------------------------------------------ camera

    public Vector3 Pixel(int x, int y, int sample, int width, int height)
    {
        _rng = new Rng(x, y, sample, _o.Seed);
        Vector2 jitter;
        if (_rs.Samples == 1)
        {
            jitter = new Vector2(0.5f);
        }
        else
        {
            uint h = Rng.Hash((uint)x * 9781u ^ (uint)y * 6271u ^ _o.Seed * 26699u);
            jitter = Sampling.R2(sample, (h & 0xFFFF) / 65536f, (h >> 16) / 65536f);
        }

        // the time within the open shutter, stratified over the samples
        _time = 0.5f;
        if (_s.Steps > 1)
        {
            uint h = Rng.Hash((uint)x * 1973u ^ (uint)y * 9277u ^ 0x5bd1e995u);
            float u = ((sample % _rs.Samples) + _rng.Next()) / _rs.Samples + (h & 0xFFFF) / 65536f;
            _time = u - MathF.Floor(u);
        }
        Matrix4x4 camWorld;
        if (_s.CameraWorld.Length > 1)
        {
            Instance.Interpolate(_s.CameraWorld, _time, out camWorld);
            Instance.Interpolate(_s.CameraView, _time, out _view);
        }
        else
        {
            camWorld = _s.CameraWorld[0];
            _view = _s.CameraView[0];
        }

        float aspect = (float)width / height;
        float px = (2 * (x + jitter.X) / width - 1) * _tanY * aspect;
        float py = (1 - 2 * (y + jitter.Y) / height) * _tanY;
        var eyeO = Vector3.Zero;
        var eyeD = new Vector3(px, py, -1);
        var cam = _s.Camera;
        if (!_o.Classic && cam.Aperture > 0)
        {
            var lens = Sampling.Disk(_rng.Next(), _rng.Next()) * cam.Aperture;
            eyeO = new Vector3(lens, 0);
            eyeD = (eyeD * cam.Focus - eyeO) / cam.Focus; // still -1 in z: t stays the eye depth
        }
        var o = Vector3.Transform(eyeO, camWorld);
        var d = Vector3.TransformNormal(eyeD, camWorld);
        // OpenGL clips by the eye depth; with a direction of -1 in eye z, t is that depth
        var c = Trace(o, d, cam.Near, cam.Far, 0, 0, long.MaxValue, 1, null, 0, 1);
        return _o.Classic ? Vector3.Clamp(c, Vector3.Zero, Vector3.One) : c;
    }

    // ------------------------------------------------------------------------------------------ tracing

    /// <summary>
    /// The colour seen along a ray, composited over the black background.
    /// <paramref name="fogBase"/> + t × <paramref name="fogScale"/> is the distance the fog uses (the eye
    /// depth for camera rays, as OpenGL computes it). Primitives drawn at or after
    /// <paramref name="drawLimit"/> are hidden by a see-through layer in front of them (OpenGL's depth test).
    /// </summary>
    private Vector3 Trace(Vector3 o, Vector3 d, float tMin, float tMax, int bounce, int layer, long drawLimit, float weight,
        Instance? medium, float fogBase, float fogScale)
    {
        var hit = default(Hit);
        if (!_s.Closest(o, d, _time, tMin, tMax, ref hit, _alpha, drawLimit)) return Vector3.Zero;

        var inst = hit.Instance!;
        var mat = inst.Mat;
        inst.Shape.Surface(hit.LocalO, hit.LocalD, hit.T, hit.Prim, hit.Bary, out var sp);
        inst.At(_time, out var world, out var inverse);
        var p = Vector3.Transform(sp.P, world);
        var n = SafeNormalize(NormalToWorld(sp.N, inverse), -d);
        var ng = SafeNormalize(NormalToWorld(sp.Ng, inverse), n);
        float dLen = d.Length();
        var dn = d / dLen;
        var v = -dn;

        var tex = Vector4.One;
        if (mat.Texture != null)
        {
            var uv = mat.SphereMap ? SphereMap(dn, n) : sp.Uv;
            tex = mat.Texture.Sample(uv.X, uv.Y);
        }

        // OpenGL lighting (the shading normal is not turned to the viewer: one-sided, as in the original)
        var ngView = Vector3.Dot(ng, v) < 0 ? -ng : ng;
        float eps = Epsilon(p);
        float ao = 1;
        if (!_o.Classic && _rs.AmbientOcclusion) ao = Occlusion(p + ngView * eps, ngView);
        var color = Light(inst, p, n, ng, v, Transform(sp.ShadowP, world), ao, eps) * new Vector3(tex.X, tex.Y, tex.Z);
        float coverage = Math.Clamp(mat.Alpha, 0f, 1f) * tex.W;
        float alpha = _o.AlphaBlend ? coverage : 1;

        if (!_o.Classic)
        {
            var nView = Vector3.Dot(n, v) < 0 ? -n : n;
            float cosT = Math.Clamp(Vector3.Dot(nView, v), 0f, 1f);
            float schlick = MathF.Pow(1 - cosT, 5);
            bool deeper = bounce < _rs.Bounces;
            Vector3? mirrored = null;

            if (mat.Glass)
            {
                float f0 = (mat.Ior - 1) / (mat.Ior + 1);
                f0 *= f0;
                float fresnel = f0 + (1 - f0) * schlick;
                var through = Vector3.Zero;
                float wThrough = weight * (1 - coverage) * (1 - fresnel);
                if (deeper && wThrough > MinWeight)
                {
                    if (inst.Shape.Solid)
                    {
                        bool entering = Vector3.Dot(ng, dn) < 0;
                        float eta = entering ? 1 / mat.Ior : mat.Ior;
                        var refr = Refract(dn, nView, eta);
                        if (refr is { } r)
                        {
                            r = Rough(r, -ngView, mat.Roughness);
                            var next = entering ? inst : (ReferenceEquals(medium, inst) ? null : medium);
                            through = Trace(p - ngView * eps, r, 0, float.PositiveInfinity, bounce + 1, layer, long.MaxValue, wThrough,
                                next, fogBase + hit.T * fogScale, 1);
                        }
                        else
                        {
                            fresnel = 1; // total internal reflection
                        }
                    }
                    else
                    {
                        // a thin sheet: the light comes out in the same direction
                        through = Trace(o, d, hit.T + eps / dLen, tMax, bounce + 1, layer, long.MaxValue, wThrough, medium, fogBase, fogScale);
                    }
                    through *= Vector3.Lerp(Vector3.One, mat.Diffuse, 0.5f);
                }
                mirrored = deeper && weight * fresnel > MinWeight
                    ? Reflect(p, dn, nView, ngView, mat.Roughness, eps, bounce, weight * fresnel, medium, fogBase + hit.T * fogScale)
                    : Vector3.Zero;
                color = Vector3.Lerp(Vector3.Lerp(through, color, coverage), mirrored.Value, fresnel);
                alpha = 1;
            }

            if (mat.Reflectivity > 0)
            {
                float r = mat.Reflectivity;
                float k = r + (1 - r) * schlick * r;
                mirrored ??= deeper && weight * k > MinWeight
                    ? Reflect(p, dn, nView, ngView, mat.Roughness, eps, bounce, weight * k, medium, fogBase + hit.T * fogScale)
                    : Vector3.Zero;
                color = Vector3.Lerp(color, mirrored.Value, k);
            }
        }

        color = Fog(color, fogBase + hit.T * fogScale);

        // see-through surfaces: what is behind, blended as glBlendFunc(SRC_ALPHA, ONE_MINUS_SRC_ALPHA).
        // The OpenGL renderers draw in scene order with depth writes on, so whatever they draw after a
        // see-through surface and behind it is not seen. The layers behind follow the same rule, so
        // scenes look as they were made in the editor (nested halos keep their brightness, a light
        // column hides the ground it was drawn before). Glass refracts instead and is not affected.
        if (alpha < 0.999f && layer < MaxLayers && weight * (1 - alpha) > MinWeight)
        {
            var behind = Trace(o, d, hit.T + eps / dLen, tMax, bounce, layer + 1, Math.Min(drawLimit, inst.DrawKey(hit.Prim)),
                weight * (1 - alpha), medium, fogBase, fogScale);
            color = color * alpha + behind * (1 - alpha);
        }
        else if (alpha < 1)
        {
            color *= alpha;
        }

        // inside glass the light is absorbed on the way (Beer–Lambert), in the glass's colour
        if (medium is { Mat.Absorption: > 0 } m)
        {
            float dist = hit.T * dLen;
            var sigma = m.Mat.Absorption * (Vector3.One - Vector3.Clamp(m.Mat.Diffuse, Vector3.Zero, Vector3.One));
            color *= new Vector3(MathF.Exp(-sigma.X * dist), MathF.Exp(-sigma.Y * dist), MathF.Exp(-sigma.Z * dist));
        }
        return color;
    }

    private Vector3 Reflect(Vector3 p, Vector3 dn, Vector3 nView, Vector3 ngView, float roughness, float eps, int bounce,
        float weight, Instance? medium, float fogBase)
    {
        var r = Vector3.Reflect(dn, nView);
        if (Vector3.Dot(r, ngView) <= 0) r = Vector3.Reflect(dn, ngView); // facets: keep it above the surface
        r = Rough(r, ngView, roughness);
        return Trace(p + ngView * eps, r, 0, float.PositiveInfinity, bounce + 1, 0, long.MaxValue, weight, medium, fogBase, 1);
    }

    /// <summary>Spreads a mirror direction into a glossy lobe; it stays on the side of <paramref name="side"/>.</summary>
    private Vector3 Rough(Vector3 dir, Vector3 side, float roughness)
    {
        if (roughness <= 0) return dir;
        float spread = roughness * roughness * 1.5f;
        for (int i = 0; i < 4; i++)
        {
            var t = Vector3.Normalize(dir + Sampling.Ball(ref _rng) * spread);
            if (Vector3.Dot(t, side) > 1e-4f) return t;
        }
        return dir;
    }

    private static Vector3? Refract(Vector3 i, Vector3 n, float eta)
    {
        float cosI = -Vector3.Dot(n, i);
        float k = 1 - eta * eta * (1 - cosI * cosI);
        if (k < 0) return null;
        return Vector3.Normalize(eta * i + (eta * cosI - MathF.Sqrt(k)) * n);
    }

    private Vector3 Fog(Vector3 color, float z)
    {
        if (_s.FogMode == FogMode.None) return color;
        float f = _s.FogMode switch
        {
            FogMode.Linear => (_s.FogEnd - z) / MathF.Max(_s.FogEnd - _s.FogStart, 1e-6f),
            FogMode.Exponential => MathF.Exp(-_s.FogDensity * z),
            _ => MathF.Exp(-(_s.FogDensity * z) * (_s.FogDensity * z)),
        };
        return Vector3.Lerp(_s.FogColor, color, Math.Clamp(f, 0f, 1f));
    }

    // ------------------------------------------------------------------------------------------ lighting

    /// <summary>The fixed-function lighting of the OpenGL renderers, with shadows in the full look.</summary>
    private Vector3 Light(Instance inst, Vector3 p, Vector3 n, Vector3 ng, Vector3 v, Vector3 shadowP, float ao, float eps)
    {
        var m = inst.Mat;
        bool classic = _o.Classic;
        var c = m.Emission * (classic ? 1 : m.Glow) + CompiledScene.GlobalAmbient * m.Ambient * ao;
        var lights = _s.Lights;
        for (int i = 0; i < lights.Length; i++)
        {
            ref readonly var l = ref lights[i];
            var lv = l.Position - p;
            float dist = lv.Length();
            var dir = lv / MathF.Max(dist, 1e-6f);
            float att = 1 / MathF.Max(l.Constant + l.Linear * dist + l.Quadratic * dist * dist, 1e-6f);
            float spot = 1;
            if (l.Spot)
            {
                float cosAng = Vector3.Dot(-dir, l.SpotDirection);
                spot = cosAng < l.CosCutoff ? 0 : MathF.Pow(MathF.Max(cosAng, 0), l.Exponent);
            }
            float nDotL = MathF.Max(Vector3.Dot(n, dir), 0);
            var direct = nDotL * l.Diffuse * m.Diffuse;
            if (nDotL > 0)
            {
                var h = Vector3.Normalize(dir + v);
                float nDotH = MathF.Max(Vector3.Dot(n, h), 0);
                direct += (m.Shininess > 0 ? MathF.Pow(nDotH, m.Shininess) : 1) * l.Specular * m.Specular;
            }
            var lit = Vector3.One;
            if (!classic && _rs.Shadows && l.CastsShadows && att * spot > 0 && nDotL > 0 && direct != Vector3.Zero)
                lit = Shadow(i, l, inst.Convex ? inst : null, shadowP, ng, eps);
            c += att * spot * (l.Ambient * m.Ambient * ao + direct * lit);
        }
        return classic ? Vector3.Clamp(c, Vector3.Zero, Vector3.One) : Vector3.Max(c, Vector3.Zero);
    }

    /// <summary>How much of a light reaches the point: 0 in full shadow, tinted by glass on the way.</summary>
    private Vector3 Shadow(int index, in LightData l, Instance? self, Vector3 shadowP, Vector3 ng, float eps)
    {
        var target = l.Position;
        if (l.Radius > 0)
        {
            // a point on the disk the spherical light shows towards the surface
            var toLight = l.Position - shadowP;
            float len = toLight.Length();
            if (len > 1e-6f)
            {
                Sampling.Basis(toLight / len, out var t, out var b);
                var s = Sampling.Disk(_rng.Next(), _rng.Next()) * l.Radius;
                target += t * s.X + b * s.Y;
            }
        }
        var side = Vector3.Dot(ng, target - shadowP) >= 0 ? ng : -ng;
        var from = shadowP + side * eps;
        var seg = target - from;
        _shadow.Reset(1u << index, self, l.Fixture, seg.Length());
        _s.Visit(from, seg, _time, 0, 1 - 1e-4f, _shadow);
        return _shadow.Transmittance;
    }

    /// <summary>The share of the hemisphere that is open within the occlusion distance.</summary>
    private float Occlusion(Vector3 from, Vector3 n)
    {
        int rays = _rs.AoRays, open = 0;
        for (int i = 0; i < rays; i++)
        {
            var dir = Sampling.CosineHemisphere(n, _rng.Next(), _rng.Next());
            _occlusion.Reset();
            if (!_s.Visit(from, dir, _time, 0, _rs.AoDistance, _occlusion)) open++;
        }
        return (float)open / rays;
    }

    // ------------------------------------------------------------------------------------------ surfaces

    /// <summary>
    /// How opaque a hit is: the material alpha times the texture alpha (with alpha blending), 1 for
    /// opaque elements. Used by the alpha test and by shadow rays through glass and cut-outs.
    /// </summary>
    public float Opacity(Instance inst, Shape shape, int prim, Vector2 bary, in Vector3 o, in Vector3 d, float t, Vector3 worldDir, float time)
    {
        var m = inst.Mat;
        if (!_o.AlphaBlend && !m.Glass) return 1;
        float a = Math.Clamp(m.Alpha, 0f, 1f);
        if (_o.AlphaBlend && m.Texture is { HasTransparentTexels: true } tex)
        {
            shape.Surface(o, d, t, prim, bary, out var sp);
            Vector2 uv = sp.Uv;
            if (m.SphereMap)
            {
                inst.At(time, out _, out var inverse);
                var n = SafeNormalize(NormalToWorld(sp.N, inverse), -worldDir);
                uv = SphereMap(SafeNormalize(worldDir, Vector3.UnitZ), n);
            }
            a *= tex.Sample(uv.X, uv.Y).W;
        }
        return a;
    }

    /// <summary>OpenGL's sphere map, from the eye-space ray and normal (as the shaders compute it).</summary>
    private Vector2 SphereMap(Vector3 dirWorld, Vector3 normalWorld)
    {
        var i = SafeNormalize(Vector3.TransformNormal(dirWorld, _view), -Vector3.UnitZ);
        var n = SafeNormalize(Vector3.TransformNormal(normalWorld, _view), Vector3.UnitZ);
        var r = Vector3.Reflect(i, n);
        float m = 2 * MathF.Sqrt(r.X * r.X + r.Y * r.Y + (r.Z + 1) * (r.Z + 1));
        return new Vector2(r.X, r.Y) / MathF.Max(m, 1e-6f) + new Vector2(0.5f);
    }

    /// <summary>A normal from object to world space: times the transposed inverse.</summary>
    private static Vector3 NormalToWorld(Vector3 n, in Matrix4x4 inv) => new(
        n.X * inv.M11 + n.Y * inv.M12 + n.Z * inv.M13,
        n.X * inv.M21 + n.Y * inv.M22 + n.Z * inv.M23,
        n.X * inv.M31 + n.Y * inv.M32 + n.Z * inv.M33);

    private static Vector3 Transform(Vector3 p, in Matrix4x4 m) => Vector3.Transform(p, m);

    private static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
    {
        float len2 = v.LengthSquared();
        return len2 > 1e-30f && float.IsFinite(len2) ? v / MathF.Sqrt(len2) : fallback;
    }

    /// <summary>How far rays start off a surface, relative to the size of the coordinates.</summary>
    private static float Epsilon(Vector3 p) =>
        2e-4f * MathF.Max(1, MathF.Max(MathF.Abs(p.X), MathF.Max(MathF.Abs(p.Y), MathF.Abs(p.Z))));

    // ------------------------------------------------------------------------------------------ visitors

    /// <summary>Multiplies the light that gets through every element on the way to a light.</summary>
    private sealed class ShadowVisitor(TraceContext ctx) : InstanceVisitor
    {
        private Instance? _inst;
        private Vector3 _dir;
        private float _time;
        private uint _light;
        private Instance? _self;
        private float _body, _length;
        private bool _partialCounted;

        public Vector3 Transmittance;

        /// <param name="body">Size of the light's glowing body (<see cref="LightData.Fixture"/>), 0 for a point.</param>
        /// <param name="length">Distance from the shaded point to the light.</param>
        public void Reset(uint lightBit, Instance? self, float body, float length)
        {
            _light = lightBit;
            _self = self;
            _body = body;
            _length = length;
            Transmittance = Vector3.One;
        }

        public override bool Wants(Instance inst) =>
            inst.CastsShadows && (inst.Encloses & _light) == 0 && !ReferenceEquals(inst, _self);

        public override void Begin(Instance inst, Vector3 worldDir, float time)
        {
            _inst = inst;
            _dir = worldDir;
            _time = time;
            _partialCounted = false;
        }

        public override bool Visit(Shape shape, int prim, Vector2 bary, in Vector3 o, in Vector3 d, float t)
        {
            float a = ctx.Opacity(_inst!, shape, prim, bary, o, d, t, _dir, _time);

            // A point light with a glowing body (a sun, a bulb): an element that looks smaller than the
            // body from the shaded point hides only part of it. The share is the ratio of the two
            // apparent sizes, squared (t is the fraction of the way to the light). A small element next
            // to a sun then leaves a faint shadow on a planet far away, not a huge black one, while
            // elements near the shaded point still hide the whole body.
            float cover = 1;
            if (_body > 0)
            {
                float k = _inst!.Size * _length / (_body * MathF.Max(t * _length, 1e-6f));
                cover = MathF.Min(1, k * k);
            }
            if (cover < 1)
            {
                if (_partialCounted) return false; // once per element, not at each of its surfaces
                _partialCounted = true;
            }
            else if (a >= 0.999f)
            {
                Transmittance = Vector3.Zero;
                return true;
            }
            var m = _inst!.Mat;
            var tint = m.Glass ? Vector3.Lerp(Vector3.One, m.Diffuse, 0.5f) : Vector3.One;
            Transmittance *= Vector3.Lerp(Vector3.One, (1 - a) * tint, cover);
            return MathF.Max(Transmittance.X, MathF.Max(Transmittance.Y, Transmittance.Z)) < 0.002f;
        }
    }

    /// <summary>Stops at the first surface that is not see-through.</summary>
    private sealed class OcclusionVisitor(TraceContext ctx) : InstanceVisitor
    {
        private Instance? _inst;
        private Vector3 _dir;
        private float _time;

        public void Reset() => _inst = null;

        public override bool Wants(Instance inst) => true;

        public override void Begin(Instance inst, Vector3 worldDir, float time)
        {
            _inst = inst;
            _dir = worldDir;
            _time = time;
        }

        public override bool Visit(Shape shape, int prim, Vector2 bary, in Vector3 o, in Vector3 d, float t) =>
            ctx.Opacity(_inst!, shape, prim, bary, o, d, t, _dir, _time) > 0.5f;
    }
}

/// <summary>The composite of the modern renderer on the CPU: bloom, exposure and the soft shoulder.</summary>
internal static class PostProcess
{
    private const int BloomLevels = 6;

    public static void Apply(Vector3[] hdr, int width, int height, ModernSettings modern)
    {
        Image? bloom = null;
        float strength = 0;
        if (modern.Bloom && modern.BloomStrength > 0)
        {
            bloom = Bloom(hdr, width, height, modern.BloomThreshold, out int count);
            strength = modern.BloomStrength / Math.Max(1, count);
        }
        float exposure = modern.Exposure;
        const float k = 0.8f;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int i = y * width + x;
            var c = hdr[i];
            if (bloom != null) c += bloom.Sample((x + 0.5f) / width, (y + 0.5f) / height) * strength;
            c = Vector3.Max(c * exposure, Vector3.Zero);
            var over = Vector3.Max(c - new Vector3(k), Vector3.Zero);
            c = Vector3.Min(c, new Vector3(k)) + (1 - k) * (Vector3.One - Exp(-over / (1 - k)));
            hdr[i] = c;
        }
    }

    private static Vector3 Exp(Vector3 v) => new(MathF.Exp(v.X), MathF.Exp(v.Y), MathF.Exp(v.Z));

    /// <summary>The dual-filter bloom of the modern renderer: down to six levels from half size and back up.</summary>
    private static Image Bloom(Vector3[] hdr, int width, int height, float threshold, out int count)
    {
        var source = new Image(width, height, hdr);
        var levels = new List<Image>();
        int w = Math.Max(1, width / 2), h = Math.Max(1, height / 2);
        for (int i = 0; i < BloomLevels && (i == 0 || Math.Min(w, h) >= 4); i++)
        {
            levels.Add(new Image(w, h, new Vector3[w * h]));
            w = Math.Max(1, w / 2);
            h = Math.Max(1, h / 2);
        }
        count = levels.Count;
        float knee = MathF.Max(0.05f, threshold * 0.5f);

        for (int i = 0; i < levels.Count; i++)
        {
            var src = i == 0 ? source : levels[i - 1];
            var dst = levels[i];
            bool prefilter = i == 0;
            float hx = 1f / src.W, hy = 1f / src.H;
            Parallel.For(0, dst.H, y =>
            {
                for (int x = 0; x < dst.W; x++)
                {
                    float u = (x + 0.5f) / dst.W, v = (y + 0.5f) / dst.H;
                    var sum = Tap(src, u, v, prefilter, threshold, knee) * 4
                              + Tap(src, u - hx, v - hy, prefilter, threshold, knee)
                              + Tap(src, u + hx, v + hy, prefilter, threshold, knee)
                              + Tap(src, u + hx, v - hy, prefilter, threshold, knee)
                              + Tap(src, u - hx, v + hy, prefilter, threshold, knee);
                    dst.P[y * dst.W + x] = sum / 8;
                }
            });
        }

        for (int i = levels.Count - 1; i > 0; i--)
        {
            var src = levels[i];
            var dst = levels[i - 1];
            float hx = 1f / src.W, hy = 1f / src.H;
            Parallel.For(0, dst.H, y =>
            {
                for (int x = 0; x < dst.W; x++)
                {
                    float u = (x + 0.5f) / dst.W, v = (y + 0.5f) / dst.H;
                    var sum = src.Sample(u - 2 * hx, v) + src.Sample(u - hx, v + hy) * 2
                              + src.Sample(u, v + 2 * hy) + src.Sample(u + hx, v + hy) * 2
                              + src.Sample(u + 2 * hx, v) + src.Sample(u + hx, v - hy) * 2
                              + src.Sample(u, v - 2 * hy) + src.Sample(u - hx, v - hy) * 2;
                    dst.P[y * dst.W + x] += sum / 12;
                }
            });
        }
        return levels[0];
    }

    private static Vector3 Tap(Image src, float u, float v, bool prefilter, float threshold, float knee)
    {
        var c = Vector3.Min(src.Sample(u, v), new Vector3(64));
        if (!prefilter) return c;
        float br = MathF.Max(c.X, MathF.Max(c.Y, c.Z));
        float rq = Math.Clamp(br - threshold + knee, 0, 2 * knee);
        rq = rq * rq / (4 * knee + 1e-5f);
        return c * MathF.Max(rq, br - threshold) / MathF.Max(br, 1e-4f);
    }

    /// <summary>A float RGB picture sampled like a GL_LINEAR, clamp-to-edge texture.</summary>
    private sealed class Image(int w, int h, Vector3[] p)
    {
        public readonly int W = w, H = h;
        public readonly Vector3[] P = p;

        public Vector3 Sample(float u, float v)
        {
            float x = u * W - 0.5f, y = v * H - 0.5f;
            float fx = MathF.Floor(x), fy = MathF.Floor(y);
            float ax = x - fx, ay = y - fy;
            int x0 = Math.Clamp((int)fx, 0, W - 1), x1 = Math.Clamp((int)fx + 1, 0, W - 1);
            int y0 = Math.Clamp((int)fy, 0, H - 1), y1 = Math.Clamp((int)fy + 1, 0, H - 1);
            var a = Vector3.Lerp(P[y0 * W + x0], P[y0 * W + x1], ax);
            var b = Vector3.Lerp(P[y1 * W + x0], P[y1 * W + x1], ax);
            return Vector3.Lerp(a, b, ay);
        }
    }
}
