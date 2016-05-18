#region BSD license
/*
Copyright © 2015, KimikoMuffin.
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer. 
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.
3. The names of its contributors may not be used to endorse or promote 
   products derived from this software without specific prior written 
   permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DieFledermaus.Globalization;

namespace DieFledermaus
{
    /// <summary>
    /// Represents a collection of key sizes. All values in the collection are unique, and stored in ascending order.
    /// </summary>
    [DebuggerDisplay(DieFledermausStream.CollectionDebuggerDisplay)]
    [DebuggerTypeProxy(typeof(DebugView))]
    public sealed class KeySizeList : IList<int>, IList
#if IREADONLY
        , IReadOnlyList<int>
#endif
    {
        private readonly int[] _items;

        #region Constructors
        /// <summary>
        /// Creates a new instance using elements copied from the specified collection.
        /// </summary>
        /// <param name="source">A collection whose elements will be copied to the new instance.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="source"/> is <see langword="null"/>.
        /// </exception>
        public KeySizeList(IEnumerable<int> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
#if NOISET
            _items = new HashSet<int>(source).ToArray();
            Array.Sort(_items, 0, _items.Length);
#else
            _items = new SortedSet<int>(source).ToArray();
#endif
            if (_items.Length == 1)
                _min = _max = _items[0];
            else if (_items.Length != 0)
            {
                _min = _items[0];
                _max = _items[_items.Length - 1];
            }
        }

        /// <summary>
        /// Creates a new instance using elements copied from the specified collection.
        /// </summary>
        /// <param name="source">A collection whose elements will be copied to the new instance.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="source"/> is <see langword="null"/>.
        /// </exception>
        public KeySizeList(params int[] source)
            : this((IEnumerable<int>)source)
        {
        }

        /// <summary>
        /// Creates a new instance containing a single value.
        /// </summary>
        /// <param name="value">The value to set.</param>
        public KeySizeList(int value)
        {
            _items = new int[] { value };
            _min = _max = value;
        }

        /// <summary>
        /// Creates a new instance containing two values.
        /// </summary>
        /// <param name="value1">A value to set.</param>
        /// <param name="value2">A value to set.</param>
        public KeySizeList(int value1, int value2)
        {
            if (value1 < value2)
            {
                _min = value1;
                _max = value2;
            }
            else
            {
                _min = value2;
                _max = value1;
            }

            if (value1 == value2)
                _items = new int[] { value1 };
            else
                _items = new int[] { _min, _max };
        }

        private KeySizeList(int minSize, int maxSize, int step)
        {
            _min = minSize;
            _max = maxSize;
            int[] _items = new int[1 + ((_max - _min) / step)];

            for (int i = 0; i < _items.Length; i++)
                _items[i] = minSize + i * step;
        }

        /// <summary>
        /// Creates a new instance using the specified range of values.
        /// </summary>
        /// <param name="minSize">The minimum value.</param>
        /// <param name="maxSize">The maximum value.</param>
        /// <param name="skipSize">The interval between valid sizes.</param>
        /// <returns>A new <see cref="KeySizeList"/> instance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <para><paramref name="minSize"/> is greater than <paramref name="maxSize"/>.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="skipSize"/> is less than 0, or is nonzero and greater than <paramref name="maxSize"/> minus <paramref name="minSize"/>.</para>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="skipSize"/> is nonzero, and <paramref name="maxSize"/> minus <paramref name="minSize"/> is not a multiple of <paramref name="skipSize"/>.
        /// </exception>
        public static KeySizeList FromStepRange(int minSize, int maxSize, int skipSize)
        {
            if (minSize > maxSize)
                throw new ArgumentOutOfRangeException(nameof(maxSize), TextResources.OutOfRangeMaxLessThanMin);

            if (skipSize < 0 || (skipSize != 0 && skipSize >= maxSize - minSize))
                throw new ArgumentOutOfRangeException(nameof(skipSize), TextResources.OutOfRangeSkipSize);

            int diff = maxSize - minSize;

            if (skipSize == 0)
            {
                if (maxSize == minSize) skipSize = 1;
                else skipSize = diff;
            }
            else if (diff % skipSize != 0)
                throw new ArgumentException(TextResources.BadSkipSize, nameof(skipSize));

            return new KeySizeList(minSize, maxSize, skipSize);
        }
        #endregion

        /// <summary>
        /// Gets the element at the specified index.
        /// </summary>
        /// <param name="index">The index of the element to get.</param>
        /// <returns>The element at the specified index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than 0 or is greater than or equal to <see cref="Count"/>.
        /// </exception>
        public int this[int index]
        {
            get
            {
                if (index < 0 || index >= _items.Length)
                    throw new ArgumentOutOfRangeException(nameof(index), TextResources.OutOfRangeIndex);
                return _items[index];
            }
        }

        int IList<int>.this[int index]
        {
            get { return this[index]; }
            set { throw new NotSupportedException(TextResources.CollectReadOnly); }
        }

        object IList.this[int index]
        {
            get { return this[index]; }
            set { throw new NotSupportedException(TextResources.CollectReadOnly); }
        }

        /// <summary>
        /// Gets the number of elements in the list.
        /// </summary>
        public int Count
        {
            get { return _items.Length; }
        }

        private readonly int _min;
        /// <summary>
        /// Gets the minimum value in the collection.
        /// </summary>
        public int MinSize { get { return _min; } }

        private readonly int _max;
        /// <summary>
        /// Gets the maximum value in the collection.
        /// </summary>
        public int MaxSize { get { return _max; } }

        /// <summary>
        /// Determines if the specified value exists in the list.
        /// </summary>
        /// <param name="value">The value to search for in the list.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> was found; <see langword="false"/> otherwise.</returns>
        public bool Contains(int value)
        {
            return IndexOf(value) >= 0;
        }

        bool IList.Contains(object value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return value is int && Contains((int)value);
        }

        /// <summary>
        /// Gets the index of the specified value.
        /// </summary>
        /// <param name="value">The value to search for in the list.</param>
        /// <returns>The index in the list of <paramref name="value"/>, or -1 if <paramref name="value"/> was not found.</returns>
        public int IndexOf(int value)
        {
            if (value == _min) return 0;
            if (value == _max)
                return _items.Length - 1;
            int dex = Array.BinarySearch(_items, value);
            if (dex < 0) return -1;
            return dex;
        }

        int IList.IndexOf(object value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value is int) return IndexOf((int)value);
            return -1;
        }

        /// <summary>
        /// Copies all elements in the current instance to the specified array.
        /// </summary>
        /// <param name="array">The array to which the current instance will be copied.</param>
        /// <param name="arrayIndex">The index in <paramref name="array"/> at which copying begins.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="array"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="arrayIndex"/> plus <see cref="Count"/> is greater than the number of elements in <paramref name="array"/>.
        /// </exception>
        public void CopyTo(int[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException(TextResources.OutOfRangeLessThanZero, nameof(arrayIndex));
            if (arrayIndex + _items.Length > array.Length)
                throw new ArgumentException(TextResources.BadIndexRange);

            Array.Copy(_items, 0, array, arrayIndex, _items.Length);
        }

        void ICollection.CopyTo(Array array, int index)
        {
            _items.CopyTo(array, index);
        }

        /// <summary>
        /// Returns an array containing elements copied from the current instance.
        /// </summary>
        /// <returns>An array containing elements copied from the current instance.</returns>
        public int[] ToArray()
        {
            return (int[])_items.Clone();
        }

        #region Not Supported
        void ICollection<int>.Add(int item)
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }

        int IList.Add(object value)
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }

        bool ICollection<int>.Remove(int item)
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }

        void IList.Remove(object value)
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }

        void ICollection<int>.Clear()
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }

        void IList.Clear()
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }

        void IList<int>.Insert(int index, int item)
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }

        void IList.Insert(int index, object value)
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }

        void IList<int>.RemoveAt(int index)
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }

        void IList.RemoveAt(int index)
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }
        #endregion

        bool ICollection<int>.IsReadOnly { get { return true; } }

        bool IList.IsReadOnly { get { return true; } }

        bool IList.IsFixedSize { get { return true; } }

        bool ICollection.IsSynchronized { get { return true; } }

        private object _syncRoot;
        object ICollection.SyncRoot
        {
            get
            {
                if (_syncRoot == null)
                    System.Threading.Interlocked.CompareExchange(ref _syncRoot, new object(), null);
                return _syncRoot;
            }
        }

        /// <summary>
        /// Returns an enumerator which iterates through the list.
        /// </summary>
        /// <returns>An enumerator which iterates through the list.</returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<int> IEnumerable<int>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// An enumerator which iterates through the list.
        /// </summary>
        public struct Enumerator : IEnumerator<int>
        {
            private int _index, _current;
            private int[] _items;

            internal Enumerator(KeySizeList list)
            {
                _index = -1;
                _current = 0;
                _items = list._items;
            }

            /// <summary>
            /// Gets the element at the current position in the enumerator.
            /// </summary>
            public int Current { get { return _current; } }

            object IEnumerator.Current { get { return _current; } }

            /// <summary>
            /// Disposes of the current instance.
            /// </summary>
            public void Dispose()
            {
                this = default(Enumerator);
            }

            /// <summary>
            /// Advances the enumerator to the next position in the list.
            /// </summary>
            /// <returns><see langword="true"/> if the enumerator was successfully advanced; <see langword="false"/> if the
            /// enumerator has passed the end of the collection.</returns>
            public bool MoveNext()
            {
                if (_items == null)
                    return false;

                if (++_index >= _items.Length)
                {
                    Dispose();
                    return false;
                }
                _current = _items[_index];
                return true;
            }

            void IEnumerator.Reset()
            {
                _current = 0;
                _index = -1;
            }
        }

        private class DebugView
        {
            private KeySizeList _col;

            public DebugView(KeySizeList col)
            {
                _col = col;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public int[] Items
            {
                get { return _col.ToArray(); }
            }
        }
    }
}