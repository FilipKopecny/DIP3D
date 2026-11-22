using System.Numerics;
using Overbryce.Core;
using Overbryce.Core.Model;

namespace Overbryce.Raytrace.Tests;

/// <summary>Whole frames: the OpenGL lighting, shadows, glass, cut-outs, reflections, blur, determinism.</summary>
public class RenderTests
{
    private const int Size = 65;
    private static readonly CameraSetup TopCamera = new(Matrix4x4.CreateLookAt(new Vector3(0, 0, 10), Vector3.Zero, Vector3.UnitY), 35, 0.1f, 100);

    // ------------------------------------------------------------------------------------------ helpers

    private static readonly Rgba Black = new(0, 0, 0, 1);

    private static RectangleElement Ground(float size = 20) => new("Ground")
    {
        SizeX = size,
        SizeY = size,
        Ambient = new Rgba(0.5f, 0.5f, 0.5f, 1),
        Diffuse = new Rgba(0.5f, 0.5f, 0.5f, 1),
        Specular = Black,
        Emission = Black,
    };

    private static LightElement Lamp(float x, float y, float z) => new("Lamp")
    {
        X = x, Y = y, Z = z,
        Ambient = Black,
        Diffuse = new Rgba(1, 1, 1, 1),
        Specular = Black,
    };

    private static Scene With(params Element[] elements)
    {
        var s = new Scene();
        foreach (var e in elements) s.Add(e);
        return s;
    }

    private static RaytraceImage Render(Scene scene, RaytraceSettings settings, bool classic = false, CameraSetup? camera = null,
        Func<string, TextureImage?>? textures = null, int threads = 0, IReadOnlyList<FrameStep>? steps = null)
    {
        var options = new RaytraceOptions
        {
            Settings = settings,
            Modern = new ModernSettings { Bloom = false },
            Classic = classic,
            Textures = textures ?? (_ => null),
            Threads = threads,
        };
        return new Raytracer().Render(steps ?? [new FrameStep(scene, camera ?? TopCamera)], Size, Size, options);
    }

    private static RaytraceSettings Settings(int samples = 1, bool shadows = true) => new() { Samples = samples, Shadows = shadows };

    /// <summary>The pixel a world point is seen in.</summary>
    private static (int X, int Y) Project(Vector3 p, CameraSetup cam)
    {
        var e = Vector3.Transform(p, cam.View);
        float tan = MathF.Tan(cam.FieldOfView * MathF.PI / 360);
        float nx = e.X / (-e.Z * tan), ny = e.Y / (-e.Z * tan);
        return ((int)((nx + 1) / 2 * Size), (int)((1 - ny) / 2 * Size));
    }

    private static (int R, int G, int B) At(RaytraceImage img, int x, int y)
    {
        int o = (y * img.Width + x) * 4;
        return (img.Bgra[o + 2], img.Bgra[o + 1], img.Bgra[o]);
    }

    private static int Gray(RaytraceImage img, Vector3 p, CameraSetup? cam = null)
    {
        var (x, y) = Project(p, cam ?? TopCamera);
        return At(img, x, y).G;
    }

    // ------------------------------------------------------------------------------------------ lighting

    [Fact]
    public void Classic_look_is_the_OpenGL_lighting()
    {
        // global ambient 0.2 × 0.5 + attenuation × N·L × diffuse 0.5, at the centre straight under the light
        var lamp = Lamp(0, 0, 5);
        lamp.LinearAttenuation = 0.1;
        var img = Render(With(Ground(), lamp), Settings(), classic: true);
        float expected = 0.1f + 1 / 1.5f * 0.5f;
        Assert.InRange(At(img, Size / 2, Size / 2).G, expected * 255 - 1, expected * 255 + 1);

        // specular: exponent shininess × 128 with the half vector between the light and the eye
        var shiny = Ground();
        shiny.Specular = new Rgba(1, 1, 1, 1);
        shiny.Shininess = 0.5f;
        var spot = Lamp(0, 0, 5);
        spot.Specular = new Rgba(0.2f, 0.2f, 0.2f, 1);
        img = Render(With(shiny, spot), Settings(), classic: true);
        expected = 0.1f + 0.5f + 0.2f;
        Assert.InRange(At(img, Size / 2, Size / 2).G, expected * 255 - 1, expected * 255 + 1);
    }

    [Fact]
    public void Classic_look_clamps_and_ignores_glow_and_mirrors()
    {
        var ground = Ground();
        ground.Emission = new Rgba(0.9f, 0.9f, 0.9f, 1);
        ground.Glow = 3;
        ground.Reflectivity = 1;
        var img = Render(With(ground, Lamp(0, 0, 5)), Settings(), classic: true);
        Assert.Equal(255, At(img, Size / 2, Size / 2).G);
        Assert.Equal(255, At(img, 2, 2).G);
    }

    [Fact]
    public void A_spot_light_lights_only_its_cone()
    {
        var spot = Lamp(0, 0, 5);
        spot.Gamma = -1; // pointing down
        spot.SpotCutoff = 20;
        var img = Render(With(Ground(), spot), Settings(), classic: true);
        Assert.True(Gray(img, Vector3.Zero) > 100);
        Assert.InRange(Gray(img, new Vector3(3, 0, 0)), 25, 26); // only the global ambient light
    }

    // ------------------------------------------------------------------------------------------ shadows

    private static readonly Vector3 InShadow = new(-1.6f, 0, 0);
    private static readonly Vector3 InLight = new(1.6f, 0, 0);

    private static Scene ShadowScene(Element blocker, float radius = 0)
    {
        var lamp = Lamp(4, 0, 4);
        lamp.LightRadius = radius;
        return With(Ground(), blocker, lamp);
    }

    private static BoxElement Block() => new("Block") { Z = 1, SizeX = 1, SizeY = 1, SizeZ = 1 };

    [Fact]
    public void Elements_cast_shadows()
    {
        var on = Render(ShadowScene(Block()), Settings());
        var off = Render(ShadowScene(Block()), Settings(shadows: false));
        Assert.InRange(Gray(on, InShadow), 24, 27);          // the ambient part only
        Assert.True(Gray(off, InShadow) > 60);
        Assert.Equal(Gray(off, InLight), Gray(on, InLight));

        var block = Block();
        block.CastsShadows = false;
        Assert.Equal(Gray(off, InShadow), Gray(Render(ShadowScene(block), Settings()), InShadow));
    }

    [Fact]
    public void A_light_with_a_radius_casts_a_soft_edge()
    {
        int Intermediate(RaytraceImage shadowed, RaytraceImage lit)
        {
            int count = 0;
            int y = Size / 2;
            for (int x = 0; x < Size / 2; x++)
            {
                float s = At(shadowed, x, y).G, l = At(lit, x, y).G;
                if (l - 25 < 20) continue;
                float r = (s - 25) / (l - 25);
                if (r is > 0.1f and < 0.9f) count++;
            }
            return count;
        }

        var lit = Render(ShadowScene(Block()), Settings(shadows: false));
        Assert.Equal(0, Intermediate(Render(ShadowScene(Block()), Settings()), lit));
        Assert.True(Intermediate(Render(ShadowScene(Block(), radius: 1.5f), Settings(samples: 64)), lit) >= 3);
    }

    [Fact]
    public void Glass_lets_tinted_light_through_its_shadow()
    {
        var glass = Block();
        glass.Diffuse = new Rgba(0.8f, 0.8f, 0.8f, 0.2f);
        glass.RefractiveIndex = 1.5f;
        int full = Gray(Render(ShadowScene(Block()), Settings()), InShadow);
        int none = Gray(Render(ShadowScene(Block()), Settings(shadows: false)), InShadow);
        int through = Gray(Render(ShadowScene(glass), Settings()), InShadow);
        Assert.InRange(through, full + 10, none - 5);
    }

    [Fact]
    public void A_light_inside_an_element_shines_out_of_it()
    {
        var bulb = new SphereElement("Bulb") { X = 4, Z = 4, Radius = 0.5 };
        int open = Gray(Render(ShadowScene(bulb), Settings(shadows: false)), InLight);
        Assert.Equal(open, Gray(Render(ShadowScene(bulb), Settings()), InLight));
    }

    [Fact]
    public void A_small_element_near_a_glowing_light_body_hides_only_part_of_it()
    {
        // a pebble next to a sun-like light: as a point light, its shadow on the ground is a hard
        // black disk; the glowing body around the light is much bigger, so the shadow nearly vanishes
        var pebble = new SphereElement("Pebble") { X = 3.1f, Z = 3.1f, Radius = 0.15 }; // just outside the sun
        var sun = new SphereElement("Sun")
        {
            X = 4, Z = 4, Radius = 1,
            Ambient = Black, Diffuse = Black, Specular = Black, Emission = new Rgba(1, 1, 1, 1),
        };
        int open = Gray(Render(ShadowScene(pebble), Settings(shadows: false)), Vector3.Zero);
        int point = Gray(Render(ShadowScene(pebble), Settings()), Vector3.Zero);
        int body = Gray(Render(With(Ground(), pebble, sun, Lamp(4, 0, 4)), Settings()), Vector3.Zero);
        Assert.InRange(point, 24, 27); // the ambient part only
        Assert.InRange(body, open - 4, open);

        // the same body does not lighten the shadow of an element close to the shaded point
        int nearPoint = Gray(Render(ShadowScene(Block()), Settings()), InShadow);
        int nearBody = Gray(Render(With(Ground(), Block(), sun, Lamp(4, 0, 4)), Settings()), InShadow);
        Assert.Equal(nearPoint, nearBody);
    }

    [Fact]
    public void A_glowing_sky_around_the_scene_is_not_a_light_body()
    {
        // the light is well inside a glowing sky sphere, but so is everything else: the sky is not the
        // light's body, and the pebble keeps its full shadow
        var pebble = new SphereElement("Pebble") { X = 3.1f, Z = 3.1f, Radius = 0.15 };
        var sky = new SphereElement("Sky")
        {
            Radius = 50,
            Ambient = Black, Diffuse = Black, Specular = Black, Emission = new Rgba(1, 1, 1, 1),
        };
        int point = Gray(Render(ShadowScene(pebble), Settings()), Vector3.Zero);
        int withSky = Gray(Render(With(Ground(), pebble, sky, Lamp(4, 0, 4)), Settings()), Vector3.Zero);
        Assert.Equal(point, withSky);
    }

    // ------------------------------------------------------------------------------------------ surfaces

    [Fact]
    public void White_texels_are_cut_out_with_alpha_blending()
    {
        // left half of the texture white (transparent), right half red
        byte[] rgba =
        [
            255, 255, 255, 0, 255, 0, 0, 255,
            255, 255, 255, 0, 255, 0, 0, 255,
        ];
        var texture = new TextureImage(rgba, 2);
        var plate = new RectangleElement("Plate") { Z = 2, SizeX = 4, SizeY = 4, Texture = "t", Specular = Black, Emission = Black };
        var scene = With(Ground(), plate, Lamp(0, 0, 5));
        var img = Render(scene, Settings(shadows: false), textures: _ => texture);
        var groundOnly = Render(With(Ground(), Lamp(0, 0, 5)), Settings(shadows: false));

        var left = new Vector3(-1, 0, 2);
        Assert.Equal(Gray(groundOnly, left), Gray(img, left));
        var (r, g, _) = At(img, Project(new Vector3(1, 0, 2), TopCamera).X, Project(new Vector3(1, 0, 2), TopCamera).Y);
        Assert.True(r > g + 50);
    }

    [Fact]
    public void Half_transparent_elements_blend_with_what_is_behind()
    {
        var veil = new RectangleElement("Veil")
        {
            Z = 2, SizeX = 4, SizeY = 4,
            Ambient = Black, Diffuse = new Rgba(0, 0, 0, 0.5f), Specular = Black, Emission = Black,
        };
        int behind = Gray(Render(With(Ground(), Lamp(0, 0, 5)), Settings()), Vector3.Zero);
        int through = Gray(Render(With(Ground(), veil, Lamp(0, 0, 5)), Settings(shadows: false)), Vector3.Zero);
        Assert.InRange(through, behind / 2 - 2, behind / 2 + 2);
    }

    [Fact]
    public void See_through_layers_follow_the_drawing_order_as_in_OpenGL()
    {
        // the OpenGL renderers draw in scene order with depth writes: what they draw after a
        // see-through surface and behind it fails the depth test
        RectangleElement Veil(string name, float z, float glow) => new(name)
        {
            Z = z, SizeX = 4, SizeY = 4,
            Ambient = Black, Diffuse = new Rgba(0, 0, 0, 0.5f), Specular = Black, Emission = new Rgba(glow, glow, glow, 1),
        };
        var light = Lamp(0, 0, 5);
        var settings = Settings(shadows: false);

        int groundSeen = Gray(Render(With(Ground(), Veil("Front", 2, 0), light), settings, classic: true), Vector3.Zero);
        int groundHidden = Gray(Render(With(Veil("Front", 2, 0), Ground(), light), settings, classic: true), Vector3.Zero);
        Assert.True(groundSeen > 20);
        Assert.Equal(0, groundHidden);

        // a glowing veil behind a dark one: half of half its light when drawn first, nothing when drawn after
        int backFirst = Gray(Render(With(Veil("Back", 1, 1), Veil("Front", 2, 0)), settings, classic: true), Vector3.Zero);
        int backAfter = Gray(Render(With(Veil("Front", 2, 0), Veil("Back", 1, 1)), settings, classic: true), Vector3.Zero);
        Assert.InRange(backFirst, 62, 66);
        Assert.Equal(0, backAfter);

        // a closed see-through shape shows the triangles OpenGL draws first: never both of its layers
        // on top of each other more than once
        var shell = new SphereElement("Shell")
        {
            Radius = 2,
            Ambient = Black, Diffuse = new Rgba(0, 0, 0, 0.5f), Specular = Black, Emission = new Rgba(1, 1, 1, 1),
        };
        int once = Gray(Render(With(Veil("Flat", 2, 1)), settings, classic: true), Vector3.Zero);
        int sphere = Gray(Render(With(shell), settings, classic: true), Vector3.Zero);
        Assert.InRange(sphere, once - 2, (int)(once * 1.5f) + 2); // one layer, or the back half under the front
    }

    [Fact]
    public void Mirrors_reflect_and_the_classic_look_does_not()
    {
        var camera = new CameraSetup(Matrix4x4.CreateLookAt(new Vector3(0, -10, 2), Vector3.Zero, Vector3.UnitZ), 35, 0.1f, 100);
        var mirror = new RectangleElement("Mirror")
        {
            SizeX = 20, SizeY = 20, Reflectivity = 1,
            Ambient = Black, Diffuse = Black, Specular = Black, Emission = Black,
        };
        var ball = new SphereElement("Ball")
        {
            Z = 3, Radius = 1,
            Ambient = Black, Diffuse = Black, Specular = Black, Emission = new Rgba(1, 0, 0, 1),
        };
        var image = new Vector3(0, 0, -3); // where the ball's reflection appears
        var full = Render(With(mirror, ball), Settings(), camera: camera);
        var classic = Render(With(mirror, ball), Settings(), classic: true, camera: camera);
        var (x, y) = Project(image, camera);
        Assert.True(At(full, x, y).R > 200);
        Assert.True(At(classic, x, y).R < 10);
    }

    // ------------------------------------------------------------------------------------------ camera and time

    [Fact]
    public void Motion_blur_smears_a_moving_element()
    {
        SphereElement Ball(float x) => new("Ball") { X = x, Radius = 1.5, Emission = new Rgba(1, 1, 1, 1) };
        var still = With(Ball(-1));
        var steps = new List<FrameStep> { new(With(Ball(-1)), TopCamera), new(With(Ball(1)), TopCamera) };
        var edge = new Vector3(-2.2f, 0, 0);
        int sharp = Gray(Render(still, Settings(16)), edge);
        int blurred = Gray(Render(still, Settings(16), steps: steps), edge);
        Assert.True(sharp > 200);
        Assert.InRange(blurred, 40, sharp - 40);
    }

    [Fact]
    public void An_open_aperture_blurs_what_is_not_in_focus()
    {
        var ball = new SphereElement("Ball") { Radius = 1.5, Emission = new Rgba(1, 1, 1, 1) };
        var near = new Vector3(-1.75f, 0, 0); // just outside the silhouette
        var pinhole = Render(With(ball), Settings(32));
        var lens = Render(With(ball), Settings(32), camera: TopCamera with { Aperture = 0.4f, Focus = 3 });
        var sharp = Render(With(ball), Settings(32), camera: TopCamera with { Aperture = 0.4f, Focus = 8.5f });
        Assert.True(Gray(pinhole, near) < 10);
        Assert.True(Gray(lens, near) > 30);
        Assert.True(Gray(sharp, near) < Gray(lens, near));
    }

    [Fact]
    public void The_same_frame_gives_the_same_picture()
    {
        var scene = ShadowScene(Block(), radius: 1);
        var a = Render(scene, Settings(4));
        var b = Render(scene, Settings(4));
        var single = Render(scene, Settings(4), threads: 1);
        Assert.Equal(a.Bgra, b.Bgra);
        Assert.Equal(a.Bgra, single.Bgra);
    }

    [Fact]
    public void Smooth_geometry_matches_the_facets_closely()
    {
        var ball = new SphereElement("Ball") { Radius = 2, QualityX = 64, QualityY = 32 };
        var faceted = Render(With(ball, Lamp(0, 0, 8)), Settings(), classic: true);
        var smooth = Render(With(ball, Lamp(0, 0, 8)), new RaytraceSettings { Samples = 1, Geometry = RaytraceGeometry.Smooth }, classic: true);
        int differ = 0;
        for (int i = 0; i < faceted.Bgra.Length; i += 4)
            if (Math.Abs(faceted.Bgra[i + 1] - smooth.Bgra[i + 1]) > 3) differ++;
        Assert.True(differ < Size * 2, $"{differ} pixels differ");
    }
}
