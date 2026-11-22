using System.Numerics;
using System.Text.Json;
using Overbryce.Core.Import;
using Overbryce.Core.IO;
using Overbryce.Core.Model;

namespace Overbryce.Core.Tests;

/// <summary>
/// The round trip: a scene the Overbryce export made comes back as its elements, from the tags and custom properties
/// the export puts on the Blender objects (written here as overbryce_import.py would read them).
/// </summary>
public class BlenderRoundTripTests
{
    private const float Rad = MathF.PI / 180;

    /// <summary>The animation's settings and key states without elements, as the export writes them (ob_settings).</summary>
    private static string Skeleton(params float[] times)
    {
        var a = new Animation("Original") { Fps = 24 };
        foreach (float t in times) a.Scenes.Add(new Scene("S" + t) { AniTime = t, FogStart = 3 });
        a.Modern.Enabled = true;
        return AniFile.Serialize(a);
    }

    /// <summary>An object's ob_tag as the export writes it.</summary>
    private static string Tag(Element e, int order, int[] states, int[]? stops = null, int verts = -1)
    {
        var holder = new Animation("tag");
        var scene = new Scene("tag");
        scene.Add(e);
        holder.Scenes.Add(scene);
        return JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["v"] = 1, ["name"] = e.Name, ["order"] = order, ["states"] = states, ["stops"] = stops ?? [], ["verts"] = verts,
            ["ani"] = AniFile.Serialize(holder),
        });
    }

    private static float[] Local(Vector3 location, Vector3 rotationDegrees, float scale = 1) =>
        [location.X, location.Y, location.Z, rotationDegrees.X * Rad, rotationDegrees.Y * Rad, rotationDegrees.Z * Rad, scale, scale, scale];

    private static SyntheticBlenderScene Exported(params float[] times)
    {
        var s = new SyntheticBlenderScene(times.Length) { FrameNumbers = times.Select(t => (double)t * 24).ToArray() };
        s.SceneProps["ob_settings"] = Skeleton(times);
        s.SceneProps["ob_keytimes"] = times.Select(t => t * 24.0).ToArray();
        return s;
    }

    [Fact]
    public void Tagged_objects_come_back_as_their_elements()
    {
        var s = Exported(0, 2);
        int cube = s.AddCube();

        int arm = s.AddObject("Arm", "EMPTY", Matrix4x4.Identity);
        s.Text(arm, "ob_tag", Tag(new GroupElement("Arm"), 1, [0, 1]));
        s.Prop(arm, "_local", f => Local(Vector3.Zero, new Vector3(0, 30 * f, 0)));
        int pos = s.AddObject("Arm.pos", "EMPTY", Matrix4x4.Identity, parent: arm);
        s.Text(pos, "ob_tag", "{\"role\": \"pos\"}");
        s.Prop(pos, "_local", f => Local(new Vector3(1, 2, 3), Vector3.Zero));

        int ball = s.AddObject("Ball", "MESH", Matrix4x4.Identity, parent: pos, mesh: cube);
        s.Text(ball, "ob_tag", Tag(new SphereElement("Ball") { QualityX = 20, QualityY = 10 }, 2, [0, 1], stops: [1], verts: 8));
        s.Prop(ball, "_local", f => Local(new Vector3(0, 0, f), new Vector3(10, 0, 0)));
        s.Prop(ball, "ob_shape", f => [1 + f]);
        s.Prop(ball, "ob_dif", _ => [0.2f, 0.4f, 0.6f]);
        s.Prop(ball, "ob_alpha", _ => [0.5f]);
        s.Prop(ball, "ob_ext", _ => [1, 1, 1, 0.3f, 1.5f, 2, 0.1f, 0, 0]);

        // scaled in Blender: no longer a sphere of its own
        int big = s.AddObject("Big", "MESH", Matrix4x4.CreateScale(2), mesh: cube);
        s.Text(big, "ob_tag", Tag(new SphereElement("Big"), 3, [1], verts: 8));
        s.Prop(big, "_local", _ => Local(Vector3.Zero, Vector3.Zero, 2));

        int lamp = s.AddPointLight("Lamp", Matrix4x4.Identity, 100);
        s.Text(lamp, "ob_tag", Tag(new LightElement("Lamp"), 0, [0, 1]));
        s.Prop(lamp, "_local", _ => Local(new Vector3(4, 5, 6), Vector3.Zero));
        s.Prop(lamp, "ob_light", _ => [0, -1, 0, 1, 0.5f, 0.25f, 1, 0, 0, 0, 1, 1, 1, 1, 1, 2, 0.5f, 0.1f, 45, 3, 0.2f, 1]);

        // an object added in Blender comes in as usual
        s.AddObject("Added", "MESH", Matrix4x4.CreateTranslation(0, 0, 5), mesh: cube);

        var a = BlenderSceneImporter.Import(s.Build()).Animation;

        Assert.Equal("Original", a.Name);
        Assert.Equal(2, a.Scenes.Count);
        Assert.Equal(2f, a.Scenes[1].AniTime);
        Assert.Equal(3f, a.Scenes[0].FogStart);
        Assert.True(a.Modern.Enabled);
        Assert.Equal(["Lamp", "Arm", "Added"], a.Scenes[0].Children.Select(c => c.Name));

        var group = Assert.IsType<GroupElement>(a.Scenes[1].FindChild("Arm"));
        Assert.Equal(new Vector3(1, 2, 3), new Vector3(group.X, group.Y, group.Z));
        Assert.Equal(30, group.Beta, 3);
        var sphere = Assert.IsType<SphereElement>(group.FindChild("Ball"));
        Assert.Equal(2, sphere.Radius, 5);
        Assert.Equal(20, sphere.QualityX);
        Assert.Equal(1f, sphere.Z);
        Assert.Equal(10, sphere.Alpha, 3);
        Assert.Equal(new Rgba(0.2f, 0.4f, 0.6f, 0.5f), sphere.Diffuse);
        Assert.Equal(0.3f, sphere.Reflectivity);
        Assert.Equal(1.5f, sphere.RefractiveIndex);
        Assert.Equal(2f, sphere.Glow);
        Assert.True(sphere.MotionBoundary);
        Assert.False(((GroupElement)a.Scenes[0].FindChild("Arm")!).FindChild("Ball")!.MotionBoundary);

        Assert.Null(a.Scenes[0].FindChild("Big"));
        var scaled = Assert.IsType<PolyMeshElement>(a.Scenes[1].FindChild("Big"));
        var (lo, hi) = scaled.Data!.Bounds;
        Assert.Equal(4, hi.X - lo.X, 3);

        var light = Assert.IsType<LightElement>(a.Scenes[0].FindChild("Lamp"));
        Assert.Equal(new Vector3(4, 5, 6), new Vector3(light.X, light.Y, light.Z));
        Assert.Equal(-1f, light.Beta);
        Assert.Equal(new Rgba(1, 0.5f, 0.25f, 1), light.Diffuse);
        Assert.Equal(0.1, light.QuadraticAttenuation, 5);
        Assert.Equal(45, light.SpotCutoff, 5);
        Assert.Equal(0.2f, light.LightRadius, 5);

        Assert.IsType<PolyMeshElement>(a.Scenes[0].FindChild("Added"));
    }

    [Fact]
    public void Permanent_rotation_is_not_counted_twice()
    {
        var s = Exported(0, 2);
        int cube = s.AddCube();
        int top = s.AddObject("Top", "MESH", Matrix4x4.Identity, mesh: cube);
        var template = new BoxElement("Top") { RotationPermanent = true, RotationAngleY = 90, RotationPeriod = 1 };
        s.Text(top, "ob_tag", Tag(template, 0, [0, 1], verts: 8));
        // Blender has the element as Overbryce shows it: turned by the permanent rotation up to each key state
        s.Prop(top, "_local", f => Local(Vector3.Zero, new Vector3(0, f == 0 ? 0 : 190, 0)));

        var a = BlenderSceneImporter.Import(s.Build()).Animation;

        var shown = a.Evaluate(a.EndTime)!.FindChild("Top")!;
        Assert.Equal(190, shown.Beta, 2);
        Assert.NotEqual(190, a.Scenes[1].FindChild("Top")!.Beta, 2);
        Assert.Equal(0, a.Evaluate(0)!.FindChild("Top")!.Beta, 3);
    }
}
