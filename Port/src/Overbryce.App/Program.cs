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

        // Overbryce.exe file.ani
        // Overbryce.exe file.ani --snapshot out.png [--time 12.5] [--view camera|front|side|top] [--wire] [--size 800x600] [--modern|--classic]
        // Overbryce.exe file.ani --frames outFolder [--from 0] [--to 10] [--view ...] [--wire] [--size 1280x720] [--modern|--classic]
        // Overbryce.exe file.ani --blender outFolder [--no-blend]
        string? file = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : null;
        int b = Array.IndexOf(args, "--blender");
        if (file != null && b >= 0 && b + 1 < args.Length)
        {
            AttachConsole(-1);   // a window application: write to the console it was started from
            Console.WriteLine();
            try
            {
                Environment.ExitCode = Export.BlenderExporter.Run(Path.GetFullPath(file), Path.GetFullPath(args[b + 1]),
                    !args.Contains("--no-blend"), Console.WriteLine);
            }
            catch (Exception ex) when (ex is IOException or Core.IO.AniFormatException or InvalidOperationException or UnauthorizedAccessException)
            {
                Console.WriteLine("Export failed: " + ex.Message);
                Environment.ExitCode = 1;
            }
            return;
        }

        ApplicationConfiguration.Initialize();

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
                To: Num("--to"),
                Modern: args.Contains("--modern") ? true : args.Contains("--classic") ? false : null);
        }

        Application.Run(new MainForm(file, snapshot));
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);
}
