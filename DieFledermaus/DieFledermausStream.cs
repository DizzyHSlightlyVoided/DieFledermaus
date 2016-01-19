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
using System.Text;

using DieFledermaus.Globalization;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.IO;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC.Multiplier;

using SevenZip;
using SevenZip.Compression.LZMA;

namespace DieFledermaus
{
    using RandomNumberGenerator = System.Security.Cryptography.RandomNumberGenerator;

    /// <summary>
    /// Provides methods and properties for compressing and decompressing files and streams in the DieFledermaus format.
    /// </summary>
    /// <remarks>
    /// <para>Unlike streams such as <see cref="DeflateStream"/>, this method reads part of the stream during the constructor, rather than the first call
    /// to <see cref="Read(byte[], int, int)"/> or <see cref="ReadByte()"/>.</para>
    /// <para>When writing, if nothing has been written to the current stream when the current instance is disposed, nothing will be written to the
    /// underlying stream.</para>
    /// </remarks>
    public partial class DieFledermausStream : Stream, IMausCrypt, IMausProgress, IMausSign
    {
        internal const int Max16Bit = 65536;
        internal const int _head = 0x5375416d; //Little-endian "mAuS"
        private const ushort _versionShort = 99, _minVersionShort = _versionShort;

        internal static readonly UTF8Encoding _textEncoding = new UTF8Encoding(false, false);

        private Stream _baseStream;
        private MausBufferStream _bufferStream;
        private CompressionMode _mode;
        private bool _leaveOpen;
        private long _uncompressedLength;

        internal static void CheckStreamRead(Stream stream)
        {
            if (stream.CanRead) return;

            if (stream.CanWrite) throw new ArgumentException(TextResources.StreamNotReadable, nameof(stream));
            throw new ObjectDisposedException(nameof(stream), TextResources.StreamClosed);
        }

        internal static void CheckStreamWrite(Stream stream)
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
        /// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open when the current instance is disposed;
        /// <see langword="false"/> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <see langword="null"/>.
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
                CheckStreamWrite(stream);
                _bufferStream = new MausBufferStream();
                _baseStream = stream;
            }
            else if (compressionMode == CompressionMode.Decompress)
            {
                CheckStreamRead(stream);
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
        /// <paramref name="stream"/> is <see langword="null"/>.
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
        /// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open when the current instance is disposed;
        /// <see langword="false"/> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <see langword="null"/>.
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
        /// <paramref name="stream"/> is <see langword="null"/>.
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
        /// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open when the current instance is disposed;
        /// <see langword="false"/> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <see langword="null"/>.
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
            CheckStreamWrite(stream);

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
        /// <paramref name="stream"/> is <see langword="null"/>.
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
        /// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open when the current instance is disposed;
        /// <see langword="false"/> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <see langword="null"/>.
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
        /// <paramref name="stream"/> is <see langword="null"/>.
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
        /// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open when the current instance is disposed;
        /// <see langword="false"/> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <see langword="null"/>.
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
        /// <paramref name="stream"/> is <see langword="null"/>.
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
        /// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open when the current instance is disposed;
        /// <see langword="false"/> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <see langword="null"/>.
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
        /// <paramref name="stream"/> is <see langword="null"/>.
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
        /// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open when the current instance is disposed;
        /// <see langword="false"/> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <see langword="null"/>.
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
            CheckStreamWrite(stream);
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
        /// <paramref name="stream"/> is <see langword="null"/>.
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
        /// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open when the current instance is disposed;
        /// <see langword="false"/> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <see langword="null"/>.
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
        /// <paramref name="stream"/> is <see langword="null"/>.
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
            else if (path.Equals(DieFledermauZManifest.Filename, StringComparison.Ordinal))
                _allowDirNames = AllowDirNames.Manifest;
            else
                _allowDirNames = AllowDirNames.Yes;
            _getHeader(readMagNum);
            if (_allowDirNames == AllowDirNames.Manifest && _filename == null)
                throw new InvalidDataException(TextResources.InvalidDataMaus);
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

        internal static KeySizeList _getKeySizes(MausEncryptionFormat encryptionFormat, out int blockByteCount)
        {
            switch (encryptionFormat)
            {
                case MausEncryptionFormat.None:
                    blockByteCount = 0;
                    return null;
                case MausEncryptionFormat.Aes:
                case MausEncryptionFormat.Twofish:
                    blockByteCount = _blockByteCtAes;
                    return new KeySizeList(_keyBitAes128, _keyBitAes192, _keyBitAes256);
                case MausEncryptionFormat.Threefish:
                    blockByteCount = _blockByteCtThreefish;
                    return new KeySizeList(_keyBitThreefish256, _keyBitThreefish512, _keyBitThreefish1024);
                default:
                    throw new InvalidEnumArgumentException(nameof(encryptionFormat), (int)encryptionFormat, typeof(MausEncryptionFormat));
            }
        }

        internal void SetSignatures(RsaKeyParameters rsaKey, byte[] rsaKeyId, DsaKeyParameters dsaKey, byte[] dsaKeyId, ECKeyParameters ecdsaKey, byte[] ecdsaKeyId)
        {
            _rsaSignParamBC = rsaKey;
            _rsaSignId = rsaKeyId;
            _dsaSignParamBC = dsaKey;
            _dsaSignId = dsaKeyId;
            _ecdsaSignParamBC = ecdsaKey;
            _ecdsaSignId = ecdsaKeyId;
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        public override bool CanRead
        {
            get { return _baseStream != null && _mode == CompressionMode.Decompress; }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking. Always returns <see langword="false"/>.
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

        /// <summary>
        /// Gets a value indicating whether the current instance is in read-mode and has been successfully decrypted.
        /// </summary>
        public bool IsDecrypted { get { return _mode == CompressionMode.Decompress && _encFmt == MausEncryptionFormat.None || _headerGotten; } }
        internal bool DataIsLoaded { get { return _bufferStream != null; } }

        private DateTime? _timeC;
        /// <summary>
        /// Gets and sets the time at which the underlying file was created, or <see langword="null"/> to specify no creation time.
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
        /// or <see langword="null"/> to specify no modification time.
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
        /// Gets the hash of the uncompressed data, or <see langword="null"/> if the current instance is in write-mode or has not yet been decrypted.
        /// </summary>
        public byte[] Hash
        {
            get
            {
                if (_hashExpected == null) return null;
                return (byte[])_hashExpected.Clone();
            }
        }

        private byte[] _hmacExpected;
        /// <summary>
        /// Gets the loaded HMAC of the encrypted data, or <see langword="null"/> if the current instance is in write-mode or is not encrypted.
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
        /// <para>In a set operation, the current stream is in read-mode.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current stream is not encrypted.</para>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// In a set operation, the specified value is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the length of the specified value is not equal to <see cref="BlockByteCount"/>.
        /// </exception>
        public byte[] IV
        {
            get
            {
                if (_iv == null) return null;
                return (byte[])_iv.Clone();
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
        /// <para>In a set operation, the current stream is in read-mode.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current stream is not encrypted.</para>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// In a set operation, the specified value is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the length of the specified value is not equal to the maximum key length specified by <see cref="LegalKeySizes"/>.
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
                if (value.Length != (_keySizes.MaxSize >> 3))
                    throw new ArgumentException(TextResources.SaltLength, nameof(value));

                _salt = (byte[])value.Clone();
            }
        }

        private KeySizeList _keySizes;
        /// <summary>
        /// Gets a list containing all valid key values for <see cref="KeySize"/>, or <see langword="null"/> if the current stream is not encrypted.
        /// </summary>
        public KeySizeList LegalKeySizes
        {
            get { return _keySizes; }
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
        /// <para>-OR-</para>
        /// <para>In a set operation, <see cref="Key"/> is not <see langword="null"/> and the specified value is not the proper length.</para>
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// In a set operation, <see cref="LegalKeySizes"/> does not contain the specified value.
        /// </exception>
        public int KeySize
        {
            get { return _keySize; }
            set
            {
                _ensureCanWrite();
                if (_encFmt == MausEncryptionFormat.None)
                    throw new NotSupportedException(TextResources.NotEncrypted);
                if (_key != null && value != _key.Length << 3)
                    throw new NotSupportedException(TextResources.NotSameLength);
                if (!_keySizes.Contains(value))
                    throw new ArgumentOutOfRangeException(nameof(value), value, TextResources.KeyLength);
                _keySize = value;
            }
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
        /// Gets the maximum number of bits in a single block of encrypted data, or 0 if the current instance is not encrypted.
        /// </summary>
        public int BlockSize { get { return _blockByteCount << 3; } }

        private int _blockByteCount;
        /// <summary>
        /// Gets the maximum number of bytes in a single block of encrypted data, or 0 if the current instance is not encrypted.
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
        /// If the current stream is in read-mode, this property will return <see langword="true"/> if and only if the underlying stream
        /// was signed when it was written.
        /// If the current stream is in write-mode, this property will return <see langword="true"/> if <see cref="RSASignParameters"/>
        /// is not <see langword="null"/>.
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
        /// <para>In a set operation, the current stream is in read-mode, and the specified value does not represent a valid public or private key.</para>
        /// </exception>
        public RsaKeyParameters RSASignParameters
        {
            get { return _rsaSignParamBC; }
            set
            {
                if (_baseStream == null)
                    throw new ObjectDisposedException(null, TextResources.CurrentClosed);
                CheckSignParam(value, _rsaSignature, _rsaSignVerified, _mode == CompressionMode.Decompress);
                CheckSignParam(value, _mode == CompressionMode.Compress);

                _rsaSignParamBC = value;
            }
        }

        internal static void CheckSignParam(RsaKeyParameters value, bool writing)
        {
            if (value == null)
                return;

            if (writing && !(value is RsaPrivateCrtKeyParameters))
                throw new ArgumentException(TextResources.RsaNeedPrivate, nameof(value));
        }

        internal static RsaKeyParameters PublicFromPrivate(RsaKeyParameters rsaKey)
        {
            if (!rsaKey.IsPrivate)
                return rsaKey;

            RsaPrivateCrtKeyParameters rsaPrivateKey = rsaKey as RsaPrivateCrtKeyParameters;
            if (rsaPrivateKey != null)
                return new RsaKeyParameters(false, rsaPrivateKey.Modulus, rsaPrivateKey.PublicExponent);

            return new RsaKeyParameters(false, rsaKey.Modulus, rsaKey.Exponent);
        }

        internal static void CheckSignParam(AsymmetricKeyParameter value, object signature, bool signVerified, bool reading)
        {
            if (reading)
            {
                if (signature == null)
                    throw new NotSupportedException(TextResources.RsaSigNone);
                if (signVerified)
                    throw new InvalidOperationException(TextResources.RsaSigVerified);
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
        /// In a set operation, <see cref="RSASignParameters"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <see langword="null"/> and has a length equal to 0 or greater than 65536.
        /// </exception>
        public byte[] RSASignIdBytes
        {
            get { return _rsaSignId; }
            set
            {
                _ensureCanWrite();
                if (_rsaSignParamBC == null)
                    throw new InvalidOperationException(TextResources.RsaSigNone);
                if (value != null && (value.Length == 0 || value.Length > Max16Bit))
                    throw new ArgumentException(TextResources.RsaIdLength, nameof(value));
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
        /// In a set operation, <see cref="RSASignParameters"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <see langword="null"/> and has a length equal to 0 or greater than 65536 UTF-8 bytes.
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
        /// If the current stream is in read-mode, this property will return <see langword="true"/> if and only if the underlying stream
        /// was signed when it was written.
        /// If the current stream is in write-mode, this property will return <see langword="true"/> if <see cref="RSASignParameters"/>
        /// is not <see langword="null"/>.
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
        private DsaPublicKeyParameters _dsaSignPub;
        /// <summary>
        /// Gets and sets a DSA key used to sign the current stream.
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
        /// <para>In a set operation, the current stream is in read-mode, and the specified value does not represent a valid public or private key.</para>
        /// </exception>
        public DsaKeyParameters DSASignParameters
        {
            get { return _dsaSignParamBC; }
            set
            {
                if (_baseStream == null)
                    throw new ObjectDisposedException(null, TextResources.CurrentClosed);
                CheckSignParam(value, _dsaSignature, _dsaSignVerified, _mode == CompressionMode.Decompress);
                _dsaSignPub = CheckSignParam(value, _mode == CompressionMode.Compress, _hashFunc);
                _dsaSignParamBC = value;
            }
        }

        internal static DsaPublicKeyParameters CheckSignParam(DsaKeyParameters value, bool writing, MausHashFunction _hashFunc)
        {
            if (value == null)
                return null;

            if (!writing)
                value = PublicFromPrivate(value);
            try
            {
                new DsaSigner(GetDsaCalc(_hashFunc)).Init(writing, value);
            }
            catch
            {
                throw new ArgumentException(writing ?
                    TextResources.RsaNeedPrivate : TextResources.RsaNeedPublic,
                    nameof(value));
            }
            return value as DsaPublicKeyParameters;
        }

        internal static DsaPublicKeyParameters PublicFromPrivate(DsaKeyParameters value)
        {
            {
                DsaPublicKeyParameters dsaKeyPub = value as DsaPublicKeyParameters;
                if (dsaKeyPub != null)
                    return dsaKeyPub;
            }

            DsaPrivateKeyParameters dsaKeyPriv = value as DsaPrivateKeyParameters;
            if (dsaKeyPriv == null)
                throw new ArgumentException(TextResources.RsaNeedPublic, nameof(value));

            try
            {
                BigInteger y = dsaKeyPriv.Parameters.G.ModPow(dsaKeyPriv.X, dsaKeyPriv.Parameters.P);
                return new DsaPublicKeyParameters(y, dsaKeyPriv.Parameters);
            }
            catch (Exception x)
            {
                throw new ArgumentException(TextResources.RsaNeedPublic, nameof(value), x);
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
        /// In a set operation, <see cref="DSASignParameters"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <see langword="null"/> and has a length equal to 0 or greater than 65536.
        /// </exception>
        public byte[] DSASignIdBytes
        {
            get { return _dsaSignId; }
            set
            {
                _ensureCanWrite();
                if (_dsaSignParamBC == null)
                    throw new InvalidOperationException(TextResources.RsaSigNone);
                if (value != null && (value.Length == 0 || value.Length > Max16Bit))
                    throw new ArgumentException(TextResources.RsaIdLength, nameof(value));
                _rsaSignId = value;
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
        /// In a set operation, <see cref="DSASignParameters"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <see langword="null"/> and has a length equal to 0 or greater than 65536 UTF-8 bytes.
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
        /// If the current stream is in read-mode, this property will return <see langword="true"/> if and only if the underlying stream
        /// was signed when it was written.
        /// If the current stream is in write-mode, this property will return <see langword="true"/> if <see cref="RSASignParameters"/>
        /// is not <see langword="null"/>.
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
        private ECPublicKeyParameters _ecdsaSignPub;
        /// <summary>
        /// Gets and sets an ECDSA key used to sign the current stream.
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
        /// <para>In a set operation, the current stream is in read-mode, and the specified value does not represent a valid public or private key.</para>
        /// </exception>
        public ECKeyParameters ECDSASignParameters
        {
            get { return _ecdsaSignParamBC; }
            set
            {
                if (_baseStream == null)
                    throw new ObjectDisposedException(null, TextResources.CurrentClosed);
                CheckSignParam(value, _ecdsaSignature, _ecdsaSignVerified, _mode == CompressionMode.Decompress);
                _ecdsaSignPub = CheckSignParam(value, _mode == CompressionMode.Compress, _hashFunc);
                _ecdsaSignParamBC = value;
            }
        }

        internal static ECPublicKeyParameters CheckSignParam(ECKeyParameters value, bool writing, MausHashFunction _hashFunc)
        {
            if (value == null)
                return null;

            if (!writing)
                value = PublicFromPrivate(value);
            try
            {
                new ECDsaSigner(GetDsaCalc(_hashFunc)).Init(writing, value);
            }
            catch
            {
                throw new ArgumentException(writing ?
                    TextResources.RsaNeedPrivate : TextResources.RsaNeedPublic,
                    nameof(value));
            }
            return value as ECPublicKeyParameters;
        }

        internal static ECPublicKeyParameters PublicFromPrivate(ECKeyParameters value)
        {
            {
                ECPublicKeyParameters ecdsaPublic = value as ECPublicKeyParameters;
                if (ecdsaPublic != null)
                    return ecdsaPublic;
            }

            ECPrivateKeyParameters ecdsaPrivate = value as ECPrivateKeyParameters;
            if (ecdsaPrivate == null)
                throw new ArgumentException(TextResources.RsaNeedPublic, nameof(value));

            try
            {
                var ec = ecdsaPrivate.Parameters;

                var q = new FixedPointCombMultiplier().Multiply(ec.G, ecdsaPrivate.D);
                if (ecdsaPrivate.PublicKeyParamSet == null)
                    return new ECPublicKeyParameters(ecdsaPrivate.AlgorithmName, q, ec);
                return new ECPublicKeyParameters(ecdsaPrivate.AlgorithmName, q, ecdsaPrivate.PublicKeyParamSet);
            }
            catch (Exception x)
            {
                throw new ArgumentException(TextResources.RsaNeedPublic, nameof(value), x);
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
        /// In a set operation, <see cref="ECDSASignParameters"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <see langword="null"/> and has a length equal to 0 or greater than 65536.
        /// </exception>
        public byte[] ECDSASignIdBytes
        {
            get { return _ecdsaSignId; }
            set
            {
                _ensureCanWrite();
                if (_ecdsaSignParamBC == null)
                    throw new InvalidOperationException(TextResources.RsaSigNone);
                if (value != null && (value.Length == 0 || value.Length > Max16Bit))
                    throw new ArgumentException(TextResources.RsaIdLength, nameof(value));
                _rsaSignId = value;
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
        /// In a set operation, <see cref="ECDSASignParameters"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <see langword="null"/> and has a length equal to 0 or greater than 65536 UTF-8 bytes.
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

        #region RSA Encrypted Key
        private byte[] _rsaEncKey;
        /// <summary>
        /// Gets a value indicating whether the current stream is encrypted with an RSA key.
        /// </summary>
        /// <remarks>
        /// If the current stream is in read-mode, this property will return <see langword="true"/> if and only if the underlying stream
        /// was encrypted with an RSA key when it was written.
        /// If the current stream is in write-mode, this property will return <see langword="true"/> if <see cref="RSAEncryptionParameters"/>
        /// is not <see langword="null"/>.
        /// </remarks>
        public bool IsRSAEncrypted
        {
            get
            {
                if (_mode == CompressionMode.Compress)
                    return _rsaEncParamBC != null;
                return _rsaEncKey != null;
            }
        }

        private RsaKeyParameters _rsaEncParamBC;
        /// <summary>
        /// Gets and sets an RSA key used to encrypt or decrypt the current stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para>The current stream is not encrypted.</para>
        /// <para>-OR-</para>
        /// <para>The current stream is in read-mode, and is not RSA encrypted.</para>
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, the current stream is in read-mode and has already been successfully decrypted.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>In a set operation, the current stream is in write-mode, and the specified value does not represent a valid public or private key.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current stream is in read-mode, and the specified value does not represent a valid private key.</para>
        /// </exception>
        public RsaKeyParameters RSAEncryptionParameters
        {
            get { return _rsaEncParamBC; }
            set
            {
                _ensureCanSetKey();
                if (_mode == CompressionMode.Decompress && _rsaEncKey == null)
                    throw new NotSupportedException(TextResources.RsaEncNone);
                CheckSignParam(value, _mode == CompressionMode.Decompress);
                _rsaEncParamBC = value;
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
        /// In a set operation, the specified value is not <see langword="null"/>, and has a length which is equal to 0 or which is greater than 65536 UTF-8 bytes.
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
        /// In a set operation, the specified value is not <see langword="null"/>, and has a length which is equal to 0 or which is greater than 65536.
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
        /// Gets a collection containing options which should be encrypted, or <see langword="null"/> if the current instance is not encrypted.
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
        /// In a set operation, the specified value is not <see langword="null"/> and is invalid.
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

        /// <summary>
        /// Sets <see cref="Key"/> to a value derived from <see cref="Password"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current stream is not encrypted.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// <para>In a set operation, the stream instance is in read-mode and has already been successfully decrypted.</para>
        /// <para>-OR-</para>
        /// <para><see cref="Password"/> is <see langword="null"/>.</para>
        /// </exception>
        public void DeriveKey()
        {
            _ensureCanSetKey();
            if (_password == null)
                throw new InvalidOperationException(TextResources.PasswordNotSet);
            _key = GetKey(this);
        }

        internal enum AllowDirNames
        {
            No,
            Yes,
            EmptyDir,
            Manifest,
            Unknown,
        }

        private AllowDirNames _allowDirNames;

        internal static bool IsValidFilename(string value, bool throwOnInvalid, AllowDirNames dirFormat, string paramName)
        {
            if (dirFormat == AllowDirNames.Manifest)
                return value.Equals(DieFledermauZManifest.Filename, StringComparison.Ordinal);

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
        /// <returns><see langword="true"/> if <paramref name="value"/> is a valid filename; <see langword="false"/> if <paramref name="value"/>
        /// has a length of 0, has a length greater than 256 UTF-8 bytes, contains unpaired surrogate characters, contains non-whitespace control
        /// characters (non-whitespace characters between <c>\u0000</c> and <c>\u001f</c> inclusive, or between <c>\u007f</c> and <c>\u009f</c>
        /// inclusive), contains only whitespace, or is "." or ".." (the "current directory" and "parent directory" identifiers).</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="value"/> is <see langword="null"/>.
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
        /// In a set operation, the specified value is not a valid <see cref="MausHashFunction"/> value.
        /// </exception>
        public MausHashFunction HashFunction
        {
            get { return _hashFunc; }
            set
            {
                _ensureCanWrite();
                if (!HashBDict.ContainsKey(value))
                    throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(MausHashFunction));
                _hashFunc = value;
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
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <see langword="null"/> and has a length of 0.
        /// </exception>
        public string Password
        {
            get { return _password; }
            set
            {
                _ensureCanSetKey();
                if (value != null && value.Length == 0)
                    throw new ArgumentException(TextResources.PasswordZeroLength, nameof(value));
                _password = value;
            }
        }

        private byte[] _key;
        /// <summary>
        /// Gets and sets a binary key used to encrypt or decrypt the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current stream is not encrypted.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, the current stream is in read-mode and has already been successfully decrypted.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value has an invalid length according to <see cref="LegalKeySizes"/>.
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
                if (value == null)
                {
                    _key = value;
                    return;
                }

                int keyBitSize = value.Length << 3;

                if (value.Length > int.MaxValue >> 3 || !_keySizes.Contains(keyBitSize))
                    throw new ArgumentException(TextResources.KeyLength, nameof(value));
                else
                {
                    _key = (byte[])value.Clone();
                    _keySize = keyBitSize;
                }
            }
        }

        internal static byte[] GetKey(IMausCrypt o)
        {
            var password = o.Password;
            var _salt = o.Salt;
            var keySize = o.KeySize;
            int keyLength = keySize >> 3;
            var _pkCount = o.PBKDF2CycleCount;
            if (_salt.Length > keyLength)
                Array.Resize(ref _salt, keyLength);

            Pkcs5S2ParametersGenerator gen = new Pkcs5S2ParametersGenerator(GetHashObject(o.HashFunction));
            gen.Init(_textEncoding.GetBytes(password), _salt, _pkCount + minPkCount);

            KeyParameter kParam = (KeyParameter)gen.GenerateDerivedMacParameters(keySize);
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

        internal const int _blockByteCtAes = 16, _blockByteCtThreefish = 128;
        internal const int _keyBitAes256 = 256;
        internal const int _keyBitAes128 = 128;
        internal const int _keyBitAes192 = 192;
        internal const int _keyBitThreefish256 = 256;
        internal const int _keyBitThreefish512 = 512;
        internal const int _keyBitThreefish1024 = 1024;

        private const string _kCmpNone = "NK", _kCmpDef = "DEF", _kCmpLzma = "LZMA";
        internal const string _kEnc = "Ver", _kEncAes = "AES", _kEncTwofish = "Twofish", _kEncThreefish = "Threefish", _kEncRsa = "RSAsch";
        internal const ushort _vEnc = 1;

        internal static readonly Dictionary<string, MausEncryptionFormat> _encDict = new Dictionary<string, MausEncryptionFormat>(StringComparer.Ordinal)
        {
            { _kEncAes, MausEncryptionFormat.Aes },
            { _kEncTwofish, MausEncryptionFormat.Twofish },
            { _kEncThreefish, MausEncryptionFormat.Threefish }
        };

        private const string _kRsaSig = "RSAsig", _kDsaSig = "DSAsig", _kECDsaSig = "ECDSAsig";
        private const ushort _vRsaSig = 1, _vDsaSig = 1, _vECDsaSig = 1;

        internal const string _kHash = "Hash";
        internal const ushort _vHash = 1;

        private const string _kTimeC = "Ers", _kTimeM = "Mod";
        private const ushort _vTime = 1;

        private bool _headerGotten;

        internal const int Max8Bit = 256;

        private static readonly Dictionary<string, MausCompressionFormat> _formDict = new Dictionary<string, MausCompressionFormat>(StringComparer.Ordinal)
        {
            { _kCmpNone, MausCompressionFormat.None },
            { _kCmpLzma, MausCompressionFormat.Lzma },
            { _kCmpDef, MausCompressionFormat.Deflate }
        };

        internal static readonly Dictionary<string, MausHashFunction> HashDict = ((MausHashFunction[])Enum.GetValues(typeof(MausHashFunction))).
            ToDictionary(i => i.ToString().Replace('_', '/').ToUpper(), StringComparer.Ordinal);
        internal static readonly Dictionary<MausHashFunction, string> HashBDict = HashDict.ToDictionary(i => i.Value, i => i.Key);

        private const string _kFilename = "Name", _kULen = "DeL";
        private const ushort _vFilename = 1;
        internal const string _kComment = "Kom";
        internal const ushort _vComment = 1;

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

                    int keySize = _keySize >> 3;
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

            ByteOptionList optionList;
            try
            {
                optionList = new ByteOptionList(reader);
            }
            catch (InvalidDataException)
            {
                throw new InvalidDataException(TextResources.InvalidDataMaus);
            }
            headSize += optionList.GetSize();

            foreach (FormatValue curValue in optionList)
            {
                string curForm = curValue.Key;

                MausCompressionFormat cmpFmt;
                if (_formDict.TryGetValue(curForm, out cmpFmt))
                {
                    if (curValue.Version != 1)
                        throw new NotSupportedException(TextResources.FormatUnknownZ);

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
                    if (curValue.Count != 1 || curValue.Version != _vHash)
                        throw new NotSupportedException(TextResources.FormatUnknown);

                    MausHashFunction hashFunc;
                    if (!HashDict.TryGetValue(curValue[0].ValueString, out hashFunc))
                        throw new NotSupportedException(TextResources.FormatUnknown);

                    if (_gotHash || fromEncrypted)
                    {
                        if (hashFunc != _hashFunc)
                            throw new InvalidDataException(TextResources.FormatBad);
                    }
                    else
                    {
                        _hashFunc = hashFunc;
                        _gotHash = true;
                    }

                    continue;
                }

                if (curForm.Equals(_kEncRsa, StringComparison.Ordinal))
                {
                    if (curValue.Count != 1 || curValue.Version != _vHash)
                        throw new NotSupportedException(TextResources.FormatUnknown);

                    byte[] gotKey = curValue[0].Value;

                    if (_rsaEncKey == null)
                        _rsaEncKey = gotKey;
                    else if (!CompareBytes(gotKey, _rsaEncKey))
                        throw new InvalidDataException(TextResources.FormatBad);

                    continue;
                }

                if (curForm.Equals(_kRsaSig, StringComparison.Ordinal))
                {
                    if ((curValue.Count != 1 && curValue.Count != 2) || curValue.Version != _vRsaSig)
                        throw new NotSupportedException(TextResources.FormatUnknown);

                    byte[] rsaSig = curValue[0].Value;

                    if (_rsaSignature == null)
                        _rsaSignature = rsaSig;
                    else if (!CompareBytes(rsaSig, _rsaSignature))
                        throw new InvalidDataException(TextResources.FormatBad);

                    if (curValue.Count == 1)
                        continue;

                    byte[] rsaId = curValue[1].Value;

                    if (_rsaSignId == null)
                        _rsaSignId = rsaId;
                    else if (!CompareBytes(rsaId, _rsaSignId))
                        throw new InvalidDataException(TextResources.FormatBad);

                    continue;
                }

                if (curForm.Equals(_kDsaSig, StringComparison.Ordinal))
                {
                    GetDsaValue(curValue, _vDsaSig, ref _dsaSignature, ref _dsaSignId);
                    continue;
                }

                if (curForm.Equals(_kECDsaSig, StringComparison.Ordinal))
                {
                    GetDsaValue(curValue, _vECDsaSig, ref _ecdsaSignature, ref _ecdsaSignId);
                    continue;
                }

                if (ReadEncFormat(curValue, ref _encFmt, ref _keySizes, ref _keySize, ref _blockByteCount, false))
                {
                    if (_encryptedOptions == null)
                        _encryptedOptions = new SettableOptions(this);
                    continue;
                }

                if (curForm.Equals(_kFilename, StringComparison.Ordinal))
                {
                    if (curValue.Count != 1 || curValue.Version != _vFilename)
                        throw new NotSupportedException(TextResources.FormatUnknown);

                    string filename = _textEncoding.GetString(curValue[0].Value);

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
                    if (curValue.Count != 1 || curValue.Version != 1)
                        throw new NotSupportedException(TextResources.FormatUnknown);

                    long? uLen = curValue[0].ValueInt64;

                    if (!uLen.HasValue || uLen <= 0 || (gotULen && uLen != _uncompressedLength))
                        throw new InvalidDataException(TextResources.FormatBad);

                    _uncompressedLength = uLen.Value;
                    gotULen = true;
                    continue;
                }

                if (curForm.Equals(_kTimeC, StringComparison.Ordinal))
                {
                    GetDate(curValue, ref _timeC, fromEncrypted, MausOptionToEncrypt.CreatedTime);
                    continue;
                }

                if (curForm.Equals(_kTimeM, StringComparison.Ordinal))
                {
                    GetDate(curValue, ref _timeM, fromEncrypted, MausOptionToEncrypt.ModTime);
                    continue;
                }

                if (curForm.Equals(_kComment, StringComparison.Ordinal))
                {
                    if (curValue.Count != 1 || curValue.Version != _vComment)
                        throw new NotSupportedException(TextResources.FormatUnknown);

                    byte[] comBytes = curValue[0].Value;

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

        internal static bool ReadEncFormat(FormatValue curValue, ref MausEncryptionFormat _encFmt, ref KeySizeList _keySizes,
            ref int _keySize, ref int _blockByteCount, bool mauZ)
        {
            if (!curValue.Key.Equals(_kEnc, StringComparison.Ordinal))
                return false;
            MausEncryptionFormat getEncFormat;

            if (curValue.Count != 2 || !_encDict.TryGetValue(curValue[0].ValueString, out getEncFormat))
                throw new NotSupportedException(mauZ ? TextResources.FormatUnknownZ : TextResources.FormatUnknown);

            if (_keySizes != null && getEncFormat != _encFmt)
                throw new InvalidDataException(mauZ ? TextResources.FormatBadZ : TextResources.FormatBad);

            ushort? keyBits = curValue[1].ValueUInt16;
            int blockByteCount;
            KeySizeList list = _getKeySizes(getEncFormat, out blockByteCount);
            if (!keyBits.HasValue)
                throw new InvalidDataException(mauZ ? TextResources.FormatBadZ : TextResources.FormatBad);

            if (!list.Contains(keyBits.Value))
                throw new NotSupportedException(mauZ ? TextResources.FormatUnknownZ : TextResources.FormatUnknown);

            if (_keySizes == null)
            {
                _keySize = keyBits.Value;
                _keySizes = new KeySizeList(keyBits.Value);
                if (getEncFormat == MausEncryptionFormat.Threefish)
                    _blockByteCount = _keySize >> 3;
                else
                    _blockByteCount = blockByteCount;
                _encFmt = getEncFormat;
            }
            else if (keyBits != _keySize || _blockByteCount != blockByteCount)
                throw new InvalidDataException(TextResources.FormatBad);
            return true;
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

        private static void GetDsaValue(FormatValue formatValue, ushort vDsa, ref DerIntegerPair existing, ref byte[] keyId)
        {
            if ((formatValue.Count != 1 && formatValue.Count != 2) || formatValue.Version != vDsa)
                throw new NotSupportedException(TextResources.FormatUnknown);

            byte[] message = formatValue[0].Value;

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

            if (formatValue.Count == 2)
            {
                byte[] newId = formatValue[1].Value;
                if (keyId == null)
                    keyId = newId;
                else if (!CompareBytes(keyId, newId))
                    throw new InvalidDataException(TextResources.FormatBad);
            }
        }

        internal static void ReadBytes(FormatValue curValue, ushort version, ref byte[] oldValue)
        {
            if (curValue.Count != 1 || curValue.Version != version)
                throw new NotSupportedException(TextResources.FormatUnknown);

            byte[] bytes = curValue[0].Value;

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

        private void GetDate(FormatValue curValue, ref DateTime? curTime, bool fromEncrypted, MausOptionToEncrypt option)
        {
            if (curValue.Count != 1 || curValue.Version != _vTime)
                throw new NotSupportedException(TextResources.FormatUnknown);

            long? value = curValue[0].ValueInt64;

            if (!value.HasValue || value < 0 || value > maxTicks)
                throw new InvalidDataException(TextResources.FormatBad);

            DateTime newVal = new DateTime(value.Value, DateTimeKind.Utc);

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
                case MausHashFunction.Whirlpool:
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
                case MausHashFunction.Whirlpool:
                    return 512;
            }
            throw new InvalidEnumArgumentException(nameof(hashFunc), (int)hashFunc, typeof(MausHashFunction));
        }

        private static IDigest GetHashObject(MausHashFunction hashFunc)
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
                case MausHashFunction.Sha3_224:
                case MausHashFunction.Sha3_256:
                case MausHashFunction.Sha3_384:
                case MausHashFunction.Sha3_512:
                    return new Sha3Digest(GetHashBitSize(hashFunc));
                case MausHashFunction.Whirlpool:
                    return new WhirlpoolDigest();
                default:
                    throw new InvalidEnumArgumentException(nameof(hashFunc), (int)hashFunc, typeof(MausHashFunction));
            }
        }

        private long _compLength;
        internal long CompressedLength { get { return _compLength; } }

        internal const int minPkCount = 9001, maxPkCount = int.MaxValue - minPkCount;
        private int _pkCount;
        /// <summary>
        /// Gets and sets the number of PBKDF2 cycles used to generate the password, minus 9001.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para>In a set operation, the current stream is not encrypted.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current stream is in read-only mode.</para>
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// In a set operation, the specified value is less than 0 or is greater than <see cref="int.MaxValue"/> minus 9001.
        /// </exception>
        public int PBKDF2CycleCount
        {
            get { return _pkCount; }
            set
            {
                _ensureCanWrite();
                if (_encFmt == MausEncryptionFormat.None)
                    throw new NotSupportedException(TextResources.NotEncrypted);
                if (value < 0 || value > maxPkCount)
                    throw new ArgumentOutOfRangeException(nameof(value), value, string.Format(TextResources.OutOfRangeMinMax, 0, maxPkCount));
                _pkCount = value;
            }
        }

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
        /// <exception cref="CryptoException">
        /// Either <see cref="Key"/> or <see cref="Password"/> is incorrect.
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
        /// <exception cref="CryptoException">
        /// The current stream is in read-mode, and either <see cref="Key"/> or <see cref="Password"/> is incorrect.
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
                return;

            byte[] key = _key;
            if (_encFmt != MausEncryptionFormat.None && _password == null && key == null)
            {
                if (_rsaEncKey == null)
                    throw new CryptoException(TextResources.KeyNotSet);

                if (_rsaEncParamBC == null)
                    throw new CryptoException(TextResources.KeyRsaNotSet);
                key = RsaDecrypt(_rsaEncKey, _rsaEncParamBC, _hashFunc, false);
            }

            GetBuffer();

            if (_encFmt != MausEncryptionFormat.None)
            {
                if (key == null)
                {
                    OnProgress(MausProgressState.BuildingKey);
                    key = GetKey(this);
                }

                using (MausBufferStream bufferStream = Decrypt(this, key, _bufferStream, _rsaEncParamBC != null))
                {
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
                    _key = key;
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
            if (_encFmt != MausEncryptionFormat.None)
                OnProgress(new MausProgressEventArgs(MausProgressState.ComputingHashCompleted, hashActual));
            if (_encFmt == MausEncryptionFormat.None || _rsaSignature != null)
            {
                if (_encFmt == MausEncryptionFormat.None)
                {
                    if (!CompareBytes(hashActual, _hashExpected))
                        throw new InvalidDataException(TextResources.BadChecksum);
                }
                _hashExpected = hashActual;
            }
            else _hashExpected = hashActual;
        }

        internal static byte[] RsaDecrypt(byte[] rsaEncKey, RsaKeyParameters rsaKeyParam, MausHashFunction hashFunc, bool mauZ)
        {
            try
            {
                OaepEncoding engine = new OaepEncoding(new RsaBlindedEngine(), GetHashObject(hashFunc));
                engine.Init(false, rsaKeyParam);
                return engine.ProcessBlock(rsaEncKey, 0, rsaEncKey.Length);
            }
            catch (Exception x)
            {
                throw new CryptoException(mauZ ? TextResources.BadRsaKeyZ : TextResources.BadRsaKey, x);
            }
        }

        #region Verify RSA
        /// <summary>
        /// Tests whether <see cref="RSASignParameters"/> is valid.
        /// </summary>
        /// <returns><see langword="true"/> if <see cref="RSASignParameters"/> is set to the correct public key; <see langword="false"/>
        /// if the current instance is not signed, or if <see cref="RSASignParameters"/> is not set to the correct value.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current stream is in write-mode.
        /// </exception>
        /// <exception cref="CryptoException">
        /// <see cref="RSASignParameters"/> is set to an entirely invalid value.
        /// </exception>
        public bool VerifyRSASignature()
        {
            _ensureCanRead();
            lock (_lock)
            {
                _readData();
            }
            if (_rsaSignVerified)
                return true;

            if (_rsaSignature == null || _rsaSignParamBC == null)
                return false;

            OnProgress(MausProgressState.VerifyingRSASignature);
            try
            {
                OaepEncoding engine = new OaepEncoding(new RsaBlindedEngine(), GetHashObject(_hashFunc));
                engine.Init(false, PublicFromPrivate(_rsaSignParamBC));

                byte[] sig;
                try
                {
                    sig = engine.ProcessBlock(_rsaSignature, 0, _rsaSignature.Length);
                }
                catch (Exception)
                {
                    return false;
                }
                if (CompareBytes(_hashExpected, sig))
                    return _rsaSignVerified = true;

                byte[] expected = GetDerEncoded(_hashExpected, _hashFunc);

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
                throw new CryptoException(TextResources.RsaSigInvalid, x);
            }
        }
        #endregion

        #region Verify DSA
        /// <summary>
        /// Tests whether <see cref="DSASignParameters"/> is valid.
        /// </summary>
        /// <returns><see langword="true"/> if <see cref="DSASignParameters"/> is set to the correct public key; <see langword="false"/>
        /// if the current instance is not signed, or if <see cref="DSASignParameters"/> is not set to the correct value.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current stream is in write-mode.
        /// </exception>
        /// <exception cref="CryptoException">
        /// <see cref="DSASignParameters"/> is set to an entirely invalid value.
        /// </exception>
        public bool VerifyDSASignature()
        {
            _ensureCanRead();
            lock (_lock)
            {
                _readData();
            }

            if (_dsaSignVerified)
                return true;

            if (_dsaSignature == null || _dsaSignParamBC == null)
                return false;
            OnProgress(MausProgressState.VerifyingDSASignature);
            try
            {
                DsaSigner signer = new DsaSigner(GetDsaCalc(_hashFunc));

                return _dsaSignVerified = VerifyDsaSignature(_hashExpected, _dsaSignature, signer, _dsaSignPub);
            }
            catch (Exception x)
            {
                throw new CryptoException(TextResources.DsaSigInvalid, x);
            }
        }
        #endregion

        #region Verify ECDSA
        /// <summary>
        /// Tests whether <see cref="ECDSASignParameters"/> is valid.
        /// </summary>
        /// <returns><see langword="true"/> if <see cref="ECDSASignParameters"/> is set to the correct public key; <see langword="false"/>
        /// if the current instance is not signed, or if <see cref="ECDSASignParameters"/> is not set to the correct value.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current stream is in write-mode.
        /// </exception>
        /// <exception cref="CryptoException">
        /// <see cref="ECDSASignParameters"/> is set to an entirely invalid value.
        /// </exception>
        public bool VerifyECDSASignature()
        {
            _ensureCanRead();
            lock (_lock)
            {
                _readData();
            }

            if (_ecdsaSignVerified)
                return true;

            if (_ecdsaSignature == null || _ecdsaSignParamBC == null)
                return false;
            OnProgress(MausProgressState.VerifyingECDSASignature);
            try
            {
                ECDsaSigner signer = new ECDsaSigner(GetDsaCalc(_hashFunc));

                return _ecdsaSignVerified = VerifyDsaSignature(_hashExpected, _ecdsaSignature, signer, _ecdsaSignPub);
            }
            catch (Exception x)
            {
                throw new CryptoException(TextResources.EcdsaSigInvalid, x);
            }
        }
        #endregion

        private static HMacDsaKCalculator GetDsaCalc(MausHashFunction _hashFunc)
        {
            return new HMacDsaKCalculator(GetHashObject(_hashFunc));
        }

        private static bool VerifyDsaSignature(byte[] hash, DerIntegerPair pair, IDsa signer, AsymmetricKeyParameter key)
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
            if (_bufferStream == null)
            {
                OnProgress(MausProgressState.LoadingData);
                long length = _compLength;
                _bufferStream = GetBuffer(length, _baseStream);
            }
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
        /// <exception cref="CryptoException">
        /// Either <see cref="Key"/> or <see cref="Password"/> is incorrect.
        /// It is safe to attempt to call <see cref="LoadData()"/>, <see cref="Read(byte[], int, int)"/>, <see cref="ReadByte()"/>, or 
        /// <see cref="ComputeHash()"/> again if this exception is caught.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="buffer"/> is <see langword="null"/>.
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
        /// <exception cref="CryptoException">
        /// Either <see cref="Key"/> or <see cref="Password"/> is incorrect.
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
        /// <paramref name="buffer"/> is <see langword="null"/>.
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
            IDigest shaHash = GetHashObject(hashFunc);

            inputStream.BufferCopyTo(shaHash.BlockUpdate);
            inputStream.Reset();

            byte[] output = new byte[shaHash.GetDigestSize()];
            shaHash.DoFinal(output, 0);
            return output;
        }

        internal static byte[] ComputeHmac(MausBufferStream inputStream, byte[] key, MausHashFunction hashFunc)
        {
            HMac hmac = new HMac(GetHashObject(hashFunc));
            hmac.Init(new KeyParameter(key));
            inputStream.BufferCopyTo(hmac.BlockUpdate);
            inputStream.Reset();

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

        private static bool RunCipher(byte[] key, IMausCrypt o, MausBufferStream bufferStream, MausBufferStream output, bool forEncryption)
        {
            IBlockCipher cipher;
            switch (o.EncryptionFormat)
            {
                case MausEncryptionFormat.Aes:
                    cipher = new AesEngine();
                    break;
                case MausEncryptionFormat.Twofish:
                    cipher = new TwofishEngine();
                    break;
                case MausEncryptionFormat.Threefish:
                    cipher = new ThreefishEngine(key.Length << 3);
                    break;
                default:
                    return true;
            }

            var bufferedCipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(cipher));

            bufferedCipher.Init(forEncryption, new ParametersWithIV(new KeyParameter(key), o.IV));

            using (MausBufferStream cryptoBuffer = new MausBufferStream())
            {
                try
                {
                    using (CipherStream cs = new CipherStream(cryptoBuffer, null, bufferedCipher))
                        bufferStream.BufferCopyTo(cs, false);
                    return true;
                }
                catch (InvalidCipherTextException)
                {
                    return false;
                }
                finally
                {
                    cryptoBuffer.Reset();
                    cryptoBuffer.BufferCopyTo(output, false);
                }
            }
        }

        internal static MausBufferStream Decrypt(IMausProgress o, byte[] key, MausBufferStream bufferStream, bool hasRsa)
        {
            o.OnProgress(MausProgressState.Decrypting);

            MausBufferStream output = new MausBufferStream();

            bool success = RunCipher(key, o, bufferStream, output, false);

            output.Reset();

            o.OnProgress(MausProgressState.VerifyingHMAC);

            byte[] actualHmac = ComputeHmac(output, key, o.HashFunction);
            if (!success || !CompareBytes(actualHmac, o.HMAC))
                throw new InvalidCipherTextException(hasRsa ? TextResources.BadKeyRsa : TextResources.BadKey);

            return output;
        }

        internal static byte[] Encrypt(IMausProgress o, MausBufferStream output, MausBufferStream bufferStream, byte[] key)
        {
            o.OnProgress(MausProgressState.Encrypting);

            RunCipher(key, o, bufferStream, output, true);

            output.Reset();
            bufferStream.Reset();
            o.OnProgress(MausProgressState.ComputingHMAC);
            byte[] hmac = ComputeHmac(bufferStream, key, o.HashFunction);
            o.OnProgress(new MausProgressEventArgs(MausProgressState.ComputingHMACCompleted, hmac));
            return hmac;
        }

        #region Disposal
        /// <summary>
        /// Releases all unmanaged resources used by the current instance, and optionally releases all managed resources.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources;
        /// <see langword="false"/> to release only unmanaged resources.</param>
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
            if (_encFmt != MausEncryptionFormat.None && _password == null && _key == null && _rsaEncKey == null)
                throw new InvalidOperationException(TextResources.KeyRsaNotSet);

            _bufferStream.Reset();

            byte[] rsaSignature, dsaSignature, ecdsaSignature;

            if (_rsaSignParamBC != null || _dsaSignParamBC != null || _ecdsaSignParamBC != null)
            {
                OnProgress(MausProgressState.ComputingHash);
                _hashExpected = ComputeHash(_bufferStream, _hashFunc);
                OnProgress(new MausProgressEventArgs(MausProgressState.ComputingHashCompleted, _hashExpected));
                if (_rsaSignParamBC != null)
                {
                    OnProgress(MausProgressState.SigningRSA);
                    byte[] message = GetDerEncoded(_hashExpected, _hashFunc);
                    OaepEncoding engine = new OaepEncoding(new RsaBlindedEngine(), GetHashObject(_hashFunc));
                    try
                    {
                        engine.Init(true, _rsaSignParamBC);
                        rsaSignature = engine.ProcessBlock(message, 0, message.Length);
                    }
                    catch (Exception x)
                    {
                        throw new CryptoException(TextResources.RsaSigPrivInvalid, x);
                    }
                }
                else rsaSignature = null;
                if (_dsaSignParamBC != null)
                {
                    OnProgress(MausProgressState.SigningDSA);
                    try
                    {
                        DsaSigner signer = new DsaSigner(GetDsaCalc(_hashFunc));

                        dsaSignature = GenerateDsaSignature(_hashExpected, signer, _dsaSignParamBC);
                    }
                    catch (Exception x)
                    {
                        throw new CryptoException(TextResources.DsaSigPrivInvalid, x);
                    }
                }
                else dsaSignature = null;
                if (_ecdsaSignParamBC != null)
                {
                    OnProgress(MausProgressState.SigningECDSA);
                    try
                    {
                        ECDsaSigner signer = new ECDsaSigner(GetDsaCalc(_hashFunc));
                        ecdsaSignature = GenerateDsaSignature(_hashExpected, signer, _ecdsaSignParamBC);
                    }
                    catch (Exception x)
                    {
                        throw new CryptoException(TextResources.EcdsaSigPrivInvalid, x);
                    }
                }
                else ecdsaSignature = null;
            }
            else rsaSignature = dsaSignature = ecdsaSignature = _hashExpected = null;

            if (_encFmt != MausEncryptionFormat.None && _key == null)
            {
                if (_password == null) //This will only happen if there's an RSA key!
                    _key = FillBuffer(_keySize >> 3);
                else
                {
                    OnProgress(MausProgressState.BuildingKey);
                    _key = GetKey(this);
                }
            }
            byte[] rsaKey = RsaEncrypt(_key, _rsaEncParamBC, _hashFunc, false);

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

                    formats.Add(_kHash, _vHash, HashBDict[_hashFunc]);

                    if (_encFmt == MausEncryptionFormat.None)
                        WriteRsaSig(rsaSignature, dsaSignature, ecdsaSignature, formats);
                    else
                    {
                        FormatValue encValue = new FormatValue(_kEnc, _vEnc);

                        switch (_encFmt)
                        {
                            case MausEncryptionFormat.Aes:
                                encValue.Add(_kEncAes);
                                break;
                            case MausEncryptionFormat.Twofish:
                                encValue.Add(_kEncTwofish);
                                break;
                            case MausEncryptionFormat.Threefish:
                                encValue.Add(_kEncThreefish);
                                break;
                        }
                        encValue.Add((ushort)_keySize);

                        formats.Add(encValue);

                        if (rsaKey != null)
                        {
                            FormatValue encRsa = new FormatValue(_kEncRsa, _vEnc);
                            encRsa.Add(rsaKey);

                            formats.Add(encRsa);
                        }
                    }

                    formats.Write(writer);
                }

                if (_encFmt == MausEncryptionFormat.None)
                {
                    writer.Write(compressedStream.Length);
                    writer.Write(_bufferStream.Length);
                    if (_hashExpected == null)
                    {
                        _bufferStream.Reset();
                        OnProgress(MausProgressState.ComputingHash);
                        _hashExpected = ComputeHash(_bufferStream, _hashFunc);
                        OnProgress(new MausProgressEventArgs(MausProgressState.ComputingHashCompleted, _hashExpected));
                    }
                    writer.Write(_hashExpected);

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

                        formats.Add(_kULen, 1, _bufferStream.Length);

                        if (compressedStream != _bufferStream)
                        {
                            _bufferStream.Close();
                            _bufferStream = compressedStream;
                        }

                        using (BinaryWriter encWriter = new BinaryWriter(opts))
                        {
                            formats.Write(encWriter);
                            _bufferStream.Prepend(opts);
                        }
                    }

                    using (MausBufferStream output = new MausBufferStream())
                    {
                        output.Write(_salt, 0, _key.Length);
                        output.Write(_iv, 0, _encFmt == MausEncryptionFormat.Threefish ? _key.Length : _iv.Length);
                        _hmacExpected = Encrypt(this, output, _bufferStream, _key);

                        writer.Write(output.Length);
                        writer.Write((long)_pkCount);
                        writer.Write(_hmacExpected);

                        output.BufferCopyTo(_baseStream, false);
                    }
                }
            }
            OnProgress(new MausProgressEventArgs(MausProgressState.CompletedWriting, oldLength, _bufferStream.Length));
#if NOLEAVEOPEN
            _baseStream.Flush();
#endif
        }

        internal static byte[] RsaEncrypt(byte[] key, RsaKeyParameters rsaKey, MausHashFunction hashFunc, bool mauZ)
        {
            if (rsaKey == null)
                return null;

            OaepEncoding engine = new OaepEncoding(new RsaBlindedEngine(), GetHashObject(hashFunc));
            try
            {
                engine.Init(true, PublicFromPrivate(rsaKey));
                return engine.ProcessBlock(key, 0, key.Length);
            }
            catch (Exception x)
            {
                throw new CryptoException(mauZ ? TextResources.RsaEncInvalidZ : TextResources.RsaEncInvalid, x);
            }
        }

        private static byte[] GetDerEncoded(byte[] hash, MausHashFunction hashFunc)
        {
            DerObjectIdentifier derId;
            switch (hashFunc)
            {
                case MausHashFunction.Sha224:
                    derId = NistObjectIdentifiers.IdSha224;
                    break;
                case MausHashFunction.Sha256:
                    derId = NistObjectIdentifiers.IdSha256;
                    break;
                case MausHashFunction.Sha384:
                    derId = NistObjectIdentifiers.IdSha384;
                    break;
                case MausHashFunction.Sha512:
                    derId = NistObjectIdentifiers.IdSha512;
                    break;
                case MausHashFunction.Sha3_224:
                    derId = NistObjectIdentifiers.IdSha3_224;
                    break;
                case MausHashFunction.Sha3_256:
                    derId = NistObjectIdentifiers.IdSha3_256;
                    break;
                case MausHashFunction.Sha3_384:
                    derId = NistObjectIdentifiers.IdSha3_384;
                    break;
                case MausHashFunction.Sha3_512:
                    derId = NistObjectIdentifiers.IdSha3_512;
                    break;
                case MausHashFunction.Whirlpool:
                    //Taken from http://javadoc.iaik.tugraz.at/iaik_jce/current/iaik/asn1/structures/AlgorithmID.html
                    //(and verified in other places)
                    derId = new DerObjectIdentifier("1.0.10118.3.0.55");
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(hashFunc), (int)hashFunc, typeof(MausHashFunction));
            }
            AlgorithmIdentifier _id = new AlgorithmIdentifier(derId, DerNull.Instance);
            DigestInfo dInfo = new DigestInfo(_id, hash);

            return dInfo.GetDerEncoded();
        }

        private static byte[] GenerateDsaSignature(byte[] hash, IDsa signer, AsymmetricKeyParameter key)
        {
            signer.Init(true, key);

            var ints = signer.GenerateSignature(hash);

            return new DerSequence(new DerInteger(ints[0]), new DerInteger(ints[1])).GetDerEncoded();
        }

        private void WriteRsaSig(byte[] rsaSignature, byte[] dsaSignature, byte[] ecdsaSignature, ByteOptionList formats)
        {
            WriteRsaSig(rsaSignature, _rsaSignId, _kRsaSig, _vRsaSig, formats);
            WriteRsaSig(dsaSignature, _dsaSignId, _kDsaSig, _vDsaSig, formats);
            WriteRsaSig(ecdsaSignature, _ecdsaSignId, _kECDsaSig, _vECDsaSig, formats);
        }

        private static void WriteRsaSig(byte[] rsaSignature, byte[] rsaSignId, string kRsaSig, ushort vRsaSig, ByteOptionList formats)
        {
            if (rsaSignature == null)
                return;

            FormatValue formatValue = new FormatValue(kRsaSig, vRsaSig, rsaSignature);

            if (rsaSignId != null)
                formatValue.Add(rsaSignId);

            formats.Add(formatValue);
        }

        private void FormatSetFilename(ByteOptionList formats)
        {
            if (_filename != null)
                formats.Add(_kFilename, _vFilename, _textEncoding.GetBytes(_filename));
        }

        private void FormatSetCompression(ByteOptionList formats)
        {
            switch (_cmpFmt)
            {
                case MausCompressionFormat.None:
                    formats.Add(_kCmpNone, 1);
                    break;
                case MausCompressionFormat.Lzma:
                    formats.Add(_kCmpLzma, 1);
                    break;
                default:
                    formats.Add(_kCmpDef, 1);
                    break;
            }
        }

        private static void FormatSetTimes(ByteOptionList formats, string kTime, DateTime? dateTime)
        {
            if (!dateTime.HasValue)
                return;

            formats.Add(kTime, _vTime, dateTime.Value.ToUniversalTime().Ticks);
        }

        private void FormatSetComment(ByteOptionList formats)
        {
            if (_comBytes == null || _comBytes.Length == 0)
                return;

            formats.Add(_kComment, _vComment, _comBytes);
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

            OnProgress(new MausProgressEventArgs(state, inSize, outSize));
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
            /// Returns <see langword="true"/> if the underlying stream is closed or is in read-mode;
            /// <see langword="false"/> otherwise.
            /// </summary>
            /// <remarks>
            /// This property indicates that the collection cannot be changed externally. If <see cref="IsFrozen"/> is <see langword="false"/>,
            /// however, it may still be changed by the base stream.
            /// </remarks>
            public override bool IsReadOnly
            {
                get { return _stream._baseStream == null || _stream._mode == CompressionMode.Decompress; }
            }

            /// <summary>
            /// Gets a value indicating whether the current instance is entirely frozen against all further changes.
            /// Returns <see langword="true"/> if the underlying stream is closed or is in read-mode and has successfully decoded the file;
            /// <see langword="false"/> otherwise.
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
        /// No encryption.
        /// </summary>
        None,
        /// <summary>
        /// The Advanced Encryption Standard algorithm.
        /// </summary>
        Aes,
        /// <summary>
        /// The Twofish encryption algorithm.
        /// </summary>
        Twofish,
        /// <summary>
        /// The Threefish encryption algorithm.
        /// </summary>
        Threefish,
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
        /// <summary>
        /// The Whirlpool hash function.
        /// </summary>
        Whirlpool,
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
