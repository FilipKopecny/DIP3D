using System.ComponentModel;
using Overbryce.Core.IO;

namespace Overbryce.Core.Model;

/// <summary>
/// Group of elements sharing a transformation (original <c>CGrupa</c>). Note the original applies
/// the group's rotation <b>before</b> its translation (glRotate x3, then glTranslate), unlike the
/// primitives, so a group's position is expressed in its own rotated frame.
/// </summary>
public class GroupElement : Element
{
    private List<Element> _children = [];

    public GroupElement(string name = "NovaGrupa") : base(name) { }

    public override ElementType Type => ElementType.Group;

    [Browsable(false)]
    public IReadOnlyList<Element> Children => _children;

    public void Add(Element element) => Insert(_children.Count, element);

    public void Insert(int index, Element element)
    {
        element.Parent?.Remove(element);
        _children.Insert(Math.Clamp(index, 0, _children.Count), element);
        element.Parent = this;
    }

    public bool Remove(Element element)
    {
        if (!_children.Remove(element)) return false;
        element.Parent = null;
        return true;
    }

    public int IndexOf(Element element) => _children.IndexOf(element);

    /// <summary>Direct child with the given name, case-insensitive (original <c>NajdiPrvok</c>, stricmp).</summary>
    public Element? FindChild(string name)
    {
        foreach (var e in _children)
            if (string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
                return e;
        return null;
    }

    /// <summary>All descendants, depth first.</summary>
    public IEnumerable<Element> Descendants()
    {
        foreach (var c in _children)
        {
            yield return c;
            if (c is GroupElement g)
                foreach (var d in g.Descendants())
                    yield return d;
        }
    }

    protected override void CloneDeepParts()
    {
        var old = _children;
        _children = new List<Element>(old.Count);
        foreach (var c in old)
        {
            var copy = c.Clone();
            copy.Parent = this;
            _children.Add(copy);
        }
    }

    public override void Resize(float factor)
    {
        if (factor <= 0) return;
        foreach (var c in _children) c.Resize(factor);
    }

    /// <summary>
    /// Original <c>CGrupa::VypocitajMedziprvok</c>: interpolates the group itself and then each child
    /// against the same-named child of the counterpart groups.
    /// FIX: the children's "stop at this state" flag is honoured too (the original ignored it inside groups).
    /// </summary>
    public override void Interpolate(in SplineWeights w, float time, Element prev, Element next, Element nextNext)
    {
        InterpolateCommon(w, time, prev, next, nextNext);

        // a group missing in a neighbouring state is passed in as itself: its children then have no
        // counterpart there either
        var pg = ReferenceEquals(prev, this) ? null : prev as GroupElement;
        var ng = ReferenceEquals(next, this) ? null : next as GroupElement;
        var nng = ReferenceEquals(nextNext, this) ? null : nextNext as GroupElement;
        foreach (var child in _children)
            InterpolateMatched(child, w, time, pg, ng, nng);
    }

    /// <summary>
    /// Interpolates <paramref name="element"/> (part of an in-between copy of the first key state)
    /// against its counterparts in the neighbouring key states (original: the NajdiPrvok look-ups in
    /// CAnimacia::Render). The velocity is zero at a key state where the element is flagged as
    /// "stop at this state" or where it has no counterpart in the state beyond it.
    /// </summary>
    internal static void InterpolateMatched(Element element, in SplineWeights w, float time,
        GroupElement? prev, GroupElement? next, GroupElement? nextNext)
    {
        var p = Counterpart(prev, element);
        var n = Counterpart(next, element);
        // FIX: the original used the element of the *first* state as the 4th control point when the
        // element was missing in the state after next; that bent the motion back towards its start.
        var nn = Counterpart(nextNext, n);
        bool easeStart = element.MotionBoundary || ReferenceEquals(p, element);
        bool easeEnd = n.MotionBoundary || ReferenceEquals(n, element) || ReferenceEquals(nn, n);
        element.Interpolate(w.WithEase(easeStart, easeEnd), time, p, n, nn);
    }

    /// <summary>Finds the element matching <paramref name="self"/> by name and type in <paramref name="group"/>, or returns <paramref name="self"/>.</summary>
    internal static Element Counterpart(GroupElement? group, Element self) => FindCounterpart(group, self) ?? self;

    /// <summary>The direct child of <paramref name="group"/> with the same name and type as <paramref name="self"/>, if any.</summary>
    internal static Element? FindCounterpart(GroupElement? group, Element self)
    {
        var found = group?.FindChild(self.Name);
        return found != null && found.Type == self.Type ? found : null;
    }

    internal override void ReadBody(AniTokenReader r)
    {
        AniFile.ReadChildren(r, this, AniTags.GroupEnd);
    }

    internal override void Write(AniTokenWriter w)
    {
        WriteCommon(w, AniTags.Group);
        foreach (var c in _children) c.Write(w);
        w.Write(AniTags.GroupEnd);
    }
}
