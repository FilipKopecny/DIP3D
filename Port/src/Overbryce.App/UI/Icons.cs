using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Path = System.Windows.Shapes.Path;

namespace Overbryce.App.UI;

/// <summary>
/// The editor's icons: line drawings of the element types (Styles.xaml, "Icon.*") and glyphs of the
/// Windows icon font. Both take the text colour of where they are shown, so they follow the theme.
/// </summary>
internal static class Icons
{
    // glyphs of Segoe Fluent Icons (Windows 11) / Segoe MDL2 Assets (Windows 10)
    public const string New = "\uE8A5";
    public const string Open = "\uE838";
    public const string Save = "\uE74E";
    public const string SaveAs = "\uE792";
    public const string Video = "\uE714";
    public const string Settings = "\uE713";
    public const string Delete = "\uE74D";
    public const string Rename = "\uE8AC";
    public const string Copy = "\uE8C8";
    public const string Camera = "\uE722";
    public const string Play = "\uE768";
    public const string Pause = "\uE769";
    public const string Loop = "\uE8EE";
    public const string Left = "\uE72B";
    public const string Right = "\uE72A";
    public const string Up = "\uE74A";
    public const string Down = "\uE74B";
    public const string Rotate = "\uE7AD";
    public const string Sun = "\uE706";
    public const string Help = "\uE897";
    public const string Info = "\uE946";
    public const string Warning = "\uE7BA";
    public const string Error = "\uE783";
    public const string More = "\uE712";
    public const string Clear = "\uE711";

    /// <summary>The line icon "Icon.<paramref name="name"/>" (an element type, "Scene", "Animation" ...).</summary>
    public static FrameworkElement Shape(string name, double size = 16)
    {
        var path = new Path { Data = (Geometry)Application.Current.FindResource("Icon." + name) };
        path.SetResourceReference(FrameworkElement.StyleProperty, "IconPath");
        if (size == 16) return path;
        return new Viewbox { Width = size, Height = size, Child = path };
    }

    /// <summary>A glyph of the icon font.</summary>
    public static FrameworkElement Glyph(string glyph, double size = 16)
    {
        var text = new TextBlock { Text = glyph, FontSize = size };
        text.SetResourceReference(FrameworkElement.StyleProperty, "Glyph");
        return text;
    }

    /// <summary>A glyph for a menu item.</summary>
    public static FrameworkElement MenuGlyph(string glyph)
    {
        var text = new TextBlock { Text = glyph };
        text.SetResourceReference(FrameworkElement.StyleProperty, "MenuGlyph");
        return text;
    }

    /// <summary>The rotate glyph turned the right way, with the axis letter beside it.</summary>
    public static FrameworkElement Rotation(char axis, bool counterClockwise)
    {
        var arrow = Glyph(Rotate, 15);
        if (counterClockwise)
        {
            arrow.RenderTransformOrigin = new Point(0.5, 0.5);
            arrow.RenderTransform = new ScaleTransform(-1, 1);
        }
        var letter = new TextBlock
        {
            Text = axis.ToString(),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(2, 0, 0, -2),
        };
        return new StackPanel { Orientation = Orientation.Horizontal, Children = { arrow, letter } };
    }
}
