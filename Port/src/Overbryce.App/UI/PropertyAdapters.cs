using System.Collections;
using System.ComponentModel;
using System.Globalization;
using Overbryce.Core;
using Overbryce.Core.Model;

namespace Overbryce.App.UI;

/// <summary>
/// Shows an element in the PropertyGrid with only the properties that make sense for its type
/// (the original had one dialog per element type: CGula, CLight, CKamera, ...).
/// </summary>
internal sealed class ElementView : ICustomTypeDescriptor
{
    private static readonly string[] MaterialProps = ["Specular", "Diffuse", "Ambient", "Emission", "Shininess"];
    private static readonly string[] SurfaceProps = ["DrawStyle", "Texture"];
    private static readonly string[] MotionProps =
    [
        "MotionBoundary", "RotationAngleX", "RotationAngleY", "RotationAngleZ", "RotationPeriod", "RotationPermanent",
        "OrbitAngleX", "OrbitAngleY", "OrbitAngleZ", "OrbitPeriod", "OrbitPermanent",
    ];

    public ElementView(Element element)
    {
        Element = element;
    }

    public Element Element { get; }

    private static readonly string[] ModernSurfaceProps = ["Reflectivity", "RefractiveIndex", "Glow", "Roughness", "Absorption"];
    private static readonly string[] ModernProps = [.. ModernSurfaceProps, "CastsShadows"];

    private IEnumerable<string> Hidden() => Element switch
    {
        Scene => MaterialProps.Concat(SurfaceProps).Concat(MotionProps).Concat(ModernProps),
        GroupElement => MaterialProps.Concat(SurfaceProps).Concat(ModernProps),
        CameraElement => MaterialProps.Concat(SurfaceProps).Concat(ModernProps),
        LightElement => SurfaceProps.Append("Emission").Append("Shininess").Concat(ModernSurfaceProps),
        _ => [],
    };

    private PropertyDescriptor Adapt(PropertyDescriptor p)
    {
        if (Element is LightElement)
        {
            switch (p.Name)
            {
                case "CastsShadows": return new RenamedProperty(p, "Casts shadows", "8 Light", "The light casts shadows (modern rendering: at most 'Lights with shadows' lights at a time; ray tracing: every light).");
                case "Alpha": return new RenamedProperty(p, "Direction X", "3 Spot direction", "X component of the spot direction vector.");
                case "Beta": return new RenamedProperty(p, "Direction Y", "3 Spot direction", "Y component of the spot direction vector.");
                case "Gamma": return new RenamedProperty(p, "Direction Z", "3 Spot direction", "Z component of the spot direction vector.");
                case "Specular" or "Diffuse" or "Ambient": return new RenamedProperty(p, p.DisplayName, "4 Light colour", null);
            }
        }
        return p;
    }

    public PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
    {
        var hidden = new HashSet<string>(Hidden());
        var all = attributes == null ? TypeDescriptor.GetProperties(Element) : TypeDescriptor.GetProperties(Element, attributes);
        var list = new List<PropertyDescriptor>();
        foreach (PropertyDescriptor p in all)
            if (!hidden.Contains(p.Name))
                list.Add(Adapt(p));
        return new PropertyDescriptorCollection(list.ToArray());
    }

    public PropertyDescriptorCollection GetProperties() => GetProperties(null);
    public object GetPropertyOwner(PropertyDescriptor? pd) => Element;
    public AttributeCollection GetAttributes() => TypeDescriptor.GetAttributes(Element);
    public string? GetClassName() => Element.Type.ToString();
    public string? GetComponentName() => Element.Name;
    public TypeConverter GetConverter() => TypeDescriptor.GetConverter(Element);
    public EventDescriptor? GetDefaultEvent() => null;
    public PropertyDescriptor? GetDefaultProperty() => null;
    public object? GetEditor(Type editorBaseType) => null;
    public EventDescriptorCollection GetEvents() => EventDescriptorCollection.Empty;
    public EventDescriptorCollection GetEvents(Attribute[]? attributes) => EventDescriptorCollection.Empty;

    private sealed class RenamedProperty : PropertyDescriptor
    {
        private readonly PropertyDescriptor _inner;
        private readonly string _display;
        private readonly string _category;
        private readonly string? _description;

        public RenamedProperty(PropertyDescriptor inner, string display, string category, string? description)
            : base(inner)
        {
            _inner = inner;
            _display = display;
            _category = category;
            _description = description;
        }

        public override string DisplayName => _display;
        public override string Category => _category;
        public override string Description => _description ?? _inner.Description;
        public override Type ComponentType => _inner.ComponentType;
        public override bool IsReadOnly => _inner.IsReadOnly;
        public override Type PropertyType => _inner.PropertyType;
        public override bool CanResetValue(object component) => _inner.CanResetValue(component);
        public override object? GetValue(object? component) => _inner.GetValue(component);
        public override void ResetValue(object component) => _inner.ResetValue(component);
        public override void SetValue(object? component, object? value) => _inner.SetValue(component, value);
        public override bool ShouldSerializeValue(object component) => _inner.ShouldSerializeValue(component);
        public override TypeConverter Converter => _inner.Converter;
        public override object? GetEditor(Type editorBaseType) => _inner.GetEditor(editorBaseType);
    }
}

/// <summary>Expandable "R; G; B; A" editing of <see cref="Rgba"/> values.</summary>
internal sealed class RgbaConverter : ExpandableObjectConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string s)
        {
            var parts = s.Split([';', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is < 3 or > 4)
                throw new FormatException("Expected 'R; G; B[; A]' with values 0..1.");
            float P(int i) => Math.Clamp(float.Parse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture), 0f, 1f);
            return new Rgba(P(0), P(1), P(2), parts.Length == 4 ? P(3) : 1f);
        }
        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is Rgba c)
            return c.ToString();
        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override bool GetCreateInstanceSupported(ITypeDescriptorContext? context) => true;

    public override object CreateInstance(ITypeDescriptorContext? context, IDictionary propertyValues)
        => new Rgba(Get(propertyValues, "R"), Get(propertyValues, "G"), Get(propertyValues, "B"), Get(propertyValues, "A"));

    private static float Get(IDictionary d, string key) => d[key] is float f ? Math.Clamp(f, 0f, 1f) : 0f;
}

/// <summary>
/// Texture picker of the property grid. The model marks <see cref="Element.Texture"/> with an [Editor]
/// attribute naming this class (from the Windows Forms version); the grid recognises it by that name.
/// </summary>
public static class TextureFileEditor
{
    /// <summary>Directory of the current animation file; textures from there are stored by bare name.</summary>
    public static string? BaseDirectory { get; set; }

    /// <summary>Asks for a texture file; null when cancelled.</summary>
    public static string? Pick(System.Windows.Window? owner, string? current)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose texture (stretched to 128x128; pure white = transparent)",
            Filter = "Bitmaps (*.bmp)|*.bmp|Images (*.bmp;*.png;*.jpg;*.gif)|*.bmp;*.png;*.jpg;*.jpeg;*.gif|All files|*.*",
            InitialDirectory = BaseDirectory ?? "",
            FileName = current != null ? Path.GetFileName(current) : "",
        };
        if (dlg.ShowDialog(owner) != true) return null;
        string dir = Path.GetDirectoryName(dlg.FileName) ?? "";
        return BaseDirectory != null && string.Equals(Path.GetFullPath(dir).TrimEnd('\\'), Path.GetFullPath(BaseDirectory).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(dlg.FileName)
            : dlg.FileName;
    }
}
