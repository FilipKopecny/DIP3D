using System.ComponentModel;
using Overbryce.Core.IO;

namespace Overbryce.Core.Model;

public enum ElementType
{
    Light,
    Sphere,
    Disk,
    Cone,
    Mesh,
    Box,
    Rectangle,
    Triangle,
    Torus,
    Camera,
    Group,
    Scene,
}

public enum DrawStyle
{
    Fill,
    Line,
}

/// <summary>
/// Base class of every scene element (original: <c>CPrvok</c>). Holds the placement, the material,
/// the texture reference and the "general movement" (permanent rotation / orbit) attributes.
/// </summary>
public abstract class Element
{
    public const int MaxNameLength = 20;
    private const double DegToRad = Math.PI / 180.0;

    private string _name = "Element";

    protected Element(string name)
    {
        Name = name;
    }

    [Browsable(false)]
    public abstract ElementType Type { get; }

    /// <summary>The group (or scene) this element lives in.</summary>
    [Browsable(false)]
    public GroupElement? Parent { get; internal set; }

    // ------------------------------------------------------------------ general

    [Category("1 General"), Description("Element name. Elements are matched between key states by name (max. 20 characters, no spaces).")]
    public string Name
    {
        get => _name;
        set => _name = SanitizeName(value);
    }

    [Category("1 General"), DisplayName("Draw style"), Description("Filled or wire-frame rendering in the shaded view.")]
    public DrawStyle DrawStyle { get; set; } = DrawStyle.Fill;

    [Category("1 General"), DisplayName("Texture"), Description("128x128 bitmap mapped onto the element (empty = no texture). Pure white texels are transparent when alpha blending is on.")]
    [Editor("Overbryce.App.UI.TextureFileEditor, Overbryce", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string? Texture { get; set; }

    // ------------------------------------------------------------------ placement

    [Category("2 Position")] public float X { get; set; }
    [Category("2 Position")] public float Y { get; set; }
    [Category("2 Position")] public float Z { get; set; }

    [Category("3 Rotation"), DisplayName("Alpha (X)"), Description("Rotation about the X axis in degrees.")]
    public float Alpha { get; set; }

    [Category("3 Rotation"), DisplayName("Beta (Y)"), Description("Rotation about the Y axis in degrees.")]
    public float Beta { get; set; }

    [Category("3 Rotation"), DisplayName("Gamma (Z)"), Description("Rotation about the Z axis in degrees.")]
    public float Gamma { get; set; }

    // ------------------------------------------------------------------ material

    [Category("4 Material")] public Rgba Specular { get; set; } = new(1f, 1f, 1f, 1f);
    [Category("4 Material")] public Rgba Diffuse { get; set; } = new(0.8f, 0.8f, 0.8f, 1f);
    [Category("4 Material")] public Rgba Ambient { get; set; } = new(0.6f, 0.6f, 0.6f, 1f);
    [Category("4 Material")] public Rgba Emission { get; set; } = new(0.1f, 0.1f, 0.2f, 0.6f);

    [Category("4 Material"), Description("0..1, multiplied by 128 to get the OpenGL specular exponent.")]
    public float Shininess { get; set; } = 0.8f;

    // ------------------------------------------------------------------ modern rendering (2.1)
    // Stored in the extension block after the original format (see AniExtensions); the classic
    // renderer ignores them.

    [Category("9 Modern rendering"), DisplayName("Reflectivity"), Description("0..1: how much the surface mirrors its surroundings, more at grazing angles (modern rendering only).")]
    public float Reflectivity { get; set; }

    [Category("9 Modern rendering"), DisplayName("Refractive index"), Description("1 = off. Above 1 the see-through part of a translucent element (diffuse alpha below 1) bends the light like glass: 1.33 water, 1.5 glass (modern rendering only).")]
    public float RefractiveIndex { get; set; } = 1f;

    [Category("9 Modern rendering"), DisplayName("Glow"), Description("Multiplies the emission colour: above 1 the element shines brighter than white and glows (modern rendering only).")]
    public float Glow { get; set; } = 1f;

    [Category("9 Modern rendering"), DisplayName("Casts shadows"), Description("The element casts shadows (modern rendering and ray tracing). An element that encloses a light never shadows that light.")]
    public bool CastsShadows { get; set; } = true;

    [Category("9 Modern rendering"), DisplayName("Roughness"), Description("0..1: blurs the reflection and the refraction (glossy metal, frosted glass). Ray tracing only.")]
    public float Roughness { get; set; }

    [Category("9 Modern rendering"), DisplayName("Absorption"), Description("How much a glass element tints the light per scene unit it travels inside, towards its diffuse colour (0 = clear). Ray tracing only.")]
    public float Absorption { get; set; }

    /// <summary>Surface values closer than this to their defaults count as the defaults.</summary>
    public const float NegligibleSurface = 1e-3f;

    /// <summary>The element mirrors its surroundings (used by the modern renderer and the ray tracer).</summary>
    [Browsable(false)]
    public bool IsReflective => Reflectivity > NegligibleSurface;

    /// <summary>The element refracts like glass: a refractive index above 1 and a see-through diffuse colour.</summary>
    [Browsable(false)]
    public bool IsGlass => RefractiveIndex > 1f + NegligibleSurface && Diffuse.A < 0.999f;

    /// <summary>Extension values this version does not know (from a newer version); kept and written back unchanged.</summary>
    [Browsable(false)]
    public Dictionary<string, string>? ExtensionData { get; set; }

    // ------------------------------------------------------------------ animation

    /// <summary>
    /// Original <c>zaciatokPohybu</c> (the "Final" check box): the element's motion stops and starts
    /// at this key state (velocity zero, acceleration from / deceleration to rest), so the path may
    /// change direction here. Without it the motion flows smoothly through the key state.
    /// <para>
    /// FIX: the original only halved the velocity (it doubled the key state), ignored the flag for
    /// elements inside groups and switched it on for every new element.
    /// </para>
    /// </summary>
    [Category("5 Animation"), DisplayName("Stop at this state"), Description("Start/end of motion (the original 'Final' check box): the element comes to rest at this key state and starts again from rest, so its path may turn sharply here. Off = the motion flows smoothly through the key state.")]
    public bool MotionBoundary { get; set; }

    /// <summary>Original <c>koniecPohybu</c>; stored in files but never used.</summary>
    [Browsable(false)]
    public int MotionEnd { get; set; } = 1;

    [Category("6 Permanent rotation"), DisplayName("Angle X"), Description("Degrees added to Alpha every 'Period' seconds.")]
    public float RotationAngleX { get; set; }
    [Category("6 Permanent rotation"), DisplayName("Angle Y")] public float RotationAngleY { get; set; }
    [Category("6 Permanent rotation"), DisplayName("Angle Z")] public float RotationAngleZ { get; set; }
    [Category("6 Permanent rotation"), DisplayName("Period [s]")] public float RotationPeriod { get; set; }
    [Category("6 Permanent rotation"), DisplayName("Enabled")] public bool RotationPermanent { get; set; }

    [Category("7 Permanent orbit"), DisplayName("Angle X"), Description("Degrees the position orbits about the parent's X axis every 'Period' seconds.")]
    public float OrbitAngleX { get; set; }
    [Category("7 Permanent orbit"), DisplayName("Angle Y")] public float OrbitAngleY { get; set; }
    [Category("7 Permanent orbit"), DisplayName("Angle Z")] public float OrbitAngleZ { get; set; }
    [Category("7 Permanent orbit"), DisplayName("Period [s]")] public float OrbitPeriod { get; set; }
    [Category("7 Permanent orbit"), DisplayName("Enabled")] public bool OrbitPermanent { get; set; }

    /// <summary>Stored in the file but unused by the original (orbits are always about the origin).</summary>
    [Browsable(false)] public float OrbitCenterX { get; set; }
    [Browsable(false)] public float OrbitCenterY { get; set; }
    [Browsable(false)] public float OrbitCenterZ { get; set; }

    /// <summary>
    /// Permanent rotation (degrees about X/Y/Z) accumulated in the key-state intervals before the
    /// one being interpolated. Set by <see cref="Animation"/> on in-between copies only.
    /// </summary>
    internal (float X, float Y, float Z) RotationCarry { get; set; }

    /// <summary>Permanent orbit (degrees about X/Y/Z) accumulated in earlier key-state intervals.</summary>
    internal (float X, float Y, float Z) OrbitCarry { get; set; }

    /// <summary>Texture dimensions as stored in the file (128 when a texture is set, 0 otherwise).</summary>
    [Browsable(false)]
    public bool HasTexture => !string.IsNullOrEmpty(Texture);

    // ------------------------------------------------------------------ helpers

    public static string SanitizeName(string? value)
    {
        string s = string.IsNullOrWhiteSpace(value) ? "NoName" : value.Trim();
        s = s.Replace(' ', '_').Replace('\t', '_');
        return s.Length > MaxNameLength ? s[..MaxNameLength] : s;
    }

    public override string ToString() => $"{Name} ({Type})";

    /// <summary>Deep copy (original: the <c>CPrvok(CPrvok* source)</c> copy constructors).</summary>
    public Element Clone()
    {
        var copy = (Element)MemberwiseClone();
        copy.Parent = null;
        if (ExtensionData != null) copy.ExtensionData = new Dictionary<string, string>(ExtensionData, StringComparer.Ordinal);
        copy.CloneDeepParts();
        return copy;
    }

    /// <summary>Called on the shallow copy to duplicate reference-type state.</summary>
    protected virtual void CloneDeepParts() { }

    /// <summary>Uniform scaling of the element's size parameters (original: <c>Resize</c>).</summary>
    public virtual void Resize(float factor) { }

    // ------------------------------------------------------------------ interpolation

    /// <summary>
    /// Turns this element (a copy of the element in the first key state) into the in-between
    /// state. <paramref name="time"/> is the time elapsed since the first key state and drives the
    /// permanent rotation / orbit.
    /// </summary>
    public virtual void Interpolate(in SplineWeights w, float time, Element prev, Element next, Element nextNext)
    {
        InterpolateCommon(w, time, prev, next, nextNext);
    }

    /// <summary>Original <c>CPrvok::VypocitajMedziprvokPrvok</c>.</summary>
    protected void InterpolateCommon(in SplineWeights w, float time, Element prev, Element next, Element nn)
    {
        X = w.Interpolate(prev.X, X, next.X, nn.X);
        Y = w.Interpolate(prev.Y, Y, next.Y, nn.Y);
        Z = w.Interpolate(prev.Z, Z, next.Z, nn.Z);
        Alpha = w.Interpolate(prev.Alpha, Alpha, next.Alpha, nn.Alpha);
        Gamma = w.Interpolate(prev.Gamma, Gamma, next.Gamma, nn.Gamma);
        Beta = w.Interpolate(prev.Beta, Beta, next.Beta, nn.Beta);

        // NOTE: the original interpolated the diffuse colour twice in a row (the second time
        // feeding the already interpolated value back in). That was a copy & paste slip; the port
        // interpolates every attribute exactly once.
        Diffuse = Rgba.Interpolate(w, prev.Diffuse, Diffuse, next.Diffuse, nn.Diffuse);
        Ambient = Rgba.Interpolate(w, prev.Ambient, Ambient, next.Ambient, nn.Ambient);
        Specular = Rgba.Interpolate(w, prev.Specular, Specular, next.Specular, nn.Specular);
        Emission = Rgba.Interpolate(w, prev.Emission, Emission, next.Emission, nn.Emission);
        Shininess = Math.Clamp(w.Interpolate(prev.Shininess, Shininess, next.Shininess, nn.Shininess), 0f, 1f);
        Reflectivity = Math.Clamp(w.Interpolate(prev.Reflectivity, Reflectivity, next.Reflectivity, nn.Reflectivity), 0f, 1f);
        RefractiveIndex = Math.Max(1f, w.Interpolate(prev.RefractiveIndex, RefractiveIndex, next.RefractiveIndex, nn.RefractiveIndex));
        Glow = Math.Max(0f, w.Interpolate(prev.Glow, Glow, next.Glow, nn.Glow));
        Roughness = Math.Clamp(w.Interpolate(prev.Roughness, Roughness, next.Roughness, nn.Roughness), 0f, 1f);
        Absorption = Math.Max(0f, w.Interpolate(prev.Absorption, Absorption, next.Absorption, nn.Absorption));

        // the spline weights do not sum to exactly 1, so a value that is the default in every key
        // state comes out as 1.0000001 and the like; snap it back, so plain elements stay plain
        if (Reflectivity <= NegligibleSurface) Reflectivity = 0f;
        if (RefractiveIndex <= 1f + NegligibleSurface) RefractiveIndex = 1f;
        if (Roughness <= NegligibleSurface) Roughness = 0f;
        if (Absorption <= NegligibleSurface) Absorption = 0f;
        if (MathF.Abs(Glow - 1f) <= NegligibleSurface) Glow = 1f;

        ApplyPermanentMotion(time);
    }

    /// <summary>
    /// Permanent rotation and orbit, original end of <c>VypocitajMedziprvokPrvok</c>.
    /// <para>
    /// FIX: the original measured <paramref name="time"/> from the start of the current key-state
    /// interval only, so the rotation restarted at every key state and jumped unless each interval
    /// happened to be a whole number of turns. The angle now also includes what was accumulated in
    /// the earlier intervals (<see cref="RotationCarry"/>, <see cref="OrbitCarry"/>), so permanent
    /// motion runs continuously through the whole animation.
    /// </para>
    /// </summary>
    private void ApplyPermanentMotion(float time)
    {
        var rot = RotationCarry;
        if (RotationPermanent && RotationPeriod != 0)
        {
            rot.X += time * RotationAngleX / RotationPeriod;
            rot.Y += time * RotationAngleY / RotationPeriod;
            rot.Z += time * RotationAngleZ / RotationPeriod;
        }
        Alpha += rot.X;
        Beta += rot.Y;
        Gamma += rot.Z;

        var orbit = OrbitCarry;
        if (OrbitPermanent && OrbitPeriod != 0)
        {
            orbit.X += time * OrbitAngleX / OrbitPeriod;
            orbit.Y += time * OrbitAngleY / OrbitPeriod;
            orbit.Z += time * OrbitAngleZ / OrbitPeriod;
        }
        if (orbit.X != 0) OrbitAboutX(orbit.X);
        if (orbit.Y != 0) OrbitAboutY(orbit.Y);
        if (orbit.Z != 0) OrbitAboutZ(orbit.Z);
    }

    /// <summary>
    /// Applies only the permanent motion accumulated in earlier intervals (<see cref="RotationCarry"/>,
    /// <see cref="OrbitCarry"/>) to this element and its descendants: the state of a key state when
    /// it is reached at the end of the animation.
    /// </summary>
    internal void ApplyCarriedMotion()
    {
        ApplyPermanentMotion(0);
        if (this is GroupElement g)
            foreach (var child in g.Children)
                child.ApplyCarriedMotion();
    }

    /// <summary>Permanent rotation / orbit this element performs during an interval of <paramref name="dt"/> seconds.</summary>
    internal ((float X, float Y, float Z) Rotation, (float X, float Y, float Z) Orbit) PermanentMotionOver(float dt)
    {
        var rot = RotationPermanent && RotationPeriod != 0
            ? (dt * RotationAngleX / RotationPeriod, dt * RotationAngleY / RotationPeriod, dt * RotationAngleZ / RotationPeriod)
            : (0f, 0f, 0f);
        var orbit = OrbitPermanent && OrbitPeriod != 0
            ? (dt * OrbitAngleX / OrbitPeriod, dt * OrbitAngleY / OrbitPeriod, dt * OrbitAngleZ / OrbitPeriod)
            : (0f, 0f, 0f);
        return (rot, orbit);
    }

    /// <summary>Rotates the element's position about the X axis through the origin and turns it accordingly.</summary>
    public void OrbitAboutX(float degrees)
    {
        Alpha += degrees;
        double a = degrees * DegToRad, s = Math.Sin(a), c = Math.Cos(a);
        float ty = (float)(Y * c - Z * s);
        float tz = (float)(Y * s + Z * c);
        Y = ty; Z = tz;
    }

    public void OrbitAboutY(float degrees)
    {
        Beta += degrees;
        double a = degrees * DegToRad, s = Math.Sin(a), c = Math.Cos(a);
        float tx = (float)(X * c + Z * s);
        float tz = (float)(-X * s + Z * c);
        X = tx; Z = tz;
    }

    public void OrbitAboutZ(float degrees)
    {
        Gamma += degrees;
        double a = degrees * DegToRad, s = Math.Sin(a), c = Math.Cos(a);
        float tx = (float)(X * c - Y * s);
        float ty = (float)(X * s + Y * c);
        X = tx; Y = ty;
    }

    // ------------------------------------------------------------------ file IO

    /// <summary>Original <c>CPrvok::ReadYouPrvok</c>: everything after the type tag up to the draw style.</summary>
    internal void ReadCommon(AniTokenReader r)
    {
        Name = r.Next();
        X = r.NextFloat(); Y = r.NextFloat(); Z = r.NextFloat();
        Alpha = r.NextFloat(); Beta = r.NextFloat(); Gamma = r.NextFloat();
        Specular = ReadRgba(r);
        Diffuse = ReadRgba(r);
        Ambient = ReadRgba(r);
        Emission = ReadRgba(r);
        Shininess = r.NextFloat();
        MotionBoundary = r.NextBool();
        MotionEnd = r.NextInt();
        RotationAngleX = r.NextFloat(); RotationAngleY = r.NextFloat(); RotationAngleZ = r.NextFloat();
        RotationPeriod = r.NextFloat();
        OrbitAngleX = r.NextFloat(); OrbitAngleY = r.NextFloat(); OrbitAngleZ = r.NextFloat();
        OrbitCenterX = r.NextFloat(); OrbitCenterY = r.NextFloat(); OrbitCenterZ = r.NextFloat();
        OrbitPeriod = r.NextFloat();
        RotationPermanent = r.NextBool();
        OrbitPermanent = r.NextBool();
        int texW = r.NextInt(), texH = r.NextInt();
        string texName = r.Next();
        Texture = texW * texH != 0 && texName != AniTags.NoTexture ? texName : null;
        DrawStyle = r.NextInt() == AniTags.GluLine ? DrawStyle.Line : DrawStyle.Fill;
    }

    /// <summary>Original <c>CPrvok::SaveYouPrvok</c>.</summary>
    internal void WriteCommon(AniTokenWriter w, string tag)
    {
        w.Write(tag);
        w.Write(Name);
        w.Write(X); w.Write(Y); w.Write(Z);
        w.Write(Alpha); w.Write(Beta); w.Write(Gamma);
        w.Write(Specular);
        w.Write(Diffuse);
        w.Write(Ambient);
        w.Write(Emission);
        w.Write(Shininess);
        w.Write(MotionBoundary);
        w.Write(MotionEnd);
        w.Write(RotationAngleX); w.Write(RotationAngleY); w.Write(RotationAngleZ);
        w.Write(RotationPeriod);
        w.Write(OrbitAngleX); w.Write(OrbitAngleY); w.Write(OrbitAngleZ);
        w.Write(OrbitCenterX); w.Write(OrbitCenterY); w.Write(OrbitCenterZ);
        w.Write(OrbitPeriod);
        w.Write(RotationPermanent);
        w.Write(OrbitPermanent);
        if (HasTexture)
        {
            w.Write(128); w.Write(128);
            // like the original only the bare file name is stored; textures are looked up next to the .ani file
            w.Write(SanitizeFileToken(Path.GetFileName(Texture!)));
        }
        else
        {
            w.Write(0); w.Write(0);
            w.Write(AniTags.NoTexture);
        }
        w.Write(DrawStyle == DrawStyle.Line ? AniTags.GluLine : AniTags.GluFill);
    }

    private static string SanitizeFileToken(string s) => s.Replace(' ', '_');

    private static Rgba ReadRgba(AniTokenReader r) => new(r.NextFloat(), r.NextFloat(), r.NextFloat(), r.NextFloat());

    /// <summary>Reads the type specific part (after the common part).</summary>
    internal abstract void ReadBody(AniTokenReader r);

    /// <summary>Writes the complete element including its type tag.</summary>
    internal abstract void Write(AniTokenWriter w);
}
