using System;
using System.Collections;
using System.Collections.Generic;

namespace Mz.ConfigApi
{
    internal sealed class ReadOnlyList<T> : IReadOnlyList<T>
    {
        private readonly T[] _items;

        public ReadOnlyList(T[] items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            _items = new T[items.Length];
            Array.Copy(items, _items, items.Length);
        }

        public int Count => _items.Length;

        public T this[int index] => _items[index];

        public IEnumerator<T> GetEnumerator() => ( (IEnumerable<T>)_items ).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
    }
    
}
