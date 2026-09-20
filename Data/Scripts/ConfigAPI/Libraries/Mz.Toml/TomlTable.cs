using System;
using System.Collections;
using System.Collections.Generic;
using Mz.Toml.Internal;

namespace Mz.Toml
{
    /// <summary>
    /// Represents an ordered TOML table.
    /// </summary>
    public sealed class TomlTable : TomlNode, IEnumerable<KeyValuePair<string, TomlNode>>
    {
        private readonly List<string> _keys;
        private readonly Dictionary<string, TomlNode> _values;

        /// <summary>
        /// Initializes an empty programmatic TOML table.
        /// </summary>
        public TomlTable() : this(0, 0, TomlTableDefinitionKind.Programmatic) { }

        internal TomlTable(int line, int column, TomlTableDefinitionKind definitionKind) : base(TomlNodeKind.Table, line, column)
        {
            _keys = new List<string>();
            Keys = new TomlReadOnlyList<string>(_keys);
            _values = new Dictionary<string, TomlNode>(StringComparer.Ordinal);
            DefinitionKind = definitionKind;
        }

        internal TomlTableDefinitionKind DefinitionKind { get; set; }

        /// <summary>
        /// Gets the number of entries in the table.
        /// </summary>
        public int Count => _keys.Count;

        /// <summary>
        /// Gets the keys in deterministic insertion order.
        /// </summary>
        public IReadOnlyList<string> Keys { get; }

        /// <summary>
        /// Gets the node associated with a key.
        /// </summary>
        public TomlNode this[string key] => _values[key];

        /// <summary>
        /// Determines whether the table contains the specified key.
        /// </summary>
        public bool ContainsKey(string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            return _values.ContainsKey(key);
        }

        /// <summary>
        /// Attempts to retrieve a node by key.
        /// </summary>
        public bool TryGetValue(string key, out TomlNode value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            return _values.TryGetValue(key, out value);
        }

        /// <summary>
        /// Adds or replaces a node while preserving deterministic key order.
        /// Empty keys are valid TOML keys and are written quoted.
        /// </summary>
        public void Set(string key, TomlNode value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (!_values.ContainsKey(key))
                _keys.Add(key);

            _values[key] = value;
        }

        /// <summary>
        /// Returns an enumerator over entries in deterministic insertion order.
        /// </summary>
        public IEnumerator<KeyValuePair<string, TomlNode>> GetEnumerator()
        {
            foreach (var key in _keys)
                yield return new KeyValuePair<string, TomlNode>(key, _values[key]);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
