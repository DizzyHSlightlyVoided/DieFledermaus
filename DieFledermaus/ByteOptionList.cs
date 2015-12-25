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
using System.IO;
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
            if (!value.IsValid)
                throw new ArgumentException();
            _add(value);
        }

        private void _add(FormatValue value)
        {
            if (IsReadOnly)
                throw new NotSupportedException();
            _items.Add(value);
        }

        public void Add(string key, ushort version, params byte[][] values)
        {
            _add(new FormatValue(key, version, values));
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

        public void Add(BinaryReader reader)
        {
            if (IsReadOnly)
                throw new NotSupportedException();
            _items.Add(new FormatValue(reader));
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

    internal struct FormatValue : IEquatable<FormatValue>, ICloneable
    {
        private static UTF8Encoding _textEncoding = new UTF8Encoding(false, true);

        public FormatValue(string key, ushort version, params byte[][] values)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (_textEncoding.GetByteCount(key) > DieFledermausStream.Max16Bit)
                throw new ArgumentOutOfRangeException(nameof(key));
            if (version == 0)
                throw new ArgumentOutOfRangeException(nameof(version));

            _key = key;
            _version = version;
            _values = Copy(values);
        }

        public FormatValue(BinaryReader reader)
        {
            int strLen = reader.ReadUInt16();
            if (strLen == 0) strLen = DieFledermausStream.Max16Bit;
            _key = DieFledermausStream._textEncoding.GetString(DieFledermausStream.ReadBytes(reader, strLen));
            _version = reader.ReadUInt16();
            if (_version == 0)
                throw new InvalidDataException();
            int itemCount = reader.ReadUInt16();

            _values = new byte[itemCount][];

            for (int i = 0; i < itemCount; i++)
            {
                int curCount = reader.ReadUInt16();
                if (curCount == 0) curCount = DieFledermausStream.Max16Bit;

                _values[i] = DieFledermausStream.ReadBytes(reader, curCount);
            }
        }

        public object Clone()
        {
            return new FormatValue()
            {
                _key = _key,
                _version = _version,
                _values = Copy(_values)
            };
        }

        private string _key;
        public string Key { get { return _key; } }

        private ushort _version;
        public ushort Version { get { return _version; } }

        private byte[][] _values;

        public int Count
        {
            get
            {
                if (_values == null) return 0;
                return _values.Length;
            }
        }

        public bool IsValid
        {
            get { return !string.IsNullOrEmpty(_key) && _version != 0; }
        }

        private static byte[][] Copy(byte[][] existing)
        {
            if (existing == null) return new byte[0][];

            if (existing.Length > DieFledermausStream.Max16Bit)
                throw new ArgumentException();

            byte[][] copy = new byte[existing.Length][];


            for (int i = 0; i < existing.Length; i++)
                copy[i] = (byte[])existing[i].Clone();

            return copy;
        }

        public byte[][] ToArray()
        {
            return Copy(_values);
        }

        public void Add(byte[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (Count >= DieFledermausStream.Max16Bit)
                throw new NotSupportedException();

            int oldDex = Count;

            if (_values == null)
                _values = new byte[1][];
            else
                Array.Resize(ref _values, oldDex + 1);

            _values[oldDex] = value;
        }

        public byte[] GetValue(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _values[index];
        }

        public void Add(string s)
        {
            Add(_textEncoding.GetBytes(s));
        }

        public string GetValueString(int index)
        {
            try
            {
                return _textEncoding.GetString(GetValue(index));
            }
            catch (DecoderFallbackException)
            {
                return null;
            }
        }

        public void Add(ushort value)
        {
            Add(new byte[] { (byte)value, (byte)(value >> 8) });
        }

        public ushort? GetValueUInt16(int index)
        {
            byte[] value = GetValue(index);

            if (value.Length != sizeof(ushort))
                return null;

            return (ushort)(value[0] | (value[1] << 8));
        }

        public void Add(long value)
        {
            Add(new byte[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24),
                (byte)(value >> 32), (byte)(value >> 40), (byte)(value >> 48), (byte)(value >> 56) });
        }

        public long? GetValueInt64(int index)
        {
            byte[] value = GetValue(index);

            if (value == null || value.Length != sizeof(long))
                return null;

            return value[0] | ((long)value[1] << 8) | ((long)value[2] << 16) | ((long)value[3] << 24) |
                ((long)value[4] << 32) | ((long)value[5] << 40) | ((long)value[6] << 48) | ((long)value[7] << 56);
        }

        public long GetSize()
        {
            long total = 6L //String prefix + version + count
                + DieFledermausStream._textEncoding.GetByteCount(_key);

            int count = Count;

            byte[][] values = _values;

            for (int i = 0; i < count; i++)
                total += 2L + values[i].Length;

            return total;
        }

        public void Write(BinaryWriter writer)
        {
            byte[] keyBytes = DieFledermausStream._textEncoding.GetBytes(_key);
            writer.Write((ushort)keyBytes.Length);
            writer.Write(keyBytes);
            writer.Write(_version);
            int count = Count;
            writer.Write((ushort)count);
            var values = _values;
            for (int i = 0; i < count; i++)
            {
                writer.Write((ushort)values[i].Length);
                writer.Write(values[i]);
            }
        }

        public override string ToString()
        {
            return string.Format("{0}, version {1}, byte[{2}][]", _key, _version, Count);
        }

        #region Equality
        public bool Equals(FormatValue other)
        {
            if (!string.Equals(_key, other._key, StringComparison.Ordinal) || _version != other._version)
                return false;
            int count = Count;
            if (count != other.Count)
                return false;

            byte[][] val1 = _values, val2 = other._values;

            if (val1 == val2) return true;

            for (int i = 0; i < count; i++)
            {
                if (val1[i] == val2[i])
                    continue;
                int curCount = val1[i] == null ? 0 : val1[i].Length;

                if (curCount != (val2[i] == null ? 0 : val2[i].Length))
                    return false;

                for (int j = 0; j < curCount; j++)
                {
                    if (val1[i][j] != val2[i][j])
                        return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is FormatValue && Equals((FormatValue)obj);
        }

        public override int GetHashCode()
        {
            int count = Count;
            if (Count == 0) return 0;

            int total = 0;

            var values = _values;

            for (int i = 0; i < count; i++)
            {
                var curVal = values[i];
                if (curVal == null) continue;

                for (int j = 0; j < curVal.Length; j++)
                    total += curVal[j];
            }

            return total;
        }
        #endregion
    }
}
