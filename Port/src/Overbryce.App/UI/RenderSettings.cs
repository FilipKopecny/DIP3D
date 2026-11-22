using System.Drawing;

namespace Overbryce.App.UI;

/// <summary>
/// Command line request to render off-screen and exit: a single picture (<see cref="Output"/> is a
/// file) or, with <see cref="Frames"/>, every frame between <see cref="From"/> and <see cref="To"/>
/// (<see cref="Output"/> is a folder).
/// </summary>
internal sealed record SnapshotRequest(string Output, float? Time, Rendering.ViewMode View, bool Wireframe, Size Size,
    bool Frames = false, float? From = null, float? To = null, bool? Modern = null,
    bool Raytrace = false, int? Samples = null, int? Bounces = null, Action<string>? Log = null);

/// <summary>Which renderer draws the exported frames.</summary>
internal enum ExportRenderer
{
    /// <summary>The classic or the modern renderer, whichever the view shows.</summary>
    AsView,
    Classic,
    Modern,
    RayTraced,
}

/// <summary>Settings of the original "Gen. settings" dialog (TGenSettings).</summary>
internal sealed class RenderSettings
{
    public static readonly Size[] Resolutions = [new(320, 200), new(640, 480), new(800, 600), new(1024, 768), new(1280, 720), new(1920, 1080)];

    public Size ExportSize { get; set; } = new(640, 480);
    public bool Antialias { get; set; } = true;
    public bool AlphaBlend { get; set; } = true;
    public bool ExportPng { get; set; }
    public ExportRenderer Renderer { get; set; } = ExportRenderer.AsView;
}
