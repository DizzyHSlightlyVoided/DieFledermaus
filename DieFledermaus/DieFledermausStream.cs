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

using DieFledermaus.Globalization;

namespace DieFledermaus
{
    /// <summary>
    /// Provides methods and properties for compressing and decompressing streams using the Die Fledermaus algorithm,
    /// which is just the DEFLATE algorithm prefixed with magic number "<c>mAuS</c>" and metadata.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="DeflateStream"/>, this method reads part of the stream during the constructor, rather than the first call to <see cref="Read(byte[], int, int)"/>.
    /// </remarks>
    public partial class DieFledermausStream : Stream
    {
        internal const int MaxBuffer = 65536;
        private const int _head = 0x5375416d; //Little-endian "mAuS"
        private const ushort _versionShort = 93, _minVersionShort = 92;
        private const float _versionDiv = 100;
        /// <summary>
        /// The version number of the current implementation, currently 0.92.
        /// This field is constant.
        /// </summary>
        public const float Version = _versionShort / _versionDiv;

        private Stream _baseStream;
        private DeflateStream _deflateStream;
        private QuickBufferStream _bufferStream;
        private CompressionMode _mode;
        private bool _leaveOpen;
        private long _uncompressedLength;

        private static void _checkRead(Stream stream)
        {
            if (stream.CanRead) return;

            if (stream.CanWrite) throw new ArgumentException(TextResources.StreamNotReadable, "stream");
            throw new ObjectDisposedException("stream", TextResources.StreamClosed);
        }

        private static void _checkWrite(Stream stream)
        {
            if (stream.CanWrite) return;

            if (stream.CanRead) throw new ArgumentException(TextResources.StreamNotWritable, "stream");
            throw new ObjectDisposedException("stream", TextResources.StreamClosed);
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
        /// <exception cref="InvalidDataException">
        /// The stream is in read-mode, and <paramref name="stream"/> contains invalid data.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The stream is in read-mode, and <paramref name="stream"/> contains data which is a lower version than the one expected.
        /// </exception>
        public DieFledermausStream(Stream stream, CompressionMode compressionMode, bool leaveOpen)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            if (compressionMode == CompressionMode.Compress)
            {
                _checkWrite(stream);
                _bufferStream = new QuickBufferStream();
                _deflateStream = new DeflateStream(_bufferStream, CompressionMode.Compress, true);
                _headerGotten = true;
                _baseStream = stream;
            }
            else if (compressionMode == CompressionMode.Decompress)
            {
                _checkRead(stream);
                _baseStream = stream;
                _getHeader();
            }
            else throw InvalidEnumException("compressionMode", (int)compressionMode, typeof(CompressionMode));
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

        /// <summary>
        /// Creates a new instance in write-mode with the specified encryption format.
        /// </summary>
        /// <param name="stream">The stream containing compressed data.</param>
        /// <param name="encryptionFormat">Indicates the format of the encryption.</param>
        /// <param name="leaveOpen"><c>true</c> to leave open <paramref name="stream"/> when the current instance is disposed;
        /// <c>false</c> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// <paramref name="encryptionFormat"/> is not a valid <see cref="MausEncryptionFormat"/> value.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, MausEncryptionFormat encryptionFormat, bool leaveOpen)
            : this(stream, CompressionMode.Compress, leaveOpen)
        {
            _setCompFormat(encryptionFormat);
        }

        /// <summary>
        /// Creates a new instance in write-mode with the specified encryption format.
        /// </summary>
        /// <param name="stream">The stream containing compressed data.</param>
        /// <param name="encryptionFormat">Indicates the format of the encryption.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// <paramref name="encryptionFormat"/> is not a valid <see cref="MausEncryptionFormat"/> value.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, MausEncryptionFormat encryptionFormat)
            : this(stream, CompressionMode.Compress, false)
        {
            _setCompFormat(encryptionFormat);
        }

        private void _setCompFormat(MausEncryptionFormat encryptionFormat)
        {
            switch (encryptionFormat)
            {
                case MausEncryptionFormat.None:
                    return;
                case MausEncryptionFormat.Aes:
                    _keySizes = new KeySizes(128, 256, 64);
                    _blockByteCount = 16;
                    break;
                default:
                    throw new InvalidEnumArgumentException("encryptionFormat", (int)encryptionFormat, typeof(MausEncryptionFormat));
            }
            _encFmt = encryptionFormat;
            _key = FillBuffer(_keySizes.MaxSize >> 3);
            _iv = FillBuffer(_blockByteCount);
            _salt = FillBuffer(_key.Length);
        }


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

        private MausEncryptionFormat _encFmt;
        /// <summary>
        /// Gets the encryption format of the current instance.
        /// </summary>
        public MausEncryptionFormat EncryptionFormat { get { return _encFmt; } }

        private byte[] _key;
        /// <summary>
        /// Gets and sets the key used to encrypt the Die Fledermaus stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, the current stream is in read-mode and the stream has already been successfully decrypted.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// In a set operation, the specified value is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is an invalid length according to <see cref="KeySizes"/>.
        /// </exception>
        public byte[] Key
        {
            get
            {
                if (_key == null) return null;
                return (byte[])_key.Clone();
            }
            set
            {
                if (_baseStream == null)
                    throw new ObjectDisposedException(null, TextResources.CurrentClosed);
                if (_encFmt == MausEncryptionFormat.None)
                    throw new NotSupportedException(TextResources.NotEncrypted);
                if (_mode == CompressionMode.Decompress && _headerGotten)
                    throw new InvalidOperationException(TextResources.AlreadyDecrypted);
                if (value == null) throw new ArgumentNullException("value");

                int bitCount = value.Length << 3;
                for (int i = _keySizes.MinSize; i <= _keySizes.MaxSize; i += _keySizes.SkipSize)
                {
                    if (i == bitCount)
                    {
                        _key = value;
                        return;
                    }
                }
                throw new ArgumentException(TextResources.KeyLength, "value");
            }
        }

        /// <summary>
        /// Gets the number of bits in a single block of encrypted data, or 0 if the current instance is not encrypted.
        /// </summary>
        public int BlockSize { get { return _blockByteCount << 3; } }

        private int _blockByteCount;
        /// <summary>
        /// Gets the number of bytes in a single block of encrypted data, or 0 if the current instance is not encrypted.
        /// </summary>
        public int BlockByteCount { get { return _blockByteCount; } }

        /// <summary>
        /// Sets <see cref="Key"/> to a value derived from the specified password.
        /// </summary>
        /// <param name="password">The password to set.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="password"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="password"/> has a length of 0.
        /// </exception>
        public void SetPassword(string password)
        {
            if (password == null)
                throw new ArgumentNullException("password");
            if (password.Length == 0)
                throw new ArgumentException(TextResources.PasswordZeroLength, "password");

            _key = _setPasswd(password);
        }

        /// <summary>
        /// Flushes the contents of the internal buffer of the current stream object to the underlying stream.
        /// </summary>
        public override void Flush()
        {
            if (_baseStream == null) throw new ObjectDisposedException(null, TextResources.CurrentClosed);
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
        /// This method is not supported and always throws <see cref="NotSupportedException"/>.
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
        /// This method is not supported and always throws <see cref="NotSupportedException"/>.
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

        private bool _headerGotten;

        private void _getHeader()
        {
#if NOLEAVEOPEN
            BinaryReader reader = new BinaryReader(_baseStream);
#else
            using (BinaryReader reader = new BinaryReader(_baseStream, System.Text.Encoding.UTF8, true))
#endif
            {
                if (reader.ReadInt32() != _head)
                    throw new InvalidDataException(TextResources.InvalidMagicNumber);
                ushort version = reader.ReadUInt16();
                if (version > _versionShort)
                    throw new NotSupportedException(TextResources.VersionTooHigh);
                if (version < _minVersionShort)
                    throw new NotSupportedException(TextResources.VersionTooLow);

                if (version > _minVersionShort)
                {
                    long options = reader.ReadInt64();
                    byte format = (byte)options;
                    if (format != 0)
                        throw new NotSupportedException(TextResources.FormatUnknown);
                    byte encryption = (byte)(options >> 8);

                    switch (encryption)
                    {
                        case 0:
                            _encFmt = MausEncryptionFormat.None;
                            break;
                        case 1:
                            _encFmt = MausEncryptionFormat.Aes;
                            _blockByteCount = 16;
                            _setKeySizes(256);
                            break;
                        case 2:
                            _encFmt = MausEncryptionFormat.Aes;
                            _blockByteCount = 16;
                            _setKeySizes(128);
                            break;
                        case 3:
                            _encFmt = MausEncryptionFormat.Aes;
                            _blockByteCount = 16;
                            _setKeySizes(192);
                            break;
                        default:
                            throw new NotSupportedException(TextResources.FormatUnknown);
                    }

                    options &= ~0xFFFF;
                    if (options != 0)
                        throw new NotSupportedException(TextResources.FormatUnknown);
                }

                _compLength = reader.ReadInt64();
                _uncompressedLength = reader.ReadInt64();

                if (_encFmt != MausEncryptionFormat.None)
                {
                    if (_uncompressedLength < 0 || _uncompressedLength > (int.MaxValue - minPkCount))
                        throw new InvalidDataException(TextResources.InvalidMagicNumber);
                    _pkCount = (int)_uncompressedLength;
                    _uncompressedLength = 0;
                }

                _hashExpected = reader.ReadBytes(hashLength);
                if (_hashExpected.Length < hashLength) throw new EndOfStreamException();

                if (_encFmt != MausEncryptionFormat.None)
                {
                    int keySize = _keySizes.MinSize >> 3;
                    _salt = reader.ReadBytes(keySize);
                    if (_salt.Length < keySize) throw new EndOfStreamException();

                    _iv = reader.ReadBytes(_blockByteCount);
                    if (_iv.Length < _blockByteCount) throw new EndOfStreamException();

                    _compLength -= keySize + _iv.Length;
                }
            }
        }

        private byte[] _hashExpected, _salt, _iv;
        private const int hashLength = 64, minPkCount = 9001;
        private long _compLength;
        private int _pkCount;

        private void _readData()
        {
            if (_headerGotten) return;
#if NOLEAVEOPEN
            BinaryReader reader = new BinaryReader(_baseStream);
#else
            using (BinaryReader reader = new BinaryReader(_baseStream, System.Text.Encoding.UTF8, true))
#endif
            {
                if (_key == null && _encFmt != MausEncryptionFormat.None)
                    throw new CryptographicException(TextResources.KeyNotSet);

                if (_bufferStream == null)
                {
                    _bufferStream = new QuickBufferStream();
                    byte[] buffer = new byte[MaxBuffer];
                    long length = _compLength;
                    while (length > 0)
                    {
                        int read = _baseStream.Read(buffer, 0, (int)Math.Min(MaxBuffer, length));
                        if (read == 0) throw new EndOfStreamException();
                        _bufferStream.Write(buffer, 0, read);
                        length -= read;
                    }
                }
                _bufferStream.Reset();

                if (_encFmt == MausEncryptionFormat.None)
                {
                    if (!CompareBytes(ComputeHash(_bufferStream)))
                        throw new InvalidDataException(TextResources.BadChecksum);
                }
                else _bufferStream = Decrypt();

                _bufferStream.Reset();
            }

            _deflateStream = new DeflateStream(_bufferStream, CompressionMode.Decompress, false);
            _headerGotten = true;
        }

        private bool CompareBytes(byte[] shaComputed)
        {
            for (int i = 0; i < hashLength; i++)
            {
                if (shaComputed[i] != _hashExpected[i])
                    return false;
            }
            return true;
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
        /// <exception cref="CryptographicException">
        /// <see cref="Key"/> is not set to the correct value.
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
            _checkReading();
            ArraySegment<byte> segment = new ArraySegment<byte>(array, offset, count);
            if (count == 0) return 0;
            lock (_lock)
            {
                _readData();
            }

            if (_encFmt == MausEncryptionFormat.None)
                count = (int)Math.Min(count, _uncompressedLength);

            int result = _deflateStream.Read(array, offset, count);
            if (result < count)
                throw new EndOfStreamException();
            if (_encFmt == MausEncryptionFormat.None)
                _uncompressedLength -= result;
            return result;
        }

        private object _lock = new object();

        private void _checkReading()
        {
            if (_baseStream == null) throw new ObjectDisposedException(null, TextResources.CurrentClosed);
            if (_mode == CompressionMode.Compress) throw new NotSupportedException(TextResources.CurrentRead);
        }

        /// <summary>
        /// Reads a single byte from the stream.
        /// </summary>
        /// <returns>The unsigned byte cast to <see cref="int"/>, or -1 if the current instance has reached the end of the stream.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current stream does not support reading.
        /// </exception>
        /// <exception cref="IOException">
        /// An I/O error occurred.
        /// </exception>
        public override int ReadByte()
        {
            byte[] singleBuffer = new byte[1];
            if (Read(singleBuffer, 0, 1) == 0)
                return -1;
            return singleBuffer[0];
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
            _checkWritable();
            _deflateStream.Write(array, offset, count);
            _headerGotten = true;
            _uncompressedLength += count;
        }

        private void _checkWritable()
        {
            if (_baseStream == null) throw new ObjectDisposedException(null, TextResources.CurrentClosed);
            if (_mode == CompressionMode.Decompress) throw new NotSupportedException(TextResources.CurrentWrite);
        }

        /// <summary>
        /// Releases all unmanaged resources used by the current instance, and optionally releases all managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources;
        /// <c>false</c> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (_baseStream == null) return;
            try
            {
                if (disposing)
                {
                    try
                    {
                        if (_mode == CompressionMode.Compress && _uncompressedLength != 0)
                        {
                            if (_encFmt != MausEncryptionFormat.None && _key == null)
                                throw new InvalidOperationException(TextResources.KeyNotSet);

                            _deflateStream.Dispose();
                            _bufferStream.Reset();
#if NOLEAVEOPEN
                            BinaryWriter writer = new BinaryWriter(_baseStream);
#else
                            using (BinaryWriter writer = new BinaryWriter(_baseStream, System.Text.Encoding.UTF8, true))
#endif
                            {
                                writer.Write(_head);
                                writer.Write(_versionShort);
                                {
                                    long format = 0;

                                    switch (_encFmt)
                                    {
                                        case MausEncryptionFormat.Aes:
                                            switch (_key.Length)
                                            {
                                                case 32:
                                                    format |= (0x100);
                                                    break;
                                                case 16:
                                                    format |= (0x200);
                                                    break;
                                                case 24:
                                                    format |= (0x300);
                                                    break;
                                            }
                                            break;
                                    }

                                    writer.Write(format);
                                }
                                _bufferStream.Reset();
                                if (_encFmt == MausEncryptionFormat.None)
                                {
                                    writer.Write(_bufferStream.Length);
                                    writer.Write(_uncompressedLength);

                                    byte[] hashChecksum = ComputeHash(_bufferStream);
                                    writer.Write(hashChecksum);

                                    _bufferStream.Reset();

                                    _bufferStream.CopyTo(_baseStream);
                                }
                                else
                                {
                                    using (QuickBufferStream output = Encrypt())
                                    {
                                        writer.Write(output.Length);
                                        writer.Write((long)_pkCount);

                                        _bufferStream.Reset();
                                        byte[] hashHmac = ComputeHmac(_bufferStream);
                                        _baseStream.Write(hashHmac, 0, hashLength);

                                        output.Reset();

                                        output.CopyTo(_baseStream);
                                    }
                                }
                            }
#if NOLEAVEOPEN
                            writer.Flush();
#endif
                            _bufferStream.Close();
                        }
                        else _deflateStream.Dispose();
                    }
                    finally
                    {
                        if (!_leaveOpen)
                            _baseStream.Dispose();
                    }
                }
                else if (_bufferStream != null)
                    _bufferStream.Dispose();
            }
            finally
            {
                _baseStream = null;

                _bufferStream = null;
                _deflateStream = null;
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        ~DieFledermausStream()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// Gets the encryption format.
    /// </summary>
    public enum MausEncryptionFormat : byte
    {
        /// <summary>
        /// The Die Fledermaus stream is not encrypted.
        /// </summary>
        None,
        /// <summary>
        /// The Die Fledermaus stream is encrypted using the Advanced Encryption Standard algorithm.
        /// </summary>
        Aes,
    }
}
