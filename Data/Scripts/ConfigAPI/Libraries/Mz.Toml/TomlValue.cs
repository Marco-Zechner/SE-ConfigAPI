using System;

namespace Mz.Toml
{
    /// <summary>
    /// Represents a scalar TOML value.
    /// </summary>
    public sealed class TomlValue : TomlNode
    {
        private readonly object _value;

        internal TomlValue(TomlValueKind valueKind, object value, int line, int column) : base(TomlNodeKind.Value, line, column)
        {
            ValueKind = valueKind;
            _value = value;
        }

        /// <summary>
        /// Gets the scalar value kind.
        /// </summary>
        public TomlValueKind ValueKind { get; }

        /// <summary>
        /// Creates a TOML string value.
        /// </summary>
        public static TomlValue FromString(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new TomlValue(TomlValueKind.String, value, 0, 0);
        }

        /// <summary>
        /// Creates a TOML integer value.
        /// </summary>
        public static TomlValue FromInteger(long value)
            => new TomlValue(TomlValueKind.Integer, value, 0, 0);

        /// <summary>
        /// Creates a TOML floating-point value.
        /// </summary>
        public static TomlValue FromFloat(double value)
            => new TomlValue(TomlValueKind.Float, value, 0, 0);

        /// <summary>
        /// Creates a TOML Boolean value.
        /// </summary>
        public static TomlValue FromBoolean(bool value)
            => new TomlValue(TomlValueKind.Boolean, value, 0, 0);

        /// <summary>
        /// Creates a TOML offset date-time value.
        /// </summary>
        public static TomlValue FromOffsetDateTime(TomlOffsetDateTime value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new TomlValue(TomlValueKind.OffsetDateTime, value, 0, 0);
        }

        /// <summary>
        /// Creates a TOML local date-time value.
        /// </summary>
        public static TomlValue FromLocalDateTime(TomlLocalDateTime value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new TomlValue(TomlValueKind.LocalDateTime, value, 0, 0);
        }

        /// <summary>
        /// Creates a TOML local date value.
        /// </summary>
        public static TomlValue FromLocalDate(TomlLocalDate value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new TomlValue(TomlValueKind.LocalDate, value, 0, 0);
        }

        /// <summary>
        /// Creates a TOML local time value.
        /// </summary>
        public static TomlValue FromLocalTime(TomlLocalTime value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new TomlValue(TomlValueKind.LocalTime, value, 0, 0);
        }

        /// <summary>
        /// Returns the value as a string.
        /// </summary>
        public string AsString()
        {
            RequireKind(TomlValueKind.String);
            return (string)_value;
        }

        /// <summary>
        /// Returns the value as a 64-bit integer.
        /// </summary>
        public long AsInteger()
        {
            RequireKind(TomlValueKind.Integer);
            return (long)_value;
        }

        /// <summary>
        /// Returns the value as a double-precision floating-point number.
        /// </summary>
        public double AsFloat()
        {
            RequireKind(TomlValueKind.Float);
            return (double)_value;
        }

        /// <summary>
        /// Returns the value as a Boolean.
        /// </summary>
        public bool AsBoolean()
        {
            RequireKind(TomlValueKind.Boolean);
            return (bool)_value;
        }

        /// <summary>
        /// Returns the value as an offset date-time.
        /// </summary>
        public TomlOffsetDateTime AsOffsetDateTime()
        {
            RequireKind(TomlValueKind.OffsetDateTime);
            return (TomlOffsetDateTime)_value;
        }

        /// <summary>
        /// Returns the value as a local date-time.
        /// </summary>
        public TomlLocalDateTime AsLocalDateTime()
        {
            RequireKind(TomlValueKind.LocalDateTime);
            return (TomlLocalDateTime)_value;
        }

        /// <summary>
        /// Returns the value as a local date.
        /// </summary>
        public TomlLocalDate AsLocalDate()
        {
            RequireKind(TomlValueKind.LocalDate);
            return (TomlLocalDate)_value;
        }

        /// <summary>
        /// Returns the value as a local time.
        /// </summary>
        public TomlLocalTime AsLocalTime()
        {
            RequireKind(TomlValueKind.LocalTime);
            return (TomlLocalTime)_value;
        }

        private void RequireKind(TomlValueKind expected)
        {
            if (ValueKind != expected)
                throw new InvalidOperationException($"TOML value is {ValueKind}, not {expected}.");
        }
    }
}
