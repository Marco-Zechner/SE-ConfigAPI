using System.Text;

namespace Mz.Toml.Internal
{
    internal sealed partial class TomlParser
    {
        private bool ParseBasicStringText(out string value, out TomlDiagnostic diagnostic)
        {
            value = null;
            diagnostic = null;

            var sourceLine = _line;
            var sourceColumn = _column;

            AdvanceCharacter();

            var sb = new StringBuilder();

            while (!IsEnd)
            {
                var c = Current;

                if (c == '"')
                {
                    AdvanceCharacter();
                    value = sb.ToString();
                    return true;
                }

                if (IsNewlineStart(c))
                {
                    diagnostic = Error("Unterminated TOML basic string.", sourceLine, sourceColumn, TomlDiagnosticCode.InvalidString);
                    return false;
                }

                if (c == '\\')
                {
                    if (!ParseEscape(sb, out diagnostic))
                        return false;

                    continue;
                }

                if ((c < 0x20 && c != '\t') || c == 0x7F)
                {
                    diagnostic = Error("Unescaped control character in TOML basic string.", _line, _column, TomlDiagnosticCode.InvalidString);
                    return false;
                }

                if (!AppendRawStringCharacter(sb, TomlDiagnosticCode.InvalidString, out diagnostic))
                    return false;
            }

            diagnostic = Error("Unterminated TOML basic string.", sourceLine, sourceColumn, TomlDiagnosticCode.InvalidString);
            return false;
        }

        private bool ParseLiteralStringText(out string value, out TomlDiagnostic diagnostic)
        {
            value = null;
            diagnostic = null;

            var sourceLine = _line;
            var sourceColumn = _column;

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
                    diagnostic = Error("Unterminated TOML literal string.", sourceLine, sourceColumn, TomlDiagnosticCode.InvalidString);
                    return false;
                }

                if ((c < 0x20 && c != '\t') || c == 0x7F)
                {
                    diagnostic = Error("Control character in TOML literal string.", _line, _column, TomlDiagnosticCode.InvalidString);
                    return false;
                }

                if (!AppendRawStringCharacter(sb, TomlDiagnosticCode.InvalidString, out diagnostic))
                    return false;
            }

            diagnostic = Error("Unterminated TOML literal string.", sourceLine, sourceColumn, TomlDiagnosticCode.InvalidString);
            return false;
        }
    }
}
