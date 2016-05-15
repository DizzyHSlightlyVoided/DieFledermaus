using System;
using System.IO;
using System.Text;

namespace DieFledermaus
{
    internal class Use7BinaryWriter : BinaryWriter
    {
        #region Constructors
        public Use7BinaryWriter(Stream output, Encoding encoding, bool leaveOpen)
#if NOLEAVEOPEN
            : base(output, encoding)
        {
            _leaveOpen = leaveOpen;
        }
#else
            : base(output, encoding, leaveOpen)
        {
        }
#endif

        public Use7BinaryWriter(Stream output, bool leaveOpen)
            : this(output, DieFledermausStream._textEncoding, leaveOpen)
        {
        }

        public Use7BinaryWriter(Stream output, Encoding encoding)
            : base(output, encoding)
        {
        }

        public Use7BinaryWriter(Stream output)
            : base(output, DieFledermausStream._textEncoding)
        {
        }
        #endregion

        public new virtual void Write7BitEncodedInt(int value)
        {
            base.Write7BitEncodedInt(value);
        }

        public virtual void WritePrefixedBytes(byte[] array)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            Write7BitEncodedInt(array.Length);
            Write(array);
        }

#if NOLEAVEOPEN
        private bool _leaveOpen;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_leaveOpen)
                    OutStream.Flush();
                else
                    OutStream.Close();
            }
        }
#endif

        internal static int ByteCount(int value)
        {
            if (value < 0 || value > (1 << 28) - 1)
                return 5;
            if (value > (1 << 21) - 1)
                return 4;
            if (value > (1 << 14) - 1)
                return 3;
            if (value > (1 << 7) - 1)
                return 2;

            return 1;
        }
    }

    internal class Use7BinaryReader : BinaryReader
    {
        #region Constructors
        public Use7BinaryReader(Stream input, Encoding encoding, bool leaveOpen)
#if NOLEAVEOPEN
            : base(input, encoding)
        {
            _leaveOpen = leaveOpen;
        }
#else
            : base(input, encoding, leaveOpen)
        {
        }
#endif

        public Use7BinaryReader(Stream input, bool leaveOpen)
            : this(input, DieFledermausStream._textEncoding, leaveOpen)
        {
        }

        public Use7BinaryReader(Stream input, Encoding encoding)
            : base(input, encoding)
        {
        }

        public Use7BinaryReader(Stream input)
            : base(input, DieFledermausStream._textEncoding)
        {
        }
        #endregion

        public new virtual int Read7BitEncodedInt()
        {
            return base.Read7BitEncodedInt();
        }

        public virtual byte[] ReadPrefixedBytes(int maxValue)
        {
            int length = Read7BitEncodedInt();
            if (length < 0 || length > maxValue) throw new InvalidDataException();
            if (length == 0) return new byte[0];
            byte[] buffer = ReadBytes(length);
            if (buffer.Length < length) throw new EndOfStreamException();
            return buffer;
        }

#if NOLEAVEOPEN
        private bool _leaveOpen;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing && !_leaveOpen);
        }
#endif
    }
}
