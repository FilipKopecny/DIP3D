using System.ComponentModel;

namespace Overbryce.Core.Model;

/// <summary>
/// The animation: an ordered list of key states (original <c>CAnimacia</c>). In-between frames are
/// produced by <see cref="Evaluate"/>, which reproduces the original <c>CAnimacia::Render</c> loop.
/// </summary>
public sealed class Animation
{
    private string _name = "Animacia";

    public Animation(string name = "Animacia")
    {
        Name = name;
    }

    [Category("Animation"), Description("Name of the animation.")]
    public string Name
    {
        get => _name;
        set => _name = Element.SanitizeName(value);
    }

    [Category("Animation"), DisplayName("Frames per second"), Description("Frame rate used for playback and frame export (1..128).")]
    public float Fps { get; set; } = 25;

    [Browsable(false)]
    public List<Scene> Scenes { get; } = [];

    /// <summary>Settings of the modern renderer (new in 2.1; stored after the original format).</summary>
    [Browsable(false)]
    public ModernSettings Modern { get; } = new();

    /// <summary>Extension records this version does not know (from a newer version); written back unchanged.</summary>
    [Browsable(false)]
    public List<string> UnknownExtensions { get; } = [];

    [Category("Animation"), DisplayName("Start time [s]")]
    public float StartTime => Scenes.Count > 0 ? Scenes[0].AniTime : 0;

    [Category("Animation"), DisplayName("End time [s]")]
    public float EndTime => Scenes.Count > 0 ? Scenes[^1].AniTime : 0;

    [Category("Animation"), DisplayName("Key states")]
    public int StateCount => Scenes.Count;

    /// <summary>Creates a new, empty animation with one state (original File/New).</summary>
    public static Animation CreateNew()
    {
        var a = new Animation("Animacia");
        a.Scenes.Add(new Scene("Scena"));
        return a;
    }

    /// <summary>
    /// Original <c>KopirujScenuZa</c>: inserts a copy of <paramref name="source"/> after
    /// <paramref name="after"/>. The copy gets the time half-way to the following state, or
    /// 5 seconds after <paramref name="after"/> when it is the last one.
    /// </summary>
    public Scene InsertCopyAfter(Scene source, Scene after)
    {
        int index = Scenes.IndexOf(after);
        if (index < 0) throw new ArgumentException("State is not part of this animation.", nameof(after));
        var copy = source.Clone();
        copy.AniTime = index + 1 < Scenes.Count
            ? (after.AniTime + Scenes[index + 1].AniTime) / 2
            : after.AniTime + 5;
        Scenes.Insert(index + 1, copy);
        return copy;
    }

    /// <summary>
    /// Computes the state of the animation at <paramref name="time"/> (seconds). Returns a fresh
    /// copy that can be rendered or modified freely.
    /// </summary>
    public Scene? Evaluate(float time)
    {
        if (Scenes.Count == 0) return null;
        if (Scenes.Count == 1 || time <= Scenes[0].AniTime) return Scenes[0].Clone();
        if (time >= Scenes[^1].AniTime) return EvaluateEnd();

        // find the segment [i, i+1] containing time (states with equal time are skipped)
        int i = 0;
        while (i < Scenes.Count - 2 && time >= Scenes[i + 1].AniTime) i++;
        var first = Scenes[i];
        var second = Scenes[i + 1];
        float dt = second.AniTime - first.AniTime;
        if (dt <= 0) return second.Clone();

        float u = (time - first.AniTime) / dt;
        return EvaluateSegment(i, u);
    }

    /// <summary>
    /// Original inner loop of <c>CAnimacia::Render</c> for segment <paramref name="index"/>
    /// (between key states index and index+1) at spline parameter <paramref name="u"/> in [0,1).
    /// </summary>
    public Scene EvaluateSegment(int index, float u)
    {
        var first = Scenes[index];
        var second = Scenes[index + 1];
        Scene? prev = index > 0 ? Scenes[index - 1] : null;
        Scene? nextNext = index + 2 < Scenes.Count ? Scenes[index + 2] : null;

        var w = SplineWeights.FromKeyTimes(prev?.AniTime ?? first.AniTime, first.AniTime, second.AniTime,
            nextNext?.AniTime ?? second.AniTime, u);

        var temp = first.Clone();
        // the whole animation starts and ends at rest
        temp.InterpolateState(w.WithEase(prev == null, nextNext == null), prev ?? first, second, nextNext ?? second);

        // FIX: the original drove the permanent motion by the *interpolated* state time, which is not
        // linear in the first and last interval; the real time of the frame is used instead.
        float elapsed = u * (second.AniTime - first.AniTime);
        temp.AniTime = first.AniTime + elapsed;

        CarryPermanentMotion(temp, index);

        foreach (var element in temp.Children)
            GroupElement.InterpolateMatched(element, w, elapsed, prev, second, nextNext);

        return temp;
    }

    /// <summary>
    /// The last key state as the final frame. FIX: a plain copy of the state dropped the permanent
    /// rotation / orbit accumulated over the whole animation, so spinning elements snapped back on the
    /// last frame; the accumulated motion is applied like on every other frame.
    /// </summary>
    private Scene EvaluateEnd()
    {
        var last = Scenes[^1].Clone();
        CarryPermanentMotion(last, Scenes.Count - 1);
        foreach (var element in last.Children)
            element.ApplyCarriedMotion();
        return last;
    }

    /// <summary>
    /// Stores in every element of the in-between copy of state <paramref name="index"/> the permanent
    /// rotation / orbit it performed in the intervals before that state, so that permanent motion
    /// continues smoothly across key states.
    /// </summary>
    private void CarryPermanentMotion(Scene temp, int index)
    {
        if (index == 0) return;
        var earlier = new GroupElement?[index];
        for (int k = 0; k < index; k++) earlier[k] = Scenes[k];
        Carry(temp, earlier, index);
    }

    private void Carry(GroupElement group, GroupElement?[] earlier, int count)
    {
        foreach (var child in group.Children)
        {
            (float X, float Y, float Z) rot = default, orbit = default;
            var childEarlier = new GroupElement?[count];
            for (int k = 0; k < count; k++)
            {
                var match = GroupElement.FindCounterpart(earlier[k], child);
                childEarlier[k] = match as GroupElement;
                if (match == null) continue;
                float dt = Scenes[k + 1].AniTime - Scenes[k].AniTime;
                var (r, o) = match.PermanentMotionOver(dt);
                rot = (rot.X + r.X, rot.Y + r.Y, rot.Z + r.Z);
                orbit = (orbit.X + o.X, orbit.Y + o.Y, orbit.Z + o.Z);
            }
            child.RotationCarry = rot;
            child.OrbitCarry = orbit;
            if (child is GroupElement g)
                Carry(g, childEarlier, count);
        }
    }

    /// <summary>Frame times for exporting/playing the whole animation at <see cref="Fps"/>.</summary>
    public IEnumerable<float> FrameTimes()
    {
        if (Scenes.Count == 0) yield break;
        float fps = Math.Clamp(Fps, 1, 128);
        float start = StartTime, end = EndTime;
        int frames = (int)MathF.Floor((end - start) * fps + 1e-3f);
        for (int f = 0; f <= frames; f++)
            yield return start + f / fps;
    }
}
