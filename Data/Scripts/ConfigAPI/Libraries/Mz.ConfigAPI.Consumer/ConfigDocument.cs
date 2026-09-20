using System;
using System.Collections.Generic;

namespace Mz.ConfigApi
{
    public sealed class ConfigDocument : IEquatable<ConfigDocument>
    {
        public ConfigDocument(params ConfigEntry[] entries)
        {
            Root = ConfigValue.Object(entries);
        }

        public IReadOnlyList<ConfigEntry> Entries => Root.Entries;

        internal ConfigValue Root { get; }

        public bool Equals(ConfigDocument other) => other != null && Root.Equals(other.Root);

        public bool TryGet(string name, out ConfigValue value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Config entry name must not be empty.", nameof(name));

            foreach (ConfigEntry entry in Entries)
            {
                if (!string.Equals(entry.Name, name, StringComparison.Ordinal))
                    continue;

                value = entry.Value;
                return true;
            }

            value = null;
            return false;
        }

        public override bool Equals(object obj) => Equals(obj as ConfigDocument);

        public override int GetHashCode() => Root.GetHashCode();
    }
}
