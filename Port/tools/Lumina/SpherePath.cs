using System.Numerics;

namespace Lumina;

/// <summary>
/// A smooth path on the unit sphere through waypoints (Catmull-Rom, sampled finely and projected back onto
/// the sphere), measured in units on the ground (<see cref="Kit.Ground"/>).
/// </summary>
internal sealed class SpherePath
{
    private readonly Vector3[] line;
    private readonly float[] dist;

    public SpherePath(IReadOnlyList<Vector3> waypoints)
    {
        var pts = waypoints.Select(Vector3.Normalize).ToList();
        var l = new List<Vector3>();
        for (int k = 0; k < pts.Count - 1; k++)
        {
            var p0 = pts[Math.Max(k - 1, 0)];
            var p1 = pts[k];
            var p2 = pts[k + 1];
            var p3 = pts[Math.Min(k + 2, pts.Count - 1)];
            for (int s = 0; s < 60; s++)
            {
                float u = s / 60f;
                var c = 0.5f * (2 * p1 + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (-p0 + 3 * p1 - 3 * p2 + p3) * u * u * u);
                l.Add(Vector3.Normalize(c));
            }
        }
        l.Add(pts[^1]);
        line = [.. l];
        dist = new float[line.Length];
        for (int i = 1; i < line.Length; i++)
            dist[i] = dist[i - 1] + MathF.Acos(Math.Clamp(Vector3.Dot(line[i - 1], line[i]), -1, 1)) * Kit.Ground;
    }

    public float Length => dist[^1];

    /// <summary>The point (unit vector) at distance <paramref name="d"/> along the path, and its direction of travel.</summary>
    public (Vector3 N, Vector3 Dir) At(float d)
    {
        d = Math.Clamp(d, 0, dist[^1] - 1e-3f);
        int i = Array.BinarySearch(dist, d);
        if (i < 0) i = ~i - 1;
        i = Math.Clamp(i, 0, line.Length - 2);
        float u = (d - dist[i]) / MathF.Max(dist[i + 1] - dist[i], 1e-6f);
        var n = Vector3.Normalize(Vector3.Lerp(line[i], line[i + 1], u));
        var dir = line[i + 1] - line[i];
        dir -= Vector3.Dot(dir, n) * n;
        return (n, Vector3.Normalize(dir));
    }

    /// <summary>The point <paramref name="side"/> units to the left of the path at distance <paramref name="d"/>.</summary>
    public Vector3 Beside(float d, float side)
    {
        var (n, dir) = At(d);
        var left = Vector3.Cross(n, dir);
        return Vector3.Normalize(n * Kit.Ground + left * side);
    }

    /// <summary>Distance (units) from <paramref name="n"/> to the path, considering the part between distances <paramref name="from"/> and <paramref name="to"/>.</summary>
    public float DistanceTo(Vector3 n, float from = 0, float to = float.MaxValue)
    {
        float best = float.MaxValue;
        for (int i = 0; i < line.Length; i += 2)
        {
            if (dist[i] < from || dist[i] > to) continue;
            best = MathF.Min(best, MathF.Acos(Math.Clamp(Vector3.Dot(line[i], n), -1, 1)) * Kit.Ground);
        }
        return best;
    }

    /// <summary>Distance along the path of its point nearest to <paramref name="n"/>.</summary>
    public float DistanceAlong(Vector3 n)
    {
        int best = 0;
        for (int i = 1; i < line.Length; i++)
            if (Vector3.Dot(line[i], n) > Vector3.Dot(line[best], n)) best = i;
        return dist[best];
    }
}
