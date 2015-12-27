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

namespace DieFledermaus
{
    [DebuggerDisplay(DieFledermausStream.CollectionDebuggerDisplay)]
    [DebuggerTypeProxy(typeof(DebugView))]
    internal class ByteOptionList : ICollection<FormatValue>
#if IREADONLY
        , IReadOnlyCollection<FormatValue>
#endif
    {
        private List<FormatValue> _items;

        public ByteOptionList()
        {
            _items = new List<FormatValue>();
        }

        public ByteOptionList(BinaryReader reader)
        {
            int itemCount = reader.ReadUInt16();

            _items = new List<FormatValue>(itemCount);

            for (int i = 0; i < itemCount; i++)
                _items.Add(new FormatValue(reader));
        }

        public bool IsReadOnly
        {
            get { return _items.Count >= ushort.MaxValue; }
        }

        public int Count
        {
            get { return _items.Count; }
        }

        public void Add(FormatValue value)
        {
            _add(value);
        }

        private void _add(FormatValue value)
        {
            if (IsReadOnly)
                throw new NotSupportedException();
            _items.Add(value);
        }

        public void Add(string key, ushort version)
        {
            _add(new FormatValue(key, version));
        }

        public void Add(string key, ushort version, byte[] value)
        {
            _add(new FormatValue(key, version, value));
        }

        public void Add(string key, ushort version, string value)
        {
            FormatValue formatValue = new FormatValue(key, version);
            formatValue.Add(value);
            _add(formatValue);
        }

        public void Add(string key, ushort version, ushort value)
        {
            FormatValue formatValue = new FormatValue(key, version);
            formatValue.Add(value);
            _add(formatValue);
        }

        public void Add(string key, ushort version, long value)
        {
            FormatValue formatValue = new FormatValue(key, version);
            formatValue.Add(value);
            _add(formatValue);
        }

        public void Clear()
        {
            _items.Clear();
        }

        public bool Contains(FormatValue item)
        {
            return _items.Contains(item);
        }

        public void CopyTo(FormatValue[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }

        public bool Remove(FormatValue item)
        {
            return _items.Remove(item);
        }

        public IEnumerator<FormatValue> GetEnumerator()
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

        public FormatValue[] ToArray()
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
            public FormatValue[] Items
            {
                get { return _optionList.ToArray(); }
            }
        }
    }

    [DebuggerTypeProxy(typeof(DebugView))]
    internal class FormatValue : IList<FormatValue.ByteValue>
#if IREADONLY
        , IReadOnlyList<FormatValue.ByteValue>
#endif
    {
        private List<ByteValue> _values;
        private static UTF8Encoding _textEncoding = new UTF8Encoding(false, true);

        public FormatValue(string key, ushort version)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.Length == 0 || _textEncoding.GetByteCount(key) > DieFledermausStream.Max16Bit)
                throw new ArgumentOutOfRangeException(nameof(key));
            if (version == 0)
                throw new ArgumentOutOfRangeException(nameof(version));

            _key = key;
            _version = version;
            _values = new List<ByteValue>();
        }

        public FormatValue(string key, ushort version, byte[] value)
            : this(key, version)
        {
            Add(value);
        }

        public FormatValue(BinaryReader reader)
        {
            int strLen = reader.ReadUInt16();
            if (strLen == 0) strLen = DieFledermausStream.Max16Bit;
            _key = _textEncoding.GetString(DieFledermausStream.ReadBytes(reader, strLen));
            _version = reader.ReadUInt16();
            if (_version == 0)
                throw new InvalidDataException();
            int itemCount = reader.ReadUInt16();

            _values = new List<ByteValue>(itemCount);

            for (int i = 0; i < itemCount; i++)
            {
                int curCount = reader.ReadUInt16();
                if (curCount == 0) curCount = DieFledermausStream.Max16Bit;

                _values.Add(new ByteValue(DieFledermausStream.ReadBytes(reader, curCount)));
            }
        }

        public ByteValue this[int index]
        {
            get { return _values[index]; }
            set { _values[index] = value; }
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

        private static byte[][] Copy(byte[][] existing)
        {
            if (existing == null) return new byte[0][];

            byte[][] copy = new byte[existing.Length][];

            for (int i = 0; i < existing.Length; i++)
                copy[i] = (byte[])existing[i].Clone();

            return copy;
        }

        public ByteValue[] ToArray()
        {
            return _values.ToArray();
        }

        public void Add(ByteValue value)
        {
            if (value.Value == null)
                throw new ArgumentNullException(nameof(value));

            if (IsReadOnly)
                throw new NotSupportedException();

            _values.Add(value);
        }

        public void Add(byte[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            Add(new ByteValue(value));
        }

        public void Add(string s)
        {
            Add(new ByteValue(_textEncoding.GetBytes(s)));
        }

        public void Add(ushort value)
        {
            Add(new ByteValue(new byte[] { (byte)value, (byte)(value >> 8) }));
        }

        public void Add(long value)
        {
            Add(new byte[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24),
                (byte)(value >> 32), (byte)(value >> 40), (byte)(value >> 48), (byte)(value >> 56) });
        }

        public void Insert(int index, ByteValue value)
        {
            if (value.Value == null)
                throw new ArgumentNullException(nameof(value));

            if (IsReadOnly)
                throw new NotSupportedException();

            _values.Insert(index, value);
        }

        public bool Remove(ByteValue value)
        {
            return _values.Remove(value);
        }

        public bool Contains(ByteValue value)
        {
            return _values.Contains(value);
        }

        public int IndexOf(ByteValue value)
        {
            return _values.IndexOf(value);
        }

        public void RemoveAt(int index)
        {
            _values.RemoveAt(index);
        }

        public void Clear()
        {
            _values.Clear();
        }

        public long GetSize()
        {
            long total = 6L //String prefix + version + count
                + _textEncoding.GetByteCount(_key);

            for (int i = 0; i < _values.Count; i++)
                total += 2L + _values[i].Value.Length;

            return total;
        }

        public void CopyTo(ByteValue[] array, int arrayIndex)
        {
            _values.CopyTo(array, arrayIndex);
        }

        public void Write(BinaryWriter writer)
        {
            byte[] keyBytes = _textEncoding.GetBytes(_key);
            writer.Write((ushort)keyBytes.Length);
            writer.Write(keyBytes);
            writer.Write(_version);
            int count = Count;
            writer.Write((ushort)count);
            var values = _values;
            for (int i = 0; i < count; i++)
            {
                var curBytes = values[i].Value;
                writer.Write((ushort)curBytes.Length);
                writer.Write(curBytes);
            }
        }

        public IEnumerator<ByteValue> GetEnumerator()
        {
            return _values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _values.GetEnumerator();
        }

        public override string ToString()
        {
            return string.Format("Key = {0}, Version = {1}, Count = {2}", _key, _version, Count);
        }

        internal struct ByteValue : IEquatable<ByteValue>
        {
            public ByteValue(byte[] value)
            {
                _value = value;
            }

            private byte[] _value;
            public byte[] Value { get { return _value; } }

            public string ValueString
            {
                get
                {
                    if (_value == null) return null;
                    try
                    {
                        return _textEncoding.GetString(_value);
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            public ushort? ValueUInt16
            {
                get
                {
                    if (_value == null || _value.Length != sizeof(ushort))
                        return null;
                    return (ushort)(_value[0] | (_value[1] << 8));
                }
            }

            public long? ValueInt64
            {
                get
                {
                    if (_value == null || _value.Length != sizeof(long)) return null;

                    return _value[0] | ((long)_value[1] << 8) | ((long)_value[2] << 16) | ((long)_value[3] << 24) |
                        ((long)_value[4] << 32) | ((long)_value[5] << 40) | ((long)_value[6] << 48) | ((long)_value[7] << 56);
                }
            }

            public override string ToString()
            {
                if (_value == null)
                    return base.ToString();

                string s = ValueString;
                if (s != null && ((_value.Length != sizeof(ushort) && _value.Length != sizeof(long)) || !s.Any(c => char.IsControl(c) && !char.IsWhiteSpace(c))))
                    return s;

                ushort? ui16 = ValueUInt16;
                if (ui16.HasValue)
                    return ui16.Value.ToString(NumberFormatInfo.InvariantInfo);

                long? i64 = ValueInt64;
                if (i64.HasValue)
                    return i64.Value.ToString(NumberFormatInfo.InvariantInfo);

                return string.Concat(_value.Select(i => i.ToString("x2", NumberFormatInfo.InvariantInfo)).ToArray());
            }

            #region Equality
            public bool Equals(ByteValue other)
            {
                if (_value == other._value) return true;
                int length = (_value == null) ? 0 : _value.Length;

                if ((length != 0 && other._value == null) || length != other._value.Length)
                    return false;

                for (int i = 0; i < length; i++)
                {
                    if (_value[i] != other._value[i])
                        return false;
                }
                return true;
            }

            public override bool Equals(object obj)
            {
                return obj is ByteValue && Equals((ByteValue)obj);
            }

            public override int GetHashCode()
            {
                if (_value == null) return 0;
                int total = 0;
                for (int i = 0; i < _value.Length; i++)
                    total += _value[i];
                return total;
            }

            public static bool operator ==(ByteValue b1, ByteValue b2)
            {
                return b1.Equals(b2);
            }

            public static bool operator !=(ByteValue b1, ByteValue b2)
            {
                return !b1.Equals(b2);
            }
            #endregion
        }

        private class DebugView
        {
            private FormatValue _optionList;

            public DebugView(FormatValue optionList)
            {
                _optionList = optionList;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public ByteValue[] Items
            {
                get { return _optionList.ToArray(); }
            }
        }
    }
}
