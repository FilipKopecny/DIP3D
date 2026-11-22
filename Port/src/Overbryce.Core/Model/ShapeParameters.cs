namespace Overbryce.Core.Model;

/// <summary>
/// The size parameters of a shape, in a fixed order: the values its vertices depend on linearly (radii, sizes,
/// control points). The Blender export turns them into shape keys and the import reads them back.
/// </summary>
public static class ShapeParameters
{
    public static float[] Get(Element e) => e switch
    {
        SphereElement s => [(float)s.Radius],
        ConeElement c => [(float)c.BaseRadius, (float)c.TopRadius, (float)c.Height],
        DiskElement d => [(float)d.InnerRadius, (float)d.OuterRadius],
        BoxElement b => [b.SizeX, b.SizeY, b.SizeZ],
        RectangleElement r => [r.SizeX, r.SizeY],
        TriangleElement t => [t.X1, t.Y1, t.X2, t.Y2],
        TorusElement t => [t.TubeRadius, t.RingRadius],
        MeshElement m => (float[])m.Points.Clone(),
        _ => [],
    };

    public static void Set(Element e, int i, float v)
    {
        switch (e)
        {
            case SphereElement s: s.Radius = v; break;
            case ConeElement c when i == 0: c.BaseRadius = v; break;
            case ConeElement c when i == 1: c.TopRadius = v; break;
            case ConeElement c: c.Height = v; break;
            case DiskElement d when i == 0: d.InnerRadius = v; break;
            case DiskElement d: d.OuterRadius = v; break;
            case BoxElement b when i == 0: b.SizeX = v; break;
            case BoxElement b when i == 1: b.SizeY = v; break;
            case BoxElement b: b.SizeZ = v; break;
            case RectangleElement r when i == 0: r.SizeX = v; break;
            case RectangleElement r: r.SizeY = v; break;
            case TriangleElement t when i == 0: t.X1 = v; break;
            case TriangleElement t when i == 1: t.Y1 = v; break;
            case TriangleElement t when i == 2: t.X2 = v; break;
            case TriangleElement t: t.Y2 = v; break;
            case TorusElement t when i == 0: t.TubeRadius = v; break;
            case TorusElement t: t.RingRadius = v; break;
            case MeshElement m when i < m.Points.Length: m.Points[i] = v; break;
        }
    }
}
