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

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace DieFledermaus
{
    [DebuggerDisplay(DieFledermausStream.CollectionDebuggerDisplay)]
    [DebuggerTypeProxy(typeof(DebugView))]
    internal class ByteOptionList : ICollection<byte[]>
#if IREADONLY
        , IReadOnlyCollection<byte[]>
#endif
    {
        private List<byte[]> _items = new List<byte[]>();

        bool ICollection<byte[]>.IsReadOnly
        {
            get { return false; }
        }

        public int Count
        {
            get { return _items.Count; }
        }

        public void Add(byte[] value)
        {
            _items.Add(value);
        }

        public void Add(string value)
        {
            byte[] b = new byte[value.Length];
            for (int i = 0; i < value.Length; i++)
                b[i] = (byte)value[i];

            _items.Add(b);
        }

        public void Add(short value)
        {
            Add(new byte[] { (byte)value, (byte)(value >> 8) });
        }

        public void Add(long value)
        {
            Add(new byte[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24),
                (byte)(value >> 32), (byte)(value >> 40), (byte)(value >> 48), (byte)(value >> 56) });
        }

        public void Clear()
        {
            _items.Clear();
        }

        public bool Contains(byte[] item)
        {
            return _items.Contains(item);
        }

        public void CopyTo(byte[][] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }

        public bool Remove(byte[] item)
        {
            return _items.Remove(item);
        }

        public IEnumerator<byte[]> GetEnumerator()
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
            {
                var item = _items[i];
                writer.Write((ushort)item.Length);
                writer.Write(item);
            }
        }

        public long GetSize()
        {
            long result = 0;
            for (int i = 0; i < _items.Count; i++)
                result += 2L + _items[i].LongLength;
            return result;
        }

        public byte[][] ToArray()
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
            public byte[][] Items
            {
                get { return _optionList.ToArray(); }
            }
        }
    }
}
