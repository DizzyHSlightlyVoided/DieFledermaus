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
    /// Base class for collections of settable options.
    /// </summary>
    [DebuggerTypeProxy(typeof(MausSettableOptions<>.DebugView))]
    [DebuggerDisplay(DieFledermausStream.CollectionDebuggerDisplay)]
    public abstract class MausSettableOptions<TValue> : ICollection<TValue>, ICollection
#if IREADONLY
        , IReadOnlyCollection<TValue>
#endif
        where TValue : struct, IConvertible
    {
        private HashSet<TValue> _set;

        IMausCrypt _owner;

        internal MausSettableOptions(IMausCrypt owner)
        {
            _set = new HashSet<TValue>();
            _owner = owner;
        }

        /// <summary>
        /// Gets the number of elements contained in the collection.
        /// </summary>
        public int Count { get { return _set.Count; } }

        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether the current instance is read-only.
        /// </summary>
        /// <remarks>
        /// This property indicates that the collection cannot be changed externally. If <see cref="IsFrozen"/> is <c>false</c>,
        /// however, it may still be changed by the internal system.
        /// </remarks>
        public abstract bool IsReadOnly { get; }

        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether the current instance is entirely frozen against all further changes.
        /// </summary>
        public abstract bool IsFrozen { get; }
        bool ICollection.IsSynchronized { get { return IsFrozen; } }

        /// <summary>
        /// When overridden in a derived class, indicates whether the specified value is valid.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <returns><c>true</c> if <paramref name="value"/> is a valid value for type <typeparamref name="TValue"/>; <c>false</c> otherwise.</returns>
        protected abstract bool IsValid(TValue value);

        /// <summary>
        /// Adds the specified value to the collection.
        /// </summary>
        /// <param name="option">The option to add.</param>
        /// <returns><c>true</c> if <paramref name="option"/> was successfully added; <c>false</c> if <paramref name="option"/>
        /// already exists in the collection, or is not a valid value for type <typeparamref name="TValue"/>.</returns>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <c>true</c>.
        /// </exception>
        public bool Add(TValue option)
        {
            if (IsReadOnly) throw new NotSupportedException(TextResources.CollectReadOnly);
            return IsValid(option) && _set.Add(option);
        }

        internal bool InternalAdd(TValue option)
        {
            return _set.Add(option);
        }

        void ICollection<TValue>.Add(TValue item)
        {
            Add(item);
        }

        /// <summary>
        /// When overridden in a derived class, adds all values for type <typeparamref name="TValue"/> to the collection.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <c>true</c>.
        /// </exception>
        public abstract void AddAll();

        /// <summary>
        /// Removes the specified value from the collection.
        /// </summary>
        /// <param name="option">The option to remove.</param>
        /// <returns><c>true</c> if <paramref name="option"/> was found and successfully removed; <c>false</c> otherwise.</returns>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <c>true</c>.
        /// </exception>
        public bool Remove(TValue option)
        {
            if (IsReadOnly) throw new NotSupportedException(TextResources.CollectReadOnly);
            return _set.Remove(option);
        }

        /// <summary>
        /// Adds all elements in the specified collection to the current instance (excluding duplicates and values already in the current collection).
        /// </summary>
        /// <param name="other">A collection containing other values to add.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="other"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <c>true</c>.
        /// </exception>
        public void AddRange(IEnumerable<TValue> other)
        {
            if (IsReadOnly) throw new NotSupportedException(TextResources.CollectReadOnly);
            _set.UnionWith(other);
        }

        /// <summary>
        /// Removes all elements matching the specified predicate from the list.
        /// </summary>
        /// <param name="match">A predicate defining the elements to remove.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="match"/> is <c>null</c>.
        /// </exception>
        public void RemoveWhere(Predicate<TValue> match)
        {
            _set.RemoveWhere(match);
        }

        /// <summary>
        /// Removes all elements from the collection.
        /// </summary>
        public void Clear()
        {
            if (IsReadOnly) throw new NotSupportedException(TextResources.CollectReadOnly);
            _set.Clear();
        }

        /// <summary>
        /// Determines if the specified value exists in the collection.
        /// </summary>
        /// <param name="option">The option to search for in the collection.</param>
        /// <returns><c>true</c> if <paramref name="option"/> was found; <c>false</c> otherwise.</returns>
        public bool Contains(TValue option)
        {
            return _set.Contains(option);
        }

        /// <summary>
        /// Copies all elements in the collection to the specified array, starting at the specified index.
        /// </summary>
        /// <param name="array">The array to which the collection will be copied. The array must have zero-based indexing.</param>
        /// <param name="arrayIndex">The index in <paramref name="array"/> at which copying begins.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="array"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="arrayIndex"/> is less than 0.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="arrayIndex"/> plus <see cref="Count"/> is greater than the length of <paramref name="array"/>.
        /// </exception>
        public void CopyTo(TValue[] array, int arrayIndex)
        {
            _set.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Returns an enumerator which iterates through the collection.
        /// </summary>
        /// <returns>An enumerator which iterates through the collection.</returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        object ICollection.SyncRoot
        {
            get { return _owner.SyncRoot; }
        }

        void ICollection.CopyTo(Array array, int index)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (array.Rank != 1 || array.GetLowerBound(0) != 0)
                throw new ArgumentException(TextResources.CollectBadArray, nameof(array));
            if (index < 0) throw new ArgumentOutOfRangeException(TextResources.OutOfRangeLessThanZero, index, nameof(index));

            TValue[] mArray = array as TValue[];

            if (mArray != null)
            {
                _set.CopyTo(mArray, index);
                return;
            }

            try
            {
                object[] oArray = array as object[];
                int i = index;

                if (oArray == null)
                {
                    foreach (TValue opt in _set)
                        mArray.SetValue(opt, i++);
                }
                else
                {
                    foreach (TValue opt in _set)
                        oArray[i++] = opt;
                }
            }
            catch (InvalidCastException x)
            {
                throw new ArgumentException(TextResources.CollectBadArrayType, nameof(array), x);
            }
        }

        /// <summary>
        /// An enumerator which iterates through the collection.
        /// </summary>
        public struct Enumerator : IEnumerator<TValue>
        {
            private IEnumerator<TValue> _enum;

            internal Enumerator(MausSettableOptions<TValue> sOpts)
            {
                _enum = sOpts._set.GetEnumerator();
                _current = default(TValue);
            }

            private TValue _current;
            /// <summary>
            /// Gets the element at the current position in the enumerator.
            /// </summary>
            public TValue Current
            {
                get { return _current; }
            }

            object IEnumerator.Current
            {
                get { return _enum.Current; }
            }

            /// <summary>
            /// Disposes of the current instance.
            /// </summary>
            public void Dispose()
            {
                if (_enum == null) return;
                _enum.Dispose();
                this = default(Enumerator);
            }

            /// <summary>
            /// Advances the enumerator to the next position in the collection.
            /// </summary>
            /// <returns><c>true</c> if the enumerator was successfully advanced; 
            /// <c>false</c> if the enumerator has passed the end of the collection.</returns>
            public bool MoveNext()
            {
                if (_enum == null) return false;
                if (!_enum.MoveNext())
                {
                    Dispose();
                    return false;
                }
                _current = _enum.Current;
                return true;
            }

            void IEnumerator.Reset()
            {
                _enum.Reset();
            }
        }

        private class DebugView
        {
            private MausSettableOptions<TValue> _col;

            public DebugView(MausSettableOptions<TValue> col)
            {
                _col = col;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public TValue[] Items
            {
                get { return _col.ToArray(); }
            }
        }
    }
}
