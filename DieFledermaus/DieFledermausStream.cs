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
using System.Security.Cryptography;
using System.Text;

using DieFledermaus.Globalization;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

using SevenZip;
using SevenZip.Compression.LZMA;

namespace DieFledermaus
{
    /// <summary>
    /// Provides methods and properties for compressing and decompressing files and streams in the DieFledermaus format,
    /// which is just the DEFLATE algorithm prefixed with magic number "<c>mAuS</c>" and metadata.
    /// </summary>
    /// <remarks>
    /// Unlike streams such as <see cref="DeflateStream"/>, this method reads part of the stream during the constructor, rather than the first call
    /// to <see cref="Read(byte[], int, int)"/> or <see cref="ReadByte()"/>.
    /// </remarks>
    public partial class DieFledermausStream : Stream, IMausCrypt, IMausProgress, IMausStream
    {
        internal const int Max16Bit = 65536;
        internal const int _head = 0x5375416d; //Little-endian "mAuS"
        private const ushort _versionShort = 98, _minVersionShort = _versionShort;

        internal static readonly UTF8Encoding _textEncoding = new UTF8Encoding(false, false);

        private Stream _baseStream;
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
        /// Creates a new instance in the specified mode.
        /// </summary>
        /// <param name="stream">The stream to read to or write from.</param>
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
                _baseStream = stream;
            }
            else if (compressionMode == CompressionMode.Decompress)
            {
                CheckRead(stream);
                _baseStream = stream;
                if (stream.CanSeek && stream.Length == stream.Position)
                    stream.Seek(0, SeekOrigin.Begin);

                _getHeader(true);
            }
            else throw new InvalidEnumArgumentException(nameof(compressionMode), (int)compressionMode, typeof(CompressionMode));
            _mode = compressionMode;
            _leaveOpen = leaveOpen;
        }

        /// <summary>
        /// Creates a new instance with the specified mode.
        /// </summary>
        /// <param name="stream">The stream to read to or write from.</param>
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
        /// <param name="stream">The stream to write to.</param>
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
        /// <param name="stream">The stream to write to.</param>
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
        /// <param name="stream">The stream to write to.</param>
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
                case MausCompressionFormat.None:
                case MausCompressionFormat.Lzma:
                    _cmpFmt = compressionFormat;
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(compressionFormat), (int)compressionFormat, typeof(MausCompressionFormat));
            }

            _baseStream = stream;
            _mode = CompressionMode.Compress;
            _leaveOpen = leaveOpen;
        }

        /// <summary>
        /// Creates a new instance in write-mode, with the specified compression format and no encryption.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
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
        /// <param name="stream">The stream to write to.</param>
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
        /// <param name="stream">The stream to write to.</param>
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
        /// <param name="stream">The stream to write to.</param>
        /// <param name="dictionarySize">Indicates the size of the dictionary, in bytes.</param>
        /// <param name="leaveOpen"><c>true</c> to leave <paramref name="stream"/> open when the current instance is disposed;
        /// <c>false</c> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="dictionarySize"/> is not <see cref="LzmaDictionarySize.Default"/>, and is an integer value less than
        /// <see cref="LzmaDictionarySize.MinValue"/> or greater than <see cref="LzmaDictionarySize.MaxValue"/>.
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
                throw new ArgumentOutOfRangeException(nameof(dictionarySize), dictionarySize, TextResources.OutOfRangeLzma);
            _lzmaDictSize = dictionarySize;
        }

        /// <summary>
        /// Creates a new instance in write-mode with LZMA encryption, using the specified dictionary size and no encryption.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="dictionarySize">Indicates the size of the dictionary, in bytes.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="dictionarySize"/> is not <see cref="LzmaDictionarySize.Default"/>, and is an integer value less than
        /// <see cref="LzmaDictionarySize.MinValue"/> or greater than <see cref="LzmaDictionarySize.MaxValue"/>.
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
        /// <param name="stream">The stream to write to.</param>
        /// <param name="dictionarySize">Indicates the size of the dictionary, in bytes.</param>
        /// <param name="encryptionFormat">Indicates the encryption format.</param>
        /// <param name="leaveOpen"><c>true</c> to leave <paramref name="stream"/> open when the current instance is disposed;
        /// <c>false</c> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="dictionarySize"/> is not <see cref="LzmaDictionarySize.Default"/>, and is an integer value less than
        /// <see cref="LzmaDictionarySize.MinValue"/> or greater than <see cref="LzmaDictionarySize.MaxValue"/>.
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
        /// <param name="stream">The stream to write to.</param>
        /// <param name="dictionarySize">Indicates the size of the dictionary, in bytes.</param>
        /// <param name="encryptionFormat">Indicates the encryption format.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="dictionarySize"/> is not <see cref="LzmaDictionarySize.Default"/>, and is an integer value less than
        /// <see cref="LzmaDictionarySize.MinValue"/> or greater than <see cref="LzmaDictionarySize.MaxValue"/>.
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
        /// <remarks>This constructor is only available in .Net 4.5 and higher.</remarks>
        public DieFledermausStream(Stream stream, CompressionLevel compressionLevel, bool leaveOpen)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            CheckWrite(stream);
            _setCompLvl(compressionLevel);
            _bufferStream = new MausBufferStream();
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
        /// <remarks>This constructor is only available in .Net 4.5 and higher.</remarks>
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
        /// <remarks>This constructor is only available in .Net 4.5 and higher.</remarks>
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
        /// <remarks>This constructor is only available in .Net 4.5 and higher.</remarks>
        public DieFledermausStream(Stream stream, CompressionLevel compressionLevel, MausEncryptionFormat encryptionFormat)
            : this(stream, compressionLevel, encryptionFormat, false)
        {
        }
#endif

        internal DieFledermausStream(DieFledermauZItem entry, string path, Stream stream, ICompressionFormat compFormat, MausEncryptionFormat encryptionFormat)
        {
            _baseStream = stream;
            _bufferStream = new MausBufferStream();
            switch (compFormat.CompressionFormat)
            {
                default:
#if COMPLVL
                    _setCompLvl(((DeflateCompressionFormat)compFormat).CompressionLevel);
#endif
                    break;
                case MausCompressionFormat.Lzma:
                    _lzmaDictSize = ((LzmaCompressionFormat)compFormat).DictionarySize;
                    break;
                case MausCompressionFormat.None:
                    break;
            }
            _cmpFmt = compFormat.CompressionFormat;
            _entry = entry;
            _setEncFormat(encryptionFormat);
            _filename = path;
            _allowDirNames = entry is DieFledermauZArchiveEntry ? AllowDirNames.Yes : AllowDirNames.EmptyDir;
            _mode = CompressionMode.Compress;
            _leaveOpen = true;
        }

        internal DieFledermausStream(Stream stream, bool readMagNum, string path)
        {
            _baseStream = stream;
            _mode = CompressionMode.Decompress;
            _leaveOpen = true;
            if (path == null)
                _allowDirNames = AllowDirNames.Unknown;
            else if (path[path.Length - 1] == '/')
                _allowDirNames = AllowDirNames.EmptyDir;
            else
                _allowDirNames = AllowDirNames.Yes;
            _getHeader(readMagNum);
        }
        #endregion

#if COMPLVL
        private CompressionLevel _cmpLvl;

        private void _setCompLvl(CompressionLevel compressionLevel)
        {
            switch (compressionLevel)
            {
                case CompressionLevel.Fastest:
                case CompressionLevel.NoCompression:
                case CompressionLevel.Optimal:
                    _cmpLvl = compressionLevel;
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(compressionLevel), (int)compressionLevel, typeof(CompressionLevel));
            }
        }
#endif

        internal DieFledermauZItem _entry;

        private void _setEncFormat(MausEncryptionFormat encryptionFormat)
        {
            _keySizes = _getKeySizes(encryptionFormat, out _blockByteCount);
            _encFmt = encryptionFormat;
            if (_encFmt == MausEncryptionFormat.None) return;
            _encryptedOptions = new SettableOptions(this);
            _keySize = _keySizes.MaxSize;
            do
            {
                _iv = FillBuffer(_blockByteCount);
                _salt = FillBuffer(_keySize >> 3);
            }
            while (_entry != null && (_entry.Archive.Entries.Any(_checkOtherEntry) ||
                (_entry.Archive.EncryptionFormat == _encFmt && _checkOther(_entry.Archive.IV, _entry.Archive.Salt))));
        }

        private bool _checkOtherEntry(DieFledermauZItem other)
        {
            if (other == _entry || other.MausStream._encFmt != _encFmt || other.MausStream._iv.Length != _iv.Length || other.MausStream._salt.Length != _salt.Length)
                return false;

            return _checkOther(other.MausStream._iv, other.MausStream._salt);
        }

        private bool _checkOther(byte[] otherIv, byte[] otherSalt)
        {
            for (int i = 0; i < _iv.Length; i++)
            {
                if (_iv[i] != otherIv[i])
                    return false;
            }

            for (int i = 0; i < _salt.Length; i++)
            {
                if (_salt[i] != otherSalt[i])
                    return false;
            }

            return true;
        }

        internal static KeySizes _getKeySizes(MausEncryptionFormat encryptionFormat, out int blockByteCount)
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
        /// Gets a value indicating whether the current stream supports seeking. Always returns <c>false</c>.
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

        private KeySizes _keySizes;
        /// <summary>
        /// Gets a <see cref="System.Security.Cryptography.KeySizes"/> object indicating all valid key sizes
        /// for <see cref="EncryptionFormat"/>, or <c>null</c> if the current stream is not encrypted.
        /// </summary>
        public KeySizes KeySizes { get { return _keySizes; } }

        private MausCompressionFormat _cmpFmt;
        /// <summary>
        /// Gets the compression format of the current instance.
        /// </summary>
        public MausCompressionFormat CompressionFormat { get { return _cmpFmt; } }

        /// <summary>
        /// Gets a value indicating whether the current instance is in read-mode and has been successfully decrypted.
        /// </summary>
        public bool IsDecrypted { get { return _mode == CompressionMode.Decompress && _encFmt == MausEncryptionFormat.None || _headerGotten; } }
        internal bool DataIsLoaded { get { return _bufferStream != null; } }

        private DateTime? _timeC;
        /// <summary>
        /// Gets and sets the time at which the underlying file was created, or <c>null</c> to specify no creation time.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
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
        /// <exception cref="NotSupportedException">
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

        private byte[] _hashExpected;
        /// <summary>
        /// Gets the hash of the uncompressed data, or <c>null</c> if the current instance is in write-mode or has not yet been decrypted.
        /// </summary>
        public byte[] Hash
        {
            get
            {
                if (_hashExpected == null || !_headerGotten) return null;
                return (byte[])_hashExpected.Clone();
            }
        }

        private byte[] _hmacExpected;
        /// <summary>
        /// Gets the loaded HMAC of the encrypted data, or <c>null</c> if the current instance is in write-mode or is not encrypted.
        /// </summary>
        public byte[] HMAC
        {
            get
            {
                if (_hmacExpected == null) return null;
                return (byte[])_hmacExpected.Clone();
            }
        }

        private byte[] _iv;
        /// <summary>
        /// Gets and sets the initialization vector used when encrypting the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para>In a set operation, the current stream is in write-mode.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current stream is not encrypted.</para>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// In a set operation, the specified value is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the length of the specified value is not equal to <see cref="BlockByteCount"/>.
        /// </exception>
        public byte[] IV
        {
            get
            {
                if (_iv == null) return null;
                return (byte[])_salt.Clone();
            }
            set
            {
                _ensureCanWrite();
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (value.Length != _blockByteCount) throw new ArgumentException(TextResources.IvLength, nameof(value));
                _iv = (byte[])value.Clone();
            }
        }

        private byte[] _salt;
        /// <summary>
        /// Gets and sets the salt used to help obfuscate the key when setting the password.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para>In a set operation, the current stream is in write-mode.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current stream is not encrypted.</para>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// In a set operation, the specified value is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the length of the specified value is less than the maximum key length specified by <see cref="KeySizes"/>.
        /// </exception>
        public byte[] Salt
        {
            get
            {
                if (_salt == null) return null;
                return (byte[])_salt.Clone();
            }
            set
            {
                _ensureCanWrite();

                if (value == null) throw new ArgumentNullException(nameof(value));
                if (value.Length < (_keySizes.MaxSize >> 3))
                    throw new ArgumentException(TextResources.SaltLength, nameof(value));

                _salt = (byte[])value.Clone();
            }
        }

        private int _keySize;
        /// <summary>
        /// Gets and sets the number of bits in the key.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para>In a set operation, the current instance is in read-mode.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current instance is not encrypted.</para>
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// In a set operation, the specified value is invalid according to <see cref="KeySizes"/>.
        /// </exception>
        public int KeySize
        {
            get { return _keySize; }
            set
            {
                _ensureCanWrite();
                if (_encFmt == MausEncryptionFormat.None)
                    throw new NotSupportedException(TextResources.NotEncrypted);
                if (!IsValidKeyBitSize(value))
                    throw new ArgumentOutOfRangeException(nameof(value), value, TextResources.KeyLength);
                _keySize = value;
            }
        }

        /// <summary>
        /// Determines whether the specified value is a valid length for a key in bytes.
        /// </summary>
        /// <param name="byteCount">The number of bytes to test.</param>
        /// <returns><c>true</c> if <paramref name="byteCount"/> is a valid byte count according to <see cref="KeySizes"/>;
        /// <c>false</c> if <paramref name="byteCount"/> is invalid, or if the current instance is not encrypted.</returns>
        public bool IsValidKeyByteSize(int byteCount)
        {
            if (_keySizes == null || byteCount > int.MaxValue >> 3) return false;

            return IsValidKeyBitSize(byteCount << 3, _keySizes);
        }

        /// <summary>
        /// Determines whether the specified value is a valid length for a key in bits.
        /// </summary>
        /// <param name="bitCount">The number of bits to test.</param>
        /// <returns><c>true</c> if <paramref name="bitCount"/> is a valid bit count according to <see cref="KeySizes"/>;
        /// <c>false</c> if <paramref name="bitCount"/> is invalid, or if the current instance is not encrypted.</returns>
        public bool IsValidKeyBitSize(int bitCount)
        {
            if (_keySizes == null) return false;

            return IsValidKeyBitSize(bitCount, _keySizes);
        }

        internal static bool IsValidKeyBitSize(int bitCount, KeySizes _keySizes)
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

        #region RSA Signature
        private byte[] _rsaSignature;

        private bool _rsaSignVerified;
        /// <summary>
        /// Gets a value indicating whether the current stream has been successfully verified using <see cref="RSASignParameters"/>.
        /// </summary>
        public bool IsRSASignVerified { get { return _rsaSignVerified; } }

        /// <summary>
        /// Gets a value indicating whether the current instance is signed using an RSA private key.
        /// </summary>
        /// <remarks>
        /// If the current stream is in read-mode, this property will return <c>true</c> if and only if the underlying stream
        /// was signed when it was written.
        /// If the current stream is in write-mode, this property will return <c>true</c> if <see cref="RSASignParameters"/>
        /// is not <c>null</c>.
        /// </remarks>
        public bool IsRSASigned
        {
            get
            {
                if (_mode == CompressionMode.Compress)
                    return _rsaSignParamBC != null;
                return _rsaSignature != null;
            }
        }

        private RsaKeyParameters _rsaSignParamBC;
        /// <summary>
        /// Gets and sets an RSA key used to sign the current stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current stream is in read-mode, and is not signed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, the current stream is in read-mode, and has already been verified.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>In a set operation, the current stream is in write-mode, and the specified value does not represent a valid private key.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current stream is in read-mode, and the specified value does not represent a valid public key.</para>
        /// </exception>
        public RsaKeyParameters RSASignParameters
        {
            get { return _rsaSignParamBC; }
            set
            {
                if (_baseStream == null)
                    throw new ObjectDisposedException(null, TextResources.CurrentClosed);

                CheckSignParam(value, _rsaSignature, _rsaSignVerified);
                _rsaSignParamBC = value;
            }
        }

        private void CheckSignParam(AsymmetricKeyParameter value, object signature, bool signVerified)
        {
            if (_mode == CompressionMode.Decompress)
            {
                if (signature == null)
                    throw new NotSupportedException(TextResources.RsaSigNone);
                if (signVerified)
                    throw new InvalidOperationException(TextResources.RsaSigVerified);
                if (value != null && value.IsPrivate)
                    throw new ArgumentException(TextResources.RsaNeedPublic, nameof(value));
            }
            else if (value != null && !value.IsPrivate)
                throw new ArgumentException(TextResources.RsaNeedPrivate, nameof(value));
        }

        private byte[] _rsaSignId;
        /// <summary>
        /// Gets and set a binary value which is used to identify the value of <see cref="RSASignParameters"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-mode.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="RSASignParameters"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <c>null</c> and has a length equal to 0 or greater than 65536.
        /// </exception>
        public byte[] RSASignIdBytes
        {
            get { return _rsaSignId; }
            set
            {
                _ensureCanWrite();
                if (_rsaSignParamBC == null)
                    throw new InvalidOperationException(TextResources.RsaSigNone);
                if (value == null)
                    _rsaSignId = null;
                else if (value.Length == 0 || value.Length > Max16Bit)
                    throw new ArgumentException(TextResources.RsaIdLength, nameof(value));
                else
                    _rsaSignId = value;
            }
        }

        /// <summary>
        /// Gets and sets a string which is used to identify the value of <see cref="RSASignParameters"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-mode.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="RSASignParameters"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <c>null</c> and has a length equal to 0 or greater than 65536 UTF-8 bytes.
        /// </exception>
        public string RSASignId
        {
            get
            {
                if (_rsaSignId == null) return null;
                return _textEncoding.GetString(_rsaSignId);
            }
            set
            {
                if (value == null)
                    RSASignIdBytes = null;
                else
                    RSASignIdBytes = _textEncoding.GetBytes(value);
            }
        }

        internal static bool CompareValues(RsaKeyParameters x, RsaKeyParameters y)
        {
            return x != null && y != null && x.Exponent != null && x.Exponent.Equals(y.Exponent) &&
                x.Modulus != null && x.Modulus.Equals(y.Modulus);
        }
        #endregion

        #region DSA Signature
        private DerIntegerPair _dsaSignature;

        private bool _dsaSignVerified;
        /// <summary>
        /// Gets a value indicating whether the current stream has been successfully verified using <see cref="DSASignParameters"/>.
        /// </summary>
        public bool IsDSASignVerified { get { return _dsaSignVerified; } }

        /// <summary>
        /// Gets a value indicating whether the current instance is signed using a DSA private key.
        /// </summary>
        /// <remarks>
        /// If the current stream is in read-mode, this property will return <c>true</c> if and only if the underlying stream
        /// was signed when it was written.
        /// If the current stream is in write-mode, this property will return <c>true</c> if <see cref="RSASignParameters"/>
        /// is not <c>null</c>.
        /// </remarks>
        public bool IsDSASigned
        {
            get
            {
                if (_mode == CompressionMode.Decompress)
                    return _dsaSignature != null;
                return _dsaSignParamBC != null;
            }
        }

        private DsaKeyParameters _dsaSignParamBC;
        /// <summary>
        /// Gets and sets an RSA key used to sign the current stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current stream is in read-mode, and is not signed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, the current stream is in read-mode, and has already been verified.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>In a set operation, the current stream is in write-mode, and the specified value does not represent a valid private key.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current stream is in read-mode, and the specified value does not represent a valid public key.</para>
        /// </exception>
        public DsaKeyParameters DSASignParameters
        {
            get { return _dsaSignParamBC; }
            set
            {
                if (_baseStream == null)
                    throw new ObjectDisposedException(null, TextResources.CurrentClosed);
                CheckSignParam(value, _dsaSignature, _dsaSignVerified);
                _dsaSignParamBC = value;
            }
        }

        private byte[] _dsaSignId;
        /// <summary>
        /// Gets and set a binary value which is used to identify the value of <see cref="DSASignParameters"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-mode.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="DSASignParameters"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <c>null</c> and has a length equal to 0 or greater than 65536.
        /// </exception>
        public byte[] DSASignIdBytes
        {
            get { return _dsaSignId; }
            set
            {
                _ensureCanWrite();
                if (_dsaSignParamBC == null)
                    throw new InvalidOperationException(TextResources.RsaSigNone);
                if (value == null)
                    _dsaSignId = null;
                else if (value.Length == 0 || value.Length > Max16Bit)
                    throw new ArgumentException(TextResources.RsaIdLength, nameof(value));
                else
                    _dsaSignId = value;
            }
        }

        /// <summary>
        /// Gets and sets a string which is used to identify the value of <see cref="DSASignParameters"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-mode.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="DSASignParameters"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <c>null</c> and has a length equal to 0 or greater than 65536 UTF-8 bytes.
        /// </exception>
        public string DSASignId
        {
            get
            {
                if (_dsaSignId == null) return null;
                return _textEncoding.GetString(_dsaSignId);
            }
            set
            {
                if (value == null)
                    DSASignIdBytes = null;
                else
                    DSASignIdBytes = _textEncoding.GetBytes(value);
            }
        }
        #endregion

        #region ECDSA Signature
        private DerIntegerPair _ecdsaSignature;

        private bool _ecdsaSignVerified;
        /// <summary>
        /// Gets a value indicating whether the current stream has been successfully verified using <see cref="ECDSASignParameters"/>.
        /// </summary>
        public bool IsECDSASignVerified { get { return _ecdsaSignVerified; } }

        /// <summary>
        /// Gets a value indicating whether the current instance is signed using an ECDSA private key.
        /// </summary>
        /// <remarks>
        /// If the current stream is in read-mode, this property will return <c>true</c> if and only if the underlying stream
        /// was signed when it was written.
        /// If the current stream is in write-mode, this property will return <c>true</c> if <see cref="RSASignParameters"/>
        /// is not <c>null</c>.
        /// </remarks>
        public bool IsECDSASigned
        {
            get
            {
                if (_mode == CompressionMode.Decompress)
                    return _ecdsaSignature != null;
                return _ecdsaSignParamBC != null;
            }
        }

        private ECKeyParameters _ecdsaSignParamBC;

        /// <summary>
        /// Gets and sets an RSA key used to sign the current stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current stream is in read-mode, and is not signed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, the current stream is in read-mode, and has already been verified.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>In a set operation, the current stream is in write-mode, and the specified value does not represent a valid private key.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current stream is in read-mode, and the specified value does not represent a valid public key.</para>
        /// </exception>
        public ECKeyParameters ECDSASignParameters
        {
            get { return _ecdsaSignParamBC; }
            set
            {
                if (_baseStream == null)
                    throw new ObjectDisposedException(null, TextResources.CurrentClosed);
                CheckSignParam(value, _ecdsaSignature, _ecdsaSignVerified);
                _ecdsaSignParamBC = value;
            }
        }

        private byte[] _ecdsaSignId;
        /// <summary>
        /// Gets and set a binary value which is used to identify the value of <see cref="ECDSASignParameters"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-mode.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="ECDSASignParameters"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <c>null</c> and has a length equal to 0 or greater than 65536.
        /// </exception>
        public byte[] ECDSASignIdBytes
        {
            get { return _ecdsaSignId; }
            set
            {
                _ensureCanWrite();
                if (_ecdsaSignParamBC == null)
                    throw new InvalidOperationException(TextResources.RsaSigNone);
                if (value == null)
                    _ecdsaSignId = null;
                else if (value.Length == 0 || value.Length > Max16Bit)
                    throw new ArgumentException(TextResources.RsaIdLength, nameof(value));
                else
                    _ecdsaSignId = value;
            }
        }

        /// <summary>
        /// Gets and sets a string which is used to identify the value of <see cref="ECDSASignParameters"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-mode.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="ECDSASignParameters"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <c>null</c> and has a length equal to 0 or greater than 65536 UTF-8 bytes.
        /// </exception>
        public string ECDSASignId
        {
            get
            {
                if (_ecdsaSignId == null) return null;
                return _textEncoding.GetString(_ecdsaSignId);
            }
            set
            {
                if (value == null)
                    ECDSASignIdBytes = null;
                else
                    ECDSASignIdBytes = _textEncoding.GetBytes(value);
            }
        }
        #endregion

        /// <summary>
        /// Gets and sets a comment on the file.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-mode.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <c>null</c>, and has a length which is equal to 0 or which is greater than 65536 UTF-8 bytes.
        /// </exception>
        public string Comment
        {
            get
            {
                if (_comBytes == null) return null;
                return _textEncoding.GetString(_comBytes);
            }
            set
            {
                _ensureCanWrite();
                _comBytes = CheckComment(value);
            }
        }

        private byte[] _comBytes;
        /// <summary>
        /// Gets and sets a binary representation of a comment on the file.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-mode.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <c>null</c>, and has a length which is equal to 0 or which is greater than 65536.
        /// </exception>
        public byte[] CommentBytes
        {
            get { return _comBytes; }
            set
            {
                _ensureCanWrite();
                CheckComment(value);
                _comBytes = value;
            }
        }

        internal static byte[] CheckComment(string value)
        {
            if (value == null) return null;
            byte[] bytes = _textEncoding.GetBytes(value);
            CheckComment(bytes);
            return bytes;
        }

        internal static void CheckComment(byte[] value)
        {
            if (value != null && value.Length == 0 || value.Length > Max16Bit)
                throw new ArgumentException(TextResources.CommentLength, nameof(value));
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

        internal enum AllowDirNames
        {
            No,
            Yes,
            EmptyDir,
            Unknown
        }

        private AllowDirNames _allowDirNames;

        internal static bool IsValidFilename(string value, bool throwOnInvalid, AllowDirNames dirFormat, string paramName)
        {
            if (value == null) throw new ArgumentNullException(paramName);

            if (value.Length == 0)
            {
                if (throwOnInvalid)
                    throw new ArgumentException(dirFormat == AllowDirNames.No ? TextResources.FilenameLengthZero : TextResources.FilenameLengthZeroPath, paramName);
                return false;
            }
            if (dirFormat == AllowDirNames.EmptyDir || dirFormat == AllowDirNames.Unknown)
            {
                int end = value.Length - 1;
                if (value[end] == '/')
                {
                    value = value.Substring(0, end);
                    dirFormat = AllowDirNames.EmptyDir;
                }
            }

            if (dirFormat == AllowDirNames.EmptyDir)
            {
                const int maxDirLen = 254;

                if (_textEncoding.GetByteCount(value) > maxDirLen)
                {
                    if (throwOnInvalid)
                        throw new ArgumentException(TextResources.FilenameEDirLengthLong);
                    return false;
                }
            }
            else if (_textEncoding.GetByteCount(value) > Max8Bit)
            {
                if (throwOnInvalid)
                    throw new ArgumentException(TextResources.FilenameLengthLong, paramName);
                return false;
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
                        throw new ArgumentException(string.Format(dirFormat == AllowDirNames.No ? TextResources.FilenameBadSurrogate :
                            TextResources.FilenameBadSurrogatePath, string.Format("\\u{0:x4} {1}", (int)c, c)), paramName);
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

                if (char.IsWhiteSpace(c))
                {
                    dotCount = -1;
                    continue;
                }

                if (c == '/')
                {
                    if (dirFormat == AllowDirNames.No)
                    {
                        if (throwOnInvalid)
                            throw new ArgumentException(TextResources.FilenameForwardSlash, paramName);
                        return false;
                    }

                    string itemName = value.Substring(0, i);

                    if (dotCount == 0)
                    {
                        if (throwOnInvalid)
                            throw new ArgumentException(i == 0 ? TextResources.FilenamePathLeadingSlash : TextResources.FilenamePathDoubleSlash, paramName);
                        return false;
                    }

                    if (dotCount > 0)
                    {
                        if (throwOnInvalid)
                            throw new ArgumentException(string.Format(TextResources.FilenameDotPath, itemName), paramName);
                        return false;
                    }

                    if (!seenNotWhite)
                    {
                        if (throwOnInvalid)
                            throw new ArgumentException(TextResources.FilenameWhitespacePath, paramName);
                        return false;
                    }

                    seenNotWhite = false;
                    dotCount = 0;
                    value = value.Substring(i + 1);
                    i = -1;
                    continue;
                }
                dotCount = -1;
                seenNotWhite = true;

                if (c < ' ' || (c > '~' && c <= '\u009f'))
                {
                    if (throwOnInvalid)
                        throw new ArgumentException(dirFormat == AllowDirNames.No ? TextResources.FilenameControl : TextResources.FilenameControlPath, paramName);
                    return false;
                }
            }

            if (throwOnInvalid)
            {
                if (!seenNotWhite)
                    throw new ArgumentException(dirFormat == AllowDirNames.No ? TextResources.FilenameWhitespace : TextResources.FilenameWhitespacePath, paramName);
                if (dotCount == 0)
                    throw new ArgumentException(TextResources.FilenamePathDoubleSlash, paramName);
                if (dotCount > 0)
                    throw new ArgumentException(string.Format(dirFormat == AllowDirNames.No ? TextResources.FilenameDot : TextResources.FilenameDotPath, value), paramName);
                return true;
            }

            return seenNotWhite && dotCount < 0;
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
            return IsValidFilename(value, false, AllowDirNames.No, nameof(value));
        }

        private MausHashFunction _hashFunc;
        /// <summary>
        /// Gets and sets the hash function used by the current instance. The default is <see cref="MausHashFunction.Sha256"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current stream is in read-mode.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// The specified value is not a valid <see cref="MausHashFunction"/> value.
        /// </exception>
        public MausHashFunction HashFunction
        {
            get { return _hashFunc; }
            set
            {
                _ensureCanWrite();
                if (HashBDict.ContainsKey(value))
                    _hashFunc = value;
                else
                    throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(MausHashFunction));
            }
        }

        private string _password;
        /// <summary>
        /// Gets and sets the password used by the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current stream is not encrypted.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, the current instance is in read-mode and has already been successfully decrypted.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// In a set operation, the specified value is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value has a length of 0.
        /// </exception>
        public string Password
        {
            get { return _password; }
            set
            {
                _ensureCanSetKey();
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                if (value.Length == 0)
                    throw new ArgumentException(TextResources.PasswordZeroLength, nameof(value));
                _password = value;
            }
        }

        internal static byte[] GetKey(string password, byte[] _salt, int _pkCount, int keySize, MausHashFunction hashFunc)
        {
            int keyLength = keySize >> 3;
            if (_salt.Length > keyLength)
                Array.Resize(ref _salt, keyLength);

            Pkcs5S2ParametersGenerator gen = new Pkcs5S2ParametersGenerator(GetDigestObject(hashFunc));
            gen.Init(Encoding.UTF8.GetBytes(password), _salt, _pkCount + minPkCount);
            KeyParameter kParam = (KeyParameter)gen.GenerateDerivedParameters("AES" + keySize.ToString(System.Globalization.NumberFormatInfo.InvariantInfo), keySize);
            return kParam.GetKey();
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
        /// <returns>This method does not return.</returns>
        /// <exception cref="NotSupportedException">
        /// Always.
        /// </exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }
        #endregion

        internal const int _blockByteCtAes = 16;
        internal const int _keyBitAes256 = 256;
        internal const int _keyBitAes128 = 128;
        internal const int _keyBitAes192 = 192;

        internal const string _keyStrAes256 = "256", _keyStrAes128 = "128", _keyStrAes192 = "192";

        private const string _kCmpNone = "NK", _kCmpDef = "DEF", _kCmpLzma = "LZMA";
        internal const string _kEncAes = "AES";

        private const string _kRsaSig = "Rsa-Sig", _kRsaSigId = "Rsa-Sig-Id",
            _kDsaSig = "Dsa-Sig", _kDsaSigId = "Dsa-Sig-Id",
            _kECDsaSig = "ECDsa-Sig", _kECDsaSigId = "ECDsa-Sig-Id";

        internal const string _kHash = "Hash";

        private const string _kTimeC = "Ers", _kTimeM = "Mod";

        private bool _headerGotten;

        internal const int Max8Bit = 256;

        private static readonly Dictionary<string, MausCompressionFormat> _formDict = new Dictionary<string, MausCompressionFormat>(StringComparer.Ordinal)
        {
            { _kCmpNone, MausCompressionFormat.None },
            { _kCmpLzma, MausCompressionFormat.Lzma },
            { _kCmpDef, MausCompressionFormat.Deflate }
        };

        internal static readonly Dictionary<string, MausHashFunction> HashDict = ((MausHashFunction[])Enum.GetValues(typeof(MausHashFunction))).
            ToDictionary(i => i.ToString().Replace('_', '/').ToUpper());
        internal static readonly Dictionary<MausHashFunction, string> HashBDict = HashDict.ToDictionary(i => i.Value, i => i.Key);

        private const string _kFilename = "Name", _kULen = "DeL";
        internal const string _kComment = "Kom";

        private long _headSize;
        internal long HeadLength { get { return _headSize; } }

        private ushort version = _versionShort;

        private void _getHeader(bool readMagNum)
        {
#if NOLEAVEOPEN
            BinaryReader reader = new BinaryReader(_baseStream);
#else
            using (BinaryReader reader = new BinaryReader(_baseStream, _textEncoding, true))
#endif
            {
                if (readMagNum && reader.ReadInt32() != _head)
                    throw new InvalidDataException(TextResources.InvalidDataMaus);
                version = reader.ReadUInt16();
                if (version > _versionShort)
                    throw new NotSupportedException(TextResources.VersionTooHigh);
                if (version < _minVersionShort)
                    throw new NotSupportedException(TextResources.VersionTooLow);

                _hashFunc = MausHashFunction.Sha512;
                _headSize = ReadFormat(reader, false);

                if ((_rsaSignId != null && _rsaSignature == null) || (_dsaSignId != null && _dsaSignature == null) || (_ecdsaSignId != null && _ecdsaSignature == null))
                    throw new InvalidDataException(TextResources.FormatBad);

                int hashLength = GetHashLength(_hashFunc);
                if (_rsaSignature != null && _rsaSignature.Length < hashLength)
                    throw new InvalidDataException(TextResources.FormatBad);
                _headSize += hashLength;

                if (readMagNum)
                    _headSize += sizeof(int);

                _compLength = reader.ReadInt64();
                if (_compLength <= 0)
                    throw new InvalidDataException(TextResources.InvalidDataMaus);
                _uncompressedLength = reader.ReadInt64();

                if (_encFmt != MausEncryptionFormat.None)
                {
                    if (_uncompressedLength < 0 || _uncompressedLength > (int.MaxValue - minPkCount))
                        throw new InvalidDataException(TextResources.InvalidDataMaus);
                    _pkCount = (int)_uncompressedLength;
                    _uncompressedLength = 0;
                }
                else if (_uncompressedLength <= 0)
                    throw new InvalidDataException(TextResources.InvalidDataMaus);
                else gotULen = true;

                byte[] hash = ReadBytes(reader, hashLength);

                if (_encFmt == MausEncryptionFormat.None)
                    _hashExpected = hash;
                else
                {
                    _hmacExpected = hash;

                    int keySize = _keySizes.MaxSize >> 3;
                    _salt = ReadBytes(reader, keySize);

                    _iv = ReadBytes(reader, _blockByteCount);

                    long getLength = keySize + _iv.Length;

                    _compLength -= getLength;

                    _headSize += getLength;
                }
            }
        }

        #region ReadFormat
        bool gotFormat, gotULen, _gotHash;
        private long ReadFormat(BinaryReader reader, bool fromEncrypted)
        {
            const long baseHeadSize = sizeof(short) + sizeof(ushort) + //Version, option count,
                sizeof(long) + sizeof(long); //compressed length, uncompressed length

            long headSize = baseHeadSize;

            int optLen = reader.ReadUInt16();

            for (int i = 0; i < optLen; i++)
            {
                string curForm = GetString(reader, ref headSize, false);

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
                        _encryptedOptions.InternalAdd(MausOptionToEncrypt.Compression);

                    gotFormat = true;
                    _cmpFmt = cmpFmt;
                    continue;
                }

                if (curForm.Equals(_kHash, StringComparison.Ordinal))
                {
                    CheckAdvance(optLen, ref i);
                    string hashName = GetString(reader, ref headSize, false);
                    MausHashFunction hashFunc;
                    if (!HashDict.TryGetValue(hashName, out hashFunc))
                        throw new NotSupportedException(TextResources.FormatUnknownZ);

                    if (_gotHash || fromEncrypted)
                    {
                        if (hashFunc != _hashFunc)
                            throw new InvalidDataException(TextResources.FormatBadZ);
                    }
                    else
                    {
                        _hashFunc = hashFunc;
                        _gotHash = true;
                    }

                    continue;
                }

                if (curForm.Equals(_kRsaSig, StringComparison.Ordinal))
                {
                    ReadBytes(reader, optLen, ref headSize, ref i, ref _rsaSignature);
                    continue;
                }

                if (curForm.Equals(_kRsaSigId, StringComparison.Ordinal))
                {
                    ReadBytes(reader, optLen, ref headSize, ref i, ref _rsaSignId);
                    continue;
                }

                if (curForm.Equals(_kDsaSig, StringComparison.Ordinal))
                {
                    GetDsaValue(reader, optLen, ref i, ref headSize, fromEncrypted, ref _dsaSignature);
                    continue;
                }

                if (curForm.Equals(_kDsaSigId, StringComparison.Ordinal))
                {
                    ReadBytes(reader, optLen, ref headSize, ref i, ref _dsaSignId);
                    continue;
                }

                if (curForm.Equals(_kECDsaSig, StringComparison.Ordinal))
                {
                    GetDsaValue(reader, optLen, ref i, ref headSize, fromEncrypted, ref _ecdsaSignature);
                    continue;
                }

                if (curForm.Equals(_kECDsaSigId, StringComparison.Ordinal))
                {
                    ReadBytes(reader, optLen, ref headSize, ref i, ref _ecdsaSignId);
                    continue;
                }

                if (curForm.Equals(_kEncAes, StringComparison.Ordinal))
                {
                    if (!fromEncrypted)
                        _encryptedOptions = new SettableOptions(this);
                    CheckAdvance(optLen, ref i);

                    byte[] bytes = GetStringBytes(reader, ref headSize, false);
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

                    if (_keySize == 0)
                    {
                        _keySize = keyBits;
                        _keySizes = new KeySizes(keyBits, keyBits, 0);
                    }
                    else if (keyBits != _keySize)
                        throw new InvalidDataException(TextResources.FormatBad);
                    _encFmt = MausEncryptionFormat.Aes;
                    continue;
                }

                if (curForm.Equals(_kFilename, StringComparison.Ordinal))
                {
                    CheckAdvance(optLen, ref i);

                    string filename = GetString(reader, ref headSize, false);

                    if (_filename == null)
                    {
                        if (fromEncrypted)
                            _encryptedOptions.InternalAdd(MausOptionToEncrypt.Filename);

                        if (!IsValidFilename(filename, false, _allowDirNames, null))
                            throw new InvalidDataException(TextResources.FormatBad);

                        _filename = filename;
                    }
                    else if (!filename.Equals(_filename, StringComparison.Ordinal))
                        throw new InvalidDataException(TextResources.FormatBad);
                    continue;
                }

                if (curForm.Equals(_kULen, StringComparison.Ordinal))
                {
                    CheckAdvance(optLen, ref i);

                    long uLen = GetValueInt64(reader, ref headSize);

                    if (uLen <= 0 || (gotULen && uLen != _uncompressedLength))
                        throw new InvalidDataException(TextResources.FormatBad);

                    _uncompressedLength = uLen;
                    gotULen = true;
                    continue;
                }

                if (curForm.Equals(_kTimeC, StringComparison.Ordinal))
                {
                    GetDate(reader, ref _timeC, ref headSize, optLen, ref i, fromEncrypted, MausOptionToEncrypt.CreatedTime);
                    continue;
                }

                if (curForm.Equals(_kTimeM, StringComparison.Ordinal))
                {
                    GetDate(reader, ref _timeM, ref headSize, optLen, ref i, fromEncrypted, MausOptionToEncrypt.ModTime);
                    continue;
                }

                if (curForm.Equals(_kComment, StringComparison.Ordinal))
                {
                    CheckAdvance(optLen, ref i);
                    byte[] comBytes = GetStringBytes(reader, ref headSize, false);

                    if (_comBytes == null)
                    {
                        if (fromEncrypted)
                            _encryptedOptions.InternalAdd(MausOptionToEncrypt.Comment);

                        _comBytes = comBytes;
                    }
                    else if (!CompareBytes(comBytes, _comBytes))
                        throw new InvalidDataException(TextResources.FormatBad);

                    continue;
                }

                throw new NotSupportedException(TextResources.FormatUnknown);
            }

            return headSize;
        }

        private class DerIntegerPair
        {
            public DerIntegerPair(DerInteger dR, DerInteger dS)
            {
                R = dR;
                S = dS;
            }

            public readonly DerInteger R;
            public readonly DerInteger S;
        }

        private static void GetDsaValue(BinaryReader reader, int optLen, ref int i, ref long curSize, bool fromEncrypted, ref DerIntegerPair existing)
        {
            CheckAdvance(optLen, ref i);

            byte[] message = GetStringBytes(reader, ref curSize, false);

            Asn1Sequence seq;
            try
            {
                seq = (Asn1Sequence)Asn1Object.FromByteArray(message);
            }
            catch
            {
                throw new InvalidDataException(TextResources.FormatBad);
            }

            if (seq.Count != 2)
                throw new InvalidDataException(TextResources.FormatBad);

            DerIntegerPair newVal;
            try
            {
                newVal = new DerIntegerPair((DerInteger)seq[0], (DerInteger)seq[1]);
            }
            catch
            {
                throw new InvalidDataException(TextResources.FormatBad);
            }

            if (existing == null)
                existing = newVal;
            else if (!existing.R.Equals(newVal.R) || !existing.S.Equals(newVal.S))
                throw new InvalidDataException(TextResources.FormatBad);
        }

        internal static void ReadBytes(BinaryReader reader, int optLen, ref long headSize, ref int i, ref byte[] oldValue)
        {
            CheckAdvance(optLen, ref i);
            byte[] bytes = GetStringBytes(reader, ref headSize, false);

            if (oldValue == null)
                oldValue = bytes;
            else if (!CompareBytes(bytes, oldValue))
                throw new InvalidDataException(TextResources.FormatBad);
        }

        internal static byte[] ReadBytes(BinaryReader reader, int size)
        {
            byte[] data = reader.ReadBytes(size);
            if (data.Length < size)
                throw new EndOfStreamException();
            return data;
        }

        private static readonly long maxTicks = DateTime.MaxValue.Ticks;

        private void GetDate(BinaryReader reader, ref DateTime? curTime, ref long curOffset, int optLen, ref int i, bool fromEncrypted, MausOptionToEncrypt option)
        {
            CheckAdvance(optLen, ref i);

            long value = GetValueInt64(reader, ref curOffset);

            if (value < 0 || value > maxTicks)
                throw new InvalidDataException(TextResources.FormatBad);

            DateTime newVal = new DateTime(value, DateTimeKind.Utc);

            if (curTime.HasValue)
            {
                if (curTime.Value != newVal)
                    throw new InvalidDataException(TextResources.FormatBad);
            }
            else
            {
                if (fromEncrypted)
                    _encryptedOptions.Add(option);
                curTime = newVal;
            }
        }

        private static long GetValueInt64(BinaryReader reader, ref long curOffset)
        {
            if (reader.ReadUInt16() != sizeof(long))
                throw new InvalidDataException(TextResources.FormatBad);

            curOffset += (sizeof(long) + sizeof(ushort));

            return reader.ReadInt64();
        }

        private static void CheckAdvance(int optLen, ref int i)
        {
            if (++i >= optLen)
                throw new InvalidDataException(TextResources.FormatBad);
        }

        internal static string GetString(BinaryReader reader, ref long curSize, bool is8bit)
        {
            byte[] strBytes = GetStringBytes(reader, ref curSize, is8bit);

            return _textEncoding.GetString(strBytes);
        }

        internal static byte[] GetStringBytes(BinaryReader reader, ref long curSize, bool is8bit)
        {
            int strLen;
            if (is8bit)
            {
                strLen = reader.ReadByte();
                if (strLen == 0) strLen = Max8Bit;
                curSize += 1;
            }
            else
            {
                strLen = reader.ReadUInt16();
                if (strLen == 0) strLen = Max16Bit;
                curSize += 2;
            }
            byte[] strBytes = ReadBytes(reader, strLen);
            curSize += strBytes.Length;
            return strBytes;
        }
        #endregion

        internal static int GetHashLength(MausHashFunction hashFunc)
        {
            switch (hashFunc)
            {
                case MausHashFunction.Sha224:
                case MausHashFunction.Sha3_224:
                    return 224 / 8;
                case MausHashFunction.Sha384:
                case MausHashFunction.Sha3_384:
                    return 384 / 8;
                case MausHashFunction.Sha512:
                case MausHashFunction.Sha3_512:
                    return 512 / 8;
                case MausHashFunction.Sha256:
                case MausHashFunction.Sha3_256:
                    return 256 / 8;
            }
            throw new InvalidEnumArgumentException(nameof(hashFunc), (int)hashFunc, typeof(MausHashFunction));
        }

        internal static int GetHashBitSize(MausHashFunction hashFunc)
        {
            switch (hashFunc)
            {
                case MausHashFunction.Sha224:
                case MausHashFunction.Sha3_224:
                    return 224;
                case MausHashFunction.Sha256:
                case MausHashFunction.Sha3_256:
                    return 256;
                case MausHashFunction.Sha384:
                case MausHashFunction.Sha3_384:
                    return 256;
                case MausHashFunction.Sha512:
                case MausHashFunction.Sha3_512:
                    return 512;
            }
            throw new InvalidEnumArgumentException(nameof(hashFunc), (int)hashFunc, typeof(MausHashFunction));
        }

        private static DerObjectIdentifier GetHashId(MausHashFunction hashFunc)
        {
            switch (hashFunc)
            {
                case MausHashFunction.Sha224:
                    return NistObjectIdentifiers.IdSha224;
                case MausHashFunction.Sha256:
                    return NistObjectIdentifiers.IdSha256;
                case MausHashFunction.Sha384:
                    return NistObjectIdentifiers.IdSha384;
                case MausHashFunction.Sha512:
                    return NistObjectIdentifiers.IdSha512;
                case MausHashFunction.Sha3_224:
                    return NistObjectIdentifiers.IdSha3_224;
                case MausHashFunction.Sha3_256:
                    return NistObjectIdentifiers.IdSha3_256;
                case MausHashFunction.Sha3_384:
                    return NistObjectIdentifiers.IdSha3_384;
                case MausHashFunction.Sha3_512:
                    return NistObjectIdentifiers.IdSha3_512;
            }
            throw new InvalidEnumArgumentException(nameof(hashFunc), (int)hashFunc, typeof(MausHashFunction));
        }

        private static IDigest GetDigestObject(MausHashFunction hashFunc)
        {
            switch (hashFunc)
            {
                case MausHashFunction.Sha224:
                    return new Sha224Digest();
                case MausHashFunction.Sha256:
                    return new Sha256Digest();
                case MausHashFunction.Sha384:
                    return new Sha384Digest();
                case MausHashFunction.Sha512:
                    return new Sha512Digest();
                default:
                    return new Sha3Digest(GetHashBitSize(hashFunc));
            }
        }

        private long _compLength;
        internal long CompressedLength { get { return _compLength; } }

        internal const int minPkCount = 9001;
        private int _pkCount;
        private LzmaDictionarySize _lzmaDictSize;

        /// <summary>
        /// Attempts to pre-load the data in the current instance. If the current stream is encrypted,
        /// attempts to decrypt the current instance using either <see cref="Password"/> or <see cref="RSASignParameters"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current stream is in write-mode.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// Either <see cref="Password"/> is not correct, or <see cref="RSASignParameters"/> is not <c>null</c> and is not set to the correct value.
        /// It is safe to attempt to call <see cref="LoadData()"/>, <see cref="Read(byte[], int, int)"/>, <see cref="ReadByte()"/>, or 
        /// <see cref="ComputeHash()"/> again if this exception is caught.
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

        void IMausCrypt.Decrypt() { LoadData(); }

        /// <summary>
        /// Computes the hash of the unencrypted data.
        /// </summary>
        /// <returns>The hash of the unencrypted data.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// The current stream is in read-mode, and
        /// either <see cref="Password"/> is not correct, or <see cref="RSASignParameters"/> is not <c>null</c> and is not set to the correct value.
        /// It is safe to attempt to call <see cref="LoadData()"/>, <see cref="Read(byte[], int, int)"/>, <see cref="ReadByte()"/>, or 
        /// <see cref="ComputeHash()"/> again if this exception is caught.
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// The current stream is in read-mode, and contains invalid data.
        /// </exception>
        /// <exception cref="IOException">
        /// An I/O error occurred.
        /// </exception>
        /// <remarks>When the current stream is in read-mode, returns the same value as <see cref="Hash"/>.
        /// In write-mode, this method computes the hash from the current written data.</remarks>
        public byte[] ComputeHash()
        {
            if (_baseStream == null)
                throw new ObjectDisposedException(null, TextResources.CurrentClosed);

            if (_mode == CompressionMode.Compress)
            {
                using (MausBufferStream tempStream = new MausBufferStream())
                {
                    tempStream.Prepend(_bufferStream);
                    return ComputeHash(tempStream, _hashFunc);
                }
            }

            return _hashExpected;
        }

        private void _readData()
        {
            if (_headerGotten)
            {
                _verifyRSASignature();
                return;
            }

            if (_password == null && _encFmt != MausEncryptionFormat.None)
                throw new CryptographicException(TextResources.KeyNotSet);

            GetBuffer();

            if (_encFmt != MausEncryptionFormat.None)
            {
                byte[] _key;
                OnProgress(MausProgressState.BuildingKey);
                _key = GetKey(_password, _salt, _pkCount, _keySize, _hashFunc);

                using (MausBufferStream bufferStream = Decrypt(this, _key, _iv, _bufferStream, _hmacExpected, _hashFunc))
                {
                    Array.Clear(_key, 0, _key.Length);
#if NOLEAVEOPEN
                    BinaryReader reader = new BinaryReader(bufferStream);
#else
                    using (BinaryReader reader = new BinaryReader(bufferStream, _textEncoding, true))
#endif
                    {
                        ReadFormat(reader, true);
                    }

                    _bufferStream.Close();
                    _bufferStream = new MausBufferStream();
                    bufferStream.BufferCopyTo(_bufferStream, true);

                    _bufferStream.Reset();
                }
            }
            long oldLength = _bufferStream.Length;
            switch (_cmpFmt)
            {
                case MausCompressionFormat.Lzma:
                    MausBufferStream lzmaStream = new MausBufferStream();
                    const int optLen = 5;

                    byte[] opts = new byte[optLen];
                    if (_bufferStream.Read(opts, 0, optLen) < optLen) throw new EndOfStreamException();
                    LzmaDecoder decoder = new LzmaDecoder();
                    decoder.SetDecoderProperties(opts);
                    if (decoder.DictionarySize < 1 || decoder.DictionarySize > (uint)LzmaDictionarySize.MaxValue)
                        throw new InvalidDataException();
                    try
                    {
                        decoder.Code(_bufferStream, lzmaStream, _bufferStream.Length - optLen, -1, this);
                    }
                    catch (DataErrorException)
                    {
                        throw new InvalidDataException();
                    }
                    if (lzmaStream.Length < _uncompressedLength || lzmaStream.Length == 0)
                        throw new EndOfStreamException();

                    _bufferStream.Dispose();
                    _bufferStream = lzmaStream;
                    _bufferStream.Reset();
                    break;
                case MausCompressionFormat.None:
                    break;
                default:
                    OnProgress(MausProgressState.Decompressing);
                    long oldLen = _bufferStream.Length;
                    using (DeflateStream _deflateStream = new DeflateStream(_bufferStream, CompressionMode.Decompress, true))
                    {
                        _bufferStream = new MausBufferStream();
#if NOCOPY
                        byte[] readBuffer = new byte[Max16Bit];

                        if (gotULen)
                        {
                            long len = _uncompressedLength;

                            while (len > 0)
                            {
                                int read = _deflateStream.Read(readBuffer, 0, (int)Math.Min(len, Max16Bit));
                                if (read == 0)
                                    throw new EndOfStreamException();
                                _bufferStream.Write(readBuffer, 0, read);
                                len -= read;
                            }
                        }
                        else
                        {
                            int read;
                            while ((read = _deflateStream.Read(readBuffer, 0, Max16Bit)) != 0)
                                _bufferStream.Write(readBuffer, 0, read);
                        }
#else
                        _deflateStream.CopyTo(_bufferStream, Max16Bit);

                        if (_bufferStream.Length < _uncompressedLength || _bufferStream.Length == 0)
                            throw new EndOfStreamException();
#endif
                    }
                    _bufferStream.Reset();
                    break;
            }
            _headerGotten = true;
            OnProgress(new MausProgressEventArgs(MausProgressState.CompletedLoading, oldLength, _bufferStream.Length));

            OnProgress(_encFmt == MausEncryptionFormat.None ? MausProgressState.VerifyingHash : MausProgressState.ComputingHash);
            byte[] hashActual = ComputeHash(_bufferStream, _hashFunc);
            _bufferStream.Reset();
            if (_encFmt == MausEncryptionFormat.None || _rsaSignature != null)
            {
                if (_encFmt == MausEncryptionFormat.None)
                {
                    if (!CompareBytes(hashActual, _hashExpected))
                        throw new InvalidDataException(TextResources.BadChecksum);
                    OnProgress(new MausProgressEventArgs(MausProgressState.VerifyingHashCompleted, hashActual));
                }
                _hashExpected = hashActual;
                if (_rsaSignature != null)
                    _verifyRSASignature();
            }
            else _hashExpected = hashActual;
        }

        #region Verify RSA
        /// <summary>
        /// Tests whether <see cref="RSASignParameters"/> is valid.
        /// </summary>
        /// <returns><c>true</c> if <see cref="RSASignParameters"/> is set to the correct public key; <c>false</c> if the current instance is not 
        /// signed, or if <see cref="RSASignParameters"/> is not set to the correct value.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current stream is in write-mode.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// <see cref="RSASignParameters"/> is set to an entirely invalid value.
        /// </exception>
        public bool VerifyRSASignature()
        {
            _ensureCanRead();
            lock (_lock)
            {
                _readData();
            }
            return _verifyRSASignature();
        }

        private bool _verifyRSASignature()
        {
            if (_rsaSignVerified)
                return true;

            if (_rsaSignature == null || _rsaSignParamBC == null)
                return false;

            OnProgress(MausProgressState.VerifyingRSASignature);
            try
            {
                RsaBlindedEngine _engine = new RsaBlindedEngine();
                _engine.Init(false, _rsaSignParamBC);

                byte[] sig;
                try
                {
                    sig = Pkcs7Provider.RemovePadding(_engine.ProcessBlock(_rsaSignature, 0, _rsaSignature.Length), _engine.GetOutputBlockSize());
                }
                catch (Exception)
                {
                    return false;
                }
                if (CompareBytes(_hashExpected, sig))
                    return _rsaSignVerified = true;

                byte[] expected = GetDerEncoded(_hashExpected, GetHashId(_hashFunc));

                if (CompareBytes(expected, sig))
                    return _rsaSignVerified = true;

                if (sig.Length != expected.Length - 2)
                    return false;

                int sigOffset = sig.Length - _hashExpected.Length - 2;
                int expectedOffset = expected.Length - _hashExpected.Length - 2;

                expected[1] -= 2;      // adjust lengths
                expected[3] -= 2;

                for (int i = 0; i < _hashExpected.Length; i++)
                {
                    if (sig[sigOffset + i] != expected[expectedOffset + i])
                        return false;
                }

                for (int i = 0; i < sigOffset; i++)
                {
                    if (sig[i] != expected[i])  // check header less NULL
                        return false;
                }

                return _rsaSignVerified = true;
            }
            catch (Exception x)
            {
                throw new CryptographicException(TextResources.RsaSigInvalid, x);
            }
        }
        #endregion

        #region Verify DSA
        /// <summary>
        /// Tests whether <see cref="DSASignParameters"/> is valid.
        /// </summary>
        /// <returns><c>true</c> if <see cref="DSASignParameters"/> is set to the correct public key; <c>false</c> if the current instance is not 
        /// signed, or if <see cref="DSASignParameters"/> is not set to the correct value.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current stream is in write-mode.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// <see cref="DSASignParameters"/> is set to an entirely invalid value.
        /// </exception>
        public bool VerifyDSASignature()
        {
            _ensureCanRead();
            lock (_lock)
            {
                _readData();
            }
            return _verifyDSASignature();
        }

        private bool _verifyDSASignature()
        {
            if (_dsaSignVerified)
                return true;

            if (_dsaSignature == null || _dsaSignParamBC == null)
                return false;
            OnProgress(MausProgressState.VerifyingDSASignature);
            try
            {
                DsaSigner signer = new DsaSigner(GetDsaCalc());

                return _dsaSignVerified = VerifyDsaSignature(_hashExpected, _dsaSignature, signer, _dsaSignParamBC);
            }
            catch (Exception x)
            {
                throw new CryptographicException(TextResources.DsaSigInvalid, x);
            }
        }
        #endregion

        #region Verify ECDSA
        /// <summary>
        /// Tests whether <see cref="ECDSASignParameters"/> is valid.
        /// </summary>
        /// <returns><c>true</c> if <see cref="ECDSASignParameters"/> is set to the correct public key; <c>false</c> if the current instance is not 
        /// signed, or if <see cref="ECDSASignParameters"/> is not set to the correct value.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current stream is in write-mode.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// <see cref="ECDSASignParameters"/> is set to an entirely invalid value.
        /// </exception>
        public bool VerifyECDSASignature()
        {
            _ensureCanRead();
            lock (_lock)
            {
                _readData();
            }
            return _verifyECDSASignature();
        }

        private bool _verifyECDSASignature()
        {
            if (_ecdsaSignVerified)
                return true;

            if (_ecdsaSignature == null || _ecdsaSignParamBC == null)
                return false;
            OnProgress(MausProgressState.VerifyingECDSASignature);
            try
            {
                ECDsaSigner signer = new ECDsaSigner(GetDsaCalc());

                return _ecdsaSignVerified = VerifyDsaSignature(_hashExpected, _ecdsaSignature, signer, _ecdsaSignParamBC);
            }
            catch (Exception x)
            {
                throw new CryptographicException(TextResources.EcdsaSigInvalid, x);
            }
        }
        #endregion

        private HMacDsaKCalculator GetDsaCalc()
        {
            return new HMacDsaKCalculator(GetDigestObject(_hashFunc));
        }

        private static bool VerifyDsaSignature(byte[] hash, DerIntegerPair pair, IDsa signer, ICipherParameters key)
        {
            signer.Init(false, key);

            try
            {
                return signer.VerifySignature(hash, pair.R.Value, pair.S.Value);
            }
            catch (IOException)
            {
                return false;
            }
        }

        internal void GetBuffer()
        {
            if (_bufferStream != null)
                return;
            OnProgress(MausProgressState.LoadingData);
            long length = _compLength;
            _bufferStream = GetBuffer(length, _baseStream);
            _bufferStream.Reset();
        }

        internal static MausBufferStream GetBuffer(long length, Stream _baseStream)
        {
            MausBufferStream _bufferStream = new MausBufferStream();
            byte[] buffer = new byte[Max16Bit];
            while (length > 0)
            {
                int read = _baseStream.Read(buffer, 0, (int)Math.Min(Max16Bit, length));
                if (read == 0) throw new EndOfStreamException();
                _bufferStream.Write(buffer, 0, read);
                length -= read;
            }

            return _bufferStream;
        }

        internal static bool CompareBytes(byte[] hashComputed, byte[] _hashExpected)
        {
            if (hashComputed == _hashExpected)
                return true;

            if (hashComputed.Length != _hashExpected.Length)
                return false;

            for (int i = 0; i < hashComputed.Length; i++)
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
        /// Either <see cref="Password"/> is not correct, or <see cref="RSASignParameters"/> is not <c>null</c> and is not set to the correct value.
        /// It is safe to attempt to call <see cref="LoadData()"/>, <see cref="Read(byte[], int, int)"/>, <see cref="ReadByte()"/>, or 
        /// <see cref="ComputeHash()"/> again if this exception is caught.
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

            return _bufferStream.Read(buffer, offset, count);
        }

        internal void BufferCopyTo(MausBufferStream other)
        {
            _readData();
            _bufferStream.BufferCopyTo(other, false);
        }

        private object _lock = new object();
        internal object SyncRoot { get { return _lock; } }

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
        /// Either <see cref="Password"/> is not correct, or <see cref="RSASignParameters"/> is not <c>null</c> and is not set to the correct value.
        /// It is safe to attempt to call <see cref="LoadData()"/>, <see cref="Read(byte[], int, int)"/>, <see cref="ReadByte()"/>, or 
        /// <see cref="ComputeHash()"/> again if this exception is caught.
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
            _bufferStream.Write(buffer, offset, count);
        }

        private void _ensureCanWrite()
        {
            if (_baseStream == null) throw new ObjectDisposedException(null, TextResources.CurrentClosed);
            if (_mode == CompressionMode.Decompress) throw new NotSupportedException(TextResources.CurrentRead);
        }

        internal static byte[] ComputeHash(MausBufferStream inputStream, MausHashFunction hashFunc)
        {
            IDigest shaHash = GetDigestObject(hashFunc);

            inputStream.BufferCopyTo(shaHash.BlockUpdate);

            byte[] output = new byte[shaHash.GetDigestSize()];
            shaHash.DoFinal(output, 0);
            return output;
        }

        internal static byte[] ComputeHmac(MausBufferStream inputStream, byte[] key, MausHashFunction hashFunc)
        {
            HMac hmac = new HMac(GetDigestObject(hashFunc));
            hmac.Init(new KeyParameter(key));
            inputStream.BufferCopyTo(hmac.BlockUpdate);

            byte[] output = new byte[hmac.GetMacSize()];
            hmac.DoFinal(output, 0);
            return output;
        }

        internal static byte[] FillBuffer(int length)
        {
            byte[] buffer = new byte[length];
#if NOCRYPTOCLOSE
            RandomNumberGenerator rng = RandomNumberGenerator.Create();
#else
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
#endif
            {
                rng.GetBytes(buffer);
            }
            return buffer;
        }

        internal static SymmetricAlgorithm GetAlgorithm(byte[] _key, byte[] _iv)
        {
            SymmetricAlgorithm alg = Aes.Create();
            alg.Key = _key;
            alg.IV = _iv;
            return alg;
        }

        internal static MausBufferStream Decrypt(IMausProgress o, byte[] _key, byte[] _iv, MausBufferStream _bufferStream, byte[] _hmac, MausHashFunction hashFunc)
        {
            o.OnProgress(MausProgressState.Decrypting);

            MausBufferStream output = new MausBufferStream();

            using (SymmetricAlgorithm alg = GetAlgorithm(_key, _iv))
            using (ICryptoTransform transform = alg.CreateDecryptor())
            {
                CryptoStream cs = new CryptoStream(output, transform, CryptoStreamMode.Write);
                _bufferStream.BufferCopyTo(cs, false);
                try
                {
                    cs.FlushFinalBlock();
                }
                catch (CryptographicException) { }
            }
            output.Reset();

            o.OnProgress(MausProgressState.VerifyingHMAC);

            byte[] actualHmac = ComputeHmac(output, _key, hashFunc);
            if (!CompareBytes(actualHmac, _hmac))
                throw new CryptographicException(TextResources.BadKey);

            output.Reset();
            return output;
        }

        internal static byte[] Encrypt(IMausProgress o, MausBufferStream output, MausBufferStream _bufferStream, byte[] _key, byte[] _iv, MausHashFunction hashFunc)
        {
            o.OnProgress(MausProgressState.Encrypting);

            using (SymmetricAlgorithm alg = GetAlgorithm(_key, _iv))
            using (ICryptoTransform transform = alg.CreateEncryptor())
            {
                CryptoStream cs = new CryptoStream(output, transform, CryptoStreamMode.Write);

                _bufferStream.BufferCopyTo(cs, false);
                cs.FlushFinalBlock();
            }
            output.Reset();
            _bufferStream.Reset();
            o.OnProgress(MausProgressState.ComputingHMAC);
            byte[] hmac = ComputeHmac(_bufferStream, _key, hashFunc);
            o.OnProgress(new MausProgressEventArgs(MausProgressState.ComputingHMACCompleted, hmac));
            return hmac;

        }

        #region Disposal
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
                    if (!_leaveOpen)
                        _baseStream.Dispose();
                }
            }
            finally
            {
                _baseStream = null;
                _bufferStream = null;
                Progress = null;
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
            if (_bufferStream == null || _mode != CompressionMode.Compress || _bufferStream.Length == 0)
                return;
            if (_encFmt != MausEncryptionFormat.None && _password == null)
                throw new InvalidOperationException(TextResources.KeyNotSet);

            _bufferStream.Reset();

            byte[] rsaSignature, dsaSignature, ecdsaSignature, hashChecksum;

            if (_rsaSignParamBC != null || _dsaSignParamBC != null || _ecdsaSignParamBC != null)
            {
                OnProgress(MausProgressState.ComputingHash);
                hashChecksum = ComputeHash(_bufferStream, _hashFunc);
                OnProgress(new MausProgressEventArgs(MausProgressState.ComputingHashCompleted, hashChecksum));
                if (_rsaSignParamBC != null)
                {
                    OnProgress(MausProgressState.SigningRSA);
                    try
                    {
                        RsaBlindedEngine _engine = new RsaBlindedEngine();
                        _engine.Init(true, _rsaSignParamBC);

                        byte[] message = Pkcs7Provider.AddPadding(GetDerEncoded(hashChecksum, GetHashId(_hashFunc)), _engine.GetInputBlockSize());

                        rsaSignature = _engine.ProcessBlock(message, 0, message.Length);
                    }
                    catch (Exception x)
                    {
                        throw new CryptographicException(TextResources.RsaSigPrivInvalid, x);
                    }
                    _bufferStream.Reset();
                }
                else rsaSignature = null;
                if (_dsaSignParamBC != null)
                {
                    OnProgress(MausProgressState.SigningDSA);
                    try
                    {
                        DsaSigner signer = new DsaSigner(GetDsaCalc());

                        dsaSignature = GenerateDsaSignature(hashChecksum, signer, _dsaSignParamBC);
                    }
                    catch (Exception x)
                    {
                        throw new CryptographicException(TextResources.DsaSigPrivInvalid, x);
                    }
                    _bufferStream.Reset();
                }
                else dsaSignature = null;
                if (_ecdsaSignParamBC != null)
                {
                    OnProgress(MausProgressState.SigningECDSA);
                    try
                    {
                        ECDsaSigner signer = new ECDsaSigner(GetDsaCalc());
                        ecdsaSignature = GenerateDsaSignature(hashChecksum, signer, _ecdsaSignParamBC);
                    }
                    catch (Exception x)
                    {
                        throw new CryptographicException(TextResources.EcdsaSigPrivInvalid, x);
                    }
                    _bufferStream.Reset();
                }
                else ecdsaSignature = null;
            }
            else rsaSignature = dsaSignature = ecdsaSignature = hashChecksum = null;

            byte[] _key;
            if (_encFmt == MausEncryptionFormat.None)
                _key = null;
            else
            {
                OnProgress(MausProgressState.BuildingKey);
                _key = GetKey(_password, _salt, _pkCount, _keySize, _hashFunc);
            }

            long oldLength = _bufferStream.Length;

            MausBufferStream compressedStream;
            if (_cmpFmt == MausCompressionFormat.None)
                compressedStream = _bufferStream;
            else if (_cmpFmt == MausCompressionFormat.Lzma)
            {
                compressedStream = new MausBufferStream();

                LzmaEncoder encoder = new LzmaEncoder();
                object[] props = new object[]
                {
                    (int)(_lzmaDictSize == LzmaDictionarySize.Default ? LzmaDictionarySize.Size8m : _lzmaDictSize),
                    2,
                    3,
                    0,
                    128,
                    2,
                    "BT4",
                    true,
                };
                encoder.SetCoderProperties(ids, props);

                encoder.WriteCoderProperties(compressedStream);
                encoder.Code(_bufferStream, compressedStream, _bufferStream.Length, -1, this);
            }
            else
            {
                OnProgress(MausProgressState.Compressing);
                compressedStream = new MausBufferStream();
#if COMPLVL
                using (DeflateStream ds = new DeflateStream(compressedStream, _cmpLvl, true))
#else
                using (DeflateStream ds = new DeflateStream(compressedStream, CompressionMode.Compress, true))
#endif
                    _bufferStream.BufferCopyTo(ds, false);
            }
            compressedStream.Reset();
#if NOLEAVEOPEN
            BinaryWriter writer = new BinaryWriter(_baseStream);
#else
            using (BinaryWriter writer = new BinaryWriter(_baseStream, _textEncoding, true))
#endif
            {
                OnProgress(MausProgressState.WritingHead);
                writer.Write(_head);
                writer.Write(_versionShort);
                {
                    ByteOptionList formats = new ByteOptionList();

                    if (_encryptedOptions == null || !_encryptedOptions.Contains(MausOptionToEncrypt.Filename))
                        FormatSetFilename(formats);

                    if (_encryptedOptions == null || !_encryptedOptions.Contains(MausOptionToEncrypt.Compression))
                        FormatSetCompression(formats);

                    if (_encryptedOptions == null || !_encryptedOptions.Contains(MausOptionToEncrypt.CreatedTime))
                        FormatSetTimes(formats, _kTimeC, _timeC);

                    if (_encryptedOptions == null || !_encryptedOptions.Contains(MausOptionToEncrypt.ModTime))
                        FormatSetTimes(formats, _kTimeM, _timeM);

                    if (_encryptedOptions == null || !_encryptedOptions.Contains(MausOptionToEncrypt.Comment))
                        FormatSetComment(formats);

                    formats.Add(_kHash);
                    formats.Add(HashBDict[_hashFunc]);

                    if (_encFmt == MausEncryptionFormat.Aes)
                    {
                        formats.Add(_kEncAes);
                        formats.Add((short)_keySize);
                    }
                    else WriteRsaSig(rsaSignature, dsaSignature, ecdsaSignature, formats);

                    formats.Write(writer);
                }

                if (_encFmt == MausEncryptionFormat.None)
                {
                    writer.Write(compressedStream.Length);
                    writer.Write(_bufferStream.Length);
                    if (hashChecksum == null)
                    {
                        _bufferStream.Reset();
                        OnProgress(MausProgressState.ComputingHash);
                        hashChecksum = ComputeHash(_bufferStream, _hashFunc);
                        OnProgress(new MausProgressEventArgs(MausProgressState.ComputingHashCompleted, hashChecksum));
                    }
                    writer.Write(hashChecksum);

                    if (_bufferStream != compressedStream)
                    {
                        _bufferStream.Close();
                        _bufferStream = compressedStream;
                    }
                    _bufferStream.Reset();

                    _bufferStream.BufferCopyTo(_baseStream, false);
                }
                else
                {
                    using (MausBufferStream opts = new MausBufferStream())
                    {
                        ByteOptionList formats = new ByteOptionList();

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
                                case MausOptionToEncrypt.CreatedTime:
                                    FormatSetTimes(formats, _kTimeC, _timeC);
                                    continue;
                                case MausOptionToEncrypt.ModTime:
                                    FormatSetTimes(formats, _kTimeM, _timeM);
                                    continue;
                                case MausOptionToEncrypt.Comment:
                                    FormatSetComment(formats);
                                    continue;
                            }
                        }

                        WriteRsaSig(rsaSignature, dsaSignature, ecdsaSignature, formats);

                        formats.Add(_kULen);
                        formats.Add(_bufferStream.Length);

                        if (compressedStream != _bufferStream)
                        {
                            _bufferStream.Close();
                            _bufferStream = compressedStream;
                        }

                        using (BinaryWriter formatWriter = new BinaryWriter(opts))
                        {
                            formats.Write(formatWriter);
                            _bufferStream.Prepend(opts);
                        }
                    }

                    using (MausBufferStream output = new MausBufferStream())
                    {
                        output.Write(_salt, 0, _key.Length);
                        output.Write(_iv, 0, _iv.Length);
                        byte[] hashHmac = Encrypt(this, output, _bufferStream, _key, _iv, _hashFunc);

                        writer.Write(output.Length);
                        writer.Write((long)_pkCount);
                        writer.Write(hashHmac);

                        output.BufferCopyTo(_baseStream, false);
                    }
                    Array.Clear(_key, 0, _key.Length);
                }
            }
            OnProgress(new MausProgressEventArgs(MausProgressState.CompletedWriting, oldLength, _bufferStream.Length));
#if NOLEAVEOPEN
            _baseStream.Flush();
#endif
        }

        private static byte[] GetDerEncoded(byte[] hash, DerObjectIdentifier derId)
        {
            AlgorithmIdentifier _id = new AlgorithmIdentifier(derId, DerNull.Instance);
            DigestInfo dInfo = new DigestInfo(_id, hash);

            return dInfo.GetDerEncoded();
        }

        private static byte[] GenerateDsaSignature(byte[] hash, IDsa signer, ICipherParameters key)
        {
            signer.Init(true, key);

            var ints = signer.GenerateSignature(hash);

            return new DerSequence(new DerInteger(ints[0]), new DerInteger(ints[1])).GetDerEncoded();
        }

        private void WriteRsaSig(byte[] rsaSignature, byte[] dsaSignature, byte[] ecdsaSignature, ByteOptionList formats)
        {
            WriteRsaSig(rsaSignature, _rsaSignId, _kRsaSig, formats);
            WriteRsaSig(dsaSignature, _dsaSignId, _kDsaSig, formats);
            WriteRsaSig(ecdsaSignature, _ecdsaSignId, _kECDsaSig, formats);
        }

        private static void WriteRsaSig(byte[] rsaSignature, byte[] _rsaSignId, string _kRsaSig, ByteOptionList formats)
        {
            if (rsaSignature == null)
                return;
            formats.Add(_kRsaSig);
            formats.Add(rsaSignature);

            if (_rsaSignId != null)
            {
                formats.Add(_kRsaSigId);
                formats.Add(_rsaSignId);
            }
        }

        private void FormatSetFilename(ByteOptionList formats)
        {
            if (_filename != null)
            {
                formats.Add(_kFilename);
                formats.Add(_textEncoding.GetBytes(_filename));
            }
        }

        private void FormatSetCompression(ByteOptionList formats)
        {
            switch (_cmpFmt)
            {
                case MausCompressionFormat.None:
                    formats.Add(_kCmpNone);
                    break;
                case MausCompressionFormat.Lzma:
                    formats.Add(_kCmpLzma);
                    break;
                default:
                    formats.Add(_kCmpDef);
                    break;
            }
        }

        private static void FormatSetTimes(ByteOptionList formats, string kTime, DateTime? dateTime)
        {
            if (!dateTime.HasValue)
                return;

            formats.Add(kTime);
            formats.Add(dateTime.Value.ToUniversalTime().Ticks);
        }

        private void FormatSetComment(ByteOptionList formats)
        {
            if (_comBytes == null || _comBytes.Length == 0)
                return;

            formats.Add(_kComment);
            formats.Add(_comBytes);
        }

        internal void Dispose(DieFledermausStream other)
        {
            other._comBytes = _comBytes;
            other._hashFunc = _hashFunc;
            other.Progress = Progress;
            Dispose();
        }
        #endregion

        /// <summary>
        /// Raised when the current stream is reading or writing data, and the progress changes meaningfully.
        /// </summary>
        public event MausProgressEventHandler Progress;

        private void OnProgress(MausProgressState state)
        {
            OnProgress(new MausProgressEventArgs(state));
        }

        void IMausProgress.OnProgress(MausProgressState state)
        {
            OnProgress(new MausProgressEventArgs(state));
        }

        private void OnProgress(MausProgressEventArgs e)
        {
            if (Progress != null)
                Progress(this, e);
        }

        void IMausProgress.OnProgress(MausProgressEventArgs e)
        {
            OnProgress(e);
        }

        void ICodeProgress.SetProgress(long inSize, long outSize)
        {
            MausProgressState state;
            if (_mode == CompressionMode.Compress)
                state = MausProgressState.CompressingWithSize;
            else
                state = MausProgressState.DecompressingWithSize;

            if (Progress != null)
                Progress(this, new MausProgressEventArgs(state, inSize, outSize));
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        ~DieFledermausStream()
        {
            Dispose(false);
        }

        /// <summary>
        /// A collection of <see cref="MausOptionToEncrypt"/> values.
        /// </summary>
        public sealed class SettableOptions : MausSettableOptions<MausOptionToEncrypt>
        {
            private DieFledermausStream _stream;

            internal SettableOptions(DieFledermausStream stream)
            {
                _stream = stream;
            }

            /// <summary>
            /// Gets a value indicating whether the current instance is read-only.
            /// Returns <c>true</c> if the underlying stream is closed or is in read-mode; <c>false</c> otherwise.
            /// </summary>
            /// <remarks>
            /// This property indicates that the collection cannot be changed externally. If <see cref="IsFrozen"/> is <c>false</c>,
            /// however, it may still be changed by the base stream.
            /// </remarks>
            public override bool IsReadOnly
            {
                get { return _stream._baseStream == null || _stream._mode == CompressionMode.Decompress; }
            }

            /// <summary>
            /// Gets a value indicating whether the current instance is entirely frozen against all further changes.
            /// Returns <c>true</c> if the underlying stream is closed or is in read-mode and has successfully decoded the file;
            /// <c>false</c> otherwise.
            /// </summary>
            public override bool IsFrozen
            {
                get { return _stream._baseStream == null || (_stream._mode == CompressionMode.Decompress && _stream._headerGotten); }
            }
        }

        internal const string CollectionDebuggerDisplay = "Count = {Count}";
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
    /// Indicates values to encrypt in a <see cref="DieFledermausStream"/>.
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
        /// Indicates that <see cref="DieFledermausStream.CreatedTime"/> will be encrypted.
        /// </summary>
        CreatedTime,
        /// <summary>
        /// Indicates that <see cref="DieFledermausStream.ModifiedTime"/> will be encrypted.
        /// </summary>
        ModTime,
        /// <summary>
        /// Indicates that <see cref="DieFledermausStream.Comment"/> will be encrypted.
        /// </summary>
        Comment,
    }

    /// <summary>
    /// Specifies which hash function is used.
    /// </summary>
    public enum MausHashFunction
    {
        /// <summary>
        /// The SHA-256 hash function (SHA-2).
        /// </summary>
        Sha256,
        /// <summary>
        /// The SHA-512 hash function (SHA-2).
        /// </summary>
        Sha512,
        /// <summary>
        /// The SHA-3/256 hash function.
        /// </summary>
        Sha3_256,
        /// <summary>
        /// The SHA-3/512 hash function.
        /// </summary>
        Sha3_512,
        /// <summary>
        /// The SHA-224 hash function (SHA-2).
        /// </summary>
        Sha224,
        /// <summary>
        /// The SHA-384 hash function (SHA-2).
        /// </summary>
        Sha384,
        /// <summary>
        /// The SHA-3/224 hash function.
        /// </summary>
        Sha3_224,
        /// <summary>
        /// The SHA-3/384 hash function.
        /// </summary>
        Sha3_384,
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
