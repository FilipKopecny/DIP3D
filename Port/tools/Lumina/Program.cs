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

// pass 1: where the cameras are and what they look at, without the new flora
clock.Restart();
var shots = new List<Culling.Shot>();
for (int i = 0; i * 0.1f <= Story.Duration + 1e-3f; i++)
    shots.Add(Story.Sample(i * 0.1f, Intro.Duration));
foreach (var shot in shots.Where(s => s.NewWorld).Where((_, i) => i % 2 == 0))
{
    foreach (var subject in shot.Subjects)
        Planet.Sightlines.Add((shot.Eye, subject));
    // low cameras must not fly through a plant
    if (shot.Eye.Length() < Kit.Ground + 4) Planet.Keepout.Add((shot.Eye, 1.3f));
}
Planet.Keepout.AddRange(Story.Keepout());
Planet.GrowFlora();
Console.WriteLine($"Flora: {Planet.Colonies.Count} colonies, {Planet.Colonies.Sum(c => c.Members.Count)} plants, {Planet.Carpets.Count} carpets of undergrowth ({clock.ElapsedMilliseconds} ms)");
Console.WriteLine($"Route: {Planet.BeachDistance:0.0} units to the beach, {Planet.WadeDistance:0.0} to the wading point; creature ahead by {Story.Gaps()}");

// pass 2: the key states
clock.Restart();
Kit.ResetUnwrap();
var animation = new Animation("Lumina") { Fps = 25 };
// lit for the modern renderer: bright light may wash its textures out (HDR), unlike the scenes from 1998
animation.Modern.ClassicTextureLight = false;
animation.Scenes.Clear();
foreach (float t in Intro.KeyTimes())
    animation.Scenes.Add(Intro.Build(t));
foreach (float t in Story.KeyTimes())
    animation.Scenes.Add(Story.Build(t, Intro.Duration));

// the depths: a world of their own after a clean cut. Both key states at the cut stop every motion
// there, so that no spline reaches across it (the astronaut and the creature keep their element names).
const float depthsStart = Intro.Duration + Story.Duration;
Rest(animation.Scenes[^1]);
int firstDepth = animation.Scenes.Count;
foreach (float t in Depths.KeyTimes())
    animation.Scenes.Add(Depths.Build(t, depthsStart));
Rest(animation.Scenes[firstDepth]);
for (int i = 0; i * 0.1f <= Depths.Duration + 1e-3f; i++)
    shots.Add(Depths.Sample(i * 0.1f, depthsStart));

// and the cameras at every key state off the 0.1 s grid (such as the one a frame before a cut), so that what such a
// state's own camera sees is not left out of it
foreach (float t in Story.KeyTimes().Where(OffGrid))
    shots.Add(Story.Sample(t, Intro.Duration));
foreach (float t in Depths.KeyTimes().Where(OffGrid))
    shots.Add(Depths.Sample(t, depthsStart));

int culled = Culling.Apply(animation.Scenes, shots, Planet.Cullable.Concat(Depths.Cullable).ToDictionary());
Console.WriteLine($"Key states built, {culled} unseen scenery groups left out ({clock.ElapsedMilliseconds} ms)");

var path = Path.Combine(output, "Lumina.ani");
AniFile.Save(animation, path);

int introElements = Count(animation.Scenes[0].Children), storyElements = animation.Scenes.Skip(1).Max(s => Count(s.Children));
Console.WriteLine($"{path}: {animation.Scenes.Count} key states ({introElements} elements in the intro, up to {storyElements} in the story), "
                  + $"{new FileInfo(path).Length / 1024} KB, {Intro.Duration + Story.Duration + Depths.Duration:0.#} s at {animation.Fps} fps");

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

static bool OffGrid(float t) => MathF.Abs(t * 10 - MathF.Round(t * 10)) > 1e-3f;

static void Rest(Element e)
{
    e.MotionBoundary = true;
    if (e is GroupElement g)
        foreach (var child in g.Children) Rest(child);
}

static int Count(IEnumerable<Element> elements)
    => elements.Sum(e => 1 + (e is GroupElement g ? Count(g.Children) : 0));

static string? FindPortRoot(string start)
{
    for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "Overbryce.sln"))) return dir.FullName;
    return null;
}
