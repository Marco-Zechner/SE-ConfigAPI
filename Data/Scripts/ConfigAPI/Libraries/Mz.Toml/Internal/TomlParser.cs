using System;
using System.Collections.Generic;

namespace Mz.Toml.Internal
{
    internal sealed partial class TomlParser
    {
        private readonly string _text;
        private readonly TomlTable _root;
        private readonly List<TomlSyntaxNode> _syntaxNodes;
        private readonly List<TomlSyntaxTrivia> _syntaxTrivia;
        private TomlTable _currentTable;
        private int _index;
        private int _line;
        private int _column;

        private TomlParser(string text)
        {
            _text = text;
            _syntaxNodes = new List<TomlSyntaxNode>();
            _syntaxTrivia = new List<TomlSyntaxTrivia>();
            _index = 0;
            _line = 1;
            _column = 1;

            _root = new TomlTable(1, 1, TomlTableDefinitionKind.Root);
            _currentTable = _root;
        }

        public static TomlParseResult Parse(string text) => new TomlParser(text).ParseDocument();

        private TomlParseResult ParseDocument()
        {
            while (!IsEnd)
            {
                TomlDiagnostic diagnostic;

                if (!SkipDocumentTrivia(out diagnostic))
                    return Failure(diagnostic);

                if (IsEnd)
                    break;

                if (IsDisabledAssignmentStart)
                {
                    if (!ParseDisabledAssignment(out diagnostic))
                        return Failure(diagnostic);
                }
                else if (Current == '[')
                {
                    if (!ParseTableHeader(out diagnostic))
                        return Failure(diagnostic);
                }
                else
                {
                    if (!ParseAssignment(out diagnostic))
                        return Failure(diagnostic);
                }
            }

            var syntax = new TomlSyntaxDocument(_text, _syntaxNodes, _syntaxTrivia);
            return new TomlParseResult(new TomlDocument(_root), Array.Empty<TomlDiagnostic>(), syntax);
        }

        private bool SkipDocumentTrivia(out TomlDiagnostic diagnostic)
        {
            diagnostic = null;

            while (!IsEnd)
            {
                SkipSyntaxHorizontalWhitespace(TomlSyntaxTriviaPlacement.TopLevel);

                if (IsEnd)
                    return true;

                if (Current == '#')
                {
                    if (IsDisabledAssignmentStart)
                        return true;

                    if (!SkipSyntaxComment(TomlSyntaxTriviaPlacement.TopLevel, out diagnostic))
                        return false;

                    if (IsEnd)
                        return true;

                    if (!ConsumeSyntaxNewline(TomlSyntaxTriviaPlacement.TopLevel, out diagnostic))
                        return false;

                    continue;
                }

                if (!IsNewlineStart(Current))
                    return true;

                if (!ConsumeSyntaxNewline(TomlSyntaxTriviaPlacement.TopLevel, out diagnostic))
                    return false;
            }

            return true;
        }

        private bool ParseAssignment(out TomlDiagnostic diagnostic) 
            => ParseAssignmentCore(TomlSyntaxNodeKind.Assignment, true, _index, out diagnostic);

        private bool ParseDisabledAssignment(out TomlDiagnostic diagnostic)
        {
            var assignmentStart = _index;

            AdvanceCharacter();
            AdvanceCharacter();

            if (ParseAssignmentCore(TomlSyntaxNodeKind.DisabledAssignment, false, assignmentStart, out diagnostic))
                return true;

            if (diagnostic == null)
            {
                diagnostic = Error("Invalid disabled TOML assignment.", _line, _column, TomlDiagnosticCode.InvalidDisabledAssignment);
                return false;
            }

            diagnostic = Error(diagnostic.Message, diagnostic.Line, diagnostic.Column, TomlDiagnosticCode.InvalidDisabledAssignment);
            return false;
        }

        private bool ParseAssignmentCore(TomlSyntaxNodeKind syntaxKind, bool assignSemanticValue, int assignmentStart, out TomlDiagnostic diagnostic)
        {
            diagnostic = null;

            List<TomlKeyPart> parts;

            if (!ParseKeyPath('=', false, out parts, out diagnostic))
                return false;

            AdvanceCharacter();
            SkipTriviaHorizontalWhitespace();

            if (IsEnd || Current == '#' || IsNewlineStart(Current))
            {
                diagnostic = Error("Expected a value after '='.", _line, _column, TomlDiagnosticCode.MissingValue);
                return false;
            }

            var valueStart = _index;
            TomlNode value;

            if (!ParseValue(out value, out diagnostic))
                return false;

            var valueSpan = new TomlSourceSpan(valueStart, _index - valueStart);

            AddSyntaxNode(syntaxKind, assignmentStart, _index, valueSpan);
            SkipSyntaxHorizontalWhitespace(TomlSyntaxTriviaPlacement.Trailing);

            if (!IsEnd && Current == '#' && !SkipSyntaxComment(TomlSyntaxTriviaPlacement.Trailing, out diagnostic)) 
                return false;

            if (!IsEnd)
            {
                if (!IsNewlineStart(Current))
                {
                    diagnostic = Error("Unexpected characters after the TOML value.",
                        _line, _column, TomlDiagnosticCode.TrailingCharacters);
                    return false;
                }

                if (!ConsumeSyntaxNewline(TomlSyntaxTriviaPlacement.TopLevel, out diagnostic))
                    return false;
            }

            if (!assignSemanticValue)
                return true;

            return AssignKeyPath(_currentTable, parts, value, out diagnostic);
        }

        private bool IsDisabledAssignmentStart 
            => _index + 1 < _text.Length &&
               _text[_index] == '#' &&
               _text[_index + 1] == '!';
    }
}
