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
    /// Base class for collections of settable <see langword="enum"/> values.
    /// </summary>
    /// <typeparam name="TValue">The type of the values in the collection.</typeparam>
    [DebuggerTypeProxy(typeof(MausSettableOptions<>.DebugView))]
    [DebuggerDisplay(DieFledermausStream.CollectionDebuggerDisplay)]
    public abstract class MausSettableOptions<TValue> : ICollection<TValue>, ICollection
#if !NOISET
        , ISet<TValue>
#endif
#if IREADONLY
        , IReadOnlyCollection<TValue>
#endif
        where TValue : struct, IConvertible
    {
        private static readonly HashSet<TValue> _allValues = new HashSet<TValue>((TValue[])Enum.GetValues(typeof(TValue)));

        private HashSet<TValue> _set;

        internal MausSettableOptions()
        {
            _set = new HashSet<TValue>();
        }

        /// <summary>
        /// Gets the number of elements contained in the collection.
        /// </summary>
        public int Count { get { return _set.Count; } }

        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether the current instance is read-only.
        /// </summary>
        /// <remarks>
        /// This property indicates that the collection cannot be changed externally. If <see cref="IsFrozen"/> is <see langword="false"/>,
        /// however, it may still be changed by the internal system.
        /// </remarks>
        public abstract bool IsReadOnly { get; }

        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether the current instance is entirely frozen against all further changes.
        /// </summary>
        public abstract bool IsFrozen { get; }
        bool ICollection.IsSynchronized { get { return IsFrozen; } }

        /// <summary>
        /// Adds the specified value to the collection.
        /// </summary>
        /// <param name="option">The option to add.</param>
        /// <returns><see langword="true"/> if <paramref name="option"/> was successfully added; <see langword="false"/> if <paramref name="option"/>
        /// already exists in the collection, or is not a valid value for type <typeparamref name="TValue"/>.</returns>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public bool Add(TValue option)
        {
            if (IsReadOnly) throw new NotSupportedException(TextResources.CollectReadOnly);
            return _allValues.Contains(option) && _set.Add(option);
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
        /// Adds all valid values to the collection.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void AddAll()
        {
            UnionWith(_allValues);
        }

        internal void DoAddAll()
        {
            _set.UnionWith(_allValues);
        }

        /// <summary>
        /// Removes the specified value from the collection.
        /// </summary>
        /// <param name="option">The option to remove.</param>
        /// <returns><see langword="true"/> if <paramref name="option"/> was found and successfully removed; <see langword="false"/> otherwise.</returns>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public bool Remove(TValue option)
        {
            if (IsReadOnly) throw new NotSupportedException(TextResources.CollectReadOnly);
            return _set.Remove(option);
        }

        /// <summary>
        /// Adds all elements in the specified collection to the current instance (excluding duplicates and values already in the current set).
        /// </summary>
        /// <param name="other">A collection containing other values to add.</param>
        /// <remarks>The number of values which were added.</remarks>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="other"/> is <see langword="null"/>.
        /// </exception>
        public void UnionWith(IEnumerable<TValue> other)
        {
            if (IsReadOnly) throw new NotSupportedException(TextResources.CollectReadOnly);
            if (other == null) throw new ArgumentNullException(nameof(other));

            _set.UnionWith(other.Where(_allValues.Contains));
        }

        /// <summary>
        /// Removes all elements from the current instance except those which already exist in the specified other collection.
        /// </summary>
        /// <param name="other">The other collection to compare.</param>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="other"/> is <see langword="null"/>.
        /// </exception>
        public void IntersectWith(IEnumerable<TValue> other)
        {
            if (IsReadOnly) throw new NotSupportedException(TextResources.CollectReadOnly);
            _set.IntersectWith(other);
        }

        /// <summary>
        /// Removes all elements in the specified collection from the current instance.
        /// </summary>
        /// <param name="other">The other collection to compare.</param>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="other"/> is <see langword="null"/>.
        /// </exception>
        public void ExceptWith(IEnumerable<TValue> other)
        {
            if (IsReadOnly) throw new NotSupportedException(TextResources.CollectReadOnly);
            _set.ExceptWith(other);
        }

        /// <summary>
        /// Modifies the contents of the current instance so that it contains all elements which were either contained in the current instance
        /// or which are contained in the specified other collection, but not both.
        /// </summary>
        /// <param name="other">The other collection to compare.</param>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="other"/> is <see langword="null"/>.
        /// </exception>
        public void SymmetricExceptWith(IEnumerable<TValue> other)
        {
            if (IsReadOnly) throw new NotSupportedException(TextResources.CollectReadOnly);
            _set.SymmetricExceptWith(other.Where(_allValues.Contains));
        }

        /// <summary>
        /// Removes all elements matching the specified predicate from the list.
        /// </summary>
        /// <param name="match">A predicate defining the elements to remove.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="match"/> is <see langword="null"/>.
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
        /// <returns><see langword="true"/> if <paramref name="option"/> was found; <see langword="false"/> otherwise.</returns>
        public bool Contains(TValue option)
        {
            return _set.Contains(option);
        }

        /// <summary>
        /// Gets a value indicating whether the specified collection overlaps with the current instance.
        /// </summary>
        /// <param name="other">The other collection to compare.</param>
        /// <returns><see langword="true"/> if <paramref name="other"/> has any elements in commmon with the current instance;
        /// <see langword="false"/> otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="other"/> is <see langword="null"/>.
        /// </exception>
        public bool Overlaps(IEnumerable<TValue> other)
        {
            return _set.Overlaps(other);
        }

        /// <summary>
        /// Gets a value indicating whether the specified collection has all the same elements as the current instance.
        /// </summary>
        /// <param name="other">The other collection to compare.</param>
        /// <returns><see langword="true"/> if every element in the current instance is also contained in <paramref name="other"/> and vice versa;
        /// <see langword="false"/> otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="other"/> is <see langword="null"/>.
        /// </exception>
        public bool SetEquals(IEnumerable<TValue> other)
        {
            return _set.SetEquals(other);
        }

        /// <summary>
        /// Gets a value indicating whether the current instance is a superset of the specified other collection.
        /// </summary>
        /// <param name="other">The other collection to compare.</param>
        /// <returns><see langword="true"/> if every element in the current instance is also contained in <paramref name="other"/>;
        /// <see langword="false"/> otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="other"/> is <see langword="null"/>.
        /// </exception>
        public bool IsSubsetOf(IEnumerable<TValue> other)
        {
            return _set.IsSubsetOf(other);
        }

        /// <summary>
        /// Gets a value indicating whether the current instance is a subset of the specified other collection.
        /// </summary>
        /// <param name="other">The other collection to compare.</param>
        /// <returns><see langword="true"/> if every element in <paramref name="other"/> is also contained in the current instance;
        /// <see langword="false"/> otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="other"/> is <see langword="null"/>.
        /// </exception>
        public bool IsSupersetOf(IEnumerable<TValue> other)
        {
            return _set.IsSupersetOf(other);
        }

        /// <summary>
        /// Gets a value indicating whether the current instance is a proper superset of the specified other collection.
        /// </summary>
        /// <param name="other">The other collection to compare.</param>
        /// <returns><see langword="true"/> if every element in the current instance is also contained in <paramref name="other"/> AND
        /// <paramref name="other"/> contains at least one element which the current instance does not have; <see langword="false"/> otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="other"/> is <see langword="null"/>.
        /// </exception>
        public bool IsProperSubsetOf(IEnumerable<TValue> other)
        {
            return _set.IsProperSubsetOf(other);
        }

        /// <summary>
        /// Gets a value indicating whether the current instance is a proper subset of the specified other collection.
        /// </summary>
        /// <param name="other">The other collection to compare.</param>
        /// <returns><see langword="true"/> if every element in <paramref name="other"/> is also contained in the current instance AND the current instance
        /// contains at least one element which <paramref name="other"/> does not have; <see langword="false"/> otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="other"/> is <see langword="null"/>.
        /// </exception>
        public bool IsProperSupersetOf(IEnumerable<TValue> other)
        {
            return _set.IsProperSupersetOf(other);
        }

        /// <summary>
        /// Copies all elements in the collection to the specified array, starting at the specified index.
        /// </summary>
        /// <param name="array">The array to which the collection will be copied. The array must have zero-based indexing.</param>
        /// <param name="arrayIndex">The index in <paramref name="array"/> at which copying begins.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="array"/> is <see langword="null"/>.
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

        [NonSerialized]
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

        void ICollection.CopyTo(Array array, int index)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (array.Rank != 1 || array.GetLowerBound(0) != 0)
                throw new ArgumentException(TextResources.CollectBadArray, nameof(array));
            if (index < 0) throw new ArgumentOutOfRangeException(TextResources.OutOfRangeLessThanZero, index, nameof(index));
            if (index + Count > array.Length)
                throw new ArgumentException(TextResources.BadIndexRange);

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
                        array.SetValue(opt, i++);
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
            /// <returns><see langword="true"/> if the enumerator was successfully advanced; 
            /// <see langword="false"/> if the enumerator has passed the end of the collection.</returns>
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
