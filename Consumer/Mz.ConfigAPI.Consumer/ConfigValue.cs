using System;
using System.Collections.Generic;

namespace Mz.ConfigApi
{
    public sealed class ConfigValue : IEquatable<ConfigValue>
    {
        private readonly ReadOnlyList<ConfigEntry> _entries;
        private readonly ReadOnlyList<ConfigValue> _items;

        private ConfigValue(ConfigValueKind kind, object scalarValue, ConfigEntry[] entries, ConfigValue[] items)
        {
            Kind = kind;
            ScalarValue = scalarValue;

            _entries = entries == null ? null : new ReadOnlyList<ConfigEntry>(entries);

            _items = items == null ? null : new ReadOnlyList<ConfigValue>(items);
        }

        public ConfigValueKind Kind { get; }

        public object ScalarValue { get; }

        public IReadOnlyList<ConfigEntry> Entries => _entries;

        public IReadOnlyList<ConfigValue> Items => _items;

        public static ConfigValue Null { get; } = new ConfigValue(ConfigValueKind.Null, null, null, null);

        public bool Equals(ConfigValue other)
        {
            if (ReferenceEquals(other, null))
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Kind != other.Kind)
                return false;

            switch (Kind)
            {
                case ConfigValueKind.Object: return ObjectEquals(other);
                case ConfigValueKind.Array:  return ArrayEquals(other);
                default:                     return Equals(ScalarValue, other.ScalarValue);
            }
        }

        public static ConfigValue Boolean(bool value) => new ConfigValue(ConfigValueKind.Boolean, value, null, null);

        public static ConfigValue Integer(long value) => new ConfigValue(ConfigValueKind.Integer, value, null, null);

        public static ConfigValue Float(double value) => new ConfigValue(ConfigValueKind.Float, value, null, null);

        public static ConfigValue String(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new ConfigValue(ConfigValueKind.String, value, null, null);
        }

        public static ConfigValue Object(params ConfigEntry[] entries)
        {
            var copy = CopyEntries(entries);

            ValidateUniqueEntryNames(copy);

            return new ConfigValue(ConfigValueKind.Object, null, copy, null);
        }

        public static ConfigValue Array(params ConfigValue[] items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            var copy = new ConfigValue[items.Length];

            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] == null)
                    throw new ArgumentNullException(nameof(items));

                copy[i] = items[i];
            }

            return new ConfigValue(ConfigValueKind.Array, null, null, copy);
        }

        public static ConfigValue OffsetDateTime(ConfigOffsetDateTime value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new ConfigValue(ConfigValueKind.OffsetDateTime, value, null, null);
        }

        public static ConfigValue LocalDateTime(ConfigLocalDateTime value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new ConfigValue(ConfigValueKind.LocalDateTime, value, null, null);
        }

        public static ConfigValue LocalDate(ConfigDate value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new ConfigValue(ConfigValueKind.LocalDate, value, null, null);
        }

        public static ConfigValue LocalTime(ConfigTime value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new ConfigValue(ConfigValueKind.LocalTime, value, null, null);
        }

        public override bool Equals(object obj) => Equals(obj as ConfigValue);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;

                switch (Kind)
                {
                    case ConfigValueKind.Object:
                    {
                        foreach (ConfigEntry entry in _entries)
                            hash ^= entry.GetHashCode();

                        return hash;
                    }
                    case ConfigValueKind.Array:
                    {
                        foreach (ConfigValue entry in _items)
                            hash = ( hash * 397 ) ^ entry.GetHashCode();

                        return hash;
                    }
                    default:
                        return ( hash * 397 ) ^ ( ScalarValue == null ? 0 : ScalarValue.GetHashCode() );
                }
            }
        }

        private bool ObjectEquals(ConfigValue other)
        {
            if (_entries.Count != other._entries.Count)
                return false;

            foreach (ConfigEntry entry in _entries)
            {
                ConfigValue otherValue;

                if (!TryGetEntry(other._entries, entry.Name, out otherValue))
                    return false;

                if (!entry.Value.Equals(otherValue))
                    return false;
            }

            return true;
        }

        private bool ArrayEquals(ConfigValue other)
        {
            if (_items.Count != other._items.Count)
                return false;

            for (var i = 0; i < _items.Count; i++)
                if (!_items[i].Equals(other._items[i]))
                    return false;

            return true;
        }

        internal static ConfigEntry[] CopyEntries(ConfigEntry[] entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            var copy = new ConfigEntry[entries.Length];

            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i] == null)
                    throw new ArgumentNullException(nameof(entries));

                copy[i] = entries[i];
            }

            return copy;
        }

        internal static void ValidateUniqueEntryNames(ConfigEntry[] entries)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (ConfigEntry entry in entries)
                if (!names.Add(entry.Name))
                    throw new ArgumentException($"Config object contains duplicate entry name: {entry.Name}", nameof(entries));
        }

        private static bool TryGetEntry(IReadOnlyList<ConfigEntry> entries, string name, out ConfigValue value)
        {
            foreach (ConfigEntry entry in entries)
            {
                if (!string.Equals(entry.Name, name, StringComparison.Ordinal))
                    continue;

                value = entry.Value;
                return true;
            }

            value = null;
            return false;
        }
    }
}
