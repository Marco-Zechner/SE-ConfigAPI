using System;
using System.Collections.Generic;

namespace MarcoZechner.ConfigAPI.V2.Domain
{
    public sealed class ConfigObjectEntry
    {
        public string Name { get; }
        public ConfigNode Value { get; }

        public ConfigObjectEntry(string name, ConfigNode value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Object entry name must not be empty.", nameof(name));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            Name = name;
            Value = value;
        }
    }

    public sealed class ConfigObjectNode : ConfigNode
    {
        private readonly Dictionary<string, ConfigNode> _values;

        public IReadOnlyList<ConfigObjectEntry> Entries { get; }

        public ConfigObjectNode(params ConfigObjectEntry[] entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            var entriesCopy = new ConfigObjectEntry[entries.Length];
            _values = new Dictionary<string, ConfigNode>(StringComparer.Ordinal);

            for (var i = 0; i < entries.Length; i++)
            {
                ConfigObjectEntry entry = entries[i];
                if (entry == null)
                    throw new ArgumentException("Object entries must not contain null.", nameof(entries));

                if (_values.ContainsKey(entry.Name))
                    throw new ArgumentException("Duplicate object entry: " + entry.Name, nameof(entries));

                entriesCopy[i] = entry;
                _values.Add(entry.Name, entry.Value);
            }

            Entries = Array.AsReadOnly(entriesCopy);
        }

        public bool TryGet(string name, out ConfigNode value)
        {
            if (name != null)
                return _values.TryGetValue(name, out value);
            
            value = null;
            return false;
        }

        protected override bool EqualsNode(ConfigNode other)
        {
            var obj = other as ConfigObjectNode;
            if (obj == null || _values.Count != obj._values.Count)
                return false;

            foreach (var pair in _values)
            {
                ConfigNode otherValue;
                if (!obj._values.TryGetValue(pair.Key, out otherValue))
                    return false;

                if (!pair.Value.Equals(otherValue))
                    return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17 ^ _values.Count;

                foreach (var pair in _values)
                {
                    int entryHash = StringComparer.Ordinal.GetHashCode(pair.Key);
                    entryHash = (entryHash * 397) ^ pair.Value.GetHashCode();
                    hash ^= entryHash;
                }

                return hash;
            }
        }
    }
}
