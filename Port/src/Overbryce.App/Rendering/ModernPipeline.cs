using System.Numerics;
using OpenTK.Graphics.OpenGL4;

namespace Overbryce.App.Rendering;

/// <summary>
/// GPU resources and post-processing of the modern renderer: the HDR multisampled frame, shadow
/// cube maps, reflection probes, the screen-space ambient occlusion buffers and the bloom chain.
/// Everything is created on first use and resized when needed.
/// </summary>
internal sealed class ModernPipeline : IDisposable
{
    private const int BloomLevels = 6;
    private const int KernelSize = 16;

    private readonly ShaderProgram _down = new(Post.Vertex, Post.Down);
    private readonly ShaderProgram _up = new(Post.Vertex, Post.Up);
    private readonly ShaderProgram _composite = new(Post.Vertex, Post.Composite);
    private readonly ShaderProgram _ssao = new(Post.Vertex, Post.Ssao);
    private readonly ShaderProgram _blur = new(Post.Vertex, Post.Blur);
    private readonly int _emptyVao = GL.GenVertexArray();
    private readonly int _noise;
    private readonly Vector3[] _kernel = new Vector3[KernelSize];

    // the HDR frame
    private int _width, _height, _samples = -1;
    private int _msFbo, _msColor, _msDepth;
    private int _hdrFbo, _hdrTex;

    // bloom
    private readonly int[] _bloomFbo = new int[BloomLevels];
    private readonly int[] _bloomTex = new int[BloomLevels];
    private readonly (int W, int H)[] _bloomSize = new (int, int)[BloomLevels];
    private int _bloomCount;

    // ambient occlusion
    private int _gFbo, _gNormal, _gDepth, _aoFbo, _aoRaw, _aoBlurFbo, _aoTex;

    // shadows and probes
    private readonly CubeTarget?[] _shadows = new CubeTarget?[ShaderProgram.MaxShadowLights];
    private readonly List<CubeTarget> _probes = [];

    public ModernPipeline()
    {
        // occlusion kernel: points in the hemisphere around +Z, denser near the centre
        var rng = new Random(1998);
        for (int i = 0; i < KernelSize; i++)
        {
            var v = Vector3.Normalize(new Vector3(rng.NextSingle() * 2 - 1, rng.NextSingle() * 2 - 1, rng.NextSingle() * 0.9f + 0.1f));
            float scale = i / (float)KernelSize;
            v *= rng.NextSingle() * (0.1f + 0.9f * scale * scale);
            _kernel[i] = v;
        }

        // 4x4 random rotations of the kernel around the normal
        var noise = new float[4 * 4 * 3];
        for (int i = 0; i < 16; i++)
        {
            var v = Vector2.Normalize(new Vector2(rng.NextSingle() * 2 - 1, rng.NextSingle() * 2 - 1) + new Vector2(1e-3f));
            noise[i * 3] = v.X;
            noise[i * 3 + 1] = v.Y;
        }
        _noise = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _noise);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb16f, 4, 4, 0, PixelFormat.Rgb, PixelType.Float, noise);
        SetFilter(TextureTarget.Texture2D, TextureMinFilter.Nearest, TextureWrapMode.Repeat);
    }

    public int AoTexture => _aoTex;

    // ==================================================================================== frame

    /// <summary>Prepares the HDR frame (and the occlusion buffers) for <paramref name="width"/> x <paramref name="height"/>.</summary>
    public void EnsureFrame(int width, int height, int samples)
    {
        GL.GetInteger(GetPName.MaxSamples, out int max);
        samples = Math.Clamp(samples, 0, max);
        if (_msFbo != 0 && width == _width && height == _height && samples == _samples) return;
        DisposeFrame();
        _width = width;
        _height = height;
        _samples = samples;

        _msFbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _msFbo);
        _msColor = Renderbuffer(RenderbufferStorage.Rgba16f, width, height, samples);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, _msColor);
        _msDepth = Renderbuffer(RenderbufferStorage.DepthComponent24, width, height, samples);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _msDepth);
        Check("HDR");

        (_hdrFbo, _hdrTex) = ColorTarget(width, height, PixelInternalFormat.Rgba16f, PixelFormat.Rgba);

        // bloom: half the size and down from there
        _bloomCount = 0;
        int w = Math.Max(1, width / 2), h = Math.Max(1, height / 2);
        for (int i = 0; i < BloomLevels && (i == 0 || Math.Min(w, h) >= 4); i++)
        {
            (_bloomFbo[i], _bloomTex[i]) = ColorTarget(w, h, PixelInternalFormat.R11fG11fB10f, PixelFormat.Rgb);
            _bloomSize[i] = (w, h);
            _bloomCount++;
            w = Math.Max(1, w / 2);
            h = Math.Max(1, h / 2);
        }

        // ambient occlusion: normals + depth of the opaque elements, the raw and the blurred occlusion
        _gFbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _gFbo);
        _gNormal = Texture2D(width, height, PixelInternalFormat.Rgba16f, PixelFormat.Rgba, PixelType.Float, TextureMinFilter.Nearest);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _gNormal, 0);
        _gDepth = Texture2D(width, height, PixelInternalFormat.DepthComponent24, PixelFormat.DepthComponent, PixelType.Float, TextureMinFilter.Nearest);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, _gDepth, 0);
        Check("G-buffer");
        (_aoFbo, _aoRaw) = ColorTarget(width, height, PixelInternalFormat.R8, PixelFormat.Red);
        (_aoBlurFbo, _aoTex) = ColorTarget(width, height, PixelInternalFormat.R8, PixelFormat.Red);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void BindFrame()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _msFbo);
        GL.Viewport(0, 0, _width, _height);
    }

    public void BindNormals()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _gFbo);
        GL.Viewport(0, 0, _width, _height);
    }

    /// <summary>Turns the normals and depth of the opaque elements into the blurred occlusion texture.</summary>
    public void ComputeOcclusion(Matrix4x4 projection, float radius, float strength)
    {
        Matrix4x4.Invert(projection, out var inverse);
        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.Blend);
        GL.BindVertexArray(_emptyVao);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _aoFbo);
        GL.Viewport(0, 0, _width, _height);
        _ssao.Use();
        Bind(0, TextureTarget.Texture2D, _gDepth);
        Bind(1, TextureTarget.Texture2D, _gNormal);
        Bind(2, TextureTarget.Texture2D, _noise);
        _ssao.Set("uDepth", 0);
        _ssao.Set("uNormal", 1);
        _ssao.Set("uNoise", 2);
        _ssao.Set("uProj", projection);
        _ssao.Set("uInvProj", inverse);
        _ssao.Set("uRadius", radius);
        _ssao.Set("uStrength", strength);
        _ssao.Set("uNoiseScale", new Vector2(_width / 4f, _height / 4f));
        for (int i = 0; i < KernelSize; i++) _ssao.Set($"uKernel[{i}]", _kernel[i]);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _aoBlurFbo);
        _blur.Use();
        Bind(0, TextureTarget.Texture2D, _aoRaw);
        _blur.Set("uSrc", 0);
        _blur.Set("uTexel", new Vector2(1f / _width, 1f / _height));
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
    }

    /// <summary>Resolves the HDR frame, adds the bloom and writes the tone-mapped picture into <paramref name="targetFbo"/>.</summary>
    public void Finish(int targetFbo, int width, int height, bool bloom, float bloomStrength, float threshold, float exposure)
    {
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _msFbo);
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _hdrFbo);
        GL.BlitFramebuffer(0, 0, _width, _height, 0, 0, _width, _height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.Blend);
        GL.BindVertexArray(_emptyVao);

        if (bloom && _bloomCount > 0)
        {
            // down: the bright part of the frame into level 0, then each level into the next
            _down.Use();
            _down.Set("uSrc", 0);
            _down.Set("uThreshold", threshold);
            _down.Set("uKnee", Math.Max(0.05f, threshold * 0.5f));
            for (int i = 0; i < _bloomCount; i++)
            {
                int src = i == 0 ? _hdrTex : _bloomTex[i - 1];
                var (sw, sh) = i == 0 ? (_width, _height) : _bloomSize[i - 1];
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _bloomFbo[i]);
                GL.Viewport(0, 0, _bloomSize[i].W, _bloomSize[i].H);
                Bind(0, TextureTarget.Texture2D, src);
                _down.Set("uTexel", new Vector2(1f / sw, 1f / sh));
                _down.Set("uPrefilter", i == 0);
                GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            }

            // up: each level added to the one above it
            _up.Use();
            _up.Set("uSrc", 0);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            for (int i = _bloomCount - 1; i > 0; i--)
            {
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _bloomFbo[i - 1]);
                GL.Viewport(0, 0, _bloomSize[i - 1].W, _bloomSize[i - 1].H);
                Bind(0, TextureTarget.Texture2D, _bloomTex[i]);
                _up.Set("uTexel", new Vector2(1f / _bloomSize[i].W, 1f / _bloomSize[i].H));
                GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            }
            GL.Disable(EnableCap.Blend);
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, targetFbo);
        GL.Viewport(0, 0, width, height);
        _composite.Use();
        Bind(0, TextureTarget.Texture2D, _hdrTex);
        Bind(1, TextureTarget.Texture2D, _bloomCount > 0 ? _bloomTex[0] : _hdrTex);
        _composite.Set("uHdr", 0);
        _composite.Set("uBloom", 1);
        _composite.Set("uBloomOn", bloom && _bloomCount > 0);
        _composite.Set("uBloomStrength", bloomStrength / Math.Max(1, _bloomCount));
        _composite.Set("uExposure", exposure);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

        GL.BindVertexArray(0);
        GL.ActiveTexture(TextureUnit.Texture0);
    }

    // ==================================================================================== shadows and probes

    /// <summary>The depth cube map of shadow slot <paramref name="slot"/>.</summary>
    public CubeTarget Shadow(int slot, int size)
    {
        var t = _shadows[slot];
        if (t == null || t.Size != size)
        {
            t?.Dispose();
            t = _shadows[slot] = new CubeTarget(size, depthOnly: true);
        }
        return t;
    }

    public CubeTarget? ShadowIfAny(int slot) => _shadows[slot];

    /// <summary>The colour cube map of reflection probe <paramref name="index"/>.</summary>
    public CubeTarget Probe(int index, int size)
    {
        while (_probes.Count <= index) _probes.Add(new CubeTarget(size, depthOnly: false));
        if (_probes[index].Size != size)
        {
            _probes[index].Dispose();
            _probes[index] = new CubeTarget(size, depthOnly: false);
        }
        return _probes[index];
    }

    // ==================================================================================== helpers

    private static void Bind(int unit, TextureTarget target, int texture)
    {
        GL.ActiveTexture(TextureUnit.Texture0 + unit);
        GL.BindTexture(target, texture);
    }

    private static int Renderbuffer(RenderbufferStorage format, int width, int height, int samples)
    {
        int rb = GL.GenRenderbuffer();
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, rb);
        if (samples > 0)
            GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, samples, format, width, height);
        else
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, format, width, height);
        return rb;
    }

    private static int Texture2D(int width, int height, PixelInternalFormat internalFormat, PixelFormat format, PixelType type, TextureMinFilter filter)
    {
        int tex = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, tex);
        GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, width, height, 0, format, type, IntPtr.Zero);
        SetFilter(TextureTarget.Texture2D, filter, TextureWrapMode.ClampToEdge);
        return tex;
    }

    private static (int Fbo, int Tex) ColorTarget(int width, int height, PixelInternalFormat internalFormat, PixelFormat format)
    {
        int fbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        int tex = Texture2D(width, height, internalFormat, format, PixelType.Float, TextureMinFilter.Linear);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, tex, 0);
        Check(internalFormat.ToString());
        return (fbo, tex);
    }

    internal static void SetFilter(TextureTarget target, TextureMinFilter filter, TextureWrapMode wrap)
    {
        GL.TexParameter(target, TextureParameterName.TextureMinFilter, (int)filter);
        GL.TexParameter(target, TextureParameterName.TextureMagFilter, (int)(filter == TextureMinFilter.Nearest ? TextureMagFilter.Nearest : TextureMagFilter.Linear));
        GL.TexParameter(target, TextureParameterName.TextureWrapS, (int)wrap);
        GL.TexParameter(target, TextureParameterName.TextureWrapT, (int)wrap);
        if (target == TextureTarget.TextureCubeMap)
            GL.TexParameter(target, TextureParameterName.TextureWrapR, (int)wrap);
    }

    internal static void Check(string what)
    {
        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
            throw new InvalidOperationException($"{what} framebuffer incomplete: {status}");
    }

    private void DisposeFrame()
    {
        if (_msFbo == 0) return;
        GL.DeleteFramebuffer(_msFbo);
        GL.DeleteRenderbuffer(_msColor);
        GL.DeleteRenderbuffer(_msDepth);
        GL.DeleteFramebuffer(_hdrFbo);
        GL.DeleteTexture(_hdrTex);
        for (int i = 0; i < _bloomCount; i++)
        {
            GL.DeleteFramebuffer(_bloomFbo[i]);
            GL.DeleteTexture(_bloomTex[i]);
        }
        GL.DeleteFramebuffer(_gFbo);
        GL.DeleteTexture(_gNormal);
        GL.DeleteTexture(_gDepth);
        GL.DeleteFramebuffer(_aoFbo);
        GL.DeleteTexture(_aoRaw);
        GL.DeleteFramebuffer(_aoBlurFbo);
        GL.DeleteTexture(_aoTex);
        _msFbo = 0;
        _bloomCount = 0;
    }

    public void Dispose()
    {
        DisposeFrame();
        foreach (var s in _shadows) s?.Dispose();
        foreach (var p in _probes) p.Dispose();
        _probes.Clear();
        GL.DeleteTexture(_noise);
        GL.DeleteVertexArray(_emptyVao);
        _down.Dispose();
        _up.Dispose();
        _composite.Dispose();
        _ssao.Dispose();
        _blur.Dispose();
    }

    // ==================================================================================== shaders

    private static class Post
    {
        public const string Vertex = """
            #version 330 core
            out vec2 vUV;
            void main()
            {
                vec2 p = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
                vUV = p;
                gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
            }
            """;

        // dual-filter downsample; on the first level only the part above the threshold (soft knee)
        public const string Down = """
            #version 330 core
            in vec2 vUV;
            out vec4 fragColor;
            uniform sampler2D uSrc;
            uniform vec2 uTexel;
            uniform bool uPrefilter;
            uniform float uThreshold;
            uniform float uKnee;

            vec3 tap(vec2 uv)
            {
                vec3 c = min(texture(uSrc, uv).rgb, vec3(64.0));
                if (!uPrefilter) return c;
                float br = max(c.r, max(c.g, c.b));
                float rq = clamp(br - uThreshold + uKnee, 0.0, 2.0 * uKnee);
                rq = rq * rq / (4.0 * uKnee + 1e-5);
                return c * max(rq, br - uThreshold) / max(br, 1e-4);
            }

            void main()
            {
                vec2 h = uTexel;
                vec3 sum = tap(vUV) * 4.0 + tap(vUV - h) + tap(vUV + h) + tap(vUV + vec2(h.x, -h.y)) + tap(vUV + vec2(-h.x, h.y));
                fragColor = vec4(sum / 8.0, 1.0);
            }
            """;

        // dual-filter upsample (tent)
        public const string Up = """
            #version 330 core
            in vec2 vUV;
            out vec4 fragColor;
            uniform sampler2D uSrc;
            uniform vec2 uTexel;
            void main()
            {
                vec2 h = uTexel;
                vec3 sum = texture(uSrc, vUV + vec2(-2.0 * h.x, 0.0)).rgb
                         + texture(uSrc, vUV + vec2(-h.x, h.y)).rgb * 2.0
                         + texture(uSrc, vUV + vec2(0.0, 2.0 * h.y)).rgb
                         + texture(uSrc, vUV + vec2(h.x, h.y)).rgb * 2.0
                         + texture(uSrc, vUV + vec2(2.0 * h.x, 0.0)).rgb
                         + texture(uSrc, vUV + vec2(h.x, -h.y)).rgb * 2.0
                         + texture(uSrc, vUV + vec2(0.0, -2.0 * h.y)).rgb
                         + texture(uSrc, vUV + vec2(-h.x, -h.y)).rgb * 2.0;
                fragColor = vec4(sum / 12.0, 1.0);
            }
            """;

        // bloom + exposure, then a shoulder that keeps everything up to 0.8 as it is and rolls the
        // rest off smoothly towards 1 (the classic look stays, the highlights no longer clip)
        public const string Composite = """
            #version 330 core
            in vec2 vUV;
            out vec4 fragColor;
            uniform sampler2D uHdr;
            uniform sampler2D uBloom;
            uniform bool uBloomOn;
            uniform float uBloomStrength;
            uniform float uExposure;
            void main()
            {
                vec3 c = texture(uHdr, vUV).rgb;
                if (uBloomOn) c += texture(uBloom, vUV).rgb * uBloomStrength;
                c = max(c * uExposure, 0.0);
                const float k = 0.8;
                vec3 over = max(c - k, 0.0);
                c = min(c, vec3(k)) + (1.0 - k) * (1.0 - exp(-over / (1.0 - k)));
                fragColor = vec4(c, 1.0);
            }
            """;

        public const string Ssao = """
            #version 330 core
            in vec2 vUV;
            out vec4 fragColor;
            uniform sampler2D uDepth;
            uniform sampler2D uNormal;
            uniform sampler2D uNoise;
            uniform mat4 uProj;
            uniform mat4 uInvProj;
            uniform vec3 uKernel[16];
            uniform float uRadius;
            uniform float uStrength;
            uniform vec2 uNoiseScale;

            vec3 viewPos(vec2 uv, float depth)
            {
                vec4 p = uInvProj * vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
                return p.xyz / p.w;
            }

            void main()
            {
                float depth = texture(uDepth, vUV).r;
                vec4 nd = texture(uNormal, vUV);
                if (depth >= 1.0 || nd.a == 0.0) { fragColor = vec4(1.0); return; }
                vec3 P = viewPos(vUV, depth);
                vec3 N = normalize(nd.xyz);
                vec3 rnd = vec3(texture(uNoise, vUV * uNoiseScale).xy, 0.0);
                vec3 T = normalize(rnd - N * dot(rnd, N) + vec3(1e-4));
                vec3 B = cross(N, T);
                mat3 tbn = mat3(T, B, N);
                float occlusion = 0.0;
                for (int i = 0; i < 16; i++)
                {
                    vec3 S = P + tbn * uKernel[i] * uRadius;
                    vec4 o = uProj * vec4(S, 1.0);
                    vec2 suv = o.xy / o.w * 0.5 + 0.5;
                    if (suv.x < 0.0 || suv.y < 0.0 || suv.x > 1.0 || suv.y > 1.0) continue;
                    float sd = texture(uDepth, suv).r;
                    if (sd >= 1.0) continue;
                    float sz = viewPos(suv, sd).z;
                    float range = smoothstep(0.0, 1.0, uRadius / max(abs(P.z - sz), 1e-4));
                    occlusion += (sz >= S.z + 0.02 * uRadius ? 1.0 : 0.0) * range;
                }
                fragColor = vec4(clamp(1.0 - uStrength * occlusion / 16.0, 0.0, 1.0));
            }
            """;

        public const string Blur = """
            #version 330 core
            in vec2 vUV;
            out vec4 fragColor;
            uniform sampler2D uSrc;
            uniform vec2 uTexel;
            void main()
            {
                float sum = 0.0;
                for (int x = -2; x < 2; x++)
                    for (int y = -2; y < 2; y++)
                        sum += texture(uSrc, vUV + (vec2(x, y) + 0.5) * uTexel).r;
                fragColor = vec4(sum / 16.0);
            }
            """;
    }
}

/// <summary>A cube map with a framebuffer per face: a light's shadow depth or a reflection probe.</summary>
internal sealed class CubeTarget : IDisposable
{
    private readonly int[] _fbo = new int[6];
    private readonly int _depth;

    public int Size { get; }
    public int Texture { get; }

    /// <summary>View directions and up vectors of the six faces, in the OpenGL cube map convention.</summary>
    public static readonly (Vector3 Dir, Vector3 Up)[] Faces =
    [
        (Vector3.UnitX, -Vector3.UnitY), (-Vector3.UnitX, -Vector3.UnitY),
        (Vector3.UnitY, Vector3.UnitZ), (-Vector3.UnitY, -Vector3.UnitZ),
        (Vector3.UnitZ, -Vector3.UnitY), (-Vector3.UnitZ, -Vector3.UnitY),
    ];

    public CubeTarget(int size, bool depthOnly)
    {
        Size = size;
        Texture = GL.GenTexture();
        GL.BindTexture(TextureTarget.TextureCubeMap, Texture);
        for (int f = 0; f < 6; f++)
        {
            if (depthOnly)
                GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + f, 0, PixelInternalFormat.DepthComponent24, size, size, 0,
                    PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            else
                GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + f, 0, PixelInternalFormat.Rgba16f, size, size, 0,
                    PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
        }
        ModernPipeline.SetFilter(TextureTarget.TextureCubeMap, TextureMinFilter.Linear, TextureWrapMode.ClampToEdge);
        if (depthOnly)
        {
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.CompareRefToTexture);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Lequal);
        }
        else
        {
            _depth = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depth);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, size, size);
        }

        for (int f = 0; f < 6; f++)
        {
            _fbo[f] = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo[f]);
            if (depthOnly)
            {
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.TextureCubeMapPositiveX + f, Texture, 0);
                GL.DrawBuffer(DrawBufferMode.None);
                GL.ReadBuffer(ReadBufferMode.None);
            }
            else
            {
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.TextureCubeMapPositiveX + f, Texture, 0);
                GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _depth);
            }
            ModernPipeline.Check(depthOnly ? "Shadow" : "Probe");
        }
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void BindFace(int face)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo[face]);
        GL.Viewport(0, 0, Size, Size);
    }

    /// <summary>View matrix of face <paramref name="face"/> seen from <paramref name="eye"/>.</summary>
    public static Matrix4x4 FaceView(Vector3 eye, int face) =>
        Matrix4x4.CreateLookAt(eye, eye + Faces[face].Dir, Faces[face].Up);

    public void Dispose()
    {
        foreach (int f in _fbo) GL.DeleteFramebuffer(f);
        if (_depth != 0) GL.DeleteRenderbuffer(_depth);
        GL.DeleteTexture(Texture);
    }
}
