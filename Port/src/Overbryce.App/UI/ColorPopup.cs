using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using Overbryce.Core;

namespace Overbryce.App.UI;

/// <summary>
/// Drop-down colour editor of the property grid: sliders for red, green, blue and alpha (0..1, as the
/// model stores them), changed live, plus the Windows colour dialog for picking from a palette.
/// </summary>
internal sealed class ColorPopup
{
    private static readonly string[] Names = ["Red", "Green", "Blue", "Alpha"];

    private readonly Popup _popup;
    private readonly Slider[] _sliders = new Slider[4];
    private readonly TextBlock[] _values = new TextBlock[4];
    private readonly Border _preview = new() { Height = 28, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 8) };
    private readonly Action<Rgba> _changed;
    private Rgba _color;
    private bool _showing;

    public ColorPopup(ToggleButton anchor, Action<Rgba> changed)
    {
        _changed = changed;
        _preview.SetResourceReference(Border.BorderBrushProperty, "ControlStrongStrokeColorDefaultBrush");

        var rows = new Grid();
        rows.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        for (int i = 0; i < 4; i++)
        {
            rows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new TextBlock { Text = Names[i], VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            var slider = _sliders[i] = new Slider { Minimum = 0, Maximum = 1, SmallChange = 0.01, LargeChange = 0.1, VerticalAlignment = VerticalAlignment.Center, IsMoveToPointEnabled = true };
            var value = _values[i] = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };
            int component = i;
            slider.ValueChanged += (_, _) => SliderMoved(component);
            Grid.SetRow(label, i);
            Grid.SetRow(slider, i);
            Grid.SetColumn(slider, 1);
            Grid.SetRow(value, i);
            Grid.SetColumn(value, 2);
            rows.Children.Add(label);
            rows.Children.Add(slider);
            rows.Children.Add(value);
        }

        var palette = new Button { Content = "Windows colour dialog...", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
        palette.Click += (_, _) => PickFromDialog();

        var border = new Border
        {
            Width = 280,
            Padding = new Thickness(14, 12, 14, 12),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Child = new StackPanel { Children = { _preview, rows, palette } },
        };
        border.SetResourceReference(Border.BackgroundProperty, "SolidBackgroundFillColorTertiaryBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "SurfaceStrokeColorFlyoutBrush");
        border.SetResourceReference(TextElement.ForegroundProperty, "TextFillColorPrimaryBrush");

        _popup = new Popup
        {
            PlacementTarget = anchor,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            Child = border,
        };
        _popup.Closed += (_, _) => anchor.IsChecked = false;
    }

    public void Open(Rgba color)
    {
        Show(color);
        _popup.IsOpen = true;
    }

    /// <summary>Shows <paramref name="color"/> without reporting it as a change.</summary>
    public void Show(Rgba color)
    {
        _showing = true;
        _color = color;
        try
        {
            float[] v = [color.R, color.G, color.B, color.A];
            for (int i = 0; i < 4; i++)
            {
                _sliders[i].Value = Math.Clamp(v[i], 0f, 1f);
                _values[i].Text = v[i].ToString("0.00", CultureInfo.CurrentCulture);
            }
            _preview.Background = new SolidColorBrush(ToColor(color));
        }
        finally
        {
            _showing = false;
        }
    }

    /// <summary>A slider sets only its own component: the others may be above 1 (bright lights) and stay so.</summary>
    private void SliderMoved(int component)
    {
        if (_showing) return;
        float v = (float)Math.Round(_sliders[component].Value, 3);
        var c = _color;
        switch (component)
        {
            case 0: c.R = v; break;
            case 1: c.G = v; break;
            case 2: c.B = v; break;
            default: c.A = v; break;
        }
        Show(c);
        _changed(c);
    }

    private void PickFromDialog()
    {
        var current = _color;
        _popup.StaysOpen = true; // the dialog takes the focus; keep the drop-down for the result
        try
        {
            using var dlg = new System.Windows.Forms.ColorDialog
            {
                FullOpen = true,
                Color = System.Drawing.Color.FromArgb(ToColor(current).R, ToColor(current).G, ToColor(current).B),
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var picked = new Rgba(dlg.Color.R / 255f, dlg.Color.G / 255f, dlg.Color.B / 255f, current.A);
            Show(picked);
            _changed(picked);
        }
        finally
        {
            _popup.StaysOpen = false;
        }
    }

    /// <summary>The colour on screen (alpha is not shown; values above 1 show as 1).</summary>
    public static Color ToColor(Rgba c) => Color.FromRgb(
        (byte)Math.Round(Math.Clamp(c.R, 0f, 1f) * 255),
        (byte)Math.Round(Math.Clamp(c.G, 0f, 1f) * 255),
        (byte)Math.Round(Math.Clamp(c.B, 0f, 1f) * 255));
}
