using System;
using System.Collections;
using System.Collections.Generic;
namespace Mz.ApiProtocol
{
    internal sealed class ReadOnlyDictionaryView<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        private readonly IDictionary<TKey, TValue> _items;
        internal ReadOnlyDictionaryView(IDictionary<TKey, TValue> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            _items = items;
        }
        public int Count => _items.Count;

        public IEnumerable<TKey> Keys => _items.Keys;

        public IEnumerable<TValue> Values => _items.Values;

        public TValue this[TKey key] => _items[key];

        public bool ContainsKey(TKey key) => _items.ContainsKey(key);

        public bool TryGetValue(TKey key, out TValue value) => _items.TryGetValue(key, out value);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
