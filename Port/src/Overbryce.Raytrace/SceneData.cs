using System.Numerics;
using Overbryce.Core.Geometry;
using Overbryce.Core.Model;

namespace Overbryce.Raytrace;

/// <summary>
/// How the scene is looked at: the view matrix (world to eye, as the OpenGL renderers use it), the
/// vertical field of view in degrees, the clipping distances and the thin lens.
/// </summary>
public sealed record CameraSetup(Matrix4x4 View, float FieldOfView, float Near, float Far, float Aperture = 0, float Focus = CameraElement.DefaultFocus);

/// <summary>One evaluation of the animation within the open shutter (one step without motion blur).</summary>
public sealed record FrameStep(Scene Scene, CameraSetup Camera);

internal sealed class Material
{
    public Vector3 Ambient, Diffuse, Specular, Emission;
    public float Alpha;
    public float Shininess; // the OpenGL exponent
    public float Glow, Reflectivity, Ior, Roughness, Absorption;
    public TextureImage? Texture;
    public bool SphereMap;

    public bool Glass => Ior > 1 && Alpha < 0.999f;
}

internal sealed class Instance
{
    public required Shape Shape;
    public required Material Mat;
    public required Matrix4x4[] World;   // per frame step
    public required Matrix4x4[] Inverse;
    public Bounds WorldBounds;
    public bool CastsShadows;
    public bool AlphaTest;
    public uint Encloses;                // lights (bits) inside this element, which it does not shadow
    public bool Convex;                  // cannot shadow itself (sphere, box, flat shapes): its facets do not either
    public int DrawOrder;                // the order in which the OpenGL renderers draw the element
    public float Size;                   // the largest half-extent along its axes (a sphere's radius)
    public required string Name;

    /// <summary>When OpenGL draws this primitive: the element's order, then the primitive's within it.</summary>
    public long DrawKey(int prim) => ((long)DrawOrder << 32) | (uint)Shape.DrawIndex(prim);

    public void At(float u, out Matrix4x4 world, out Matrix4x4 inverse)
    {
        if (World.Length == 1)
        {
            world = World[0];
            inverse = Inverse[0];
            return;
        }
        Interpolate(World, u, out world);
        Interpolate(Inverse, u, out inverse);
    }

    public void ToLocal(in Vector3 o, in Vector3 d, float u, out Vector3 lo, out Vector3 ld)
    {
        Matrix4x4 inv;
        if (Inverse.Length == 1) inv = Inverse[0];
        else Interpolate(Inverse, u, out inv);
        lo = Vector3.Transform(o, inv);
        ld = Vector3.TransformNormal(d, inv);
    }

    public static void Interpolate(Matrix4x4[] steps, float u, out Matrix4x4 m)
    {
        float x = Math.Clamp(u, 0, 1) * (steps.Length - 1);
        int i = Math.Min((int)x, steps.Length - 2);
        m = Matrix4x4.Lerp(steps[i], steps[i + 1], x - i);
    }
}

internal struct LightData
{
    public Vector3 Position, SpotDirection;
    public Vector3 Ambient, Diffuse, Specular;
    public bool Spot;
    public float CosCutoff, Exponent;
    public float Constant, Linear, Quadratic;
    public float Radius;
    public float Fixture;                // size of the glowing body around a point light, 0 if none
    public bool CastsShadows;
}

internal struct Hit
{
    public float T;
    public Instance? Instance;
    public int Prim;
    public Vector2 Bary;
    public Vector3 LocalO, LocalD;
}

/// <summary>A frame ready to trace: instances in a two-level BVH, lights, fog and the camera.</summary>
internal sealed class CompiledScene
{
    public required Instance[] Instances;   // in BVH leaf order
    public required BvhNode[] Nodes;
    public required LightData[] Lights;
    public required Matrix4x4[] CameraWorld; // eye to world, per frame step
    public required Matrix4x4[] CameraView;  // world to eye, per frame step
    public required CameraSetup Camera;      // of the reference step
    public int Steps;
    public FogMode FogMode;
    public float FogStart, FogEnd, FogDensity;
    public Vector3 FogColor;
    public int TriangleCount;

    public static readonly Vector3 GlobalAmbient = new(0.2f); // GL_LIGHT_MODEL_AMBIENT default

    /// <summary>
    /// The nearest accepted hit; the filter is used for elements whose textures cut holes. Primitives
    /// drawn at or after <paramref name="drawLimit"/> (an <see cref="Instance.DrawKey"/>) are skipped:
    /// OpenGL's depth test hides them behind a see-through surface it drew earlier.
    /// </summary>
    public bool Closest(in Vector3 o, in Vector3 d, float time, float tMin, float tMax, ref Hit hit, AlphaFilter alpha,
        long drawLimit = long.MaxValue)
    {
        bool limited = drawLimit != long.MaxValue;
        int limitElement = (int)(drawLimit >> 32), limitPrim = (int)(drawLimit & 0xFFFFFFFF);
        if (Nodes.Length == 0) return false;
        var inv = Bounds.Inverse(d);
        Span<int> stack = stackalloc int[128];
        Span<float> enter = stackalloc float[128];
        int sp = 0;
        if (!Bounds.Hit(Nodes[0].Min, Nodes[0].Max, o, inv, tMin, tMax, out float t0)) return false;
        stack[sp] = 0;
        enter[sp++] = t0;
        bool found = false;
        int prim = 0;
        var bary = Vector2.Zero;
        while (sp > 0)
        {
            sp--;
            if (enter[sp] > tMax) continue;
            ref readonly var node = ref Nodes[stack[sp]];
            if (node.Count > 0)
            {
                for (int i = node.Start, end = node.Start + node.Count; i < end; i++)
                {
                    var inst = Instances[i];
                    if (limited && inst.DrawOrder > limitElement) continue;
                    if (!Bounds.Hit(inst.WorldBounds.Min, inst.WorldBounds.Max, o, inv, tMin, tMax, out _)) continue;
                    inst.ToLocal(o, d, time, out var lo, out var ld);
                    int primLimit = limited && inst.DrawOrder == limitElement ? limitPrim : int.MaxValue;
                    HitFilter? filter = null;
                    if (inst.AlphaTest || primLimit != int.MaxValue)
                    {
                        alpha.Begin(inst, d, time, primLimit);
                        filter = alpha;
                    }
                    if (inst.Shape.Closest(lo, ld, tMin, ref tMax, ref prim, ref bary, filter))
                    {
                        found = true;
                        hit.T = tMax;
                        hit.Instance = inst;
                        hit.Prim = prim;
                        hit.Bary = bary;
                        hit.LocalO = lo;
                        hit.LocalD = ld;
                    }
                }
                continue;
            }
            int l = node.Start, r = l + 1;
            bool hl = Bounds.Hit(Nodes[l].Min, Nodes[l].Max, o, inv, tMin, tMax, out float tl);
            bool hr = Bounds.Hit(Nodes[r].Min, Nodes[r].Max, o, inv, tMin, tMax, out float tr);
            if (hl && hr)
            {
                if (tl <= tr) { stack[sp] = r; enter[sp++] = tr; stack[sp] = l; enter[sp++] = tl; }
                else { stack[sp] = l; enter[sp++] = tl; stack[sp] = r; enter[sp++] = tr; }
            }
            else if (hl) { stack[sp] = l; enter[sp++] = tl; }
            else if (hr) { stack[sp] = r; enter[sp++] = tr; }
        }
        return found;
    }

    /// <summary>Shows every hit between tMin and tMax to the visitor (which picks the elements it cares about).</summary>
    public bool Visit(in Vector3 o, in Vector3 d, float time, float tMin, float tMax, InstanceVisitor visitor)
    {
        if (Nodes.Length == 0) return false;
        var inv = Bounds.Inverse(d);
        Span<int> stack = stackalloc int[128];
        int sp = 0;
        stack[sp++] = 0;
        while (sp > 0)
        {
            ref readonly var node = ref Nodes[stack[--sp]];
            if (!Bounds.Hit(node.Min, node.Max, o, inv, tMin, tMax, out _)) continue;
            if (node.Count > 0)
            {
                for (int i = node.Start, end = node.Start + node.Count; i < end; i++)
                {
                    var inst = Instances[i];
                    if (!visitor.Wants(inst)) continue;
                    if (!Bounds.Hit(inst.WorldBounds.Min, inst.WorldBounds.Max, o, inv, tMin, tMax, out _)) continue;
                    inst.ToLocal(o, d, time, out var lo, out var ld);
                    visitor.Begin(inst, d, time);
                    if (inst.Shape.Visit(lo, ld, tMin, tMax, visitor)) return true;
                }
                continue;
            }
            stack[sp++] = node.Start;
            stack[sp++] = node.Start + 1;
        }
        return false;
    }
}

/// <summary>A visitor that is told which element its hits belong to.</summary>
internal abstract class InstanceVisitor : HitVisitor
{
    public abstract bool Wants(Instance inst);
    public abstract void Begin(Instance inst, Vector3 worldDir, float time);
}

/// <summary>
/// Rejects hits on texels that the texture makes transparent (OpenGL's alpha test), and primitives
/// that OpenGL's depth test hides behind a see-through layer drawn earlier.
/// </summary>
internal sealed class AlphaFilter(TraceContext ctx) : HitFilter
{
    private Instance? _inst;
    private Vector3 _dir;
    private float _time;
    private int _primLimit;

    public void Begin(Instance inst, Vector3 worldDir, float time, int primLimit = int.MaxValue)
    {
        _inst = inst;
        _dir = worldDir;
        _time = time;
        _primLimit = primLimit;
    }

    public override bool Accept(Shape shape, int prim, Vector2 bary, in Vector3 o, in Vector3 d, float t)
    {
        if (_primLimit != int.MaxValue && shape.DrawIndex(prim) >= _primLimit) return false;
        return !_inst!.AlphaTest || ctx.Opacity(_inst, shape, prim, bary, o, d, t, _dir, _time) > 0.004f;
    }
}

/// <summary>Turns elements of a scene into instances, sharing the geometry of equal shapes.</summary>
internal sealed class GeometryCache
{
    private readonly Dictionary<ShapeKey, (Shape Shape, int Used)> _shapes = new();
    private int _generation;

    public int Count => _shapes.Count;

    /// <summary>Marks the start of a frame; shapes unused for a few frames are dropped.</summary>
    public void NextFrame()
    {
        _generation++;
        if (_shapes.Count < 64) return;
        foreach (var key in _shapes.Where(p => _generation - p.Value.Used > 2).Select(p => p.Key).ToList())
            _shapes.Remove(key);
    }

    public Shape Get(in ShapeKey key, Func<Shape> build)
    {
        if (_shapes.TryGetValue(key, out var entry))
        {
            _shapes[key] = (entry.Shape, _generation);
            return entry.Shape;
        }
        var shape = build();
        _shapes[key] = (shape, _generation);
        return shape;
    }
}

/// <summary>The identity of a piece of geometry: the type and the shape parameters after the scale is taken out.</summary>
internal readonly record struct ShapeKey(ElementType Type, double A, double B, double C, int Q1, int Q2, bool Smooth, MeshKey? Mesh = null);

/// <summary>The control points of a Bezier mesh as a key.</summary>
internal sealed class MeshKey : IEquatable<MeshKey>
{
    private readonly float[] _points;
    private readonly int _hash;

    public MeshKey(float[] points)
    {
        _points = (float[])points.Clone();
        var h = new HashCode();
        foreach (float f in _points) h.Add(f);
        _hash = h.ToHashCode();
    }

    public float[] Points => _points;

    public bool Equals(MeshKey? other) => other != null && other._hash == _hash && other._points.AsSpan().SequenceEqual(_points);
    public override bool Equals(object? obj) => Equals(obj as MeshKey);
    public override int GetHashCode() => _hash;
}

/// <summary>Builds a <see cref="CompiledScene"/> from the evaluated animation.</summary>
internal static class SceneCompiler
{
    public static CompiledScene Compile(IReadOnlyList<FrameStep> steps, RaytraceOptions options, GeometryCache cache)
    {
        if (steps.Count == 0) throw new ArgumentException("No frame step.", nameof(steps));
        cache.NextFrame();
        int refIndex = steps.Count / 2;
        var reference = steps[refIndex].Scene;
        bool smooth = options.Settings.Geometry == RaytraceGeometry.Smooth;

        // world matrices of every element in each step, by name path (for motion blur)
        var worlds = new Dictionary<string, Matrix4x4>[steps.Count];
        for (int s = 0; s < steps.Count; s++)
        {
            worlds[s] = new Dictionary<string, Matrix4x4>(StringComparer.OrdinalIgnoreCase);
            if (s != refIndex) Collect(steps[s].Scene, Matrix4x4.Identity, "", worlds[s]);
        }

        var lights = CompileLights(reference);
        var textures = new Dictionary<string, TextureImage?>(StringComparer.OrdinalIgnoreCase);
        var instances = new List<Instance>();
        var fixtures = new List<(Vector3 Center, float Size)>();
        int triangles = 0;

        void Visit(Element e, Matrix4x4 parentWorld, string path)
        {
            if (e is GroupElement g)
            {
                var groupWorld = Transforms.GroupLocal(g) * parentWorld;
                foreach (var child in g.Children)
                    Visit(child, groupWorld, path + "/" + child.Name);
                return;
            }
            if (e is LightElement or CameraElement) return;
            if (e.DrawStyle == DrawStyle.Line) return; // wire-frame elements are not traced

            var mat = CompileMaterial(e, options, textures);
            if (options.AlphaBlend && mat.Alpha <= 0.004f) return; // always discarded by the alpha test
            if (Canonical(e, smooth) is not { } canon) return;
            var shape = cache.Get(canon.Key, () => Build(canon.Key));
            if (shape.Bounds.IsEmpty) return;
            if (shape is TriangleMesh mesh) triangles += mesh.TriangleCount;

            var local = Transforms.ElementLocal(e) * parentWorld;
            var world = new Matrix4x4[steps.Count];
            var inverse = new Matrix4x4[steps.Count];
            var box = Bounds.Empty;
            for (int s = 0; s < steps.Count; s++)
            {
                var w = s == refIndex || !worlds[s].TryGetValue(path, out var other) ? local : other;
                world[s] = canon.Scale * w;
                if (!Matrix4x4.Invert(world[s], out inverse[s])) return;
                box.Grow(shape.Bounds.Transform(world[s]));
            }
            if (steps.Count > 1 && world.All(m => m == world[0]))
            {
                world = [world[0]];
                inverse = [inverse[0]];
            }

            // an element around a light (a sun, a lamp, a glowing crystal) does not shadow it, so the
            // light shines out as it did in OpenGL; the same rule as the modern renderer's shadow maps:
            // the light is inside the element's bounding sphere
            uint encloses = 0;
            var refWorld = world[Math.Min(refIndex, world.Length - 1)];
            var center = Vector3.Transform(shape.Bounds.Center, refWorld);
            float radius = 0;
            for (int c = 0; c < 8; c++)
            {
                var b = shape.Bounds;
                var corner = new Vector3((c & 1) != 0 ? b.Max.X : b.Min.X, (c & 2) != 0 ? b.Max.Y : b.Min.Y, (c & 4) != 0 ? b.Max.Z : b.Min.Z);
                radius = MathF.Max(radius, Vector3.Distance(center, Vector3.Transform(corner, refWorld)));
            }
            for (int i = 0; i < lights.Length; i++)
                if (Vector3.Distance(lights[i].Position, center) < radius)
                    encloses |= 1u << i;

            // the element's own size: the largest half-extent along its axes (the radius of a sphere or
            // a disk); glowing elements can be the bodies of lights (a sun, a bulb)
            var half = (shape.Bounds.Max - shape.Bounds.Min) * 0.5f;
            float size = MathF.Max(half.X * new Vector3(refWorld.M11, refWorld.M12, refWorld.M13).Length(),
                MathF.Max(half.Y * new Vector3(refWorld.M21, refWorld.M22, refWorld.M23).Length(),
                    half.Z * new Vector3(refWorld.M31, refWorld.M32, refWorld.M33).Length()));
            if (!float.IsFinite(size)) size = 0;
            if (size > 0 && MathF.Max(mat.Emission.X, MathF.Max(mat.Emission.Y, mat.Emission.Z)) * mat.Glow >= 0.5f)
                fixtures.Add((center, size));

            instances.Add(new Instance
            {
                Shape = shape,
                Mat = mat,
                World = world,
                Inverse = inverse,
                WorldBounds = box,
                CastsShadows = e.CastsShadows,
                AlphaTest = options.AlphaBlend && mat.Texture is { HasTransparentTexels: true },
                Encloses = encloses,
                Convex = e is SphereElement or BoxElement or RectangleElement or TriangleElement or DiskElement,
                DrawOrder = instances.Count,
                Size = size,
                Name = e.Name,
            });
        }
        Visit(reference, Matrix4x4.Identity, "");

        // The body of a point light: the smallest glowing element centred on it (the modern renderer's
        // rule). Elements less than half its size cast no shadow from it: as a point, the light would
        // make a small element near a sun throw a huge hard shadow across the scene, where a light of
        // the sun's real size leaves only a faint penumbra. A light with a radius of its own is sampled
        // as an area light instead, which gives that penumbra.
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].Radius > 0) continue;
            float best = 0;
            foreach (var (c, size) in fixtures)
                if (Vector3.Distance(c, lights[i].Position) <= 0.5f * size && (best == 0 || size < best))
                    best = size;
            lights[i].Fixture = best;
        }

        var boxes = instances.Select(i => i.WorldBounds).ToArray();
        var (nodes, order) = instances.Count > 0 ? BvhBuilder.Build(boxes, 2) : ([], []);

        var cameraView = steps.Select(s => s.Camera.View).ToArray();
        var cameraWorld = cameraView.Select(v => Matrix4x4.Invert(v, out var m) ? m : Matrix4x4.Identity).ToArray();
        if (cameraWorld.All(m => m == cameraWorld[0]))
        {
            cameraWorld = [cameraWorld[0]];
            cameraView = [cameraView[0]];
        }

        return new CompiledScene
        {
            Instances = order.Select(i => instances[i]).ToArray(),
            Nodes = nodes,
            Lights = lights,
            CameraWorld = cameraWorld,
            CameraView = cameraView,
            Steps = steps.Count,
            Camera = steps[refIndex].Camera,
            FogMode = reference.FogMode,
            FogStart = reference.FogStart,
            FogEnd = reference.FogEnd,
            FogDensity = reference.FogDensity,
            FogColor = new Vector3(reference.FogColor.R, reference.FogColor.G, reference.FogColor.B),
            TriangleCount = triangles,
        };
    }

    private static void Collect(Element e, Matrix4x4 parentWorld, string path, Dictionary<string, Matrix4x4> into)
    {
        if (e is GroupElement g)
        {
            var world = Transforms.GroupLocal(g) * parentWorld;
            foreach (var child in g.Children) Collect(child, world, path + "/" + child.Name, into);
            return;
        }
        into[path] = Transforms.ElementLocal(e) * parentWorld;
    }

    /// <summary>The lights as the OpenGL renderers set them up: the first eight, the disabled ones skipped.</summary>
    private static LightData[] CompileLights(Scene scene)
    {
        var list = new List<LightData>();
        int slot = 0;
        foreach (var light in scene.Lights)
        {
            if (slot++ >= LightElement.MaxPerScene) break; // the original had GL_LIGHT0..7 only
            if (!light.Enabled) continue;
            var toWorld = Transforms.ParentChain(light);
            var pos = Vector3.Transform(new Vector3(light.X, light.Y, light.Z), toWorld);
            var dir = Vector3.TransformNormal(new Vector3(light.Alpha, light.Beta, light.Gamma), toWorld);
            float cutoff = light.SpotCutoff > 90 ? 180f : (float)Math.Max(0, light.SpotCutoff);
            list.Add(new LightData
            {
                Position = pos,
                SpotDirection = dir.LengthSquared() > 1e-12f ? Vector3.Normalize(dir) : new Vector3(0, 0, -1),
                Ambient = Rgb(light.Ambient),
                Diffuse = Rgb(light.Diffuse),
                Specular = Rgb(light.Specular),
                Spot = cutoff < 179.999f,
                CosCutoff = MathF.Cos(cutoff * MathF.PI / 180),
                Exponent = (float)Math.Clamp(light.SpotExponent, 0, 128),
                Constant = (float)light.ConstantAttenuation,
                Linear = (float)light.LinearAttenuation,
                Quadratic = (float)light.QuadraticAttenuation,
                Radius = Math.Max(0, light.LightRadius),
                CastsShadows = light.CastsShadows,
            });
        }
        return list.ToArray();
    }

    private static Vector3 Rgb(Overbryce.Core.Rgba c) => new(c.R, c.G, c.B);

    private static Material CompileMaterial(Element e, RaytraceOptions options, Dictionary<string, TextureImage?> textures)
    {
        TextureImage? tex = null;
        if (e.HasTexture && !textures.TryGetValue(e.Texture!, out tex))
        {
            tex = options.Textures(e.Texture!);
            textures[e.Texture!] = tex;
        }
        return new Material
        {
            Ambient = Rgb(e.Ambient),
            Diffuse = Rgb(e.Diffuse),
            Specular = Rgb(e.Specular),
            Emission = Rgb(e.Emission),
            Alpha = Math.Clamp(e.Diffuse.A, 0f, 1f),
            Shininess = Math.Clamp(e.Shininess, 0f, 1f) * 128f,
            Glow = Math.Max(0, e.Glow),
            // the same rules as the modern renderer: a negligible value is off
            Reflectivity = e.IsReflective ? Math.Clamp(e.Reflectivity, 0f, 1f) : 0f,
            Ior = e.IsGlass ? e.RefractiveIndex : 1f,
            Roughness = Math.Clamp(e.Roughness, 0f, 1f),
            Absorption = Math.Max(0, e.Absorption),
            Texture = tex,
            SphereMap = e is TorusElement,
        };
    }

    /// <summary>
    /// The geometry key and the scale (or shear) that turns the shared unit shape into this element's
    /// shape. Taking out the size lets equal shapes of different sizes share one BVH; the normals still
    /// come out as OpenGL computes them, because the normal matrix carries the same scale.
    /// </summary>
    private static (ShapeKey Key, Matrix4x4 Scale)? Canonical(Element e, bool smooth)
    {
        switch (e)
        {
            case SphereElement s:
                if (s.Radius > 0) return (new ShapeKey(ElementType.Sphere, 1, 0, 0, s.QualityX, s.QualityY, smooth), Matrix4x4.CreateScale((float)s.Radius));
                if (s.Radius < 0) return (new ShapeKey(ElementType.Sphere, s.Radius, 0, 0, s.QualityX, s.QualityY, false), Matrix4x4.Identity);
                return null;

            case ConeElement c:
                if (c.Height == 0 && c.BaseRadius == c.TopRadius) return null;
                if (c.BaseRadius > 0 && c.Height != 0)
                    return (new ShapeKey(ElementType.Cone, 1, c.TopRadius / c.BaseRadius, 1, c.QualityX, c.QualityY, smooth),
                        Matrix4x4.CreateScale((float)c.BaseRadius, (float)c.BaseRadius, (float)c.Height));
                return (new ShapeKey(ElementType.Cone, c.BaseRadius, c.TopRadius, c.Height, c.QualityX, c.QualityY, false), Matrix4x4.Identity);

            case DiskElement d:
                if (d.OuterRadius > 0)
                    return (new ShapeKey(ElementType.Disk, d.InnerRadius / d.OuterRadius, 1, d.SweepAngle, d.QualityX, d.QualityY, smooth),
                        Matrix4x4.CreateScale((float)d.OuterRadius));
                if (d.OuterRadius == d.InnerRadius) return null;
                return (new ShapeKey(ElementType.Disk, d.InnerRadius, d.OuterRadius, d.SweepAngle, d.QualityX, d.QualityY, false), Matrix4x4.Identity);

            case TorusElement t:
                if (t.RingRadius > 0)
                    return (new ShapeKey(ElementType.Torus, t.TubeRadius / t.RingRadius, 1, 0, t.QualityX, t.QualityY, smooth),
                        Matrix4x4.CreateScale(t.RingRadius));
                return (new ShapeKey(ElementType.Torus, t.TubeRadius, t.RingRadius, 0, t.QualityX, t.QualityY, false), Matrix4x4.Identity);

            case BoxElement b:
                if (b.SizeX != 0 && b.SizeY != 0 && b.SizeZ != 0)
                    return (new ShapeKey(ElementType.Box, 1, 1, 1, 0, 0, false),
                        Matrix4x4.CreateScale(MathF.Abs(b.SizeX), MathF.Abs(b.SizeY), MathF.Abs(b.SizeZ)));
                return (new ShapeKey(ElementType.Box, b.SizeX, b.SizeY, b.SizeZ, 0, 0, false), Matrix4x4.Identity);

            case RectangleElement r:
                if (r.SizeX == 0 || r.SizeY == 0) return null;
                return (new ShapeKey(ElementType.Rectangle, 1, 1, 0, 0, 0, false), Matrix4x4.CreateScale(r.SizeX, r.SizeY, 1));

            case TriangleElement tr:
                if (tr.X1 * tr.Y2 - tr.Y1 * tr.X2 == 0) return null;
                // (1,0,0) -> (x1,y1,0) and (0,1,0) -> (x2,y2,0); the unit triangle is (0,0), (1,0), (0,1)
                return (new ShapeKey(ElementType.Triangle, 1, 0, 0, 0, 0, false),
                    new Matrix4x4(tr.X1, tr.Y1, 0, 0, tr.X2, tr.Y2, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1));

            case MeshElement m:
                return (new ShapeKey(ElementType.Mesh, m.CountX, m.CountY, 0, m.QualityX, m.QualityY, false, new MeshKey(m.Points)), Matrix4x4.Identity);
        }
        return null;
    }

    private static Shape Build(in ShapeKey k)
    {
        if (k.Smooth)
        {
            switch (k.Type)
            {
                case ElementType.Sphere: return new SphereShape();
                case ElementType.Cone: return new ConeShape((float)k.B);
                case ElementType.Disk: return new DiskShape((float)k.A, (float)k.C);
                case ElementType.Torus: return new TorusShape((float)k.A);
            }
        }

        var g = new GeometryBuilder();
        var ranges = new List<DrawRange>();
        bool solid = false;
        switch (k.Type)
        {
            case ElementType.Sphere:
                g.Sphere(ranges, k.A, k.Q1, k.Q2, false);
                solid = true;
                break;
            case ElementType.Cone:
                g.Cylinder(ranges, k.A, k.B, k.C, k.Q1, k.Q2, false);
                break;
            case ElementType.Disk:
                g.PartialDisk(ranges, k.A, k.B, k.Q1, k.Q2, 0, k.C, false);
                break;
            case ElementType.Torus:
                g.Torus(ranges, k.A, k.Q1, k.B, k.Q2, false);
                solid = true;
                break;
            case ElementType.Box:
                g.Box(ranges, k.A, k.B, k.C, false);
                solid = true;
                break;
            case ElementType.Rectangle:
                g.Rectangle(ranges, k.A, k.B, false);
                break;
            case ElementType.Triangle:
                // the unit triangle with the vertex order and texture coordinates of the original
                g.Triangle(ranges, 1, 0, 0, 1, false);
                break;
            case ElementType.Mesh:
                g.BezierPatch(ranges, MeshPoints(k), (int)k.A, (int)k.B, k.Q1, k.Q2, false);
                break;
        }
        return new TriangleMesh(g.Data, ranges, solid);
    }

    private static float[] MeshPoints(in ShapeKey k) => k.Mesh!.Points;
}
