using System.Numerics;
using Overbryce.Core.Import;
using Overbryce.Core.Model;

namespace Overbryce.Core.Tests;

/// <summary>The Blender scene import (2.4), from the interchange file on: no Blender needed.</summary>
public class BlenderImportTests
{
    private static readonly Vector3[] CubeCorners =
    [
        new(-1, -1, -1), new(1, -1, -1), new(1, 1, -1), new(-1, 1, -1),
        new(-1, -1, 1), new(1, -1, 1), new(1, 1, 1), new(-1, 1, 1),
    ];

    /// <summary>Blender's Z up as Overbryce's Y up: (x, y, z) → (x, z, -y).</summary>
    private static Vector3 ToYUp(Vector3 v) => new(v.X, v.Z, -v.Y);

    private static Matrix4x4 Deg(float x, float y, float z) =>
        Matrix4x4.CreateRotationX(x * MathF.PI / 180) * Matrix4x4.CreateRotationY(y * MathF.PI / 180) * Matrix4x4.CreateRotationZ(z * MathF.PI / 180);

    /// <summary>The distinct world-space vertices of a polygon mesh element as the renderers place it.</summary>
    private static List<Vector3> WorldVertices(PolyMeshElement e)
    {
        var m = Transforms.World(e);
        var list = new List<Vector3>();
        for (int i = 0; i < e.Data!.VertexCount; i++)
        {
            var p = Vector3.Transform(new Vector3(e.Data.Positions[i * 3], e.Data.Positions[i * 3 + 1], e.Data.Positions[i * 3 + 2]), m);
            if (!list.Any(q => Vector3.Distance(p, q) < 1e-4f)) list.Add(p);
        }
        return list;
    }

    private static void AssertSameVertices(IEnumerable<Vector3> expected, List<Vector3> actual)
    {
        var e = expected.ToList();
        Assert.Equal(e.Count, actual.Count);
        foreach (var p in e)
            Assert.True(actual.Any(q => Vector3.Distance(p, q) < 1e-3f), $"vertex {p} is missing");
    }

    [Fact]
    public void Polar_decomposition_splits_rotation_and_stretch()
    {
        var rot = Mat3.RotationX(0.3) * Mat3.RotationY(-1.1) * Mat3.RotationZ(2.5);
        var stretch = new Mat3(2, 0.3, 0, 0.3, 0.5, 0.1, 0, 0.1, 1.5);
        foreach (var mirror in new[] { false, true })
        {
            var s = mirror ? stretch * Mat3.Diagonal(-1, 1, 1) : stretch;
            var a = rot * s;
            var (r, k) = a.Polar();
            Assert.True(Mat3.Distance(r * r.Transposed, Mat3.Identity) < 1e-9, "the rotation is orthonormal");
            Assert.Equal(1, r.Determinant, 9);
            Assert.True(Mat3.Distance(r * k, a) < 1e-9, "rotation times stretch gives the matrix back");
            Assert.Equal(mirror, k.Determinant < 0);
        }
    }

    [Fact]
    public void Euler_angles_rebuild_the_rotation_and_follow_the_previous_frame()
    {
        var random = new Random(4);
        for (int i = 0; i < 200; i++)
        {
            var e = new Vec3d(random.NextDouble() * 7 - 3.5, random.NextDouble() * 3 - 1.5, random.NextDouble() * 7 - 3.5);
            var r = Mat3.FromEulerXyz(e);
            Assert.True(Mat3.Distance(Mat3.FromEulerXyz(r.EulerXyz(Vec3d.Zero)), r) < 1e-9);
            var c = Mat3.FromEulerCamera(e);
            Assert.True(Mat3.Distance(Mat3.FromEulerCamera(c.EulerCamera(Vec3d.Zero)), c) < 1e-9);
        }
        // gimbal lock and whole turns: the solution nearest to the previous angles
        var locked = Mat3.FromEulerXyz(new Vec3d(0.4, Math.PI / 2, 0.2));
        var angles = locked.EulerXyz(new Vec3d(0, 0, 0.2));
        Assert.True(Mat3.Distance(Mat3.FromEulerXyz(angles), locked) < 1e-9);
        Assert.Equal(0.2, angles.Z, 9);
        var turned = Mat3.RotationZ(0.1).EulerXyz(new Vec3d(0, 0, 4 * Math.PI));
        Assert.Equal(4 * Math.PI + 0.1, turned.Z, 9);
    }

    [Fact]
    public void Attenuation_follows_a_light_as_the_display_shows_it()
    {
        foreach (double radius in new[] { 0.0, 0.5 })
        {
            var (c, l, q) = BlenderConversions.FitAttenuation(radius, 1, 20);
            Assert.True(c >= 0 && l >= 0 && q >= 0);
            for (double d = 1; d <= 20; d *= 1.5)
            {
                double target = Math.Pow(d * d + radius * radius, -1 / BlenderConversions.Gamma);
                double fit = 1 / (c + l * d + q * d * d);
                Assert.InRange(fit / target, 0.9, 1.1);
            }
        }
    }

    [Fact]
    public void Spot_exponent_inverts_the_export()
    {
        foreach (var (cutoff, exponent) in new[] { (30.0, 10.0), (45.0, 4.0), (60.0, 40.0) })
        {
            // the export's soft edge (BlenderExporter.SpotBlend)
            double half = Math.Acos(Math.Pow(0.5, 1 / exponent)) * 180 / Math.PI;
            double blend = 1 - half / cutoff;
            Assert.Equal(exponent, BlenderConversions.SpotExponent(cutoff, blend), 6);
        }
        Assert.Equal(0, BlenderConversions.SpotExponent(30, 0));
    }

    [Fact]
    public void Agx_is_monotonic_and_desaturates()
    {
        double last = 0;
        for (double v = 0.001; v < 100; v *= 2)
        {
            double shown = BlenderConversions.View(v, agx: true);
            Assert.True(shown >= last);
            last = shown;
        }
        Assert.InRange(BlenderConversions.AgxWhite, 0.7, 0.85);
        var (r, g, b) = BlenderConversions.Agx(1, 0, 0);
        Assert.True(r > g && g > 0 && b > 0, "a pure red gets a little of the other channels");
    }

    [Fact]
    public void Hierarchy_and_placement_survive_with_scale_baked_into_the_mesh()
    {
        var s = new SyntheticBlenderScene();
        int cube = s.AddCube();
        var parentWorld = Matrix4x4.CreateScale(2) * Deg(0, 0, 30) * Matrix4x4.CreateTranslation(1, 2, 3);
        int root = s.AddObject("Rig Root", "EMPTY", parentWorld);
        var local = Matrix4x4.CreateScale(1, 2, 0.5f) * Deg(40, 0, 0) * Matrix4x4.CreateTranslation(0, 0, 1);
        s.AddObject("Cube", "MESH", local * parentWorld, root, cube);
        s.AddObject("Loose empty", "EMPTY", Matrix4x4.Identity);

        var (animation, report, _) = BlenderSceneImporter.Import(s.Build(), new BlenderImportOptions());
        var state = Assert.Single(animation.Scenes);
        var group = Assert.IsType<GroupElement>(Assert.Single(state.Children));
        Assert.Equal("Rig_Root", group.Name);
        var mesh = Assert.IsType<PolyMeshElement>(Assert.Single(group.Children));
        Assert.Equal("Cube", mesh.Name);

        AssertSameVertices(CubeCorners.Select(v => ToYUp(Vector3.Transform(v, local * parentWorld))), WorldVertices(mesh));
        Assert.Contains(report.Info, line => line.Contains("empties", StringComparison.Ordinal));
    }

    [Fact]
    public void A_mirrored_object_keeps_its_faces_outside()
    {
        var s = new SyntheticBlenderScene();
        s.AddObject("Mirror", "MESH", Matrix4x4.CreateScale(-1, 1, 1) * Matrix4x4.CreateTranslation(5, 0, 0), mesh: s.AddCube());
        var mesh = (PolyMeshElement)BlenderSceneImporter.Import(s.Build()).Animation.Scenes[0].Children.Single();
        var world = Transforms.World(mesh);
        var center = Vector3.Transform(Vector3.Zero, world);
        for (int i = 0; i < mesh.Data!.VertexCount; i++)
        {
            var p = Vector3.Transform(new Vector3(mesh.Data.Positions[i * 3], mesh.Data.Positions[i * 3 + 1], mesh.Data.Positions[i * 3 + 2]), world);
            var n = Vector3.TransformNormal(new Vector3(mesh.Data.Normals[i * 3], mesh.Data.Normals[i * 3 + 1], mesh.Data.Normals[i * 3 + 2]), world);
            Assert.True(Vector3.Dot(n, p - center) > 0, "normals point away from the centre");
        }
    }

    [Fact]
    public void Lights_and_cameras_are_placed_in_the_world()
    {
        var s = new SyntheticBlenderScene();
        s.AddPointLight("Lamp", Matrix4x4.CreateTranslation(0, 0, 5), 1000);
        // a Blender camera at -10 on Y, turned 90° about X, looks along +Y at the origin
        s.AddCamera("Cam", Deg(90, 0, 0) * Matrix4x4.CreateTranslation(0, -10, 0), 40, 0.5f, 250);
        s.AddObject("Box", "MESH", Matrix4x4.Identity, mesh: s.AddCube());
        s.Cameras.Add("Cam");
        var state = BlenderSceneImporter.Import(s.Build()).Animation.Scenes[0];

        var lamp = state.Lights.Single();
        Assert.True(Vector3.Distance(new Vector3(0, 5, 0), new Vector3(lamp.X, lamp.Y, lamp.Z)) < 1e-5f);
        Assert.True(lamp.Diffuse.R > 0 && lamp.LinearAttenuation + lamp.QuadraticAttenuation > 0);
        Assert.Equal(new Rgba(0, 0, 0, 1), lamp.Ambient);

        var cam = Assert.Single(state.Cameras);
        Assert.True(cam.Active);
        Assert.Equal(40, cam.FieldOfView, 3);
        Assert.Equal(0.5f, cam.Near);
        Assert.Equal(250, cam.Far);
        // in Overbryce's Y up the camera sits at +10 on Z and looks along -Z: no rotation at all
        var view = Transforms.World(cam);
        Assert.True(Vector3.Distance(new Vector3(0, 0, 10), view.Translation) < 1e-4f);
        Assert.True(Vector3.Distance(-Vector3.UnitZ, Vector3.TransformNormal(-Vector3.UnitZ, view)) < 1e-4f);
        // lights come first (they take the OpenGL light slots), then the cameras
        Assert.IsType<LightElement>(state.Children[0]);
    }

    [Fact]
    public void Names_are_short_and_unique_among_siblings()
    {
        var s = new SyntheticBlenderScene();
        int cube = s.AddCube();
        s.AddObject("Cube A", "MESH", Matrix4x4.Identity, mesh: cube);
        s.AddObject("Cube_A", "MESH", Matrix4x4.CreateTranslation(3, 0, 0), mesh: cube);
        s.AddObject("A rather long object name", "MESH", Matrix4x4.CreateTranslation(6, 0, 0), mesh: cube);
        var names = BlenderSceneImporter.Import(s.Build()).Animation.Scenes[0].Children.Select(c => c.Name).ToList();
        Assert.Equal(["Cube_A", "Cube_A_2", "A_rather_long_object"], names);
    }

    [Fact]
    public void Material_slots_become_meshes_of_a_group_and_materials_become_phong()
    {
        var s = new SyntheticBlenderScene();
        int red = s.AddMaterial("Red metal", [0.8, 0.05, 0.05], metallic: 1, roughness: 0.2);
        int glow = s.AddMaterial("Glow", [0, 0, 0], emission: [1, 0.5, 0.1], emissionStrength: 10);
        int mesh = s.AddMesh([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(1, 1, 0)], [0, 1, 2, 1, 3, 2], [0, 1], [red, glow]);
        s.AddObject("Two", "MESH", Matrix4x4.Identity, mesh: mesh);
        var group = Assert.IsType<GroupElement>(BlenderSceneImporter.Import(s.Build()).Animation.Scenes[0].Children.Single());
        Assert.Equal(["Red_metal", "Glow"], group.Children.Select(c => c.Name).ToList());
        var metal = (PolyMeshElement)group.Children[0];
        Assert.Equal(1, metal.TriangleCount);
        Assert.True(metal.Diffuse.R > 3 * metal.Diffuse.G);
        Assert.True(metal.Reflectivity > 0);
        Assert.Equal(new Rgba(0, 0, 0, 1), metal.Emission);
        var light = group.Children[1];
        Assert.True(light.Glow > 1, "a bright emission glows");
        Assert.True(light.Emission.R > light.Emission.G && light.Emission.G > light.Emission.B);
    }

    [Fact]
    public void World_colour_becomes_ambient_light_and_a_sky()
    {
        var s = new SyntheticBlenderScene { WorldColor = [0.05, 0.05, 0.2] };
        s.AddObject("Box", "MESH", Matrix4x4.Identity, mesh: s.AddCube());
        var state = BlenderSceneImporter.Import(s.Build()).Animation.Scenes[0];
        var sky = Assert.IsType<SphereElement>(state.FindChild("Sky"));
        Assert.False(sky.CastsShadows);
        Assert.True(sky.Emission.B > sky.Emission.R);
        var box = state.FindChild("Box")!;
        Assert.True(box.Ambient.B > box.Ambient.R, "the blue world lights the box blue");
    }

    [Fact]
    public void Motion_and_visibility_are_sampled_per_frame()
    {
        var s = new SyntheticBlenderScene(frames: 3);
        int cube = s.AddCube();
        s.AddObject("Mover", "MESH", [Matrix4x4.CreateTranslation(0, 0, 0), Matrix4x4.CreateTranslation(1, 0, 0), Matrix4x4.CreateTranslation(2, 0, 0)], mesh: cube);
        s.AddObject("Blink", "MESH", [Matrix4x4.Identity], mesh: cube, visible: [true, false, true]);
        var animation = BlenderSceneImporter.Import(s.Build()).Animation;
        Assert.Equal(24, animation.Fps);
        Assert.Equal(2 / 24f, animation.EndTime, 5);
        Assert.Equal(1, animation.Evaluate(1 / 24f)!.FindChild("Mover")!.X, 3);
        Assert.Null(animation.Evaluate(1 / 24f)!.FindChild("Blink"));
        Assert.NotNull(animation.Evaluate(2 / 24f)!.FindChild("Blink"));

        var still = BlenderSceneImporter.Import(s.Build(), new BlenderImportOptions { Static = true }).Animation;
        Assert.Single(still.Scenes);
    }

    [Fact]
    public void Wrong_files_are_refused()
    {
        Assert.Throws<InvalidDataException>(() => BlenderSceneFile.Parse("{\"format\":\"something else\"}", []));
        Assert.Throws<InvalidDataException>(() => BlenderSceneFile.Parse($"{{\"format\":\"{BlenderSceneFile.FormatName}\",\"version\":99}}", []));
    }
}
