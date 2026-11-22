using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Overbryce.App.UI;

internal static class DialogLayout
{
    /// <summary>A fixed-size dialog centred on its owner; its content is <paramref name="body"/> above the buttons.</summary>
    public static Window Create(Window? owner, string title, UIElement body, double width, UIElement buttons)
    {
        DockPanel.SetDock(buttons, Dock.Bottom);
        return new Window
        {
            Title = title,
            Owner = owner,
            Width = width,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            ShowInTaskbar = false,
            Content = new DockPanel { Margin = new Thickness(20, 16, 20, 16), LastChildFill = true, Children = { buttons, body } },
        };
    }

    /// <summary>OK (Enter) and Cancel (Esc) at the bottom right.</summary>
    public static StackPanel OkCancel(Func<Window> window)
    {
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 96, Margin = new Thickness(0, 0, 8, 0) };
        ok.SetResourceReference(FrameworkElement.StyleProperty, "AccentButtonStyle");
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 96 };
        ok.Click += (_, _) => window().DialogResult = true;
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
            Children = { ok, cancel },
        };
    }

    /// <summary>A label and its control on one row of a two-column form.</summary>
    public static void AddRow(Grid form, string label, FrameworkElement control)
    {
        int row = form.RowDefinitions.Count;
        form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 12, 4) };
        Grid.SetRow(text, row);
        control.Margin = new Thickness(0, 4, 0, 4);
        Grid.SetRow(control, row);
        Grid.SetColumn(control, 1);
        form.Children.Add(text);
        form.Children.Add(control);
    }

    public static Grid Form()
    {
        var form = new Grid();
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        return form;
    }

    /// <summary>A control over the whole width of a form.</summary>
    public static void AddWide(Grid form, FrameworkElement control)
    {
        int row = form.RowDefinitions.Count;
        form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        control.Margin = new Thickness(0, 4, 0, 4);
        Grid.SetRow(control, row);
        Grid.SetColumnSpan(control, 2);
        form.Children.Add(control);
    }
}

/// <summary>Kind of a <see cref="Message"/> (its icon).</summary>
internal enum MessageKind
{
    Information,
    Question,
    Warning,
    Error,
}

/// <summary>Message box in the editor's theme (the Windows one stays light in dark mode).</summary>
internal static class Message
{
    public static void Show(Window? owner, string text, MessageKind kind = MessageKind.Information, string? title = null)
        => Ask(owner, text, kind, MessageBoxButton.OK, title);

    /// <summary>Yes/No or Yes/No/Cancel; Esc and closing the window answer Cancel (or No).</summary>
    public static MessageBoxResult Ask(Window? owner, string text, MessageKind kind, MessageBoxButton buttons, string? title = null)
    {
        var result = buttons == MessageBoxButton.YesNo ? MessageBoxResult.No : MessageBoxResult.Cancel;
        Window? window = null;

        var icon = Icons.Glyph(kind switch
        {
            MessageKind.Question => Icons.Help,
            MessageKind.Warning => Icons.Warning,
            MessageKind.Error => Icons.Error,
            _ => Icons.Info,
        }, 28);
        icon.VerticalAlignment = VerticalAlignment.Top;
        icon.Margin = new Thickness(0, 2, 16, 0);
        icon.SetResourceReference(TextBlock.ForegroundProperty, kind switch
        {
            MessageKind.Warning => "SystemFillColorCautionBrush",
            MessageKind.Error => "SystemFillColorCriticalBrush",
            _ => "AccentTextFillColorPrimaryBrush",
        });
        var body = new DockPanel
        {
            Children =
            {
                icon,
                new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center },
            },
        };
        DockPanel.SetDock(icon, Dock.Left);

        var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        void Add(string label, MessageBoxResult answer, bool isDefault, bool isCancel)
        {
            var b = new Button { Content = label, MinWidth = 96, Margin = new Thickness(8, 0, 0, 0), IsDefault = isDefault, IsCancel = isCancel };
            if (isDefault) b.SetResourceReference(FrameworkElement.StyleProperty, "AccentButtonStyle");
            b.Click += (_, _) =>
            {
                result = answer;
                window!.DialogResult = true;
            };
            panel.Children.Add(b);
        }
        switch (buttons)
        {
            case MessageBoxButton.YesNo:
                Add("Yes", MessageBoxResult.Yes, true, false);
                Add("No", MessageBoxResult.No, false, true);
                break;
            case MessageBoxButton.YesNoCancel:
                Add("Yes", MessageBoxResult.Yes, true, false);
                Add("No", MessageBoxResult.No, false, false);
                Add("Cancel", MessageBoxResult.Cancel, false, true);
                break;
            default:
                Add("OK", MessageBoxResult.OK, true, true);
                break;
        }

        window = DialogLayout.Create(owner, title ?? MainWindow.AppTitle, body, text.Length > 400 ? 760 : 460, panel);
        window.ShowDialog();
        return result;
    }
}

internal static class SettingsDialog
{
    public static bool Show(Window owner, RenderSettings settings)
    {
        Window? window = null;
        var form = DialogLayout.Form();

        var resolution = new ComboBox();
        foreach (var s in RenderSettings.Resolutions) resolution.Items.Add($"{s.Width} x {s.Height}");
        int idx = Array.IndexOf(RenderSettings.Resolutions, settings.ExportSize);
        resolution.SelectedIndex = idx >= 0 ? idx : 1;
        var renderer = new ComboBox { ItemsSource = new[] { "As the view (classic or modern)", "Classic", "Modern", "Ray traced" }, SelectedIndex = (int)settings.Renderer };
        var antialias = new CheckBox { Content = "Antialiasing (multisampling)", IsChecked = settings.Antialias };
        var alpha = new CheckBox { Content = "Alpha blending (white texels transparent)", IsChecked = settings.AlphaBlend };
        var png = new CheckBox { Content = "Export PNG instead of BMP", IsChecked = settings.ExportPng };

        DialogLayout.AddRow(form, "Export resolution", resolution);
        DialogLayout.AddRow(form, "Export renderer", renderer);
        DialogLayout.AddWide(form, antialias);
        DialogLayout.AddWide(form, alpha);
        DialogLayout.AddWide(form, png);

        window = DialogLayout.Create(owner, "Rendering settings", form, 440, DialogLayout.OkCancel(() => window!));
        if (window.ShowDialog() != true) return false;
        settings.ExportSize = RenderSettings.Resolutions[Math.Max(0, resolution.SelectedIndex)];
        settings.Antialias = antialias.IsChecked == true;
        settings.AlphaBlend = alpha.IsChecked == true;
        settings.ExportPng = png.IsChecked == true;
        settings.Renderer = (ExportRenderer)Math.Max(0, renderer.SelectedIndex);
        return true;
    }
}

/// <summary>The ray tracer's settings of the animation (new in 2.2). Cancel restores the previous values.</summary>
internal static class RaytraceSettingsDialog
{
    public static bool Show(Window owner, Core.Model.RaytraceSettings settings)
    {
        var original = settings.Clone();
        Window? window = null;
        var grid = new PropertyGridView { Height = 380 };
        grid.SelectedObject = settings;
        grid.PropertyValueChanged += _ =>
        {
            settings.Normalize();
            grid.RefreshValues();
        };
        var note = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
            Text = "Exposure and bloom come from the modern rendering settings. Per element: reflectivity, refractive index, " +
                   "roughness, absorption; per light: radius; per camera: aperture and focus distance. F9 traces the view.",
        };
        note.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        DockPanel.SetDock(note, Dock.Top);
        window = DialogLayout.Create(owner, "Ray tracing settings", new DockPanel { Children = { note, grid } }, 480,
            DialogLayout.OkCancel(() => window!));
        if (window.ShowDialog() == true)
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
    public static bool Show(Window owner, Core.Model.ModernSettings settings, Action preview)
    {
        var original = settings.Clone();
        Window? window = null;
        var grid = new PropertyGridView { Height = 480 };
        grid.SelectedObject = settings;
        grid.PropertyValueChanged += _ =>
        {
            settings.Normalize();
            grid.RefreshValues();
            preview();
        };
        window = DialogLayout.Create(owner, "Modern rendering settings", grid, 480, DialogLayout.OkCancel(() => window!));
        // beside the main window, so the view it changes stays visible
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = owner.Left + Math.Max(0, owner.ActualWidth - 500);
        window.Top = owner.Top + 80;
        window.ContentRendered += (_, _) => preview();
        if (window.ShowDialog() == true)
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
    public static bool Show(Window owner, Core.Model.Animation animation)
    {
        Window? window = null;
        var form = DialogLayout.Form();
        var name = new TextBox { Text = animation.Name, MaxLength = Core.Model.Element.MaxNameLength };
        var fps = new NumberBox { Minimum = 1, Maximum = 128, Increment = 1, Decimals = 0, Value = Math.Clamp(animation.Fps, 1, 128) };
        DialogLayout.AddRow(form, "Name", name);
        DialogLayout.AddRow(form, "Frames per second", fps);
        window = DialogLayout.Create(owner, "Animation settings", form, 360, DialogLayout.OkCancel(() => window!));
        window.Loaded += (_, _) => { name.Focus(); name.SelectAll(); };
        if (window.ShowDialog() != true) return false;
        animation.Name = name.Text;
        animation.Fps = (float)fps.Value;
        return true;
    }
}

internal static class AboutDialog
{
    public static void Show(Window owner)
    {
        Window? window = null;
        var title = new TextBlock { Text = "Overbryce Animator", FontSize = 22, FontWeight = FontWeights.SemiBold };
        var original = new TextBlock
        {
            Margin = new Thickness(0, 4, 0, 12),
            Text = "Version 1.0 - Tomáš Kopečný, FEI STU Bratislava, 1998\nDiploma thesis: key-frame animation with OpenGL",
        };
        var port = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = "Port v2.3 for Windows / .NET 10 / OpenGL 3.3 core, with a WPF editor.\n" +
                   "The animation model, the .ani file format and the Catmull-Rom key-frame interpolation are carried over " +
                   "from the original Borland C++ Builder application; the fixed-function OpenGL 1.1 pipeline is emulated " +
                   "with shaders.",
        };
        port.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        var ok = new Button { Content = "OK", IsDefault = true, IsCancel = true, MinWidth = 96, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        ok.SetResourceReference(FrameworkElement.StyleProperty, "AccentButtonStyle");
        ok.Click += (_, _) => window!.DialogResult = true;
        var logo = Icons.Shape("Animation", 48);
        logo.Margin = new Thickness(0, 4, 20, 0);
        logo.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(logo, Dock.Left);
        var body = new DockPanel { Children = { logo, new StackPanel { Children = { title, original, port } } } };
        window = DialogLayout.Create(owner, "About Overbryce Animator", body, 520, ok);
        window.ShowDialog();
    }
}
