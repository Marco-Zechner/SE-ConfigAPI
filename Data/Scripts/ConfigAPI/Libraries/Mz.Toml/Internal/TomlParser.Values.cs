namespace Mz.Toml.Internal
{
    internal sealed partial class TomlParser
    {
        private bool ParseValue(out TomlNode node, out TomlDiagnostic diagnostic)
        {
            node = null;
            diagnostic = null;

            switch (Current)
            {
                case '"':
                {
                    var line = _line;
                    var column = _column;
                    string text;

                    if (IsTripleDelimiter('"'))
                    {
                        if (!ParseMultilineBasicStringText(out text, out diagnostic))
                            return false;
                    }
                    else
                    {
                        if (!ParseBasicStringText(out text, out diagnostic))
                            return false;
                    }

                    node = new TomlValue(TomlValueKind.String, text, line, column);
                    return true;
                }
                case '\'':
                {
                    var line = _line;
                    var column = _column;
                    string text;

                    if (IsTripleDelimiter('\''))
                    {
                        if (!ParseMultilineLiteralStringText(out text, out diagnostic))
                            return false;
                    }
                    else
                    {
                        if (!ParseLiteralStringText(out text, out diagnostic))
                            return false;
                    }

                    node = new TomlValue(TomlValueKind.String, text, line, column);
                    return true;
                }
                case '[':
                    return ParseArray(out node, out diagnostic);
                case '{':
                    return ParseInlineTable(out node, out diagnostic);
                default:
                    return ParseBareValue(out node, out diagnostic);
            }
        }
    }
}
