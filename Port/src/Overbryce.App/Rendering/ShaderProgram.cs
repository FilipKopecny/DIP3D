using System.Numerics;
using OpenTK.Graphics.OpenGL4;

namespace Overbryce.App.Rendering;

/// <summary>A linked GLSL program with cached uniform locations.</summary>
internal sealed class ShaderProgram : IDisposable
{
    public const int MaxLights = 8;

    /// <summary>At most this many lights cast shadows at a time (one shadow cube map each).</summary>
    public const int MaxShadowLights = 4;

    private readonly int _program;
    private readonly Dictionary<string, int> _locations = new();

    /// <summary>The scene shader: the classic OpenGL 1.1 look and the modern extensions.</summary>
    public ShaderProgram() : this(SceneShader.Vertex, SceneShader.Fragment) { }

    public ShaderProgram(string vertexSource, string fragmentSource)
    {
        int vs = Compile(ShaderType.VertexShader, vertexSource);
        int fs = Compile(ShaderType.FragmentShader, fragmentSource);
        _program = GL.CreateProgram();
        GL.AttachShader(_program, vs);
        GL.AttachShader(_program, fs);
        GL.LinkProgram(_program);
        GL.GetProgram(_program, GetProgramParameterName.LinkStatus, out int ok);
        GL.DetachShader(_program, vs);
        GL.DetachShader(_program, fs);
        GL.DeleteShader(vs);
        GL.DeleteShader(fs);
        if (ok == 0)
            throw new InvalidOperationException("Shader link failed: " + GL.GetProgramInfoLog(_program));
    }

    private static int Compile(ShaderType type, string source)
    {
        int s = GL.CreateShader(type);
        GL.ShaderSource(s, source);
        GL.CompileShader(s);
        GL.GetShader(s, ShaderParameter.CompileStatus, out int ok);
        if (ok == 0)
            throw new InvalidOperationException($"{type} compile failed: " + GL.GetShaderInfoLog(s));
        return s;
    }

    public void Use() => GL.UseProgram(_program);

    private int Loc(string name)
    {
        if (!_locations.TryGetValue(name, out int loc))
        {
            loc = GL.GetUniformLocation(_program, name);
            _locations[name] = loc;
        }
        return loc;
    }

    public void Set(string name, bool v) => GL.Uniform1(Loc(name), v ? 1 : 0);
    public void Set(string name, int v) => GL.Uniform1(Loc(name), v);
    public void Set(string name, float v) => GL.Uniform1(Loc(name), v);
    public void Set(string name, Vector2 v) => GL.Uniform2(Loc(name), v.X, v.Y);
    public void Set(string name, Vector3 v) => GL.Uniform3(Loc(name), v.X, v.Y, v.Z);
    public void Set(string name, Vector4 v) => GL.Uniform4(Loc(name), v.X, v.Y, v.Z, v.W);

    public unsafe void Set(string name, Matrix4x4 m)
    {
        // System.Numerics row-vector matrices have the memory layout of column-major GL matrices
        GL.UniformMatrix4(Loc(name), 1, false, &m.M11);
    }

    public void Dispose() => GL.DeleteProgram(_program);
}

/// <summary>
/// GLSL 3.30 re-implementation of the OpenGL 1.1 fixed-function pipeline state the original used:
/// up to 8 positional/spot lights with attenuation (local viewer), the material model
/// (emission + ambient + diffuse + Blinn specular), GL_MODULATE texturing, sphere-map texture
/// generation (torus) and linear / exp / exp2 fog. Lighting is computed in world space, which gives
/// the same result as the eye space of OpenGL 1.1 (the view is a rigid motion) and lets the same
/// light setup serve the camera, the shadow maps and the reflection probes.
/// <para>
/// With <c>uModern</c> the shader adds the modern look: no clamping of the light (HDR, the bloom and
/// tone mapping follow in post-processing), emission times glow, shadows from cube maps, ambient
/// occlusion from a screen-space texture, and reflection / refraction from an environment cube map.
/// </para>
/// </summary>
internal static class SceneShader
{
    /// <summary>The variant for the shadow maps: writes the distance from the light as the depth.</summary>
    public static string ShadowFragment => Fragment.Replace("#version 330 core", "#version 330 core\n#define SHADOW_DEPTH");

    public const string Vertex = """
        #version 330 core
        layout(location = 0) in vec3 aPos;
        layout(location = 1) in vec3 aNormal;
        layout(location = 2) in vec2 aUV;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProj;

        out vec3 vEyePos;
        out vec3 vEyeNormal;
        out vec3 vWorldPos;
        out vec3 vWorldNormal;
        out vec2 vUV;

        void main()
        {
            vec4 world = uModel * vec4(aPos, 1.0);
            vec4 eye = uView * world;
            vEyePos = eye.xyz;
            vWorldPos = world.xyz;
            // GL_NORMALIZE is applied in the fragment shader
            vWorldNormal = transpose(inverse(mat3(uModel))) * aNormal;
            vEyeNormal = transpose(inverse(mat3(uView * uModel))) * aNormal;
            vUV = aUV;
            gl_Position = uProj * eye;
        }
        """;

    public const string Fragment = """
        #version 330 core
        in vec3 vEyePos;
        in vec3 vEyeNormal;
        in vec3 vWorldPos;
        in vec3 vWorldNormal;
        in vec2 vUV;
        out vec4 fragColor;

        uniform int uPass;                   // 0 colour, 1 object id (picking), 2 normals (occlusion), 3 shadow depth
        uniform bool uLit;
        uniform vec4 uColor;                 // unlit (wire frame) colour, or the object id

        uniform vec4 uMatAmbient;
        uniform vec4 uMatDiffuse;
        uniform vec4 uMatSpecular;
        uniform vec4 uMatEmission;
        uniform float uShininess;
        uniform vec4 uGlobalAmbient;
        uniform vec3 uCameraPos;             // world space

        uniform int uLightCount;
        uniform vec3 uLightPos[8];           // world space
        uniform vec3 uLightSpotDir[8];       // world space
        uniform vec4 uLightAmbient[8];
        uniform vec4 uLightDiffuse[8];
        uniform vec4 uLightSpecular[8];
        uniform vec4 uLightSpot[8];          // cutoff (deg), exponent, constant att., linear att.
        uniform float uLightQuadratic[8];

        uniform bool uTextured;
        uniform bool uSphereMap;
        uniform sampler2D uTexture;

        uniform int uFogMode;                // 0 none, 1 linear, 2 exp, 3 exp2
        uniform vec4 uFogColor;
        uniform float uFogStart;
        uniform float uFogEnd;
        uniform float uFogDensity;

        uniform bool uAlphaTest;

        // ---- modern rendering
        uniform bool uModern;
        uniform float uGlow;
        uniform bool uUseAo;
        uniform sampler2D uAoTex;
        uniform vec2 uViewport;
        uniform float uAoDirect;             // share of the occlusion applied to direct light
        uniform int uShadowSlot[8];          // per light: -1 none, else 0..3
        uniform samplerCubeShadow uShadow0;
        uniform samplerCubeShadow uShadow1;
        uniform samplerCubeShadow uShadow2;
        uniform samplerCubeShadow uShadow3;
        uniform float uShadowFar[4];
        uniform float uShadowSize;           // texels per cube face
        uniform float uShadowSoftness;
        uniform vec3 uShadowLightPos;        // pass 3: the light the depth is measured from
        uniform float uShadowLightFar;       // pass 3
        uniform bool uEnvOn;
        uniform samplerCube uEnvMap;
        uniform float uReflect;
        uniform float uIor;

        const vec2 Disk[12] = vec2[](
            vec2(-0.326, -0.406), vec2(-0.840, -0.074), vec2(-0.696, 0.457), vec2(-0.203, 0.621),
            vec2(0.962, -0.195), vec2(0.473, -0.480), vec2(0.519, 0.767), vec2(0.185, -0.893),
            vec2(0.507, 0.064), vec2(0.896, 0.412), vec2(-0.322, -0.933), vec2(-0.792, -0.598));

        vec2 texCoord()
        {
            if (!uSphereMap) return vUV;
            vec3 r = reflect(normalize(vEyePos), normalize(vEyeNormal));
            float m = 2.0 * sqrt(r.x * r.x + r.y * r.y + (r.z + 1.0) * (r.z + 1.0));
            return r.xy / max(m, 1e-6) + 0.5;
        }

        float shadowTap(int slot, vec4 c)
        {
            if (slot == 0) return texture(uShadow0, c);
            if (slot == 1) return texture(uShadow1, c);
            if (slot == 2) return texture(uShadow2, c);
            return texture(uShadow3, c);
        }

        // fraction of the light reaching this point (soft edge from 12 taps around the direction)
        float shadow(int slot, vec3 lightPos, vec3 N)
        {
            float far = uShadowFar[slot];
            vec3 d = vWorldPos - lightPos;
            float dist = length(d);
            if (dist >= far || dist < 1e-4) return 1.0;
            float texel = 2.0 * dist / uShadowSize;               // size of a shadow-map texel here
            vec3 Nl = dot(N, d) <= 0.0 ? N : -N;                   // the side facing the light
            vec3 p = vWorldPos + Nl * texel * 1.5;                 // normal offset against acne
            vec3 dir = p - lightPos;
            float dd = length(dir);
            vec3 w = dir / dd;
            float ref = (dd - texel) / far;
            vec3 t = normalize(cross(w, abs(w.y) < 0.99 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0)));
            vec3 b = cross(w, t);
            float r = max(uShadowSoftness, 0.0) * 2.5 * texel / dd;
            if (r <= 0.0) return shadowTap(slot, vec4(w, ref));
            float sum = 0.0;
            for (int k = 0; k < 12; k++)
                sum += shadowTap(slot, vec4(w + (t * Disk[k].x + b * Disk[k].y) * r, ref));
            return sum / 12.0;
        }

        void main()
        {
            if (uPass == 1)
            {
                if (uTextured && uAlphaTest && texture(uTexture, texCoord()).a <= 0.004)
                    discard;
                fragColor = uColor;
                return;
            }
            if (uPass == 2 || uPass == 3)
            {
                if (uTextured && uAlphaTest && texture(uTexture, texCoord()).a <= 0.004)
                    discard;
        #ifdef SHADOW_DEPTH
                // only in the shadow variant: a shader that writes gl_FragDepth anywhere leaves the
                // depth undefined on every path that does not
                gl_FragDepth = clamp(length(vWorldPos - uShadowLightPos) / uShadowLightFar, 0.0, 1.0);
                fragColor = vec4(0.0);
                return;
        #endif
                vec3 n = normalize(vEyeNormal);
                fragColor = vec4(dot(n, vEyePos) > 0.0 ? -n : n, 1.0);   // facing the camera
                return;
            }

            vec4 color;
            vec3 N = normalize(vWorldNormal);
            vec3 V = normalize(uCameraPos - vWorldPos);
            if (uLit)
            {
                float ao = 1.0;
                if (uModern && uUseAo)
                    ao = texture(uAoTex, gl_FragCoord.xy / uViewport).r;
                float aoDirect = mix(1.0, ao, uAoDirect);

                vec3 c = uMatEmission.rgb * (uModern ? uGlow : 1.0) + uGlobalAmbient.rgb * uMatAmbient.rgb * ao;
                for (int i = 0; i < uLightCount; i++)
                {
                    vec3 Lv = uLightPos[i] - vWorldPos;
                    float d = length(Lv);
                    vec3 L = Lv / max(d, 1e-6);
                    vec4 sp = uLightSpot[i];
                    float att = 1.0 / max(sp.z + sp.w * d + uLightQuadratic[i] * d * d, 1e-6);
                    float spot = 1.0;
                    if (sp.x < 179.999)
                    {
                        vec3 dir = uLightSpotDir[i];
                        dir = length(dir) > 1e-6 ? normalize(dir) : vec3(0.0, 0.0, -1.0);
                        float cosAng = dot(-L, dir);
                        spot = cosAng < cos(radians(sp.x)) ? 0.0 : pow(max(cosAng, 0.0), sp.y);
                    }
                    float nDotL = max(dot(N, L), 0.0);
                    vec3 direct = nDotL * uLightDiffuse[i].rgb * uMatDiffuse.rgb;
                    if (nDotL > 0.0)
                    {
                        vec3 H = normalize(L + V);
                        float nDotH = max(dot(N, H), 0.0);
                        direct += (uShininess > 0.0 ? pow(nDotH, uShininess) : 1.0) * uLightSpecular[i].rgb * uMatSpecular.rgb;
                    }
                    float lit = 1.0;
                    if (uModern && uShadowSlot[i] >= 0 && att * spot > 0.0 && nDotL > 0.0)
                        lit = shadow(uShadowSlot[i], uLightPos[i], N);
                    c += att * spot * (uLightAmbient[i].rgb * uMatAmbient.rgb * ao + direct * lit * aoDirect);
                }
                color = vec4(uModern ? max(c, 0.0) : clamp(c, 0.0, 1.0), clamp(uMatDiffuse.a, 0.0, 1.0));
            }
            else
            {
                color = uColor;
            }

            if (uTextured)
                color *= texture(uTexture, texCoord());   // GL_MODULATE

            if (uAlphaTest && color.a <= 0.004)
                discard;

            if (uModern && uEnvOn)
            {
                vec3 I = -V;
                vec3 Nf = dot(N, V) < 0.0 ? -N : N;        // the side facing the camera
                float cosT = clamp(dot(Nf, V), 0.0, 1.0);
                float schlick = pow(1.0 - cosT, 5.0);
                vec3 mirrored = texture(uEnvMap, reflect(I, Nf)).rgb;
                if (uIor > 1.0 && color.a < 0.999)
                {
                    // glass: the see-through part shows the surroundings bent by the surface
                    vec3 bent = refract(I, Nf, 1.0 / uIor);
                    vec3 through = texture(uEnvMap, dot(bent, bent) > 0.0 ? bent : reflect(I, Nf)).rgb
                                 * mix(vec3(1.0), uMatDiffuse.rgb, 0.5);
                    float f0 = (uIor - 1.0) / (uIor + 1.0);
                    f0 *= f0;
                    color.rgb = mix(mix(through, color.rgb, color.a), mirrored, f0 + (1.0 - f0) * schlick);
                    color.a = 1.0;
                }
                if (uReflect > 0.0)
                    color.rgb = mix(color.rgb, mirrored, uReflect + (1.0 - uReflect) * schlick * uReflect);
            }

            if (uFogMode != 0)
            {
                float z = abs(vEyePos.z);
                float f;
                if (uFogMode == 1)
                    f = (uFogEnd - z) / max(uFogEnd - uFogStart, 1e-6);
                else if (uFogMode == 2)
                    f = exp(-uFogDensity * z);
                else
                    f = exp(-(uFogDensity * z) * (uFogDensity * z));
                f = clamp(f, 0.0, 1.0);
                color.rgb = mix(uFogColor.rgb, color.rgb, f);
            }

            fragColor = color;
        }
        """;
}
