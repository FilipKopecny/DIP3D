using System.Diagnostics;
using System.Numerics;
using Lumina;
using Overbryce.Core.IO;
using Overbryce.Core.Model;

// Generates samples/Lumina: the textures and Lumina.ani (an Overbryce animation file).
var root = FindPortRoot(AppContext.BaseDirectory) ?? FindPortRoot(Environment.CurrentDirectory)
           ?? throw new InvalidOperationException("Cannot find the Port folder (the one with Overbryce.sln).");
var output = Path.Combine(root, "samples", "Lumina");
var originals = Path.GetFullPath(Path.Combine(root, "..", "DIP", "SCENES"));
Directory.CreateDirectory(output);

var clock = Stopwatch.StartNew();
Textures.Generate(output, originals);
Console.WriteLine($"Textures written to {output} ({clock.ElapsedMilliseconds} ms)");

var animation = new Animation("Lumina") { Fps = 25 };
animation.Scenes.Clear();
foreach (float t in Intro.KeyTimes())
    animation.Scenes.Add(Intro.Build(t));
foreach (float t in Story.KeyTimes())
    animation.Scenes.Add(Story.Build(t, Intro.Duration));

var path = Path.Combine(output, "Lumina.ani");
AniFile.Save(animation, path);

int introElements = Count(animation.Scenes[0].Children), storyElements = Count(animation.Scenes[^1].Children);
Console.WriteLine($"{path}: {animation.Scenes.Count} key states ({introElements} elements in the intro, {storyElements} in the story), "
                  + $"{new FileInfo(path).Length / 1024} KB, {Intro.Duration + Story.Duration} s at {animation.Fps} fps");

// how close the lander really comes to the camera in the interpolated animation
float closest = float.MaxValue, when = 0;
for (float t = 8.5f; t < 12f; t += 0.01f)
{
    var s = animation.Evaluate(t)!;
    var cam = s.FindChild("VKamera")!;
    var probe = s.FindChild("VSonda")!;
    float d = Vector3.Distance(new Vector3(cam.X, cam.Y, cam.Z), new Vector3(probe.X, probe.Y, probe.Z));
    if (d < closest) (closest, when) = (d, t);
}
Console.WriteLine($"fly-by: the lander passes {closest:0.00} units from the camera at {when:0.00} s");
return 0;

static int Count(IEnumerable<Element> elements)
    => elements.Sum(e => 1 + (e is GroupElement g ? Count(g.Children) : 0));

static string? FindPortRoot(string start)
{
    for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "Overbryce.sln"))) return dir.FullName;
    return null;
}
