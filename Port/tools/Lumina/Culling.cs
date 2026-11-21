using System.Numerics;
using Overbryce.Core.Model;
using static Lumina.Kit;

namespace Lumina;

/// <summary>
/// Leaves scenery out of the key states in which no camera can see it. Overbryce draws every element
/// of the current key state, so the flora of the whole moon in every state would make the file large
/// and the playback slow; the cameras show only a part of it at a time.
/// </summary>
internal static class Culling
{
    /// <summary>What the active camera sees at animation time <paramref name="Time"/>.</summary>
    /// <param name="FogEnd">Distance beyond which the fog hides everything (0 = no fog).</param>
    /// <param name="Moon">True when the moon (radius about <see cref="Kit.Ground"/>) hides what lies beyond its horizon.</param>
    /// <param name="Subjects">The points the shot is about: nothing new may grow between them and the camera.</param>
    internal sealed record Shot(float Time, Vector3 Eye, Vector3 Forward, float Fov, float FogEnd, bool Moon, bool NewWorld, List<Vector3> Subjects);

    /// <summary>The widest picture the film is rendered in.</summary>
    private const float Aspect = 16f / 9f;

    private const float Margin = 6 * Deg;

    /// <summary>Radius of the sphere that stands for the moon when testing what its horizon hides.</summary>
    private const float Occluder = Ground - 0.3f;

    public static bool Sees(Shot s, Vector3 center, float radius)
    {
        var d = center - s.Eye;
        float dist = d.Length();
        if (dist <= radius + 0.5f) return true;
        if (s.FogEnd > 0 && dist - radius > s.FogEnd) return false;
        if (dist - radius > 100) return false;

        float half = MathF.Atan(MathF.Tan(s.Fov * Deg / 2) * MathF.Sqrt(1 + Aspect * Aspect)) + Margin;
        float angle = MathF.Acos(Math.Clamp(Vector3.Dot(s.Forward, d / dist), -1, 1));
        if (angle - MathF.Asin(Math.Clamp(radius / dist, 0, 1)) > half) return false;

        return !s.Moon || !Hidden(s.Eye, center, radius);
    }

    /// <summary>True when the whole bounding sphere lies behind the moon's horizon (tested at seven points of it).</summary>
    private static bool Hidden(Vector3 eye, Vector3 c, float r)
    {
        if (!Blocked(eye, c)) return false;
        Span<Vector3> axes = [Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ];
        foreach (var a in axes)
            if (!Blocked(eye, c + a * r) || !Blocked(eye, c - a * r)) return false;
        return true;
    }

    private static bool Blocked(Vector3 eye, Vector3 p)
    {
        if (p.Length() < Occluder) return true;
        var d = p - eye;
        float u = Math.Clamp(-Vector3.Dot(eye, d) / d.LengthSquared(), 0, 1);
        return (eye + d * u).Length() < Occluder;
    }

    /// <summary>
    /// Removes from every key state the registered scenery that none of the shots between the previous
    /// and the next key state sees. Returns how many elements were left out.
    /// </summary>
    public static int Apply(IList<Scene> states, IReadOnlyList<Shot> shots, IReadOnlyDictionary<string, (Vector3 Center, float Radius)> cullable)
    {
        int removed = 0;
        for (int k = 0; k < states.Count; k++)
        {
            float t0 = states[k].AniTime - 1e-3f, t1 = states[Math.Min(k + 1, states.Count - 1)].AniTime + 1e-3f;
            var relevant = shots.Where(s => s.Time >= t0 && s.Time <= t1).ToList();
            foreach (var child in states[k].Children.ToList())
            {
                if (!cullable.TryGetValue(child.Name, out var b)) continue;
                if (relevant.Any(s => Sees(s, b.Center, b.Radius))) continue;
                states[k].Remove(child);
                removed++;
            }
        }
        return removed;
    }
}

