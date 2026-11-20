using System.Numerics;

namespace Overbryce.Core.Model;

/// <summary>
/// Reproduces the original fixed-function matrix sequences with <see cref="Matrix4x4"/>.
/// <para>
/// System.Numerics uses row vectors, so the OpenGL call <c>glMultMatrix(X)</c> on the current
/// matrix C (C := C·X) becomes <c>C := X * C</c>. Laid out in memory, the resulting matrix is exactly the
/// column-major OpenGL matrix and can be uploaded to a shader without transposing.
/// </para>
/// </summary>
public static class Transforms
{
    private const float DegToRad = MathF.PI / 180f;

    private static Matrix4x4 Gl(Matrix4x4 current, Matrix4x4 op) => op * current;

    private static Matrix4x4 T(float x, float y, float z) => Matrix4x4.CreateTranslation(x, y, z);
    private static Matrix4x4 Rx(float deg) => Matrix4x4.CreateRotationX(deg * DegToRad);
    private static Matrix4x4 Ry(float deg) => Matrix4x4.CreateRotationY(deg * DegToRad);
    private static Matrix4x4 Rz(float deg) => Matrix4x4.CreateRotationZ(deg * DegToRad);

    /// <summary>
    /// Local matrix of a renderable element or a scene:
    /// glTranslate(x,y,z); glRotate(alfa,X); glRotate(beta,Y); glRotate(gama,Z).
    /// </summary>
    public static Matrix4x4 ElementLocal(Element e)
    {
        var m = T(e.X, e.Y, e.Z);
        m = Gl(m, Rx(e.Alpha));
        m = Gl(m, Ry(e.Beta));
        m = Gl(m, Rz(e.Gamma));
        return m;
    }

    /// <summary>
    /// Local matrix of a group (CGrupa::Render): the rotations come first and the translation last.
    /// </summary>
    public static Matrix4x4 GroupLocal(GroupElement g)
    {
        if (g is Scene) return ElementLocal(g);
        var m = Rx(g.Alpha);
        m = Gl(m, Ry(g.Beta));
        m = Gl(m, Rz(g.Gamma));
        m = Gl(m, T(g.X, g.Y, g.Z));
        return m;
    }

    /// <summary>Matrix that maps the element's local space to world space through all its parents.</summary>
    public static Matrix4x4 ParentChain(Element e)
    {
        var m = Matrix4x4.Identity;
        for (var p = e.Parent; p != null; p = p.Parent)
            m = m * GroupLocal(p); // parent applied after the child: child * parent in row-vector form
        return m;
    }

    /// <summary>
    /// Local matrix of a camera: glTranslate(x,y,z); glRotate(beta,Y); glRotate(alfa,X); glRotate(gama,Z)
    /// (yaw, pitch, roll). This is the exact inverse of the view CKamera::RenderDirect set up for a
    /// camera placed directly in a state, so such cameras look exactly as in the original.
    /// </summary>
    public static Matrix4x4 CameraLocal(Element camera)
    {
        var m = T(camera.X, camera.Y, camera.Z);
        m = Gl(m, Ry(camera.Beta));
        m = Gl(m, Rx(camera.Alpha));
        m = Gl(m, Rz(camera.Gamma));
        return m;
    }

    /// <summary>Local-to-world matrix of any element, including all parent groups.</summary>
    public static Matrix4x4 World(Element e)
    {
        var local = e switch
        {
            CameraElement => CameraLocal(e),
            GroupElement g => GroupLocal(g),
            _ => ElementLocal(e),
        };
        return local * ParentChain(e);
    }

    /// <summary>
    /// View matrix of a camera: the exact inverse of its world matrix.
    /// <para>
    /// FIX: CKamera::RenderDirect undid the parent groups with the camera's own inverse sequence
    /// (rotations Z,X,Y and then the translation), but a group applies its rotations X,Y,Z
    /// <i>before</i> its translation. A camera inside a rotated and moved group therefore did not look
    /// where the group pointed it. For cameras placed directly in a state the result is unchanged.
    /// </para>
    /// </summary>
    public static Matrix4x4 CameraView(Element camera)
    {
        return Matrix4x4.Invert(World(camera), out var view) ? view : Matrix4x4.Identity;
    }

    /// <summary>Right-handed OpenGL style perspective projection (gluPerspective).</summary>
    public static Matrix4x4 Perspective(float fovYDegrees, float aspect, float near, float far)
    {
        float f = 1f / MathF.Tan(fovYDegrees * DegToRad / 2f);
        var m = new Matrix4x4
        {
            M11 = f / aspect,
            M22 = f,
            M33 = (far + near) / (near - far),
            M34 = -1f,
            M43 = 2f * far * near / (near - far),
        };
        return m;
    }
}
