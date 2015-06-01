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
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace DieFledermaus
{
    /// <summary>
    /// Provides methods and properties for compressing and decompressing streams using the Die Fledermaus algorithm,
    /// which is just the Deflate algorithm with magic number "<c>mAuS</c>", a length-prefix, and an SHA512 checksum at the end.
    /// </summary>
    public class DieFledermausStream : Stream
    {
        internal const int MaxBuffer = 65536;
        private const int _head = 0x5375416d; //Little-endian "mAuS"

        private const ushort VersionMajor = 0, VersionMinor = 9, VersionRevision = 0;

        private Stream _baseStream;
        private DeflateStream _deflateStream;
        private QuickBufferStream _bufferStream;
        private CompressionMode _mode;
        private bool _leaveOpen;

        private static void _checkRead(Stream stream)
        {
            if (stream.CanRead) return;

            if (stream.CanWrite) throw new ArgumentException("The stream does not support reading.", "stream");
            throw new ObjectDisposedException("stream");
        }

        private static void _checkWrite(Stream stream)
        {
            if (stream.CanWrite) return;

            if (stream.CanRead) throw new ArgumentException("The stream does not support writing.", "stream");
            throw new ObjectDisposedException("stream");
        }

        /// <summary>
        /// Creates a new instance with the specified mode.
        /// </summary>
        /// <param name="stream">The stream containing compressed data.</param>
        /// <param name="compressionMode">Indicates whether the stream should be in compression or decompression mode.</param>
        /// <param name="leaveOpen"><c>true</c> to leave open <paramref name="stream"/> when the current instance is disposed;
        /// <c>false</c> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// <paramref name="compressionMode"/> is not a valid <see cref="CompressionMode"/> value.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para><paramref name="compressionMode"/> is <see cref="CompressionMode.Compress"/>, and <paramref name="stream"/> does not support writing.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="compressionMode"/> is <see cref="CompressionMode.Decompress"/>, and <paramref name="stream"/> does not support reading.</para>
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, CompressionMode compressionMode, bool leaveOpen)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            if (compressionMode == CompressionMode.Compress)
            {
                _checkWrite(stream);
                _bufferStream = new QuickBufferStream();
                _deflateStream = new DeflateStream(_bufferStream, CompressionMode.Compress, true);
            }
            else if (compressionMode == CompressionMode.Decompress)
            {
                _checkRead(stream);
            }
            else throw new InvalidEnumArgumentException("compressionMode", (int)compressionMode, typeof(CompressionMode));
            _baseStream = stream;
            _mode = compressionMode;
            _leaveOpen = leaveOpen;
        }

        /// <summary>
        /// Creates a new instance with the specified mode.
        /// </summary>
        /// <param name="stream">The stream containing compressed data.</param>
        /// <param name="compressionMode">Indicates whether the stream should be in compression or decompression mode.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// <paramref name="compressionMode"/> is not a valid <see cref="CompressionMode"/> value.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para><paramref name="compressionMode"/> is <see cref="CompressionMode.Compress"/>, and <paramref name="stream"/> does not support writing.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="compressionMode"/> is <see cref="CompressionMode.Decompress"/>, and <paramref name="stream"/> does not support reading.</para>
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, CompressionMode compressionMode)
            : this(stream, compressionMode, false)
        {
        }

#if NET_4_5
        /// <summary>
        /// Creates a new instance in compression mode.
        /// </summary>
        /// <param name="stream">The stream to which compressed data will be written.</param>
        /// <param name="compressionLevel">Indicates the compression level of the stream.</param>
        /// <param name="leaveOpen"><c>true</c> to leave open <paramref name="stream"/> when the current instance is disposed;
        /// <c>false</c> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// <paramref name="compressionLevel"/> is not a valid <see cref="CompressionLevel"/> value.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, CompressionLevel compressionLevel, bool leaveOpen)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            _checkWrite(stream);
            switch (compressionLevel)
            {
                case CompressionLevel.Fastest:
                case CompressionLevel.NoCompression:
                case CompressionLevel.Optimal:
                    break;
                default:
                    throw new InvalidEnumArgumentException("compressionLevel", (int)compressionLevel, typeof(CompressionLevel));
            }

            _bufferStream = new QuickBufferStream();
            _deflateStream = new DeflateStream(_bufferStream, compressionLevel, true);
            _baseStream = stream;
            _mode = CompressionMode.Compress;
            _leaveOpen = leaveOpen;
        }

        /// <summary>
        /// Creates a new instance in compression mode.
        /// </summary>
        /// <param name="stream">The stream to which compressed data will be written.</param>
        /// <param name="compressionLevel">Indicates the compression level of the stream.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// <paramref name="compressionLevel"/> is not a valid <see cref="CompressionLevel"/> value.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, CompressionLevel compressionLevel)
            : this(stream, compressionLevel, false)
        {
        }
#endif

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        public override bool CanRead
        {
            get { return _baseStream != null && _mode == CompressionMode.Decompress; }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading. Always returns <c>false</c>.
        /// </summary>
        public override bool CanSeek
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        public override bool CanWrite
        {
            get { return _baseStream != null && _mode == CompressionMode.Compress; }
        }

        /// <summary>
        /// Flushes the contents of the internal buffer of the current stream object to the underlying stream.
        /// </summary>
        public override void Flush()
        {
            if (_baseStream == null) throw new ObjectDisposedException(null);
        }

        #region Not supported
        /// <summary>
        /// Gets the length of the stream.
        /// This property is not supported and always throws <see cref="NotSupportedException"/>.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Always.
        /// </exception>
        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Gets and sets the position in the stream.
        /// This property is not supported and always throws <see cref="NotSupportedException"/>.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Always.
        /// </exception>
        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Sets the length of the stream.
        /// This property is not supported and always throws <see cref="NotSupportedException"/>.
        /// </summary>
        /// <param name="value">This parameter is ignored.</param>
        /// <exception cref="NotSupportedException">
        /// Always.
        /// </exception>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Seeks within the stream.
        /// This property is not supported and always throws <see cref="NotSupportedException"/>.
        /// </summary>
        /// <param name="offset">This parameter is ignored.</param>
        /// <param name="origin">This parameter is ignored.</param>
        /// <exception cref="NotSupportedException">
        /// Always.
        /// </exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }
        #endregion

        private bool _versionHigher(ushort major, ushort minor, ushort revision)
        {
            if (major > VersionMajor) return true;
            if (major < VersionMajor) return false;

            if (minor > VersionMinor) return true;
            if (minor < VersionMinor) return false;

            return revision > VersionRevision;
        }

        private bool _headerGotten;

        private void _getHeader()
        {
            if (_headerGotten) return;

            _bufferStream = new QuickBufferStream();

#if NET_4_5
            using (BinaryReader reader = new BinaryReader(_baseStream, new UTF8Encoding(), true))
#else
            BinaryReader reader = new BinaryReader(_baseStream, new UTF8Encoding());
#endif
            {
                if (reader.ReadInt32() != _head) throw new InvalidDataException();

                if (_versionHigher(reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16()))
                    throw new IOException("The version number is higher than the current supported implementation.");

                long length = reader.ReadInt64();

                byte[] buffer = new byte[MaxBuffer];
                while (length > 0)
                {
                    int read = _baseStream.Read(buffer, 0, (int)Math.Min(MaxBuffer, length));
                    if (read == 0) throw new EndOfStreamException();
                    _bufferStream.Write(buffer, 0, read);
                    length -= read;
                }
                _bufferStream.Reset();

                const int hashLength = 64;

                byte[] shaExpected = reader.ReadBytes(hashLength);
                if (shaExpected.Length < hashLength) throw new EndOfStreamException();

                using (SHA512 shaHash = SHA512.Create())
                {
                    byte[] shaComputed = shaHash.ComputeHash(_bufferStream);

                    for (int i = 0; i < hashLength; i++)
                        if (shaComputed[i] != shaExpected[i]) throw new IOException("The computed SHA-512 checksum did not match the expected value.");
                }

                _bufferStream.Reset();
            }

            _deflateStream = new DeflateStream(_bufferStream, CompressionMode.Decompress, false);
            _headerGotten = true;
        }

        /// <summary>
        /// Reads from the stream into the specified array.
        /// </summary>
        /// <param name="array">The array containing the bytes to write.</param>
        /// <param name="offset">The index in <paramref name="array"/> at which copying begins.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <returns>The number of bytes which were read.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current stream does not support reading.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="array"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="offset"/> or <paramref name="count"/> is less than 0.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="offset"/> plus <paramref name="count"/> is greater than the length of <paramref name="array"/>.
        /// </exception>
        /// <exception cref="IOException">
        /// An I/O error occurred.
        /// </exception>
        public override int Read(byte[] array, int offset, int count)
        {
            if (_baseStream == null) throw new ObjectDisposedException(null);
            if (_mode == CompressionMode.Compress) throw new NotSupportedException("The current stream is in write-mode.");
            ArraySegment<byte> segment = new ArraySegment<byte>(array, offset, count);
            _getHeader();
            return _deflateStream.Read(array, offset, count);
        }

        /// <summary>
        /// Writes the specified byte array into the stream.
        /// </summary>
        /// <param name="array">The array containing the bytes to write.</param>
        /// <param name="offset">The index in <paramref name="array"/> at which writing begins.</param>
        /// <param name="count">The number of bytes to write.</param>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current stream does not support writing.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="array"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="offset"/> or <paramref name="count"/> is less than 0.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="offset"/> plus <paramref name="count"/> is greater than the length of <paramref name="array"/>.
        /// </exception>
        /// <exception cref="IOException">
        /// An I/O error occurred.
        /// </exception>
        public override void Write(byte[] array, int offset, int count)
        {
            if (_baseStream == null) throw new ObjectDisposedException(null);
            if (_mode == CompressionMode.Decompress) throw new NotSupportedException("The current stream is in read-mode.");
            _deflateStream.Write(array, offset, count);
            _headerGotten = true;
        }

        /// <summary>
        /// Releases all resources used by the current instance.
        /// </summary>
        public override void Close()
        {
            base.Close();
            if (_baseStream == null) return;
            if (_mode == CompressionMode.Compress && _headerGotten)
            {
                _deflateStream.Close();
                _bufferStream.Reset();

#if NET_4_5
                using (BinaryWriter writer = new BinaryWriter(_baseStream, new UTF8Encoding(), true))
#else
                BinaryWriter writer = new BinaryWriter(_baseStream, new UTF8Encoding());
#endif
                using (SHA512 hashGenerator = SHA512.Create())
                {
                    writer.Write(_head);

                    writer.Write(VersionMajor);
                    writer.Write(VersionMinor);
                    writer.Write(VersionRevision);

                    writer.Write(_bufferStream.Length);
                    _bufferStream.CopyTo(_baseStream);

                    _bufferStream.Reset();

                    byte[] hashChecksum = hashGenerator.ComputeHash(_bufferStream);
                    writer.Write(hashChecksum);
                }
#if !NET_4_5
                writer.Flush();
#endif
            }
            else _deflateStream.Close();
            _bufferStream = null;
            _deflateStream = null;
            if (!_leaveOpen)
                _baseStream.Close();
            _baseStream = null;
        }
    }

    internal class QuickBufferStream : Stream
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
            if (_reading) throw new NotSupportedException();
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

        public override void Close()
        {
            base.Close();
            _currentBuffer = null;

            _currentPos = 0;

            while (_firstBuffer != null)
            {
                _firstBuffer.End = 0;
                _firstBuffer = _firstBuffer.Next;
            }
        }

#if NOSTREAMCOPY
        public void CopyTo(Stream destination)
        {
            if (destination == null) throw new ArgumentNullException("destination");
            if (!destination.CanWrite)
            {
                if (destination.CanRead) throw new NotSupportedException("The destination stream does not support writing.");
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
