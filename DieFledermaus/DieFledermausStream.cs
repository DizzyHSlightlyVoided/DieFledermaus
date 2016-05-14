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
using System.IO;
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
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Zlib;

using SevenZip;
using SevenZip.Compression.LZMA;

namespace DieFledermaus
{
#if PCL
    using InvalidEnumArgumentException = DieFledermaus.MausInvalidEnumException;
#else
    using System.ComponentModel;
    using System.IO.Compression;
#endif

    /// <summary>
    /// Provides methods and properties for compressing and decompressing files and streams in the DieFledermaus format.
    /// </summary>
    /// <remarks>
    /// <para>Unlike streams such as <see cref="T:System.IO.Compression.DeflateStream"/>, this class reads part of the stream during the constructor, rather than the first call
    /// to <see cref="Read(byte[], int, int)"/> or <see cref="ReadByte()"/>.</para>
    /// <para>When writing, if nothing has been written to the current stream when the current instance is disposed, nothing will be written to the
    /// underlying stream.</para>
    /// </remarks>
    public partial class DieFledermausStream : Stream, IMausProgress, IMausStream
    {
        internal const int Max16Bit = 65536;
        internal const int _head = 0x5375416d; //Little-endian "mAuS"
        private const ushort _versionShort = 101, _minVersionShort = _versionShort;

        internal static readonly UTF8Encoding _textEncoding = new UTF8Encoding(false, false);

        private Stream _baseStream;
        private MausBufferStream _bufferStream;
        private MausStreamMode _mode;
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
        /// <paramref name="compressionMode"/> is not a valid <see cref="MausStreamMode"/> value.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para><paramref name="compressionMode"/> is <see cref="MausStreamMode.Compress"/>, and <paramref name="stream"/> does not support writing.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="compressionMode"/> is <see cref="MausStreamMode.Decompress"/>, and <paramref name="stream"/> does not support reading.</para>
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
        public DieFledermausStream(Stream stream, MausStreamMode compressionMode, bool leaveOpen)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (compressionMode == MausStreamMode.Compress)
            {
                CheckStreamWrite(stream);
                _bufferStream = new MausBufferStream();
                _baseStream = stream;
            }
            else if (compressionMode == MausStreamMode.Decompress)
            {
                CheckStreamRead(stream);
                _baseStream = stream;
                if (stream.CanSeek && stream.Length == stream.Position)
                    stream.Seek(0, SeekOrigin.Begin);

                _getHeader(true);
            }
            else throw new InvalidEnumArgumentException(nameof(compressionMode), (int)compressionMode, typeof(MausStreamMode));
            _mode = compressionMode;
            _leaveOpen = leaveOpen;
        }

#if !NOCOMPMODE
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
        /// <remarks>This constructor is not available in PCL.</remarks>
        public DieFledermausStream(Stream stream, CompressionMode compressionMode, bool leaveOpen)
            : this(stream, (MausStreamMode)compressionMode, leaveOpen)
        {
        }
#endif

        /// <summary>
        /// Creates a new instance with the specified mode.
        /// </summary>
        /// <param name="stream">The stream to read to or write from.</param>
        /// <param name="compressionMode">Indicates whether the stream should be in compression or decompression mode.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// <paramref name="compressionMode"/> is not a valid <see cref="MausStreamMode"/> value.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para><paramref name="compressionMode"/> is <see cref="MausStreamMode.Compress"/>, and <paramref name="stream"/> does not support writing.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="compressionMode"/> is <see cref="MausStreamMode.Decompress"/>, and <paramref name="stream"/> does not support reading.</para>
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, MausStreamMode compressionMode)
            : this(stream, compressionMode, false)
        {
        }

#if !NOCOMPMODE
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
        /// <remarks>This constructor is not available in PCL.</remarks>
        public DieFledermausStream(Stream stream, CompressionMode compressionMode)
            : this(stream, (MausStreamMode)compressionMode, false)
        {
        }
#endif

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
                    _cmpLvl = 6;
                    _cmpFmt = MausCompressionFormat.Deflate;
                    break;
                case MausCompressionFormat.None:
                case MausCompressionFormat.Lzma:
                    _cmpFmt = compressionFormat;
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(compressionFormat), (int)compressionFormat, typeof(MausCompressionFormat));
            }
            _baseStream = stream;
            _mode = MausStreamMode.Compress;
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
            : this(stream, MausStreamMode.Compress, leaveOpen)
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
            : this(stream, MausStreamMode.Compress, false)
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
                throw new ArgumentOutOfRangeException(nameof(dictionarySize), TextResources.OutOfRangeLzma);
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

        /// <summary>
        /// Creates a new instance in write-mode using DEFLATE with the specified compression level.
        /// </summary>
        /// <param name="stream">The stream to which compressed data will be written.</param>
        /// <param name="compressionLevel">Indicates the compression level of the stream. 0 = no compression, 1 = fastest compression, 9 = optimal compression.</param>
        /// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open when the current instance is disposed;
        /// <see langword="false"/> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="compressionLevel"/> is less than 0 or is greater than 9.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, int compressionLevel, bool leaveOpen)
            : this(stream, MausCompressionFormat.Deflate, leaveOpen)
        {
            _cmpLvl = GetCompLvl(compressionLevel);
        }

#if COMPLVL
        internal static int GetCompLvl(CompressionLevel compressionLevel)
        {
            switch (compressionLevel)
            {
                case System.IO.Compression.CompressionLevel.Fastest:
                    return 1;
                case System.IO.Compression.CompressionLevel.Optimal:
                    return 9;
                case System.IO.Compression.CompressionLevel.NoCompression:
                    return 0;
                default:
                    throw new InvalidEnumArgumentException(nameof(compressionLevel), (int)compressionLevel, typeof(CompressionLevel));
            }
        }

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
        /// <paramref name="compressionLevel"/> is not a valid <see cref="System.IO.Compression.CompressionLevel"/> value.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        /// <remarks>This constructor is only available in non-PCL versions in .Net 4.5 and higher.</remarks>
        public DieFledermausStream(Stream stream, CompressionLevel compressionLevel, bool leaveOpen)
            : this(stream, GetCompLvl(compressionLevel), leaveOpen)
        {
        }
#endif

        /// <summary>
        /// Creates a new instance in write-mode using DEFLATE with the specified compression level.
        /// </summary>
        /// <param name="stream">The stream to which compressed data will be written.</param>
        /// <param name="compressionLevel">Indicates the compression level of the stream. 0 = no compression, 1 = fastest compression, 9 = optimal compression.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="compressionLevel"/> is less than 0 or is greater than 9.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermausStream(Stream stream, int compressionLevel)
            : this(stream, compressionLevel, false)
        {
        }

#if COMPLVL
        /// <summary>
        /// Creates a new instance in write-mode using DEFLATE with the specified compression level.
        /// </summary>
        /// <param name="stream">The stream to which compressed data will be written.</param>
        /// <param name="compressionLevel">Indicates the compression level of the stream.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// <paramref name="compressionLevel"/> is not a valid <see cref="System.IO.Compression.CompressionLevel"/> value.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        /// <remarks>This constructor is only available in non-PCL versions in .Net 4.5 and higher.</remarks>
        public DieFledermausStream(Stream stream, CompressionLevel compressionLevel)
            : this(stream, GetCompLvl(compressionLevel), false)
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
        /// <para><paramref name="compressionLevel"/> is not a valid <see cref="System.IO.Compression.CompressionLevel"/> value.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="encryptionFormat"/> is not a valid <see cref="MausEncryptionFormat"/> value.</para>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        /// <remarks>This constructor is only available in non-PCL versions in .Net 4.5 and higher.</remarks>
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
        /// <para><paramref name="compressionLevel"/> is not a valid <see cref="System.IO.Compression.CompressionLevel"/> value.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="encryptionFormat"/> is not a valid <see cref="MausEncryptionFormat"/> value.</para>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        /// <remarks>This constructor is only available in non-PCL versions in .Net 4.5 and higher.</remarks>
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
                    _cmpLvl = GetCompLvl(((DeflateCompressionFormat)compFormat).CompressionLevel);
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
            _mode = MausStreamMode.Compress;
            _leaveOpen = true;
        }

        internal DieFledermausStream(Stream stream, bool readMagNum, string path)
        {
            _baseStream = stream;
            _mode = MausStreamMode.Decompress;
            _leaveOpen = true;
            if (path == null)
                _allowDirNames = AllowDirNames.Unknown;
            else if (path[path.Length - 1] == '/')
                _allowDirNames = AllowDirNames.EmptyDir;
            else if (path == DieFledermauZManifest.Filename)
                _allowDirNames = AllowDirNames.Manifest;
            else
                _allowDirNames = AllowDirNames.Yes;
            _getHeader(readMagNum);
            if (_allowDirNames == AllowDirNames.Manifest && _filename == null)
                throw new InvalidDataException(TextResources.InvalidDataMaus);
        }
        #endregion

        internal DieFledermauZItem _entry;

        internal static int GetCompLvl(int compressionLevel)
        {
            if (compressionLevel < 0 || compressionLevel > 9)
                throw new ArgumentOutOfRangeException(nameof(compressionLevel), TextResources.CompressionLevel);
            return compressionLevel;
        }

        private void _setEncFormat(MausEncryptionFormat encryptionFormat)
        {
            _keySizes = _getKeySizes(encryptionFormat, out _blockByteCount);
            _encFmt = encryptionFormat;
            if (_encFmt == MausEncryptionFormat.None) return;
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

        internal void SetSignatures(RsaKeyParameters rsaKey, string rsaKeyId, DsaKeyParameters dsaKey, string dsaKeyId, ECKeyParameters ecdsaKey, string ecdsaKeyId)
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
            get { return _baseStream != null && _mode == MausStreamMode.Decompress; }
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
            get { return _baseStream != null && _mode == MausStreamMode.Compress; }
        }

        private MausEncryptionFormat _encFmt;
        /// <summary>
        /// Gets and sets the encryption format of the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current stream is in read-only mode.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// In a set operation, the specified value is not a valid <see cref="MausEncryptionFormat"/> value.
        /// </exception>
        public MausEncryptionFormat EncryptionFormat
        {
            get { return _encFmt; }
            set
            {
                _ensureCanWrite();
                _encFmt = SetEncryptionFormat(value, ref _key, ref _iv, ref _salt, ref _keySize, ref _keySizes, ref _blockByteCount, ref _password, ref _rsaEncParamBC);
            }
        }

        internal static MausEncryptionFormat SetEncryptionFormat(MausEncryptionFormat value, ref byte[] _key, ref byte[] _iv, ref byte[] _salt,
            ref int _keySize, ref KeySizeList _keySizes, ref int _blockByteCount, ref string _password, ref RsaKeyParameters _rsaEncParamBC)
        {
            switch (value)
            {
                case MausEncryptionFormat.None:
                    _key = _iv = _salt = null;
                    _keySize = 0;
                    _password = null;
                    _rsaEncParamBC = null;
                    _keySizes = null;
                    return value;
                case MausEncryptionFormat.Aes:
                case MausEncryptionFormat.Twofish:
                case MausEncryptionFormat.Threefish:
                    {
                        _keySizes = _getKeySizes(value, out _blockByteCount);
                        if (_key == null)
                            _keySize = _keySizes.MaxSize;
                        else if (!_keySizes.Contains(_key.Length << 3))
                            _key = null;

                        int maxSize = _keySizes.MaxSize;
                        if (!_keySizes.Contains(_keySize))
                            _keySize = maxSize;

                        maxSize >>= 3;

                        if (_salt == null || _salt.Length != maxSize)
                            _salt = FillBuffer(maxSize);
                        if (_iv == null || _iv.Length != _blockByteCount)
                            _iv = FillBuffer(_blockByteCount);
                    }
                    return value;
                default:
                    throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(MausEncryptionFormat));
            }
        }

        private MausCompressionFormat _cmpFmt;
        /// <summary>
        /// Gets and sets the compression format of the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current stream is in read-only mode.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// In a set operation, the specified value is not a valid <see cref="MausCompressionFormat"/> value.
        /// </exception>
        public MausCompressionFormat CompressionFormat
        {
            get { return _cmpFmt; }
            set
            {
                _ensureCanWrite();
                switch (value)
                {
                    case MausCompressionFormat.Deflate:
                        _cmpFmt = value;
                        _cmpLvl = 6;
                        break;
                    case MausCompressionFormat.None:
                    case MausCompressionFormat.Lzma:
                        _cmpFmt = value;
                        _cmpLvl = 0;
                        break;
                    default:
                        throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(MausCompressionFormat));
                }
            }
        }

        private int _cmpLvl = 6;
        /// <summary>
        /// Gets and sets the compression level of the current stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current stream is in read-only mode or is not compressed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// In a set operation, the specified value is less than 0 or is greater than 9 if <see cref="CompressionFormat"/> is <see cref="MausCompressionFormat.Deflate"/>,
        /// or is nonzero and is less than <see cref="LzmaDictionarySize.MinValue"/> or is greater than <see cref="LzmaDictionarySize.MaxValue"/> if
        /// <see cref="CompressionFormat"/> is <see cref="MausCompressionFormat.Lzma"/>.
        /// </exception>
        /// <remarks>
        /// When the current stream is compressed using DEFLATE, this value is on a scale from 0 to 9 inclusive, where 0 is uncompressed, 1 is fastest compression,
        /// and 9 is optimal compression. When the current instance is compressed using LZMA, this is the dictionary size in bytes, where 0 is the default value of
        /// <see cref="LzmaDictionarySize.Default"/>. When the current instance is in read-mode or is uncompressed, this property returns 0.
        /// </remarks>
        public int CompressionLevel
        {
            get { return _cmpLvl; }
            set
            {
                _ensureCanWrite();
                switch (_cmpFmt)
                {
                    case MausCompressionFormat.None:
                        throw new NotSupportedException(TextResources.NotCompressed);
                    case MausCompressionFormat.Lzma:
                        if (value != 0 && value < (int)LzmaDictionarySize.MinValue || value > (int)LzmaDictionarySize.MaxValue)
                            throw new ArgumentOutOfRangeException(nameof(value), TextResources.OutOfRangeLzma);
                        break;
                    default:
                        if (value < 0 || value > 9)
                            throw new ArgumentOutOfRangeException(nameof(value), string.Format(TextResources.OutOfRangeMinMax, 0, 9));
                        break;
                }
                _cmpLvl = value;
            }
        }

        /// <summary>
        /// Sets the compression level on the current stream.
        /// </summary>
        /// <param name="value">The dictionary size in bytes.</param>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current stream is in read-only mode or is not compressed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// In a set operation, <paramref name="value"/> is nonzero and is less than <see cref="LzmaDictionarySize.MinValue"/> or is greater than
        /// <see cref="LzmaDictionarySize.MaxValue"/>.
        /// </exception>
        public void SetCompressionLevel(LzmaDictionarySize value)
        {
            _ensureCanWrite();
            if (_cmpFmt != MausCompressionFormat.Lzma)
                throw new NotSupportedException(TextResources.NotLzma);
            CompressionLevel = (int)value;
        }

        private MausSavingOptions _saveCmpFmt;
        /// <summary>
        /// Gets and sets a value indicating how <see cref="CompressionFormat"/> is saved.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current stream is in read-only mode.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// In a set operation, the specified value is not a valid <see cref="MausSavingOptions"/> value.
        /// </exception>
        public MausSavingOptions CompressionFormatSaving
        {
            get { return _saveCmpFmt; }
            set
            {
                _ensureCanWrite();
                _saveCmpFmt = SetSavingOption(value);
            }
        }

        internal static MausSavingOptions SetSavingOption(MausSavingOptions value)
        {
            switch (value)
            {
                case MausSavingOptions.SecondaryOnly:
                case MausSavingOptions.Default:
                    return MausSavingOptions.SecondaryOnly;
                case MausSavingOptions.PrimaryOnly:
                case MausSavingOptions.Both:
                    return value;
                default:
                    throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(MausSavingOptions));
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current instance is in read-mode and has been successfully decrypted.
        /// </summary>
        public bool IsDecrypted { get { return _mode == MausStreamMode.Decompress && _encFmt == MausEncryptionFormat.None || _headerGotten; } }
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

        private MausSavingOptions _saveTimeC;
        /// <summary>
        /// Gets and sets options for saving <see cref="CreatedTime"/>.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current stream is in read-only mode.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// In a set operation, the specified value is not a valid <see cref="MausSavingOptions"/> value.
        /// </exception>
        public MausSavingOptions CreatedTimeSaving
        {
            get { return _saveTimeC; }
            set
            {
                _ensureCanWrite();
                _saveTimeC = SetSavingOption(value);
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

        private MausSavingOptions _saveTimeM;
        /// <summary>
        /// Gets and sets options for saving <see cref="ModifiedTime"/>.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current stream is in read-only mode.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// In a set operation, the specified value is not a valid <see cref="MausSavingOptions"/> value.
        /// </exception>
        public MausSavingOptions ModifiedTimeSaving
        {
            get { return _saveTimeM; }
            set
            {
                _ensureCanWrite();
                _saveTimeM = SetSavingOption(value);
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
        /// Gets the loaded hash code of the compressed version of the current instance and options,
        /// the HMAC of the current instance if the current instance is encrypted,
        /// or <see langword="null"/> if the current instance is in write-mode.
        /// </summary>
        public byte[] CompressedHash
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
                    throw new ArgumentOutOfRangeException(nameof(value), TextResources.KeyLength);
                _keySize = value;
            }
        }

        private void _ensureCanSetKey()
        {
            if (_baseStream == null)
                throw new ObjectDisposedException(null, TextResources.CurrentClosed);
            if (_encFmt == MausEncryptionFormat.None)
                throw new NotSupportedException(TextResources.NotEncrypted);
            if (_mode == MausStreamMode.Decompress && _headerGotten)
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
                if (_mode == MausStreamMode.Compress)
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
                CheckSignParam(value, _rsaSignature, _rsaSignVerified, _mode == MausStreamMode.Decompress);
                CheckSignParam(value, _mode == MausStreamMode.Compress);

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

        private string _rsaSignId;
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
            get { return _rsaSignId; }
            set
            {
                CheckComment(value);
                _rsaSignId = value;
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
                if (_mode == MausStreamMode.Decompress)
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
                CheckSignParam(value, _dsaSignature, _dsaSignVerified, _mode == MausStreamMode.Decompress);
                _dsaSignPub = CheckSignParam(value, _mode == MausStreamMode.Compress, _hashFunc);
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
            catch
            {
                throw new ArgumentException(TextResources.RsaNeedPublic, nameof(value));
            }
        }

        private string _dsaSignId;
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
            get { return _dsaSignId; }
            set
            {
                CheckComment(value);
                _dsaSignId = value;
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
                if (_mode == MausStreamMode.Decompress)
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
                CheckSignParam(value, _ecdsaSignature, _ecdsaSignVerified, _mode == MausStreamMode.Decompress);
                _ecdsaSignPub = CheckSignParam(value, _mode == MausStreamMode.Compress, _hashFunc);
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
            catch
            {
                throw new ArgumentException(TextResources.RsaNeedPublic, nameof(value));
            }
        }

        private string _ecdsaSignId;
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
            get { return _ecdsaSignId; }
            set
            {
                CheckComment(value);
                _ecdsaSignId = value;
            }
        }
        #endregion

        #region RSA Encrypted Key
        private byte[] _rsaEncKey;
        byte[] IMausProgress.RSAEncryptedKey
        {
            get
            {
                if (_rsaEncKey == null) return null;
                return (byte[])_rsaEncKey.Clone();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream is encrypted with an RSA key.
        /// </summary>
        /// <remarks>
        /// If the current stream is in read-mode, this property will return <see langword="true"/> if and only if the underlying stream
        /// was encrypted with an RSA key when it was written.
        /// If the current stream is in write-mode, this property will return <see langword="true"/> if <see cref="RSAEncryptParameters"/>
        /// is not <see langword="null"/>.
        /// </remarks>
        public bool IsRSAEncrypted
        {
            get
            {
                if (_mode == MausStreamMode.Compress)
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
        public RsaKeyParameters RSAEncryptParameters
        {
            get { return _rsaEncParamBC; }
            set
            {
                _ensureCanSetKey();
                if (_mode == MausStreamMode.Decompress && _rsaEncKey == null)
                    throw new NotSupportedException(TextResources.RsaEncNone);
                CheckSignParam(value, _mode == MausStreamMode.Decompress);
                _rsaEncParamBC = value;
            }
        }
        #endregion

        private string _comment;
        /// <summary>
        /// Gets and sets a comment on the file.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current stream is in read-mode.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <see langword="null"/>, and has a length which is equal to 0 or which is greater than 65536 UTF-8 bytes.
        /// </exception>
        public string Comment
        {
            get { return _comment; }
            set
            {
                _ensureCanWrite();
                CheckComment(value);
                _comment = value;
            }
        }

        private MausSavingOptions _saveComment;
        /// <summary>
        /// Gets and sets options for saving <see cref="Comment"/>.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current stream is in read-only mode.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// In a set operation, the specified value is not a valid <see cref="MausSavingOptions"/> value.
        /// </exception>
        public MausSavingOptions CommentSaving
        {
            get { return _saveComment; }
            set
            {
                _ensureCanWrite();
                _saveComment = SetSavingOption(value);
            }
        }

        internal static void CheckComment(string value)
        {
            if (value != null && (value.Length == 0 || _textEncoding.GetByteCount(value) > Max16Bit))
                throw new ArgumentException(TextResources.CommentLength, nameof(value));
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

        private MausSavingOptions _saveFilename;
        /// <summary>
        /// Gets and sets options for saving <see cref="Filename"/>.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current stream is in read-only mode.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current stream is closed.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// In a set operation, the specified value is not a valid <see cref="MausSavingOptions"/> value.
        /// </exception>
        public MausSavingOptions FilenameSaving
        {
            get { return _saveFilename; }
            set
            {
                _ensureCanWrite();
                _saveFilename = SetSavingOption(value);
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
                return value == DieFledermauZManifest.Filename;

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
            string password = o.Password;

            var keySize = o.KeySize;
            int keyLength = keySize >> 3;

            if (password == null)
                return FillBuffer(keyLength);

            var _salt = o.Salt;
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

                _hmacExpected = ReadBytes(reader, hashLength);

                if (_encFmt == MausEncryptionFormat.None)
                    _headSize += ReadFormat(reader, true);
                else
                {
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

            long headSize = fromEncrypted ? sizeof(short) : baseHeadSize;

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

            foreach (FormatEntry curValue in optionList)
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
                    }
                    else
                    {
                        _cmpFmt = cmpFmt;
                        gotFormat = true;
                    }
                    if (fromEncrypted)
                        _saveCmpFmt |= MausSavingOptions.SecondaryOnly;
                    else
                        _saveCmpFmt |= MausSavingOptions.PrimaryOnly;

                    continue;
                }

                if (curForm == _kHash)
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

                if (curForm == _kEncRsa)
                {
                    if (curValue.Count != 1 || curValue.Version != _vHash)
                        throw new NotSupportedException(TextResources.FormatUnknown);

                    if (curValue[0].TypeCode != FormatValueTypeCode.ByteArray)
                        throw new InvalidDataException(TextResources.FormatBad);

                    byte[] gotKey = curValue[0].Value;

                    if (_rsaEncKey == null)
                        _rsaEncKey = gotKey;
                    else if (!CompareBytes(gotKey, _rsaEncKey))
                        throw new InvalidDataException(TextResources.FormatBad);

                    continue;
                }

                if (curForm == _kRsaSig)
                {
                    if ((curValue.Count != 1 && curValue.Count != 2) || curValue.Version != _vRsaSig)
                        throw new NotSupportedException(TextResources.FormatUnknown);

                    if (curValue[0].TypeCode != FormatValueTypeCode.ByteArray) throw new InvalidDataException(TextResources.FormatBad);
                    byte[] rsaSig = curValue[0].Value;

                    if (_rsaSignature == null)
                        _rsaSignature = rsaSig;
                    else if (!CompareBytes(rsaSig, _rsaSignature))
                        throw new InvalidDataException(TextResources.FormatBad);

                    if (curValue.Count == 1)
                        continue;

                    string rsaId = curValue[1].ValueString;
                    if (rsaId == null) throw new InvalidDataException(TextResources.FormatBad);

                    if (_rsaSignId == null)
                        _rsaSignId = rsaId;
                    else if (_rsaSignId != rsaId)
                        throw new InvalidDataException(TextResources.FormatBad);

                    continue;
                }

                if (curForm == _kDsaSig)
                {
                    GetDsaValue(curValue, _vDsaSig, ref _dsaSignature, ref _dsaSignId);
                    continue;
                }

                if (curForm == _kECDsaSig)
                {
                    GetDsaValue(curValue, _vECDsaSig, ref _ecdsaSignature, ref _ecdsaSignId);
                    continue;
                }

                if (ReadEncFormat(curValue, ref _encFmt, ref _keySizes, ref _keySize, ref _blockByteCount, false))
                    continue;

                if (curForm == _kFilename)
                {
                    if (curValue.Count != 1 || curValue.Version != _vFilename)
                        throw new NotSupportedException(TextResources.FormatUnknown);

                    string filename = curValue[0].ValueString;

                    if (_filename == null)
                    {
                        if (!IsValidFilename(filename, false, _allowDirNames, null))
                            throw new InvalidDataException(TextResources.FormatBad);

                        _filename = filename;
                    }
                    else if (filename != _filename)
                        throw new InvalidDataException(TextResources.FormatBad);

                    if (fromEncrypted)
                        _saveFilename |= MausSavingOptions.SecondaryOnly;
                    else
                        _saveFilename |= MausSavingOptions.PrimaryOnly;

                    continue;
                }

                if (curForm == _kULen)
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

                if (curForm == _kTimeC)
                {
                    GetDate(curValue, ref _timeC, fromEncrypted, ref _saveTimeC);
                    continue;
                }

                if (curForm == _kTimeM)
                {
                    GetDate(curValue, ref _timeM, fromEncrypted, ref _saveTimeM);
                    continue;
                }

                if (curForm == _kComment)
                {
                    if (curValue.Count != 1 || curValue.Version != _vComment)
                        throw new NotSupportedException(TextResources.FormatUnknown);

                    string comment = curValue[0].ValueString;
                    if (comment == null)
                        throw new InvalidDataException(TextResources.FormatBad);

                    if (_comment == null)
                        _comment = comment;
                    else if (_comment != comment)
                        throw new InvalidDataException(TextResources.FormatBad);

                    if (fromEncrypted)
                        _saveComment |= MausSavingOptions.SecondaryOnly;
                    else
                        _saveComment |= MausSavingOptions.PrimaryOnly;

                    continue;
                }

                throw new NotSupportedException(TextResources.FormatUnknown);
            }

            if (fromEncrypted)
            {
                _hashExpected = ReadBytes(reader, GetHashLength(_hashFunc));
                headSize += _hashExpected.Length;
            }

            return headSize;
        }

        internal static bool ReadEncFormat(FormatEntry curValue, ref MausEncryptionFormat _encFmt, ref KeySizeList _keySizes,
            ref int _keySize, ref int _blockByteCount, bool mauZ)
        {
            if (curValue.Key != _kEnc)
                return false;
            MausEncryptionFormat getEncFormat;

            if (curValue.Count != 2)
                throw new NotSupportedException(mauZ ? TextResources.FormatUnknownZ : TextResources.FormatUnknown);

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

        private static void GetDsaValue(FormatEntry formatValue, ushort vDsa, ref DerIntegerPair existing, ref string keyId)
        {
            if ((formatValue.Count != 1 && formatValue.Count != 2) || formatValue.Version != vDsa)
                throw new NotSupportedException(TextResources.FormatUnknown);

            DerSequence seq = formatValue[0].GetDerObject() as DerSequence;
            if (seq == null || seq.Count != 2 || !seq.Cast<Asn1Encodable>().All(i => i is DerInteger))
                throw new InvalidDataException(TextResources.FormatBad);

            DerIntegerPair newVal = new DerIntegerPair((DerInteger)seq[0], (DerInteger)seq[1]);

            if (existing == null)
                existing = newVal;
            else if (!existing.R.Equals(newVal.R) || !existing.S.Equals(newVal.S))
                throw new InvalidDataException(TextResources.FormatBad);

            if (formatValue.Count == 2)
            {
                string newId = formatValue[1].ValueString;
                if (newId == null)
                    throw new InvalidDataException(TextResources.FormatBad);

                if (keyId == null)
                    keyId = newId;
                else if (newId != keyId)
                    throw new InvalidDataException(TextResources.FormatBad);
            }
        }

        internal static byte[] ReadBytes(BinaryReader reader, int size)
        {
            byte[] data = reader.ReadBytes(size);
            if (data.Length < size)
                throw new EndOfStreamException();
            return data;
        }

        internal static byte[] ReadBytes8Bit(BinaryReader reader)
        {
            int len = reader.ReadByte();
            if (len == 0) len = Max8Bit;
            return ReadBytes(reader, len);
        }

        internal static byte[] ReadBytes16Bit(BinaryReader reader)
        {
            int len = reader.ReadInt16();
            if (len == 0) len = Max16Bit;
            return ReadBytes(reader, len);
        }

        private static readonly long maxTicks = DateTime.MaxValue.Ticks;

        private void GetDate(FormatEntry curValue, ref DateTime? curTime, bool fromEncrypted, ref MausSavingOptions savingOption)
        {
            if (curValue.Count != 1 || curValue.Version != _vTime)
                throw new NotSupportedException(TextResources.FormatUnknown);

            DateTime? value = curValue[0].ValueDateTime;

            if (!value.HasValue)
                throw new InvalidDataException(TextResources.FormatBad);

            if (curTime.HasValue)
            {
                if (curTime.Value != value.Value)
                    throw new InvalidDataException(TextResources.FormatBad);
            }
            else curTime = value;

            if (fromEncrypted)
                savingOption |= MausSavingOptions.SecondaryOnly;
            else
                savingOption |= MausSavingOptions.PrimaryOnly;
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
                    throw new ArgumentOutOfRangeException(nameof(value), string.Format(TextResources.OutOfRangeMinMax, 0, maxPkCount));
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

            if (_mode == MausStreamMode.Compress)
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

            GetBuffer();

            if (_encFmt != MausEncryptionFormat.None)
            {
                using (MausBufferStream bufferStream = Decrypt(this, _bufferStream, false))
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
                        throw new InvalidDataException(TextResources.InvalidDataMaus);
                    try
                    {
                        decoder.Code(_bufferStream, lzmaStream, _bufferStream.Length - optLen, -1, this);
                    }
                    catch (DataErrorException)
                    {
                        throw new InvalidDataException(TextResources.InvalidDataMaus);
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
                    using (ZInputStream _deflateStream = new ZInputStream(_bufferStream, true))
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

            OnProgress(MausProgressState.VerifyingHash);
            byte[] hashActual = ComputeHash(_bufferStream, _hashFunc);

            if (!CompareBytes(hashActual, _hashExpected))
                throw new InvalidDataException(TextResources.BadChecksum);

            if (_encFmt != MausEncryptionFormat.None)
                OnProgress(new MausProgressEventArgs(MausProgressState.ComputingHashCompleted, hashActual));
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
                if (CompareBytes(_hmacExpected, sig))
                    return _rsaSignVerified = true;

                byte[] expected = GetDerEncoded(_hmacExpected, _hashFunc);

                if (CompareBytes(expected, sig))
                    return _rsaSignVerified = true;

                if (sig.Length != expected.Length - 2)
                    return false;

                int sigOffset = sig.Length - _hmacExpected.Length - 2;
                int expectedOffset = expected.Length - _hmacExpected.Length - 2;

                expected[1] -= 2;      // adjust lengths
                expected[3] -= 2;

                for (int i = 0; i < _hmacExpected.Length; i++)
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

                return _dsaSignVerified = VerifyDsaSignature(_hmacExpected, _dsaSignature, signer, _dsaSignPub);
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

                return _ecdsaSignVerified = VerifyDsaSignature(_hmacExpected, _ecdsaSignature, signer, _ecdsaSignPub);
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
            if ((hashComputed == null) != (_hashExpected == null))
                return false;

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
                throw new ArgumentOutOfRangeException(nameof(offset), TextResources.OutOfRangeLessThanZero);
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), TextResources.OutOfRangeLessThanZero);
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
            if (_mode == MausStreamMode.Compress) throw new NotSupportedException(TextResources.CurrentWrite);
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
            if (_mode == MausStreamMode.Decompress) throw new NotSupportedException(TextResources.CurrentRead);
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
            SecureRandom rng = new SecureRandom();
            rng.NextBytes(buffer);
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

        internal static MausBufferStream Decrypt(IMausProgress o, MausBufferStream bufferStream, bool mauZ)
        {
            byte[] key = o.Key;
            string password = o.Password;

            bool noKey = false, hasRsa = false;
            if (key == null)
            {
                noKey = true;
                if (password == null)
                {
                    byte[] rsaEncKey = o.RSAEncryptedKey;

                    if (rsaEncKey == null)
                        throw new CryptoException(mauZ ? TextResources.KeyNotSetZ : TextResources.KeyNotSet);

                    RsaKeyParameters rsaKeyParam = o.RSAEncryptParameters;
                    if (rsaKeyParam == null)
                        throw new CryptoException(mauZ ? TextResources.KeyRsaNotSetZ : TextResources.KeyRsaNotSet);

                    try
                    {
                        OaepEncoding engine = new OaepEncoding(new RsaBlindedEngine(), GetHashObject(o.HashFunction));
                        engine.Init(false, rsaKeyParam);
                        key = engine.ProcessBlock(rsaEncKey, 0, rsaEncKey.Length);
                    }
                    catch (Exception x)
                    {
                        throw new CryptoException(mauZ ? TextResources.BadRsaKeyZ : TextResources.BadRsaKey, x);
                    }
                    hasRsa = true;
                }
                else
                {
                    o.OnProgress(MausProgressState.BuildingKey);
                    key = GetKey(o);
                }
            }

            o.OnProgress(MausProgressState.Decrypting);

            MausBufferStream output = new MausBufferStream();

            bool success = RunCipher(key, o, bufferStream, output, false);

            output.Reset();

            o.OnProgress(MausProgressState.VerifyingHMAC);

            byte[] actualHmac = ComputeHmac(output, key, o.HashFunction);
            if (!success || !CompareBytes(actualHmac, o.CompressedHash))
                throw new InvalidCipherTextException(hasRsa ? TextResources.BadKeyRsa : TextResources.BadKey);

            if (noKey)
                o.Key = key;

            return output;
        }

        internal static byte[] Encrypt(IMausProgress o, MausBufferStream output, MausBufferStream bufferStream)
        {
            byte[] key = o.Key;
            if (key == null)
                o.Key = key = GetKey(o);
            {
                output.Write(o.Salt, 0, key.Length);
                byte[] iv = o.IV;
                output.Write(iv, 0, o.EncryptionFormat == MausEncryptionFormat.Threefish ? key.Length : iv.Length);
            }
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

        private void AddOptions(ByteOptionList formats, MausSavingOptions savingOption)
        {
            if (_filename != null && (savingOption & _saveFilename) != 0)
                formats.Add(_kFilename, _vFilename, _filename);

            if ((savingOption & _saveCmpFmt) != 0)
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

            if ((savingOption & _saveTimeC) != 0 && _timeC.HasValue)
                formats.Add(_kTimeC, _vTime, _timeC.Value);

            if ((savingOption & _saveTimeM) != 0 && _timeM.HasValue)
                formats.Add(_kTimeM, _vTime, _timeM.Value);
        }

        private void WriteFile()
        {
            if (_entry != null && _entry.Archive == null)
                return;
            if (_bufferStream == null || _mode != MausStreamMode.Compress || _bufferStream.Length == 0)
                return;
            if (_encFmt != MausEncryptionFormat.None && _password == null && _key == null && _rsaEncParamBC == null)
                throw new InvalidOperationException(TextResources.KeyRsaNotSet);

            _bufferStream.Reset();
            OnProgress(MausProgressState.ComputingHash);
            _hashExpected = ComputeHash(_bufferStream, _hashFunc);
            OnProgress(new MausProgressEventArgs(MausProgressState.ComputingHashCompleted, _hashExpected));
            _bufferStream.Reset();

            MausBufferStream secondaryStream = new MausBufferStream();

            #region Secondary Format
#if NOLEAVEOPEN
            BinaryWriter secWriter = new BinaryWriter(secondaryStream, _textEncoding);
#else
            using (BinaryWriter secWriter = new BinaryWriter(secondaryStream, _textEncoding, true))
#endif
            {
                _saveCmpFmt = SetSavingOption(_saveCmpFmt);
                _saveComment = SetSavingOption(_saveCmpFmt);
                _saveFilename = SetSavingOption(_saveFilename);
                _saveTimeC = SetSavingOption(_saveTimeC);
                _saveTimeM = SetSavingOption(_saveTimeM);

                ByteOptionList secondaryFormat = new ByteOptionList();
                AddOptions(secondaryFormat, MausSavingOptions.SecondaryOnly);

                if (_encFmt != MausEncryptionFormat.None)
                    secondaryFormat.Add(_kULen, 1, _bufferStream.Length);

                secondaryFormat.Write(secWriter);
            }
            #endregion

            secondaryStream.Write(_hashExpected, 0, _hashExpected.Length);

            #region Compress
            MausBufferStream compressedStream = new MausBufferStream();
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

                using (ZOutputStream ds = new ZOutputStream(compressedStream, _cmpLvl, true))
                    _bufferStream.BufferCopyTo(ds, false);
            }
            compressedStream.Reset();
            #endregion

            long oldLength = _bufferStream.Length;
            long compLength = compressedStream.Length;

            compressedStream.BufferCopyTo(secondaryStream, false);
            compressedStream.Dispose();
            compressedStream = secondaryStream;

            if (_encFmt == MausEncryptionFormat.None)
                _hmacExpected = ComputeHash(compressedStream, _hashFunc);
            else
            {
                MausBufferStream encryptionStream = new MausBufferStream();
                _hmacExpected = Encrypt(this, encryptionStream, compressedStream);
                compressedStream.Dispose();
                compressedStream = encryptionStream;
            }
            _bufferStream.Dispose();
            _bufferStream = compressedStream;
            _bufferStream.Reset();

            byte[] rsaKey = RsaEncrypt(_key, _rsaEncParamBC, _hashFunc, false);

            #region Signatures
            byte[] rsaSignature;
            DerSequence dsaSignature, ecdsaSignature;

            if (_rsaSignParamBC != null || _dsaSignParamBC != null || _ecdsaSignParamBC != null)
            {
                if (_rsaSignParamBC != null)
                {
                    OnProgress(MausProgressState.SigningRSA);
                    byte[] message = GetDerEncoded(_hmacExpected, _hashFunc);
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

                        dsaSignature = GenerateDsaSignature(_hmacExpected, signer, _dsaSignParamBC);
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
                        ecdsaSignature = GenerateDsaSignature(_hmacExpected, signer, _ecdsaSignParamBC);
                    }
                    catch (Exception x)
                    {
                        throw new CryptoException(TextResources.EcdsaSigPrivInvalid, x);
                    }
                }
                else ecdsaSignature = null;
            }
            else
            {
                rsaSignature = _hashExpected = null;
                dsaSignature = ecdsaSignature = null;
            }
            #endregion

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

                    AddOptions(formats, MausSavingOptions.PrimaryOnly);

                    formats.Add(_kHash, _vHash, HashBDict[_hashFunc]);

                    if (_encFmt != MausEncryptionFormat.None)
                    {
                        WriteEncFormat(_encFmt, _keySize, rsaKey, formats);

                        if (rsaKey != null)
                            formats.Add(_kEncRsa, _vEnc, rsaKey);
                    }

                    WriteRsaSig(rsaSignature, dsaSignature, ecdsaSignature, formats);

                    formats.Write(writer);
                }

                if (_encFmt == MausEncryptionFormat.None)
                {
                    writer.Write(compLength);
                    writer.Write(oldLength);
                }
                else
                {
                    writer.Write(compressedStream.Length);
                    writer.Write((long)_pkCount);
                }
                _baseStream.Write(_hmacExpected, 0, _hmacExpected.Length);

                compressedStream.BufferCopyTo(_baseStream, false);
            }
#if NOLEAVEOPEN
            _baseStream.Flush();
#endif
            OnProgress(new MausProgressEventArgs(MausProgressState.CompletedWriting, oldLength, compLength));
        }

        internal static void WriteEncFormat(MausEncryptionFormat _encFmt, int _keySize, byte[] rsaKey, ByteOptionList formats)
        {
            FormatEntry encValue = new FormatEntry(_kEnc, _vEnc);

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
                FormatEntry encRsa = new FormatEntry(_kEncRsa, _vEnc);
                encRsa.Add(rsaKey);

                formats.Add(encRsa);
            }
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
                    derId = _whirlpoolOID;
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(hashFunc), (int)hashFunc, typeof(MausHashFunction));
            }
            AlgorithmIdentifier _id = new AlgorithmIdentifier(derId, DerNull.Instance);
            DigestInfo dInfo = new DigestInfo(_id, hash);

            return dInfo.GetDerEncoded();
        }

        //Taken from http://javadoc.iaik.tugraz.at/iaik_jce/current/iaik/asn1/structures/AlgorithmID.html
        //(and verified in other places)
        private static readonly DerObjectIdentifier _whirlpoolOID = new DerObjectIdentifier("1.0.10118.3.0.55");

        private static DerSequence GenerateDsaSignature(byte[] hash, IDsa signer, AsymmetricKeyParameter key)
        {
            signer.Init(true, key);

            var ints = signer.GenerateSignature(hash);

            return new DerSequence(new DerInteger(ints[0]), new DerInteger(ints[1]));
        }

        private void WriteRsaSig(byte[] rsaSignature, DerSequence dsaSignature, DerSequence ecdsaSignature, ByteOptionList formats)
        {
            if (rsaSignature != null)
            {
                FormatEntry formatValue = new FormatEntry(_kRsaSig, _vRsaSig, rsaSignature);

                if (_rsaSignId != null) formatValue.Add(_rsaSignId);

                formats.Add(formatValue);
            }
            WriteRsaSig(dsaSignature, _dsaSignId, _kDsaSig, _vDsaSig, formats);
            WriteRsaSig(ecdsaSignature, _ecdsaSignId, _kECDsaSig, _vECDsaSig, formats);
        }

        private static void WriteRsaSig(DerSequence rsaSignature, string rsaSignId, string kRsaSig, ushort vRsaSig, ByteOptionList formats)
        {
            if (rsaSignature == null)
                return;

            FormatEntry formatValue = new FormatEntry(kRsaSig, vRsaSig, rsaSignature);

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

        internal static void FormatSetComment(string comment, ByteOptionList formats)
        {
            if (comment == null || comment.Length == 0)
                return;

            formats.Add(_kComment, _vComment, comment);
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
            if (_mode == MausStreamMode.Compress)
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

        internal const string CollectionDebuggerDisplay = "Count = {Count}";
    }

    /// <summary>
    /// Indicates whether the stream is in read or write mode.
    /// </summary>
    public enum MausStreamMode
    {
        /// <summary>
        /// The stream is in write-mode.
        /// </summary>
        Compress = 1,
        /// <summary>
        /// The stream is in read-mode.
        /// </summary>
        Decompress = 0,
    }
}
