using System;

namespace Mz.ConfigApi
{
    public sealed class ConfigOffsetDateTime : IEquatable<ConfigOffsetDateTime>
    {
        public ConfigOffsetDateTime(ConfigDate date, ConfigTime time, int offsetMinutes, bool isUnknownLocalOffset = false)
        {
            if (date == null)
                throw new ArgumentNullException(nameof(date));

            if (time == null)
                throw new ArgumentNullException(nameof(time));

            if (offsetMinutes < -1439 || offsetMinutes > 1439)
                throw new ArgumentException("UTC offset must be between -23:59 and +23:59.", nameof(offsetMinutes));

            if (isUnknownLocalOffset && offsetMinutes != 0)
                throw new ArgumentException("Unknown local offset is only valid with offset 00:00.", nameof(isUnknownLocalOffset));

            Date = date;
            Time = time;
            OffsetMinutes = offsetMinutes;
            IsUnknownLocalOffset = isUnknownLocalOffset;
        }

        public ConfigDate Date { get; }
        public ConfigTime Time { get; }
        public int OffsetMinutes { get; }
        public bool IsUnknownLocalOffset { get; }

        public bool Equals(ConfigOffsetDateTime other) 
            => other != null && Date.Equals(other.Date) && Time.Equals(other.Time) &&
               OffsetMinutes == other.OffsetMinutes && IsUnknownLocalOffset == other.IsUnknownLocalOffset;

        public override bool Equals(object obj) => Equals(obj as ConfigOffsetDateTime);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Date.GetHashCode();
                hash = ( hash * 397 ) ^ Time.GetHashCode();
                hash = ( hash * 397 ) ^ OffsetMinutes;
                hash = ( hash * 397 ) ^ IsUnknownLocalOffset.GetHashCode();

                return hash;
            }
        }
    }
}
