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

        public ByteOptionList(Use7BinaryReader reader)
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

        public void Write(Use7BinaryWriter writer)
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

    [DebuggerTypeProxy(typeof(DebugView))]
    internal class FormatEntry : IList<FormatValue>, IList
#if IREADONLY
        , IReadOnlyList<FormatValue>
#endif
    {
        private List<FormatValue> _values;
        internal static readonly UTF8Encoding TextEncoding = new UTF8Encoding(false, true);

        #region Constructors
        public FormatEntry(string key, ushort version)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.Length == 0 || TextEncoding.GetByteCount(key) > DieFledermausStream.Max16Bit)
                throw new ArgumentOutOfRangeException(nameof(key), TextResources.ByteLength16BitString);
            if (version == 0)
                throw new ArgumentOutOfRangeException(nameof(version), TextResources.OutOfRangeVersion);

            _key = key;
            _version = version;
            _values = new List<FormatValue>();
        }

        public FormatEntry(string key, ushort version, byte[] value)
            : this(key, version)
        {
            Add(value);
        }

        public FormatEntry(string key, ushort version, string s)
            : this(key, version)
        {
            Add(s);
        }

        public FormatEntry(string key, ushort version, short value)
            : this(key, version)
        {
            Add(value);
        }

        public FormatEntry(string key, ushort version, ushort value)
            : this(key, version)
        {
            Add(value);
        }

        public FormatEntry(string key, ushort version, int value)
            : this(key, version)
        {
            Add(value);
        }

        public FormatEntry(string key, ushort version, uint value)
            : this(key, version)
        {
            Add(value);
        }

        public FormatEntry(string key, ushort version, long value)
            : this(key, version)
        {
            Add(value);
        }

        public FormatEntry(string key, ushort version, ulong value)
            : this(key, version)
        {
            Add(value);
        }

        public FormatEntry(string key, ushort version, float value)
            : this(key, version)
        {
            Add(value);
        }

        public FormatEntry(string key, ushort version, double value)
            : this(key, version)
        {
            Add(value);
        }

        public FormatEntry(string key, ushort version, DateTime value)
            : this(key, version)
        {
            Add(value);
        }


        public FormatEntry(string key, ushort version, Asn1Encodable value)
            : this(key, version)
        {
            Add(value);
        }

        internal FormatEntry(Use7BinaryReader reader)
        {
            try
            {
                _key = TextEncoding.GetString(DieFledermausStream.ReadBytes16Bit(reader));
            }
            catch (DecoderFallbackException)
            {
                throw new InvalidDataException();
            }
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
                        buffer = reader.ReadPrefixedBytes(DieFledermausStream.Max21Bit);
                        break;
                    case FormatValueTypeCode.StringUtf8:
                        buffer = reader.ReadPrefixedBytes(DieFledermausStream.Max21Bit);
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
                        buffer = reader.ReadPrefixedBytes(DieFledermausStream.Max21Bit);
                        Asn1Object o;
                        try
                        {
                            o = Asn1Object.FromByteArray(buffer);
                        }
                        catch
                        {
                            throw new InvalidDataException();
                        }
                        if (o == null) throw new InvalidDataException();
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
        public string Key { get { return _key; } }

        private ushort _version;
        public ushort Version { get { return _version; } }

        public int Count
        {
            get { return _values.Count; }
        }

        public bool IsReadOnly
        {
            get { return _values.Count >= ushort.MaxValue; }
        }

        public FormatValue[] ToArray()
        {
            return _values.ToArray();
        }

        #region Add
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

        public void Add(byte[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            Add(new FormatValue(value));
        }

        public void Add(string s)
        {
            Add(new FormatValue(s));
        }

        public void Add(short value)
        {
            Add(new FormatValue(value));
        }

        public void Add(ushort value)
        {
            Add(new FormatValue(value));
        }

        public void Add(int value)
        {
            Add(new FormatValue(value));
        }

        public void Add(uint value)
        {
            Add(new FormatValue(value));
        }

        public void Add(long value)
        {
            Add(new FormatValue(value));
        }

        public void Add(ulong value)
        {
            Add(new FormatValue(value));
        }

        public void Add(float value)
        {
            Add(new FormatValue(value));
        }

        public void Add(double value)
        {
            Add(new FormatValue(value));
        }

        public void Add(DateTime value)
        {
            Add(new FormatValue(value));
        }

        public void Add(Asn1Encodable value)
        {
            Add(new FormatValue(value));
        }
        #endregion

        #region Insert
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

        public void Insert(int index, byte[] value)
        {
            Insert(index, new FormatValue(value));
        }

        public void Insert(int index, string s)
        {
            Insert(index, new FormatValue(s));
        }

        public void Insert(int index, short value)
        {
            Insert(index, new FormatValue(value));
        }

        public void Insert(int index, ushort value)
        {
            Insert(index, new FormatValue(value));
        }

        public void Insert(int index, int value)
        {
            Insert(index, new FormatValue(value));
        }

        public void Insert(int index, uint value)
        {
            Insert(index, new FormatValue(value));
        }

        public void Insert(int index, long value)
        {
            Insert(index, new FormatValue(value));
        }

        public void Insert(int index, ulong value)
        {
            Insert(index, new FormatValue(value));
        }

        public void Insert(int index, float value)
        {
            Insert(index, new FormatValue(value));
        }

        public void Insert(int index, double value)
        {
            Insert(index, new FormatValue(value));
        }

        public void Insert(int index, DateTime value)
        {
            Insert(index, new FormatValue(value));
        }

        public void Insert(int index, Asn1Encodable value)
        {
            Insert(index, new FormatValue(value));
        }
        #endregion

        public bool Remove(FormatValue value)
        {
            return _values.Remove(value);
        }

        void IList.Remove(object value)
        {
            ((IList)_values).Remove(value);
        }

        public bool Contains(FormatValue value)
        {
            return _values.Contains(value);
        }

        bool IList.Contains(object value)
        {
            return ((IList)_values).Contains(value);
        }

        public int IndexOf(FormatValue value)
        {
            return _values.IndexOf(value);
        }

        int IList.IndexOf(object value)
        {
            return ((IList)_values).IndexOf(value);
        }

        public void RemoveAt(int index)
        {
            _values.RemoveAt(index);
        }

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
                int length = _values[i].Value.Length;
                total += length;

                switch (_values[i].TypeCode)
                {
                    case FormatValueTypeCode.ByteArray:
                    case FormatValueTypeCode.StringUtf8:
                    case FormatValueTypeCode.DerEncoded:
                        total += Use7BinaryWriter.ByteCount(length);
                        break;
                }
            }

            return total;
        }

        public void CopyTo(FormatValue[] array, int arrayIndex)
        {
            _values.CopyTo(array, arrayIndex);
        }

        void ICollection.CopyTo(Array array, int index)
        {
            ((ICollection)_values).CopyTo(array, index);
        }

        internal void Write(Use7BinaryWriter writer)
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
                        writer.Write7BitEncodedInt(curBytes.Length);
                        break;
                }
                writer.Write(curBytes);
            }
        }

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

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "Key = {0}, Version = {1}, Count = {2}", _key, _version, Count);
        }

        bool ICollection.IsSynchronized { get { return false; } }
        object ICollection.SyncRoot { get { return ((ICollection)_values).SyncRoot; } }
        bool IList.IsFixedSize { get { return false; } }

        public struct Enumerator : IEnumerator<FormatValue>
        {
            private IEnumerator<FormatValue> _enum;
            private FormatValue _current;

            internal Enumerator(FormatEntry fVal)
            {
                _enum = fVal._values.GetEnumerator();
                _current = default(FormatValue);
            }

            public FormatValue Current
            {
                get { return _current; }
            }

            object IEnumerator.Current
            {
                get { return _current; }
            }

            public void Dispose()
            {
                this = default(Enumerator);
            }

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

    internal struct FormatValue : IEquatable<FormatValue>
    {
        #region Constructors
        public FormatValue(byte[] value)
            : this(value, FormatValueTypeCode.ByteArray)
        {
        }

        internal FormatValue(byte[] value, FormatValueTypeCode typeCode)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Length > DieFledermausStream.Max21Bit)
                throw new ArgumentOutOfRangeException(nameof(value), TextResources.ByteLength21Bit);
            _value = (byte[])value.Clone();
            _typeCode = typeCode;
        }

        public FormatValue(string s)
        {
            byte[] bytes = DieFledermausStream._textEncoding.GetBytes(s);
            if (bytes.Length > DieFledermausStream.Max21Bit)
                throw new ArgumentOutOfRangeException(nameof(s), TextResources.ByteLength21BitString);
            _value = bytes;
            _typeCode = FormatValueTypeCode.StringUtf8;
        }

        public FormatValue(short value)
        {
            _value = new byte[] { (byte)value, (byte)(value >> 8) };
            _typeCode = FormatValueTypeCode.Int16;
        }

        public FormatValue(ushort value)
        {
            _value = new byte[] { (byte)value, (byte)(value >> 8) };
            _typeCode = FormatValueTypeCode.UInt16;
        }

        public FormatValue(int value)
        {
            _value = new byte[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24) };
            _typeCode = FormatValueTypeCode.Int32;
        }

        public FormatValue(uint value)
        {
            _value = new byte[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24) };
            _typeCode = FormatValueTypeCode.UInt32;
        }

        public FormatValue(long value)
        {
            _value = new byte[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24),
                (byte)(value >> 32), (byte)(value >> 40), (byte)(value >> 48), (byte)(value >> 56) };
            _typeCode = FormatValueTypeCode.Int64;
        }

        public FormatValue(ulong value)
        {
            _value = new byte[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24),
                (byte)(value >> 32), (byte)(value >> 40), (byte)(value >> 48), (byte)(value >> 56) };
            _typeCode = FormatValueTypeCode.UInt64;
        }

        public FormatValue(float value)
            : this(BufferConvert<float, int>(value, sizeof(float)))
        {
            _typeCode = FormatValueTypeCode.Single;
        }

        public FormatValue(Asn1Encodable value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            byte[] buffer = value.GetDerEncoded();
            if (buffer == null || buffer.Length > DieFledermausStream.Max21Bit)
                throw new ArgumentException(TextResources.ByteLength21BitDer, nameof(value));
            _value = buffer;
            _typeCode = FormatValueTypeCode.DerEncoded;
        }

        public FormatValue(double value)
            : this(BufferConvert<double, long>(value, sizeof(double)))
        {
            _typeCode = FormatValueTypeCode.Double;
        }

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
        public byte[] Value
        {
            get
            {
                if (_value == null) return null;
                return (byte[])_value.Clone();
            }
        }

        public string ValueString
        {
            get
            {
                if (_typeCode == FormatValueTypeCode.StringUtf8) return FormatEntry.TextEncoding.GetString(_value);
                return null;
            }
        }

        public short? ValueInt16
        {
            get
            {
                if (_typeCode == FormatValueTypeCode.Int16) return GetValInt16();
                return null;
            }
        }

        public ushort? ValueUInt16
        {
            get
            {
                if (_typeCode == FormatValueTypeCode.UInt16) return (ushort)GetValInt16();
                return null;
            }
        }

        private short GetValInt16() { return (short)(_value[0] | _value[1] << 8); }

        public int? ValueInt32
        {
            get
            {
                if (_typeCode == FormatValueTypeCode.Int32) return GetValInt32();
                return null;
            }
        }
        public uint? ValueUInt32
        {
            get
            {
                if (_typeCode == FormatValueTypeCode.UInt32) return (uint)GetValInt32();
                return null;
            }
        }

        private int GetValInt32() { return _value[0] | _value[1] << 8 | _value[2] << 16 | _value[3] << 24; }

        public long? ValueInt64
        {
            get
            {
                if (_typeCode == FormatValueTypeCode.Int64) return GetValInt64();
                return null;
            }
        }
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

        public float? ValueSingle
        {
            get
            {
                if (_typeCode == FormatValueTypeCode.Single)
                    return BufferConvert<int, float>(GetValInt32(), sizeof(int));
                return null;
            }
        }

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

        public override string ToString()
        {
            if (_value == null)
                return string.Empty;

            object value;
            bool isFloat = false;

            switch (_typeCode)
            {
                default:
                    value = Convert.ToBase64String(_value);
                    break;
                case FormatValueTypeCode.StringUtf8:
                    value = ValueString;
                    break;
                case FormatValueTypeCode.Int16:
                    value = ValueInt16.Value;
                    break;
                case FormatValueTypeCode.UInt16:
                    value = ValueUInt16.Value;
                    break;
                case FormatValueTypeCode.Int32:
                    value = ValueInt32.Value;
                    break;
                case FormatValueTypeCode.UInt32:
                    value = ValueUInt32.Value;
                    break;
                case FormatValueTypeCode.Int64:
                    value = ValueInt64.Value;
                    break;
                case FormatValueTypeCode.UInt64:
                    value = ValueUInt64.Value;
                    break;
                case FormatValueTypeCode.Single:
                    value = ValueSingle.Value;
                    isFloat = true;
                    break;
                case FormatValueTypeCode.Double:
                    value = ValueDouble.Value;
                    isFloat = true;
                    break;
                case FormatValueTypeCode.DateTime:
                    value = ValueDateTime.Value.ToString("o", CultureInfo.InvariantCulture);
                    break;
#if DEBUG
                case FormatValueTypeCode.DerEncoded:
                    value = GetDerObject();
                    break;
#endif
            }

            return string.Format(CultureInfo.InvariantCulture, isFloat ? "{0}: {1:r}" : "{0}: {1}", _typeCode, value);
        }

        #region Equality
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

        public override bool Equals(object obj)
        {
            return obj is FormatValue && Equals((FormatValue)obj);
        }

        public override int GetHashCode()
        {
            if (_value == null) return (byte)_typeCode;
            int total = (byte)_typeCode;
            for (int i = 0; i < _value.Length; i++)
                total += _value[i];
            return total;
        }

        public static bool operator ==(FormatValue b1, FormatValue b2)
        {
            return b1.Equals(b2);
        }

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
