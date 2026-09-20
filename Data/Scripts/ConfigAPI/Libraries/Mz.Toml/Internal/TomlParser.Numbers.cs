using System.Globalization;
using System.Text;

namespace Mz.Toml.Internal
{
    internal sealed partial class TomlParser
    {
        private static bool TryParseTomlNumber(string token, out bool isFloat, out long integerValue, out double floatValue, out bool rangeError)
        {
            isFloat = false;
            integerValue = 0;
            floatValue = 0.0;
            rangeError = false;

            if (string.IsNullOrEmpty(token))
                return false;

            if (token.Length >= 2 && token[0] == '0')
            {
                int numberBase;

                switch (token[1])
                {
                    case 'x':
                        numberBase = 16;
                        break;

                    case 'o':
                        numberBase = 8;
                        break;

                    case 'b':
                        numberBase = 2;
                        break;

                    default:
                        numberBase = 0;
                        break;
                }

                if (numberBase != 0)
                    return TryParseBaseInteger(token, numberBase, out integerValue, out rangeError);
            }

            var index = 0;

            if (token[index] == '+' || token[index] == '-')
            {
                index++;

                if (index >= token.Length)
                    return false;
            }

            var integerStart = index;
            int integerDigits;

            if (!ConsumeDecimalDigits(token, ref index, out integerDigits))
                return false;

            if (token[integerStart] == '0' && integerDigits > 1)
                return false;

            var hasFraction = false;
            var hasExponent = false;

            if (index < token.Length && token[index] == '.')
            {
                hasFraction = true;
                index++;

                int fractionDigits;

                if (!ConsumeDecimalDigits(token, ref index, out fractionDigits))
                    return false;
            }

            if (index < token.Length && (token[index] == 'e' || token[index] == 'E'))
            {
                hasExponent = true;
                index++;

                if (index < token.Length && (token[index] == '+' || token[index] == '-'))
                    index++;

                int exponentDigits;

                if (!ConsumeDecimalDigits(token, ref index, out exponentDigits))
                    return false;
            }

            if (index != token.Length)
                return false;

            var normalized = RemoveNumericUnderscores(token);

            rangeError = true;
            if (hasFraction || hasExponent)
            {
                isFloat = true;

                if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue))
                    return false;
                
                if (double.IsInfinity(floatValue) || double.IsNaN(floatValue))
                    return false;

                rangeError = false;
                return true;
            }

            if (!long.TryParse(normalized, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out integerValue))
                return false;

            rangeError = false;
            return true;
        }

        private static bool TryParseBaseInteger(string token, int numberBase, out long value, out bool rangeError)
        {
            value = 0;
            rangeError = false;

            var index = 2;

            if (index >= token.Length)
                return false;

            var firstDigit = DigitValue(token[index], numberBase);

            if (firstDigit < 0)
                return false;

            ulong accumulated = 0;

            while (index < token.Length)
            {
                var digit = DigitValue(token[index], numberBase);

                if (digit >= 0)
                {
                    var unsignedDigit = (ulong)digit;

                    if (accumulated > (((ulong)long.MaxValue) - unsignedDigit) / (ulong)numberBase)
                    {
                        rangeError = true;
                        return false;
                    }

                    accumulated = accumulated * (ulong)numberBase + unsignedDigit;

                    index++;
                    continue;
                }

                if (token[index] != '_') return false;
                
                if (index == 2 || index + 1 >= token.Length)
                    return false;

                if (DigitValue(token[index - 1], numberBase) < 0 || DigitValue(token[index + 1], numberBase) < 0)
                    return false;
                
                index++;
            }

            value = (long)accumulated;
            return true;
        }

        private static int DigitValue(char c, int numberBase)
        {
            int value;

            if (c >= '0' && c <= '9')
                value = c - '0';
            else if (c >= 'a' && c <= 'f')
                value = 10 + c - 'a';
            else if (c >= 'A' && c <= 'F')
                value = 10 + c - 'A';
            else
                return -1;

            return value < numberBase ? value : -1;
        }

        private static bool ConsumeDecimalDigits(string token, ref int index, out int digitCount)
        {
            digitCount = 0;

            if (index >= token.Length || !IsAsciiDigit(token[index]))
                return false;

            index++;
            digitCount++;

            while (index < token.Length)
            {
                if (IsAsciiDigit(token[index]))
                {
                    index++;
                    digitCount++;
                    continue;
                }

                if (token[index] == '_')
                {
                    if (index + 1 >= token.Length || !IsAsciiDigit(token[index + 1]))
                        return false;

                    index += 2;
                    digitCount++;
                    continue;
                }

                break;
            }

            return true;
        }

        private static string RemoveNumericUnderscores(string token)
        {
            if (token.IndexOf('_') < 0)
                return token;

            var sb = new StringBuilder(token.Length);

            foreach (var t in token)
                if (t != '_')
                    sb.Append(t);

            return sb.ToString();
        }

        private static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';
    }
}
