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
using System.Security.Cryptography;

using DieFledermaus.Globalization;
using Org.BouncyCastle.Crypto.Parameters;

namespace DieFledermaus
{
    /// <summary>
    /// Represents a single entry in a <see cref="DieFledermauZArchive"/>.
    /// </summary>
    public abstract class DieFledermauZItem : IMausCrypt
    {
        internal DieFledermauZItem(DieFledermauZArchive archive, string path, ICompressionFormat compFormat, MausEncryptionFormat encFormat)
        {
            _bufferStream = new MausBufferStream();
            _arch = archive;
            MausStream = new DieFledermausStream(this, path, _bufferStream, compFormat ?? new DeflateCompressionFormat(), encFormat);
            MausStream.Progress += MausStream_Progress;
        }

        internal DieFledermauZItem(DieFledermauZArchive archive, string path, DieFledermausStream stream, long curOffset, long realOffset)
        {
            _arch = archive;
            MausStream = stream;
            Offset = curOffset;
            RealOffset = realOffset;
            MausStream._entry = this;
            if (path != null)
                _path = path;
            MausStream.Progress += MausStream_Progress;
        }

        /// <summary>
        /// Raised when the current instance is reading or writing data, and the progress changes meaningfully.
        /// </summary>
        public event MausProgressEventHandler Progress;

        private void MausStream_Progress(object sender, MausProgressEventArgs e)
        {
            if (Progress != null)
                Progress(this, e);
        }

        internal readonly long Offset, RealOffset;

        internal long HeadLength { get { return MausStream.HeadLength; } }

        internal readonly object _lock = new object();

        /// <summary>
        /// Gets the hash of the uncompressed data, or <c>null</c> if <see cref="Archive"/> is in write-mode or
        /// the current instance has not yet been decrypted.
        /// </summary>
        public byte[] Hash { get { return MausStream.Hash; } }

        /// <summary>
        /// Gets the loaded HMAC of the encrypted data, or <c>null</c> if <see cref="Archive"/> is in write-mode or
        /// the current instance is not encrypted.
        /// </summary>
        public byte[] HMAC { get { return MausStream.HMAC; } }

        internal DieFledermauZArchive _arch;
        /// <summary>
        /// Gets the <see cref="DieFledermauZArchive"/> containing the current instance, or <c>null</c> if
        /// the current instance has been deleted.
        /// </summary>
        public DieFledermauZArchive Archive { get { return _arch; } }

        /// <summary>
        /// Gets the number of bits in a single block of data, or 0 if the current instance is not encrypted.
        /// </summary>
        public int BlockSize { get { return MausStream.BlockSize; } }

        /// <summary>
        /// Gets the number of bytes in a single block of data, or 0 if the current instance is not encrypted.
        /// </summary>
        public int BlockByteCount { get { return MausStream.BlockByteCount; } }

        /// <summary>
        /// Gets and sets the hash function used by the current instance. The default is <see cref="MausHashFunction.Sha256"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, <see cref="Archive"/> is in read-mode.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// The specified value is not a valid <see cref="MausHashFunction"/> value.
        /// </exception>
        public MausHashFunction HashFunction
        {
            get { return MausStream.HashFunction; }
            set
            {
                EnsureCanWrite();
                MausStream.HashFunction = value;
            }
        }

        /// <summary>
        /// Gets and sets the number of bits in the key.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para>In a set operation, <see cref="Archive"/> is in read-only mode.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current instance is not encrypted.</para>
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// In a set operation, the specified value is invalid according to <see cref="KeySizes"/>.
        /// </exception>
        public int KeySize
        {
            get { return MausStream.KeySize; }
            set
            {
                EnsureCanWrite();
                MausStream.KeySize = value;
            }
        }

        /// <summary>
        /// Gets and sets a comment on the file.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance has been deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-only mode.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <c>null</c>, and has a length which is equal to 0 or which is greater than 65536 UTF-8 bytes.
        /// </exception>
        public string Comment
        {
            get { return MausStream.Comment; }
            set
            {
                EnsureCanWrite();
                MausStream.Comment = value;
            }
        }

        /// <summary>
        /// Gets and sets a binary representation of a comment on the file.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance has been deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-only mode.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <c>null</c>, and has a length which is equal to 0 or which is greater than 65536.
        /// </exception>
        public byte[] CommentBytes
        {
            get { return MausStream.CommentBytes; }
            set
            {
                EnsureCanWrite();
                MausStream.CommentBytes = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current instance has an RSA-encrypted key.
        /// </summary>
        /// <remarks>
        /// If <see cref="Archive"/> is in read-mode, this property returns <c>true</c> if and only if the original archive entry
        /// had an RSA-encrypted key when it was written. If <see cref="Archive"/> is in write-mode, this property
        /// returns <c>true</c> if <see cref="RSAKeyParameters"/> is not <c>null</c>.
        /// </remarks>
        public bool HasRSAEncryptedKey { get { return MausStream.HasRSAEncryptedKey; } }

        /// <summary>
        /// Gets and sets an RSA key used to encrypt or decrypt the key of the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para>The current instance is not encrypted.</para>
        /// <para>-OR-</para>
        /// <para>The <see cref="Archive"/> is in read-mode, and the current instance does not have an RSA-encrypted key.</para>
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="Archive"/> is in read-mode and the current instance has already been successfully decrypted.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>In a set operation, <see cref="Archive"/> is in write-mode, and the specified value is not a valid public key.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, <see cref="Archive"/> is in read-mode, and the specified value is not a valid private key.</para>
        /// </exception>
        public virtual RsaKeyParameters RSAKeyParameters
        {
            get { return MausStream.RSAKeyParameters; }
            set
            {
                _ensureCanSetKey();
                MausStream.RSAKeyParameters = value;
            }
        }

        private string _path;
        /// <summary>
        /// Gets the path of the current instance within the archive.
        /// </summary>
        public string Path
        {
            get
            {
                if (_path == null)
                    return MausStream.Filename;
                return _path;
            }
        }

        internal abstract bool IsFilenameEncrypted { get; }

        internal bool _isDecrypted;
        /// <summary>
        /// Gets a value indicating whether the current instance is in read-mode and has been successfully decrypted.
        /// </summary>
        public bool IsDecrypted { get { return _isDecrypted; } }

        /// <summary>
        /// Decrypts the current instance.
        /// </summary>
        /// <returns>The current instance.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current instance has been deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="Archive"/> is in write-only mode.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current instance is not encrypted.
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// The stream contains invalid data.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// The password is not correct. It is safe to attempt to call <see cref="Decrypt()"/>
        /// again if this exception is caught.
        /// </exception>
        public virtual DieFledermauZItem Decrypt()
        {
            EnsureCanRead();
            if (MausStream.EncryptionFormat == MausEncryptionFormat.None)
                throw new InvalidOperationException(TextResources.NotEncrypted);
            if (MausStream.IsDecrypted) return this;
            SeekToFile();

            if (MausStream.Filename == null)
            {
                if (Offset != 0)
                    throw new InvalidDataException(TextResources.InvalidDataMaus);
            }
            else if (_path == null)
            {
                _path = MausStream.Filename;
                _arch.AddPath(_path, this);
            }
            else if (!_path.Equals(MausStream.Filename, StringComparison.Ordinal))
                throw new InvalidDataException(TextResources.InvalidDataMaus);

            return this;
        }

        /// <summary>
        /// Determines whether the specified value is a valid length for a key, in bits.
        /// </summary>
        /// <param name="bitCount">The number of bits to test.</param>
        /// <returns><c>true</c> if <paramref name="bitCount"/> is a valid bit count according to <see cref="KeySizes"/>;
        /// <c>false</c> if <paramref name="bitCount"/> is invalid, or if the current instance is not encrypted.</returns>
        public bool IsValidKeyBitSize(int bitCount)
        {
            return MausStream.IsValidKeyBitSize(bitCount);
        }

        /// <summary>
        /// Determines whether the specified value is a valid length for a key, in bytes.
        /// </summary>
        /// <param name="byteCount">The number of bytes to test.</param>
        /// <returns><c>true</c> if <paramref name="byteCount"/> is a valid bit count according to <see cref="KeySizes"/>;
        /// <c>false</c> if <paramref name="byteCount"/> is invalid, or if the current instance is not encrypted.</returns>
        public bool IsValidKeyByteSize(int byteCount)
        {
            return MausStream.IsValidKeyByteSize(byteCount);
        }

        void IMausCrypt.Decrypt()
        {
            Decrypt();
        }

        internal void SeekToFile()
        {
            if (_arch.BaseStream.CanSeek && !MausStream.DataIsLoaded)
                _arch.BaseStream.Seek(RealOffset + _arch.StreamOffset + MausStream.HeadLength, SeekOrigin.Begin);
            MausStream.LoadData();
        }

        /// <summary>
        /// Gets the encryption format of the current instance.
        /// </summary>
        public MausEncryptionFormat EncryptionFormat { get { return MausStream.EncryptionFormat; } }

        /// <summary>
        /// Gets and sets the initialization vector used for the current instance, or <c>null</c> if the current instance is not encrypted.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance has been deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para>The current instance is not encrypted.</para>
        /// <para>-OR-</para>
        /// <para>The current instance is in read-mode.</para>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// In a set operation, the specified value is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is the wrong length.
        /// </exception>
        public byte[] IV
        {
            get { return MausStream.IV; }
            set
            {
                EnsureCanWrite();
                MausStream.IV = value;
            }
        }

        /// <summary>
        /// Gets and sets the salt used for the current instance, or <c>null</c> if the current instance is not encrypted.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance has been deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para>The current instance is not encrypted.</para>
        /// <para>-OR-</para>
        /// <para>The current instance is in read-mode.</para>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// In a set operation, the specified value is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is the wrong length.
        /// </exception>
        public byte[] Salt
        {
            get { return MausStream.Salt; }
            set
            {
                EnsureCanWrite();
                MausStream.Salt = value;
            }
        }

        /// <summary>
        /// Gets a <see cref="System.Security.Cryptography.KeySizes"/> object indicating all valid key sizes
        /// for <see cref="EncryptionFormat"/>, or <c>null</c> if the current entry is not encrypted.
        /// </summary>
        public KeySizes KeySizes { get { return MausStream.KeySizes; } }

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
            get { return MausStream.Password; }
            set
            {
                _ensureCanSetKey();
                MausStream.Password = value;
            }
        }

        private void _ensureCanSetKey()
        {
            if (_arch == null) throw new ObjectDisposedException(null, TextResources.ArchiveEntryDeleted);
            if (MausStream.EncryptionFormat == MausEncryptionFormat.None)
                throw new NotSupportedException(TextResources.NotEncrypted);
            if (_arch.Mode == MauZArchiveMode.Read && MausStream.IsDecrypted)
                throw new InvalidOperationException(TextResources.AlreadyDecryptedArchive);
        }

        internal void EnsureCanWrite()
        {
            if (_arch == null) throw new ObjectDisposedException(null, TextResources.ArchiveEntryDeleted);
            _arch.EnsureCanWrite();
        }

        internal void EnsureCanRead()
        {
            if (_arch == null) throw new ObjectDisposedException(null, TextResources.ArchiveEntryDeleted);
            _arch.EnsureCanRead();
        }

        internal readonly MausBufferStream _bufferStream;
        internal DieFledermausStream MausStream;

        internal virtual MausBufferStream GetWritten()
        {
            MausStream.Dispose();
            return _bufferStream;
        }

        /// <summary>
        /// Deletes the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance has already been deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="Archive"/> is in read-only mode.
        /// </exception>
        public void Delete()
        {
            lock (_lock)
            {
                EnsureCanWrite();
                _arch.Delete(this);
                DoDelete(true);
            }
        }

        internal virtual void DoDelete(bool deleteMaus)
        {
            _arch = null;
            if (deleteMaus && MausStream != null)
                MausStream.Dispose();
            if (_bufferStream != null)
                _bufferStream.Dispose();
            Progress = null;
        }

        /// <summary>
        /// Returns a string representation of the current instance.
        /// </summary>
        /// <returns>A string representation of the current instance.</returns>
        public override string ToString()
        {
            return Path;
        }
    }

    internal interface ICompressionFormat
    {
        MausCompressionFormat CompressionFormat { get; }
    }

    internal struct DeflateCompressionFormat : ICompressionFormat
    {
        public MausCompressionFormat CompressionFormat { get { return MausCompressionFormat.Deflate; } }
#if COMPLVL
        public System.IO.Compression.CompressionLevel CompressionLevel;
#endif
    }

    internal struct LzmaCompressionFormat : ICompressionFormat
    {
        public MausCompressionFormat CompressionFormat { get { return MausCompressionFormat.Lzma; } }

        public LzmaDictionarySize DictionarySize;
    }

    internal struct NoneCompressionFormat : ICompressionFormat
    {
        public MausCompressionFormat CompressionFormat { get { return MausCompressionFormat.None; } }
    }
}
