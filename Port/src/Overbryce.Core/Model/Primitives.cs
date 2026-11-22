using System.ComponentModel;
using Overbryce.Core.IO;

namespace Overbryce.Core.Model;

/// <summary>Sphere (original <c>CGula</c>, rendered with gluSphere).</summary>
public sealed class SphereElement : Element
{
    public SphereElement(string name = "NovaGula") : base(name) { }

    public override ElementType Type => ElementType.Sphere;

    [Category("8 Shape")] public double Radius { get; set; } = 2;
    [Category("8 Shape"), DisplayName("Slices")] public int QualityX { get; set; } = 22;
    [Category("8 Shape"), DisplayName("Stacks")] public int QualityY { get; set; } = 22;

    public override void Resize(float factor)
    {
        if (factor > 0) Radius *= factor;
    }

    public override void Interpolate(in SplineWeights w, float time, Element prev, Element next, Element nextNext)
    {
        base.Interpolate(w, time, prev, next, nextNext);
        var p = (SphereElement)prev; var n = (SphereElement)next; var nn = (SphereElement)nextNext;
        Radius = w.Interpolate(p.Radius, Radius, n.Radius, nn.Radius);
    }

    internal override void ReadBody(AniTokenReader r)
    {
        Radius = r.NextFloat();
        QualityX = r.NextInt(); QualityY = r.NextInt();
    }

    internal override void Write(AniTokenWriter w)
    {
        WriteCommon(w, AniTags.Sphere);
        w.Write(Radius); w.Write(QualityX); w.Write(QualityY);
    }
}

/// <summary>Truncated cone / cylinder (original <c>CZrezkuzel</c>, rendered with gluCylinder along +Z).</summary>
public sealed class ConeElement : Element
{
    public ConeElement(string name = "NovyKuzel") : base(name) { }

    public override ElementType Type => ElementType.Cone;

    [Category("8 Shape"), DisplayName("Base radius"), Description("Radius at z = 0.")]
    public double BaseRadius { get; set; } = 1;

    [Category("8 Shape"), DisplayName("Top radius"), Description("Radius at z = Height.")]
    public double TopRadius { get; set; } = 2;

    [Category("8 Shape")] public double Height { get; set; } = 2;
    [Category("8 Shape"), DisplayName("Slices")] public int QualityX { get; set; } = 22;
    [Category("8 Shape"), DisplayName("Stacks")] public int QualityY { get; set; } = 22;

    public override void Resize(float factor)
    {
        if (factor <= 0) return;
        BaseRadius *= factor; TopRadius *= factor; Height *= factor;
    }

    public override void Interpolate(in SplineWeights w, float time, Element prev, Element next, Element nextNext)
    {
        base.Interpolate(w, time, prev, next, nextNext);
        var p = (ConeElement)prev; var n = (ConeElement)next; var nn = (ConeElement)nextNext;
        BaseRadius = w.Interpolate(p.BaseRadius, BaseRadius, n.BaseRadius, nn.BaseRadius);
        TopRadius = w.Interpolate(p.TopRadius, TopRadius, n.TopRadius, nn.TopRadius);
        Height = w.Interpolate(p.Height, Height, n.Height, nn.Height);
    }

    internal override void ReadBody(AniTokenReader r)
    {
        BaseRadius = r.NextFloat(); TopRadius = r.NextFloat(); Height = r.NextFloat();
        QualityX = r.NextInt(); QualityY = r.NextInt();
    }

    internal override void Write(AniTokenWriter w)
    {
        WriteCommon(w, AniTags.Cone);
        w.Write(BaseRadius); w.Write(TopRadius); w.Write(Height);
        w.Write(QualityX); w.Write(QualityY);
    }
}

/// <summary>Disk / ring / sector (original <c>CDisk</c>, rendered with gluPartialDisk).</summary>
public sealed class DiskElement : Element
{
    public DiskElement(string name = "NovyDisk") : base(name) { }

    public override ElementType Type => ElementType.Disk;

    [Category("8 Shape"), DisplayName("Inner radius")] public double InnerRadius { get; set; } = 1;
    [Category("8 Shape"), DisplayName("Outer radius")] public double OuterRadius { get; set; } = 2;
    [Category("8 Shape"), DisplayName("Sweep angle"), Description("360 = closed disk, less = sector.")]
    public double SweepAngle { get; set; } = 270;
    [Category("8 Shape"), DisplayName("Slices")] public int QualityX { get; set; } = 22;
    [Category("8 Shape"), DisplayName("Loops")] public int QualityY { get; set; } = 22;

    public override void Resize(float factor)
    {
        if (factor <= 0) return;
        InnerRadius *= factor; OuterRadius *= factor;
    }

    public override void Interpolate(in SplineWeights w, float time, Element prev, Element next, Element nextNext)
    {
        base.Interpolate(w, time, prev, next, nextNext);
        var p = (DiskElement)prev; var n = (DiskElement)next; var nn = (DiskElement)nextNext;
        // The original used a different (wrong) end-point formula for the disk only, which only
        // agreed with the other elements for evenly spaced key states. Fixed to the common formula.
        InnerRadius = w.Interpolate(p.InnerRadius, InnerRadius, n.InnerRadius, nn.InnerRadius);
        OuterRadius = w.Interpolate(p.OuterRadius, OuterRadius, n.OuterRadius, nn.OuterRadius);
        SweepAngle = w.Interpolate(p.SweepAngle, SweepAngle, n.SweepAngle, nn.SweepAngle);
    }

    internal override void ReadBody(AniTokenReader r)
    {
        InnerRadius = r.NextFloat(); OuterRadius = r.NextFloat(); SweepAngle = r.NextFloat();
        QualityX = r.NextInt(); QualityY = r.NextInt();
    }

    internal override void Write(AniTokenWriter w)
    {
        WriteCommon(w, AniTags.Disk);
        w.Write(InnerRadius); w.Write(OuterRadius); w.Write(SweepAngle);
        w.Write(QualityX); w.Write(QualityY);
    }
}

/// <summary>Box centred on its position (original <c>CKvader</c>).</summary>
public sealed class BoxElement : Element
{
    public BoxElement(string name = "NovyKvader") : base(name) { }

    public override ElementType Type => ElementType.Box;

    [Category("8 Shape"), DisplayName("Size X")] public float SizeX { get; set; } = 2;
    [Category("8 Shape"), DisplayName("Size Y")] public float SizeY { get; set; } = 2;
    [Category("8 Shape"), DisplayName("Size Z")] public float SizeZ { get; set; } = 2;

    public override void Interpolate(in SplineWeights w, float time, Element prev, Element next, Element nextNext)
    {
        base.Interpolate(w, time, prev, next, nextNext);
        var p = (BoxElement)prev; var n = (BoxElement)next; var nn = (BoxElement)nextNext;
        SizeX = w.Interpolate(p.SizeX, SizeX, n.SizeX, nn.SizeX);
        SizeY = w.Interpolate(p.SizeY, SizeY, n.SizeY, nn.SizeY);
        SizeZ = w.Interpolate(p.SizeZ, SizeZ, n.SizeZ, nn.SizeZ);
    }

    internal override void ReadBody(AniTokenReader r)
    {
        SizeX = r.NextFloat(); SizeY = r.NextFloat(); SizeZ = r.NextFloat();
    }

    internal override void Write(AniTokenWriter w)
    {
        WriteCommon(w, AniTags.Box);
        w.Write(SizeX); w.Write(SizeY); w.Write(SizeZ);
    }
}

/// <summary>Rectangle in the local XY plane, centred on its position (original <c>CObdlznik</c>).</summary>
public sealed class RectangleElement : Element
{
    public RectangleElement(string name = "NovyObdlznik") : base(name) { }

    public override ElementType Type => ElementType.Rectangle;

    [Category("8 Shape"), DisplayName("Size X")] public float SizeX { get; set; } = 2;
    [Category("8 Shape"), DisplayName("Size Y")] public float SizeY { get; set; } = 2;

    public override void Interpolate(in SplineWeights w, float time, Element prev, Element next, Element nextNext)
    {
        base.Interpolate(w, time, prev, next, nextNext);
        var p = (RectangleElement)prev; var n = (RectangleElement)next; var nn = (RectangleElement)nextNext;
        SizeX = w.Interpolate(p.SizeX, SizeX, n.SizeX, nn.SizeX);
        SizeY = w.Interpolate(p.SizeY, SizeY, n.SizeY, nn.SizeY);
    }

    internal override void ReadBody(AniTokenReader r)
    {
        SizeX = r.NextFloat(); SizeY = r.NextFloat();
    }

    internal override void Write(AniTokenWriter w)
    {
        WriteCommon(w, AniTags.Rectangle);
        w.Write(SizeX); w.Write(SizeY);
    }
}

/// <summary>Triangle (0,0)-(X1,Y1)-(X2,Y2) in the local XY plane (original <c>CTrojuholnik</c>).</summary>
public sealed class TriangleElement : Element
{
    public TriangleElement(string name = "NovyTrojuholnik") : base(name) { }

    public override ElementType Type => ElementType.Triangle;

    [Category("8 Shape")] public float X1 { get; set; } = 2;
    [Category("8 Shape")] public float Y1 { get; set; }
    [Category("8 Shape")] public float X2 { get; set; }
    [Category("8 Shape")] public float Y2 { get; set; } = 3;

    public override void Interpolate(in SplineWeights w, float time, Element prev, Element next, Element nextNext)
    {
        base.Interpolate(w, time, prev, next, nextNext);
        var p = (TriangleElement)prev; var n = (TriangleElement)next; var nn = (TriangleElement)nextNext;
        X1 = w.Interpolate(p.X1, X1, n.X1, nn.X1);
        X2 = w.Interpolate(p.X2, X2, n.X2, nn.X2);
        Y1 = w.Interpolate(p.Y1, Y1, n.Y1, nn.Y1);
        Y2 = w.Interpolate(p.Y2, Y2, n.Y2, nn.Y2);
    }

    internal override void ReadBody(AniTokenReader r)
    {
        X1 = r.NextFloat(); Y1 = r.NextFloat(); X2 = r.NextFloat(); Y2 = r.NextFloat();
    }

    internal override void Write(AniTokenWriter w)
    {
        WriteCommon(w, AniTags.Triangle);
        w.Write(X1); w.Write(Y1); w.Write(X2); w.Write(Y2);
    }
}

/// <summary>Torus around the local Z axis (original <c>CTorus</c>), sphere-mapped texture.</summary>
public sealed class TorusElement : Element
{
    public TorusElement(string name = "NovyTorus") : base(name) { }

    public override ElementType Type => ElementType.Torus;

    [Category("8 Shape"), DisplayName("Tube radius"), Description("Radius of the tube (original rin).")]
    public float TubeRadius { get; set; } = 1;

    [Category("8 Shape"), DisplayName("Ring radius"), Description("Distance of the tube centre from the axis (original rout).")]
    public float RingRadius { get; set; } = 2;

    [Category("8 Shape"), DisplayName("Tube sides")] public int QualityX { get; set; } = 22;
    [Category("8 Shape"), DisplayName("Ring segments")] public int QualityY { get; set; } = 22;

    public override void Interpolate(in SplineWeights w, float time, Element prev, Element next, Element nextNext)
    {
        base.Interpolate(w, time, prev, next, nextNext);
        var p = (TorusElement)prev; var n = (TorusElement)next; var nn = (TorusElement)nextNext;
        TubeRadius = w.Interpolate(p.TubeRadius, TubeRadius, n.TubeRadius, nn.TubeRadius);
        RingRadius = w.Interpolate(p.RingRadius, RingRadius, n.RingRadius, nn.RingRadius);
    }

    internal override void ReadBody(AniTokenReader r)
    {
        TubeRadius = r.NextFloat(); RingRadius = r.NextFloat();
        QualityX = r.NextInt(); QualityY = r.NextInt();
    }

    internal override void Write(AniTokenWriter w)
    {
        WriteCommon(w, AniTags.Torus);
        w.Write(TubeRadius); w.Write(RingRadius); w.Write(QualityX); w.Write(QualityY);
    }
}

/// <summary>
/// Bezier surface given by a grid of control points (original <c>CMesh</c>, rendered with
/// OpenGL evaluators glMap2f/glEvalMesh2). Interpolating the control points morphs the surface.
/// </summary>
public sealed class MeshElement : Element
{
    private float[] _points;

    public MeshElement(string name = "NovyMesh") : this(name, MeshPresets.Mesh1, MeshPresets.Size, MeshPresets.Size) { }

    public MeshElement(string name, float[] points, int countX, int countY) : base(name)
    {
        _points = (float[])points.Clone();
        CountX = countX;
        CountY = countY;
    }

    public override ElementType Type => ElementType.Mesh;

    /// <summary>Control points laid out as [CountY][CountX][3].</summary>
    [Browsable(false)]
    public float[] Points => _points;

    [Category("8 Shape"), DisplayName("Control points X"), ReadOnly(true)] public int CountX { get; private set; }
    [Category("8 Shape"), DisplayName("Control points Y"), ReadOnly(true)] public int CountY { get; private set; }
    [Category("8 Shape"), DisplayName("Grid U")] public int QualityX { get; set; } = 10;
    [Category("8 Shape"), DisplayName("Grid V")] public int QualityY { get; set; } = 10;

    public void SetControlPoints(float[] points, int countX, int countY)
    {
        if (points.Length != countX * countY * 3)
            throw new ArgumentException("Point array does not match the grid size.", nameof(points));
        _points = (float[])points.Clone();
        CountX = countX;
        CountY = countY;
    }

    protected override void CloneDeepParts()
    {
        _points = (float[])_points.Clone();
    }

    public override void Resize(float factor)
    {
        if (factor <= 0) return;
        for (int i = 0; i < _points.Length; i++) _points[i] *= factor;
    }

    public override void Interpolate(in SplineWeights w, float time, Element prev, Element next, Element nextNext)
    {
        base.Interpolate(w, time, prev, next, nextNext);
        var p = (MeshElement)prev; var n = (MeshElement)next; var nn = (MeshElement)nextNext;
        if (p._points.Length != _points.Length || n._points.Length != _points.Length || nn._points.Length != _points.Length)
            return; // incompatible grids cannot be morphed (the original would read out of bounds)
        for (int i = 0; i < _points.Length; i++)
            _points[i] = w.Interpolate(p._points[i], _points[i], n._points[i], nn._points[i]);
    }

    internal override void ReadBody(AniTokenReader r)
    {
        int cx = r.NextInt(), cy = r.NextInt();
        if (cx <= 0 || cy <= 0 || cx * cy > 4096)
            throw new AniFormatException($"Invalid mesh size {cx}x{cy}.");
        var pts = new float[cx * cy * 3];
        for (int i = 0; i < pts.Length; i++) pts[i] = r.NextFloat();
        _points = pts;
        CountX = cx; CountY = cy;
        QualityX = r.NextInt(); QualityY = r.NextInt();
    }

    internal override void Write(AniTokenWriter w)
    {
        WriteCommon(w, AniTags.Mesh);
        w.Write(CountX); w.Write(CountY);
        foreach (float f in _points) w.Write(f);
        w.Write(QualityX); w.Write(QualityY);
    }
}

/// <summary>
/// Positional (spot) light (original <c>CSvetlo</c>). The light colour uses the material
/// colours of the base class; Alpha/Beta/Gamma are the spot direction <b>vector</b>.
/// At most 8 lights per state are used (OpenGL GL_LIGHT0..7).
/// </summary>
public sealed class LightElement : Element
{
    public const int MaxPerScene = 8;

    public LightElement(string name = "NoveSvetlo") : base(name) { }

    public override ElementType Type => ElementType.Light;

    [Category("8 Light"), DisplayName("On")] public bool Enabled { get; set; } = true;

    [Category("8 Light"), DisplayName("Spot cutoff"), Description("Half-angle of the light cone in degrees; 180 = omnidirectional point light.")]
    public double SpotCutoff { get; set; } = 180;

    [Category("8 Light"), DisplayName("Spot exponent"), Description("0..128, concentration of the spot.")]
    public double SpotExponent { get; set; }

    [Category("8 Light"), DisplayName("Constant attenuation")] public double ConstantAttenuation { get; set; } = 1;
    [Category("8 Light"), DisplayName("Linear attenuation")] public double LinearAttenuation { get; set; }
    [Category("8 Light"), DisplayName("Quadratic attenuation")] public double QuadraticAttenuation { get; set; }

    [Category("8 Light"), DisplayName("Radius"), Description("Size of the light as a sphere: above 0 its shadows get soft edges, wider the farther they fall from the object. At 0 the light is a point; a glowing element around it (a sun, a bulb) then lightens the shadows of elements too small to hide it (ray tracing only).")]
    public float LightRadius { get; set; }

    public override void Interpolate(in SplineWeights w, float time, Element prev, Element next, Element nextNext)
    {
        base.Interpolate(w, time, prev, next, nextNext);
        var p = (LightElement)prev; var n = (LightElement)next; var nn = (LightElement)nextNext;
        LightRadius = Math.Max(0f, w.Interpolate(p.LightRadius, LightRadius, n.LightRadius, nn.LightRadius));
        if (LightRadius <= NegligibleSurface) LightRadius = 0f; // rounding of the spline, not a size
        SpotCutoff = w.Interpolate(p.SpotCutoff, SpotCutoff, n.SpotCutoff, nn.SpotCutoff);
        if (SpotCutoff > 90) SpotCutoff = 180;
        SpotExponent = w.Interpolate(p.SpotExponent, SpotExponent, n.SpotExponent, nn.SpotExponent);
        if (SpotExponent > 128) SpotExponent = 128;
        LinearAttenuation = w.Interpolate(p.LinearAttenuation, LinearAttenuation, n.LinearAttenuation, nn.LinearAttenuation);
        ConstantAttenuation = w.Interpolate(p.ConstantAttenuation, ConstantAttenuation, n.ConstantAttenuation, nn.ConstantAttenuation);
        QuadraticAttenuation = w.Interpolate(p.QuadraticAttenuation, QuadraticAttenuation, n.QuadraticAttenuation, nn.QuadraticAttenuation);
    }

    internal override void ReadBody(AniTokenReader r)
    {
        Enabled = r.NextBool();
        SpotExponent = r.NextFloat();
        SpotCutoff = r.NextFloat();
        ConstantAttenuation = r.NextFloat();
        LinearAttenuation = r.NextFloat();
        QuadraticAttenuation = r.NextFloat();
    }

    internal override void Write(AniTokenWriter w)
    {
        WriteCommon(w, AniTags.Light);
        w.Write(Enabled);
        w.Write(SpotExponent); w.Write(SpotCutoff);
        w.Write(ConstantAttenuation); w.Write(LinearAttenuation); w.Write(QuadraticAttenuation);
    }
}

/// <summary>Camera (original <c>CKamera</c>). Looks along its local -Z axis.</summary>
public sealed class CameraElement : Element
{
    public const int MaxPerScene = 8;

    public CameraElement(string name = "NovaKamera") : base(name) { }

    public override ElementType Type => ElementType.Camera;

    [Category("8 Camera"), DisplayName("Field of view"), Description("Vertical field of view in degrees.")]
    public float FieldOfView { get; set; } = 35;

    [Category("8 Camera"), DisplayName("Active"), Description("The state is rendered through the active camera. Only one camera per state can be active.")]
    public bool Active { get; set; }

    public const float DefaultFocus = 10f;

    [Category("8 Camera"), DisplayName("Aperture"), Description("Radius of the lens in scene units: above 0, what is not at the focus distance gets blurred (depth of field; ray tracing only).")]
    public float Aperture { get; set; }

    [Category("8 Camera"), DisplayName("Focus distance"), Description("Distance from the camera that is sharp when the aperture is above 0 (ray tracing only).")]
    public float Focus { get; set; } = DefaultFocus;

    /// <summary>The clipping distances the original used for every camera.</summary>
    public const float DefaultNear = 0.1f, DefaultFar = 100f;

    [Category("8 Camera"), DisplayName("Clip start"), Description("Nearest distance from the camera that is drawn (0.1 in the original).")]
    public float Near { get; set; } = DefaultNear;

    [Category("8 Camera"), DisplayName("Clip end"), Description("Farthest distance from the camera that is drawn (100 in the original).")]
    public float Far { get; set; } = DefaultFar;

    public override void Interpolate(in SplineWeights w, float time, Element prev, Element next, Element nextNext)
    {
        base.Interpolate(w, time, prev, next, nextNext);
        var p = (CameraElement)prev; var n = (CameraElement)next; var nn = (CameraElement)nextNext;
        FieldOfView = w.Interpolate(p.FieldOfView, FieldOfView, n.FieldOfView, nn.FieldOfView);
        if (p.Near != Near || n.Near != Near || nn.Near != Near) Near = Math.Max(1e-4f, w.Interpolate(p.Near, Near, n.Near, nn.Near));
        if (p.Far != Far || n.Far != Far || nn.Far != Far) Far = Math.Max(Near * 1.001f, w.Interpolate(p.Far, Far, n.Far, nn.Far));
        Aperture = Math.Max(0f, w.Interpolate(p.Aperture, Aperture, n.Aperture, nn.Aperture));
        if (Aperture <= NegligibleSurface) Aperture = 0f; // rounding of the spline, not a lens
        Focus = Math.Max(0.01f, w.Interpolate(p.Focus, Focus, n.Focus, nn.Focus));
    }

    internal override void ReadBody(AniTokenReader r)
    {
        FieldOfView = r.NextFloat();
        Active = r.NextBool();
    }

    internal override void Write(AniTokenWriter w)
    {
        WriteCommon(w, AniTags.Camera);
        w.Write(FieldOfView); w.Write(Active);
    }
}
