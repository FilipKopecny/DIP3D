using System.Globalization;
using System.Text;

namespace Overbryce.Core.IO;

/// <summary>Type tags used in the original text file format (proj.h).</summary>
public static class AniTags
{
    public const string Light = "Svetlo";
    public const string Sphere = "Gula";
    public const string Disk = "Disk";
    public const string Cone = "Zrezany_kuzel";
    public const string Mesh = "Mesh";
    public const string Box = "Kvader";
    public const string Rectangle = "Obdlznik";
    public const string Triangle = "Trojuholnik";
    public const string Torus = "Torus";
    public const string Group = "Grupa";
    public const string Scene = "Scena";
    public const string GroupEnd = "Koniec_grupy";
    public const string SceneEnd = "Koniec_sceny";
    public const string Camera = "Kamera";
    public const string Animation = "Animacia";
    public const string AnimationEnd = "KoniecAnimacie";
    public const string NoTexture = "NoTex";

    /// <summary>GLU_FILL / GLU_LINE enum values stored in the files as the draw style.</summary>
    public const int GluFill = 100012;
    public const int GluLine = 100011;
}

/// <summary>Whitespace separated token stream, equivalent of the C++ <c>ifstream &gt;&gt;</c> reads.</summary>
public sealed class AniTokenReader
{
    private readonly string[] _tokens;
    private int _pos;

    public AniTokenReader(string text)
    {
        _tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    public bool AtEnd => _pos >= _tokens.Length;

    public string Next()
    {
        if (_pos >= _tokens.Length)
            throw new AniFormatException("Unexpected end of file.");
        return _tokens[_pos++];
    }

    public float NextFloat()
    {
        string t = Next();
        if (float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            return v;
        return t.ToLowerInvariant() switch
        {
            "inf" or "+inf" => float.PositiveInfinity,
            "-inf" => float.NegativeInfinity,
            "nan" or "-nan" => float.NaN,
            _ => throw new AniFormatException($"Expected a number but found '{t}' (token {_pos}).")
        };
    }

    public int NextInt()
    {
        string t = Next();
        if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
            return v;
        if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            return (int)d;
        throw new AniFormatException($"Expected an integer but found '{t}' (token {_pos}).");
    }

    public bool NextBool() => NextInt() != 0;
}

/// <summary>Writes tokens separated by single spaces, like the original <c>subor &lt;&lt; value &lt;&lt; ' '</c>.</summary>
public sealed class AniTokenWriter
{
    private readonly StringBuilder _sb = new();

    public void Write(string token)
    {
        _sb.Append(token).Append(' ');
    }

    /// <summary>C++ ostream default formatting is %g with 6 significant digits.</summary>
    public void Write(float value) => Write(value.ToString("G6", CultureInfo.InvariantCulture));

    public void Write(double value) => Write(((float)value).ToString("G6", CultureInfo.InvariantCulture));

    public void Write(int value) => Write(value.ToString(CultureInfo.InvariantCulture));

    public void Write(bool value) => Write(value ? 1 : 0);

    public void Write(Rgba c)
    {
        Write(c.R); Write(c.G); Write(c.B); Write(c.A);
    }

    public override string ToString() => _sb.ToString();
}

public sealed class AniFormatException : Exception
{
    public AniFormatException(string message) : base(message) { }
}
