using System;

namespace MarcoZechner.ConfigAPI.V2.Domain
{
    public sealed class ConfigLocalDateTime : IEquatable<ConfigLocalDateTime>
    {
        public ConfigLocalDate Date { get; }
        public ConfigLocalTime Time { get; }

        public ConfigLocalDateTime(ConfigLocalDate date, ConfigLocalTime time)
        {
            if (date == null)
                throw new ArgumentNullException(nameof(date));

            if (time == null)
                throw new ArgumentNullException(nameof(time));

            Date = date;
            Time = time;
        }

        public bool Equals(ConfigLocalDateTime other)
        {
            if (ReferenceEquals(other, null))
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return Date.Equals(other.Date) && Time.Equals(other.Time);
        }

        public override bool Equals(object obj) => Equals(obj as ConfigLocalDateTime);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Date.GetHashCode() * 397) ^ Time.GetHashCode();
            }
        }
    }
}