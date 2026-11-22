using System.Globalization;
using System.Text;
using Overbryce.Core.Model;

namespace Overbryce.Core.IO;

/// <summary>
/// The extension block of Overbryce 2.1 and later, written after <c>KoniecAnimacie</c>. The 1998 program and
/// Overbryce 2.0 stop reading at that tag, so they open such files unchanged and simply do not see
/// the block (backward compatible files). The block is a list of records, each ended by <c>;</c>,
/// whose values are <c>key=value</c> tokens:
/// <code>
/// Overbryce2 1
/// Modern enabled=1 bloomStrength=0.6 ;
/// Raytrace samples=64 ;
/// Element 2 0.3.1 reflectivity=0.8 ior=1.5 ;
/// EndOverbryce2
/// </code>
/// <c>Element</c> addresses an element by its key state (0-based) and the child indices from the
/// state down to it. Unknown records and keys (from a newer version) are kept and written back, and
/// bad values fall back to the defaults, so a newer file still opens (forward compatible).
/// </summary>
public static class AniExtensions
{
    public const string Begin = "Overbryce2";
    public const string End = "EndOverbryce2";
    public const int Version = 1;
    private const string RecordEnd = ";";
    private const string ModernRecord = "Modern";
    private const string ElementRecord = "Element";
    private const string RaytraceRecord = "Raytrace";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ------------------------------------------------------------------ keys

    private sealed record Key<T>(string Name, Func<T, string> Get, Action<T, string> Set, Func<T, bool> IsDefault);

    private static Key<T> FloatKey<T>(string name, Func<T, float> get, Action<T, float> set, float def) =>
        new(name, o => Format(get(o)), (o, v) => { if (TryFloat(v, out float f)) set(o, f); }, o => get(o) == def);

    private static Key<T> IntKey<T>(string name, Func<T, int> get, Action<T, int> set, int def) =>
        new(name, o => get(o).ToString(Inv), (o, v) => { if (int.TryParse(v, NumberStyles.Integer, Inv, out int i)) set(o, i); }, o => get(o) == def);

    private static Key<T> BoolKey<T>(string name, Func<T, bool> get, Action<T, bool> set, bool def) =>
        new(name, o => get(o) ? "1" : "0", (o, v) => { if (v is "0" or "1") set(o, v == "1"); }, o => get(o) == def);

    private static readonly Key<ModernSettings>[] ModernKeys =
    [
        BoolKey<ModernSettings>("enabled", s => s.Enabled, (s, v) => s.Enabled = v, false),
        FloatKey<ModernSettings>("exposure", s => s.Exposure, (s, v) => s.Exposure = v, 1f),
        BoolKey<ModernSettings>("bloom", s => s.Bloom, (s, v) => s.Bloom = v, true),
        FloatKey<ModernSettings>("bloomStrength", s => s.BloomStrength, (s, v) => s.BloomStrength = v, 0.5f),
        FloatKey<ModernSettings>("bloomThreshold", s => s.BloomThreshold, (s, v) => s.BloomThreshold = v, 0.9f),
        BoolKey<ModernSettings>("shadows", s => s.Shadows, (s, v) => s.Shadows = v, true),
        FloatKey<ModernSettings>("shadowSoftness", s => s.ShadowSoftness, (s, v) => s.ShadowSoftness = v, 1f),
        IntKey<ModernSettings>("shadowLights", s => s.ShadowLights, (s, v) => s.ShadowLights = v, 8),
        IntKey<ModernSettings>("shadowMapSize", s => s.ShadowMapSize, (s, v) => s.ShadowMapSize = v, 1024),
        BoolKey<ModernSettings>("ao", s => s.AmbientOcclusion, (s, v) => s.AmbientOcclusion = v, true),
        FloatKey<ModernSettings>("aoRadius", s => s.AoRadius, (s, v) => s.AoRadius = v, 0.5f),
        FloatKey<ModernSettings>("aoStrength", s => s.AoStrength, (s, v) => s.AoStrength = v, 1f),
        BoolKey<ModernSettings>("reflections", s => s.Reflections, (s, v) => s.Reflections = v, true),
        IntKey<ModernSettings>("probes", s => s.ReflectionProbes, (s, v) => s.ReflectionProbes = v, 4),
        IntKey<ModernSettings>("probeSize", s => s.ProbeSize, (s, v) => s.ProbeSize = v, 256),
    ];

    private static readonly Key<RaytraceSettings>[] RaytraceKeys =
    [
        IntKey<RaytraceSettings>("samples", s => s.Samples, (s, v) => s.Samples = v, 16),
        IntKey<RaytraceSettings>("bounces", s => s.Bounces, (s, v) => s.Bounces = v, 6),
        new("geometry", s => s.Geometry == RaytraceGeometry.Smooth ? "smooth" : "faceted",
            (s, v) => { if (v is "smooth" or "faceted") s.Geometry = v == "smooth" ? RaytraceGeometry.Smooth : RaytraceGeometry.Faceted; },
            s => s.Geometry == RaytraceGeometry.Faceted),
        BoolKey<RaytraceSettings>("shadows", s => s.Shadows, (s, v) => s.Shadows = v, true),
        BoolKey<RaytraceSettings>("ao", s => s.AmbientOcclusion, (s, v) => s.AmbientOcclusion = v, false),
        IntKey<RaytraceSettings>("aoRays", s => s.AoRays, (s, v) => s.AoRays = v, 4),
        FloatKey<RaytraceSettings>("aoDistance", s => s.AoDistance, (s, v) => s.AoDistance = v, 0.5f),
        FloatKey<RaytraceSettings>("shutter", s => s.Shutter, (s, v) => s.Shutter = v, 0f),
        IntKey<RaytraceSettings>("blurSteps", s => s.BlurSteps, (s, v) => s.BlurSteps = v, 3),
    ];

    private static readonly Key<Element>[] ElementKeys =
    [
        FloatKey<Element>("reflectivity", e => e.Reflectivity, (e, v) => e.Reflectivity = Math.Clamp(v, 0f, 1f), 0f),
        FloatKey<Element>("ior", e => e.RefractiveIndex, (e, v) => e.RefractiveIndex = Math.Max(1f, v), 1f),
        FloatKey<Element>("glow", e => e.Glow, (e, v) => e.Glow = Math.Max(0f, v), 1f),
        BoolKey<Element>("shadows", e => e.CastsShadows, (e, v) => e.CastsShadows = v, true),
        FloatKey<Element>("roughness", e => e.Roughness, (e, v) => e.Roughness = Math.Clamp(v, 0f, 1f), 0f),
        FloatKey<Element>("absorption", e => e.Absorption, (e, v) => e.Absorption = Math.Max(0f, v), 0f),
    ];

    private static readonly Key<Element>[] LightKeys =
    [
        .. ElementKeys,
        FloatKey<Element>("lightRadius", e => ((LightElement)e).LightRadius, (e, v) => ((LightElement)e).LightRadius = Math.Max(0f, v), 0f),
    ];

    private static readonly Key<Element>[] CameraKeys =
    [
        .. ElementKeys,
        FloatKey<Element>("aperture", e => ((CameraElement)e).Aperture, (e, v) => ((CameraElement)e).Aperture = Math.Max(0f, v), 0f),
        FloatKey<Element>("focus", e => ((CameraElement)e).Focus, (e, v) => ((CameraElement)e).Focus = Math.Max(0.01f, v), CameraElement.DefaultFocus),
    ];

    /// <summary>The keys of an element; a key that does not belong to its type is kept as unknown.</summary>
    private static Key<Element>[] KeysOf(Element e) => e switch
    {
        LightElement => LightKeys,
        CameraElement => CameraKeys,
        _ => ElementKeys,
    };

    // ------------------------------------------------------------------ reading

    /// <summary>
    /// Reads the extension block, if any, from the tokens after <c>KoniecAnimacie</c>. Never throws:
    /// whatever cannot be understood is skipped.
    /// </summary>
    public static void Read(AniTokenReader r, Animation animation)
    {
        try
        {
            // anything else after the end of the animation is ignored, as the original did
            while (!r.AtEnd && r.Next() != Begin) { }
            if (r.AtEnd) return;
            r.Next(); // version: later versions keep the record syntax, so it only documents the writer

            var record = new List<string>();
            while (!r.AtEnd)
            {
                string token = r.Next();
                if (token == End) break;
                if (token != RecordEnd)
                {
                    record.Add(token);
                    continue;
                }
                if (record.Count > 0) ApplyRecord(record, animation);
                record.Clear();
            }
        }
        catch (AniFormatException)
        {
            // a damaged block: keep what was read
        }
        animation.Modern.Normalize();
        animation.Raytrace.Normalize();
    }

    private static void ApplyRecord(List<string> record, Animation animation)
    {
        switch (record[0])
        {
            case ModernRecord:
                ApplyKeys(record.Skip(1), animation.Modern, ModernKeys, animation.Modern.Unknown);
                return;

            case RaytraceRecord:
                ApplyKeys(record.Skip(1), animation.Raytrace, RaytraceKeys, animation.Raytrace.Unknown);
                return;

            case ElementRecord when record.Count >= 3
                                    && int.TryParse(record[1], NumberStyles.Integer, Inv, out int state)
                                    && state >= 0 && state < animation.Scenes.Count
                                    && Find(animation.Scenes[state], record[2]) is { } element:
                element.ExtensionData ??= new Dictionary<string, string>(StringComparer.Ordinal);
                ApplyKeys(record.Skip(3), element, KeysOf(element), element.ExtensionData);
                if (element.ExtensionData.Count == 0) element.ExtensionData = null;
                return;

            case ElementRecord:
                return; // the element no longer exists (the file was edited by an older version): drop it

            default:
                animation.UnknownExtensions.Add(string.Join(' ', record));
                return;
        }
    }

    private static void ApplyKeys<T>(IEnumerable<string> tokens, T target, Key<T>[] keys, Dictionary<string, string> unknown)
    {
        foreach (string token in tokens)
        {
            int eq = token.IndexOf('=');
            if (eq <= 0) continue;
            string name = token[..eq], value = token[(eq + 1)..];
            var key = Array.Find(keys, k => k.Name == name);
            if (key != null) key.Set(target, value);
            else unknown[name] = value;
        }
    }

    private static Element? Find(Scene scene, string path)
    {
        Element current = scene;
        foreach (string part in path.Split('.'))
        {
            if (current is not GroupElement g || !int.TryParse(part, NumberStyles.Integer, Inv, out int i) || i < 0 || i >= g.Children.Count)
                return null;
            current = g.Children[i];
        }
        return ReferenceEquals(current, scene) ? null : current;
    }

    // ------------------------------------------------------------------ writing

    /// <summary>The extension block for <paramref name="animation"/>, or an empty string when it has nothing to store.</summary>
    public static string Write(Animation animation)
    {
        var records = new List<string>();

        var modern = new StringBuilder();
        foreach (var key in ModernKeys)
            if (!key.IsDefault(animation.Modern))
                modern.Append(' ').Append(key.Name).Append('=').Append(key.Get(animation.Modern));
        foreach (var (name, value) in animation.Modern.Unknown)
            modern.Append(' ').Append(name).Append('=').Append(value);
        if (modern.Length > 0) records.Add(ModernRecord + modern);

        var raytrace = new StringBuilder();
        foreach (var key in RaytraceKeys)
            if (!key.IsDefault(animation.Raytrace))
                raytrace.Append(' ').Append(key.Name).Append('=').Append(key.Get(animation.Raytrace));
        foreach (var (name, value) in animation.Raytrace.Unknown)
            raytrace.Append(' ').Append(name).Append('=').Append(value);
        if (raytrace.Length > 0) records.Add(RaytraceRecord + raytrace);

        for (int s = 0; s < animation.Scenes.Count; s++)
            WriteElements(animation.Scenes[s], s.ToString(Inv), "", records);

        records.AddRange(animation.UnknownExtensions);
        if (records.Count == 0) return "";

        var sb = new StringBuilder();
        sb.Append("\r\n").Append(Begin).Append(' ').Append(Version.ToString(Inv)).Append("\r\n");
        foreach (string record in records) sb.Append(record).Append(' ').Append(RecordEnd).Append("\r\n");
        sb.Append(End).Append("\r\n");
        return sb.ToString();
    }

    private static void WriteElements(GroupElement group, string state, string prefix, List<string> records)
    {
        for (int i = 0; i < group.Children.Count; i++)
        {
            var e = group.Children[i];
            string path = prefix + i.ToString(Inv);
            var values = new StringBuilder();
            foreach (var key in KeysOf(e))
                if (!key.IsDefault(e))
                    values.Append(' ').Append(key.Name).Append('=').Append(key.Get(e));
            if (e.ExtensionData != null)
                foreach (var (name, value) in e.ExtensionData)
                    values.Append(' ').Append(name).Append('=').Append(value);
            if (values.Length > 0) records.Add($"{ElementRecord} {state} {path}{values}");
            if (e is GroupElement g) WriteElements(g, state, path + ".", records);
        }
    }

    private static string Format(float v) => v.ToString("G6", Inv);

    private static bool TryFloat(string s, out float v) =>
        float.TryParse(s, NumberStyles.Float, Inv, out v) && float.IsFinite(v);
}
