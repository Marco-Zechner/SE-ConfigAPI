using System;
using System.Collections;
using System.Collections.Generic;

namespace Mz.Collections
{
    /// <summary>
    /// Exposes an <see cref="IList{T}"/> through the read-only list interface
    /// without copying the backing collection.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public sealed class ReadOnlyListView<T> : IReadOnlyList<T>
    {
        private readonly IList<T> _items;

        /// <summary>
        /// Creates a live read-only view over the supplied list.
        /// </summary>
        /// <param name="items">The backing list.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is null.</exception>
        public ReadOnlyListView(IList<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            _items = items;
        }

        /// <inheritdoc />
        public int Count => _items.Count;

        /// <inheritdoc />
        public T this[int index] => _items[index];

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}