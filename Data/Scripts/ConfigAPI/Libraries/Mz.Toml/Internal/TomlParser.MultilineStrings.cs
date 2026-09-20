using System.Text;

namespace Mz.Toml.Internal
{
    internal sealed partial class TomlParser
    {
        private bool ParseMultilineBasicStringText(out string value, out TomlDiagnostic diagnostic)
        {
            value = null;
            diagnostic = null;

            var sourceLine = _line;
            var sourceColumn = _column;

            AdvanceCharacter();
            AdvanceCharacter();
            AdvanceCharacter();

            var sb = new StringBuilder();

            if (!IsEnd && IsNewlineStart(Current) && !ConsumeNewline(out diagnostic)) 
                return false;

            while (!IsEnd)
            {
                var c = Current;

                switch (c)
                {
                    case '"':
                        var quoteCount = CountConsecutive('"');

                        if (quoteCount < 3)
                        {
                            for (var i = 0; i < quoteCount; i++)
                            {
                                sb.Append('"');
                                AdvanceCharacter();
                            }

                            continue;
                        }

                        if (quoteCount <= 5)
                        {
                            for (var i = 0; i < quoteCount - 3; i++)
                                sb.Append('"');

                            for (var i = 0; i < quoteCount; i++)
                                AdvanceCharacter();
                        }
                        else
                        {
                            for (var i = 0; i < 3; i++)
                                AdvanceCharacter();
                        }

                        value = sb.ToString();
                        return true;
                    
                    case '\\':
                        bool consumedContinuation;

                        if (!TryConsumeMultilineContinuation(out consumedContinuation, out diagnostic))
                            return false;

                        if (consumedContinuation)
                            continue;

                        if (!ParseEscape(sb, out diagnostic))
                            return false;

                        continue;
                }

                if (IsNewlineStart(c))
                {
                    if (!ConsumeNewline(out diagnostic))
                        return false;

                    sb.Append('\n');
                    continue;
                }

                if ((c < 0x20 && c != '\t') || c == 0x7F)
                {
                    diagnostic = Error("Unescaped control character in TOML multiline basic string.", _line, _column, TomlDiagnosticCode.InvalidString);
                    return false;
                }

                if (!AppendRawStringCharacter(sb, TomlDiagnosticCode.InvalidString, out diagnostic))
                    return false;
            }

            diagnostic = Error("Unterminated TOML multiline basic string.", sourceLine, sourceColumn, TomlDiagnosticCode.InvalidString);
            return false;
        }

        private bool ParseMultilineLiteralStringText(out string value, out TomlDiagnostic diagnostic)
        {
            value = null;
            diagnostic = null;

            var sourceLine = _line;
            var sourceColumn = _column;

            AdvanceCharacter();
            AdvanceCharacter();
            AdvanceCharacter();

            var sb = new StringBuilder();

            if (!IsEnd && IsNewlineStart(Current))
            {
                if (!ConsumeNewline(out diagnostic))
                    return false;
            }

            while (!IsEnd)
            {
                var c = Current;

                if (c == '\'')
                {
                    var quoteCount = CountConsecutive('\'');

                    if (quoteCount < 3)
                    {
                        for (var i = 0; i < quoteCount; i++)
                        {
                            sb.Append('\'');
                            AdvanceCharacter();
                        }

                        continue;
                    }

                    if (quoteCount <= 5)
                    {
                        for (var i = 0; i < quoteCount - 3; i++)
                            sb.Append('\'');

                        for (var i = 0; i < quoteCount; i++)
                            AdvanceCharacter();
                    }
                    else
                    {
                        for (var i = 0; i < 3; i++)
                            AdvanceCharacter();
                    }

                    value = sb.ToString();
                    return true;
                }

                if (IsNewlineStart(c))
                {
                    if (!ConsumeNewline(out diagnostic))
                        return false;

                    sb.Append('\n');
                    continue;
                }

                if ((c < 0x20 && c != '\t') || c == 0x7F)
                {
                    diagnostic = Error("Control character in TOML multiline literal string.", _line, _column, TomlDiagnosticCode.InvalidString);
                    return false;
                }

                if (!AppendRawStringCharacter(sb, TomlDiagnosticCode.InvalidString, out diagnostic))
                    return false;
            }

            diagnostic = Error("Unterminated TOML multiline literal string.", sourceLine, sourceColumn, TomlDiagnosticCode.InvalidString);
            return false;
        }

        private bool TryConsumeMultilineContinuation(out bool consumed, out TomlDiagnostic diagnostic)
        {
            consumed = false;
            diagnostic = null;

            var scan = _index + 1;

            while (scan < _text.Length && IsHorizontalWhitespace(_text[scan]))
                scan++;

            if (scan >= _text.Length || !IsNewlineStart(_text[scan]))
                return true;

            AdvanceCharacter();

            while (!IsEnd && IsHorizontalWhitespace(Current))
                AdvanceCharacter();

            if (!ConsumeNewline(out diagnostic))
                return false;

            while (!IsEnd)
            {
                if (IsHorizontalWhitespace(Current))
                {
                    AdvanceCharacter();
                    continue;
                }

                if (IsNewlineStart(Current))
                {
                    if (!ConsumeNewline(out diagnostic))
                        return false;

                    continue;
                }

                break;
            }

            consumed = true;
            return true;
        }

        private bool AppendRawStringCharacter(StringBuilder sb, TomlDiagnosticCode diagnosticCode, out TomlDiagnostic diagnostic) 
        {
            diagnostic = null;

            var c = Current;

            if (IsHighSurrogate(c))
            {
                if (_index + 1 >= _text.Length || !IsLowSurrogate(_text[_index + 1]))
                {
                    diagnostic = Error("String contains an unpaired UTF-16 surrogate.", _line, _column, diagnosticCode);
                    return false;
                }

                sb.Append(c);
                AdvanceCharacter();

                sb.Append(Current);
                AdvanceCharacter();

                return true;
            }

            if (IsLowSurrogate(c))
            {
                diagnostic = Error("String contains an unpaired UTF-16 surrogate.", _line, _column, diagnosticCode);
                return false;
            }

            sb.Append(c);
            AdvanceCharacter();

            return true;
        }

        private bool IsTripleDelimiter(char delimiter) 
            => _index + 2 < _text.Length && 
               _text[_index] == delimiter && 
               _text[_index + 1] == delimiter && 
               _text[_index + 2] == delimiter;

        private int CountConsecutive(char value)
        {
            var count = 0;

            while (_index + count < _text.Length && _text[_index + count] == value)
            {
                count++;
            }

            return count;
        }

        private static bool IsHighSurrogate(char c) => c >= 0xD800 && c <= 0xDBFF;

        private static bool IsLowSurrogate(char c) => c >= 0xDC00 && c <= 0xDFFF;
    }
}
