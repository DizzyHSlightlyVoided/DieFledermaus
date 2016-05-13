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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DieFledermaus.Globalization;
using Org.BouncyCastle.Asn1;

namespace DieFledermaus
{
    [DebuggerDisplay(DieFledermausStream.CollectionDebuggerDisplay)]
    [DebuggerTypeProxy(typeof(DebugView))]
    internal class ByteOptionList : ICollection<FormatEntry>
#if IREADONLY
        , IReadOnlyCollection<FormatEntry>
#endif
    {
        private List<FormatEntry> _items;

        #region Constructors
        public ByteOptionList()
        {
            _items = new List<FormatEntry>();
        }

        public ByteOptionList(BinaryReader reader)
        {
            int itemCount = reader.ReadUInt16();

            _items = new List<FormatEntry>(itemCount);

            for (int i = 0; i < itemCount; i++)
                _items.Add(new FormatEntry(reader));
        }
        #endregion

        public bool IsReadOnly
        {
            get { return _items.Count >= ushort.MaxValue; }
        }

        public int Count
        {
            get { return _items.Count; }
        }

        #region Add
        public void Add(FormatEntry value)
        {
            if (IsReadOnly)
                throw new NotSupportedException();
            _items.Add(value);
        }

        public void Add(string key, ushort version)
        {
            Add(new FormatEntry(key, version));
        }

        public void Add(string key, ushort version, byte[] value)
        {
            Add(new FormatEntry(key, version, value));
        }

        public void Add(string key, ushort version, string s)
        {
            Add(new FormatEntry(key, version, s));
        }

        public void Add(string key, ushort version, short value)
        {
            Add(new FormatEntry(key, version, value));
        }

        public void Add(string key, ushort version, ushort value)
        {
            Add(new FormatEntry(key, version, value));
        }

        public void Add(string key, ushort version, int value)
        {
            Add(new FormatEntry(key, version, value));
        }

        public void Add(string key, ushort version, uint value)
        {
            Add(new FormatEntry(key, version, value));
        }

        public void Add(string key, ushort version, long value)
        {
            Add(new FormatEntry(key, version, value));
        }

        public void Add(string key, ushort version, ulong value)
        {
            Add(new FormatEntry(key, version, value));
        }

        public void Add(string key, ushort version, float value)
        {
            Add(new FormatEntry(key, version, value));
        }

        public void Add(string key, ushort version, double value)
        {
            Add(new FormatEntry(key, version, value));
        }

        public void Add(string key, ushort version, DateTime value)
        {
            Add(new FormatEntry(key, version, value));
        }

        public void Add(string key, ushort version, Asn1Encodable value)
        {
            Add(new FormatEntry(key, version, value));
        }
        #endregion

        public void Clear()
        {
            _items.Clear();
        }

        public bool Contains(FormatEntry item)
        {
            return _items.Contains(item);
        }

        public void CopyTo(FormatEntry[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }

        public bool Remove(FormatEntry item)
        {
            return _items.Remove(item);
        }

        public IEnumerator<FormatEntry> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write((ushort)_items.Count);
            for (int i = 0; i < _items.Count; i++)
                _items[i].Write(writer);
        }

        public long GetSize()
        {
            long result = 0;
            for (int i = 0; i < _items.Count; i++)
                result += _items[i].GetSize();
            return result;
        }

        public FormatEntry[] ToArray()
        {
            return _items.ToArray();
        }

        private class DebugView
        {
            private ByteOptionList _optionList;

            public DebugView(ByteOptionList optionList)
            {
                _optionList = optionList;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public FormatEntry[] Items
            {
                get { return _optionList.ToArray(); }
            }
        }
    }

    /// <summary>
    /// Represents all elements in a format-list.
    /// </summary>
    [DebuggerTypeProxy(typeof(DebugView))]
    internal class FormatEntry : IList<FormatValue>, IList
#if IREADONLY
        , IReadOnlyList<FormatValue>
#endif
    {
        private List<FormatValue> _values;
        internal static readonly UTF8Encoding TextEncoding = new UTF8Encoding(false, true);

        #region Constructors
        /// <summary>
        /// Creates a new instance with the specified key and version.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="version">The current version of the byte value.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="key"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <para><paramref name="key"/> has a length of 0 or is greater than 65536 UTF-8 bytes.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="version"/> is 0.</para>
        /// </exception>
        public FormatEntry(string key, ushort version)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.Length == 0 || TextEncoding.GetByteCount(key) > DieFledermausStream.Max16Bit)
                throw new ArgumentOutOfRangeException(nameof(key), TextResources.CommentLength);
            if (version == 0)
                throw new ArgumentOutOfRangeException(nameof(version), TextResources.OutOfRangeVersion);

            _key = key;
            _version = version;
            _values = new List<FormatValue>();
        }

        /// <summary>
        /// Creates a new instance with the specified key, version, and initial value.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="version">The current version of the byte value.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="key"/> or <paramref name="value"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <para><paramref name="key"/> has a length of 0 or greater than 65536 UTF-8 bytes.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="value"/> has a length of 0 or greater than 65536.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="version"/> is 0.</para>
        /// </exception>
        public FormatEntry(string key, ushort version, byte[] value)
            : this(key, version)
        {
            Add(value);
        }

        /// <summary>
        /// Creates a new instance with the specified key, version, and initial value.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="version">The current version of the byte value.</param>
        /// <param name="s">The value to add.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="key"/> or <paramref name="s"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <para><paramref name="key"/> or <paramref name="s"/> has a length of 0 or greater than 65536 UTF-8 bytes.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="version"/> is 0.</para>
        /// </exception>
        /// <exception cref="EncoderFallbackException">
        /// <paramref name="s"/> contains invalid characters.
        /// </exception>
        public FormatEntry(string key, ushort version, string s)
            : this(key, version)
        {
            Add(s);
        }

        /// <summary>
        /// Creates a new instance with the specified key, version, and initial value.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="version">The current version of the byte value.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="key"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <para><paramref name="key"/> has a length of 0 or greater than 65536 UTF-8 bytes.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="version"/> is 0.</para>
        /// </exception>
        public FormatEntry(string key, ushort version, short value)
            : this(key, version)
        {
            Add(value);
        }

        /// <summary>
        /// Creates a new instance with the specified key, version, and initial value.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="version">The current version of the byte value.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="key"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <para><paramref name="key"/> has a length of 0 or greater than 65536 UTF-8 bytes.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="version"/> is 0.</para>
        /// </exception>
        public FormatEntry(string key, ushort version, ushort value)
            : this(key, version)
        {
            Add(value);
        }

        /// <summary>
        /// Creates a new instance with the specified key, version, and initial value.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="version">The current version of the byte value.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="key"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <para><paramref name="key"/> has a length of 0 or greater than 65536 UTF-8 bytes.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="version"/> is 0.</para>
        /// </exception>
        public FormatEntry(string key, ushort version, int value)
            : this(key, version)
        {
            Add(value);
        }

        /// <summary>
        /// Creates a new instance with the specified key, version, and initial value.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="version">The current version of the byte value.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="key"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <para><paramref name="key"/> has a length of 0 or greater than 65536 UTF-8 bytes.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="version"/> is 0.</para>
        /// </exception>
        public FormatEntry(string key, ushort version, uint value)
            : this(key, version)
        {
            Add(value);
        }

        /// <summary>
        /// Creates a new instance with the specified key, version, and initial value.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="version">The current version of the byte value.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="key"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <para><paramref name="key"/> has a length of 0 or greater than 65536 UTF-8 bytes.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="version"/> is 0.</para>
        /// </exception>
        public FormatEntry(string key, ushort version, long value)
            : this(key, version)
        {
            Add(value);
        }

        /// <summary>
        /// Creates a new instance with the specified key, version, and initial value.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="version">The current version of the byte value.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="key"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <para><paramref name="key"/> has a length of 0 or greater than 65536 UTF-8 bytes.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="version"/> is 0.</para>
        /// </exception>
        public FormatEntry(string key, ushort version, ulong value)
            : this(key, version)
        {
            Add(value);
        }

        /// <summary>
        /// Creates a new instance with the specified key, version, and initial value.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="version">The current version of the byte value.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="key"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <para><paramref name="key"/> has a length of 0 or greater than 65536 UTF-8 bytes.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="version"/> is 0.</para>
        /// </exception>
        public FormatEntry(string key, ushort version, float value)
            : this(key, version)
        {
            Add(value);
        }

        /// <summary>
        /// Creates a new instance with the specified key, version, and initial value.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="version">The current version of the byte value.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="key"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <para><paramref name="key"/> has a length of 0 or greater than 65536 UTF-8 bytes.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="version"/> is 0.</para>
        /// </exception>
        public FormatEntry(string key, ushort version, double value)
            : this(key, version)
        {
            Add(value);
        }

        /// <summary>
        /// Creates a new instance with the specified key, version, and initial value.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="version">The current version of the byte value.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="key"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <para><paramref name="key"/> has a length of 0 or greater than 65536 UTF-8 bytes.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="version"/> is 0.</para>
        /// </exception>
        public FormatEntry(string key, ushort version, DateTime value)
            : this(key, version)
        {
            Add(value);
        }


        /// <summary>
        /// Creates a new instance with the specified key, version, and initial value.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="version">The current version of the byte value.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="key"/> or <paramref name="value"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <para><paramref name="key"/> has a length of 0 or greater than 65536 UTF-8 bytes.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="version"/> is 0.</para>
        /// </exception>
        public FormatEntry(string key, ushort version, Asn1Encodable value)
            : this(key, version)
        {
            Add(value);
        }

        internal FormatEntry(BinaryReader reader)
        {
            int strLen = reader.ReadUInt16();
            if (strLen == 0) strLen = DieFledermausStream.Max16Bit;
            _key = TextEncoding.GetString(DieFledermausStream.ReadBytes(reader, strLen));
            _version = reader.ReadUInt16();
            if (_version == 0)
                throw new InvalidDataException();
            int itemCount = reader.ReadUInt16();

            _values = new List<FormatValue>(itemCount);

            for (int i = 0; i < itemCount; i++)
            {
                FormatValueTypeCode code = (FormatValueTypeCode)reader.ReadByte();
                byte[] buffer;

                switch (code)
                {
                    case FormatValueTypeCode.ByteArray:
                        buffer = DieFledermausStream.ReadBytes16Bit(reader);
                        break;
                    case FormatValueTypeCode.StringUtf8:
                        buffer = DieFledermausStream.ReadBytes16Bit(reader);
                        try
                        {
                            TextEncoding.GetString(buffer);
                        }
                        catch (DecoderFallbackException)
                        {
                            throw new InvalidDataException();
                        }
                        break;
                    case FormatValueTypeCode.DerEncoded:
                        buffer = DieFledermausStream.ReadBytes16Bit(reader);
                        try
                        {
                            Asn1Object.FromByteArray(buffer);
                        }
                        catch
                        {
                            throw new InvalidDataException();
                        }
                        break;
                    case FormatValueTypeCode.DateTime:
                        try
                        {
                            _values.Add(new FormatValue(new DateTime(reader.ReadInt64(), DateTimeKind.Utc)));
                            continue;
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            throw new InvalidDataException();
                        }
                    case FormatValueTypeCode.Int16:
                    case FormatValueTypeCode.UInt16:
                        buffer = DieFledermausStream.ReadBytes(reader, sizeof(short));
                        break;
                    case FormatValueTypeCode.Int32:
                    case FormatValueTypeCode.UInt32:
                    case FormatValueTypeCode.Single:
                        buffer = DieFledermausStream.ReadBytes(reader, sizeof(int));
                        break;
                    case FormatValueTypeCode.Int64:
                    case FormatValueTypeCode.UInt64:
                    case FormatValueTypeCode.Double:
                        buffer = DieFledermausStream.ReadBytes(reader, sizeof(long));
                        break;
                    default:
                        throw new InvalidDataException();
                }

                _values.Add(new FormatValue(buffer, code));
            }
        }
        #endregion

        /// <summary>
        /// Gets and sets the element at the specified index.
        /// </summary>
        /// <param name="index">The index of the element to get or set.</param>
        /// <returns>The element at <paramref name="index"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than 0 or is greater than or equal to <see cref="Count"/>.
        /// </exception>
        public FormatValue this[int index]
        {
            get { return _values[index]; }
            set { _values[index] = value; }
        }

        object IList.this[int index]
        {
            get { return _values[index]; }
            set { ((IList)_values)[index] = value; }
        }

        private string _key;
        /// <summary>
        /// Gets the key of the current entry.
        /// </summary>
        public string Key { get { return _key; } }

        private ushort _version;
        /// <summary>
        /// Gets the version number of the current entry.
        /// </summary>
        public ushort Version { get { return _version; } }

        /// <summary>
        /// Gets the number of elements contained in the list.
        /// </summary>
        public int Count
        {
            get { return _values.Count; }
        }

        /// <summary>
        /// Gets a value indicating whether <see cref="Count"/> is equal to the maximum value of 65535.
        /// </summary>
        public bool IsReadOnly
        {
            get { return _values.Count >= ushort.MaxValue; }
        }

        /// <summary>
        /// Returns an array containing all elements in the new list.
        /// </summary>
        /// <returns>An array containing all elements in the new list.</returns>
        public FormatValue[] ToArray()
        {
            return _values.ToArray();
        }

        #region Add
        /// <summary>
        /// Adds the specified value to the list.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentException">
        /// The <see cref="FormatValue.Value"/> property is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Add(FormatValue value)
        {
            Insert(_values.Count, value);
        }

        int IList.Add(object value)
        {
            if (IsReadOnly) return -1;
            int newDex = _values.Count;

            ((IList)this).Insert(_values.Count, value);
            return newDex;
        }

        /// <summary>
        /// Adds the specified value to the list.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="value"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="value"/> has a length of 0 or greater than 65536.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Add(byte[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            Add(new FormatValue(value));
        }

        /// <summary>
        /// Adds the specified value to the list.
        /// </summary>
        /// <param name="s">The value to add.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="s"/> has a length of 0 or greater than 65536 UTF-8 bytes.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="s"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="EncoderFallbackException">
        /// <paramref name="s"/> contains invalid characters.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Add(string s)
        {
            Add(new FormatValue(s));
        }

        /// <summary>
        /// Adds the specified value to the list.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Add(short value)
        {
            Add(new FormatValue(value));
        }

        /// <summary>
        /// Adds the specified value to the list.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Add(ushort value)
        {
            Add(new FormatValue(value));
        }

        /// <summary>
        /// Adds the specified value to the list.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Add(int value)
        {
            Add(new FormatValue(value));
        }

        /// <summary>
        /// Adds the specified value to the list.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Add(uint value)
        {
            Add(new FormatValue(value));
        }

        /// <summary>
        /// Adds the specified value to the list.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Add(long value)
        {
            Add(new FormatValue(value));
        }

        /// <summary>
        /// Adds the specified value to the list.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Add(ulong value)
        {
            Add(new FormatValue(value));
        }

        /// <summary>
        /// Adds the specified value to the list.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Add(float value)
        {
            Add(new FormatValue(value));
        }

        /// <summary>
        /// Adds the specified value to the list.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Add(double value)
        {
            Add(new FormatValue(value));
        }

        /// <summary>
        /// Adds the specified value to the list.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Add(DateTime value)
        {
            Add(new FormatValue(value));
        }

        /// <summary>
        /// Adds the specified value to the list.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="value"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Add(Asn1Encodable value)
        {
            Add(new FormatValue(value));
        }
        #endregion

        #region Insert
        /// <summary>
        /// Inserts the specified value into the list at the specified index.
        /// </summary>
        /// <param name="index">The index of the value to add.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than 0 or is greater than <see cref="Count"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The <see cref="FormatValue.Value"/> property is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Insert(int index, FormatValue value)
        {
            if (value.Value == null)
                throw new ArgumentException(string.Format(TextResources.PropertyNull, nameof(FormatValue) + "." + nameof(FormatValue.Value)), nameof(value));

            if (IsReadOnly)
                throw new NotSupportedException(TextResources.ByteListAtMaximum);

            _values.Insert(index, value);
        }

        void IList.Insert(int index, object value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            if (IsReadOnly)
                throw new NotSupportedException(TextResources.ByteListAtMaximum);

            ((IList)_values).Insert(index, value);
        }

        /// <summary>
        /// Inserts the specified value into the list at the specified index.
        /// </summary>
        /// <param name="index">The index of the value to add.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <para><paramref name="index"/> is less than 0 or is greater than <see cref="Count"/>.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="value"/> has a length of 0 or greater than 65536.</para>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="value"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Insert(int index, byte[] value)
        {
            Insert(index, new FormatValue(value));
        }

        /// <summary>
        /// Inserts the specified value into the list at the specified index.
        /// </summary>
        /// <param name="index">The index of the value to add.</param>
        /// <param name="s">The value to add.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <para><paramref name="index"/> is less than 0 or is greater than <see cref="Count"/>.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="s"/> has a length of 0 or greater than 65536 UTF-8 bytes.</para>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="s"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="EncoderFallbackException">
        /// <paramref name="s"/> contains invalid characters.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Insert(int index, string s)
        {
            Insert(index, new FormatValue(s));
        }

        /// <summary>
        /// Inserts the specified value into the list at the specified index.
        /// </summary>
        /// <param name="index">The index of the value to add.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than 0 or is greater than <see cref="Count"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Insert(int index, short value)
        {
            Insert(index, new FormatValue(value));
        }

        /// <summary>
        /// Inserts the specified value into the list at the specified index.
        /// </summary>
        /// <param name="index">The index of the value to add.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than 0 or is greater than <see cref="Count"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Insert(int index, ushort value)
        {
            Insert(index, new FormatValue(value));
        }

        /// <summary>
        /// Inserts the specified value into the list at the specified index.
        /// </summary>
        /// <param name="index">The index of the value to add.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than 0 or is greater than <see cref="Count"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Insert(int index, int value)
        {
            Insert(index, new FormatValue(value));
        }

        /// <summary>
        /// Inserts the specified value into the list at the specified index.
        /// </summary>
        /// <param name="index">The index of the value to add.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than 0 or is greater than <see cref="Count"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Insert(int index, uint value)
        {
            Insert(index, new FormatValue(value));
        }

        /// <summary>
        /// Inserts the specified value into the list at the specified index.
        /// </summary>
        /// <param name="index">The index of the value to add.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than 0 or is greater than <see cref="Count"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Insert(int index, long value)
        {
            Insert(index, new FormatValue(value));
        }

        /// <summary>
        /// Inserts the specified value into the list at the specified index.
        /// </summary>
        /// <param name="index">The index of the value to add.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than 0 or is greater than <see cref="Count"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Insert(int index, ulong value)
        {
            Insert(index, new FormatValue(value));
        }

        /// <summary>
        /// Inserts the specified value into the list at the specified index.
        /// </summary>
        /// <param name="index">The index of the value to add.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than 0 or is greater than <see cref="Count"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Insert(int index, float value)
        {
            Insert(index, new FormatValue(value));
        }

        /// <summary>
        /// Inserts the specified value into the list at the specified index.
        /// </summary>
        /// <param name="index">The index of the value to add.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than 0 or is greater than <see cref="Count"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Insert(int index, double value)
        {
            Insert(index, new FormatValue(value));
        }

        /// <summary>
        /// Inserts the specified value into the list at the specified index.
        /// </summary>
        /// <param name="index">The index of the value to add.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than 0 or is greater than <see cref="Count"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Insert(int index, DateTime value)
        {
            Insert(index, new FormatValue(value));
        }

        /// <summary>
        /// Inserts the specified value into the list at the specified index.
        /// </summary>
        /// <param name="index">The index of the value to add.</param>
        /// <param name="value">The value to add.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than 0 or is greater than <see cref="Count"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="value"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="IsReadOnly"/> is <see langword="true"/>.
        /// </exception>
        public void Insert(int index, Asn1Encodable value)
        {
            Insert(index, new FormatValue(value));
        }
        #endregion

        /// <summary>
        /// Removes the specified element from the list.
        /// </summary>
        /// <param name="value">The element to remove from the list.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> was found and successfully removed; <see langword="false"/> otherwise.</returns>
        public bool Remove(FormatValue value)
        {
            return _values.Remove(value);
        }

        void IList.Remove(object value)
        {
            ((IList)_values).Remove(value);
        }

        /// <summary>
        /// Gets a value indicating whether the specified element exists in the list.
        /// </summary>
        /// <param name="value">The element to search for in the list.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> was found; <see langword="false"/> otherwise.</returns>
        public bool Contains(FormatValue value)
        {
            return _values.Contains(value);
        }

        bool IList.Contains(object value)
        {
            return ((IList)_values).Contains(value);
        }

        /// <summary>
        /// Returns the index in the list of the specified value.
        /// </summary>
        /// <param name="value">The value to search for in the list.</param>
        /// <returns>The index of <paramref name="value"/>, if found; otherwise, -1.</returns>
        public int IndexOf(FormatValue value)
        {
            return _values.IndexOf(value);
        }

        int IList.IndexOf(object value)
        {
            return ((IList)_values).IndexOf(value);
        }

        /// <summary>
        /// Removes the element at the specified index.
        /// </summary>
        /// <param name="index">The index of the element to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than 0 or is greater than or equal to <see cref="Count"/>.
        /// </exception>
        public void RemoveAt(int index)
        {
            _values.RemoveAt(index);
        }

        /// <summary>
        /// Removes all elements in the list.
        /// </summary>
        public void Clear()
        {
            _values.Clear();
        }

        internal long GetSize()
        {
            long total = 6L //String prefix + version + count
                + TextEncoding.GetByteCount(_key);

            for (int i = 0; i < _values.Count; i++)
            {
                total++; //1-byte code
                switch (_values[i].TypeCode)
                {
                    case FormatValueTypeCode.ByteArray:
                    case FormatValueTypeCode.StringUtf8:
                    case FormatValueTypeCode.DerEncoded:
                        total += sizeof(ushort);
                        break;
                }
                total += _values[i].Value.Length;
            }

            return total;
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
        public void CopyTo(FormatValue[] array, int arrayIndex)
        {
            _values.CopyTo(array, arrayIndex);
        }

        void ICollection.CopyTo(Array array, int index)
        {
            ((ICollection)_values).CopyTo(array, index);
        }

        internal void Write(BinaryWriter writer)
        {
            byte[] keyBytes = TextEncoding.GetBytes(_key);
            writer.Write((ushort)keyBytes.Length);
            writer.Write(keyBytes);
            writer.Write(_version);
            int count = Count;
            writer.Write((ushort)count);
            var values = _values;
            for (int i = 0; i < count; i++)
            {
                var curVal = values[i];
                var curBytes = curVal.Value;
                writer.Write((byte)curVal.TypeCode);
                switch (curVal.TypeCode)
                {
                    case FormatValueTypeCode.ByteArray:
                    case FormatValueTypeCode.StringUtf8:
                    case FormatValueTypeCode.DerEncoded:
                        writer.Write((ushort)curBytes.Length);
                        break;
                }
                writer.Write(curBytes);
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

        IEnumerator<FormatValue> IEnumerable<FormatValue>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Returns a string representation of the current instance.
        /// </summary>
        /// <returns>A string representation of the current instance.</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "Key = {0}, Version = {1}, Count = {2}", _key, _version, Count);
        }

        bool ICollection.IsSynchronized { get { return false; } }
        object ICollection.SyncRoot { get { return ((ICollection)_values).SyncRoot; } }
        bool IList.IsFixedSize { get { return false; } }

        /// <summary>
        /// An enumerator which iterates through the list.
        /// </summary>
        public struct Enumerator : IEnumerator<FormatValue>
        {
            private IEnumerator<FormatValue> _enum;
            private FormatValue _current;

            internal Enumerator(FormatEntry fVal)
            {
                _enum = fVal._values.GetEnumerator();
                _current = default(FormatValue);
            }

            /// <summary>
            /// Gets the element at the current position in the enumerator.
            /// </summary>
            public FormatValue Current
            {
                get { return _current; }
            }

            object IEnumerator.Current
            {
                get { return _current; }
            }

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
                if (_enum == null) return false;
                if (_enum.MoveNext())
                {
                    _current = _enum.Current;
                    return true;
                }
                Dispose();
                return false;
            }

            void IEnumerator.Reset()
            {
                _enum.Reset();
            }
        }

        private class DebugView
        {
            private FormatEntry _optionList;

            public DebugView(FormatEntry optionList)
            {
                _optionList = optionList;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public FormatValue[] Items
            {
                get { return _optionList.ToArray(); }
            }
        }
    }

    /// <summary>
    /// Represents a single element of a format value.
    /// </summary>
    internal struct FormatValue : IEquatable<FormatValue>
    {
        #region Constructors
        /// <summary>
        /// Creates a new instance using the specified binary value.
        /// </summary>
        /// <param name="value">The value to set.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="value"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="value"/> has a length of 0 or greater than 65536.
        /// </exception>
        public FormatValue(byte[] value)
            : this(value, FormatValueTypeCode.ByteArray)
        {
        }

        internal FormatValue(byte[] value, FormatValueTypeCode typeCode)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Length == 0 || value.Length > DieFledermausStream.Max16Bit)
                throw new ArgumentOutOfRangeException(nameof(value), TextResources.OutOfRangeByteLength);
            _value = (byte[])value.Clone();
            _typeCode = typeCode;
        }

        /// <summary>
        /// Creates a new instance using the specified value.
        /// </summary>
        /// <param name="s">The value to set.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="s"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="s"/> has a length of 0 or greater than 65536 UTF-8 bytes.
        /// </exception>
        /// <exception cref="EncoderFallbackException">
        /// <paramref name="s"/> contains invalid characters.
        /// </exception>
        public FormatValue(string s)
        {
            if (FormatEntry.TextEncoding.GetByteCount(s) > DieFledermausStream.Max16Bit || s.Length == 0)
                throw new ArgumentOutOfRangeException(nameof(s), TextResources.CommentLength);
            _value = FormatEntry.TextEncoding.GetBytes(s);
            _typeCode = FormatValueTypeCode.StringUtf8;
        }

        /// <summary>
        /// Creates a new instance using the specified value.
        /// </summary>
        /// <param name="value">The value to set.</param>
        public FormatValue(short value)
        {
            _value = new byte[] { (byte)value, (byte)(value >> 8) };
            _typeCode = FormatValueTypeCode.Int16;
        }

        /// <summary>
        /// Creates a new instance using the specified value.
        /// </summary>
        /// <param name="value">The value to set.</param>
        public FormatValue(ushort value)
        {
            _value = new byte[] { (byte)value, (byte)(value >> 8) };
            _typeCode = FormatValueTypeCode.UInt16;
        }

        /// <summary>
        /// Creates a new instance using the specified value.
        /// </summary>
        /// <param name="value">The value to set.</param>
        public FormatValue(int value)
        {
            _value = new byte[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24) };
            _typeCode = FormatValueTypeCode.Int32;
        }

        /// <summary>
        /// Creates a new instance using the specified value.
        /// </summary>
        /// <param name="value">The value to set.</param>
        public FormatValue(uint value)
        {
            _value = new byte[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24) };
            _typeCode = FormatValueTypeCode.UInt32;
        }

        /// <summary>
        /// Creates a new instance using the specified value.
        /// </summary>
        /// <param name="value">The value to set.</param>
        public FormatValue(long value)
        {
            _value = new byte[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24),
                (byte)(value >> 32), (byte)(value >> 40), (byte)(value >> 48), (byte)(value >> 56) };
            _typeCode = FormatValueTypeCode.Int64;
        }

        /// <summary>
        /// Creates a new instance using the specified value.
        /// </summary>
        /// <param name="value">The value to set.</param>
        public FormatValue(ulong value)
        {
            _value = new byte[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24),
                (byte)(value >> 32), (byte)(value >> 40), (byte)(value >> 48), (byte)(value >> 56) };
            _typeCode = FormatValueTypeCode.UInt64;
        }

        /// <summary>
        /// Creates a new instance using the specified value.
        /// </summary>
        /// <param name="value">The value to set.</param>
        public FormatValue(float value)
            : this(BufferConvert<float, int>(value, sizeof(float)))
        {
            _typeCode = FormatValueTypeCode.Single;
        }

        public FormatValue(Asn1Encodable value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            byte[] buffer = value.GetDerEncoded();
            if (buffer == null || buffer.Length == 0 || buffer.Length > DieFledermausStream.Max16Bit)
                throw new InvalidOperationException(); //TODO: Message
            _value = buffer;
            _typeCode = FormatValueTypeCode.DerEncoded;
        }

        /// <summary>
        /// Creates a new instance using the specified value.
        /// </summary>
        /// <param name="value">The value to set.</param>
        public FormatValue(double value)
            : this(BufferConvert<double, long>(value, sizeof(double)))
        {
            _typeCode = FormatValueTypeCode.Double;
        }

        /// <summary>
        /// Creates a new instance using the specified value.
        /// </summary>
        /// <param name="value">The value to set.</param>
        public FormatValue(DateTime value)
            : this(value.ToUniversalTime().Ticks)
        {
            _typeCode = FormatValueTypeCode.DateTime;
        }

        private static TOut BufferConvert<TIn, TOut>(TIn value, int size)
        {
            TIn[] inBuffer = new TIn[] { value };
            TOut[] outBuffer = new TOut[1];
            Buffer.BlockCopy(inBuffer, 0, outBuffer, 0, size);
            return outBuffer[0];
        }
        #endregion

        private FormatValueTypeCode _typeCode;
        public FormatValueTypeCode TypeCode { get { return _typeCode; } }

        #region Values
        private byte[] _value;
        /// <summary>
        /// Gets the binary value of the current instance.
        /// </summary>
        public byte[] Value
        {
            get
            {
                if (_value == null) return null;
                return (byte[])_value.Clone();
            }
        }

        /// <summary>
        /// Gets the UTF-8 string representation of <see cref="Value"/>, or <see langword="null"/> if <see cref="TypeCode"/>
        /// is not <see cref="FormatValueTypeCode.StringUtf8"/>.
        /// </summary>
        public string ValueString
        {
            get
            {
                if (_typeCode == FormatValueTypeCode.StringUtf8) return FormatEntry.TextEncoding.GetString(_value);
                return null;
            }
        }

        /// <summary>
        /// Gets a signed 16-bit integer representation of <see cref="Value"/>, or <see langword="null"/> if <see cref="TypeCode"/>
        /// is not <see cref="FormatValueTypeCode.Int16"/>.
        /// </summary>
        public short? ValueInt16
        {
            get
            {
                if (_typeCode == FormatValueTypeCode.Int16) return GetValInt16();
                return null;
            }
        }

        /// <summary>
        /// Gets an unsigned 16-bit integer representation of <see cref="Value"/>, or <see langword="null"/> if <see cref="TypeCode"/>
        /// is not <see cref="FormatValueTypeCode.UInt16"/>.
        /// </summary>
        public ushort? ValueUInt16
        {
            get
            {
                if (_typeCode == FormatValueTypeCode.UInt16) return (ushort)GetValInt16();
                return null;
            }
        }

        private short GetValInt16() { return (short)(_value[0] | _value[1] << 8); }

        /// <summary>
        /// Gets a signed 32-bit integer representation of <see cref="Value"/>, or <see langword="null"/> if <see cref="TypeCode"/>
        /// is not <see cref="FormatValueTypeCode.Int32"/>.
        /// </summary>
        public int? ValueInt32
        {
            get
            {
                if (_typeCode == FormatValueTypeCode.Int32) return GetValInt32();
                return null;
            }
        }
        /// <summary>
        /// Gets an unsigned 32-bit integer representation of <see cref="Value"/>, or <see langword="null"/> if <see cref="TypeCode"/>
        /// is not <see cref="FormatValueTypeCode.UInt32"/>.
        /// </summary>
        public uint? ValueUInt32
        {
            get
            {
                if (_typeCode == FormatValueTypeCode.UInt32) return (uint)GetValInt32();
                return null;
            }
        }

        private int GetValInt32() { return _value[0] | _value[1] << 8 | _value[2] << 16 | _value[3] << 24; }

        /// <summary>
        /// Gets a signed 64-bit integer representation of <see cref="Value"/>, or <see langword="null"/> if <see cref="TypeCode"/>
        /// is not <see cref="FormatValueTypeCode.Int64"/>.
        /// </summary>
        public long? ValueInt64
        {
            get
            {
                if (_typeCode == FormatValueTypeCode.Int64) return GetValInt64();
                return null;
            }
        }
        /// <summary>
        /// Gets an unsigned 64-bit integer representation of <see cref="Value"/>, or <see langword="null"/> if <see cref="TypeCode"/>
        /// is not <see cref="FormatValueTypeCode.UInt64"/>.
        /// </summary>
        public ulong? ValueUInt64
        {
            get
            {
                if (_typeCode == FormatValueTypeCode.UInt64) return (ulong)GetValInt64();
                return null;
            }
        }

        private long GetValInt64()
        {
            return _value[0] | ((long)_value[1] << 8) | ((long)_value[2] << 16) | ((long)_value[3] << 24) |
                ((long)_value[4] << 32) | ((long)_value[5] << 40) | ((long)_value[6] << 48) | ((long)_value[7] << 56);
        }

        /// <summary>
        /// Gets a single-precision floating-point value representation of <see cref="Value"/>, or <see langword="null"/> if <see cref="TypeCode"/>
        /// is not <see cref="FormatValueTypeCode.Single"/>.
        /// </summary>
        public float? ValueSingle
        {
            get
            {
                if (_typeCode == FormatValueTypeCode.Single)
                    return BufferConvert<int, float>(GetValInt32(), sizeof(int));
                return null;
            }
        }

        /// <summary>
        /// Gets a double-precision floating-point value representation of <see cref="Value"/>, or <see langword="null"/> if <see cref="TypeCode"/>
        /// is not <see cref="FormatValueTypeCode.Double"/>.
        /// </summary>
        public double? ValueDouble
        {
            get
            {
                if (_typeCode == FormatValueTypeCode.Double)
                    return BufferConvert<long, double>(GetValInt64(), sizeof(int));
                return null;
            }
        }

        public DateTime? ValueDateTime
        {
            get
            {
                if (_typeCode == FormatValueTypeCode.DateTime) return new DateTime(GetValInt64(), DateTimeKind.Utc);
                return null;
            }
        }

        public Asn1Object GetDerObject()
        {
            if (_typeCode == FormatValueTypeCode.DerEncoded) return Asn1Object.FromByteArray(_value);
            return null;
        }
        #endregion

        /// <summary>
        /// Returns a string representation of the current value.
        /// </summary>
        /// <returns>A string representation of the current value.</returns>
        public override string ToString()
        {
            if (_value == null)
                return string.Empty;

            switch (_typeCode)
            {
                default:
                    return string.Concat(_value.Select(i => i.ToString("x2", NumberFormatInfo.InvariantInfo)).ToArray());
                case FormatValueTypeCode.StringUtf8:
                    return ValueString;
                case FormatValueTypeCode.Int16:
                    return ValueInt16.Value.ToString(NumberFormatInfo.InvariantInfo);
                case FormatValueTypeCode.UInt16:
                    return ValueUInt16.Value.ToString(NumberFormatInfo.InvariantInfo);
                case FormatValueTypeCode.Int32:
                    return ValueInt32.Value.ToString(NumberFormatInfo.InvariantInfo);
                case FormatValueTypeCode.UInt32:
                    return ValueUInt32.Value.ToString(NumberFormatInfo.InvariantInfo);
                case FormatValueTypeCode.Int64:
                    return ValueInt64.Value.ToString(NumberFormatInfo.InvariantInfo);
                case FormatValueTypeCode.UInt64:
                    return ValueUInt64.Value.ToString(NumberFormatInfo.InvariantInfo);
                case FormatValueTypeCode.Single:
                    return ValueSingle.Value.ToString("r", NumberFormatInfo.InvariantInfo);
                case FormatValueTypeCode.Double:
                    return ValueDouble.Value.ToString("r", NumberFormatInfo.InvariantInfo);
                case FormatValueTypeCode.DateTime:
                    return ValueDateTime.Value.ToString("o", CultureInfo.InvariantCulture);
            }
        }

        #region Equality
        /// <summary>
        /// Determines if the current value is equal to the specified other <see cref="FormatValue"/> object.
        /// </summary>
        /// <param name="other">The other <see cref="FormatValue"/> to compare.</param>
        /// <returns><see langword="true"/> if the current instance is equal to <paramref name="other"/>; <see langword="false"/> otherwise.</returns>
        public bool Equals(FormatValue other)
        {
            if (_value == other._value) return true;
            if (_typeCode != other._typeCode) return false;

            int length = (_value == null) ? 0 : _value.Length;
            int otherLength = other._value == null ? 0 : other._value.Length;

            if (length != otherLength) return false;

            for (int i = 0; i < length; i++)
            {
                if (_value[i] != other._value[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Determines if the current value is equal to the specified other object.
        /// </summary>
        /// <param name="obj">The other object to compare.</param>
        /// <returns><see langword="true"/> if <paramref name="obj"/> is a <see cref="FormatValue"/> equal to the current instance;
        /// <see langword="false"/> otherwise.</returns>
        public override bool Equals(object obj)
        {
            return obj is FormatValue && Equals((FormatValue)obj);
        }

        /// <summary>
        /// Returns the hash code for the current value.
        /// </summary>
        /// <returns>The hash code for the current value.</returns>
        public override int GetHashCode()
        {
            if (_value == null) return (byte)_typeCode;
            int total = (byte)_typeCode;
            for (int i = 0; i < _value.Length; i++)
                total += _value[i];
            return total;
        }

        /// <summary>
        /// Determines equality of two <see cref="FormatValue"/> objects.
        /// </summary>
        /// <param name="b1">A <see cref="FormatValue"/> to compare.</param>
        /// <param name="b2">A <see cref="FormatValue"/> to compare.</param>
        /// <returns><see langword="true"/> if <paramref name="b1"/> is equal to <paramref name="b2"/>; <see langword="false"/> otherwise.</returns>
        public static bool operator ==(FormatValue b1, FormatValue b2)
        {
            return b1.Equals(b2);
        }

        /// <summary>
        /// Determines inequality of two <see cref="FormatValue"/> objects.
        /// </summary>
        /// <param name="b1">A <see cref="FormatValue"/> to compare.</param>
        /// <param name="b2">A <see cref="FormatValue"/> to compare.</param>
        /// <returns><see langword="true"/> if <paramref name="b1"/> is not equal to <paramref name="b2"/>; <see langword="false"/> otherwise.</returns>
        public static bool operator !=(FormatValue b1, FormatValue b2)
        {
            return !b1.Equals(b2);
        }
        #endregion
    }

    internal enum FormatValueTypeCode : byte
    {
        ByteArray,
        StringUtf8,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Single,
        Double,
        DateTime,
        DerEncoded
    }
}
