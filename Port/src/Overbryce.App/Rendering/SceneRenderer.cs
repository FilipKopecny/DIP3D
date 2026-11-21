using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Overbryce.Core;
using Overbryce.Core.Geometry;
using Overbryce.Core.Model;

namespace Overbryce.App.Rendering;

internal sealed class RenderOptions
{
    /// <summary>Original "Wire frame" view (GLU_LINE render style): unlit white lines, lights and cameras visible.</summary>
    public bool Wireframe { get; init; }

    /// <summary>Original "Alpha blending" setting: GL_BLEND + alpha test.</summary>
    public bool AlphaBlend { get; init; }

    /// <summary>Highlighted element (and its children).</summary>
    public Element? Selected { get; init; }

    public Matrix4x4 View { get; init; } = Matrix4x4.Identity;
    public Matrix4x4 Projection { get; init; } = Matrix4x4.Identity;

    /// <summary>Maps a texture name from the model to a file path (or null when not found).</summary>
    public Func<string, string?> ResolveTexture { get; init; } = _ => null;

    /// <summary>The modern renderer's settings, or null for the classic (1998) look.</summary>
    public ModernSettings? Modern { get; init; }

    /// <summary>Multisampling of the modern renderer's frame (the classic one uses the target's own).</summary>
    public int Samples { get; init; } = 4;
}

/// <summary>Draws a <see cref="Scene"/> with OpenGL 3.3 core and finds the element under the mouse.</summary>
internal sealed partial class SceneRenderer : IDisposable
{
    private static readonly Vector4 White = new(1, 1, 1, 1);
    private static readonly Vector4 LightIconColor = new(1, 1, 0, 1);
    private static readonly Vector4 ActiveCameraColor = new(0, 1, 1, 1);
    private static readonly Vector4 CameraColor = new(0, 1, 0, 1);
    private static readonly Vector4 HighlightColor = new(1f, 0.55f, 0.1f, 1);

    private const float IconLength = 0.8f;

    private readonly ShaderProgram _shader;
    private readonly GeometryBuilder _geometry = new();
    private readonly List<DrawCommand> _commands = new();
    private readonly int _vao;
    private readonly int _vbo;
    private int _vboCapacity;
    private PickBuffer? _pickBuffer;

    public TextureCache Textures { get; } = new();

    private sealed class DrawCommand
    {
        public List<DrawRange> Ranges = new();
        public Matrix4x4 Model;
        public bool Lit;
        public Vector4 Color;
        public Element? Material;
        public int Texture;
        public bool SphereMap;
        public bool Overlay;

        /// <summary>The scene element this geometry belongs to (for picking).</summary>
        public Element? Owner;

        /// <summary>Filled (not wire-frame) geometry.</summary>
        public bool Filled;

        /// <summary>Bounding sphere in world space.</summary>
        public Vector3 Center;
        public float Radius;

        /// <summary>Modern rendering: the reflection probe this element uses (-1 none).</summary>
        public int Probe = -1;
    }

    public SceneRenderer()
    {
        _shader = new ShaderProgram();
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        int stride = GeometryBuilder.FloatsPerVertex * sizeof(float);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
        GL.BindVertexArray(0);
    }

    public void Render(Scene? scene, RenderOptions options, int width, int height)
    {
        if (scene != null && options.Modern != null && !options.Wireframe)
        {
            RenderModern(scene, options, options.Modern, Math.Max(1, width), Math.Max(1, height));
            return;
        }

        GL.Viewport(0, 0, Math.Max(1, width), Math.Max(1, height));
        GL.ClearColor(0, 0, 0, 1);
        GL.ClearDepth(1);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        if (scene == null) return;

        Prepare(scene, options);
        SetBlend(options);

        _shader.Set("uPass", 0);
        _shader.Set("uModern", false);
        _shader.Set("uEnvOn", false);
        _shader.Set("uUseAo", false);
        _shader.Set("uGlobalAmbient", new Vector4(0.2f, 0.2f, 0.2f, 1f)); // GL_LIGHT_MODEL_AMBIENT default
        _shader.Set("uCameraPos", CameraPosition(options.View));
        SetupLights(scene, options);
        SetupFog(scene, options);

        GL.Enable(EnableCap.PolygonOffsetFill);
        GL.PolygonOffset(1f, 1f);
        foreach (var cmd in _commands)
            if (!cmd.Overlay) Draw(cmd, cmd.Color);
        GL.Disable(EnableCap.PolygonOffsetFill);

        _shader.Set("uFogMode", 0);
        foreach (var cmd in _commands)
            if (cmd.Overlay) Draw(cmd, cmd.Color);

        GL.BindVertexArray(0);
    }

    /// <summary>
    /// Returns the element drawn at window pixel (<paramref name="x"/>, <paramref name="y"/>) (origin
    /// top-left), or null for the background. Each element is drawn in a unique colour into an
    /// off-screen buffer; the nearest hit within <paramref name="radius"/> pixels wins, so thin wire-frame
    /// lines can be picked too. Transparent texels (with alpha blending on) are click-through.
    /// </summary>
    public Element? Pick(Scene? scene, RenderOptions options, int width, int height, int x, int y, int radius)
    {
        if (scene == null || width <= 0 || height <= 0) return null;

        _pickBuffer ??= new PickBuffer();
        _pickBuffer.Ensure(width, height);
        _pickBuffer.Bind();
        try
        {
            GL.Viewport(0, 0, width, height);
            GL.ClearColor(0, 0, 0, 0);
            GL.ClearDepth(1);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            Prepare(scene, options);
            GL.Disable(EnableCap.Blend);
            _shader.Set("uPass", 1);
            _shader.Set("uFogMode", 0);
            _shader.Set("uLightCount", 0);

            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.PolygonOffset(1f, 1f);
            for (int i = 0; i < _commands.Count; i++)
            {
                var cmd = _commands[i];
                if (cmd.Overlay || cmd.Owner == null) continue;
                int id = i + 1;
                Draw(cmd, new Vector4((id & 0xFF) / 255f, ((id >> 8) & 0xFF) / 255f, ((id >> 16) & 0xFF) / 255f, 1f));
            }
            GL.Disable(EnableCap.PolygonOffsetFill);
            GL.BindVertexArray(0);
            _shader.Set("uPass", 0);

            // read a small window around the cursor (GL rows start at the bottom)
            int cx = x, cy = height - 1 - y;
            int x0 = Math.Clamp(cx - radius, 0, width - 1), x1 = Math.Clamp(cx + radius, 0, width - 1);
            int y0 = Math.Clamp(cy - radius, 0, height - 1), y1 = Math.Clamp(cy + radius, 0, height - 1);
            int w = x1 - x0 + 1, h = y1 - y0 + 1;
            var pixels = new byte[w * h * 4];
            GL.PixelStore(PixelStoreParameter.PackAlignment, 1);
            GL.ReadPixels(x0, y0, w, h, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

            int best = 0;
            int bestDist = int.MaxValue;
            for (int py = 0; py < h; py++)
            for (int px = 0; px < w; px++)
            {
                int o = (py * w + px) * 4;
                int id = pixels[o] | (pixels[o + 1] << 8) | (pixels[o + 2] << 16);
                if (id == 0) continue;
                int dx = x0 + px - cx, dy = y0 + py - cy, d = dx * dx + dy * dy;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = id;
                }
            }
            return best > 0 && best <= _commands.Count ? _commands[best - 1].Owner : null;
        }
        finally
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }
    }

    /// <summary>Builds the draw commands for <paramref name="scene"/>, uploads the vertices and sets the shared state.</summary>
    private void Prepare(Scene scene, RenderOptions options)
    {
        _geometry.Clear();
        _commands.Clear();
        Visit(scene, Matrix4x4.Identity, options, highlighted: false);

        var data = _geometry.Data;
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        int bytes = data.Length * sizeof(float);
        if (bytes > _vboCapacity)
            _vboCapacity = Math.Max(bytes, _vboCapacity * 2);
        GL.BufferData(BufferTarget.ArrayBuffer, _vboCapacity, IntPtr.Zero, BufferUsageHint.StreamDraw); // (re)allocate / orphan
        if (bytes > 0)
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, bytes, ref System.Runtime.InteropServices.MemoryMarshal.GetReference(data));

        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);
        GL.Disable(EnableCap.CullFace);

        _shader.Use();
        _shader.Set("uView", options.View);
        _shader.Set("uProj", options.Projection);
        _shader.Set("uAlphaTest", options.AlphaBlend);
        _shaderAlphaTest = options.AlphaBlend;
        AssignSamplerUnits(_shader);
    }

    /// <summary>Every sampler on its own unit: samplers of different types sharing one make draws fail.</summary>
    private static void AssignSamplerUnits(ShaderProgram program)
    {
        program.Set("uTexture", 0);
        program.Set("uAoTex", AoUnit);
        program.Set("uEnvMap", EnvUnit);
        for (int i = 0; i < ShaderProgram.MaxShadowLights; i++) program.Set($"uShadow{i}", ShadowUnit + i);
    }

    private static void SetBlend(RenderOptions options)
    {
        if (options.AlphaBlend)
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }
        else
        {
            GL.Disable(EnableCap.Blend);
        }
    }

    /// <summary>World position of the eye of a view matrix.</summary>
    private static Vector3 CameraPosition(Matrix4x4 view) =>
        Matrix4x4.Invert(view, out var inv) ? new Vector3(inv.M41, inv.M42, inv.M43) : Vector3.Zero;

    /// <summary>Bounding sphere of a command's vertices in world space.</summary>
    private void ComputeBounds(DrawCommand cmd)
    {
        var data = _geometry.Data;
        const int F = GeometryBuilder.FloatsPerVertex;
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var r in cmd.Ranges)
            for (int v = r.First; v < r.First + r.Count; v++)
            {
                var p = new Vector3(data[v * F], data[v * F + 1], data[v * F + 2]);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        if (min.X > max.X) return;
        var c = (min + max) / 2;
        float r2 = 0;
        foreach (var r in cmd.Ranges)
            for (int v = r.First; v < r.First + r.Count; v++)
                r2 = MathF.Max(r2, Vector3.DistanceSquared(c, new Vector3(data[v * F], data[v * F + 1], data[v * F + 2])));
        var m = cmd.Model;
        float scale = MathF.Sqrt(MathF.Max(new Vector3(m.M11, m.M12, m.M13).LengthSquared(),
            MathF.Max(new Vector3(m.M21, m.M22, m.M23).LengthSquared(), new Vector3(m.M31, m.M32, m.M33).LengthSquared())));
        cmd.Center = Vector3.Transform(c, m);
        cmd.Radius = MathF.Sqrt(r2) * scale;
    }

    private void Draw(DrawCommand cmd, Vector4 color)
    {
        _shader.Set("uModel", cmd.Model);
        _shader.Set("uLit", cmd.Lit);
        _shader.Set("uColor", color);
        if (cmd.Material is { } m)
        {
            _shader.Set("uMatAmbient", ToVec(m.Ambient));
            _shader.Set("uMatDiffuse", ToVec(m.Diffuse));
            _shader.Set("uMatSpecular", ToVec(m.Specular));
            _shader.Set("uMatEmission", ToVec(m.Emission));
            _shader.Set("uShininess", Math.Clamp(m.Shininess, 0f, 1f) * 128f);
        }
        _shader.Set("uTextured", cmd.Texture != 0);
        _shader.Set("uSphereMap", cmd.SphereMap);
        if (cmd.Texture != 0)
        {
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, cmd.Texture);
        }
        foreach (var r in cmd.Ranges)
            GL.DrawArrays(ToGl(r.Mode), r.First, r.Count);
    }

    private static Vector4 ToVec(Rgba c) => new(c.R, c.G, c.B, c.A);

    // ---------------------------------------------------------------------------- lights & fog

    /// <summary>A light as uploaded to the shader (world space), for the modern passes.</summary>
    private readonly record struct ActiveLight(LightElement Light, Vector3 Position, Vector3 Direction);

    private readonly List<ActiveLight> _lights = new();

    private void SetupLights(Scene scene, RenderOptions options)
    {
        int count = 0;
        _lights.Clear();
        if (!options.Wireframe)
        {
            int slot = 0;
            foreach (var light in scene.Lights)
            {
                if (slot++ >= ShaderProgram.MaxLights) break; // the original had GL_LIGHT0..7 only
                if (!light.Enabled) continue;

                // position/direction are given in the frame of the light's parent (scene/group chain)
                var toWorld = Transforms.ParentChain(light);
                var pos = Vector3.Transform(new Vector3(light.X, light.Y, light.Z), toWorld);
                var dir = Vector3.TransformNormal(new Vector3(light.Alpha, light.Beta, light.Gamma), toWorld);
                _lights.Add(new ActiveLight(light, pos, dir));
                string i = $"[{count}]";
                _shader.Set("uLightPos" + i, pos);
                _shader.Set("uLightSpotDir" + i, dir);
                _shader.Set("uLightAmbient" + i, ToVec(light.Ambient));
                _shader.Set("uLightDiffuse" + i, ToVec(light.Diffuse));
                _shader.Set("uLightSpecular" + i, ToVec(light.Specular));
                float cutoff = light.SpotCutoff > 90 ? 180f : (float)Math.Max(0, light.SpotCutoff);
                _shader.Set("uLightSpot" + i, new Vector4(cutoff, (float)Math.Clamp(light.SpotExponent, 0, 128),
                    (float)light.ConstantAttenuation, (float)light.LinearAttenuation));
                _shader.Set("uLightQuadratic" + i, (float)light.QuadraticAttenuation);
                count++;
            }
        }
        _shader.Set("uLightCount", count);
    }

    private void SetupFog(Scene scene, RenderOptions options)
    {
        int mode = options.Wireframe ? 0 : (int)scene.FogMode;
        _shader.Set("uFogMode", mode);
        _shader.Set("uFogColor", ToVec(scene.FogColor));
        _shader.Set("uFogStart", scene.FogStart);
        _shader.Set("uFogEnd", scene.FogEnd);
        _shader.Set("uFogDensity", scene.FogDensity);
    }

    // ---------------------------------------------------------------------------- traversal

    private void Visit(Element e, Matrix4x4 parentWorld, RenderOptions options, bool highlighted)
    {
        highlighted |= e is not Scene && ReferenceEquals(e, options.Selected);

        if (e is GroupElement g)
        {
            var world = Transforms.GroupLocal(g) * parentWorld;
            foreach (var child in g.Children)
                Visit(child, world, options, highlighted);
            return;
        }

        switch (e)
        {
            case LightElement light:
                if (options.Wireframe || highlighted)
                {
                    // the icon points along the spot direction vector (alpha/beta/gamma of a light);
                    // the original rotated it by these values as if they were angles
                    var model = AlignZ(new Vector3(light.Alpha, light.Beta, light.Gamma))
                                * Matrix4x4.CreateTranslation(light.X, light.Y, light.Z) * parentWorld;
                    var color = highlighted ? HighlightColor : LightIconColor;
                    AddLines(e, model, color, overlay: !options.Wireframe, ranges =>
                    {
                        if (light.SpotCutoff > 90)
                            _geometry.Sphere(ranges, 0.25, 7, 5, lines: true);
                        else
                            _geometry.Cylinder(ranges, 0, IconLength * Math.Tan(Math.Min(light.SpotCutoff, 80) * Math.PI / 180),
                                IconLength, 8, 1, lines: true);
                    });
                }
                return;

            case CameraElement camera:
                if (options.Wireframe || highlighted)
                {
                    // a cone opening along the viewing direction (-Z of the camera frame)
                    var model = Matrix4x4.CreateRotationY(MathF.PI) * Transforms.CameraLocal(camera) * parentWorld;
                    var color = highlighted ? HighlightColor : camera.Active ? ActiveCameraColor : CameraColor;
                    AddLines(e, model, color, overlay: !options.Wireframe,
                        ranges => _geometry.Cylinder(ranges, 0, IconLength * Math.Tan(Math.Clamp(camera.FieldOfView, 1, 170) * Math.PI / 360),
                            IconLength, 4, 1, lines: true));
                }
                return;
        }

        var local = Transforms.ElementLocal(e) * parentWorld;
        bool lines = options.Wireframe || e.DrawStyle == DrawStyle.Line;
        if (options.Wireframe)
        {
            AddLines(e, local, highlighted ? HighlightColor : White, overlay: false, ranges => BuildShape(e, ranges, true));
            return;
        }

        var cmd = new DrawCommand
        {
            Model = local,
            Lit = true,
            Color = White,
            Material = e,
            SphereMap = e is TorusElement,
            Owner = e,
            Filled = !lines,
        };
        if (e.HasTexture && options.ResolveTexture(e.Texture!) is { } path)
            cmd.Texture = Textures.Get(path);
        BuildShape(e, cmd.Ranges, lines);
        if (cmd.Ranges.Count > 0) _commands.Add(cmd);

        if (highlighted)
            AddLines(e, local, HighlightColor, overlay: true, ranges => BuildShape(e, ranges, true));
    }

    /// <summary>Rotation taking the +Z axis onto <paramref name="dir"/> (identity for a zero vector).</summary>
    private static Matrix4x4 AlignZ(Vector3 dir)
    {
        if (dir.LengthSquared() < 1e-12f) return Matrix4x4.Identity;
        dir = Vector3.Normalize(dir);
        float c = Vector3.Dot(Vector3.UnitZ, dir);
        if (c > 0.99999f) return Matrix4x4.Identity;
        if (c < -0.99999f) return Matrix4x4.CreateRotationX(MathF.PI);
        var axis = Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, dir));
        float angle = MathF.Acos(c);
        var m = Matrix4x4.CreateFromAxisAngle(axis, angle);
        return Vector3.Distance(Vector3.TransformNormal(Vector3.UnitZ, m), dir) < 1e-3f ? m : Matrix4x4.CreateFromAxisAngle(axis, -angle);
    }

    private void AddLines(Element owner, Matrix4x4 model, Vector4 color, bool overlay, Action<List<DrawRange>> build)
    {
        var cmd = new DrawCommand { Model = model, Lit = false, Color = color, Overlay = overlay, Owner = owner };
        build(cmd.Ranges);
        if (cmd.Ranges.Count > 0) _commands.Add(cmd);
    }

    private void BuildShape(Element e, List<DrawRange> o, bool lines) => _geometry.Shape(o, e, lines);

    /// <summary>The OpenGL primitive of a range of the shared tessellation.</summary>
    internal static PrimitiveType ToGl(PrimitiveMode mode) => mode switch
    {
        PrimitiveMode.Triangles => PrimitiveType.Triangles,
        PrimitiveMode.TriangleStrip => PrimitiveType.TriangleStrip,
        PrimitiveMode.Lines => PrimitiveType.Lines,
        PrimitiveMode.LineStrip => PrimitiveType.LineStrip,
        _ => PrimitiveType.LineLoop,
    };

    private bool _shaderAlphaTest;

    public void Dispose()
    {
        _modern?.Dispose();
        _shadowShader?.Dispose();
        Textures.Dispose();
        _pickBuffer?.Dispose();
        GL.DeleteBuffer(_vbo);
        GL.DeleteVertexArray(_vao);
        _shader.Dispose();
    }

    /// <summary>Single-sampled colour + depth framebuffer for the object-id pass.</summary>
    private sealed class PickBuffer : IDisposable
    {
        private int _fbo, _color, _depth, _width, _height;

        public void Ensure(int width, int height)
        {
            if (_fbo != 0 && width == _width && height == _height) return;
            Dispose();
            _width = width;
            _height = height;
            _fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
            _color = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _color);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Rgba8, width, height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, _color);
            _depth = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depth);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, width, height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _depth);
            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            if (status != FramebufferErrorCode.FramebufferComplete)
                throw new InvalidOperationException("Pick framebuffer incomplete: " + status);
        }

        public void Bind() => GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);

        public void Dispose()
        {
            if (_fbo == 0) return;
            GL.DeleteFramebuffer(_fbo);
            GL.DeleteRenderbuffer(_color);
            GL.DeleteRenderbuffer(_depth);
            _fbo = _color = _depth = 0;
        }
    }
}
