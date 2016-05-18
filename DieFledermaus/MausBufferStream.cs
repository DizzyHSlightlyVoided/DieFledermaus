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
using System.IO;

using DieFledermaus.Globalization;

namespace DieFledermaus
{
    internal partial class MausBufferStream : Stream
    {
        public MausBufferStream()
        {
            _currentBuffer = _firstBuffer = new MiniBuffer();
        }

        private class MiniBuffer
        {
            public MiniBuffer()
            {
                Data = new byte[DieFledermausStream.Max16Bit];
            }

            public MiniBuffer(byte[] buffer)
            {
                Data = buffer;
                End = buffer.Length;
            }

            public byte[] Data;
            public int End;
            public MiniBuffer Next;
        }

        private MiniBuffer _firstBuffer, _currentBuffer;
        private int _currentPos;
        private bool _reading, _closed;

        public override bool CanRead
        {
            get { return !_closed && _reading; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return !_closed && !_reading; }
        }

        private long _length;
        public override long Length
        {
            get
            {
                if (_closed) throw new ObjectDisposedException(null, TextResources.CurrentClosed);
                if (Disposing != null) throw new NotSupportedException(TextResources.CurrentNotSeekable);
                return _length;
            }
        }

        public override long Position
        {
            get
            {
                if (_closed) throw new ObjectDisposedException(null, TextResources.CurrentClosed);
                if (Disposing != null) throw new NotSupportedException(TextResources.CurrentNotSeekable);
                if (_reading) return _position;
                return _length;
            }
            set { throw new NotSupportedException(TextResources.CurrentNotSeekable); }
        }

        private long _position = 0;

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException(TextResources.CurrentNotSeekable);
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException(TextResources.CurrentNotSeekable);
        }

        public void Reset()
        {
            _currentBuffer = _firstBuffer;
            _position = 0;
            _reading = true;
            _closed = false;
        }

        public override void Flush()
        {
            if (_closed) throw new ObjectDisposedException(null, TextResources.CurrentClosed);
        }

        public override int Read(byte[] buffer, int originalOffset, int originalCount)
        {
            if (_closed) throw new ObjectDisposedException(null, TextResources.CurrentClosed);
            if (!_reading) throw new NotSupportedException(TextResources.CurrentWrite);
            DieFledermausStream.CheckSegment(buffer, originalOffset, originalCount);

            int offset = originalOffset, count = originalCount;

            if (_currentBuffer == null)
                return 0;
            int bytesRead = 0;

            while (count > 0 && _currentBuffer != null)
            {
                int bytesToRead = Math.Min(count, _currentBuffer.End - _currentPos);
                Array.Copy(_currentBuffer.Data, _currentPos, buffer, offset, bytesToRead);
                _currentPos += bytesToRead;
                offset += bytesToRead;
                count -= bytesToRead;
                bytesRead += bytesToRead;

                if (_currentPos == _currentBuffer.End)
                {
                    _currentBuffer = _currentBuffer.Next;
                    _currentPos = 0;
                }
            }
            _position += bytesRead;
            return bytesRead;
        }

        public override void Write(byte[] buffer, int originalOffset, int originalCount)
        {
            if (_closed) throw new ObjectDisposedException(null, TextResources.CurrentClosed);
            if (_reading) throw new NotSupportedException(TextResources.CurrentRead);
            DieFledermausStream.CheckSegment(buffer, originalOffset, originalCount);

            int offset = originalOffset, count = originalCount;

            _length += count;

            while (count > 0)
            {
                int bytesToWrite = Math.Min(count, DieFledermausStream.Max16Bit - _currentBuffer.End);
                Array.Copy(buffer, offset, _currentBuffer.Data, _currentBuffer.End, bytesToWrite);
                _currentBuffer.End += bytesToWrite;
                offset += bytesToWrite;
                count -= bytesToWrite;

                if (_currentBuffer.End == DieFledermausStream.Max16Bit)
                {
                    _currentBuffer.Next = new MiniBuffer();
                    _currentBuffer = _currentBuffer.Next;
                }
            }
        }

        internal void BufferCopyTo(Stream destination, bool forceWrite)
        {
            MausBufferStream qbs = destination as MausBufferStream;
            if (qbs == null || forceWrite)
            {
                BufferCopyTo(destination.Write);
                return;
            }

            if (qbs._currentBuffer == qbs._firstBuffer && qbs._firstBuffer.End == 0)
            {
                qbs._firstBuffer = _firstBuffer;
                qbs._length = _length;
            }
            else
            {
                qbs._currentBuffer.Next = _firstBuffer;
                qbs._length += _length;
            }
            qbs.Reset();
            _position = _length;
            _currentBuffer = null;
        }

        internal void BufferCopyTo(WriteDelegate write)
        {
            if (_currentPos != 0)
            {
                write(_currentBuffer.Data, _currentPos, _currentBuffer.End - _currentPos);
                _currentBuffer = _currentBuffer.Next;
                _currentPos = 0;
            }

            while (_currentBuffer != null)
            {
                write(_currentBuffer.Data, 0, _currentBuffer.End);
                _currentBuffer = _currentBuffer.Next;
            }

            _position = _length;
        }

        internal delegate void WriteDelegate(byte[] buffer, int offset, int count);

        internal event EventHandler<DisposeEventArgs> Disposing;

        protected override void Dispose(bool disposing)
        {
            if (_closed)
                return;

            _closed = true;

            if (Disposing != null)
                Disposing(this, new DisposeEventArgs(_length));

            Disposing = null;
            base.Dispose(disposing);
        }

        internal void Prepend(MausBufferStream other)
        {
            _length += other._length;
            if (!_closed && _firstBuffer.End != 0)
                other._currentBuffer.Next = _firstBuffer;
            _firstBuffer = other._firstBuffer;
            Reset();
        }

        internal void Prepend(byte[] buffer)
        {
            _length += buffer.Length;
            MiniBuffer newFirst = new MiniBuffer(buffer);
            newFirst.Next = _firstBuffer;
            _firstBuffer = newFirst;
            _closed = false;
        }
    }

    internal class DisposeEventArgs : EventArgs
    {
        public DisposeEventArgs(long length)
        {
            _length = length;
        }

        private readonly long _length;
        public long Length { get { return _length; } }
    }
}
