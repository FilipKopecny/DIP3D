using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Overbryce.App.UI;

/// <summary>
/// A number with up/down buttons (WPF has no NumericUpDown). The arrow keys and the mouse wheel step
/// it too; typed text is taken on Enter or when the box loses the focus.
/// </summary>
internal sealed class NumberBox : UserControl
{
    private readonly TextBox _text = new() { VerticalContentAlignment = VerticalAlignment.Center, MinWidth = 0 };
    private double _value;

    public NumberBox()
    {
        var up = new RepeatButton { Content = Icons.Glyph(Icons.Up, 8), Focusable = false, Padding = new Thickness(4, 0, 4, 0), MinHeight = 0, MinWidth = 0 };
        var down = new RepeatButton { Content = Icons.Glyph(Icons.Down, 8), Focusable = false, Padding = new Thickness(4, 0, 4, 0), MinHeight = 0, MinWidth = 0 };
        up.Click += (_, _) => Step(+1);
        down.Click += (_, _) => Step(-1);
        var buttons = new Grid { Margin = new Thickness(2, 0, 0, 0) };
        buttons.RowDefinitions.Add(new RowDefinition());
        buttons.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(down, 1);
        buttons.Children.Add(up);
        buttons.Children.Add(down);

        var panel = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Right);
        panel.Children.Add(buttons);
        panel.Children.Add(_text);
        Content = panel;

        _text.LostKeyboardFocus += (_, _) => Commit();
        _text.PreviewKeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Enter: Commit(); _text.SelectAll(); e.Handled = true; break;
                case Key.Escape: ShowValue(); _text.SelectAll(); e.Handled = true; break;
                case Key.Up: Step(+1); e.Handled = true; break;
                case Key.Down: Step(-1); e.Handled = true; break;
            }
        };
        _text.MouseWheel += (_, e) =>
        {
            if (!_text.IsKeyboardFocusWithin) return;
            Step(Math.Sign(e.Delta));
            e.Handled = true;
        };
        ShowValue();
    }

    public double Minimum { get; set; }
    public double Maximum { get; set; } = 100;
    public double Increment { get; set; } = 1;
    public int Decimals { get; set; }

    public double Value
    {
        get => _value;
        set
        {
            _value = value;
            ShowValue();
        }
    }

    public event EventHandler? ValueChanged;

    private void Step(int direction)
    {
        Commit();
        SetValue(_value + direction * Increment);
    }

    private void Commit()
    {
        if (TryParse(_text.Text, out double v)) SetValue(v);
        else ShowValue();
    }

    private void SetValue(double v)
    {
        v = Math.Round(Math.Clamp(v, Minimum, Maximum), Decimals);
        bool changed = v != _value;
        _value = v;
        ShowValue();
        if (changed) ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ShowValue() => _text.Text = _value.ToString("F" + Decimals, CultureInfo.CurrentCulture);

    /// <summary>The user's decimal separator, or a point.</summary>
    internal static bool TryParse(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
