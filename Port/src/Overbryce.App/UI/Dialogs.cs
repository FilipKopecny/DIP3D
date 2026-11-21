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

internal static class DialogLayout
{
    public static Form CreateDialog(string title, int width, int height)
    {
        return new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.Dpi,
            ClientSize = new Size(width, height),
            Padding = new Padding(12),
        };
    }

    public static FlowLayoutPanel Buttons(Form form)
    {
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(80, 0) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(80, 0) };
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        panel.Controls.Add(cancel);
        panel.Controls.Add(ok);
        return panel;
    }
}

internal static class SettingsDialog
{
    public static bool Show(IWin32Window owner, RenderSettings settings)
    {
        using var form = DialogLayout.CreateDialog("Rendering settings", 380, 250);
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var resolution = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        foreach (var s in RenderSettings.Resolutions) resolution.Items.Add($"{s.Width} x {s.Height}");
        int idx = Array.IndexOf(RenderSettings.Resolutions, settings.ExportSize);
        resolution.SelectedIndex = idx >= 0 ? idx : 1;

        var antialias = new CheckBox { Text = "Antialiasing (multisampling)", Checked = settings.Antialias, AutoSize = true };
        var alpha = new CheckBox { Text = "Alpha blending (white texels transparent)", Checked = settings.AlphaBlend, AutoSize = true };
        var png = new CheckBox { Text = "Export PNG instead of BMP", Checked = settings.ExportPng, AutoSize = true };
        var renderer = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        renderer.Items.AddRange(["As the view (classic or modern)", "Classic", "Modern", "Ray traced"]);
        renderer.SelectedIndex = (int)settings.Renderer;

        table.Controls.Add(new Label { Text = "Export resolution:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        table.Controls.Add(resolution, 1, 0);
        table.Controls.Add(new Label { Text = "Export renderer:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        table.Controls.Add(renderer, 1, 1);
        table.Controls.Add(antialias, 0, 2);
        table.SetColumnSpan(antialias, 2);
        table.Controls.Add(alpha, 0, 3);
        table.SetColumnSpan(alpha, 2);
        table.Controls.Add(png, 0, 4);
        table.SetColumnSpan(png, 2);

        form.Controls.Add(table);
        form.Controls.Add(DialogLayout.Buttons(form));

        if (form.ShowDialog(owner) != DialogResult.OK) return false;
        settings.ExportSize = RenderSettings.Resolutions[Math.Max(0, resolution.SelectedIndex)];
        settings.Antialias = antialias.Checked;
        settings.AlphaBlend = alpha.Checked;
        settings.ExportPng = png.Checked;
        settings.Renderer = (ExportRenderer)Math.Max(0, renderer.SelectedIndex);
        return true;
    }
}

/// <summary>The ray tracer's settings of the animation (new in 2.2). Cancel restores the previous values.</summary>
internal static class RaytraceSettingsDialog
{
    public static bool Show(IWin32Window owner, Core.Model.RaytraceSettings settings)
    {
        var original = settings.Clone();
        using var form = DialogLayout.CreateDialog("Ray tracing settings", 440, 470);
        var grid = new PropertyGrid
        {
            Dock = DockStyle.Fill,
            SelectedObject = settings,
            ToolbarVisible = false,
            HelpVisible = true,
            PropertySort = PropertySort.Categorized,
        };
        grid.PropertyValueChanged += (_, _) =>
        {
            settings.Normalize();
            grid.Refresh();
        };
        var note = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 48,
            Text = "Exposure and bloom come from the modern rendering settings. Per element: reflectivity, refractive index, " +
                   "roughness, absorption; per light: radius; per camera: aperture and focus distance. F9 traces the view.",
        };
        form.Controls.Add(grid);
        form.Controls.Add(note);
        form.Controls.Add(DialogLayout.Buttons(form));
        if (form.ShowDialog(owner) == DialogResult.OK)
        {
            settings.Normalize();
            return true;
        }
        settings.CopyFrom(original);
        return false;
    }
}

/// <summary>
/// The modern renderer's settings of the animation (new in 2.1). Changes are shown at once; Cancel
/// restores the previous values.
/// </summary>
internal static class ModernSettingsDialog
{
    public static bool Show(IWin32Window owner, Core.Model.ModernSettings settings, Action preview)
    {
        var original = settings.Clone();
        using var form = DialogLayout.CreateDialog("Modern rendering settings", 440, 560);
        var grid = new PropertyGrid
        {
            Dock = DockStyle.Fill,
            SelectedObject = settings,
            ToolbarVisible = false,
            HelpVisible = true,
            PropertySort = PropertySort.Categorized,
        };
        grid.PropertyValueChanged += (_, _) =>
        {
            settings.Normalize();
            grid.Refresh();
            preview();
        };
        form.Controls.Add(grid);
        form.Controls.Add(DialogLayout.Buttons(form));
        form.Shown += (_, _) => preview();
        if (form.ShowDialog(owner) == DialogResult.OK)
        {
            settings.Normalize();
            return true;
        }
        settings.CopyFrom(original);
        return false;
    }
}

internal static class AnimationSettingsDialog
{
    /// <summary>Original "Animation settings": the frame rate (1..128).</summary>
    public static bool Show(IWin32Window owner, Core.Model.Animation animation)
    {
        using var form = DialogLayout.CreateDialog("Animation settings", 300, 140);
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        var name = new TextBox { Text = animation.Name, Dock = DockStyle.Fill, MaxLength = Core.Model.Element.MaxNameLength };
        var fps = new NumericUpDown { Minimum = 1, Maximum = 128, DecimalPlaces = 0, Value = (decimal)Math.Clamp(animation.Fps, 1, 128), Dock = DockStyle.Fill };
        table.Controls.Add(new Label { Text = "Name:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        table.Controls.Add(name, 1, 0);
        table.Controls.Add(new Label { Text = "Frames per second:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        table.Controls.Add(fps, 1, 1);
        form.Controls.Add(table);
        form.Controls.Add(DialogLayout.Buttons(form));
        if (form.ShowDialog(owner) != DialogResult.OK) return false;
        animation.Name = name.Text;
        animation.Fps = (float)fps.Value;
        return true;
    }
}

internal static class AboutDialog
{
    public static void Show(IWin32Window owner)
    {
        using var form = DialogLayout.CreateDialog("About Overbryce Animator", 420, 230);
        var text = new Label
        {
            Dock = DockStyle.Fill,
            Text =
                "Overbryce Animator v1.0\r\n" +
                "Tomáš Kopečný, FEI STU Bratislava, 1998\r\n" +
                "Diploma thesis: key-frame animation with OpenGL\r\n\r\n" +
                "Port v2.2 for Windows / .NET 10 / OpenGL 3.3 core.\r\n" +
                "The animation model, the .ani file format and the Catmull-Rom\r\n" +
                "key-frame interpolation are carried over from the original\r\n" +
                "Borland C++ Builder application; the fixed-function OpenGL 1.1\r\n" +
                "pipeline is emulated with shaders.",
        };
        form.Controls.Add(text);
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom };
        form.AcceptButton = ok;
        form.Controls.Add(ok);
        form.ShowDialog(owner);
    }
}
