using System;
using System.Collections;
using System.Collections.Generic;

namespace Mz.Toml.Internal
{
    internal sealed class TomlReadOnlyList<T> : IReadOnlyList<T>
    {
        private readonly IList<T> _items;

        public TomlReadOnlyList(IList<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            _items = items;
        }

        public int Count => _items.Count;

        public T this[int index] => _items[index];

        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
