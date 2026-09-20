using System.Collections.Generic;

namespace Mz.Toml.Internal
{
    internal sealed partial class TomlParser
    {
        private bool ParseTableHeader(out TomlDiagnostic diagnostic)
        {
            diagnostic = null;

            var headerStart = _index;
            var headerLine = _line;
            var headerColumn = _column;

            AdvanceCharacter();

            var isArrayOfTables = !IsEnd && Current == '[';

            if (isArrayOfTables)
                AdvanceCharacter();

            List<TomlKeyPart> parts;

            if (!ParseKeyPath(']', true, out parts, out diagnostic))
                return false;

            AdvanceCharacter();

            if (isArrayOfTables)
            {
                if (IsEnd || Current != ']')
                {
                    diagnostic = Error("Array-of-tables headers must end with two closing brackets.",
                        headerLine, headerColumn, TomlDiagnosticCode.InvalidTable);
                    return false;
                }

                AdvanceCharacter();
            }

            AddSyntaxNode(isArrayOfTables ? TomlSyntaxNodeKind.ArrayTableHeader : TomlSyntaxNodeKind.TableHeader, headerStart, _index);
            SkipSyntaxHorizontalWhitespace(TomlSyntaxTriviaPlacement.Trailing);

            if (!IsEnd && Current == '#' && !SkipSyntaxComment(TomlSyntaxTriviaPlacement.Trailing, out diagnostic)) 
                return false;

            if (!IsEnd)
            {
                if (!IsNewlineStart(Current))
                {
                    diagnostic = Error("Unexpected characters after the table header.",
                        _line, _column, TomlDiagnosticCode.TrailingCharacters);
                    return false;
                }

                if (!ConsumeSyntaxNewline(TomlSyntaxTriviaPlacement.TopLevel, out diagnostic))
                    return false;
            }

            TomlTable table;

            if (isArrayOfTables)
            {
                if (!ResolveArrayTableHeader(parts, out table, out diagnostic))
                    return false;
            }
            else
            {
                if (!ResolveTableHeader(parts, out table, out diagnostic))
                    return false;
            }

            _currentTable = table;
            return true;
        }
    }
}
