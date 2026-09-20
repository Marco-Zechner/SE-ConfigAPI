namespace Mz.Toml.Internal
{
    internal static class TomlTemporalParser
    {
        public static bool LooksTemporal(string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            if (token.Length >= 3 && IsDigit(token[0]) && IsDigit(token[1]) && token[2] == ':')
                return true;

            if (token.Length < 5 || token[4] != '-')
                return false;
            
            return IsDigit(token[0]) && IsDigit(token[1]) && IsDigit(token[2]) && IsDigit(token[3]);
        }

        public static bool TryParse(string token, int line, int column, out TomlValue value)
        {
            value = null;

            if (string.IsNullOrEmpty(token))
                return false;

            if (LooksLikeLocalTime(token))
            {
                TomlLocalTime time;
                var timePosition = 0;

                if (!TryParseTime(token, ref timePosition, out time) || timePosition != token.Length)
                    return false;

                value = new TomlValue(TomlValueKind.LocalTime, time, line, column);
                return true;
            }

            if (!LooksLikeDate(token))
                return false;

            TomlLocalDate date;

            if (!TryParseDate(token, out date))
                return false;

            if (token.Length == 10)
            {
                value = new TomlValue(TomlValueKind.LocalDate, date, line, column);
                return true;
            }

            if (token.Length < 19)
                return false;

            var separator = token[10];

            if (separator != 'T' && separator != 't' && separator != ' ')
                return false;

            var position = 11;
            TomlLocalTime localTime;

            if (!TryParseTime(token, ref position, out localTime))
                return false;

            if (position == token.Length)
            {
                value = new TomlValue(TomlValueKind.LocalDateTime, new TomlLocalDateTime(date, localTime), line, column);
                return true;
            }

            var offsetMarker = token[position];

            if ((offsetMarker == 'Z' || offsetMarker == 'z') && position + 1 == token.Length)
            {
                value = new TomlValue(TomlValueKind.OffsetDateTime, new TomlOffsetDateTime(date, localTime, 0), line, column);
                return true;
            }

            if (offsetMarker != '+' && offsetMarker != '-')
                return false;

            if (position + 6 != token.Length || token[position + 3] != ':')
                return false;

            int offsetHour;
            int offsetMinute;

            if (!ReadTwoDigits(token, position + 1, out offsetHour) || !ReadTwoDigits(token, position + 4, out offsetMinute))
                return false;

            if (offsetHour > 23 || offsetMinute > 59)
                return false;

            var offset = offsetHour * 60 + offsetMinute;

            var isUnknownLocalOffset = offsetMarker == '-' && offsetHour == 0 && offsetMinute == 0;

            if (offsetMarker == '-')
                offset = -offset;

            value = new TomlValue(
                TomlValueKind.OffsetDateTime, 
                new TomlOffsetDateTime(date, localTime, offset, isUnknownLocalOffset), 
                line, column
            );

            return true;
        }

        private static bool LooksLikeLocalTime(string token) =>
            token.Length >= 8 && token[2] == ':' && token[5] == ':' &&
            IsDigit(token[0]) && IsDigit(token[1]) &&
            IsDigit(token[3]) && IsDigit(token[4]);

        private static bool LooksLikeDate(string token) =>
            token.Length >= 10 && token[4] == '-' && token[7] == '-' &&
            IsDigit(token[0]) && IsDigit(token[1]) &&
            IsDigit(token[2]) && IsDigit(token[3]) &&
            IsDigit(token[5]) && IsDigit(token[6]) &&
            IsDigit(token[8]) && IsDigit(token[9]);

        private static bool TryParseDate(string token, out TomlLocalDate date)
        {
            date = null;

            int year;
            int month;
            int day;

            if (!ReadFourDigits(token, 0, out year))
                return false;
            
            if (!ReadTwoDigits(token, 5, out month))
                return false;
            
            if (!ReadTwoDigits(token, 8, out day))
                return false;

            if (!TomlLocalDate.IsValidDate(year, month, day))
                return false;

            date = new TomlLocalDate(year, month, day);
            return true;
        }

        private static bool TryParseTime(string token, ref int position, out TomlLocalTime time)
        {
            time = null;

            if (position + 8 > token.Length)
                return false;

            if (token[position + 2] != ':' || token[position + 5] != ':')
                return false;

            int hour;
            int minute;
            int second;

            if (!ReadTwoDigits(token, position, out hour))
                
                return false;
            if (!ReadTwoDigits(token, position + 3, out minute))
                
                return false;
            if (!ReadTwoDigits(token, position + 6, out second))
                return false;

            if (hour > 23 || minute > 59 || second > 60)
                return false;

            position += 8;

            var fractionalSeconds = string.Empty;

            if (position < token.Length && token[position] == '.')
            {
                position++;

                var fractionStart = position;

                while (position < token.Length && IsDigit(token[position]))
                    position++;

                if (position == fractionStart)
                    return false;

                fractionalSeconds = token.Substring(fractionStart, position - fractionStart);
            }

            time = new TomlLocalTime(hour, minute, second, fractionalSeconds);

            return true;
        }

        private static bool ReadFourDigits(string text, int offset, out int value)
        {
            value = 0;

            if (offset < 0 || offset + 4 > text.Length)
                return false;

            for (var i = 0; i < 4; i++)
            {
                var c = text[offset + i];

                if (!IsDigit(c))
                    return false;

                value = (value * 10) + (c - '0');
            }

            return true;
        }

        private static bool ReadTwoDigits(string text, int offset, out int value)
        {
            value = 0;

            if (offset < 0 || offset + 2 > text.Length)
                return false;

            var first = text[offset];
            var second = text[offset + 1];

            if (!IsDigit(first) || !IsDigit(second))
                return false;

            value = ((first - '0') * 10) + (second - '0');

            return true;
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';
    }
}
