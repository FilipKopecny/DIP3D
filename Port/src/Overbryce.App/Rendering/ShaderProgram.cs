using System.Numerics;
using OpenTK.Graphics.OpenGL4;

namespace Overbryce.App.Rendering;

/// <summary>
/// GLSL 3.30 re-implementation of the OpenGL 1.1 fixed-function pipeline state the original used:
/// up to 8 positional/spot lights with attenuation (local viewer), the material model
/// (emission + ambient + diffuse + Blinn specular), GL_MODULATE texturing, sphere-map texture
/// generation (torus) and linear / exp / exp2 fog.
/// </summary>
internal sealed class ShaderProgram : IDisposable
{
    public const int MaxLights = 8;

    private const string VertexSource = """
        #version 330 core
        layout(location = 0) in vec3 aPos;
        layout(location = 1) in vec3 aNormal;
        layout(location = 2) in vec2 aUV;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProj;

        out vec3 vEyePos;
        out vec3 vEyeNormal;
        out vec2 vUV;

        void main()
        {
            mat4 modelView = uView * uModel;
            vec4 eye = modelView * vec4(aPos, 1.0);
            vEyePos = eye.xyz;
            vEyeNormal = transpose(inverse(mat3(modelView))) * aNormal;   // GL_NORMALIZE is applied in the fragment shader
            vUV = aUV;
            gl_Position = uProj * eye;
        }
        """;

    private const string FragmentSource = """
        #version 330 core
        in vec3 vEyePos;
        in vec3 vEyeNormal;
        in vec2 vUV;
        out vec4 fragColor;

        uniform bool uLit;
        uniform vec4 uColor;                 // unlit (wire frame) colour

        uniform vec4 uMatAmbient;
        uniform vec4 uMatDiffuse;
        uniform vec4 uMatSpecular;
        uniform vec4 uMatEmission;
        uniform float uShininess;
        uniform vec4 uGlobalAmbient;

        uniform int uLightCount;
        uniform vec4 uLightPos[8];           // eye space, w = 1
        uniform vec3 uLightSpotDir[8];       // eye space
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
        uniform bool uPick;                  // object-id pass: output uColor, honour texture transparency

        vec2 texCoord()
        {
            if (!uSphereMap) return vUV;
            vec3 r = reflect(normalize(vEyePos), normalize(vEyeNormal));
            float m = 2.0 * sqrt(r.x * r.x + r.y * r.y + (r.z + 1.0) * (r.z + 1.0));
            return r.xy / max(m, 1e-6) + 0.5;
        }

        void main()
        {
            if (uPick)
            {
                if (uTextured && uAlphaTest && texture(uTexture, texCoord()).a <= 0.004)
                    discard;
                fragColor = uColor;
                return;
            }

            vec4 color;
            if (uLit)
            {
                vec3 N = normalize(vEyeNormal);
                vec3 V = normalize(-vEyePos);
                vec3 c = uMatEmission.rgb + uGlobalAmbient.rgb * uMatAmbient.rgb;
                for (int i = 0; i < uLightCount; i++)
                {
                    vec3 Lv = uLightPos[i].xyz - vEyePos;
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
                    vec3 term = uLightAmbient[i].rgb * uMatAmbient.rgb
                              + nDotL * uLightDiffuse[i].rgb * uMatDiffuse.rgb;
                    if (nDotL > 0.0)
                    {
                        vec3 H = normalize(L + V);
                        float nDotH = max(dot(N, H), 0.0);
                        term += (uShininess > 0.0 ? pow(nDotH, uShininess) : 1.0) * uLightSpecular[i].rgb * uMatSpecular.rgb;
                    }
                    c += att * spot * term;
                }
                color = vec4(clamp(c, 0.0, 1.0), clamp(uMatDiffuse.a, 0.0, 1.0));
            }
            else
            {
                color = uColor;
            }

            if (uTextured)
                color *= texture(uTexture, texCoord());   // GL_MODULATE

            if (uAlphaTest && color.a <= 0.004)
                discard;

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

    private readonly int _program;
    private readonly Dictionary<string, int> _locations = new();

    public ShaderProgram()
    {
        int vs = Compile(ShaderType.VertexShader, VertexSource);
        int fs = Compile(ShaderType.FragmentShader, FragmentSource);
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
    public void Set(string name, Vector3 v) => GL.Uniform3(Loc(name), v.X, v.Y, v.Z);
    public void Set(string name, Vector4 v) => GL.Uniform4(Loc(name), v.X, v.Y, v.Z, v.W);

    public unsafe void Set(string name, Matrix4x4 m)
    {
        // System.Numerics row-vector matrices have the memory layout of column-major GL matrices
        GL.UniformMatrix4(Loc(name), 1, false, &m.M11);
    }

    public void Dispose() => GL.DeleteProgram(_program);
}
