using System;

namespace Mz.ConfigApi
{
    public sealed class ConfigEntry : IEquatable<ConfigEntry>
    {
        public ConfigEntry(string name, ConfigValue value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Config entry name must not be empty.", nameof(name));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            Name = name;
            Value = value;
        }

        public string Name { get; }
        public ConfigValue Value { get; }

        public bool Equals(ConfigEntry other) 
            => other != null && string.Equals(Name, other.Name, StringComparison.Ordinal) && Value.Equals(other.Value);

        public override bool Equals(object obj) => Equals(obj as ConfigEntry);

        public override int GetHashCode()
        {
            unchecked
            {
                return ( StringComparer.Ordinal.GetHashCode(Name) * 397 ) ^ Value.GetHashCode();
            }
        }
    }
}
