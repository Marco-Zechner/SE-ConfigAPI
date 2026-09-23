using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Domain;

namespace MarcoZechner.ConfigAPI.Persistence
{
    public sealed class ConfigDefaultsEntry
    {
        public string File { get; }
        public ConfigIdentity Identity { get; }
        public ConfigDocument BaselineDefaults { get; }

        public ConfigDefaultsEntry(string file, ConfigIdentity identity, ConfigDocument baselineDefaults)
        {
            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));
            if (identity == null)
                throw new ArgumentNullException(nameof(identity));
            if (baselineDefaults == null)
                throw new ArgumentNullException(nameof(baselineDefaults));

            File = file;
            Identity = identity;
            BaselineDefaults = baselineDefaults;
        }
    }

    public sealed class ConfigDefaultsStore
    {
        private readonly Dictionary<string, ConfigDefaultsEntry> _entries;

        public ConfigDefaultsStore()
        {
            _entries = new Dictionary<string, ConfigDefaultsEntry>(StringComparer.Ordinal);
        }

        internal ConfigDefaultsStore(IEnumerable<ConfigDefaultsEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            _entries = new Dictionary<string, ConfigDefaultsEntry>(StringComparer.Ordinal);

            foreach (ConfigDefaultsEntry entry in entries)
            {
                if (entry == null)
                    throw new ArgumentException("Defaults store entries must not contain null.", nameof(entries));
                if (_entries.ContainsKey(entry.File))
                    throw new ArgumentException("Defaults store contains a duplicate config file: " + entry.File, nameof(entries));

                _entries.Add(entry.File, entry);
            }
        }

        public bool TryGet(string file, out ConfigDefaultsEntry entry)
        {
            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));

            return _entries.TryGetValue(file, out entry);
        }

        public ConfigDefaultsStore With(ConfigDefaultsEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var entries = new Dictionary<string, ConfigDefaultsEntry>(_entries, StringComparer.Ordinal);
            entries[entry.File] = entry;
            return new ConfigDefaultsStore(entries.Values);
        }

        internal ConfigDefaultsEntry[] GetEntries()
        {
            var entries = new ConfigDefaultsEntry[_entries.Count];
            _entries.Values.CopyTo(entries, 0);
            Array.Sort(entries, (left, right) => StringComparer.Ordinal.Compare(left.File, right.File));
            return entries;
        }
    }
}
