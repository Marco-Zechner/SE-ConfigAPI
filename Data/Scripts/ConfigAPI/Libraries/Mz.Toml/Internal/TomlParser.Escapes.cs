using System.Globalization;
using System.Text;

namespace Mz.Toml.Internal
{
    internal sealed partial class TomlParser
    {
        private bool ParseEscape(StringBuilder sb, out TomlDiagnostic diagnostic)
        {
            diagnostic = null;

            var escapeLine = _line;
            var escapeColumn = _column;

            AdvanceCharacter();

            if (IsEnd || IsNewlineStart(Current))
            {
                diagnostic = Error("String ends immediately after an escape character.",
                    escapeLine, escapeColumn, TomlDiagnosticCode.InvalidEscape);
                return false;
            }

            var escaped = Current;
            AdvanceCharacter();

            switch (escaped)
            {
                case 'b':
                    sb.Append('\b');
                    return true;

                case 't':
                    sb.Append('\t');
                    return true;

                case 'n':
                    sb.Append('\n');
                    return true;

                case 'f':
                    sb.Append('\f');
                    return true;

                case 'r':
                    sb.Append('\r');
                    return true;

                case '"':
                    sb.Append('"');
                    return true;

                case '\\':
                    sb.Append('\\');
                    return true;

                case 'u':
                    return ParseUnicodeEscape(
                        sb,
                        4,
                        escapeLine,
                        escapeColumn,
                        out diagnostic);

                case 'U':
                    return ParseUnicodeEscape(
                        sb,
                        8,
                        escapeLine,
                        escapeColumn,
                        out diagnostic);

                default:
                    diagnostic = Error($"Unknown TOML escape sequence '\\{escaped}'.",
                        escapeLine, escapeColumn, TomlDiagnosticCode.InvalidEscape);
                    return false;
            }
        }

        private bool ParseUnicodeEscape(StringBuilder sb, int digitCount, int escapeLine, int escapeColumn, out TomlDiagnostic diagnostic)
        {
            diagnostic = null;

            if (_index + digitCount > _text.Length)
            {
                diagnostic = Error("Unicode escape does not contain enough hexadecimal digits.",
                    escapeLine, escapeColumn, TomlDiagnosticCode.InvalidEscape);
                return false;
            }

            var start = _index;

            for (var i = 0; i < digitCount; i++)
            {
                if (IsEnd || IsNewlineStart(Current) || !IsHexDigit(Current))
                {
                    diagnostic = Error("Unicode escape contains a non-hexadecimal character.",
                        escapeLine, escapeColumn, TomlDiagnosticCode.InvalidEscape);
                    return false;
                }

                AdvanceCharacter();
            }

            var hex = _text.Substring(start, digitCount);

            uint codePoint;

            if (!uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out codePoint) ||
                codePoint > 0x10FFFFu || (codePoint >= 0xD800u && codePoint <= 0xDFFFu))
            {
                diagnostic = Error("Unicode escape contains an invalid Unicode scalar value.",
                    escapeLine, escapeColumn, TomlDiagnosticCode.InvalidEscape);
                return false;
            }

            sb.Append(char.ConvertFromUtf32((int)codePoint));
            return true;
        }
    }
}
