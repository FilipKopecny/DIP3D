using System.ComponentModel;
using Overbryce.App.UI;
using Overbryce.Core;

namespace Overbryce.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Rgba lives in the UI-free core library; attach its text form ("R; G; B; A") for the property grid here.
        TypeDescriptor.AddAttributes(typeof(Rgba), new TypeConverterAttribute(typeof(RgbaConverter)));

        // Overbryce.exe file.ani
        // Overbryce.exe file.ani --snapshot out.png [--time 12.5] [--view camera|front|side|top] [--wire] [--size 800x600] [--modern|--classic]
        // Overbryce.exe file.ani --frames outFolder [--from 0] [--to 10] [--view ...] [--wire] [--size 1280x720] [--modern|--classic]
        //   with --raytrace [--samples 64] [--bounces 6] either is ray traced (with --classic: the classic look through the tracer)
        // Overbryce.exe file.ani --blender outFolder [--no-blend]
        // Overbryce.exe scene.blend --import-blender out.ani [--static] [--axis auto|yup|zup] [--tolerance 0.001]
        string? file = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : null;
        int ib = Array.IndexOf(args, "--import-blender");
        if (file != null && ib >= 0 && ib + 1 < args.Length)
        {
            AttachConsole(-1);
            Console.WriteLine();
            try
            {
                int ax = Array.IndexOf(args, "--axis");
                int tol = Array.IndexOf(args, "--tolerance");
                var options = new Core.Import.BlenderImportOptions
                {
                    Static = args.Contains("--static"),
                    Axis = ax >= 0 && ax + 1 < args.Length && Enum.TryParse<Core.Import.ImportAxis>(args[ax + 1], true, out var axis) ? axis : Core.Import.ImportAxis.Auto,
                };
                if (tol >= 0 && tol + 1 < args.Length && double.TryParse(args[tol + 1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double tolerance))
                    options.Tolerance = Math.Max(0, tolerance);
                var result = Core.Import.BlenderImport.Run(Path.GetFullPath(file), Path.GetFullPath(args[ib + 1]), options, Console.WriteLine);
                foreach (var line in result.Report.Approximated) Console.WriteLine("Approximated: " + line);
                foreach (var line in result.Report.Dropped) Console.WriteLine("Left out: " + line);
                Environment.ExitCode = 0;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                Console.WriteLine("Import failed: " + ex.Message);
                Environment.ExitCode = 1;
            }
            return;
        }
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

        var app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose };
        Theme.Initialize(app);

        SnapshotRequest? snapshot = null;
        int s = Array.IndexOf(args, "--snapshot");
        int f = Array.IndexOf(args, "--frames");
        int target = s >= 0 ? s : f;
        if (file != null && target >= 0 && target + 1 < args.Length)
        {
            string? Opt(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
            float? Num(string name) => Opt(name) is { } v ? float.Parse(v, System.Globalization.CultureInfo.InvariantCulture) : null;
            var size = (Opt("--size") ?? "800x600").Split('x');
            bool raytrace = args.Contains("--raytrace");
            if (raytrace)
            {
                AttachConsole(-1);   // report each traced picture to the console it was started from
                Console.WriteLine();
            }
            snapshot = new SnapshotRequest(
                Path.GetFullPath(args[target + 1]),
                Num("--time"),
                Enum.TryParse<Rendering.ViewMode>(Opt("--view") ?? "camera", true, out var mode) ? mode : Rendering.ViewMode.Camera,
                args.Contains("--wire"),
                new System.Drawing.Size(int.Parse(size[0]), int.Parse(size[1])),
                Frames: s < 0,
                From: Num("--from"),
                To: Num("--to"),
                Modern: args.Contains("--modern") ? true : args.Contains("--classic") ? false : null,
                Raytrace: raytrace,
                Samples: Opt("--samples") is { } sp ? int.Parse(sp, System.Globalization.CultureInfo.InvariantCulture) : null,
                Bounces: Opt("--bounces") is { } bo ? int.Parse(bo, System.Globalization.CultureInfo.InvariantCulture) : null,
                Log: raytrace ? Console.WriteLine : null);
        }

        app.Run(new MainWindow(file, snapshot));
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);
}
