using System.Collections.Generic;
using System.Text;

namespace Mz.Toml.Internal
{
    internal sealed partial class TomlParser
    {
        private bool ParseKeyPath(char terminator, bool tableHeader, out List<TomlKeyPart> parts, out TomlDiagnostic diagnostic)
        {
            parts = new List<TomlKeyPart>();
            diagnostic = null;

            SkipTriviaHorizontalWhitespace();

            while (true)
            {
                if (IsEnd || IsNewlineStart(Current) || Current == '#' || Current == '.' || Current == terminator)
                {
                    diagnostic = Error("Expected a TOML key segment.",
                        _line, _column, tableHeader ? TomlDiagnosticCode.InvalidTable : TomlDiagnosticCode.InvalidKey);
                    return false;
                }

                var line = _line;
                var column = _column;
                string key;

                switch (Current)
                {
                    case '"':
                        if (!ParseBasicStringText(out key, out diagnostic))
                            return false;
                        break
                            ;
                    case '\'':
                        if (!ParseLiteralKey(out key, out diagnostic))
                            return false;
                        break;
                    
                    default:
                        var start = _index;

                        while (!IsEnd && IsBareKeyCharacter(Current))
                            AdvanceCharacter();

                        if (_index == start)
                        {
                            diagnostic = Error("Expected a bare or quoted TOML key.", _line, _column, TomlDiagnosticCode.InvalidKey);
                            return false;
                        }

                        key = _text.Substring(start, _index - start);
                        break;
                }

                parts.Add(new TomlKeyPart(key, line, column));

                SkipTriviaHorizontalWhitespace();

                if (IsEnd || IsNewlineStart(Current) || Current == '#')
                {
                    diagnostic = Error(tableHeader ? "Expected ']' after the table name." : "Expected '=' after the key.",
                        _line, _column, tableHeader ? TomlDiagnosticCode.InvalidTable : TomlDiagnosticCode.MissingEquals);
                    return false;
                }

                if (Current == terminator)
                    return true;

                if (Current != '.')
                {
                    diagnostic = Error($"Expected '.' or '{terminator}' after the key segment.",
                        _line, _column, tableHeader ? TomlDiagnosticCode.InvalidTable : TomlDiagnosticCode.InvalidKey);
                    return false;
                }

                AdvanceCharacter();
                SkipTriviaHorizontalWhitespace();

                if (!IsEnd && !IsNewlineStart(Current) && Current != '#' && Current != '.' &&
                    Current != terminator) continue;
                
                diagnostic = Error("Expected a key segment after '.'.",
                    _line, _column, tableHeader ? TomlDiagnosticCode.InvalidTable : TomlDiagnosticCode.InvalidKey);
                return false;
            }
        }

        private bool ParseLiteralKey(out string value, out TomlDiagnostic diagnostic)
        {
            diagnostic = null;

            var startLine = _line;
            var startColumn = _column;

            AdvanceCharacter();

            var sb = new StringBuilder();

            while (!IsEnd)
            {
                var c = Current;

                if (c == '\'')
                {
                    AdvanceCharacter();
                    value = sb.ToString();
                    return true;
                }

                if (IsNewlineStart(c))
                {
                    value = null;
                    diagnostic = Error("Unterminated literal quoted key.", startLine, startColumn, TomlDiagnosticCode.InvalidKey);
                    return false;
                }

                if ((c < 0x20 && c != '\t') || c == 0x7F)
                {
                    value = null;
                    diagnostic = Error("Control character in literal quoted key.", _line, _column, TomlDiagnosticCode.InvalidKey);
                    return false;
                }

                if (!AppendRawStringCharacter(sb, TomlDiagnosticCode.InvalidKey, out diagnostic))
                {
                    value = null;
                    return false;
                }
            }

            value = null;
            diagnostic = Error("Unterminated literal quoted key.", startLine, startColumn, TomlDiagnosticCode.InvalidKey);
            return false;
        }
    }
}
