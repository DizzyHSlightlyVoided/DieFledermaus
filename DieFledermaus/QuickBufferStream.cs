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
    internal partial class QuickBufferStream : Stream
    {
        public QuickBufferStream()
        {
            _currentBuffer = _firstBuffer = new MiniBuffer();
        }

        private class MiniBuffer
        {
            public byte[] Data = new byte[DieFledermausStream.MaxBuffer];
            public int End;
            public MiniBuffer Next;
        }

        private MiniBuffer _firstBuffer, _currentBuffer;
        private int _currentPos;
        private bool _reading;

        public override bool CanRead
        {
            get { return _firstBuffer != null; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return _firstBuffer != null && !_reading; }
        }

        private long _length;
        public override long Length
        {
            get { return _length; }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public void Reset()
        {
            _currentBuffer = _firstBuffer;
            _currentPos = 0;
            _reading = true;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_firstBuffer == null) throw new ObjectDisposedException(null);
            if (!_reading) Reset();
#if DEBUG
            int oldCount = count, oldOffset = offset;
#endif
            if (_currentBuffer == null) return 0;
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
                    if (_currentBuffer == null) break;
                    _currentPos = 0;
                }
            }

            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_firstBuffer == null) throw new ObjectDisposedException(null);
            if (_reading) throw new NotSupportedException(TextResources.CurrentRead);
#if DEBUG
            int oldCount = count, oldOffset = offset;
#endif
            while (count > 0)
            {
                int bytesToWrite = Math.Min(count, DieFledermausStream.MaxBuffer - _currentBuffer.End);
                Array.Copy(buffer, offset, _currentBuffer.Data, _currentBuffer.End, bytesToWrite);
                _currentPos = (_currentBuffer.End += bytesToWrite);
                offset += bytesToWrite;
                count -= bytesToWrite;
                _length += bytesToWrite;

                if (_currentPos == DieFledermausStream.MaxBuffer)
                {
                    _currentBuffer.Next = new MiniBuffer();
                    _currentBuffer = _currentBuffer.Next;
                    _currentPos = 0;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _currentBuffer = null;

            _currentPos = 0;

            while (_firstBuffer != null)
            {
                _firstBuffer.End = 0;
                _firstBuffer = _firstBuffer.Next;
            }
        }

#if !STREAMCOPY
        public void CopyTo(Stream destination)
        {
            if (destination == null) throw new ArgumentNullException("destination");
            if (!destination.CanWrite)
            {
                if (destination.CanRead) throw new NotSupportedException(TextResources.StreamNotWritable);
                throw new ObjectDisposedException("destination");
            }

            byte[] buffer = new byte[DieFledermausStream.MaxBuffer];

            int read = Read(buffer, 0, DieFledermausStream.MaxBuffer);

            while (read > 0)
            {
                destination.Write(buffer, 0, read);
                read = Read(buffer, 0, DieFledermausStream.MaxBuffer);
            }
        }
#endif
    }
}
