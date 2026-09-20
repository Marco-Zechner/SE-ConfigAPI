using System;
using System.Globalization;

namespace Mz.Toml
{
    /// <summary>
    /// Represents a TOML offset date-time.
    /// </summary>
    public sealed class TomlOffsetDateTime
    {
        /// <summary>
        /// Initializes a TOML offset date-time.
        /// Set <paramref name="isUnknownLocalOffset"/> only for the RFC 3339
        /// negative-zero offset spelling -00:00.
        /// </summary>
        public TomlOffsetDateTime(TomlLocalDate date, TomlLocalTime time, int offsetMinutes, bool isUnknownLocalOffset = false)
        {
            if (date == null)
                throw new ArgumentNullException(nameof(date));

            if (time == null)
                throw new ArgumentNullException(nameof(time));

            if (offsetMinutes < -1439 || offsetMinutes > 1439)
                throw new ArgumentException("TOML numeric UTC offsets must be between -23:59 and +23:59.");

            if (isUnknownLocalOffset && offsetMinutes != 0)
                throw new ArgumentException("The RFC 3339 unknown-local-offset marker is only valid with offset 00:00.");

            Date = date;
            Time = time;
            OffsetMinutes = offsetMinutes;
            IsUnknownLocalOffset = isUnknownLocalOffset;
        }

        /// <summary>
        /// Initializes a TOML offset date-time from components.
        /// Fractional seconds are supplied as decimal digits without a dot.
        /// Set <paramref name="isUnknownLocalOffset"/> only for the RFC 3339
        /// negative-zero offset spelling -00:00.
        /// </summary>
        public TomlOffsetDateTime(int year, int month, int day, int hour, int minute, int second, string fractionalSeconds, 
            int offsetMinutes, bool isUnknownLocalOffset = false)
            : this(new TomlLocalDate(year, month, day), new TomlLocalTime(hour, minute, second, fractionalSeconds), 
                offsetMinutes, isUnknownLocalOffset) { }

        /// <summary>
        /// Gets the local date component before applying the offset.
        /// </summary>
        public TomlLocalDate Date { get; }

        /// <summary>
        /// Gets the local time component before applying the offset.
        /// </summary>
        public TomlLocalTime Time { get; }

        /// <summary>
        /// Gets the signed UTC offset in minutes.
        /// </summary>
        public int OffsetMinutes { get; }

        /// <summary>
        /// Gets whether the value used RFC 3339's -00:00 marker indicating
        /// that the local UTC offset is unknown.
        /// </summary>
        public bool IsUnknownLocalOffset { get; }

        /// <summary>
        /// Returns a deterministic TOML offset-date-time spelling.
        /// Known zero offset is written as Z and unknown zero offset as
        /// -00:00.
        /// </summary>
        public override string ToString()
        {
            var text = $"{Date}T{Time}";

            if (IsUnknownLocalOffset)
                return text + "-00:00";

            if (OffsetMinutes == 0)
                return text + "Z";

            var absolute = OffsetMinutes < 0 ? -OffsetMinutes : OffsetMinutes;
            var offsetHour = absolute / 60;
            var offsetMinute = absolute % 60;
            var sign = OffsetMinutes < 0 ? "-" : "+";

            var inv = CultureInfo.InvariantCulture;
            return $"{text}{sign}{offsetHour.ToString("D2", inv)}:{offsetMinute.ToString("D2", inv)}";
        }
    }
}
