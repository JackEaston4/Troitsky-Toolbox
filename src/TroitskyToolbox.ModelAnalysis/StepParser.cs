using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace TroitskyToolbox.ModelAnalysis;
/// <summary>
/// A minimal reader for ISO 10303-21 (STEP Part 21) exchange files. It reads the
/// header and the entity instances of the DATA section into a generic tree; it does
/// not interpret geometry. Tessellation for display is done in the browser by
/// occt-import-js.
/// </summary>
public static class StepParser
{
    /// <summary>
    /// The line that opens a STEP file
    /// </summary>
    public const string StepOpener = "ISO-10303-21;";
    /// <summary>
    /// The line that closes a STEP file
    /// </summary>
    public const string StepCloser = "END-ISO-10303-21";
    /// <summary>
    /// The line that opens the header section
    /// </summary>
    private const string HeaderOpener = "HEADER";
    /// <summary>
    /// The line that opens the data section
    /// </summary>
    private const string DataOpener = "DATA";
    /// <summary>
    /// The line that closes a section
    /// </summary>
    private const string SectionCloser = "ENDSEC";
    /// <summary>
    /// Check after processing this number of lines for a cancellation request
    /// </summary>
    private const int CancelCheckEvery = 4096;
    /// <summary>
    /// Maximum string length (anything greater is truncated)
    /// </summary>
    private const int MaxStringLength = 60;
    /// <summary>
    /// Display for closer STEP file
    /// </summary>
    public static readonly string StepCloserDisplay = StepCloser;

    enum StepSection { None, Header, Data, End }

    public static StepDocument Parse(string text, CancellationToken ct = default)
    {
       
        List<string> statements = SplitStatements(StripComments(text), ct);

        IReadOnlyList<object?> fileName = [];
        IReadOnlyList<object?> fileSchema = [];
        var entities = new Dictionary<int, StepEntity>();
        var section = StepSection.None;
        

        foreach (string stmt in statements)
        {
            ct.ThrowIfCancellationRequested();
            string s = stmt.Trim();
            if (s.Length == 0) continue;

            switch (s)
            {
                case StepOpener: continue;
                case StepCloser: section = StepSection.End; continue;
                case HeaderOpener: section = StepSection.Header; continue;
                case DataOpener: section = StepSection.Data; continue;
                case SectionCloser: section = StepSection.None; continue;
            }
            if (s.StartsWith("DATA(", StringComparison.Ordinal)) { section = StepSection.Data; continue; }

            if (section == StepSection.Header)
            {
                StepTyped typed = new Reader(s).ReadTyped();
                if (typed.Name == "FILE_NAME") fileName = typed.Args;
                else if (typed.Name == "FILE_SCHEMA") fileSchema = typed.Args;
            }
            else if (section == StepSection.Data)
            {
                int eqIdx = s.IndexOf('=');

                if (s[0] != '#' || eqIdx == -1)
                {
                    throw new StepFormatException($"Malformed entity instance: '{Truncate(s)}'");
                }
                    

                if (!int.TryParse(s.AsSpan(1, eqIdx - 1).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var entityId))
                {
                    throw new StepFormatException($"Malformed entity id: '{Truncate(s)}'");
                }
                    
                var reader = new Reader(s[(eqIdx + 1)..]);
                List<StepTyped> parts = reader.PeekChar() == '(' ? reader.ReadComplex() : [reader.ReadTyped()];
                entities[entityId] = new StepEntity(entityId, parts);
            }
        }

        if (section != StepSection.End)
        {
            throw new StepFormatException($"The file ends before {StepCloserDisplay}; it may be truncated.");
        }
            

        return new StepDocument { FileName = fileName, FileSchema = fileSchema, Entities = entities };
    }

    private static string Truncate(string s) => s.Length > MaxStringLength ? s[..MaxStringLength] + "…" : s;

    /// <summary>Removes /* ... */ comments that are outside string literals.</summary>
    private static string StripComments(string text)
    {
        var sb = new StringBuilder(text.Length);
        var inString = false;
        for (var i = 0; i < text.Length; i++)
        {
            char c = text[i];
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
            // Test for /*, begin of comment
            else if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                int end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
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
            char c = text[i];
            if (c == '\'') inString = !inString;
            else if (c == ';' && !inString)
            {
                result.Add(text[start..i]);
                start = i + 1;
                if ((result.Count % CancelCheckEvery) == 0) ct.ThrowIfCancellationRequested();
            }
        }
        if (text[start..].Trim().Length > 0)
        {
            throw new StepFormatException("The file ends in the middle of a statement (it may be truncated).");
        }
            
        return result;
    }

    /// <summary>Recursive-descent reader for one statement's parameter syntax.</summary>
    private sealed class Reader(string s)
    {
        /// <summary>
        /// Position in string
        /// </summary>
        private int _pos;
        /// <summary>
        /// Non-numeric characters that may be in a valid double
        /// </summary>
        private static readonly char[] OtherDoubleChars = { '+', '-', 'e', 'E', '.' };

        public char PeekChar()
        {
            SkipWhitespace();
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
                char c = PeekChar();
                _pos++;
                if (c == ')') return items;
                if (c != ',')
                {
                    ThrowStepError($"Expected ',' or ')'. Got {c}");
                }
            }
        }

        private object? ReadValue()
        {
            char c = PeekChar();
            switch (c)
            {
                case '$':
                    {
                        _pos++;
                        return null;
                    }
                case '*':
                    {
                        _pos++;
                        return "*";
                    }
                case '(': return ReadList();
                case '\'': return ReadString();
                case '#':
                    {
                        _pos++;
                        int start = _pos;
                        while (_pos < s.Length && char.IsAsciiDigit(s[_pos])) _pos++;
                        return new StepRef(int.Parse(s.AsSpan(start, _pos - start), CultureInfo.InvariantCulture));
                    }
                    
                case '.':
                    {
                        int end = s.IndexOf('.', _pos + 1);
                        if (end == -1) ThrowStepError("Unterminated enumeration");
                        string v = s[(_pos + 1)..end];
                        _pos = end + 1;
                        return new StepUnitEnum(v);
                    } 
                case '"':
                    {
                        int end = s.IndexOf('"', _pos + 1);
                        if (end < 0) ThrowStepError("Unterminated binary");
                        string v = s[(_pos + 1)..end];
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
                char c = s[_pos++];
                if (c == '\'')
                {
                    if (_pos < s.Length && s[_pos] == '\'')
                    { 
                        sb.Append('\'');
                        _pos++;
                    }
                    else return sb.ToString();
                }
                else sb.Append(c);
            }
            ThrowStepError("Unterminated string");
            // Never reaches here
            return string.Empty;
        }

        private double ReadNumber()
        {
            int start = _pos;
            while (_pos < s.Length && (char.IsAsciiDigit(s[_pos]) || OtherDoubleChars.Contains(s[_pos]))) _pos++;
            string token = s[start.._pos];
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            {
                ThrowStepError($"Invalid number '{token}'");
            }  
            return d;
        }

        private string ReadKeyword()
        {
            SkipWhitespace();
            int start = _pos;
            while (_pos < s.Length && (char.IsAsciiLetterOrDigit(s[_pos]) || s[_pos] is '_' or '!')) _pos++;
            if (_pos == start) ThrowStepError("Expected keyword");
            return s[start.._pos];
        }

        private void Expect(char c)
        {
            if (PeekChar() != c) ThrowStepError($"Expected '{c}'");
            _pos++;
        }

        private void SkipWhitespace()
        {
            while (_pos < s.Length && char.IsWhiteSpace(s[_pos])) _pos++;
        }

        [DoesNotReturn]
        private void ThrowStepError(string msg)
        {
            throw new($"{msg} at position {_pos} in '{Truncate(s)}'");
        }
            


    }
}
