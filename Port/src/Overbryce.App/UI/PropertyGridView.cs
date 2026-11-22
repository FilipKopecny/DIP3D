using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Overbryce.Core;

namespace Overbryce.App.UI;

/// <summary>
/// Property editor for the element, the animation and the renderer settings (WPF has no PropertyGrid).
/// Reads the properties through <see cref="TypeDescriptor"/> like the Windows Forms grid did, so the
/// model's [Category], [DisplayName], [Description], [ReadOnly] and [Browsable] attributes and the
/// element views of <see cref="ElementView"/> keep working. Categories are sorted by name; a leading
/// number ("2 Position") only orders them and is not shown.
/// <list type="bullet">
/// <item>numbers and text: typed, taken on Enter or when leaving the field (Esc restores); Up/Down
/// step numbers (Shift: ×10, Ctrl: ×0.1)</item>
/// <item>yes/no: check box; choices: drop-down list</item>
/// <item>colours: swatch with sliders, or typed as "R; G; B; A"</item>
/// <item>textures: file name or the file dialog</item>
/// </list>
/// </summary>
internal sealed partial class PropertyGridView : UserControl
{
    private static readonly HashSet<string> CollapsedCategories = new(StringComparer.Ordinal);

    private readonly StackPanel _categories = new();
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _helpTitle = new() { FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _helpText = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
    private readonly Border _help;
    private readonly List<Action> _updates = new();
    private object? _selected;
    private bool _updating;

    public PropertyGridView()
    {
        _helpText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        _help = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(10, 6, 10, 8),
            MinHeight = 64,
            Child = new StackPanel { Children = { _helpTitle, _helpText } },
        };
        _help.SetResourceReference(Border.BorderBrushProperty, "DividerStrokeColorDefaultBrush");
        DockPanel.SetDock(_help, Dock.Bottom);

        _scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _categories,
            Padding = new Thickness(0, 0, 4, 0),
        };
        Content = new DockPanel { Children = { _help, _scroll } };
    }

    /// <summary>The object shown; null shows nothing.</summary>
    public object? SelectedObject
    {
        get => _selected;
        set
        {
            bool same = value != null && _selected != null && ReferenceEquals(OwnerOf(value), OwnerOf(_selected));
            double offset = _scroll.VerticalOffset;
            _selected = value;
            Rebuild();
            if (same)
            {
                _scroll.UpdateLayout();
                _scroll.ScrollToVerticalOffset(offset);
            }
            else
            {
                _scroll.ScrollToTop();
            }
        }
    }

    /// <summary>Shows the description of the focused property at the bottom (like the Windows Forms grid).</summary>
    public bool HelpVisible
    {
        get => _help.Visibility == Visibility.Visible;
        set => _help.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Raised after a property was changed here, with its name.</summary>
    public event Action<string>? PropertyValueChanged;

    /// <summary>Shows the current values again (after they were changed elsewhere).</summary>
    public void RefreshValues()
    {
        _updating = true;
        try
        {
            foreach (var update in _updates) update();
        }
        finally
        {
            _updating = false;
        }
    }

    private static object OwnerOf(object o) => o is ElementView v ? v.Element : o;

    // ==================================================================================== layout

    private void Rebuild()
    {
        _categories.Children.Clear();
        _updates.Clear();
        ShowHelp(null, null);
        if (_selected == null) return;

        var properties = TypeDescriptor.GetProperties(_selected, [BrowsableAttribute.Yes]).Cast<PropertyDescriptor>();
        foreach (var group in properties.GroupBy(p => p.Category ?? "Misc").OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            string category = group.Key;
            var rows = new Grid { Margin = new Thickness(4, 2, 12, 6) }; // room for the scroll bar on the right
            rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            foreach (var p in group) AddRow(rows, p);

            var expander = new Expander
            {
                Header = new TextBlock { Text = CategoryNumber().Replace(category, ""), FontWeight = FontWeights.SemiBold },
                IsExpanded = !CollapsedCategories.Contains(category),
                Content = rows,
                Margin = new Thickness(0, 0, 0, 2),
                Padding = new Thickness(0),
            };
            expander.Expanded += (_, _) => CollapsedCategories.Remove(category);
            expander.Collapsed += (_, _) => CollapsedCategories.Add(category);
            _categories.Children.Add(expander);
        }
        RefreshValues();
    }

    [GeneratedRegex(@"^\d+\s+")]
    private static partial Regex CategoryNumber();

    private void AddRow(Grid rows, PropertyDescriptor p)
    {
        object owner = _selected is ICustomTypeDescriptor custom ? custom.GetPropertyOwner(p) ?? _selected! : _selected!;
        int row = rows.RowDefinitions.Count;
        rows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock
        {
            Text = p.DisplayName,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = string.IsNullOrEmpty(p.Description) ? null : p.Description,
        };
        Grid.SetRow(label, row);
        rows.Children.Add(label);

        var editor = CreateEditor(p, owner);
        editor.Margin = new Thickness(0, 2, 0, 2);
        Grid.SetRow(editor, row);
        Grid.SetColumn(editor, 1);
        rows.Children.Add(editor);

        void Describe() => ShowHelp(p.DisplayName, p.Description);
        label.MouseEnter += (_, _) => Describe();
        editor.IsKeyboardFocusWithinChanged += (_, e) => { if ((bool)e.NewValue) Describe(); };
    }

    private void ShowHelp(string? title, string? text)
    {
        _helpTitle.ClearValue(TextBlock.ForegroundProperty);
        _helpTitle.Text = title ?? "";
        _helpText.Text = title == null ? "Point at a setting to see what it does." : text ?? "";
    }

    private void ShowError(string title, string message)
    {
        _helpTitle.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        _helpTitle.Text = title;
        _helpText.Text = message;
    }

    // ==================================================================================== editors

    private FrameworkElement CreateEditor(PropertyDescriptor p, object owner)
    {
        var type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        if (p.IsReadOnly)
        {
            var text = Compact(new TextBox { IsReadOnly = true, BorderThickness = new Thickness(0) });
            text.SetResourceReference(TextBox.ForegroundProperty, "TextFillColorSecondaryBrush");
            text.Background = Brushes.Transparent;
            _updates.Add(() => text.Text = Format(p, owner));
            return text;
        }
        if (type == typeof(bool)) return BoolEditor(p, owner);
        if (type.IsEnum) return EnumEditor(p, owner, type);
        if (type == typeof(Rgba)) return ColorEditor(p, owner);
        if (p.Attributes[typeof(EditorAttribute)] is EditorAttribute { EditorTypeName: var editor } &&
            editor.StartsWith(typeof(TextureFileEditor).FullName!, StringComparison.Ordinal))
            return FileEditor(p, owner);
        return TextEditor(p, owner);
    }

    /// <summary>Fluent's controls are made for touch; the grid has many rows, so its editors are smaller.</summary>
    private static T Compact<T>(T c) where T : Control
    {
        c.MinHeight = 0;
        c.Padding = new Thickness(6, 3, 6, 3);
        c.VerticalContentAlignment = VerticalAlignment.Center;
        return c;
    }

    private FrameworkElement BoolEditor(PropertyDescriptor p, object owner)
    {
        var check = new CheckBox { MinHeight = 0, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(4, 0, 0, 0) };
        check.Click += (_, _) => Set(p, owner, check.IsChecked == true);
        _updates.Add(() => check.IsChecked = p.GetValue(owner) is true);
        return check;
    }

    private FrameworkElement EnumEditor(PropertyDescriptor p, object owner, Type type)
    {
        var combo = Compact(new ComboBox { ItemsSource = Enum.GetValues(type) });
        combo.SelectionChanged += (_, _) =>
        {
            if (!_updating && combo.SelectedItem is { } v && !Equals(v, p.GetValue(owner))) Set(p, owner, v);
        };
        _updates.Add(() => combo.SelectedItem = p.GetValue(owner));
        return combo;
    }

    private FrameworkElement TextEditor(PropertyDescriptor p, object owner)
    {
        var box = Compact(new TextBox());
        _updates.Add(() => box.Text = Format(p, owner));
        AttachCommit(box, p, owner);
        var type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        bool integer = type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte);
        bool real = type == typeof(float) || type == typeof(double) || type == typeof(decimal);
        if (integer || real)
        {
            box.PreviewKeyDown += (_, e) =>
            {
                if (e.Key is not (Key.Up or Key.Down)) return;
                double step = integer ? 1 : 0.1;
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) step *= 10;
                if (!integer && (Keyboard.Modifiers & ModifierKeys.Control) != 0) step *= 0.1;
                if (!NumberBox.TryParse(box.Text, out double v)) v = Convert.ToDouble(p.GetValue(owner), CultureInfo.InvariantCulture);
                v = Math.Round(v + (e.Key == Key.Up ? step : -step), integer ? 0 : 4);
                if (Set(p, owner, Convert.ChangeType(v, type, CultureInfo.InvariantCulture))) box.SelectAll();
                e.Handled = true;
            };
        }
        return box;
    }

    /// <summary>Takes the text on Enter and when the field loses the focus; Esc shows the value again.</summary>
    private void AttachCommit(TextBox box, PropertyDescriptor p, object owner)
    {
        void Commit()
        {
            if (box.Text == Format(p, owner)) return;
            if (Parse(p, box.Text, out object? value)) Set(p, owner, value);
            else
            {
                ShowError("Invalid value for " + p.DisplayName, $"'{box.Text}' is not a valid {Describe(p.PropertyType)}.");
                box.Text = Format(p, owner);
            }
        }

        box.LostKeyboardFocus += (_, _) => Commit();
        box.GotKeyboardFocus += (_, _) => box.SelectAll();
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Commit();
                box.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                box.Text = Format(p, owner);
                box.SelectAll();
                e.Handled = true;
            }
        };
    }

    private FrameworkElement FileEditor(PropertyDescriptor p, object owner)
    {
        var box = Compact(new TextBox());
        _updates.Add(() => box.Text = Format(p, owner));
        AttachCommit(box, p, owner);
        var browse = Compact(new Button { Content = Icons.Glyph(Icons.More, 12), ToolTip = "Choose a texture file...", Margin = new Thickness(2, 0, 0, 0), Padding = new Thickness(6, 2, 6, 2) });
        browse.Click += (_, _) =>
        {
            if (TextureFileEditor.Pick(Window.GetWindow(this), p.GetValue(owner) as string) is { } file) Set(p, owner, file);
        };
        DockPanel.SetDock(browse, Dock.Right);
        return new DockPanel { Children = { browse, box } };
    }

    private FrameworkElement ColorEditor(PropertyDescriptor p, object owner)
    {
        var box = Compact(new TextBox());
        AttachCommit(box, p, owner);
        var swatch = new Border { Width = 18, Height = 14, CornerRadius = new CornerRadius(2), BorderThickness = new Thickness(1) };
        swatch.SetResourceReference(Border.BorderBrushProperty, "ControlStrongStrokeColorDefaultBrush");
        var button = Compact(new ToggleButton { Content = swatch, ToolTip = "Pick the colour", Margin = new Thickness(0, 0, 2, 0), Padding = new Thickness(5, 3, 5, 3) });
        var popup = new ColorPopup(button, v => Set(p, owner, v));
        button.Checked += (_, _) => popup.Open((Rgba)p.GetValue(owner)!);
        _updates.Add(() =>
        {
            var c = (Rgba)p.GetValue(owner)!;
            box.Text = Format(p, owner);
            swatch.Background = new SolidColorBrush(ColorPopup.ToColor(c));
            popup.Show(c);
        });
        DockPanel.SetDock(button, Dock.Left);
        return new DockPanel { Children = { button, box } };
    }

    // ==================================================================================== values

    private static string Format(PropertyDescriptor p, object owner)
    {
        object? value = p.GetValue(owner);
        if (value == null) return "";
        try
        {
            return p.Converter.ConvertToString(null, CultureInfo.CurrentCulture, value) ?? "";
        }
        catch (NotSupportedException)
        {
            return value.ToString() ?? "";
        }
    }

    /// <summary>Text to a value: in the user's number format, else with a decimal point.</summary>
    private static bool Parse(PropertyDescriptor p, string text, out object? value)
    {
        var type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        if (type == typeof(string))
        {
            value = text;
            return true;
        }
        foreach (var culture in new[] { CultureInfo.CurrentCulture, CultureInfo.InvariantCulture })
        {
            try
            {
                value = p.Converter.ConvertFromString(null, culture, text.Trim());
                return true;
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or NotSupportedException or OverflowException)
            {
                // try the next culture
            }
        }
        value = null;
        return false;
    }

    private static string Describe(Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;
        if (t == typeof(int)) return "whole number";
        if (t == typeof(float) || t == typeof(double)) return "number";
        if (t == typeof(Rgba)) return "colour ('R; G; B; A' with values 0..1)";
        return t.Name;
    }

    /// <summary>Sets the value and reports it; false (with the reason shown below) when the model refuses it.</summary>
    private bool Set(PropertyDescriptor p, object owner, object? value)
    {
        if (_updating) return false;
        try
        {
            p.SetValue(owner, value);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or NotSupportedException)
        {
            ShowError("Invalid value for " + p.DisplayName, (ex.InnerException ?? ex).Message);
            RefreshValues();
            return false;
        }
        ShowHelp(p.DisplayName, p.Description);
        PropertyValueChanged?.Invoke(p.Name);
        RefreshValues();
        return true;
    }
}
