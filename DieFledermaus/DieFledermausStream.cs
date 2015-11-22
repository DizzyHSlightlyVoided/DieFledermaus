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
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;

using DieFledermaus.Globalization;
using SevenZip;
using SevenZip.Compression.LZMA;

namespace DieFledermaus
{
    /// <summary>
    /// Provides methods and properties for compressing and decompressing files and streams in the DieFledermaus format,
    /// which is just the DEFLATE algorithm prefixed with magic number "<c>mAuS</c>" and metadata.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="DeflateStream"/>, this method reads part of the stream during the constructor, rather than the first call
    /// to <see cref="Read(byte[], int, int)"/>.
    /// </remarks>
    public partial class DieFledermausStream : Stream
    {
        internal const int MaxBuffer = 65536;
        private const int _head = 0x5375416d; //Little-endian "mAuS"
        private const ushort _versionShort = 95, _minVersionShort = 94;
        private const float _versionDiv = 100;

        internal static readonly UTF8Encoding _textEncoding = new UTF8Encoding(false, false);

        private Stream _baseStream;
        private Stream _deflateStream;
        private MausBufferStream _bufferStream;
        private CompressionMode _mode;
        private bool _leaveOpen;
        private long _uncompressedLength;

        internal static void CheckRead(Stream stream)
        {
            if (stream.CanRead) return;

            if (stream.CanWrite) throw new ArgumentException(TextResources.StreamNotReadable, nameof(stream));
            throw new ObjectDisposedException(nameof(stream), TextResources.StreamClosed);
        }

        internal static void CheckWrite(Stream stream)
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
        /// <param name="leaveOpen"><c>true</c> to leave <paramref name="stream"/> open when the current instance is disposed;
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
                CheckWrite(stream);
                _bufferStream = new MausBufferStream();
                _deflateStream = new DeflateStream(_bufferStream, CompressionMode.Compress, true);
                _baseStream = stream;
            }
            else if (compressionMode == CompressionMode.Decompress)
            {
                CheckRead(stream);
                _baseStream = stream;
                _getHeader();
            }
            else throw new InvalidEnumArgumentException(nameof(compressionMode), (int)compressionMode, typeof(CompressionMode));
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
        /// <param name="encryptionFormat">Indicates the encryption format.</param>
        /// <param name="leaveOpen"><c>true</c> to leave <paramref name="stream"/> open when the current instance is disposed;
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
        /// <param name="encryptionFormat">Indicates the encryption format.</param>
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
        /// Creates a new instance in write-mode, with the specified compression and no encryption.
        /// </summary>
        /// <param name="stream">The stream containing compressed data.</param>
        /// <param name="compressionFormat">Indicates the format of the stream.</param>
        /// <param name="leaveOpen"><c>true</c> to leave <paramref name="stream"/> open when the current instance is disposed;
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
            CheckWrite(stream);

            _bufferStream = new MausBufferStream();
            switch (compressionFormat)
            {
                case MausCompressionFormat.Deflate:
                    _deflateStream = new DeflateStream(_bufferStream, CompressionMode.Compress, true);
                    break;
                case MausCompressionFormat.None:
                case MausCompressionFormat.Lzma:
                    _deflateStream = _bufferStream;
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(compressionFormat), (int)compressionFormat, typeof(MausCompressionFormat));
            }

            _cmpFmt = compressionFormat;
            _baseStream = stream;
            _mode = CompressionMode.Compress;
            _leaveOpen = leaveOpen;
        }

        /// <summary>
        /// Creates a new instance in write-mode, with the specified compression format and no encryption.
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
        /// <param name="encryptionFormat">Indicates the encryption format.</param>
        /// <param name="leaveOpen"><c>true</c> to leave <paramref name="stream"/> open when the current instance is disposed;
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
        /// <param name="encryptionFormat">Indicates the encryption format.</param>
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

        /// <summary>
        /// Creates a new instance in write-mode with LZMA encryption, using the specified dictionary size and no encryption.
        /// </summary>
        /// <param name="stream">The stream containing compressed data.</param>
        /// <param name="dictionarySize">Indicates the size of the dictionary, in bytes.</param>
        /// <param name="leaveOpen"><c>true</c> to leave <paramref name="stream"/> open when the current instance is disposed;
        /// <c>false</c> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="dictionarySize"/> is an integer value less than <see cref="LzmaDictionarySize.MinValue"/> or greater
        /// than <see cref="LzmaDictionarySize.MaxValue"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, LzmaDictionarySize dictionarySize, bool leaveOpen)
            : this(stream, MausCompressionFormat.Lzma, leaveOpen)
        {
            if (dictionarySize == 0)
                dictionarySize = LzmaDictionarySize.Size8m;
            else if (dictionarySize < LzmaDictionarySize.MinValue || dictionarySize > LzmaDictionarySize.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(dictionarySize), dictionarySize,
                    string.Format(TextResources.OutOfRange, LzmaDictionarySize.MinValue, LzmaDictionarySize.MaxValue));
            }
            _lzmaDictSize = dictionarySize;
        }

        /// <summary>
        /// Creates a new instance in write-mode with LZMA encryption, using the specified dictionary size and no encryption.
        /// </summary>
        /// <param name="stream">The stream containing compressed data.</param>
        /// <param name="dictionarySize">Indicates the size of the dictionary, in bytes.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="dictionarySize"/> is an integer value less than <see cref="LzmaDictionarySize.MinValue"/> or greater
        /// than <see cref="LzmaDictionarySize.MaxValue"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, LzmaDictionarySize dictionarySize)
            : this(stream, dictionarySize, false)
        {
        }

        /// <summary>
        /// Creates a new instance in write-mode with LZMA encryption, using the specified dictionary size and no encryption.
        /// </summary>
        /// <param name="stream">The stream containing compressed data.</param>
        /// <param name="dictionarySize">Indicates the size of the dictionary, in bytes.</param>
        /// <param name="encryptionFormat">Indicates the encryption format.</param>
        /// <param name="leaveOpen"><c>true</c> to leave <paramref name="stream"/> open when the current instance is disposed;
        /// <c>false</c> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="dictionarySize"/> is an integer value less than <see cref="LzmaDictionarySize.MinValue"/> or greater
        /// than <see cref="LzmaDictionarySize.MaxValue"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, LzmaDictionarySize dictionarySize, MausEncryptionFormat encryptionFormat, bool leaveOpen)
            : this(stream, dictionarySize, leaveOpen)
        {
            _setEncFormat(encryptionFormat);
        }

        /// <summary>
        /// Creates a new instance in write-mode with LZMA encryption, using the specified dictionary size and no encryption.
        /// </summary>
        /// <param name="stream">The stream containing compressed data.</param>
        /// <param name="dictionarySize">Indicates the size of the dictionary, in bytes.</param>
        /// <param name="encryptionFormat">Indicates the encryption format.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="dictionarySize"/> is an integer value less than <see cref="LzmaDictionarySize.MinValue"/> or greater
        /// than <see cref="LzmaDictionarySize.MaxValue"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, LzmaDictionarySize dictionarySize, MausEncryptionFormat encryptionFormat)
            : this(stream, dictionarySize, false)
        {
            _setEncFormat(encryptionFormat);
        }

#if COMPLVL
        /// <summary>
        /// Creates a new instance in write-mode using DEFLATE with the specified compression level.
        /// </summary>
        /// <param name="stream">The stream to which compressed data will be written.</param>
        /// <param name="compressionLevel">Indicates the compression level of the stream.</param>
        /// <param name="leaveOpen"><c>true</c> to leave <paramref name="stream"/> open when the current instance is disposed;
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
            CheckWrite(stream);
            switch (compressionLevel)
            {
                case CompressionLevel.Fastest:
                case CompressionLevel.NoCompression:
                case CompressionLevel.Optimal:
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(compressionLevel), (int)compressionLevel, typeof(CompressionLevel));
            }

            _bufferStream = new MausBufferStream();
            _deflateStream = new DeflateStream(_bufferStream, compressionLevel, true);
            _baseStream = stream;
            _mode = CompressionMode.Compress;
            _leaveOpen = leaveOpen;
        }

        /// <summary>
        /// Creates a new instance in write-mode using DEFLATE with the specified compression level.
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
        /// Creates a new instance in write-mode using DEFLATE with the specified compression level and encryption format.
        /// </summary>
        /// <param name="stream">The stream to which compressed data will be written.</param>
        /// <param name="compressionLevel">Indicates the compression level of the stream.</param>
        /// <param name="encryptionFormat">Indicates the encryption format.</param>
        /// <param name="leaveOpen"><c>true</c> to leave <paramref name="stream"/> open when the current instance is disposed;
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
        /// Creates a new instance in write-mode using DEFLATE with the specified compression level and encryption format.
        /// </summary>
        /// <param name="stream">The stream to which compressed data will be written.</param>
        /// <param name="compressionLevel">Indicates the compression level of the stream.</param>
        /// <param name="encryptionFormat">Indicates the encryption format.</param>
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

        internal DieFledermausStream(DieFledermauZArchiveEntry entry, Stream stream, ICompressionFormat compFormat, MausEncryptionFormat encryptionFormat)
        {
            _baseStream = stream;
            _bufferStream = new MausBufferStream();
            switch (compFormat.CompressionFormat)
            {
                case MausCompressionFormat.Deflate:
#if COMPLVL
                    _deflateStream = new DeflateStream(_bufferStream, ((DeflateCompressionFormat)compFormat).CompressionLevel, true);
#else
                    _deflateStream = new DeflateStream(_bufferStream, CompressionMode.Compress, true);
#endif
                    break;
                case MausCompressionFormat.Lzma:
                    _lzmaDictSize = ((LzmaCompressionFormat)compFormat).DictionarySize;
                    goto default;
                default:
                    _deflateStream = _bufferStream;
                    break;
            }
            _cmpFmt = compFormat.CompressionFormat;
            _setEncFormat(encryptionFormat);
            _allowDirNames = true;
            _entry = entry;
        }
        #endregion

        private DieFledermauZArchiveEntry _entry;

        private void _setEncFormat(MausEncryptionFormat encryptionFormat)
        {
            _keySizes = _getKeySizes(encryptionFormat, out _blockByteCount);
            _encFmt = encryptionFormat;
            if (_encFmt == MausEncryptionFormat.None) return;
            _key = FillBuffer(_keySizes.MaxSize >> 3);
            _iv = FillBuffer(_blockByteCount);
            _salt = FillBuffer(_key.Length);
            _encryptedOptions = new SettableOptions(this);
        }

        /// <summary>
        /// Gets a <see cref="System.Security.Cryptography.KeySizes"/> value indicating the valid key sizes for the specified encryption scheme.
        /// </summary>
        /// <param name="encryptionFormat">The encryption format to check.</param>
        /// <param name="blockBitCount">When this method returns, contains the number of bits in a single block of encrypted data,
        /// or <c>none</c> if <paramref name="encryptionFormat"/> is <see cref="MausEncryptionFormat.None"/>. This parameter is
        /// passed uninitialized.</param>
        /// <returns>A <see cref="System.Security.Cryptography.KeySizes"/> value indicating the valid key sizes for <paramref name="encryptionFormat"/>,
        /// or <c>null</c> if <paramref name="encryptionFormat"/> is <see cref="MausEncryptionFormat.None"/></returns>
        public static KeySizes GetKeySizes(MausEncryptionFormat encryptionFormat, out int blockBitCount)
        {
            KeySizes sizes = _getKeySizes(encryptionFormat, out blockBitCount);
            blockBitCount <<= 3;
            return sizes;
        }

        /// <summary>
        /// Gets a <see cref="System.Security.Cryptography.KeySizes"/> value indicating the valid key sizes for the specified encryption scheme.
        /// </summary>
        /// <param name="encryptionFormat">The encryption format to check.</param>
        /// <returns>A <see cref="System.Security.Cryptography.KeySizes"/> value indicating the valid key sizes for <paramref name="encryptionFormat"/>,
        /// or <c>null</c> if <paramref name="encryptionFormat"/> is <see cref="MausEncryptionFormat.None"/></returns>
        public static KeySizes GetKeySizes(MausEncryptionFormat encryptionFormat)
        {
            int blockByteCount;
            return _getKeySizes(encryptionFormat, out blockByteCount);
        }

        private static KeySizes _getKeySizes(MausEncryptionFormat encryptionFormat, out int blockByteCount)
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

        internal bool HeaderIsProcessed { get { return _headerGotten; } }

        private DateTime? _timeC;
        /// <summary>
        /// Gets and sets the time at which the underlying file was created, or <c>null</c> to specify no creation time.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, the current stream is in read-mode.
        /// </exception>
        public DateTime? CreatedTime
        {
            get { return _timeC; }
            set
            {
                _ensureCanWrite();
                _timeC = value;
            }
        }

        private DateTime? _timeM;
        /// <summary>
        /// Gets and sets the time at which the underlying file was last modified prior to being archived,
        /// or <c>null</c> to specify no modification time.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, the current stream is in read-mode.
        /// </exception>
        public DateTime? ModifiedTime
        {
            get { return _timeM; }
            set
            {
                _ensureCanWrite();
                _timeM = value;
            }
        }

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
            var keySizes = _getKeySizes(encryptionFormat, out blockByteCount);
            if (keySizes == null || byteCount > int.MaxValue >> 3) return false;
            return IsValidKeyBitSize(byteCount << 3, keySizes);
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
            var keySizes = _getKeySizes(encryptionFormat, out blockByteCount);
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

        private const int _maxComment = 65536;

        private string _comment;
        /// <summary>
        /// Gets and sets a comment on the file.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-mode.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <c>null</c>, and has a length of either 0 or which is greater than 65536.
        /// </exception>
        public string Comment
        {
            get { return _comment; }
            set
            {
                _ensureCanWrite();
                if (value != null && (value.Length <= 0 || value.Length > _maxComment))
                    throw new ArgumentException(TextResources.CommentLength, nameof(value));
                _comment = value;
            }
        }

        private SettableOptions _encryptedOptions;
        /// <summary>
        /// Gets a collection containing options which should be encrypted, or <c>null</c> if the current instance is not encrypted.
        /// </summary>
        public SettableOptions EncryptedOptions { get { return _encryptedOptions; } }

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
                _ensureCanWrite();
                if (value == null || IsValidFilename(value, true, _allowDirNames, nameof(value)))
                    _filename = value;
            }
        }

        internal static bool IsValidFilename(string value, bool throwOnInvalid, bool allowDirNames, string paramName)
        {
            if (value == null) throw new ArgumentNullException(paramName);

            if (value.Length == 0)
            {
                if (throwOnInvalid)
                    throw new ArgumentException(TextResources.FilenameLengthZero, paramName);
                return false;
            }
            if (_textEncoding.GetByteCount(value) > maxLen)
            {
                if (throwOnInvalid)
                    throw new ArgumentException(TextResources.FilenameLengthLong, paramName);
            }

            if (allowDirNames)
            {
                //TODO: Something better than this?
                try
                {
                    return value.Split('/').All(f => IsValidFilename(value, throwOnInvalid, false, paramName));
                }
                catch (ArgumentException e)
                {
                    throw new ArgumentException(string.Format(TextResources.FilenameComponent, e.Message), paramName, e);
                }
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
                            string.Format("\\u{0:x4} {1}", (int)c, c)), paramName);
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

                if (c == '/')
                {
                    if (throwOnInvalid)
                        throw new ArgumentException(TextResources.FilenameForwardSlash);
                    return false;
                }

                seenNotWhite = true;

                if (c < ' ' || (c > '~' && c <= '\u009f'))
                {
                    if (throwOnInvalid)
                        throw new ArgumentException(TextResources.FilenameControl, paramName);
                    return false;
                }
            }

            if (throwOnInvalid)
            {
                if (!seenNotWhite)
                    throw new ArgumentException(TextResources.FilenameWhitespace, paramName);
                if (dotCount > 0)
                    throw new ArgumentException(string.Format(TextResources.FilenameDot, value), paramName);
                return true;
            }

            return seenNotWhite && dotCount <= 0;
        }

        /// <summary>
        /// Determines if the specified value is a valid value for the <see cref="Filename"/> property.
        /// </summary>
        /// <param name="value">The value to set.</param>
        /// <returns><c>true</c> if <paramref name="value"/> is a valid filename; <c>false</c> if <paramref name="value"/> has a length of 0, has a length
        /// greater than 256 UTF-8 bytes, contains unpaired surrogate characters, contains non-whitespace control characters (non-whitespace characters
        /// between <c>\u0000</c> and <c>\u001f</c> inclusive, or between <c>\u007f</c> and <c>\u009f</c> inclusive), contains only whitespace,
        /// or is "." or ".." (the "current directory" and "parent directory" identifiers).</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="value"/> is <c>null</c>.
        /// </exception>
        public static bool IsValidFilename(string value)
        {
            return IsValidFilename(value, false, false, nameof(value));
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

            SetPassword(_textEncoding.GetBytes(password));
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
                bytes = _textEncoding.GetBytes(data);
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

        private const int _blockByteCtAes = 16;
        private const int _keyBitAes256 = 256, _keyByteAes256 = _keyBitAes256 >> 3;
        private const int _keyBitAes128 = 128, _keyByteAes128 = _keyBitAes128 >> 3;
        private const int _keyBitAes192 = 192, _keyByteAes192 = _keyBitAes192 >> 3;
        private const string _keyStrAes256 = "256", _keyStrAes128 = "128", _keyStrAes192 = "192";
        private static readonly byte[] _keyBAes256 = { 0, 1 }, _keyBAes128 = { 128, 0 }, _keyBAes192 = { 192, 0 };

        private const string _cmpNone = "NK", _cmpDef = "DEF", _cmpLzma = "LZMA";
        private const string _encAes = "AES";
        private static readonly byte[] _cmpBNone = { (byte)'N', (byte)'K' }, _cmpBDef = { (byte)'D', (byte)'E', (byte)'F' },
            _cmpBLzma = { (byte)'L', (byte)'Z', (byte)'M', (byte)'A' },
            _encBAes = { (byte)'A', (byte)'E', (byte)'S' };


        private const string _kTimeC = "Ers", _kTimeM = "Mod";
        private static readonly byte[] _bTimeC = { (byte)'E', (byte)'r', (byte)'s' }, _bTimeM = { (byte)'M', (byte)'o', (byte)'d' };

        private bool _headerGotten, _allowDirNames = false;

        internal const int maxLen = 256;

        private static readonly Dictionary<string, MausCompressionFormat> _formDict = new Dictionary<string, MausCompressionFormat>(StringComparer.Ordinal)
        {
            { _cmpNone, MausCompressionFormat.None },
            { _cmpLzma, MausCompressionFormat.Lzma },
            { _cmpDef, MausCompressionFormat.Deflate }
        };

        private const string _kFilename = "Name", _kEncFilename = "KName", _kULen = "DeL", _kComment = "Kom";
        private static readonly byte[] _bFilename = { (byte)'N', (byte)'a', (byte)'m', (byte)'e' }, _bULen = { (byte)'D', (byte)'e', (byte)'L' },
            _bComment = { (byte)'K', (byte)'o', (byte)'m' };

        private ushort version = _versionShort;
        private void _getHeader()
        {
#if NOLEAVEOPEN
            BinaryReader reader = new BinaryReader(_baseStream);
#else
            using (BinaryReader reader = new BinaryReader(_baseStream, _textEncoding, true))
#endif
            {
                if (reader.ReadInt32() != _head)
                    throw new InvalidDataException(TextResources.InvalidMagicNumber);
                version = reader.ReadUInt16();
                if (version > _versionShort)
                    throw new NotSupportedException(TextResources.VersionTooHigh);
                if (version < _minVersionShort)
                    throw new NotSupportedException(TextResources.VersionTooLow);

                ReadOptions(reader, false);

                _compLength = reader.ReadInt64();
                _uncompressedLength = reader.ReadInt64();

                if (_encFmt != MausEncryptionFormat.None)
                {
                    if (_uncompressedLength < 0 || _uncompressedLength > (int.MaxValue - minPkCount))
                        throw new InvalidDataException(TextResources.InvalidMagicNumber);
                    _pkCount = (int)_uncompressedLength;
                    _uncompressedLength = 0;
                }
                else if (_uncompressedLength <= 0)
                    throw new InvalidDataException(TextResources.InvalidMagicNumber);
                else gotULen = true;

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

        bool gotFormat, gotULen;
        private void ReadOptions(BinaryReader reader, bool fromEncrypted)
        {
            int optLen = version == _minVersionShort ? reader.ReadByte() : reader.ReadUInt16();

            SettableOptions options = fromEncrypted ? _encryptedOptions : new SettableOptions(this);

            for (int i = 0; i < optLen; i++)
            {
                string curForm = GetString(reader);

                MausCompressionFormat cmpFmt;
                if (_formDict.TryGetValue(curForm, out cmpFmt))
                {
                    if (gotFormat)
                    {
                        if (_cmpFmt != cmpFmt)
                            throw new InvalidDataException(TextResources.FormatBad);
                        continue;
                    }
                    else if (fromEncrypted)
                        options.InternalAdd(MausOptionToEncrypt.Compression);

                    gotFormat = true;
                    _cmpFmt = cmpFmt;
                    continue;
                }

                if (curForm.Equals(_encAes, StringComparison.Ordinal))
                {
                    _encryptedOptions = options;
                    CheckAdvance(optLen, ref i);

                    byte[] bytes = GetStringBytes(reader);
                    _blockByteCount = _blockByteCtAes;
                    int keyBits;
                    if (bytes.Length == 3)
                    {
                        string strVal = _textEncoding.GetString(bytes);
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

                    if (_keySizes == null)
                        _setKeySizes(keyBits);
                    else if (keyBits != _keySizes.MinSize && keyBits != _keySizes.MaxSize)
                        throw new InvalidDataException(TextResources.FormatBad);
                    _encFmt = MausEncryptionFormat.Aes;
                    continue;
                }

                if (curForm.Equals(_kFilename, StringComparison.Ordinal))
                {
                    CheckAdvance(optLen, ref i);

                    string filename = GetString(reader);

                    if (_filename == null)
                    {
                        if (fromEncrypted)
                            options.InternalAdd(MausOptionToEncrypt.Filename);
                    }
                    else if (!filename.Equals(_filename, StringComparison.Ordinal))
                        throw new InvalidDataException(TextResources.FormatBad);

                    if (!IsValidFilename(filename, false, _allowDirNames, null))
                        throw new InvalidDataException(TextResources.FormatFilename);
                    continue;
                }

                if (curForm.Equals(_kULen, StringComparison.Ordinal))
                {
                    CheckAdvance(optLen, ref i);

                    long uLen = GetValueInt64(reader);

                    if (uLen <= 0 || (gotULen && uLen != _uncompressedLength))
                        throw new InvalidDataException(TextResources.FormatBad);

                    _uncompressedLength = uLen;
                    gotULen = true;
                    continue;
                }

                if (curForm.Equals(_kTimeC, StringComparison.Ordinal))
                {
                    GetDate(reader, ref _timeC, optLen, ref i);
                    continue;
                }

                if (curForm.Equals(_kTimeM, StringComparison.Ordinal))
                {
                    GetDate(reader, ref _timeM, optLen, ref i);
                    continue;
                }

                if (curForm.Equals(_kComment, StringComparison.Ordinal))
                {
                    CheckAdvance(optLen, ref i);
                    byte[] buffer = GetStringBytes(reader);
                    if (buffer.Length != 1)
                        throw new InvalidDataException(TextResources.FormatBad);
                    int curLen = buffer[0];
                    if (curLen == 0) curLen = maxLen;

                    List<byte> byteList = new List<byte>(curLen << 8);
                    curLen--;

                    for (int j = 0; j <= curLen; j++)
                    {
                        CheckAdvance(optLen, ref i);
                        buffer = GetStringBytes(reader);
                        if (j < curLen && buffer.Length != maxLen)
                            throw new InvalidDataException(TextResources.FormatBad);
                        byteList.AddRange(buffer);
                    }

                    string comment = _textEncoding.GetString(byteList.ToArray());
                    if (_comment != null && !_comment.Equals(comment, StringComparison.Ordinal))
                        throw new InvalidDataException(TextResources.FormatBad);

                    _comment = comment;
                    continue;
                }

                if (version == _minVersionShort && curForm.Equals(_kEncFilename, StringComparison.Ordinal))
                {
                    if (_filename != null)
                        throw new InvalidDataException(TextResources.FormatBad);

                    options.InternalAdd(MausOptionToEncrypt.Filename);
                    continue;
                }

                throw new NotSupportedException(TextResources.FormatUnknown);
            }

            if (_minVersionShort == version && options.Contains(MausOptionToEncrypt.Filename) && _encFmt == MausEncryptionFormat.None)
                throw new InvalidDataException(TextResources.FormatBad);
        }

        private static readonly long maxTicks = DateTime.MaxValue.Ticks;

        private void GetDate(BinaryReader reader, ref DateTime? curTime, int optLen, ref int i)
        {
            CheckAdvance(optLen, ref i);

            long value = GetValueInt64(reader);

            if (value < 0 || value > maxTicks)
                throw new InvalidDataException(TextResources.FormatBad);

            DateTime newVal = new DateTime(value, DateTimeKind.Utc);

            if (curTime.HasValue && curTime.Value != newVal)
                throw new InvalidDataException(TextResources.FormatBad);

            curTime = newVal;
        }

        private static long GetValueInt64(BinaryReader reader)
        {
            if (reader.ReadByte() != sizeof(long))
                throw new InvalidDataException(TextResources.FormatBad);

            return reader.ReadInt64();
        }

        private static void CheckAdvance(int optLen, ref int i)
        {
            if (++i >= optLen)
                throw new InvalidDataException(TextResources.FormatBad);
        }

        private static string GetString(BinaryReader reader)
        {
            byte[] strBytes = GetStringBytes(reader);

            return _textEncoding.GetString(strBytes);
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
        private LzmaDictionarySize _lzmaDictSize;

        /// <summary>
        /// Attempts to pre-load the data in the current instance, and test whether <see cref="Key"/> is set to the correct value
        /// if the current stream is encrypted and to decrypt any encrypted options.
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
            _ensureCanRead();
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
                _bufferStream = new MausBufferStream();
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

                if (version > _minVersionShort)
                {
#if NOLEAVEOPEN
                    BinaryReader reader = new BinaryReader(bufferStream);
#else
                    using (BinaryReader reader = new BinaryReader(bufferStream, _textEncoding, true))
#endif
                    {
                        ReadOptions(reader, true);
                    }
                }
                else if (_encryptedOptions.Contains(MausOptionToEncrypt.Filename))
                {
                    string filename;
#if NOLEAVEOPEN
                    BinaryReader reader = new BinaryReader(bufferStream);
#else
                    using (BinaryReader reader = new BinaryReader(bufferStream, _textEncoding, true))
#endif
                    {
                        filename = GetString(reader);
                    }

                    if (!IsValidFilename(filename, false, _allowDirNames, null))
                        throw new InvalidDataException(TextResources.FormatFilename);
                    _filename = filename;
                }

                _bufferStream.Close();
                _bufferStream = bufferStream;
            }

            switch (_cmpFmt)
            {
                case MausCompressionFormat.Lzma:
                    _deflateStream = _bufferStream;
                    _bufferStream = new MausBufferStream();
                    const int optLen = 5;

                    byte[] opts = new byte[optLen];
                    if (_deflateStream.Read(opts, 0, optLen) < optLen) throw new EndOfStreamException();
                    LzmaDecoder decoder = new LzmaDecoder();
                    decoder.SetDecoderProperties(opts);
                    if (decoder.DictionarySize < 1 || decoder.DictionarySize > (uint)LzmaDictionarySize.MaxValue)
                        throw new InvalidDataException();
                    try
                    {
                        decoder.Code(_deflateStream, _bufferStream, _deflateStream.Length - optLen, -1);
                    }
                    catch (DataErrorException)
                    {
                        throw new InvalidDataException();
                    }
                    _bufferStream.Reset();
                    _deflateStream.Close();
                    _deflateStream = _bufferStream;
                    break;
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
            _ensureCanRead();
            CheckSegment(buffer, offset, count);
            if (count == 0) return 0;
            lock (_lock)
            {
                _readData();
            }

            if (gotULen)
                count = (int)Math.Min(count, _uncompressedLength);

            int result = _deflateStream.Read(buffer, offset, count);

            if (gotULen)
            {
                if (result < count)
                    throw new EndOfStreamException();
                _uncompressedLength -= result;
            }
            return result;
        }

        private object _lock = new object();

        private void _ensureCanRead()
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
            _ensureCanWrite();
            _deflateStream.Write(buffer, offset, count);
            _uncompressedLength += count;
        }

        private void _ensureCanWrite()
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
                return;

            try
            {
                if (!disposing)
                    return;

                try
                {
                    WriteFile();
                }
                finally
                {
                    if (_bufferStream != null)
                        _bufferStream.Dispose();
                    if (_deflateStream != null)
                        _deflateStream.Dispose();

                    if (!_leaveOpen)
                        _baseStream.Dispose();
                }
            }
            finally
            {
                _baseStream = null;

                _bufferStream = null;
                _deflateStream = null;
                base.Dispose(disposing);
                GC.SuppressFinalize(this);
            }
        }

        private static readonly CoderPropID[] ids = new CoderPropID[]
        {
            CoderPropID.DictionarySize,
            CoderPropID.PosStateBits,
            CoderPropID.LitContextBits,
            CoderPropID.LitPosBits,
            CoderPropID.NumFastBytes,
            CoderPropID.Algorithm,
            CoderPropID.MatchFinder,
            CoderPropID.EndMarker,
        };

        private void WriteFile()
        {
            if (_entry != null && _entry.Archive == null)
                return;
            if (_deflateStream == null || _bufferStream == null || _mode != CompressionMode.Compress || _uncompressedLength == 0)
                return;
            if (_encFmt != MausEncryptionFormat.None && _key == null)
                throw new InvalidOperationException(TextResources.KeyNotSet);

            if (_deflateStream != _bufferStream)
            {
                _deflateStream.Dispose();
                _deflateStream = null;
            }
            else if (_cmpFmt == MausCompressionFormat.Lzma)
            {
                _bufferStream.Reset();
                _bufferStream = new MausBufferStream();

                LzmaEncoder encoder = new LzmaEncoder();
                object[] props = new object[]
                {
                        (int)_lzmaDictSize,
                        2,
                        3,
                        0,
                        128,
                        2,
                        "BT4",
                        true,
                };
                encoder.SetCoderProperties(ids, props);

                encoder.WriteCoderProperties(_bufferStream);
                encoder.Code(_deflateStream, _bufferStream, _deflateStream.Length, -1);
                _deflateStream.Dispose();
                _deflateStream = null;
            }

            _bufferStream.Reset();
#if NOLEAVEOPEN
            BinaryWriter writer = new BinaryWriter(_baseStream);
#else
            using (BinaryWriter writer = new BinaryWriter(_baseStream, _textEncoding, true))
#endif
            {
                writer.Write(_head);
                writer.Write(_versionShort);
                {
                    List<byte[]> formats = new List<byte[]>();

                    if (_encryptedOptions == null || !_encryptedOptions.Contains(MausOptionToEncrypt.Filename))
                        FormatSetFilename(formats);

                    if (_encryptedOptions == null || !_encryptedOptions.Contains(MausOptionToEncrypt.Compression))
                        FormatSetCompression(formats);

                    if (_encryptedOptions == null || !_encryptedOptions.Contains(MausOptionToEncrypt.ModTime))
                        FormatSetTimes(formats);

                    if (_encryptedOptions == null || !_encryptedOptions.Contains(MausOptionToEncrypt.Comment))
                        FormatSetComment(formats);

                    if (_encFmt == MausEncryptionFormat.Aes)
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

                    WriteFormats(writer, formats);
                }

                if (_encFmt == MausEncryptionFormat.None)
                {
                    writer.Write(_bufferStream.Length);
                    writer.Write(_uncompressedLength);

                    _bufferStream.Reset();
                    byte[] hashChecksum = ComputeHash(_bufferStream);
                    writer.Write(hashChecksum);

                    _bufferStream.Reset();

                    _bufferStream.BufferCopyTo(_baseStream);
                }
                else
                {
                    using (MausBufferStream opts = new MausBufferStream())
                    {
                        List<byte[]> formats = new List<byte[]>();

                        foreach (MausOptionToEncrypt opt in _encryptedOptions)
                        {
                            switch (opt)
                            {
                                case MausOptionToEncrypt.Filename:
                                    FormatSetFilename(formats);
                                    continue;
                                case MausOptionToEncrypt.Compression:
                                    FormatSetCompression(formats);
                                    continue;
                                case MausOptionToEncrypt.ModTime:
                                    FormatSetTimes(formats);
                                    continue;
                                case MausOptionToEncrypt.Comment:
                                    FormatSetComment(formats);
                                    continue;
                            }
                        }

                        formats.Add(_bULen);
                        formats.Add(GetBytes(_uncompressedLength));

                        using (BinaryWriter formatWriter = new BinaryWriter(opts))
                        {
                            WriteFormats(formatWriter, formats);
                            _bufferStream.Prepend(opts);
                        }
                    }

                    using (MausBufferStream output = Encrypt())
                    {
                        writer.Write(output.Length);
                        writer.Write((long)_pkCount);

                        _bufferStream.Reset();
                        byte[] hashHmac = ComputeHmac(_bufferStream);
                        _baseStream.Write(hashHmac, 0, hashLength);

                        output.BufferCopyTo(_baseStream);
                    }
                }
            }
#if NOLEAVEOPEN
            _baseStream.Flush();
#endif
        }

        private byte[] GetBytes(long value)
        {
            return new byte[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24),
                (byte)(value >> 32), (byte)(value >> 40), (byte)(value >> 48), (byte)(value >> 56) };
        }

        private void FormatSetFilename(List<byte[]> formats)
        {
            if (_filename != null)
            {
                formats.Add(_bFilename);
                formats.Add(_textEncoding.GetBytes(_filename));
            }
        }

        private void FormatSetCompression(List<byte[]> formats)
        {
            switch (_cmpFmt)
            {
                case MausCompressionFormat.None:
                    formats.Add(_cmpBNone);
                    break;
                case MausCompressionFormat.Lzma:
                    formats.Add(_cmpBLzma);
                    break;
                default:
                    formats.Add(_cmpBDef);
                    break;
            }
        }

        private void FormatSetTimes(List<byte[]> formats)
        {
            if (_timeC.HasValue)
            {
                formats.Add(_bTimeC);
                formats.Add(GetBytes(_timeC.Value.ToUniversalTime().Ticks));
            }

            if (_timeM.HasValue)
            {
                formats.Add(_bTimeM);
                formats.Add(GetBytes(_timeM.Value.ToUniversalTime().Ticks));
            }
        }

        private void FormatSetComment(List<byte[]> formats)
        {
            if (string.IsNullOrEmpty(_comment))
                return;

            formats.Add(_bComment);
            byte[] allBytes = _textEncoding.GetBytes(_comment);

            byte byteLen = (byte)Math.Ceiling((double)allBytes.Length / maxLen);

            formats.Add(new byte[] { byteLen });
            for (int i = 0; i < allBytes.Length; i += maxLen)
            {
                byte[] curBuffer = new byte[Math.Min(maxLen, allBytes.Length - i)];
                Array.Copy(allBytes, i, curBuffer, 0, curBuffer.Length);
                formats.Add(curBuffer);
            }
        }

        private static void WriteFormats(BinaryWriter writer, List<byte[]> formats)
        {
            writer.Write((ushort)formats.Count);

            for (int i = 0; i < formats.Count; i++)
            {
                byte[] curForm = formats[i];
                writer.Write((byte)curForm.Length);
                writer.Write(curForm);
            }
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        ~DieFledermausStream()
        {
            Dispose(false);
        }

        /// <summary>
        /// Represents a collection of <see cref="MausOptionToEncrypt"/> options.
        /// </summary>
        public sealed class SettableOptions : ICollection<MausOptionToEncrypt>, ICollection
#if IREADONLY
            , IReadOnlyCollection<MausOptionToEncrypt>
#endif
        {
            private static readonly HashSet<MausOptionToEncrypt> _allVals = new HashSet<MausOptionToEncrypt>(Enum.GetValues(typeof(MausOptionToEncrypt))
                .OfType<MausOptionToEncrypt>());

            private HashSet<MausOptionToEncrypt> _set;
            private DieFledermausStream _stream;

            internal SettableOptions(DieFledermausStream stream)
            {
                _stream = stream;
                _set = new HashSet<MausOptionToEncrypt>();
            }

            /// <summary>
            /// Gets the number of elements contained in the collection.
            /// </summary>
            public int Count { get { return _set.Count; } }

            /// <summary>
            /// Gets a value indicating whether the current instance is read-only.
            /// Returns <c>true</c> if the underlying stream is closed or is in read-mode; <c>false</c> otherwise.
            /// </summary>
            /// <remarks>
            /// This property indicates that the collection cannot be changed externally. If <see cref="IsFrozen"/> is <c>false</c>,
            /// however, it may still be changed by the base stream.
            /// </remarks>
            public bool IsReadOnly
            {
                get { return _stream._baseStream == null || _stream._mode == CompressionMode.Decompress; }
            }

            /// <summary>
            /// Gets a value indicating whether the current instance is entirely immutable.
            /// Returns <c>true</c> if the underlying stream is closed or is in read-mode and has successfully decoded the file;
            /// <c>false</c> otherwise.
            /// </summary>
            private bool IsFrozen
            {
                get { return _stream._baseStream == null || (_stream._mode == CompressionMode.Decompress && (_minVersionShort == _stream.version || _stream._headerGotten)); }
            }
            bool ICollection.IsSynchronized { get { return IsFrozen; } }

            /// <summary>
            /// Adds the specified value to the collection.
            /// </summary>
            /// <param name="option">The option to add.</param>
            /// <returns><c>true</c> if <paramref name="option"/> was successfully added; <c>false</c> if <paramref name="option"/>
            /// already exists in the collection, or is not a valid <see cref="MausOptionToEncrypt"/> value.</returns>
            /// <exception cref="NotSupportedException">
            /// <see cref="IsReadOnly"/> is <c>true</c>.
            /// </exception>
            public bool Add(MausOptionToEncrypt option)
            {
                if (IsReadOnly) throw new NotSupportedException(TextResources.CollectReadOnly);
                return _set.Add(option);
            }

            internal bool InternalAdd(MausOptionToEncrypt option)
            {
                return _set.Add(option);
            }

            void ICollection<MausOptionToEncrypt>.Add(MausOptionToEncrypt item)
            {
                Add(item);
            }

            /// <summary>
            /// Removes the specified value from the collection.
            /// </summary>
            /// <param name="option">The option to remove.</param>
            /// <returns><c>true</c> if <paramref name="option"/> was found and successfully removed; <c>false</c> otherwise.</returns>
            /// <exception cref="NotSupportedException">
            /// <see cref="IsReadOnly"/> is <c>true</c>.
            /// </exception>
            public bool Remove(MausOptionToEncrypt option)
            {
                if (IsReadOnly) throw new NotSupportedException(TextResources.CollectReadOnly);
                return _set.Remove(option);
            }

            /// <summary>
            /// Adds all elements in the specified collection to the current instance (excluding duplicates and values already in the current collection).
            /// </summary>
            /// <param name="other">A collection containing other values to add.</param>
            /// <exception cref="ArgumentNullException">
            /// <paramref name="other"/> is <c>null</c>.
            /// </exception>
            /// <exception cref="NotSupportedException">
            /// <see cref="IsReadOnly"/> is <c>true</c>.
            /// </exception>
            public void AddRange(IEnumerable<MausOptionToEncrypt> other)
            {
                if (IsReadOnly) throw new NotSupportedException(TextResources.CollectReadOnly);
                _set.UnionWith(other);
            }

            /// <summary>
            /// Adds all available values to the collection.
            /// </summary>
            /// <exception cref="NotSupportedException">
            /// <see cref="IsReadOnly"/> is <c>true</c>.
            /// </exception>
            public void AddAll()
            {
                AddRange(_allVals);
            }

            /// <summary>
            /// Removes all elements matching the specified predicate from the list.
            /// </summary>
            /// <param name="match">A predicate defining the elements to remove.</param>
            /// <exception cref="ArgumentNullException">
            /// <paramref name="match"/> is <c>null</c>.
            /// </exception>
            public void RemoveWhere(Predicate<MausOptionToEncrypt> match)
            {
                _set.RemoveWhere(match);
            }

            /// <summary>
            /// Removes all elements from the collection.
            /// </summary>
            public void Clear()
            {
                if (IsReadOnly) throw new NotSupportedException(TextResources.CollectReadOnly);
                _set.Clear();
            }

            /// <summary>
            /// Determines if the specified value exists in the collection.
            /// </summary>
            /// <param name="option">The option to search for in the collection.</param>
            /// <returns><c>true</c> if <paramref name="option"/> was found; <c>false</c> otherwise.</returns>
            public bool Contains(MausOptionToEncrypt option)
            {
                return _set.Contains(option);
            }

            /// <summary>
            /// Copies all elements in the collection to the specified array, starting at the specified index.
            /// </summary>
            /// <param name="array">The array to which the collection will be copied. The array must have zero-based indexing.</param>
            /// <param name="arrayIndex">The index in <paramref name="array"/> at which copying begins.</param>
            /// <exception cref="ArgumentNullException">
            /// <paramref name="array"/> is <c>null</c>.
            /// </exception>
            /// <exception cref="ArgumentOutOfRangeException">
            /// <paramref name="arrayIndex"/> is less than 0.
            /// </exception>
            /// <exception cref="ArgumentException">
            /// <paramref name="arrayIndex"/> plus <see cref="Count"/> is greater than the length of <paramref name="array"/>.
            /// </exception>
            public void CopyTo(MausOptionToEncrypt[] array, int arrayIndex)
            {
                _set.CopyTo(array, arrayIndex);
            }

            /// <summary>
            /// Returns an enumerator which iterates through the collection.
            /// </summary>
            /// <returns>An enumerator which iterates through the collection.</returns>
            public Enumerator GetEnumerator()
            {
                return new Enumerator(this);
            }

            IEnumerator<MausOptionToEncrypt> IEnumerable<MausOptionToEncrypt>.GetEnumerator()
            {
                return GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            [NonSerialized]
            private object _syncRoot;
            object ICollection.SyncRoot
            {
                get
                {
                    if (_syncRoot == null)
                        System.Threading.Interlocked.CompareExchange<object>(ref _syncRoot, new object(), null);

                    return _syncRoot;
                }
            }

            void ICollection.CopyTo(Array array, int index)
            {
                if (array == null) throw new ArgumentNullException(nameof(array));
                if (array.Rank != 1 || array.GetLowerBound(0) != 0)
                    throw new ArgumentException(TextResources.CollectBadArray, nameof(array));
                if (index < 0) throw new ArgumentOutOfRangeException(TextResources.OutOfRangeLessThanZero, index, nameof(index));

                MausOptionToEncrypt[] mArray = array as MausOptionToEncrypt[];

                if (mArray != null)
                {
                    _set.CopyTo(mArray, index);
                    return;
                }

                try
                {
                    object[] oArray = array as object[];
                    int i = index;

                    if (oArray == null)
                    {
                        foreach (MausOptionToEncrypt opt in _set)
                            mArray.SetValue(opt, i++);
                    }
                    else
                    {
                        foreach (MausOptionToEncrypt opt in _set)
                            oArray[i++] = opt;
                    }
                }
                catch (InvalidCastException x)
                {
                    throw new ArgumentException(TextResources.CollectBadArrayType, nameof(array), x);
                }
            }

            /// <summary>
            /// An enumerator which iterates through the collection.
            /// </summary>
            public struct Enumerator : IEnumerator<MausOptionToEncrypt>
            {
                private IEnumerator<MausOptionToEncrypt> _enum;

                internal Enumerator(SettableOptions sOpts)
                {
                    _enum = sOpts._set.GetEnumerator();
                    _current = 0;
                }

                private MausOptionToEncrypt _current;
                /// <summary>
                /// Gets the element at the current position in the enumerator.
                /// </summary>
                public MausOptionToEncrypt Current
                {
                    get { return _current; }
                }

                object IEnumerator.Current
                {
                    get { return _enum.Current; }
                }

                /// <summary>
                /// Disposes of the current instance.
                /// </summary>
                public void Dispose()
                {
                    if (_enum == null) return;
                    _enum.Dispose();
                    this = default(Enumerator);
                }

                /// <summary>
                /// Advances the enumerator to the next position in the collection.
                /// </summary>
                /// <returns><c>true</c> if the enumerator was successfully advanced; 
                /// <c>false</c> if the enumerator has passed the end of the collection.</returns>
                public bool MoveNext()
                {
                    if (_enum == null) return false;
                    if (!_enum.MoveNext())
                    {
                        Dispose();
                        return false;
                    }
                    _current = _enum.Current;
                    return true;
                }

                void IEnumerator.Reset()
                {
                    _enum.Reset();
                }
            }
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
        /// <summary>
        /// The file is compressed using the Lempel-Ziv-Markov chain algorithm
        /// </summary>
        Lzma,
    }

    /// <summary>
    /// Indicates values to encrypt.
    /// </summary>
    public enum MausOptionToEncrypt
    {
        /// <summary>
        /// Indicates that <see cref="DieFledermausStream.Filename"/> will be encrypted.
        /// </summary>
        Filename,
        /// <summary>
        /// Indicates that <see cref="DieFledermausStream.CompressionFormat"/> will be encrypted.
        /// </summary>
        Compression,
        /// <summary>
        /// Indicates that <see cref="DieFledermausStream.CreatedTime"/> and <see cref="DieFledermausStream.ModifiedTime"/> will be encrypted.
        /// </summary>
        ModTime,
        /// <summary>
        /// Indicates that <see cref="DieFledermausStream.Comment"/> will be encrypted.
        /// </summary>
        Comment,
    }

    /// <summary>
    /// Options for setting the LZMA dictionary size.
    /// A larger value alows a smaller compression size, but results in a higher memory usage when encoding and decoding and a longer encoding time. 
    /// </summary>
    public enum LzmaDictionarySize
    {
        /// <summary>
        /// The default value, <see cref="Size8m"/>
        /// </summary>
        Default = 0,
        /// <summary>
        /// 16 kilobytes.
        /// </summary>
        Size16k = 1 << 14,
        /// <summary>
        /// 64 kilobytes.
        /// </summary>
        Size64k = 1 << 16,
        /// <summary>
        /// 1 megabyte.
        /// </summary>
        Size1m = 1 << 20,
        /// <summary>
        /// 2 megabytes.
        /// </summary>
        Size2m = 1 << 21,
        /// <summary>
        /// 3 megabytes.
        /// </summary>
        Size3m = Size1m + Size2m,
        /// <summary>
        /// 4 megabytes.
        /// </summary>
        Size4m = 1 << 22,
        /// <summary>
        /// 6 megabytes.
        /// </summary>
        Size6m = Size3m * 2,
        /// <summary>
        /// 8 megabytes.
        /// </summary>
        Size8m = 1 << 23,
        /// <summary>
        /// 12 megabytes.
        /// </summary>
        Size12m = Size6m * 2,
        /// <summary>
        /// 16 megabytes.
        /// </summary>
        Size16m = 1 << 24,
        /// <summary>
        /// 24 megabytes.
        /// </summary>
        Size24m = Size12m * 2,
        /// <summary>
        /// 32 megabytes.
        /// </summary>
        Size32m = 1 << 25,
        /// <summary>
        /// 48 megabytes.
        /// </summary>
        Size48m = Size24m * 2,
        /// <summary>
        /// 64 megabytes.
        /// </summary>
        Size64m = 1 << 26,
        /// <summary>
        /// The minimum value, equal to <see cref="Size16k"/>
        /// </summary>
        MinValue = Size16k,
        /// <summary>
        /// The maximum value, equal to <see cref="Size64m"/>.
        /// </summary>
        MaxValue = Size64m,
    }
}
