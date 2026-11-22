using System.Text;

namespace Overbryce.Core.Import;

/// <summary>What an import changed on the way: what it only approximated, what it left out, and plain facts.</summary>
public sealed class ImportReport
{
    public List<string> Approximated { get; } = [];
    public List<string> Dropped { get; } = [];
    public List<string> Info { get; } = [];

    public void Approximate(string text) { if (!Approximated.Contains(text)) Approximated.Add(text); }
    public void Drop(string text) { if (!Dropped.Contains(text)) Dropped.Add(text); }
    public void Note(string text) => Info.Add(text);

    /// <summary>A note of the Blender script, "kind|text".</summary>
    public void AddScriptNote(string note)
    {
        int bar = note.IndexOf('|');
        string kind = bar > 0 ? note[..bar] : "";
        string text = bar > 0 ? note[(bar + 1)..] : note;
        if (kind == "dropped") Drop(text);
        else if (kind == "approximated") Approximate(text);
        else Note(text);
    }

    public string ToText(string title)
    {
        var sb = new StringBuilder();
        sb.AppendLine(title);
        sb.AppendLine(new string('=', title.Length));
        sb.AppendLine();
        foreach (var line in Info) sb.AppendLine(line);
        Section(sb, "Approximated", Approximated);
        Section(sb, "Left out", Dropped);
        return sb.ToString();
    }

    private static void Section(StringBuilder sb, string name, List<string> lines)
    {
        sb.AppendLine();
        sb.AppendLine($"{name} ({lines.Count})");
        sb.AppendLine(new string('-', name.Length + 3 + lines.Count.ToString().Length));
        if (lines.Count == 0) sb.AppendLine("nothing");
        foreach (var line in lines) sb.AppendLine("- " + line);
    }
}
