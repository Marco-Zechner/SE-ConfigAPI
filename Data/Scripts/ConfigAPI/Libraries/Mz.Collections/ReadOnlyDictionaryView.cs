using System;
using System.Collections;
using System.Collections.Generic;

namespace Mz.Collections
{
    /// <summary>
    /// Exposes an <see cref="IDictionary{TKey,TValue}"/> through the read-only
    /// dictionary interface without copying the backing collection.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    public sealed class ReadOnlyDictionaryView<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        private readonly IDictionary<TKey, TValue> _items;

        /// <summary>
        /// Creates a live read-only view over the supplied dictionary.
        /// </summary>
        /// <param name="items">The backing dictionary.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is null.</exception>
        public ReadOnlyDictionaryView(IDictionary<TKey, TValue> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            _items = items;
        }

        /// <inheritdoc />
        public int Count => _items.Count;

        /// <inheritdoc />
        public IEnumerable<TKey> Keys => _items.Keys;

        /// <inheritdoc />
        public IEnumerable<TValue> Values => _items.Values;

        /// <inheritdoc />
        public TValue this[TKey key] => _items[key];

        /// <inheritdoc />
        public bool ContainsKey(TKey key) => _items.ContainsKey(key);

        /// <inheritdoc />
        public bool TryGetValue(TKey key, out TValue value) => _items.TryGetValue(key, out value);

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _items.GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}