using System;
using System.Globalization;

namespace MarcoZechner.ConfigAPI.Persistence
{
    public static class ConfigBackupName
    {
        public static string Create(string file, DateTime timestampUtc) => Create(file, timestampUtc, collisionIndex: 0);

        public static string Create(string file, DateTime timestampUtc, int collisionIndex)
        {
            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));

            if (collisionIndex < 0)
                throw new ArgumentException("Backup collision index must not be negative.", nameof(collisionIndex));

            DateTime utc = timestampUtc.Kind == DateTimeKind.Utc
                               ? timestampUtc
                               : timestampUtc.ToUniversalTime();

            string collisionSuffix = collisionIndex == 0
                                         ? string.Empty
                                         : "." + collisionIndex.ToString(CultureInfo.InvariantCulture);

            return $"{file}.{utc.ToString("yyyyMMdd'T'HHmmss.fffffff'Z'", CultureInfo.InvariantCulture)}{collisionSuffix}.bak";
        }
    }
}
