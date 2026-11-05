using System.Globalization;
using System.Text;

namespace TroitskyToolbox.ModelAnalysis;

/// <summary>A typed parameter such as <c>LENGTH_MEASURE(1.0)</c>, or one part of a complex entity.</summary>
public sealed record StepTyped(string Name, IReadOnlyList<object?> Args);

/// <summary>A reference to another entity instance, written <c>#123</c>.</summary>
public readonly record struct StepRef(int Id);

/// <summary>An enumeration value, written <c>.MILLI.</c>.</summary>
public readonly record struct StepEnum(string Value);

/// <summary>
/// One entity instance from the DATA section. A simple instance has a single part
/// (<c>#1=CARTESIAN_POINT(...)</c>); a complex instance has several
/// (<c>#2=(LENGTH_UNIT() NAMED_UNIT(*) SI_UNIT(.MILLI.,.METRE.))</c>).
/// </summary>
public sealed record StepEntity(int Id, IReadOnlyList<StepTyped> Parts)
{
    public bool Is(string type) => Parts.Any(p => p.Name == type);

    public StepTyped? Part(string type) => Parts.FirstOrDefault(p => p.Name == type);
}

/// <summary>The parsed contents of a STEP Part 21 file.</summary>
public sealed class StepDocument
{
    public required IReadOnlyList<object?> FileName { get; init; }
    public required IReadOnlyList<object?> FileSchema { get; init; }
    public required IReadOnlyDictionary<int, StepEntity> Entities { get; init; }

    public StepEntity? Get(object? value) =>
        value is StepRef r && Entities.TryGetValue(r.Id, out var e) ? e : null;
}

public sealed class StepFormatException(string message) : Exception(message);

/// <summary>
/// A minimal reader for ISO 10303-21 (STEP Part 21) exchange files. It reads the
/// header and the entity instances of the DATA section into a generic tree; it does
/// not interpret geometry. Tessellation for display is done in the browser by
/// occt-import-js.
/// </summary>
public static class StepParser
{
    public const string Magic = "ISO-10303-21;";
    public const string EndMagic = "END-ISO-10303-21;";

    public static StepDocument Parse(string text, CancellationToken ct = default)
    {
        var statements = SplitStatements(StripComments(text), ct);

        IReadOnlyList<object?> fileName = [];
        IReadOnlyList<object?> fileSchema = [];
        var entities = new Dictionary<int, StepEntity>();
        var section = "";

        foreach (var stmt in statements)
        {
            ct.ThrowIfCancellationRequested();
            var s = stmt.Trim();
            if (s.Length == 0) continue;

            switch (s)
            {
                case "ISO-10303-21": continue;
                case "END-ISO-10303-21": section = "END"; continue;
                case "HEADER": section = "HEADER"; continue;
                case "DATA": section = "DATA"; continue;
                case "ENDSEC": section = ""; continue;
            }
            if (s.StartsWith("DATA(", StringComparison.Ordinal)) { section = "DATA"; continue; }

            if (section == "HEADER")
            {
                var typed = new Reader(s).ReadTyped();
                if (typed.Name == "FILE_NAME") fileName = typed.Args;
                else if (typed.Name == "FILE_SCHEMA") fileSchema = typed.Args;
            }
            else if (section == "DATA")
            {
                var eq = s.IndexOf('=');
                if (s[0] != '#' || eq < 0)
                    throw new StepFormatException($"Malformed entity instance: '{Truncate(s)}'");
                if (!int.TryParse(s.AsSpan(1, eq - 1).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                    throw new StepFormatException($"Malformed entity id: '{Truncate(s)}'");

                var reader = new Reader(s[(eq + 1)..]);
                var parts = reader.PeekChar() == '(' ? reader.ReadComplex() : [reader.ReadTyped()];
                entities[id] = new StepEntity(id, parts);
            }
        }

        if (section != "END")
            throw new StepFormatException("The file ends before END-ISO-10303-21; (it may be truncated).");

        return new StepDocument { FileName = fileName, FileSchema = fileSchema, Entities = entities };
    }

    private static string Truncate(string s) => s.Length > 60 ? s[..60] + "…" : s;

    /// <summary>Removes /* ... */ comments that are outside string literals.</summary>
    private static string StripComments(string text)
    {
        var sb = new StringBuilder(text.Length);
        var inString = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                sb.Append(c);
                if (c == '\'') inString = false; // '' escapes re-enter on the next quote
            }
            else if (c == '\'')
            {
                inString = true;
                sb.Append(c);
            }
            else if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0) throw new StepFormatException("Unterminated comment (the file may be truncated).");
                i = end + 1;
            }
            else
            {
                sb.Append(c);
            }
        }
        if (inString) throw new StepFormatException("Unterminated string literal (the file may be truncated).");
        return sb.ToString();
    }

    /// <summary>Splits the file on ';' outside string literals.</summary>
    private static List<string> SplitStatements(string text, CancellationToken ct)
    {
        var result = new List<string>();
        var start = 0;
        var inString = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\'') inString = !inString;
            else if (c == ';' && !inString)
            {
                result.Add(text[start..i]);
                start = i + 1;
                if ((result.Count & 0xFFF) == 0) ct.ThrowIfCancellationRequested();
            }
        }
        if (text[start..].Trim().Length > 0)
            throw new StepFormatException("The file ends in the middle of a statement (it may be truncated).");
        return result;
    }

    /// <summary>Recursive-descent reader for one statement's parameter syntax.</summary>
    private sealed class Reader(string s)
    {
        private int _pos;

        public char PeekChar()
        {
            SkipWs();
            return _pos < s.Length ? s[_pos] : '\0';
        }

        public List<StepTyped> ReadComplex()
        {
            Expect('(');
            var parts = new List<StepTyped>();
            while (PeekChar() != ')') parts.Add(ReadTyped());
            Expect(')');
            return parts;
        }

        public StepTyped ReadTyped()
        {
            var name = ReadKeyword();
            return new StepTyped(name, ReadList());
        }

        private List<object?> ReadList()
        {
            Expect('(');
            var items = new List<object?>();
            if (PeekChar() == ')') { _pos++; return items; }
            while (true)
            {
                items.Add(ReadValue());
                var c = PeekChar();
                _pos++;
                if (c == ')') return items;
                if (c != ',') throw Error("Expected ',' or ')'");
            }
        }

        private object? ReadValue()
        {
            var c = PeekChar();
            switch (c)
            {
                case '$': _pos++; return null;
                case '*': _pos++; return "*";
                case '(': return ReadList();
                case '\'': return ReadString();
                case '#':
                    {
                        _pos++;
                        var start = _pos;
                        while (_pos < s.Length && char.IsAsciiDigit(s[_pos])) _pos++;
                        return new StepRef(int.Parse(s.AsSpan(start, _pos - start), CultureInfo.InvariantCulture));
                    }
                case '.':
                    {
                        var end = s.IndexOf('.', _pos + 1);
                        if (end < 0) throw Error("Unterminated enumeration");
                        var v = s[(_pos + 1)..end];
                        _pos = end + 1;
                        return new StepEnum(v);
                    }
                case '"':
                    {
                        var end = s.IndexOf('"', _pos + 1);
                        if (end < 0) throw Error("Unterminated binary");
                        var v = s[(_pos + 1)..end];
                        _pos = end + 1;
                        return v;
                    }
            }
            if (char.IsAsciiLetter(c) || c == '_') return ReadTyped();
            return ReadNumber();
        }

        private string ReadString()
        {
            _pos++; // opening quote
            var sb = new StringBuilder();
            while (_pos < s.Length)
            {
                var c = s[_pos++];
                if (c == '\'')
                {
                    if (_pos < s.Length && s[_pos] == '\'') { sb.Append('\''); _pos++; }
                    else return sb.ToString();
                }
                else sb.Append(c);
            }
            throw Error("Unterminated string");
        }

        private double ReadNumber()
        {
            var start = _pos;
            while (_pos < s.Length && (char.IsAsciiDigit(s[_pos]) || s[_pos] is '+' or '-' or '.' or 'E' or 'e')) _pos++;
            var token = s[start.._pos];
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw Error($"Invalid number '{token}'");
            return v;
        }

        private string ReadKeyword()
        {
            SkipWs();
            var start = _pos;
            while (_pos < s.Length && (char.IsAsciiLetterOrDigit(s[_pos]) || s[_pos] is '_' or '!')) _pos++;
            if (_pos == start) throw Error("Expected keyword");
            return s[start.._pos];
        }

        private void Expect(char c)
        {
            if (PeekChar() != c) throw Error($"Expected '{c}'");
            _pos++;
        }

        private void SkipWs()
        {
            while (_pos < s.Length && char.IsWhiteSpace(s[_pos])) _pos++;
        }

        private StepFormatException Error(string msg) =>
            new($"{msg} at position {_pos} in '{(s.Length > 60 ? s[..60] + "…" : s)}'");
    }
}
