using System.Diagnostics;
using Overbryce.Core.IO;
using Overbryce.Core.Model;

namespace Overbryce.Core.Import;

/// <summary>
/// The whole import of a .blend file: runs Blender in the background with overbryce_import.py, reads what it wrote,
/// builds the animation and saves it with its textures, polygon meshes and a report (<c>name.import.txt</c>).
/// </summary>
public static class BlenderImport
{
    public const string ScriptName = "overbryce_import.py";

    public sealed record Result(Animation Animation, ImportReport Report, string AniPath, string ReportPath);

    /// <summary>Blender's executable: the BLENDER environment variable, or the newest one in Program Files.</summary>
    public static string? FindBlender()
    {
        if (Environment.GetEnvironmentVariable("BLENDER") is { } env && File.Exists(env)) return env;
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"C:\Program Files" })
        {
            string dir = Path.Combine(root, "Blender Foundation");
            if (!Directory.Exists(dir)) continue;
            var found = Directory.GetDirectories(dir)
                .Select(d => Path.Combine(d, "blender.exe"))
                .Where(File.Exists)
                .OrderByDescending(p => Version.TryParse(new string(Path.GetFileName(Path.GetDirectoryName(p))!.Where(ch => char.IsDigit(ch) || ch == '.').ToArray()), out var v) ? v : new Version(0, 0))
                .FirstOrDefault();
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>Writes the Blender script (built into the program) to <paramref name="directory"/>.</summary>
    public static string WriteScript(string directory)
    {
        using var stream = typeof(BlenderImport).Assembly.GetManifestResourceStream(ScriptName)
                           ?? throw new InvalidOperationException("The Blender import script is missing from the program.");
        string path = Path.Combine(directory, ScriptName);
        using var file = File.Create(path);
        stream.CopyTo(file);
        return path;
    }

    /// <summary>
    /// Imports <paramref name="blendPath"/> into <paramref name="aniPath"/>: the textures go next to it as
    /// <c>name_image.bmp</c>, the polygon meshes into <c>name.meshes</c>.
    /// </summary>
    public static Result Run(string blendPath, string aniPath, BlenderImportOptions options, Action<string>? log = null,
        string? blender = null, CancellationToken cancel = default)
    {
        blender ??= FindBlender() ?? throw new InvalidOperationException("Blender was not found. Install Blender or set the BLENDER environment variable to blender.exe.");
        if (!File.Exists(blendPath)) throw new FileNotFoundException("The Blender file does not exist.", blendPath);
        string outDir = Path.GetDirectoryName(Path.GetFullPath(aniPath))!;
        Directory.CreateDirectory(outDir);
        string name = Element.SanitizeName(Path.GetFileNameWithoutExtension(aniPath));
        string work = Path.Combine(Path.GetTempPath(), "overbryce-import", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            string script = WriteScript(work);
            string json = Path.Combine(work, "scene.json");
            // The textures go next to the animation only once it is saved, so a cancelled or failed import leaves nothing behind
            string textures = Path.Combine(work, "textures");
            Directory.CreateDirectory(textures);
            var args = new List<string> { "-b", Path.GetFullPath(blendPath), "--factory-startup", "--python", script, "--", json,
                "--textures", textures, "--prefix", name + "_" };
            if (options.Static) args.Add("--static");
            var clock = Stopwatch.StartNew();
            log?.Invoke($"Reading {Path.GetFileName(blendPath)} with {blender} ...");
            int code = RunBlender(blender, args, Path.Combine(work, "blender.log"), log, cancel);
            if (code != 0 || !File.Exists(json))
            {
                string tail = File.Exists(Path.Combine(work, "blender.log"))
                    ? string.Join(Environment.NewLine, File.ReadLines(Path.Combine(work, "blender.log")).TakeLast(15)) : "";
                throw new InvalidOperationException($"Blender could not read the scene (exit code {code}).{Environment.NewLine}{tail}");
            }
            log?.Invoke($"Blender finished ({clock.ElapsedMilliseconds} ms); building the animation ...");
            var file = BlenderSceneFile.Load(json);
            options.Name ??= name;
            var (animation, report, textureColors) = BlenderSceneImporter.Import(file, options);
            report.Info.Insert(0, $"Imported {Path.GetFileName(blendPath)} into {Path.GetFileName(aniPath)} on {DateTime.Now:yyyy-MM-dd HH:mm}.");
            if (textureColors != null)
                foreach (var t in file.Textures)
                    Bmp24.Recolor(Path.Combine(textures, t.File), textureColors);
            cancel.ThrowIfCancellationRequested();
            foreach (string texture in Directory.EnumerateFiles(textures))
                File.Move(texture, Path.Combine(outDir, Path.GetFileName(texture)), true);
            AniFile.Save(animation, aniPath);
            string reportPath = Path.ChangeExtension(aniPath, ".import.txt");
            File.WriteAllText(reportPath, report.ToText("Blender import report"));
            log?.Invoke($"Saved {aniPath} ({animation.Scenes.Count} key states, {clock.ElapsedMilliseconds} ms); report: {reportPath}");
            return new Result(animation, report, aniPath, reportPath);
        }
        finally
        {
            try { Directory.Delete(work, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static int RunBlender(string blender, IEnumerable<string> args, string logPath, Action<string>? log, CancellationToken cancel)
    {
        var info = new ProcessStartInfo(blender)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var a in args) info.ArgumentList.Add(a);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Blender did not start.");
        using var writer = new StreamWriter(logPath);
        void Line(string? s)
        {
            if (s == null) return;
            lock (writer) writer.WriteLine(s);
            if (s.StartsWith("Overbryce:", StringComparison.Ordinal) || s.Contains("Error", StringComparison.Ordinal)) log?.Invoke(s);
        }
        process.OutputDataReceived += (_, a) => Line(a.Data);
        process.ErrorDataReceived += (_, a) => Line(a.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using (cancel.Register(() => { try { process.Kill(true); } catch (InvalidOperationException) { } }))
            process.WaitForExit();
        cancel.ThrowIfCancellationRequested();
        return process.ExitCode;
    }
}
