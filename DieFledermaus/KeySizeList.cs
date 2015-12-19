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
    public sealed class KeySizeList : IList<int>
#if IREADONLY
        , IReadOnlyList<int>
#endif
    {
        private int[] _items;

        #region Constructors
        /// <summary>
        /// Creates a new instance using elements copied from the specified collection.
        /// </summary>
        /// <param name="source">A collection whose elements will be copied to the new instance.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="source"/> is <c>null</c>.
        /// </exception>
        public KeySizeList(IEnumerable<int> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _items = new HashSet<int>(source).ToArray();
            Array.Sort(_items, 0, _items.Length);

            if (_items.Length == 0)
                return;

            if (_items.Length == 1)
            {
                _min = _max = _items[0];
                return;
            }

            _min = _items[0];
            _max = _items[_items.Length - 1];

            int prev = _items[1];
            _skip = prev - _items[0];

            for (int i = 2; _skip != 0 && i < _items.Length; prev = _items[i], i++)
            {
                if (_items[i] - prev != _skip)
                    _skip = 0;
            }
        }

        /// <summary>
        /// Creates a new instance using elements copied from the specified collection.
        /// </summary>
        /// <param name="source">A collection whose elements will be copied to the new instance.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="source"/> is <c>null</c>.
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
            {
                _skip = _max - _min;
                _items = new int[] { _min, _max };
            }
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
                    throw new ArgumentOutOfRangeException(nameof(index), index, TextResources.OutOfRangeIndex);
                return _items[index];
            }
        }

        int IList<int>.this[int index]
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

        private int _min;
        /// <summary>
        /// Gets the minimum value in the collection.
        /// </summary>
        public int MinSize { get { return _min; } }

        private int _max;
        /// <summary>
        /// Gets the maximum value in the collection.
        /// </summary>
        public int MaxSize { get { return _max; } }

        private int _skip;
        /// <summary>
        /// Gets the interval between each element in the list, or 0 if the interval is not fixed.
        /// </summary>
        public int SkipSize { get { return _skip; } }

        /// <summary>
        /// Determines if the specified value exists in the list.
        /// </summary>
        /// <param name="value">The value to search for in the list.</param>
        /// <returns><c>true</c> if <paramref name="value"/> was found; <c>false</c> otherwise.</returns>
        public bool Contains(int value)
        {
            return IndexOf(value) >= 0;
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
            if (value < _min || value > _max)
                return -1;
            for (int i = 0; value <= _items[i]; i++)
            {
                int curVal = _items[i];
                if (value == curVal)
                    return i;
                if (value > curVal)
                    return -1;
            }
            return -1;
        }

        /// <summary>
        /// Copies all elements in the current instance to the specified array.
        /// </summary>
        /// <param name="destinationArray">The array to which the current instance will be copied.</param>
        /// <param name="destinationIndex">The index in <paramref name="destinationArray"/> at which copying begins.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="destinationArray"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="destinationIndex"/> plus <see cref="Count"/> is greater than the number of elements in <paramref name="destinationArray"/>.
        /// </exception>
        public void CopyTo(int[] destinationArray, int destinationIndex)
        {
            var items = _items ?? new int[0];
            Array.Copy(items, 0, destinationArray, destinationIndex, items.Length);
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

        bool ICollection<int>.Remove(int item)
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }

        void ICollection<int>.Clear()
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }

        bool ICollection<int>.IsReadOnly
        {
            get { return true; }
        }

        void IList<int>.Insert(int index, int item)
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }

        void IList<int>.RemoveAt(int index)
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }
        #endregion

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
            /// <returns><c>true</c> if the enumerator was successfully advanced; <c>false</c> if the
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