using System;

namespace Mz.ConfigApi
{
    public sealed class ConfigLocalDateTime : IEquatable<ConfigLocalDateTime>
    {
        public ConfigLocalDateTime(ConfigDate date, ConfigTime time)
        {
            if (date == null)
                throw new ArgumentNullException(nameof(date));

            if (time == null)
                throw new ArgumentNullException(nameof(time));

            Date = date;
            Time = time;
        }

        public ConfigDate Date { get; }
        public ConfigTime Time { get; }

        public bool Equals(ConfigLocalDateTime other) 
            => other != null && Date.Equals(other.Date) && Time.Equals(other.Time);

        public override bool Equals(object obj) => Equals(obj as ConfigLocalDateTime);

        public override int GetHashCode()
        {
            unchecked
            {
                return ( Date.GetHashCode() * 397 ) ^ Time.GetHashCode();
            }
        }
    }
}
