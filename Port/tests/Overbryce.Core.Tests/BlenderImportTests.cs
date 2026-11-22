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

    /// <summary>Blender's Z up as Overbryce's Y up: (x, y, z) â†’ (x, z, -y).</summary>
    private static Vector3 ToYUp(Vector3 v) => new(v.X, v.Z, -v.Y);

    /// <summary>The cubes of these tests stay polygon meshes (they are about placement, not about shapes).</summary>
    private static BlenderImportOptions AsMeshes => new() { RecognizeShapes = false };

    [Fact]
    public void A_plain_cube_and_plane_become_a_box_and_a_rectangle()
    {
        var s = new SyntheticBlenderScene();
        s.AddObject("Cube", "MESH", Matrix4x4.CreateScale(1, 2, 3), mesh: s.AddCube());
        Vector3[] plane = [new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0)];
        s.AddObject("Plane", "MESH", Matrix4x4.Identity, mesh: s.AddMesh(plane, [0, 1, 2, 0, 2, 3]));
        Vector3[] offCentre = [.. CubeCorners.Select(v => v + new Vector3(3, 0, 0))];
        s.AddObject("Moved", "MESH", Matrix4x4.Identity, mesh: s.AddMesh(offCentre,
            [0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 2, 3, 7, 2, 7, 6, 1, 2, 6, 1, 6, 5, 3, 0, 4, 3, 4, 7]));

        var state = BlenderSceneImporter.Import(s.Build()).Animation.Scenes[0];

        var box = Assert.IsType<BoxElement>(state.FindChild("Cube"));
        Assert.Equal(new Vector3(2, 4, 6), new Vector3(box.SizeX, box.SizeY, box.SizeZ));
        var rect = Assert.IsType<RectangleElement>(state.FindChild("Plane"));
        Assert.Equal(2f, rect.SizeX);
        Assert.IsType<PolyMeshElement>(state.FindChild("Moved"));   // not centred: stays a polygon mesh
    }

    [Fact]
    public void A_mesh_a_rig_moves_as_a_whole_moves_by_its_placement()
    {
        const int frames = 25;
        var s = new SyntheticBlenderScene(frames);
        int obj = s.AddObject("Arm", "MESH", Matrix4x4.Identity, mesh: s.AddCube());
        // an armature modifier turning every vertex about Z and lifting it: the mesh deforms, but rigidly
        s.Deform(obj, f => CubeCorners.Select(v => Vector3.Transform(v, Matrix4x4.CreateRotationZ(f * 0.05f) * Matrix4x4.CreateTranslation(0, 0, f * 0.1f))).ToArray());

        var (animation, report, _) = BlenderSceneImporter.Import(s.Build(), AsMeshes);

        var meshes = animation.Scenes.Select(k => (PolyMeshElement)k.FindChild("Arm")!).ToList();
        Assert.All(meshes, m => Assert.Same(meshes[0].Data, m.Data));   // one mesh, no morph
        var last = meshes[^1];
        AssertSameVertices(CubeCorners.Select(v => ToYUp(Vector3.Transform(v, Matrix4x4.CreateRotationZ((frames - 1) * 0.05f) * Matrix4x4.CreateTranslation(0, 0, (frames - 1) * 0.1f)))), WorldVertices(last));
        Assert.Contains(report.Info, l => l.Contains("moved by a rig"));
    }

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

        var (animation, report, _) = BlenderSceneImporter.Import(s.Build(), AsMeshes);
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
        var mesh = (PolyMeshElement)BlenderSceneImporter.Import(s.Build(), AsMeshes).Animation.Scenes[0].Children.Single();
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
        // a Blender camera at -10 on Y, turned 90Â° about X, looks along +Y at the origin
        s.AddCamera("Cam", Deg(90, 0, 0) * Matrix4x4.CreateTranslation(0, -10, 0), 40, 0.5f, 250);
        s.AddObject("Box", "MESH", Matrix4x4.Identity, mesh: s.AddCube());
        s.Cameras.Add("Cam");
        var state = BlenderSceneImporter.Import(s.Build(), AsMeshes).Animation.Scenes[0];

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
        var names = BlenderSceneImporter.Import(s.Build(), AsMeshes).Animation.Scenes[0].Children.Select(c => c.Name).ToList();
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
        var group = Assert.IsType<GroupElement>(BlenderSceneImporter.Import(s.Build(), AsMeshes).Animation.Scenes[0].Children.Single());
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
        var state = BlenderSceneImporter.Import(s.Build(), AsMeshes).Animation.Scenes[0];
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
        var animation = BlenderSceneImporter.Import(s.Build(), AsMeshes).Animation;
        Assert.Equal(24, animation.Fps);
        Assert.Equal(2 / 24f, animation.EndTime, 5);
        Assert.Equal(1, animation.Evaluate(1 / 24f)!.FindChild("Mover")!.X, 3);
        Assert.Null(animation.Evaluate(1 / 24f)!.FindChild("Blink"));
        Assert.NotNull(animation.Evaluate(2 / 24f)!.FindChild("Blink"));

        var still = BlenderSceneImporter.Import(s.Build(), new BlenderImportOptions { Static = true, RecognizeShapes = false }).Animation;
        Assert.Single(still.Scenes);
    }

    /// <summary>The largest distance between the vertices of Blender's frames and Overbryce's in-between frames.</summary>
    private static double WorstMeshError(Animation animation, string name, Func<int, List<Vector3>> truth, int frames)
    {
        double worst = 0;
        for (int f = 0; f < frames; f++)
        {
            var e = (PolyMeshElement)animation.Evaluate(f / 24f)!.FindChild(name)!;
            var expected = truth(f);
            var actual = WorldVertices(e);
            foreach (var p in expected) worst = Math.Max(worst, actual.Min(q => Vector3.Distance(p, q)));
        }
        return worst;
    }

    [Fact]
    public void Smooth_motion_needs_few_key_states_and_stays_within_the_tolerance()
    {
        const int frames = 97;
        var s = new SyntheticBlenderScene(frames);
        var path = Enumerable.Range(0, frames).Select(f =>
        {
            float t = f / (float)(frames - 1);
            return Deg(0, 0, 360 * t) * Matrix4x4.CreateTranslation(4 * MathF.Sin(3 * t), 2 * t, t * t);
        }).ToArray();
        s.AddObject("Spinner", "MESH", path, mesh: s.AddCube());
        var options = new BlenderImportOptions { Tolerance = 0.001, RecognizeShapes = false };
        var animation = BlenderSceneImporter.Import(s.Build(), options).Animation;
        Assert.InRange(animation.Scenes.Count, 3, frames / 3);
        double worst = WorstMeshError(animation, "Spinner", f => CubeCorners.Select(v => ToYUp(Vector3.Transform(v, path[f]))).ToList(), frames);
        Assert.True(worst < 0.01, $"strays {worst}");   // the scene is a few units big: 0.001 of it is below 0.01
    }

    [Fact]
    public void A_jump_becomes_a_stop_and_does_not_swing_the_rest()
    {
        const int frames = 30;
        var s = new SyntheticBlenderScene(frames);
        var path = Enumerable.Range(0, frames).Select(f => Matrix4x4.CreateTranslation(f < 15 ? 0.1f * f : 10 + 0.1f * f, 0, 0)).ToArray();
        s.AddObject("Teleport", "MESH", path, mesh: s.AddCube());
        var animation = BlenderSceneImporter.Import(s.Build(), AsMeshes).Animation;
        var times = animation.Scenes.Select(k => (int)MathF.Round(k.AniTime * 24)).ToList();
        Assert.Contains(14, times);
        Assert.Contains(15, times);
        Assert.True(animation.Scenes[times.IndexOf(15)].FindChild("Teleport")!.MotionBoundary);
        double worst = WorstMeshError(animation, "Teleport", f => CubeCorners.Select(v => ToYUp(Vector3.Transform(v, path[f]))).ToList(), frames);
        Assert.True(worst < 0.02, $"strays {worst}");
    }

    [Fact]
    public void A_camera_cut_switches_at_its_frame()
    {
        const int frames = 20;
        var s = new SyntheticBlenderScene(frames);
        s.AddCamera("A", Matrix4x4.CreateTranslation(0, -10, 0), 40);
        s.AddCamera("B", Matrix4x4.CreateTranslation(5, -10, 0), 40);
        s.AddObject("Box", "MESH", Matrix4x4.Identity, mesh: s.AddCube());
        for (int f = 0; f < frames; f++) s.Cameras.Add(f < 12 ? "A" : "B");
        var animation = BlenderSceneImporter.Import(s.Build(), AsMeshes).Animation;
        for (int f = 0; f < frames; f++)
            Assert.Equal(f < 12 ? "A" : "B", animation.Evaluate(f / 24f)!.ActiveCamera!.Name);
    }

    [Fact]
    public void Deforming_meshes_morph_between_key_states()
    {
        const int frames = 25;
        var s = new SyntheticBlenderScene(frames);
        Vector3[] flat = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(1, 1, 0)];
        int mesh = s.AddMesh(flat, [0, 1, 2, 1, 3, 2]);
        int obj = s.AddObject("Wave", "MESH", Matrix4x4.Identity, mesh: mesh);
        Vector3[] At(int f) => flat.Select((v, i) => i == 3 ? v + new Vector3(0, 0, MathF.Sin(f / 10f)) : v).ToArray();
        s.Deform(obj, At);
        var animation = BlenderSceneImporter.Import(s.Build(), AsMeshes).Animation;
        Assert.True(animation.Scenes.Count < frames);
        double worst = WorstMeshError(animation, "Wave", f => At(f).Select(ToYUp).ToList(), frames);
        Assert.True(worst < 0.01, $"strays {worst}");
    }

    [Fact]
    public void A_large_deforming_mesh_is_fitted_by_a_sample_and_stays_within_the_tolerance()
    {
        // 961 vertices: the fitting looks at every fourth of them, the key states morph all
        const int frames = 25, side = 31;
        var s = new SyntheticBlenderScene(frames);
        var flat = new Vector3[side * side];
        for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++) flat[y * side + x] = new Vector3(x / 10f, y / 10f, 0);
        var triangles = new List<int>();
        for (int y = 0; y + 1 < side; y++)
            for (int x = 0; x + 1 < side; x++)
            {
                int i = y * side + x;
                triangles.AddRange([i, i + 1, i + side, i + 1, i + side + 1, i + side]);
            }
        int obj = s.AddObject("Cloth", "MESH", Matrix4x4.Identity, mesh: s.AddMesh(flat, [.. triangles]));
        Vector3[] At(int f) => flat.Select(v => v + new Vector3(0, 0, 0.5f * MathF.Sin(v.X * 2 + f / 6f))).ToArray();
        s.Deform(obj, At);
        var animation = BlenderSceneImporter.Import(s.Build(), AsMeshes).Animation;
        Assert.True(animation.Scenes.Count < frames);
        double worst = WorstMeshError(animation, "Cloth", f => At(f).Select(ToYUp).ToList(), frames);
        Assert.True(worst < 0.02, $"strays {worst}");
    }

    [Fact]
    public void Still_objects_do_not_change_or_slow_the_choice_of_key_states()
    {
        // a scene full of still objects (stones, trees) around one moving object, as in a set dressed for a character
        const int frames = 97;
        var path = Enumerable.Range(0, frames).Select(f =>
        {
            float t = f / (float)(frames - 1);
            return Deg(0, 0, 360 * t) * Matrix4x4.CreateTranslation(4 * MathF.Sin(3 * t), 2 * t, t * t);
        }).ToArray();
        SyntheticBlenderScene Scene(int stillObjects)
        {
            var s = new SyntheticBlenderScene(frames);
            int cube = s.AddCube();
            s.AddObject("Spinner", "MESH", path, mesh: cube);
            // where the spinner is in the frames that measure the scene, so its size (and the tolerance) stays the same
            for (int i = 0; i < stillObjects; i++)
                s.AddObject($"Stone{i}", "MESH", path[i % 9 * (frames - 1) / 8], mesh: cube);
            return s;
        }
        static List<float> Times(Animation a) => a.Scenes.Select(k => k.AniTime).ToList();
        var alone = BlenderSceneImporter.Import(Scene(0).Build(), AsMeshes).Animation;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var crowded = BlenderSceneImporter.Import(Scene(2500).Build(), AsMeshes).Animation;
        Assert.Equal(Times(alone), Times(crowded));
        Assert.All(crowded.Scenes, k => Assert.Equal(2501, k.Children.Count(c => c.Name.StartsWith("Stone", StringComparison.Ordinal) || c.Name == "Spinner")));
        Assert.True(clock.Elapsed.TotalSeconds < 60, $"took {clock.Elapsed.TotalSeconds:0} s");
    }

    [Fact]
    public void Wrong_files_are_refused()
    {
        Assert.Throws<InvalidDataException>(() => BlenderSceneFile.Parse("{\"format\":\"something else\"}", []));
        Assert.Throws<InvalidDataException>(() => BlenderSceneFile.Parse($"{{\"format\":\"{BlenderSceneFile.FormatName}\",\"version\":99}}", []));
    }
}
