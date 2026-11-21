using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Overbryce.Core.Model;

namespace Overbryce.App.Rendering;

/// <summary>
/// The modern renderer (new in 2.1). One frame is drawn in these passes:
/// <list type="number">
/// <item>shadow cube maps of the most important lights (the distance from the light per texel);</item>
/// <item>reflection probes: cube maps of the surroundings of the most visible reflecting elements;</item>
/// <item>normals and depth of the opaque elements, turned into screen-space ambient occlusion;</item>
/// <item>the scene in HDR (multisampled), lit like the classic renderer plus glow, shadows, occlusion
/// and reflections;</item>
/// <item>bloom and tone mapping into the target (the window or an export frame).</item>
/// </list>
/// The classic renderer is not affected; the 1998 look stays the default.
/// </summary>
internal sealed partial class SceneRenderer
{
    private const int AoUnit = 1;
    private const int EnvUnit = 2;
    private const int ShadowUnit = 3;
    private const float ShadowNear = 0.02f;
    private const float ProbeNear = 0.05f;
    private const float MaxRange = 100f;

    /// <summary>Share of the ambient occlusion applied to direct light (fully to ambient light).</summary>
    private const float AoDirect = 0.4f;

    private ModernPipeline? _modern;
    private ShaderProgram? _shadowShader;
    private readonly HashSet<DrawCommand> _occluding = new(ReferenceEqualityComparer.Instance);

    private void RenderModern(Scene scene, RenderOptions options, ModernSettings s, int width, int height)
    {
        GL.GetInteger(GetPName.DrawFramebufferBinding, out int target);
        _modern ??= new ModernPipeline();
        _modern.EnsureFrame(width, height, options.Samples);
        GL.Enable(EnableCap.TextureCubeMapSeamless);

        Prepare(scene, options);
        foreach (var cmd in _commands) ComputeBounds(cmd);
        var camera = CameraPosition(options.View);
        var frustum = new Frustum(options.View * options.Projection);

        UnbindModernTextures();
        _shader.Set("uPass", 0);
        _shader.Set("uModern", true);
        _shader.Set("uEnvOn", false);
        _shader.Set("uUseAo", false);
        _shader.Set("uGlobalAmbient", new Vector4(0.2f, 0.2f, 0.2f, 1f));
        _shader.Set("uShadowSoftness", s.ShadowSoftness);
        _shader.Set("uAoDirect", AoDirect);
        _shader.Set("uViewport", new Vector2(width, height));
        SetupLights(scene, options);
        SetupFog(scene, options);

        // 1. shadows
        var slots = RenderShadows(s, camera);
        for (int i = 0; i < ShaderProgram.MaxLights; i++)
            _shader.Set($"uShadowSlot[{i}]", i < slots.Length ? slots[i] : -1);
        for (int k = 0; k < ShaderProgram.MaxShadowLights; k++)
            if (_modern.ShadowIfAny(k) is { } cube)
                BindUnit(ShadowUnit + k, TextureTarget.TextureCubeMap, cube.Texture);

        // 2. reflection probes
        bool env = s.Reflections && RenderProbes(s, camera, frustum, options) > 0;

        // 3. ambient occlusion
        bool ao = s.AmbientOcclusion && s.AoStrength > 0;
        _occluding.Clear();
        if (ao)
        {
            RenderNormals(options, frustum);
            _modern.ComputeOcclusion(options.Projection, s.AoRadius, s.AoStrength);
            _shader.Use();
            BindUnit(AoUnit, TextureTarget.Texture2D, _modern.AoTexture);
        }

        // 4. the scene
        _shader.Use();
        GL.BindVertexArray(_vao);
        _shader.Set("uPass", 0);
        _shader.Set("uView", options.View);
        _shader.Set("uProj", options.Projection);
        _shader.Set("uCameraPos", camera);
        _modern.BindFrame();
        GL.ClearColor(0, 0, 0, 1);
        GL.ClearDepth(1);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);
        SetBlend(options);
        GL.Enable(EnableCap.PolygonOffsetFill);
        GL.PolygonOffset(1f, 1f);
        foreach (var cmd in _commands)
        {
            if (cmd.Overlay || !frustum.Visible(cmd.Center, cmd.Radius)) continue;
            SetModernMaterial(cmd, ao, env);
            Draw(cmd, cmd.Color);
        }
        GL.Disable(EnableCap.PolygonOffsetFill);
        _shader.Set("uFogMode", 0);
        _shader.Set("uEnvOn", false);
        foreach (var cmd in _commands)
            if (cmd.Overlay) Draw(cmd, cmd.Color);

        // 5. bloom and tone mapping
        _modern.Finish(target, width, height, s.Bloom && s.BloomStrength > 0, s.BloomStrength, s.BloomThreshold, s.Exposure);

        _shader.Use();
        _shader.Set("uModern", false);
        _shader.Set("uUseAo", false);
        UnbindModernTextures();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, target);
        GL.Viewport(0, 0, width, height);
        GL.Enable(EnableCap.DepthTest);
        GL.BindVertexArray(0);
    }

    private void SetModernMaterial(DrawCommand cmd, bool ao, bool env)
    {
        var m = cmd.Material;
        _shader.Set("uGlow", m?.Glow ?? 1f);
        _shader.Set("uReflect", m?.Reflectivity ?? 0f);
        _shader.Set("uIor", m?.RefractiveIndex ?? 1f);
        bool useEnv = env && cmd.Probe >= 0 && _modern != null;
        _shader.Set("uEnvOn", useEnv);
        if (useEnv)
            BindUnit(EnvUnit, TextureTarget.TextureCubeMap, _modern!.Probe(cmd.Probe, _probeSize).Texture);
        _shader.Set("uUseAo", ao && _occluding.Contains(cmd));
    }

    // ==================================================================================== shadows

    /// <summary>Renders the shadow maps; returns for each uploaded light its shadow slot or -1.</summary>
    private int[] RenderShadows(ModernSettings s, Vector3 camera)
    {
        var slots = Enumerable.Repeat(-1, _lights.Count).ToArray();
        if (!s.Shadows || s.ShadowLights <= 0 || _modern == null) return slots;

        var ranked = _lights.Select((l, i) => (Light: l, Index: i))
            .Where(x => x.Light.Light.CastsShadows)
            .OrderByDescending(x => Importance(x.Light, camera))
            .Take(Math.Min(s.ShadowLights, ShaderProgram.MaxShadowLights))
            .ToList();
        if (ranked.Count == 0) return slots;

        int size = s.ShadowMapSize;
        var depth = _shadowShader ??= new ShaderProgram(SceneShader.Vertex, SceneShader.ShadowFragment);
        depth.Use();
        depth.Set("uPass", 3);
        depth.Set("uAlphaTest", _shaderAlphaTest);
        AssignSamplerUnits(depth);
        // the shadow maps being drawn must not be bound for sampling at the same time
        for (int k = 0; k < ShaderProgram.MaxShadowLights; k++) BindUnit(ShadowUnit + k, TextureTarget.TextureCubeMap, 0);
        GL.Disable(EnableCap.Blend);
        GL.Disable(EnableCap.PolygonOffsetFill);
        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);
        GL.DepthMask(true);
        GL.BindVertexArray(_vao);

        var farOf = new float[ranked.Count];
        for (int slot = 0; slot < ranked.Count; slot++)
        {
            var (light, index) = ranked[slot];
            float far = Range(light.Light);
            var cube = _modern.Shadow(slot, size);
            var proj = Transforms.Perspective(90f, 1f, ShadowNear, far);
            depth.Set("uProj", proj);
            depth.Set("uShadowLightPos", light.Position);
            depth.Set("uShadowLightFar", far);

            // a spot light only needs the faces its cone reaches
            bool spot = light.Light.SpotCutoff <= 90 && light.Direction.LengthSquared() > 1e-12f;
            float reach = MathF.Cos(MathF.Min(MathF.PI, ((float)light.Light.SpotCutoff + 55f) * MathF.PI / 180f));
            var spotDir = spot ? Vector3.Normalize(light.Direction) : Vector3.Zero;

            for (int f = 0; f < 6; f++)
            {
                cube.BindFace(f);
                GL.Clear(ClearBufferMask.DepthBufferBit);
                if (spot && Vector3.Dot(CubeTarget.Faces[f].Dir, spotDir) < reach) continue;
                var view = CubeTarget.FaceView(light.Position, f);
                depth.Set("uView", view);
                var fr = new Frustum(view * proj);
                foreach (var cmd in _commands)
                {
                    if (!CastsShadow(cmd, light.Position, far) || !fr.Visible(cmd.Center, cmd.Radius)) continue;
                    DrawGeometry(depth, cmd);
                }
            }
            farOf[slot] = far;
            slots[index] = slot;
        }

        _shader.Use();
        for (int k = 0; k < ranked.Count; k++) _shader.Set($"uShadowFar[{k}]", farOf[k]);
        _shader.Set("uShadowSize", (float)size);
        _shader.Set("uPass", 0);
        return slots;
    }

    private static bool CastsShadow(DrawCommand cmd, Vector3 light, float far)
    {
        if (cmd.Overlay || !cmd.Lit || !cmd.Filled || cmd.Material is not { CastsShadows: true } m) return false;
        if (m.Diffuse.A < 0.5f || (m.RefractiveIndex > 1f && m.Diffuse.A < 0.999f)) return false; // glass and the nearly invisible
        float d = Vector3.Distance(cmd.Center, light);
        // an element enclosing the light (a lamp, a sun, a sky) does not shadow it
        return d >= cmd.Radius && d - cmd.Radius < far;
    }

    /// <summary>How much a light matters for the picture: its brightness where the camera is.</summary>
    private static float Importance(ActiveLight l, Vector3 camera)
    {
        var c = l.Light.Diffuse;
        float lum = 0.3f * c.R + 0.59f * c.G + 0.11f * c.B;
        double d = Vector3.Distance(l.Position, camera);
        double att = 1 / Math.Max(l.Light.ConstantAttenuation + l.Light.LinearAttenuation * d + l.Light.QuadraticAttenuation * d * d, 1e-6);
        return (float)(lum * att);
    }

    /// <summary>The distance beyond which the light is too weak to matter (1/256 of white).</summary>
    private static float Range(LightElement l)
    {
        var c = l.Diffuse;
        double lum = Math.Max(Math.Max(c.R, c.G), Math.Max(c.B, 1e-3)) * 256;
        double k = l.ConstantAttenuation, lin = l.LinearAttenuation, q = l.QuadraticAttenuation;
        if (lin <= 0 && q <= 0) return MaxRange;
        double d = q > 0 ? (-lin + Math.Sqrt(lin * lin + 4 * q * Math.Max(lum - k, 0))) / (2 * q) : Math.Max(lum - k, 0) / lin;
        return (float)Math.Clamp(d, 1, MaxRange);
    }

    /// <summary>Draws a command's geometry for a depth or normal pass (no material).</summary>
    private static void DrawGeometry(ShaderProgram program, DrawCommand cmd)
    {
        program.Set("uModel", cmd.Model);
        program.Set("uTextured", cmd.Texture != 0);
        program.Set("uSphereMap", cmd.SphereMap);
        if (cmd.Texture != 0)
        {
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, cmd.Texture);
        }
        foreach (var r in cmd.Ranges)
            GL.DrawArrays(r.Mode, r.First, r.Count);
    }

    // ==================================================================================== reflections

    private int _probeSize = 256;

    /// <summary>Renders the reflection probes; returns how many were rendered.</summary>
    private int RenderProbes(ModernSettings s, Vector3 camera, Frustum frustum, RenderOptions options)
    {
        foreach (var cmd in _commands) cmd.Probe = -1;
        var candidates = _commands.Where(c => !c.Overlay && c.Lit && c.Material is { } m
                                              && (m.Reflectivity > 0 || (m.RefractiveIndex > 1 && m.Diffuse.A < 0.999f))
                                              && frustum.Visible(c.Center, c.Radius)).ToList();
        if (candidates.Count == 0 || _modern == null) return 0;

        // the elements that look biggest get their own probe, the others share the nearest one
        var chosen = candidates.OrderByDescending(c => c.Radius / MathF.Max(Vector3.Distance(camera, c.Center), 0.01f))
            .Take(s.ReflectionProbes).ToList();
        foreach (var c in candidates)
        {
            int best = 0;
            for (int k = 1; k < chosen.Count; k++)
                if (Vector3.DistanceSquared(c.Center, chosen[k].Center) < Vector3.DistanceSquared(c.Center, chosen[best].Center))
                    best = k;
            c.Probe = best;
        }

        _probeSize = s.ProbeSize;
        var proj = Transforms.Perspective(90f, 1f, ProbeNear, MaxRange);
        _shader.Set("uProj", proj);
        _shader.Set("uPass", 0);
        BindUnit(EnvUnit, TextureTarget.TextureCubeMap, 0);
        GL.BindVertexArray(_vao);
        for (int k = 0; k < chosen.Count; k++)
        {
            var self = chosen[k];
            var cube = _modern.Probe(k, _probeSize);
            _shader.Set("uCameraPos", self.Center);
            for (int f = 0; f < 6; f++)
            {
                cube.BindFace(f);
                GL.ClearColor(0, 0, 0, 1);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                GL.Enable(EnableCap.DepthTest);
                GL.DepthFunc(DepthFunction.Lequal);
                SetBlend(options);
                var view = CubeTarget.FaceView(self.Center, f);
                _shader.Set("uView", view);
                var fr = new Frustum(view * proj);
                foreach (var cmd in _commands)
                {
                    if (ReferenceEquals(cmd, self) || cmd.Overlay || !fr.Visible(cmd.Center, cmd.Radius)) continue;
                    SetModernMaterial(cmd, ao: false, env: false);
                    Draw(cmd, cmd.Color);
                }
            }
        }
        return chosen.Count;
    }

    // ==================================================================================== occlusion

    private void RenderNormals(RenderOptions options, Frustum frustum)
    {
        _modern!.BindNormals();
        GL.ClearColor(0, 0, 0, 0);
        GL.ClearDepth(1);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);
        GL.Disable(EnableCap.Blend);
        _shader.Use();
        GL.BindVertexArray(_vao);
        _shader.Set("uPass", 2);
        _shader.Set("uView", options.View);
        _shader.Set("uProj", options.Projection);
        foreach (var cmd in _commands)
        {
            if (cmd.Overlay || !cmd.Lit || !cmd.Filled || cmd.Material is not { } m) continue;
            if (m.Diffuse.A < 0.999f || !frustum.Visible(cmd.Center, cmd.Radius)) continue;
            _occluding.Add(cmd);
            DrawGeometry(_shader, cmd);
        }
        _shader.Set("uPass", 0);
    }

    // ==================================================================================== helpers

    private static void BindUnit(int unit, TextureTarget target, int texture)
    {
        GL.ActiveTexture(TextureUnit.Texture0 + unit);
        GL.BindTexture(target, texture);
        GL.ActiveTexture(TextureUnit.Texture0);
    }

    private static void UnbindModernTextures()
    {
        BindUnit(AoUnit, TextureTarget.Texture2D, 0);
        BindUnit(EnvUnit, TextureTarget.TextureCubeMap, 0);
        for (int k = 0; k < ShaderProgram.MaxShadowLights; k++)
            BindUnit(ShadowUnit + k, TextureTarget.TextureCubeMap, 0);
    }

    /// <summary>The six clip planes of a view-projection matrix, for culling bounding spheres.</summary>
    private readonly struct Frustum
    {
        private readonly Vector4[] _planes;

        public Frustum(Matrix4x4 m)
        {
            // row vectors: clip = v * M, so the planes come from the columns
            var c1 = new Vector4(m.M11, m.M21, m.M31, m.M41);
            var c2 = new Vector4(m.M12, m.M22, m.M32, m.M42);
            var c3 = new Vector4(m.M13, m.M23, m.M33, m.M43);
            var c4 = new Vector4(m.M14, m.M24, m.M34, m.M44);
            _planes = [c4 + c1, c4 - c1, c4 + c2, c4 - c2, c4 + c3, c4 - c3];
            for (int i = 0; i < 6; i++)
            {
                float len = new Vector3(_planes[i].X, _planes[i].Y, _planes[i].Z).Length();
                if (len > 0) _planes[i] /= len;
            }
        }

        public bool Visible(Vector3 c, float r)
        {
            foreach (var p in _planes)
                if (p.X * c.X + p.Y * c.Y + p.Z * c.Z + p.W < -r)
                    return false;
            return true;
        }
    }
}
