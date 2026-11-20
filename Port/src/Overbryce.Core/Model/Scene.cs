using System.ComponentModel;
using Overbryce.Core.IO;

namespace Overbryce.Core.Model;

public enum FogMode
{
    None = 0,
    Linear = 1,
    Exponential = 2,
    Exponential2 = 3,
}

/// <summary>
/// One key state of the animation (original <c>CScena</c>): a group of elements with a time stamp
/// and fog settings.
/// </summary>
public sealed class Scene : GroupElement
{
    public Scene(string name = "Scena") : base(name) { }

    public override ElementType Type => ElementType.Scene;

    [Category("0 State"), DisplayName("Time [s]"), Description("Time of this key state within the animation.")]
    public float AniTime { get; set; }

    [Category("9 Fog"), DisplayName("Mode")] public FogMode FogMode { get; set; } = FogMode.None;
    [Category("9 Fog"), DisplayName("Start"), Description("Linear fog: distance where the fog starts.")] public float FogStart { get; set; }
    [Category("9 Fog"), DisplayName("End"), Description("Linear fog: distance of full fog.")] public float FogEnd { get; set; } = 1;
    [Category("9 Fog"), DisplayName("Density"), Description("Exponential fog density (0..1).")] public float FogDensity { get; set; } = 1;
    [Category("9 Fog"), DisplayName("Colour")] public Rgba FogColor { get; set; } = new(0.2f, 0.2f, 0.2f, 0.2f);

    [Browsable(false)]
    public IEnumerable<LightElement> Lights => Descendants().OfType<LightElement>();

    [Browsable(false)]
    public IEnumerable<CameraElement> Cameras => Descendants().OfType<CameraElement>();

    [Browsable(false)]
    public CameraElement? ActiveCamera => Cameras.FirstOrDefault(c => c.Active);

    /// <summary>Original <c>CScena::SetAktualna</c>: makes the camera the only active one.</summary>
    public void SetActiveCamera(CameraElement? camera)
    {
        foreach (var c in Cameras) c.Active = ReferenceEquals(c, camera);
    }

    public new Scene Clone() => (Scene)base.Clone();

    /// <summary>
    /// Original <c>CScena::VypocitajMedziscenu</c>: interpolates the state's own attributes
    /// (transform, time stamp, fog range and colour). Elements are interpolated by <see cref="Animation"/>.
    /// </summary>
    public void InterpolateState(in SplineWeights w, Scene prev, Scene next, Scene nextNext)
    {
        InterpolateCommon(w, 0, prev, next, nextNext);
        FogStart = w.Interpolate(prev.FogStart, FogStart, next.FogStart, nextNext.FogStart);
        FogEnd = w.Interpolate(prev.FogEnd, FogEnd, next.FogEnd, nextNext.FogEnd);
        AniTime = w.Interpolate(prev.AniTime, AniTime, next.AniTime, nextNext.AniTime);
        FogColor = new Rgba(
            w.Interpolate(prev.FogColor.R, FogColor.R, next.FogColor.R, nextNext.FogColor.R),
            w.Interpolate(prev.FogColor.G, FogColor.G, next.FogColor.G, nextNext.FogColor.G),
            w.Interpolate(prev.FogColor.B, FogColor.B, next.FogColor.B, nextNext.FogColor.B),
            FogColor.A);
    }

    public override void Interpolate(in SplineWeights w, float time, Element prev, Element next, Element nextNext)
        => throw new InvalidOperationException("Scenes are interpolated by Animation.Evaluate.");

    internal override void ReadBody(AniTokenReader r)
    {
        AniTime = r.NextFloat();
        FogMode = (FogMode)Math.Clamp(r.NextInt(), 0, 3);
        FogStart = r.NextFloat();
        FogEnd = r.NextFloat();
        FogDensity = r.NextFloat();
        FogColor = new Rgba(r.NextFloat(), r.NextFloat(), r.NextFloat(), r.NextFloat());
        AniFile.ReadChildren(r, this, AniTags.SceneEnd);
    }

    internal override void Write(AniTokenWriter w)
    {
        WriteCommon(w, AniTags.Scene);
        w.Write(AniTime);
        w.Write((int)FogMode);
        w.Write(FogStart);
        w.Write(FogEnd);
        w.Write(FogDensity);
        w.Write(FogColor);
        foreach (var c in Children) c.Write(w);
        w.Write(AniTags.SceneEnd);
    }
}
