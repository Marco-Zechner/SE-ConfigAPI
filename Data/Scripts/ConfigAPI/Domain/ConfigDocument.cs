using System;
using System.Collections.Generic;

namespace MarcoZechner.ConfigAPI.V2.Domain
{
    public sealed class ConfigDocument : IEquatable<ConfigDocument>
    {
        public ConfigObjectNode Root { get; }

        public ConfigDocument(ConfigObjectNode root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            Root = root;
        }

        public bool TryGet(ConfigValuePath path, out ConfigNode value)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            ConfigNode current = Root;

            foreach (string segment in path.Segments)
            {
                var obj = current as ConfigObjectNode;
                if (obj != null && obj.TryGet(segment, out current))
                    continue;
                
                value = null;
                return false;
            }

            value = current;
            return true;
        }

        public ConfigDocument WithValue(ConfigValuePath path, ConfigNode value)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (path.Segments.Count == 0)
                throw new ArgumentException("Value path must contain at least one segment.", nameof(path));

            ConfigObjectNode root = ReplaceValue(Root, path, 0, value);
            return new ConfigDocument(root);
        }

        public bool Equals(ConfigDocument other)
        {
            if (ReferenceEquals(other, null))
                return false;

            return ReferenceEquals(this, other) || Root.Equals(other.Root);
        }

        public override bool Equals(object obj) => Equals(obj as ConfigDocument);

        public override int GetHashCode() => Root.GetHashCode();

        private static ConfigObjectNode ReplaceValue(ConfigObjectNode current, ConfigValuePath path, int segmentIndex, ConfigNode value)
        {
            string segment = path.Segments[segmentIndex];

            ConfigNode existing;
            if (!current.TryGet(segment, out existing))
                throw new KeyNotFoundException("Config value path does not exist.");

            ConfigNode replacement;
            if (segmentIndex == path.Segments.Count - 1)
                replacement = value;
            else
            {
                var child = existing as ConfigObjectNode;
                if (child == null)
                    throw new KeyNotFoundException("Config value path does not resolve through an object.");

                replacement = ReplaceValue(child, path, segmentIndex + 1, value);
            }

            var entries = new ConfigObjectEntry[current.Entries.Count];

            for (var i = 0; i < current.Entries.Count; i++)
            {
                ConfigObjectEntry entry = current.Entries[i];
                entries[i] = string.Equals(entry.Name, segment, StringComparison.Ordinal)
                    ? new ConfigObjectEntry(entry.Name, replacement)
                    : entry;
            }

            return new ConfigObjectNode(entries);
        }
    }
}
