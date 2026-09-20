namespace Mz.Toml.Internal
{
    internal sealed partial class TomlParser
    {
        private bool ParseBareValue(out TomlNode node, out TomlDiagnostic diagnostic)
        {
            node = null;
            diagnostic = null;

            var start = _index;
            var line = _line;
            var column = _column;

            while (!IsEnd)
            {
                if (IsNewlineStart(Current) || Current == '#' || Current == ',' || Current == ']' || Current == '}')
                    break;

                if (IsHorizontalWhitespace(Current))
                {
                    if (ShouldConsumeDateTimeSpace(start))
                    {
                        AdvanceCharacter();
                        continue;
                    }

                    break;
                }

                AdvanceCharacter();
            }

            var token = _text.Substring(start, _index - start);

            switch (token)
            {
                case "true":
                    node = new TomlValue(TomlValueKind.Boolean, true, line, column);
                    return true;
                
                case "false":
                    node = new TomlValue(TomlValueKind.Boolean, false, line, column);
                    return true;
                
                case "inf":
                case "+inf":
                    node = new TomlValue(TomlValueKind.Float, double.PositiveInfinity, line, column);
                    return true;
                
                case "-inf":
                    node = new TomlValue(TomlValueKind.Float, double.NegativeInfinity, line, column);
                    return true;
                
                case "nan":
                case "+nan":
                case "-nan":
                    node = new TomlValue(TomlValueKind.Float, double.NaN, line, column);
                    return true;
            }

            TomlValue temporalValue;

            if (TomlTemporalParser.TryParse(token, line, column, out temporalValue))
            {
                node = temporalValue;
                return true;
            }

            if (TomlTemporalParser.LooksTemporal(token))
            {
                diagnostic = Error($"Malformed TOML date or time value '{token}'.", line, column, TomlDiagnosticCode.InvalidDateTime);
                return false;
            }

            bool isFloat;
            long integerValue;
            double floatValue;
            bool rangeError;

            if (TryParseTomlNumber(token, out isFloat, out integerValue, out floatValue, out rangeError))
            {
                if (isFloat)
                    node = new TomlValue(TomlValueKind.Float, floatValue, line, column);
                else
                    node = new TomlValue(TomlValueKind.Integer, integerValue, line, column);

                return true;
            }

            if (rangeError)
            {
                diagnostic = Error("Numeric value is outside the supported TOML range.",
                    line, column, TomlDiagnosticCode.InvalidNumber);
                return false;
            }

            diagnostic = Error($"Unrecognized or unsupported TOML value '{token}'.",
                line, column, LooksNumeric(token) ? TomlDiagnosticCode.InvalidNumber : TomlDiagnosticCode.InvalidValue);
            return false;
        }

        private bool ShouldConsumeDateTimeSpace(int start)
        {
            if (Current != ' ' || _index != start + 10 || start < 0 || start + 11 >= _text.Length)
                return false;

            if (_text[start + 4] != '-' || _text[start + 7] != '-')
                return false;
            
            if (!IsAsciiDigit(_text[start]) || !IsAsciiDigit(_text[start + 1]) || 
                !IsAsciiDigit(_text[start + 2]) || !IsAsciiDigit(_text[start + 3]))
                return false;

            if (!IsAsciiDigit(_text[start + 5]) || !IsAsciiDigit(_text[start + 6]))
                return false;
            
            if (!IsAsciiDigit(_text[start + 8]) || !IsAsciiDigit(_text[start + 9]))
                return false;

            return IsAsciiDigit(_text[start + 11]);
        }
    }
}
