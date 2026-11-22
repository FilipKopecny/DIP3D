using Overbryce.Core.Model;

namespace Overbryce.Core.Geometry;

/// <summary>How the vertices of a <see cref="DrawRange"/> are joined (the OpenGL primitive types the builder uses).</summary>
public enum PrimitiveMode
{
    Triangles,
    TriangleStrip,
    Lines,
    LineStrip,
    LineLoop,
}

/// <summary>One draw call inside the vertex stream.</summary>
public readonly record struct DrawRange(PrimitiveMode Mode, int First, int Count);

/// <summary>
/// CPU tessellation of the original primitives. The OpenGL 1.1 original used GLU quadrics,
/// glBegin/glEnd and evaluators; these builders produce the same vertices (positions, normals and
/// texture coordinates follow the Mesa GLU / evaluator conventions) into an interleaved stream
/// <c>pos.xyz, normal.xyz, uv.st</c>. The OpenGL renderers draw it with a core-profile shader and the
/// ray tracer intersects the same triangles, so both see exactly the same facets.
/// </summary>
public sealed class GeometryBuilder
{
    public const int FloatsPerVertex = 8;

    private float[] _data = new float[1 << 16];
    private int _floats;

    public int VertexCount => _floats / FloatsPerVertex;
    public ReadOnlySpan<float> Data => _data.AsSpan(0, _floats);

    public void Clear() => _floats = 0;

    private void V(double x, double y, double z, double nx, double ny, double nz, double s, double t)
    {
        if (_floats + FloatsPerVertex > _data.Length)
            Array.Resize(ref _data, _data.Length * 2);
        var d = _data;
        int i = _floats;
        d[i] = (float)x; d[i + 1] = (float)y; d[i + 2] = (float)z;
        d[i + 3] = (float)nx; d[i + 4] = (float)ny; d[i + 5] = (float)nz;
        d[i + 6] = (float)s; d[i + 7] = (float)t;
        _floats += FloatsPerVertex;
    }

    /// <summary>The geometry of a shape element as the original drew it (nothing for lights, cameras and groups).</summary>
    public void Shape(List<DrawRange> o, Element e, bool lines)
    {
        switch (e)
        {
            case SphereElement s:
                Sphere(o, s.Radius, s.QualityX, s.QualityY, lines);
                break;
            case ConeElement c:
                Cylinder(o, c.BaseRadius, c.TopRadius, c.Height, c.QualityX, c.QualityY, lines);
                break;
            case DiskElement d:
                // CDisk passed polomer1 as the inner and polomer2 as the outer radius to gluPartialDisk
                PartialDisk(o, d.InnerRadius, d.OuterRadius, d.QualityX, d.QualityY, 0, d.SweepAngle, lines);
                break;
            case TorusElement t:
                Torus(o, t.TubeRadius, t.QualityX, t.RingRadius, t.QualityY, lines);
                break;
            case BoxElement b:
                Box(o, b.SizeX, b.SizeY, b.SizeZ, lines);
                break;
            case RectangleElement r:
                Rectangle(o, r.SizeX, r.SizeY, lines);
                break;
            case TriangleElement tr:
                Triangle(o, tr.X1, tr.Y1, tr.X2, tr.Y2, lines);
                break;
            case MeshElement m:
                BezierPatch(o, m.Points, m.CountX, m.CountY, m.QualityX, m.QualityY, lines);
                break;
            case PolyMeshElement p when p.Data != null:
                PolyMesh(o, p.Data, lines);
                break;
        }
    }

    // ---------------------------------------------------------------------------------- polygon mesh

    /// <summary>The triangles of a polygon mesh (three vertices each), or its edges as lines.</summary>
    public void PolyMesh(List<DrawRange> o, PolyMeshData mesh, bool lines)
    {
        var p = mesh.Positions;
        var n = mesh.Normals;
        var uv = mesh.Uvs;
        var indices = lines ? mesh.Edges : mesh.Indices;
        int needed = _floats + indices.Length * FloatsPerVertex;
        if (needed > _data.Length)
        {
            int size = _data.Length;
            while (size < needed) size *= 2;
            Array.Resize(ref _data, size);
        }
        Begin(out int first);
        var d = _data;
        int k = _floats;
        foreach (int v in indices)
        {
            d[k] = p[v * 3]; d[k + 1] = p[v * 3 + 1]; d[k + 2] = p[v * 3 + 2];
            d[k + 3] = n[v * 3]; d[k + 4] = n[v * 3 + 1]; d[k + 5] = n[v * 3 + 2];
            if (uv != null) { d[k + 6] = uv[v * 2]; d[k + 7] = uv[v * 2 + 1]; }
            else { d[k + 6] = 0; d[k + 7] = 0; }
            k += FloatsPerVertex;
        }
        _floats = k;
        End(o, lines ? PrimitiveMode.Lines : PrimitiveMode.Triangles, first);
    }

    /// <summary>
    /// Calls <paramref name="triangle"/> with the vertex indices of every triangle of the filled
    /// <paramref name="ranges"/>, in the winding OpenGL uses (every second strip triangle reversed).
    /// </summary>
    public static void Triangles(IEnumerable<DrawRange> ranges, Action<int, int, int> triangle)
    {
        foreach (var r in ranges)
        {
            if (r.Mode == PrimitiveMode.TriangleStrip)
            {
                for (int i = 0; i + 2 < r.Count; i++)
                {
                    int v = r.First + i;
                    if (i % 2 == 0) triangle(v, v + 1, v + 2);
                    else triangle(v + 1, v, v + 2);
                }
            }
            else if (r.Mode == PrimitiveMode.Triangles)
            {
                for (int i = 0; i + 2 < r.Count; i += 3) triangle(r.First + i, r.First + i + 1, r.First + i + 2);
            }
        }
    }

    private void Begin(out int first) => first = VertexCount;

    private void End(List<DrawRange> output, PrimitiveMode mode, int first)
    {
        int count = VertexCount - first;
        if (count > 0) output.Add(new DrawRange(mode, first, count));
    }

    // ------------------------------------------------------------------------------------ sphere

    /// <summary>gluSphere with texture coordinates (pole on +Z).</summary>
    public void Sphere(List<DrawRange> o, double radius, int slices, int stacks, bool lines)
    {
        slices = Math.Clamp(slices, 2, 512);
        stacks = Math.Clamp(stacks, 2, 512);
        double drho = Math.PI / stacks, dtheta = 2 * Math.PI / slices;

        void P(double rho, double theta, double s, double t)
        {
            double x = -Math.Sin(theta) * Math.Sin(rho), y = Math.Cos(theta) * Math.Sin(rho), z = Math.Cos(rho);
            V(x * radius, y * radius, z * radius, x, y, z, s, t);
        }

        if (!lines)
        {
            double t = 1, ds = 1.0 / slices, dt = 1.0 / stacks;
            for (int i = 0; i < stacks; i++)
            {
                double rho = i * drho;
                Begin(out int first);
                double s = 0;
                for (int j = 0; j <= slices; j++)
                {
                    double theta = j == slices ? 0 : j * dtheta;
                    P(rho, theta, s, t);
                    P(rho + drho, theta, s, t - dt);
                    s += ds;
                }
                End(o, PrimitiveMode.TriangleStrip, first);
                t -= dt;
            }
            return;
        }

        // GLU_LINE: latitude circles and meridians
        for (int i = 1; i < stacks; i++)
        {
            Begin(out int first);
            for (int j = 0; j < slices; j++) P(i * drho, j * dtheta, 0, 0);
            End(o, PrimitiveMode.LineLoop, first);
        }
        for (int j = 0; j < slices; j++)
        {
            Begin(out int first);
            for (int i = 0; i <= stacks; i++) P(i * drho, j * dtheta, 0, 0);
            End(o, PrimitiveMode.LineStrip, first);
        }
    }

    // ---------------------------------------------------------------------------------- cylinder

    /// <summary>gluCylinder: from z = 0 (base radius) to z = height (top radius).</summary>
    public void Cylinder(List<DrawRange> o, double baseRadius, double topRadius, double height, int slices, int stacks, bool lines)
    {
        slices = Math.Clamp(slices, 2, 512);
        stacks = Math.Clamp(stacks, 1, 512);
        double da = 2 * Math.PI / slices;
        double nz = height != 0 ? (baseRadius - topRadius) / height : 0;

        void P(int i, int j, double s, double t)
        {
            double a = i == slices ? 0 : i * da;
            double x = Math.Sin(a), y = Math.Cos(a);
            double r = baseRadius + (topRadius - baseRadius) * j / stacks;
            double z = height * j / stacks;
            V(x * r, y * r, z, x, y, nz, s, t);
        }

        if (!lines)
        {
            for (int j = 0; j < stacks; j++)
            {
                Begin(out int first);
                for (int i = 0; i <= slices; i++)
                {
                    double s = (double)i / slices;
                    P(i, j, s, (double)j / stacks);
                    P(i, j + 1, s, (double)(j + 1) / stacks);
                }
                End(o, PrimitiveMode.TriangleStrip, first);
            }
            return;
        }

        for (int j = 0; j <= stacks; j++)
        {
            Begin(out int first);
            for (int i = 0; i < slices; i++) P(i, j, 0, 0);
            End(o, PrimitiveMode.LineLoop, first);
        }
        Begin(out int f2);
        for (int i = 0; i < slices; i++)
        {
            P(i, 0, 0, 0);
            P(i, stacks, 0, 0);
        }
        End(o, PrimitiveMode.Lines, f2);
    }

    // ---------------------------------------------------------------------------------- disk

    /// <summary>gluPartialDisk in the z = 0 plane; angles in degrees measured from +Y clockwise.</summary>
    public void PartialDisk(List<DrawRange> o, double inner, double outer, int slices, int loops, double startDeg, double sweepDeg, bool lines)
    {
        slices = Math.Clamp(slices, 2, 512);
        loops = Math.Clamp(loops, 1, 512);
        if (sweepDeg < -360) sweepDeg = 360;
        if (sweepDeg > 360) sweepDeg = 360;
        double start = startDeg * Math.PI / 180, sweep = sweepDeg * Math.PI / 180;
        double da = sweep / slices, dr = (outer - inner) / loops;
        double dtc = outer != 0 ? 2 * outer : 1;

        void P(double r, double a)
        {
            double sa = Math.Sin(a), ca = Math.Cos(a);
            V(r * sa, r * ca, 0, 0, 0, 1, 0.5 + sa * r / dtc, 0.5 + ca * r / dtc);
        }

        if (!lines)
        {
            for (int l = 0; l < loops; l++)
            {
                double r1 = inner + l * dr, r2 = r1 + dr;
                Begin(out int first);
                for (int s = 0; s <= slices; s++)
                {
                    double a = start + s * da;
                    P(r2, a);
                    P(r1, a);
                }
                End(o, PrimitiveMode.TriangleStrip, first);
            }
            return;
        }

        bool closed = Math.Abs(sweepDeg) >= 360;
        for (int l = 0; l <= loops; l++)
        {
            double r = inner + l * dr;
            if (r <= 0) continue;
            Begin(out int first);
            int n = closed ? slices : slices + 1;
            for (int s = 0; s < n; s++) P(r, start + s * da);
            End(o, closed ? PrimitiveMode.LineLoop : PrimitiveMode.LineStrip, first);
        }
        Begin(out int f2);
        int spokes = closed ? slices : slices + 1;
        for (int s = 0; s < spokes; s++)
        {
            double a = start + s * da;
            P(inner, a);
            P(outer, a);
        }
        End(o, PrimitiveMode.Lines, f2);
    }

    // ---------------------------------------------------------------------------------- torus

    /// <summary>The original FillTorus (torus.cpp): numc tube sides, numt ring segments.</summary>
    public void Torus(List<DrawRange> o, double rc, int numc, double rt, int numt, bool lines)
    {
        numc = Math.Clamp(numc, 3, 512);
        numt = Math.Clamp(numt, 3, 512);
        const double twopi = 2 * Math.PI;
        for (int i = 0; i < numc; i++)
        {
            Begin(out int first);
            for (int j = 0; j <= numt; j++)
            {
                for (int k = 1; k >= 0; k--)
                {
                    double s = (i + k) % numc + 0.5;
                    double t = j % numt;
                    double cs = Math.Cos(s * twopi / numc), ss = Math.Sin(s * twopi / numc);
                    double ct = Math.Cos(t * twopi / numt), st = Math.Sin(t * twopi / numt);
                    V((rt + rc * cs) * ct, (rt + rc * cs) * st, rc * ss, ct * cs, st * cs, ss, 0, 0);
                }
            }
            End(o, lines ? PrimitiveMode.LineLoop : PrimitiveMode.TriangleStrip, first);
        }
    }

    // ---------------------------------------------------------------------------------- box

    private static readonly double[,,] BoxTex =
    {
        { { 1.1, 1.1 }, { -0.1, 1.1 }, { -0.1, -0.1 }, { 1.1, -0.1 } },
        { { 1.1, 1.1 }, { -0.1, 1.1 }, { -0.1, -0.1 }, { 1.1, -0.1 } },
        { { -0.1, 1.1 }, { 1.1, 1.1 }, { 1.1, -0.1 }, { -0.1, -0.1 } },
        { { 1.1, 1.1 }, { -0.1, 1.1 }, { -0.1, -0.1 }, { 1.1, -0.1 } },
        { { 1.1, 1.1 }, { -0.1, 1.1 }, { -0.1, -0.1 }, { 1.1, -0.1 } },
        { { 1.1, 1.1 }, { -0.1, 1.1 }, { -0.1, -0.1 }, { 1.1, -0.1 } },
    };

    private static readonly double[,] BoxNormals =
    {
        { -1, 0, 0 }, { 0, 1, 0 }, { 1, 0, 0 }, { 0, -1, 0 }, { 0, 0, 1 }, { 0, 0, -1 },
    };

    private static readonly int[,] BoxFaces =
    {
        { 0, 1, 2, 3 }, { 3, 2, 6, 7 }, { 7, 6, 5, 4 }, { 4, 5, 1, 0 }, { 5, 6, 2, 1 }, { 7, 4, 0, 3 },
    };

    /// <summary>The original drawbox (kvader.cpp) with its texture coordinate table.</summary>
    public void Box(List<DrawRange> o, double sx, double sy, double sz, bool lines)
    {
        double x0 = -Math.Abs(sx) / 2, x1 = -x0, y0 = -Math.Abs(sy) / 2, y1 = -y0, z0 = -Math.Abs(sz) / 2, z1 = -z0;
        var v = new double[8, 3];
        for (int i = 0; i < 8; i++)
        {
            v[i, 0] = i < 4 ? x0 : x1;
            v[i, 1] = i is 0 or 1 or 4 or 5 ? y0 : y1;
            v[i, 2] = i is 0 or 3 or 4 or 7 ? z0 : z1;
        }

        void C(int face, int corner)
        {
            int vi = BoxFaces[face, corner];
            V(v[vi, 0], v[vi, 1], v[vi, 2], BoxNormals[face, 0], BoxNormals[face, 1], BoxNormals[face, 2],
                BoxTex[face, corner, 0], BoxTex[face, corner, 1]);
        }

        for (int f = 0; f < 6; f++)
        {
            Begin(out int first);
            if (lines)
            {
                for (int c = 0; c < 4; c++) C(f, c);
                End(o, PrimitiveMode.LineLoop, first);
            }
            else
            {
                C(f, 0); C(f, 1); C(f, 3); C(f, 2); // quad as a strip
                End(o, PrimitiveMode.TriangleStrip, first);
            }
        }
    }

    // ---------------------------------------------------------------------------------- planar

    public void Rectangle(List<DrawRange> o, double sx, double sy, bool lines)
    {
        double px = sx / 2, py = sy / 2;
        Begin(out int first);
        if (lines)
        {
            V(-px, -py, 0, 0, 0, 1, 0, 0);
            V(px, -py, 0, 0, 0, 1, 1, 0);
            V(px, py, 0, 0, 0, 1, 1, 1);
            V(-px, py, 0, 0, 0, 1, 0, 1);
            End(o, PrimitiveMode.LineLoop, first);
        }
        else
        {
            V(-px, -py, 0, 0, 0, 1, 0, 0);
            V(px, -py, 0, 0, 0, 1, 1, 0);
            V(-px, py, 0, 0, 0, 1, 0, 1);
            V(px, py, 0, 0, 0, 1, 1, 1);
            End(o, PrimitiveMode.TriangleStrip, first);
        }
    }

    public void Triangle(List<DrawRange> o, double x1, double y1, double x2, double y2, bool lines)
    {
        Begin(out int first);
        V(0, 0, 0, 0, 0, 1, 0, 0);
        V(x1, y1, 0, 0, 0, 1, 0, 1);
        V(x2, y2, 0, 0, 0, 1, 1, 0);
        End(o, lines ? PrimitiveMode.LineLoop : PrimitiveMode.Triangles, first);
    }

    // ---------------------------------------------------------------------------------- bezier mesh

    /// <summary>
    /// glMap2f(GL_MAP2_VERTEX_3, uorder = countX, vorder = countY) + glEvalMesh2 with
    /// GL_AUTO_NORMAL; texture coordinates follow the original bilinear map, i.e. (s,t) = (v,u).
    /// </summary>
    public void BezierPatch(List<DrawRange> o, float[] points, int countX, int countY, int gridU, int gridV, bool lines)
    {
        gridU = Math.Clamp(gridU, 1, 256);
        gridV = Math.Clamp(gridV, 1, 256);
        int nu = countX - 1, nv = countY - 1;
        if (nu < 1 || nv < 1) return;

        var bu = new double[countX]; var dbu = new double[countX];
        var bv = new double[countY]; var dbv = new double[countY];

        void Eval(double u, double vv)
        {
            Bernstein(nu, u, bu, dbu);
            Bernstein(nv, vv, bv, dbv);
            double px = 0, py = 0, pz = 0, ux = 0, uy = 0, uz = 0, wx = 0, wy = 0, wz = 0;
            for (int j = 0; j < countY; j++)
            for (int i = 0; i < countX; i++)
            {
                int k = (j * countX + i) * 3;
                double cx = points[k], cy = points[k + 1], cz = points[k + 2];
                double b = bu[i] * bv[j], du = dbu[i] * bv[j], dv = bu[i] * dbv[j];
                px += b * cx; py += b * cy; pz += b * cz;
                ux += du * cx; uy += du * cy; uz += du * cz;
                wx += dv * cx; wy += dv * cy; wz += dv * cz;
            }
            // auto normal: dP/du x dP/dv
            double nx = uy * wz - uz * wy, ny = uz * wx - ux * wz, nz = ux * wy - uy * wx;
            V(px, py, pz, nx, ny, nz, vv, u);
        }

        if (!lines)
        {
            for (int j = 0; j < gridV; j++)
            {
                double v0 = (double)j / gridV, v1 = (double)(j + 1) / gridV;
                Begin(out int first);
                for (int i = 0; i <= gridU; i++)
                {
                    double u = (double)i / gridU;
                    Eval(u, v0);
                    Eval(u, v1);
                }
                End(o, PrimitiveMode.TriangleStrip, first);
            }
            return;
        }

        for (int j = 0; j <= gridV; j++)
        {
            Begin(out int first);
            for (int i = 0; i <= gridU; i++) Eval((double)i / gridU, (double)j / gridV);
            End(o, PrimitiveMode.LineStrip, first);
        }
        for (int i = 0; i <= gridU; i++)
        {
            Begin(out int first);
            for (int j = 0; j <= gridV; j++) Eval((double)i / gridU, (double)j / gridV);
            End(o, PrimitiveMode.LineStrip, first);
        }
    }

    /// <summary>Bernstein polynomials B_i^n(t) and their derivatives.</summary>
    private static void Bernstein(int n, double t, double[] b, double[] db)
    {
        // de Casteljau style evaluation of degree n-1 basis for the derivative
        var lower = new double[n + 1];
        Basis(n - 1, t, lower);
        Basis(n, t, b);
        for (int i = 0; i <= n; i++)
        {
            double left = i > 0 ? lower[i - 1] : 0;
            double right = i < n ? lower[i] : 0;
            db[i] = n * (left - right);
        }
    }

    private static void Basis(int n, double t, double[] b)
    {
        Array.Clear(b);
        b[0] = 1;
        double s = 1 - t;
        for (int k = 1; k <= n; k++)
        {
            double saved = 0;
            for (int i = 0; i < k; i++)
            {
                double tmp = b[i];
                b[i] = saved + s * tmp;
                saved = t * tmp;
            }
            b[k] = saved;
        }
    }
}
