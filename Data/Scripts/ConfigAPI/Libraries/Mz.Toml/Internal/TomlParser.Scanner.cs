namespace Mz.Toml.Internal
{
    internal sealed partial class TomlParser
    {
        private bool SkipComment(out TomlDiagnostic diagnostic)
        {
            diagnostic = null;

            while (!IsEnd && !IsNewlineStart(Current))
            {
                var c = Current;

                if ((c < 0x20 && c != '\t') || c == 0x7F)
                {
                    diagnostic = Error("Control characters other than tab are not permitted in TOML comments.",
                        _line, _column, TomlDiagnosticCode.InvalidComment);
                    return false;
                }

                AdvanceCharacter();
            }

            return true;
        }

        private bool ConsumeNewline(out TomlDiagnostic diagnostic)
        {
            diagnostic = null;

            if (IsEnd)
                return true;

            if (Current == '\n')
            {
                _index++;
                _line++;
                _column = 1;
                return true;
            }

            if (Current != '\r')
                return false;

            if (_index + 1 >= _text.Length || _text[_index + 1] != '\n')
            {
                diagnostic = Error("TOML newlines must use LF or CRLF; a lone CR is invalid.",
                    _line, _column, TomlDiagnosticCode.InvalidNewline);
                return false;
            }

            _index += 2;
            _line++;
            _column = 1;
            return true;
        }

        private void AddSyntaxNode(TomlSyntaxNodeKind kind, int start, int end) 
            => AddSyntaxNode(kind, start, end, null);

        private void AddSyntaxNode(TomlSyntaxNodeKind kind, int start, int end, TomlSourceSpan? valueSpan)
        {
            if (end <= start)
                return;

            _syntaxNodes.Add(new TomlSyntaxNode(kind, new TomlSourceSpan(start, end - start), valueSpan));
        }

        private void AddSyntaxTrivia(TomlSyntaxTriviaKind kind, int start, int end, TomlSyntaxTriviaPlacement placement)
        {
            if (end <= start)
                return;

            _syntaxTrivia.Add(new TomlSyntaxTrivia(kind, new TomlSourceSpan(start, end - start), placement));
        }

        private void SkipSyntaxHorizontalWhitespace(TomlSyntaxTriviaPlacement placement)
        {
            var start = _index;
            SkipHorizontalWhitespace();
            AddSyntaxNode(TomlSyntaxNodeKind.Whitespace, start, _index);
            AddSyntaxTrivia(TomlSyntaxTriviaKind.Whitespace, start, _index, placement);
        }

        private bool SkipSyntaxComment(TomlSyntaxTriviaPlacement placement, out TomlDiagnostic diagnostic)
        {
            var start = _index;

            if (!SkipComment(out diagnostic))
                return false;

            AddSyntaxNode(TomlSyntaxNodeKind.Comment, start, _index);
            AddSyntaxTrivia(TomlSyntaxTriviaKind.Comment, start, _index, placement);
            return true;
        }

        private bool ConsumeSyntaxNewline(TomlSyntaxTriviaPlacement placement, out TomlDiagnostic diagnostic)
        {
            var start = _index;

            if (!ConsumeNewline(out diagnostic))
                return false;

            AddSyntaxNode(TomlSyntaxNodeKind.Newline, start, _index);
            AddSyntaxTrivia(TomlSyntaxTriviaKind.Newline, start, _index, placement);
            return true;
        }

        private void SkipTriviaHorizontalWhitespace()
        {
            var start = _index;
            SkipHorizontalWhitespace();
            AddSyntaxTrivia(TomlSyntaxTriviaKind.Whitespace, start, _index, TomlSyntaxTriviaPlacement.WithinStatement);
        }

        private bool SkipTriviaComment(out TomlDiagnostic diagnostic)
        {
            var start = _index;

            if (!SkipComment(out diagnostic))
                return false;

            AddSyntaxTrivia(TomlSyntaxTriviaKind.Comment, start, _index, TomlSyntaxTriviaPlacement.WithinStatement);
            return true;
        }

        private bool ConsumeTriviaNewline(out TomlDiagnostic diagnostic)
        {
            var start = _index;

            if (!ConsumeNewline(out diagnostic))
                return false;

            AddSyntaxTrivia(TomlSyntaxTriviaKind.Newline, start, _index, TomlSyntaxTriviaPlacement.WithinStatement);
            return true;
        }

        private void SkipHorizontalWhitespace()
        {
            while (!IsEnd && IsHorizontalWhitespace(Current))
                AdvanceCharacter();
        }

        private void AdvanceCharacter()
        {
            _index++;
            _column++;
        }

        private bool IsEnd => _index >= _text.Length;

        private char Current => _text[_index];

        private static bool LooksNumeric(string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            return IsDigit(token[0]) || token[0] == '+' || token[0] == '-';
        }

        private static bool IsBareKeyCharacter(char c) =>
            (c >= 'A' && c <= 'Z') ||
            (c >= 'a' && c <= 'z') ||
            (c >= '0' && c <= '9') ||
            c == '_' || c == '-';

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        private static bool IsHexDigit(char c) =>
            (c >= '0' && c <= '9') ||
            (c >= 'A' && c <= 'F') ||
            (c >= 'a' && c <= 'f');

        private static bool IsHorizontalWhitespace(char c) => c == ' ' || c == '\t';

        private static bool IsNewlineStart(char c) => c == '\n' || c == '\r';

        private static TomlDiagnostic Error(string message, int line, int column, TomlDiagnosticCode code) =>
            new TomlDiagnostic(message, line, column, code);

        private TomlParseResult Failure(TomlDiagnostic diagnostic)
        {
            var coveredEnd = 0;
            if (_syntaxNodes.Count != 0)
                coveredEnd = _syntaxNodes[_syntaxNodes.Count - 1].Span.End;

            AddSyntaxNode(TomlSyntaxNodeKind.Unparsed, coveredEnd, _text.Length);

            var syntax = new TomlSyntaxDocument(_text, _syntaxNodes, _syntaxTrivia);

            return new TomlParseResult(null, new[] { diagnostic }, syntax);
        }
    }
}
