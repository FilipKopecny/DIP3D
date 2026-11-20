using System.ComponentModel;
using System.Drawing.Design;
using Overbryce.App.UI;
using Overbryce.Core;

namespace Overbryce.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Rgba lives in the UI-free core library; attach the WinForms editing support here.
        TypeDescriptor.AddAttributes(typeof(Rgba),
            new TypeConverterAttribute(typeof(RgbaConverter)),
            new EditorAttribute(typeof(RgbaEditor), typeof(UITypeEditor)));

        ApplicationConfiguration.Initialize();

        // Overbryce.exe file.ani
        // Overbryce.exe file.ani --snapshot out.png [--time 12.5] [--view camera|front|side|top] [--wire] [--size 800x600]
        // Overbryce.exe file.ani --frames outFolder [--from 0] [--to 10] [--view ...] [--wire] [--size 1280x720]
        string? file = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : null;
        SnapshotRequest? snapshot = null;
        int s = Array.IndexOf(args, "--snapshot");
        int f = Array.IndexOf(args, "--frames");
        int target = s >= 0 ? s : f;
        if (file != null && target >= 0 && target + 1 < args.Length)
        {
            string? Opt(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
            float? Num(string name) => Opt(name) is { } v ? float.Parse(v, System.Globalization.CultureInfo.InvariantCulture) : null;
            var size = (Opt("--size") ?? "800x600").Split('x');
            snapshot = new SnapshotRequest(
                Path.GetFullPath(args[target + 1]),
                Num("--time"),
                Enum.TryParse<Rendering.ViewMode>(Opt("--view") ?? "camera", true, out var mode) ? mode : Rendering.ViewMode.Camera,
                args.Contains("--wire"),
                new Size(int.Parse(size[0]), int.Parse(size[1])),
                Frames: s < 0,
                From: Num("--from"),
                To: Num("--to"));
        }

        Application.Run(new MainForm(file, snapshot));
    }
}
