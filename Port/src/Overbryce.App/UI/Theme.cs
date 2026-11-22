using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace Overbryce.App.UI;

/// <summary>Light or dark editor; <see cref="System"/> follows the Windows setting (also when it changes).</summary>
internal enum ThemeChoice
{
    System,
    Light,
    Dark,
}

/// <summary>
/// The editor's look: WPF's Fluent theme (the Windows 11 controls) in light or dark, plus the editor's
/// own styles and icons. The choice is remembered per user. It changes only the editor, never the
/// rendered scene.
/// </summary>
internal static class Theme
{
    public static ThemeChoice Choice { get; private set; }

    /// <summary>Raised after the theme changed.</summary>
    public static event Action? Changed;

    /// <summary>Call once, before the first window is created.</summary>
    public static void Initialize(Application app)
    {
        Choice = Preferences.Load().Theme;
        Apply(app);
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Overbryce;component/UI/Styles.xaml", UriKind.Absolute),
        });
    }

    public static void Set(ThemeChoice choice)
    {
        if (choice == Choice) return;
        Choice = choice;
        Apply(Application.Current);
        var prefs = Preferences.Load();
        prefs.Theme = choice;
        prefs.Save();
        Changed?.Invoke();
    }

    // WPF marks the Fluent theme as experimental (WPF0001)
#pragma warning disable WPF0001
    private static void Apply(Application app) => app.ThemeMode = Choice switch
    {
        ThemeChoice.Light => ThemeMode.Light,
        ThemeChoice.Dark => ThemeMode.Dark,
        _ => ThemeMode.System,
    };
#pragma warning restore WPF0001
}

/// <summary>Per-user editor preferences in %APPDATA%\Overbryce\settings.json.</summary>
internal sealed class Preferences
{
    [JsonConverter(typeof(JsonStringEnumConverter<ThemeChoice>))]
    public ThemeChoice Theme { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Overbryce", "settings.json");

    public static Preferences Load()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath)) ?? new() : new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new(); // unreadable preferences are not worth an error message
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // the theme still applies to this run
        }
    }
}
