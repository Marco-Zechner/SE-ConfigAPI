using System;

namespace MarcoZechner.ConfigAPI.V2.Domain
{
    public sealed class ConfigIdentity : IEquatable<ConfigIdentity>
    {
        public string OwnerId { get; }
        public string ConfigKey { get; }

        public ConfigIdentity(string ownerId, string configKey)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                throw new ArgumentException("Owner ID must not be empty.", nameof(ownerId));

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));

            OwnerId = ownerId;
            ConfigKey = configKey;
        }

        public bool Equals(ConfigIdentity other)
        {
            if (ReferenceEquals(other, null))
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return string.Equals(OwnerId, other.OwnerId, StringComparison.Ordinal)
                && string.Equals(ConfigKey, other.ConfigKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as ConfigIdentity);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(OwnerId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ConfigKey);
                return hash;
            }
        }
    }
}
