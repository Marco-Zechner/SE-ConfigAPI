using System.Collections.Generic;

namespace Mz.Toml.Internal
{
    internal sealed partial class TomlParser
    {
        private bool ParseArray(out TomlNode node, out TomlDiagnostic diagnostic)
        {
            node = null;
            diagnostic = null;

            var line = _line;
            var column = _column;

            AdvanceCharacter();

            var array = new TomlArray(line, column);

            if (!SkipArrayTrivia(out diagnostic))
                return false;

            if (IsEnd)
            {
                diagnostic = Error("Unterminated TOML array.",
                    line, column, TomlDiagnosticCode.InvalidValue);
                return false;
            }

            if (Current == ']')
            {
                AdvanceCharacter();
                node = array;
                return true;
            }

            while (true)
            {
                TomlNode value;

                if (!ParseValue(out value, out diagnostic))
                    return false;

                array.Add(value);

                if (!SkipArrayTrivia(out diagnostic))
                    return false;

                if (IsEnd)
                {
                    diagnostic = Error("Unterminated TOML array.", line, column, TomlDiagnosticCode.InvalidValue);
                    return false;
                }

                if (Current == ']')
                {
                    AdvanceCharacter();
                    node = array;
                    return true;
                }

                if (Current != ',')
                {
                    diagnostic = Error("Expected ',' or ']' after a TOML array element.", _line, _column, TomlDiagnosticCode.InvalidValue);
                    return false;
                }

                AdvanceCharacter();

                if (!SkipArrayTrivia(out diagnostic))
                    return false;

                if (IsEnd)
                {
                    diagnostic = Error("Unterminated TOML array.", line, column, TomlDiagnosticCode.InvalidValue);
                    return false;
                }

                if (Current == ']')
                {
                    AdvanceCharacter();
                    node = array;
                    return true;
                }
            }
        }

        private bool ParseInlineTable(out TomlNode node, out TomlDiagnostic diagnostic)
        {
            node = null;
            diagnostic = null;

            var line = _line;
            var column = _column;

            AdvanceCharacter();
            SkipTriviaHorizontalWhitespace();

            var table = new TomlTable(line, column, TomlTableDefinitionKind.Inline);

            if (IsEnd)
            {
                diagnostic = Error("Unterminated TOML inline table.", line, column, TomlDiagnosticCode.InvalidValue);
                return false;
            }

            if (Current == '}')
            {
                AdvanceCharacter();
                node = table;
                return true;
            }

            if (IsNewlineStart(Current) || Current == '#')
            {
                diagnostic = Error("TOML 1.0 inline tables cannot contain line breaks or comments between entries.", 
                    _line, _column, TomlDiagnosticCode.InvalidValue);
                return false;
            }

            while (true)
            {
                List<TomlKeyPart> parts;

                if (!ParseKeyPath('=', false, out parts, out diagnostic))
                    return false;

                AdvanceCharacter();
                SkipTriviaHorizontalWhitespace();

                if (IsEnd || Current == '}' || Current == ',' || Current == '#' || IsNewlineStart(Current))
                {
                    diagnostic = Error("Expected a value after '=' in the TOML inline table.", _line, _column, TomlDiagnosticCode.MissingValue);
                    return false;
                }

                TomlNode value;

                if (!ParseValue(out value, out diagnostic))
                    return false;

                if (!AssignKeyPath(table, parts, value, out diagnostic))
                    return false;

                SkipTriviaHorizontalWhitespace();

                if (IsEnd)
                {
                    diagnostic = Error("Unterminated TOML inline table.", line, column, TomlDiagnosticCode.InvalidValue);
                    return false;
                }

                if (Current == '}')
                {
                    AdvanceCharacter();
                    node = table;
                    return true;
                }

                if (Current != ',')
                {
                    diagnostic = Error("Expected ',' or '}' after a TOML inline-table entry.", _line, _column, TomlDiagnosticCode.InvalidValue);
                    return false;
                }

                AdvanceCharacter();
                SkipTriviaHorizontalWhitespace();

                if (IsEnd)
                {
                    diagnostic = Error("Unterminated TOML inline table.", line, column, TomlDiagnosticCode.InvalidValue);
                    return false;
                }

                if (Current == '}')
                {
                    diagnostic = Error("TOML 1.0 inline tables do not permit a trailing comma.", _line, _column, TomlDiagnosticCode.InvalidValue);
                    return false;
                }

                if (IsNewlineStart(Current) || Current == '#')
                {
                    diagnostic = Error("TOML 1.0 inline tables cannot contain line breaks or comments between entries.",
                        _line, _column, TomlDiagnosticCode.InvalidValue);
                    return false;
                }
            }
        }

        private bool SkipArrayTrivia(out TomlDiagnostic diagnostic)
        {
            diagnostic = null;

            while (!IsEnd)
            {
                SkipTriviaHorizontalWhitespace();

                if (IsEnd)
                    return true;

                if (Current == '#')
                {
                    if (!SkipTriviaComment(out diagnostic))
                        return false;

                    if (IsEnd)
                        return true;

                    if (!ConsumeTriviaNewline(out diagnostic))
                        return false;

                    continue;
                }

                if (!IsNewlineStart(Current))
                    return true;

                if (!ConsumeTriviaNewline(out diagnostic))
                    return false;
            }

            return true;
        }
    }
}
