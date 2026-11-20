namespace Overbryce.Core.Model;

/// <summary>The four predefined 8x8 Bezier control grids of the original (proj.cpp, mesh1Points..mesh4Points).</summary>
public static class MeshPresets
{
    public const int Size = 8;

    private static readonly float[] Xs = [-3.5f, -2.5f, -1.5f, -0.5f, 0.5f, 1.5f, 2.5f, 3.5f];

    // heights (z) per row (y index) and column (x index)
    private static readonly float[,] Z1 =
    {
        { 0, -1, 0, -1, 0, 1, 0, 1 },
        { 1, 0, 1, 0, 0, -1, 0, -1 },
        { 2, 0, 2, 0, -1, 0, -1, 0 },
        { 1, 5, 1, 5, 0, -1, 0, -1 },
        { 4, 0, 4, 0, 3, 4, 3, 4 },
        { -2, 4, -2, 4, 0, -1, 0, -1 },
        { 0, 0, 0, 0, 1, 2, 1, 2 },
        { 0, 0, 0, 0, 1, 2, 1, 2 },
    };

    private static readonly float[,] Z2 =
    {
        { 0, -1, 0, -1, -1, 1, -2, 1 },
        { 1, 0, 1, 1, 0, -1, 0, 3 },
        { 2, 1, 2, 3, 2, 0, 0, 5 },
        { 1, 2, 3, 5, 5, 1, 0, 6 },
        { 3, 1, 4, 5, 6, 4, 3, 6 },
        { 2, 4, 3, 4, 3, 1, 0, 4 },
        { 0, 1, 1, 1, 1, 2, -1, 2 },
        { -1, -2, 0, -2, -1, 1, -1, 0 },
    };

    private static readonly float[,] Z3 =
    {
        { 5, 1, 0, 0, 0, 0, 1, 5 },
        { 1, 0, 1, 2, 2, 1, 0, 1 },
        { 0, 1, 2, 4, 4, 2, 1, 0 },
        { 1, 2, 4, 6, 6, 4, 2, -1 },
        { 1, 2, 4, 6, 7, 4, 2, 0 },
        { 0, 1, 2, 4, 4, 2, 1, -1 },
        { 1, 0, 1, 2, 2, 1, 0, 1 },
        { 4, 1, 0, 0, 0, 0, 1, 5 },
    };

    private static readonly float[,] Z4 =
    {
        { -2, -1, 9, 9, 7, 1, 5, 4 },
        { 4, 3, 1, 3, 0, -5, 0, -1 },
        { 2, 6, 2, 7, -1, 0, -1, 0 },
        { 1, 5, 1, 5, 0, -1, 0, -1 },
        { 4, 0, 4, 0, 3, 4, 3, 4 },
        { -2, 4, -2, -4, 0, -1, 5, -1 },
        { 2, 0, 0, 7, 8, 2, -1, -2 },
        { 0, 8, 4, 4, -1, 4, 1, 2 },
    };

    public static float[] Mesh1 { get; } = Build(Z1);
    public static float[] Mesh2 { get; } = Build(Z2);
    public static float[] Mesh3 { get; } = Build(Z3);
    public static float[] Mesh4 { get; } = Build(Z4);

    public static float[] Get(int index) => index switch
    {
        1 => Mesh2,
        2 => Mesh3,
        3 => Mesh4,
        _ => Mesh1,
    };

    private static float[] Build(float[,] z)
    {
        var pts = new float[Size * Size * 3];
        for (int row = 0; row < Size; row++)
        for (int col = 0; col < Size; col++)
        {
            int i = (row * Size + col) * 3;
            pts[i] = Xs[col];
            pts[i + 1] = Xs[row];
            pts[i + 2] = z[row, col];
        }
        return pts;
    }
}
