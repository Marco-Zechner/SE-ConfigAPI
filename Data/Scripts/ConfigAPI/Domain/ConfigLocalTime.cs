using System;
using System.Linq;

namespace MarcoZechner.ConfigAPI.V2.Domain
{
    public sealed class ConfigLocalTime : IEquatable<ConfigLocalTime>
    {
        public int Hour { get; }
        public int Minute { get; }
        public int Second { get; }
        public string FractionalSeconds { get; }

        public ConfigLocalTime(int hour, int minute, int second) : this(hour, minute, second, string.Empty) { }

        public ConfigLocalTime(int hour, int minute, int second, string fractionalSeconds)
        {
            if (hour < 0 || hour > 23 || minute < 0 || minute > 59 || second < 0 || second > 60)
                throw new ArgumentException("The supplied components do not form a valid local time.");

            if (fractionalSeconds == null)
                throw new ArgumentNullException(nameof(fractionalSeconds));

            if (fractionalSeconds.Any(c => c < '0' || c > '9'))
                throw new ArgumentException("Fractional seconds must contain only decimal digits.", nameof(fractionalSeconds));

            Hour = hour;
            Minute = minute;
            Second = second;
            FractionalSeconds = fractionalSeconds;
        }

        public bool Equals(ConfigLocalTime other)
        {
            if (ReferenceEquals(other, null))
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return Hour == other.Hour &&
                   Minute == other.Minute &&
                   Second == other.Second &&
                   string.Equals(FractionalSeconds, other.FractionalSeconds, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as ConfigLocalTime);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Hour;
                hash = (hash * 397) ^ Minute;
                hash = (hash * 397) ^ Second;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(FractionalSeconds);
                return hash;
            }
        }
    }
}