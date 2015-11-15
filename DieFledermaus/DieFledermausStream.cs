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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;

using DieFledermaus.Globalization;

namespace DieFledermaus
{
    /// <summary>
    /// Provides methods and properties for compressing and decompressing files and streams in the DieFledermaus format,
    /// which is just the DEFLATE algorithm prefixed with magic number "<c>mAuS</c>" and metadata.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="DeflateStream"/>, this method reads part of the stream during the constructor, rather than the first call to <see cref="Read(byte[], int, int)"/>.
    /// </remarks>
    public partial class DieFledermausStream : Stream
    {
        internal const int MaxBuffer = 65536;
        private const int _head = 0x5375416d; //Little-endian "mAuS"
        private const ushort _versionShort = 94, _minVersionShort = 92;
        private const float _versionDiv = 100;

        private Stream _baseStream;
        private Stream _deflateStream;
        private QuickBufferStream _bufferStream;
        private CompressionMode _mode;
        private bool _leaveOpen;
        private long _uncompressedLength;

        private static void _checkRead(Stream stream)
        {
            if (stream.CanRead) return;

            if (stream.CanWrite) throw new ArgumentException(TextResources.StreamNotReadable, nameof(stream));
            throw new ObjectDisposedException(nameof(stream), TextResources.StreamClosed);
        }

        private static void _checkWrite(Stream stream)
        {
            if (stream.CanWrite) return;

            if (stream.CanRead) throw new ArgumentException(TextResources.StreamNotWritable, nameof(stream));
            throw new ObjectDisposedException(nameof(stream), TextResources.StreamClosed);
        }

        #region Constructors
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
            if (stream == null) throw new ArgumentNullException(nameof(stream));
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
            else throw InvalidEnumException(nameof(compressionMode), (int)compressionMode, typeof(CompressionMode));
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
        /// Creates a new instance in write-mode, with the specified compression and encryption formats.
        /// </summary>
        /// <param name="stream">The stream containing compressed data.</param>
        /// <param name="compressionFormat">Indicates the format of the stream.</param>
        /// <param name="encryptionFormat">Indicates the format of the encryption.</param>
        /// <param name="leaveOpen"><c>true</c> to leave open <paramref name="stream"/> when the current instance is disposed;
        /// <c>false</c> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// <para><paramref name="compressionFormat"/> is not a valid <see cref="MausCompressionFormat"/> value.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="encryptionFormat"/> is not a valid <see cref="MausEncryptionFormat"/> value.</para>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, MausCompressionFormat compressionFormat, MausEncryptionFormat encryptionFormat, bool leaveOpen)
            : this(stream, compressionFormat, leaveOpen)
        {
            _setEncFormat(encryptionFormat);
        }

        /// <summary>
        /// Creates a new instance in write-mode, with the specified compression and encryption formats.
        /// </summary>
        /// <param name="stream">The stream containing compressed data.</param>
        /// <param name="compressionFormat">Indicates the format of the stream.</param>
        /// <param name="encryptionFormat">Indicates the format of the encryption.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// <para><paramref name="compressionFormat"/> is not a valid <see cref="MausCompressionFormat"/> value.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="encryptionFormat"/> is not a valid <see cref="MausEncryptionFormat"/> value.</para>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, MausCompressionFormat compressionFormat, MausEncryptionFormat encryptionFormat)
            : this(stream, compressionFormat, false)
        {
            _setEncFormat(encryptionFormat);
        }

        /// <summary>
        /// Creates a new instance in write-mode, with the specified compression and encryption formats.
        /// </summary>
        /// <param name="stream">The stream containing compressed data.</param>
        /// <param name="compressionFormat">Indicates the format of the stream.</param>
        /// <param name="leaveOpen"><c>true</c> to leave open <paramref name="stream"/> when the current instance is disposed;
        /// <c>false</c> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// <paramref name="compressionFormat"/> is not a valid <see cref="MausCompressionFormat"/> value.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, MausCompressionFormat compressionFormat, bool leaveOpen)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            _checkWrite(stream);

            _bufferStream = new QuickBufferStream();
            switch (compressionFormat)
            {
                case MausCompressionFormat.Deflate:
                    _deflateStream = new DeflateStream(_bufferStream, CompressionMode.Compress, true);
                    break;
                case MausCompressionFormat.None:
                    _deflateStream = _bufferStream;
                    break;
                default:
                    throw InvalidEnumException(nameof(compressionFormat), (int)compressionFormat, typeof(MausCompressionFormat));
            }

            _cmpFmt = compressionFormat;
            _baseStream = stream;
            _mode = CompressionMode.Compress;
            _leaveOpen = leaveOpen;
            _headerGotten = true;
        }

        /// <summary>
        /// Creates a new instance in write-mode, with the specified compression and encryption formats.
        /// </summary>
        /// <param name="stream">The stream containing compressed data.</param>
        /// <param name="compressionFormat">Indicates the format of the stream.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// <paramref name="compressionFormat"/> is not a valid <see cref="MausCompressionFormat"/> value.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, MausCompressionFormat compressionFormat)
            : this(stream, compressionFormat, false)
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
            _setEncFormat(encryptionFormat);
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
            _setEncFormat(encryptionFormat);
        }

#if COMPLVL
        /// <summary>
        /// Creates a new instance in write-mode with the specified compression level.
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
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            _checkWrite(stream);
            switch (compressionLevel)
            {
                case CompressionLevel.Fastest:
                case CompressionLevel.NoCompression:
                case CompressionLevel.Optimal:
                    break;
                default:
                    throw InvalidEnumException(nameof(compressionLevel), (int)compressionLevel, typeof(CompressionLevel));
            }

            _bufferStream = new QuickBufferStream();
            _deflateStream = new DeflateStream(_bufferStream, compressionLevel, true);
            _baseStream = stream;
            _mode = CompressionMode.Compress;
            _leaveOpen = leaveOpen;
            _headerGotten = true;
        }

        /// <summary>
        /// Creates a new instance in write-mode with the specified compression level.
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

        /// <summary>
        /// Creates a new instance in write-mode with the specified compression level and encryption format.
        /// </summary>
        /// <param name="stream">The stream to which compressed data will be written.</param>
        /// <param name="compressionLevel">Indicates the compression level of the stream.</param>
        /// <param name="encryptionFormat">Indicates the format of the compression mode.</param>
        /// <param name="leaveOpen"><c>true</c> to leave open <paramref name="stream"/> when the current instance is disposed;
        /// <c>false</c> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// <para><paramref name="compressionLevel"/> is not a valid <see cref="CompressionLevel"/> value.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="encryptionFormat"/> is not a valid <see cref="MausEncryptionFormat"/> value.</para>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, CompressionLevel compressionLevel, MausEncryptionFormat encryptionFormat, bool leaveOpen)
            : this(stream, compressionLevel, leaveOpen)
        {
            _setEncFormat(encryptionFormat);
        }

        /// <summary>
        /// Creates a new instance in write-mode with the specified compression level and encryption format.
        /// </summary>
        /// <param name="stream">The stream to which compressed data will be written.</param>
        /// <param name="compressionLevel">Indicates the compression level of the stream.</param>
        /// <param name="encryptionFormat">Indicates the format of the encryption.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// <para><paramref name="compressionLevel"/> is not a valid <see cref="CompressionLevel"/> value.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="encryptionFormat"/> is not a valid <see cref="MausEncryptionFormat"/> value.</para>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, CompressionLevel compressionLevel, MausEncryptionFormat encryptionFormat)
            : this(stream, compressionLevel, encryptionFormat, false)
        {
        }
#endif
        #endregion

        private void _setEncFormat(MausEncryptionFormat encryptionFormat)
        {
            _keySizes = GetKeySizes(encryptionFormat, out _blockByteCount);
            _encFmt = encryptionFormat;
            if (_encFmt == MausEncryptionFormat.None) return;
            _key = FillBuffer(_keySizes.MaxSize >> 3);
            _iv = FillBuffer(_blockByteCount);
            _salt = FillBuffer(_key.Length);
        }

        private static KeySizes GetKeySizes(MausEncryptionFormat encryptionFormat, out int blockByteCount)
        {
            switch (encryptionFormat)
            {
                case MausEncryptionFormat.None:
                    blockByteCount = 0;
                    return null;
                case MausEncryptionFormat.Aes:
                    blockByteCount = _blockByteCtAes;
                    return new KeySizes(128, 256, 64);
                default:
                    throw new InvalidEnumArgumentException(nameof(encryptionFormat), (int)encryptionFormat, typeof(MausEncryptionFormat));
            }
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

        private MausCompressionFormat _cmpFmt;
        /// <summary>
        /// Gets the compression format of the current instance.
        /// </summary>
        public MausCompressionFormat CompressionFormat { get { return _cmpFmt; } }

        private byte[] _key;
        /// <summary>
        /// Gets and sets the key used to encrypt the DieFledermaus stream.
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
                _ensureCanSetKey();
                if (value == null) throw new ArgumentNullException(nameof(value));

                if (!IsValidKeyByteSize(value.Length))
                    throw new ArgumentException(TextResources.KeyLength, nameof(value));
                _key = value;
            }
        }

        /// <summary>
        /// Determines whether the specified value is a valid length for <see cref="Key"/>, in bytes.
        /// </summary>
        /// <param name="byteCount">The number of bytes to test.</param>
        /// <returns><c>true</c> if <paramref name="byteCount"/> is a valid byte count according to <see cref="KeySizes"/>;
        /// <c>false</c> if <paramref name="byteCount"/> is invalid, or if the current instance is not encrypted.</returns>
        public bool IsValidKeyByteSize(int byteCount)
        {
            if (byteCount > int.MaxValue >> 3)
                return false;

            return IsValidKeyBitSize(byteCount << 3);
        }

        /// <summary>
        /// Determines whether the specified value is a valid length for <see cref="Key"/>, in bits.
        /// </summary>
        /// <param name="bitCount">The number of bits to test.</param>
        /// <returns><c>true</c> if <paramref name="bitCount"/> is a valid bit count according to <see cref="KeySizes"/>;
        /// <c>false</c> if <paramref name="bitCount"/> is invalid, or if the current instance is not encrypted.</returns>
        public bool IsValidKeyBitSize(int bitCount)
        {
            if (_keySizes == null) return false;

            if (bitCount < _keySizes.MinSize || bitCount > _keySizes.MaxSize) return false;

            if (bitCount == _keySizes.MaxSize) return true;

            for (int i = _keySizes.MinSize; i <= bitCount; i++)
            {
                if (i == bitCount)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Determines whether the specified value is a valid length for <see cref="Key"/>, in bytes.
        /// </summary>
        /// <param name="byteCount">The number of bytes to test.</param>
        /// <param name="encryptionFormat">The encryption format to test for.</param>
        /// <returns><c>true</c> if <paramref name="byteCount"/> is a valid byte count according to <paramref name="encryptionFormat"/>;
        /// <c>false</c> if <paramref name="byteCount"/> is invalid, or if the current instance is not encrypted.</returns>
        /// <exception cref="InvalidEnumArgumentException">
        /// <paramref name="encryptionFormat"/> is not a valid <see cref="MausEncryptionFormat"/> value.
        /// </exception>
        public static bool IsValidKeyByteSize(int byteCount, MausEncryptionFormat encryptionFormat)
        {
            int blockByteCount;
            var keySizes = GetKeySizes(encryptionFormat, out blockByteCount);
            if (keySizes == null || byteCount > int.MaxValue >> 3) return false;
            return IsValidKeyBitSize(byteCount >> 3, keySizes);
        }

        /// <summary>
        /// Determines whether the specified value is a valid length for <see cref="Key"/>, in bits.
        /// </summary>
        /// <param name="bitCount">The number of bits to test.</param>
        /// <param name="encryptionFormat">The encryption format to test for.</param>
        /// <returns><c>true</c> if <paramref name="bitCount"/> is a valid bit count according to <see cref="KeySizes"/>;
        /// <c>false</c> if <paramref name="bitCount"/> is invalid, or if the current instance is not encrypted.</returns>
        /// <exception cref="InvalidEnumArgumentException">
        /// <paramref name="encryptionFormat"/> is not a valid <see cref="MausEncryptionFormat"/> value.
        /// </exception>
        public static bool IsValidKeyBitSize(int bitCount, MausEncryptionFormat encryptionFormat)
        {
            int blockByteCount;
            var keySizes = GetKeySizes(encryptionFormat, out blockByteCount);
            return keySizes != null && IsValidKeyBitSize(bitCount, keySizes);
        }

        private static bool IsValidKeyBitSize(int bitCount, KeySizes _keySizes)
        {
            if (_keySizes == null) return false;

            if (bitCount < _keySizes.MinSize || bitCount > _keySizes.MaxSize) return false;

            if (bitCount == _keySizes.MaxSize) return true;

            for (int i = _keySizes.MinSize; i <= bitCount; i++)
            {
                if (i == bitCount)
                    return true;
            }
            return false;
        }

        private void _ensureCanSetKey()
        {
            if (_baseStream == null)
                throw new ObjectDisposedException(null, TextResources.CurrentClosed);
            if (_encFmt == MausEncryptionFormat.None)
                throw new NotSupportedException(TextResources.NotEncrypted);
            if (_mode == CompressionMode.Decompress && _headerGotten)
                throw new InvalidOperationException(TextResources.AlreadyDecrypted);
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

        private bool _encryptName;
        /// <summary>
        /// Gets and sets a value indicating whether <see cref="Filename"/> should be encrypted.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-mode.
        /// </exception>
        public bool EncryptFilename
        {
            get { return _encryptName; }
            set
            {
                _ensureCanSetKey();
                _encryptName = value;
            }
        }

        private string _filename;
        /// <summary>
        /// Gets and sets a filename for the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-mode.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <c>null</c> and is invalid.
        /// </exception>
        /// <seealso cref="IsValidFilename(string)"/>
        public string Filename
        {
            get { return _filename; }
            set
            {
                _checkWritable();
                if (value == null || IsValidFilename(value, true))
                    _filename = value;
            }
        }

        private static bool IsValidFilename(string value, bool throwOnInvalid)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            if (value.Length == 0)
            {
                if (throwOnInvalid)
                    throw new ArgumentException(TextResources.FilenameLengthZero, nameof(value));
                return false;
            }
            if (Encoding.UTF8.GetByteCount(value) > maxLen)
            {
                if (throwOnInvalid)
                    throw new ArgumentException(TextResources.FilenameLengthLong, nameof(value));
            }

            bool seenNotWhite = false;
            int dotCount = 0;
            int add;
            for (int i = 0; i < value.Length; i += add)
            {
                char c = value[i];

                if (char.IsSurrogate(c))
                {
                    if (char.IsSurrogatePair(value, i))
                    {
                        add = 2;
                        dotCount = -1;
                        seenNotWhite = true;
                        continue;
                    }

                    if (throwOnInvalid)
                    {
                        throw new ArgumentException(string.Format(TextResources.FilenameBadSurrogate,
                            string.Format("\\u{0:x4} {1}", (int)c, c)), nameof(value));
                    }
                    return false;
                }
                add = 1;

                if (c == '.' && dotCount >= 0)
                {
                    if (++dotCount == 3)
                        dotCount = -1;
                    seenNotWhite = true;
                    continue;
                }
                else dotCount = -1;

                if (char.IsWhiteSpace(c))
                    continue;
                seenNotWhite = true;

                if (c < ' ' || (c > '~' && c <= '\u009f'))
                {
                    if (throwOnInvalid)
                        throw new ArgumentException(TextResources.FilenameControl, nameof(value));
                    return false;
                }
            }

            if (throwOnInvalid)
            {
                if (!seenNotWhite)
                    throw new ArgumentException(TextResources.FilenameWhitespace, nameof(value));
                if (dotCount > 0)
                    throw new ArgumentException(string.Format(TextResources.FilenameDot, value), nameof(value));
                return true;
            }

            return seenNotWhite && dotCount <= 0;
        }

        /// <summary>
        /// Determines if the specified value is a valid value for the <see cref="Filename"/> property.
        /// </summary>
        /// <param name="value">The value to set.</param>
        /// <returns><c>true</c> if <paramref name="value"/> is a valid filename; <c>false</c> if <paramref name="value"/> has a length of 0, has a length
        /// greater than 256 UTF-8 characters, contains unpaired surrogate characters, contains non-whitespace control characters (non-whitespace characters
        /// between <c>\u0000</c> and <c>\u001f</c> inclusive, or between <c>\u007f</c> and <c>\u009f</c> inclusive), contains only whitespace, or is "." or
        /// ".." (the "current directory" and "parent directory" identifiers).</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="value"/> is <c>null</c>.
        /// </exception>
        public static bool IsValidFilename(string value)
        {
            return IsValidFilename(value, false);
        }

        /// <summary>
        /// Sets <see cref="Key"/> to a value derived from the specified password.
        /// </summary>
        /// <param name="password">The password to set.</param>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current stream is in read-mode and the stream has already been successfully decrypted.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="password"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="password"/> has a length of 0.
        /// </exception>
        public void SetPassword(string password)
        {
            _ensureCanSetKey();
            if (password == null)
                throw new ArgumentNullException(nameof(password));
            if (password.Length == 0)
                throw new ArgumentException(TextResources.PasswordZeroLength, nameof(password));

            SetPassword(Encoding.UTF8.GetBytes(password));
        }

        private void SetPassword(byte[] data)
        {
            try
            {
#if NOCRYPTOCLOSE
                Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(data, _salt, _pkCount + minPkCount);
#else
                using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(data, _salt, _pkCount + minPkCount))
#endif
                {
                    _key = pbkdf2.GetBytes(_salt.Length);
                }
            }
            finally
            {
                Array.Clear(data, 0, data.Length);
            }
        }

        /// <summary>
        /// Sets <see cref="Key"/> to a value derived from the specified password.
        /// </summary>
        /// <param name="password">The password to set.</param>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current stream is in read-mode and the stream has already been successfully decrypted.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="password"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="password"/> has a length of 0.
        /// </exception>
        public void SetPassword(SecureString password)
        {
            _ensureCanSetKey();
            if (password == null)
                throw new ArgumentNullException(nameof(password));
            if (password.Length == 0)
                throw new ArgumentException(TextResources.PasswordZeroLength, nameof(password));

            char[] data = new char[password.Length];
            IntPtr pData = IntPtr.Zero;
            byte[] bytes = null;
            try
            {
                pData = Marshal.SecureStringToGlobalAllocUnicode(password);
                Marshal.Copy(pData, data, 0, data.Length);
                bytes = Encoding.UTF8.GetBytes(data);
                SetPassword(bytes);
            }
            finally
            {
                if (pData != IntPtr.Zero)
                    Marshal.FreeHGlobal(pData);
                Array.Clear(data, 0, data.Length);
            }
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

        private const byte IdAes256 = 1, IdAes128 = 2, IdAes192 = 3;
        private const int _blockByteCtAes = 16;
        private const int _keyBitAes256 = 256, _keyByteAes256 = _keyBitAes256 >> 3;
        private const int _keyBitAes128 = 128, _keyByteAes128 = _keyBitAes128 >> 3;
        private const int _keyBitAes192 = 192, _keyByteAes192 = _keyBitAes192 >> 3;
        private const string _keyStrAes256 = "256", _keyStrAes128 = "128", _keyStrAes192 = "192";
        private static readonly byte[] _keyBAes256 = { 0, 1 }, _keyBAes128 = { 128, 0 }, _keyBAes192 = { 192, 0 };

        private const string _cmpNone = "NK", _cmpDef = "DEF";
        private const string _encAes = "AES";
        private static readonly byte[] _cmpBNone = Encoding.UTF8.GetBytes(_cmpNone), _cmpBDef = Encoding.UTF8.GetBytes(_cmpDef),
            _encBAes = Encoding.UTF8.GetBytes(_encAes);

        private bool _headerGotten;

        private const int maxLen = 256;

        private static readonly Dictionary<string, MausCompressionFormat> _formDict = new Dictionary<string, MausCompressionFormat>()
        {
            { "NC", MausCompressionFormat.None },
            { _cmpNone, MausCompressionFormat.None },
            { _cmpDef, MausCompressionFormat.Deflate }
        };

        private static readonly Dictionary<string, MausEncryptionFormat> _encDict = new Dictionary<string, MausEncryptionFormat>()
        {
            { _encAes, MausEncryptionFormat.Aes }
        };

        private const string _kFilename = "Name", _kEncFilename = "KName";
        private static readonly byte[] _bFilename = Encoding.UTF8.GetBytes(_kFilename), _bEncFilename = Encoding.UTF8.GetBytes(_kEncFilename);

        private void _getHeader()
        {
#if NOLEAVEOPEN
            BinaryReader reader = new BinaryReader(_baseStream);
#else
            using (BinaryReader reader = new BinaryReader(_baseStream, Encoding.UTF8, true))
#endif
            {
                if (reader.ReadInt32() != _head)
                    throw new InvalidDataException(TextResources.InvalidMagicNumber);
                ushort version = reader.ReadUInt16();
                if (version > _versionShort)
                    throw new NotSupportedException(TextResources.VersionTooHigh);
                if (version < _minVersionShort)
                    throw new NotSupportedException(TextResources.VersionTooLow);

                if (version == 93)
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
                        case IdAes256:
                            _encFmt = MausEncryptionFormat.Aes;
                            _blockByteCount = _blockByteCtAes;
                            _setKeySizes(_keyBitAes256);
                            break;
                        case IdAes128:
                            _encFmt = MausEncryptionFormat.Aes;
                            _blockByteCount = _blockByteCtAes;
                            _setKeySizes(_keyBitAes128);
                            break;
                        case IdAes192:
                            _encFmt = MausEncryptionFormat.Aes;
                            _blockByteCount = _blockByteCtAes;
                            _setKeySizes(_keyBitAes192);
                            break;
                        default:
                            throw new NotSupportedException(TextResources.FormatUnknown);
                    }

                    options &= ~0xFFFF;
                    if (options != 0)
                        throw new NotSupportedException(TextResources.FormatUnknown);
                }
                else if (version > 93)
                {
                    bool gotFormat = false, gotEnc = false;

                    byte optLen = reader.ReadByte();

                    for (int i = 0; i < optLen; i++)
                    {
                        string curForm = GetString(reader);

                        MausCompressionFormat cmpFmt;
                        if (_formDict.TryGetValue(curForm, out cmpFmt))
                        {
                            if (gotFormat)
                            {
                                if (_cmpFmt == cmpFmt)
                                    continue;

                                throw new InvalidDataException(TextResources.FormatBad);
                            }
                            gotFormat = true;
                            _cmpFmt = cmpFmt;
                            continue;
                        }

                        MausEncryptionFormat encFmt;
                        if (_encDict.TryGetValue(curForm, out encFmt))
                        {
                            if (gotEnc && _encFmt != encFmt)
                                throw new InvalidDataException(TextResources.FormatBad);
                            gotEnc = true;
                            switch (encFmt)
                            {
                                case MausEncryptionFormat.Aes:
                                    {
                                        i++;
                                        if (i >= optLen)
                                            throw new InvalidDataException(TextResources.FormatBad);

                                        byte[] bytes = GetStringBytes(reader);
                                        _blockByteCount = _blockByteCtAes;
                                        int keyBits;
                                        if (bytes.Length == 3)
                                        {
                                            string strVal = Encoding.UTF8.GetString(bytes);
                                            switch (strVal)
                                            {
                                                case _keyStrAes128:
                                                    keyBits = _keyBitAes128;
                                                    break;
                                                case _keyStrAes192:
                                                    keyBits = _keyBitAes192;
                                                    break;
                                                case _keyStrAes256:
                                                    keyBits = _keyBitAes256;
                                                    break;
                                                default:
                                                    throw new NotSupportedException(TextResources.FormatUnknown);
                                            }
                                        }
                                        else if (bytes.Length == 2)
                                        {
                                            keyBits = bytes[0] | (bytes[1] << 8);

                                            switch (keyBits)
                                            {
                                                case _keyBitAes128:
                                                case _keyBitAes192:
                                                case _keyBitAes256:
                                                    break;
                                                default:
                                                    throw new NotSupportedException(TextResources.FormatUnknown);
                                            }
                                        }
                                        else throw new NotSupportedException(TextResources.FormatUnknown);

                                        _setKeySizes(keyBits);

                                        if (gotEnc && keyBits != _keySizes.MinSize && keyBits != _keySizes.MaxSize)
                                            throw new InvalidDataException(TextResources.FormatBad);
                                    }
                                    break;
                            }
                            _encFmt = encFmt;
                            continue;
                        }

                        if (curForm.Equals(_kFilename, StringComparison.Ordinal))
                        {
                            i++;
                            string filename = GetString(reader);

                            if (_encryptName || (_filename != null && !_filename.Equals(filename, StringComparison.Ordinal)))
                                throw new InvalidDataException(TextResources.FormatBad);

                            if (!IsValidFilename(filename, false))
                                throw new InvalidDataException(TextResources.FormatFilename);
                            continue;
                        }

                        if (curForm.Equals(_kEncFilename, StringComparison.Ordinal))
                        {
                            if (_filename != null)
                                throw new InvalidDataException(TextResources.FormatBad);

                            _encryptName = true;
                            continue;
                        }

                        throw new NotSupportedException(TextResources.FormatUnknown);
                    }

                    if (_encryptName && _encFmt == MausEncryptionFormat.None)
                        throw new InvalidDataException(TextResources.FormatBad);
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

        private static string GetString(BinaryReader reader)
        {
            byte[] strBytes = GetStringBytes(reader);

            return Encoding.UTF8.GetString(strBytes);
        }

        private static byte[] GetStringBytes(BinaryReader reader)
        {
            int strLen = reader.ReadByte();
            if (strLen == 0) strLen = maxLen;
            byte[] strBytes = reader.ReadBytes(strLen);
            if (strBytes.Length < strLen)
                throw new EndOfStreamException();
            return strBytes;
        }

        private byte[] _hashExpected, _salt, _iv;
        private const int hashLength = 64, minPkCount = 9001;
        private long _compLength;
        private int _pkCount;

        /// <summary>
        /// Attempts to pre-load the data in the current instance, and test whether <see cref="Key"/> is set to the correct value
        /// if the current stream is encrypted and to decrypt <see cref="Filename"/> if <see cref="EncryptFilename"/> is <c>true</c>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current stream is in write-mode.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// <see cref="Key"/> is not set to the correct value. It is safe to attempt to call <see cref="LoadData()"/> or <see cref="Read(byte[], int, int)"/>
        /// again if this exception is caught.
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// The stream contains invalid data.
        /// </exception>
        /// <exception cref="IOException">
        /// An I/O error occurred.
        /// </exception>
        public void LoadData()
        {
            _checkReading();
            lock (_lock)
            {
                _readData();
            }
        }

        private void _readData()
        {
            if (_headerGotten) return;

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
                _bufferStream.Reset();
            }
            else
            {
                var bufferStream = Decrypt();

                if (!CompareBytes(ComputeHmac(bufferStream)))
                    throw new CryptographicException(TextResources.BadKey);
                bufferStream.Reset();

                if (_encryptName)
                {
                    string filename;
#if NOLEAVEOPEN
                    BinaryReader reader = new BinaryReader(bufferStream);
#else
                    using (BinaryReader reader = new BinaryReader(bufferStream, Encoding.UTF8, true))
#endif
                    {
                        filename = GetString(reader);
                    }

                    if (!IsValidFilename(filename, false))
                        throw new InvalidDataException(TextResources.FormatFilename);
                    _filename = filename;
                }

                _bufferStream.Close();
                _bufferStream = bufferStream;
            }

            switch (_cmpFmt)
            {
                case MausCompressionFormat.None:
                    _deflateStream = _bufferStream;
                    break;
                default:
                    _deflateStream = new DeflateStream(_bufferStream, CompressionMode.Decompress, false);
                    break;
            }
            _headerGotten = true;
        }

        private bool CompareBytes(byte[] hashComputed)
        {
            for (int i = 0; i < hashLength; i++)
            {
                if (hashComputed[i] != _hashExpected[i])
                    return false;
            }
            return true;
        }

        internal static void CheckSegment(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), offset, TextResources.OutOfRangeLessThanZero);
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, TextResources.OutOfRangeLessThanZero);
            if (offset + count > buffer.Length)
                throw new ArgumentException(string.Format(TextResources.OutOfRangeLength, nameof(offset), nameof(count)));
        }

        /// <summary>
        /// Reads from the stream into the specified array.
        /// </summary>
        /// <param name="buffer">The array containing the bytes to write.</param>
        /// <param name="offset">The index in <paramref name="buffer"/> at which copying begins.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <returns>The number of bytes which were read.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current stream is in write-mode.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// <see cref="Key"/> is not set to the correct value. It is safe to attempt to call <see cref="LoadData()"/> or <see cref="Read(byte[], int, int)"/>
        /// again if this exception is caught.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="buffer"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="offset"/> or <paramref name="count"/> is less than 0.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="offset"/> plus <paramref name="count"/> is greater than the length of <paramref name="buffer"/>.
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// The stream contains invalid data.
        /// </exception>
        /// <exception cref="IOException">
        /// An I/O error occurred.
        /// </exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            _checkReading();
            CheckSegment(buffer, offset, count);
            if (count == 0) return 0;
            lock (_lock)
            {
                _readData();
            }

            if (_encFmt == MausEncryptionFormat.None)
                count = (int)Math.Min(count, _uncompressedLength);

            int result = _deflateStream.Read(buffer, offset, count);

            if (_encFmt == MausEncryptionFormat.None)
            {
                if (result < count)
                    throw new EndOfStreamException();
                _uncompressedLength -= result;
            }
            return result;
        }

        private object _lock = new object();

        private void _checkReading()
        {
            if (_baseStream == null) throw new ObjectDisposedException(null, TextResources.CurrentClosed);
            if (_mode == CompressionMode.Compress) throw new NotSupportedException(TextResources.CurrentWrite);
        }

        /// <summary>
        /// Reads a single byte from the stream.
        /// </summary>
        /// <returns>The unsigned byte cast to <see cref="int"/>, or -1 if the current instance has reached the end of the stream.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current stream is in write-mode.
        /// </exception>
        /// <exception cref="IOException">
        /// An I/O error occurred.
        /// </exception>
        public override int ReadByte()
        {
            return base.ReadByte();
        }

        /// <summary>
        /// Writes the specified byte array into the stream.
        /// </summary>
        /// <param name="buffer">The array containing the bytes to write.</param>
        /// <param name="offset">The index in <paramref name="buffer"/> at which writing begins.</param>
        /// <param name="count">The number of bytes to write.</param>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current stream is in read-mode.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="buffer"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="offset"/> or <paramref name="count"/> is less than 0.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="offset"/> plus <paramref name="count"/> is greater than the length of <paramref name="buffer"/>.
        /// </exception>
        /// <exception cref="IOException">
        /// An I/O error occurred.
        /// </exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            _checkWritable();
            _deflateStream.Write(buffer, offset, count);
            _headerGotten = true;
            _uncompressedLength += count;
        }

        private void _checkWritable()
        {
            if (_baseStream == null) throw new ObjectDisposedException(null, TextResources.CurrentClosed);
            if (_mode == CompressionMode.Decompress) throw new NotSupportedException(TextResources.CurrentRead);
        }

        /// <summary>
        /// Releases all unmanaged resources used by the current instance, and optionally releases all managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources;
        /// <c>false</c> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (_baseStream == null)
            {
                base.Dispose(disposing);
                return;
            }

            try
            {
                if (disposing)
                {
                    try
                    {
                        if (_deflateStream == null || _bufferStream == null)
                            return;

                        if (_mode == CompressionMode.Compress && _uncompressedLength != 0)
                        {
                            if (_encFmt != MausEncryptionFormat.None && _key == null)
                                throw new InvalidOperationException(TextResources.KeyNotSet);

                            if (_deflateStream != _bufferStream)
                                _deflateStream.Dispose();
                            _bufferStream.Reset();
#if NOLEAVEOPEN
                            BinaryWriter writer = new BinaryWriter(_baseStream);
#else
                            using (BinaryWriter writer = new BinaryWriter(_baseStream, Encoding.UTF8, true))
#endif
                            {
                                writer.Write(_head);
                                writer.Write(_versionShort);
                                {
                                    List<byte[]> formats = new List<byte[]>();

                                    if (_filename != null)
                                    {
                                        if (_encryptName)
                                        {
                                            formats.Add(_bEncFilename);
                                        }
                                        else
                                        {
                                            formats.Add(_bFilename);
                                            formats.Add(Encoding.UTF8.GetBytes(_filename));
                                        }
                                    }

                                    switch (_cmpFmt)
                                    {
                                        case MausCompressionFormat.None:
                                            formats.Add(_cmpBNone);
                                            break;
                                        default:
                                            formats.Add(_cmpBDef);
                                            break;
                                    }

                                    switch (_encFmt)
                                    {
                                        case MausEncryptionFormat.Aes:
                                            {
                                                formats.Add(_encBAes);
                                                switch (_key.Length)
                                                {
                                                    case _keyByteAes256:
                                                        formats.Add(_keyBAes256);
                                                        break;
                                                    case _keyByteAes192:
                                                        formats.Add(_keyBAes192);
                                                        break;
                                                    case _keyByteAes128:
                                                        formats.Add(_keyBAes128);
                                                        break;
                                                }
                                            }
                                            break;
                                    }

                                    writer.Write((byte)formats.Count);

                                    for (int i = 0; i < formats.Count; i++)
                                    {
                                        byte[] curForm = formats[i];
                                        writer.Write((byte)curForm.Length);
                                        writer.Write(curForm);
                                    }
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
                                    if (_encryptName && _filename != null)
                                    {
                                        byte[] fBytes = Encoding.UTF8.GetBytes(_filename);

                                        _bufferStream.Prepend(new byte[] { (byte)fBytes.Length }.Concat(fBytes).ToArray());
                                    }

                                    using (QuickBufferStream output = Encrypt())
                                    {
                                        writer.Write(output.Length);
                                        writer.Write((long)_pkCount);

                                        _bufferStream.Reset();
                                        byte[] hashHmac = ComputeHmac(_bufferStream);
                                        _baseStream.Write(hashHmac, 0, hashLength);

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
    /// Options indicating the format used to encrypt the DieFledermaus stream.
    /// </summary>
    public enum MausEncryptionFormat
    {
        /// <summary>
        /// The DieFledermaus stream is not encrypted.
        /// </summary>
        None,
        /// <summary>
        /// The DieFledermaus stream is encrypted using the Advanced Encryption Standard algorithm.
        /// </summary>
        Aes,
    }

    /// <summary>
    /// Options indicating the format used to compress the DieFledermaus stream.
    /// </summary>
    public enum MausCompressionFormat
    {
        /// <summary>
        /// The file is DEFLATE-compressed.
        /// </summary>
        Deflate,
        /// <summary>
        /// The file is not compressed.
        /// </summary>
        None,
    }
}
