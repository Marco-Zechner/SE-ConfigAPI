using System;
using System.Collections.Generic;

namespace MarcoZechner.ConfigAPI.V2.Domain
{
    public sealed class ConfigArrayNode : ConfigNode
    {
        private readonly ConfigNode[] _items;

        public IReadOnlyList<ConfigNode> Items { get; }

        public ConfigArrayNode(params ConfigNode[] items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            _items = new ConfigNode[items.Length];

            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] == null)
                    throw new ArgumentException("Array items must not contain null. Use ConfigNullNode.Instance for semantic null.", 
                                                nameof(items));

                _items[i] = items[i];
            }

            Items = Array.AsReadOnly(_items);
        }

        protected override bool EqualsNode(ConfigNode other)
        {
            var array = other as ConfigArrayNode;
            if (array == null || _items.Length != array._items.Length)
                return false;

            for (var i = 0; i < _items.Length; i++)
            {
                if (!_items[i].Equals(array._items[i]))
                    return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;

                foreach (ConfigNode item in _items)
                    hash = (hash * 31) ^ item.GetHashCode();

                return hash;
            }
        }
    }
}
