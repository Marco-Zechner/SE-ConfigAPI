using System;
using System.Globalization;

namespace Mz.Toml
{
    /// <summary>
    /// Represents a TOML local time without a date or timezone.
    /// </summary>
    public sealed class TomlLocalTime
    {
        /// <summary>
        /// Initializes a TOML local time without fractional seconds.
        /// </summary>
        public TomlLocalTime(int hour, int minute, int second) : this(hour, minute, second, string.Empty) { }

        /// <summary>
        /// Initializes a TOML local time.
        /// Fractional seconds are supplied as decimal digits without a dot.
        /// </summary>
        public TomlLocalTime(int hour, int minute, int second, string fractionalSeconds)
        {
            if (hour < 0 || hour > 23 || minute < 0 || minute > 59 || second < 0 || second > 60)
                throw new ArgumentException("The supplied components do not form a valid TOML local time.");

            if (fractionalSeconds == null)
                throw new ArgumentNullException(nameof(fractionalSeconds));

            foreach (var c in fractionalSeconds)
            {
                if (c < '0' || c > '9')
                    throw new ArgumentException("TOML fractional seconds must contain only decimal digits.");
            }

            Hour = hour;
            Minute = minute;
            Second = second;
            FractionalSeconds = fractionalSeconds;
        }

        /// <summary>
        /// Gets the hour from 0 through 23.
        /// </summary>
        public int Hour { get; }

        /// <summary>
        /// Gets the minute from 0 through 59.
        /// </summary>
        public int Minute { get; }

        /// <summary>
        /// Gets the second. TOML permits the leap-second spelling 60.
        /// </summary>
        public int Second { get; }

        /// <summary>
        /// Gets fractional-second decimal digits without the leading dot.
        /// An empty string means that no fraction was supplied.
        /// </summary>
        public string FractionalSeconds { get; }

        /// <summary>
        /// Returns the canonical TOML local-time spelling.
        /// </summary>
        public override string ToString()
        {
            var inv = CultureInfo.InvariantCulture;
            var text = $"{Hour.ToString("D2", inv)}:{Minute.ToString("D2", inv)}:{Second.ToString("D2", inv)}";

            if (FractionalSeconds.Length > 0)
                text += $".{FractionalSeconds}";

            return text;
        }
    }
}
