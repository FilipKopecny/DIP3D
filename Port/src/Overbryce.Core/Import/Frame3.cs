using System.Numerics;

namespace Overbryce.Core.Import;

/// <summary>
/// A 3 × 3 matrix in double precision, in the column-vector convention of Blender and OpenGL (p' = M·p), stored
/// row by row. Only what the import needs: products, the polar decomposition and the Euler angles of Overbryce.
/// </summary>
public readonly struct Mat3
{
    public readonly double M00, M01, M02, M10, M11, M12, M20, M21, M22;

    public Mat3(double m00, double m01, double m02, double m10, double m11, double m12, double m20, double m21, double m22)
    {
        M00 = m00; M01 = m01; M02 = m02;
        M10 = m10; M11 = m11; M12 = m12;
        M20 = m20; M21 = m21; M22 = m22;
    }

    public static Mat3 Identity => new(1, 0, 0, 0, 1, 0, 0, 0, 1);

    public static Mat3 Diagonal(double x, double y, double z) => new(x, 0, 0, 0, y, 0, 0, 0, z);

    public double this[int r, int c] => (r * 3 + c) switch
    {
        0 => M00, 1 => M01, 2 => M02, 3 => M10, 4 => M11, 5 => M12, 6 => M20, 7 => M21, _ => M22,
    };

    public static Mat3 operator *(Mat3 a, Mat3 b) => new(
        a.M00 * b.M00 + a.M01 * b.M10 + a.M02 * b.M20, a.M00 * b.M01 + a.M01 * b.M11 + a.M02 * b.M21, a.M00 * b.M02 + a.M01 * b.M12 + a.M02 * b.M22,
        a.M10 * b.M00 + a.M11 * b.M10 + a.M12 * b.M20, a.M10 * b.M01 + a.M11 * b.M11 + a.M12 * b.M21, a.M10 * b.M02 + a.M11 * b.M12 + a.M12 * b.M22,
        a.M20 * b.M00 + a.M21 * b.M10 + a.M22 * b.M20, a.M20 * b.M01 + a.M21 * b.M11 + a.M22 * b.M21, a.M20 * b.M02 + a.M21 * b.M12 + a.M22 * b.M22);

    public static Vec3d operator *(Mat3 m, Vec3d v) => new(
        m.M00 * v.X + m.M01 * v.Y + m.M02 * v.Z,
        m.M10 * v.X + m.M11 * v.Y + m.M12 * v.Z,
        m.M20 * v.X + m.M21 * v.Y + m.M22 * v.Z);

    public static Mat3 operator +(Mat3 a, Mat3 b) => new(a.M00 + b.M00, a.M01 + b.M01, a.M02 + b.M02, a.M10 + b.M10, a.M11 + b.M11,
        a.M12 + b.M12, a.M20 + b.M20, a.M21 + b.M21, a.M22 + b.M22);

    public static Mat3 operator *(double s, Mat3 a) => new(s * a.M00, s * a.M01, s * a.M02, s * a.M10, s * a.M11, s * a.M12, s * a.M20,
        s * a.M21, s * a.M22);

    public Mat3 Transposed => new(M00, M10, M20, M01, M11, M21, M02, M12, M22);

    public double Determinant => M00 * (M11 * M22 - M12 * M21) - M01 * (M10 * M22 - M12 * M20) + M02 * (M10 * M21 - M11 * M20);

    public bool TryInvert(out Mat3 inverse)
    {
        double det = Determinant;
        if (!(Math.Abs(det) > 1e-300) || !double.IsFinite(det))
        {
            inverse = Identity;
            return false;
        }
        double k = 1 / det;
        inverse = new Mat3(
            (M11 * M22 - M12 * M21) * k, (M02 * M21 - M01 * M22) * k, (M01 * M12 - M02 * M11) * k,
            (M12 * M20 - M10 * M22) * k, (M00 * M22 - M02 * M20) * k, (M02 * M10 - M00 * M12) * k,
            (M10 * M21 - M11 * M20) * k, (M01 * M20 - M00 * M21) * k, (M00 * M11 - M01 * M10) * k);
        return true;
    }

    /// <summary>The largest absolute difference between two matrices.</summary>
    public static double Distance(Mat3 a, Mat3 b)
    {
        double d = 0;
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                d = Math.Max(d, Math.Abs(a[r, c] - b[r, c]));
        return d;
    }

    /// <summary>
    /// Splits the matrix into a rotation and a stretch, M = R·S (R a rotation, S symmetric). A mirroring matrix
    /// (negative determinant) keeps the mirror in S: M = R·S with S = R⁻¹·M having a negative determinant.
    /// A degenerate matrix (a scale of 0) gives what is left of it, or no rotation at all.
    /// </summary>
    public (Mat3 Rotation, Mat3 Stretch) Polar()
    {
        bool mirror = Determinant < 0;
        var flip = Diagonal(-1, 1, 1);
        var a = mirror ? this * flip : this;
        var r = a;
        bool ok = false;
        for (int i = 0; i < 60; i++)
        {
            if (!r.TryInvert(out var inv)) break;
            var next = 0.5 * (r + inv.Transposed);
            double change = Distance(next, r);
            r = next;
            if (change < 1e-12) { ok = true; break; }
        }
        if (!ok && Math.Abs(r.Determinant - 1) > 1e-6) r = Orthonormalize(a);
        var stretch = r.Transposed * this;
        return (r, stretch);
    }

    /// <summary>The nearest rotation found by Gram–Schmidt on the columns (for matrices the iteration cannot invert).</summary>
    private static Mat3 Orthonormalize(Mat3 a)
    {
        var x = new Vec3d(a.M00, a.M10, a.M20);
        var y = new Vec3d(a.M01, a.M11, a.M21);
        if (x.Length < 1e-12) x = new Vec3d(1, 0, 0);
        x = x.Normalized;
        y -= x * Vec3d.Dot(x, y);
        if (y.Length < 1e-12) y = Math.Abs(x.X) < 0.9 ? Vec3d.Cross(new Vec3d(1, 0, 0), x) : Vec3d.Cross(new Vec3d(0, 1, 0), x);
        y = y.Normalized;
        var z = Vec3d.Cross(x, y);
        return new Mat3(x.X, y.X, z.X, x.Y, y.Y, z.Y, x.Z, y.Z, z.Z);
    }

    public static Mat3 RotationX(double a) { double c = Math.Cos(a), s = Math.Sin(a); return new(1, 0, 0, 0, c, -s, 0, s, c); }
    public static Mat3 RotationY(double a) { double c = Math.Cos(a), s = Math.Sin(a); return new(c, 0, s, 0, 1, 0, -s, 0, c); }
    public static Mat3 RotationZ(double a) { double c = Math.Cos(a), s = Math.Sin(a); return new(c, -s, 0, s, c, 0, 0, 0, 1); }

    /// <summary>A System.Numerics matrix (row vectors) that applies this matrix, as the Overbryce transforms build them.</summary>
    public Matrix4x4 ToNumerics() => new(
        (float)M00, (float)M10, (float)M20, 0,
        (float)M01, (float)M11, (float)M21, 0,
        (float)M02, (float)M12, (float)M22, 0,
        0, 0, 0, 1);

    // ------------------------------------------------------------------ Euler angles

    /// <summary>
    /// The angles (radians) of an element or group rotation R = Rx(a)·Ry(b)·Rz(c) (glRotate X, then Y, then Z), the
    /// solution nearest to <paramref name="near"/> (unwrapped by whole turns), so the angles of consecutive frames do
    /// not jump.
    /// </summary>
    public Vec3d EulerXyz(Vec3d near)
    {
        double b = Math.Asin(Math.Clamp(M02, -1, 1));
        double a, c;
        if (Math.Abs(M02) < 1 - 1e-9)
        {
            a = Math.Atan2(-M12, M22);
            c = Math.Atan2(-M01, M00);
        }
        else
        {
            // gimbal lock: only a + c (or a - c) is defined; keep c as it was
            c = near.Z;
            var rest = this * RotationZ(-c);
            a = Math.Atan2(rest.M21, rest.M11);
        }
        return Nearest(new Vec3d(a, b, c), new Vec3d(a + Math.PI, Math.PI - b, c + Math.PI), near);
    }

    /// <summary>The angles (radians) of a camera rotation R = Ry(b)·Rx(a)·Rz(c) (yaw, pitch, roll) nearest to <paramref name="near"/>.</summary>
    public Vec3d EulerCamera(Vec3d near)
    {
        double a = Math.Asin(Math.Clamp(-M12, -1, 1));
        double b, c;
        if (Math.Abs(M12) < 1 - 1e-9)
        {
            b = Math.Atan2(M02, M22);
            c = Math.Atan2(M10, M11);
        }
        else
        {
            c = near.Z;
            var rest = this * RotationZ(-c);
            b = Math.Atan2(-rest.M20, rest.M00);
        }
        return Nearest(new Vec3d(a, b, c), new Vec3d(Math.PI - a, b + Math.PI, c + Math.PI), near);
    }

    public static Mat3 FromEulerXyz(Vec3d e) => RotationX(e.X) * RotationY(e.Y) * RotationZ(e.Z);

    public static Mat3 FromEulerCamera(Vec3d e) => RotationY(e.Y) * RotationX(e.X) * RotationZ(e.Z);

    private static Vec3d Nearest(Vec3d first, Vec3d second, Vec3d near)
    {
        var a = Unwrap(first, near);
        var b = Unwrap(second, near);
        return (a - near).LengthSquared <= (b - near).LengthSquared ? a : b;
    }

    private static Vec3d Unwrap(Vec3d v, Vec3d near) => new(Unwrap(v.X, near.X), Unwrap(v.Y, near.Y), Unwrap(v.Z, near.Z));

    /// <summary>The angle plus whole turns that is nearest to <paramref name="near"/>.</summary>
    public static double Unwrap(double angle, double near) => angle + 2 * Math.PI * Math.Round((near - angle) / (2 * Math.PI));
}

/// <summary>A vector in double precision.</summary>
public readonly record struct Vec3d(double X, double Y, double Z)
{
    public static Vec3d Zero => new(0, 0, 0);
    public static Vec3d operator +(Vec3d a, Vec3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3d operator -(Vec3d a, Vec3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3d operator -(Vec3d a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3d operator *(Vec3d a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static double Dot(Vec3d a, Vec3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static Vec3d Cross(Vec3d a, Vec3d b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    public double LengthSquared => X * X + Y * Y + Z * Z;
    public double Length => Math.Sqrt(LengthSquared);
    public Vec3d Normalized => Length > 0 ? this * (1 / Length) : this;
    public double this[int i] => i switch { 0 => X, 1 => Y, _ => Z };
}

/// <summary>A rigid placement: a rotation and a translation (p' = R·p + T).</summary>
public readonly record struct RigidFrame(Mat3 R, Vec3d T)
{
    public static RigidFrame Identity => new(Mat3.Identity, Vec3d.Zero);

    /// <summary>The placement relative to <paramref name="parent"/>: parent⁻¹ · this.</summary>
    public RigidFrame RelativeTo(RigidFrame parent)
    {
        var rt = parent.R.Transposed;
        return new RigidFrame(rt * R, rt * (T - parent.T));
    }

    public RigidFrame Then(RigidFrame local) => new(R * local.R, R * local.T + T);
}
