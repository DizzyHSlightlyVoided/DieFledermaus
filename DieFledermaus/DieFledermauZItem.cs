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

using DieFledermaus.Globalization;
using Org.BouncyCastle.Crypto;
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
            _path = path;
            MausStream = new DieFledermausStream(this, path, _bufferStream, compFormat ?? new DeflateCompressionFormat(), encFormat);
            MausStream.Progress += MausStream_Progress;
        }

        internal DieFledermauZItem(DieFledermauZArchive archive, string path, string originalPath, DieFledermausStream stream, long curOffset, long realOffset)
        {
            _arch = archive;
            MausStream = stream;
            Offset = curOffset;
            RealOffset = realOffset;
            MausStream._entry = this;
            _path = path;
            OriginalPath = originalPath;
            MausStream.Progress += MausStream_Progress;
            if (MausStream.EncryptionFormat == MausEncryptionFormat.None)
                _isDecrypted = true;
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

        internal readonly string OriginalPath;

        internal readonly long Offset, RealOffset;

        internal long HeadLength { get { return MausStream.HeadLength; } }

        internal readonly object _lock = new object();

        /// <summary>
        /// Gets the hash of the uncompressed data, or <see langword="null"/> if <see cref="Archive"/> is in write-mode or
        /// the current instance has not yet been decrypted.
        /// </summary>
        public byte[] Hash { get { return MausStream.Hash; } }

        /// <summary>
        /// Gets the loaded hash code of the compressed version of the current instance and options,
        /// the HMAC of the current instance if the current instance is encrypted,
        /// or <see langword="null"/> if <see cref="Archive"/> is in write-mode.
        /// </summary>
        public byte[] CompressedHash { get { return MausStream.CompressedHash; } }

        /// <summary>
        /// Gets and sets the number of PBKDF2 cycles used to generate the password, minus 9001.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para>In a set operation, the current instance is not encrypted.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current instance is in read-only mode.</para>
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// In a set operation, the specified value is less than 0 or is greater than <see cref="int.MaxValue"/> minus 9001.
        /// </exception>
        public int PBKDF2CycleCount
        {
            get { return MausStream.PBKDF2CycleCount; }
            set
            {
                EnsureCanWrite();
                MausStream.PBKDF2CycleCount = value;
            }
        }

        internal DieFledermauZArchive _arch;
        /// <summary>
        /// Gets the <see cref="DieFledermauZArchive"/> containing the current instance, or <see langword="null"/> if
        /// the current instance has been deleted.
        /// </summary>
        public DieFledermauZArchive Archive { get { return _arch; } }

        /// <summary>
        /// Gets the maximum number of bits in a single block of data, or 0 if the current instance is not encrypted.
        /// </summary>
        public int BlockSize { get { return MausStream.BlockSize; } }

        /// <summary>
        /// Gets the maximum number of bytes in a single block of data, or 0 if the current instance is not encrypted.
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
        /// <para>-OR-</para>
        /// <para>In a set operation, <see cref="Key"/> is not <see langword="null"/> and the specified value is not the proper length.</para>
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// In a set operation, the specified value is invalid according to <see cref="LegalKeySizes"/>.
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
        /// In a set operation, the specified value is not <see langword="null"/>, and has a length which is equal to 0 or which is greater than 65536 UTF-8 bytes.
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
        /// In a set operation, the specified value is not <see langword="null"/>, and has a length which is equal to 0 or which is greater than 65536.
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
        /// Also returns <see langword="true"/> if the current instance is not encrypted.
        /// </summary>
        public bool IsDecrypted { get { return _isDecrypted; } }

        /// <summary>
        /// Sets <see cref="Key"/> to a value derived from <see cref="Password"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current instance is not encrypted.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// <para>In a set operation, <see cref="Archive"/> is in read-mode and has already been successfully decrypted.</para>
        /// <para>-OR-</para>
        /// <para><see cref="Password"/> is <see langword="null"/>.</para>
        /// </exception>
        public void DeriveKey()
        {
            _ensureCanSetKey();
            MausStream.DeriveKey();
        }

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
        /// <exception cref="CryptoException">
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
        /// Gets and sets a binary key used to encrypt or decrypt the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current instance is not encrypted.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, the <see cref="Archive"/> is in read-mode and the current instance has already been successfully decrypted.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value has an invalid length according to <see cref="LegalKeySizes"/>.
        /// </exception>
        public byte[] Key
        {
            get { return MausStream.Key; }
            set
            {
                _ensureCanSetKey();
                MausStream.Key = value;
            }
        }

        /// <summary>
        /// Gets and sets the initialization vector used for the current instance, or <see langword="null"/> if the current instance is not encrypted.
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
        /// In a set operation, the specified value is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the length of the specified value is not equal to <see cref="BlockByteCount"/>.
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
        /// Gets and sets the salt used for the current instance, or <see langword="null"/> if the current instance is not encrypted.
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
        /// In a set operation, the specified value is <see langword="null"/>.
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
        /// Gets a <see cref="KeySizeList"/> object indicating all valid key sizes
        /// for <see cref="EncryptionFormat"/>, or <see langword="null"/> if the current entry is not encrypted.
        /// </summary>
        public KeySizeList LegalKeySizes { get { return MausStream.LegalKeySizes; } }

        /// <summary>
        /// Gets and sets the password used by the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current instance is not encrypted.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="Archive"/> is in read-mode and the current instance has already been successfully decrypted.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <see langword="null"/> and has a length of 0.
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

        #region RSA Encryption
        /// <summary>
        /// Gets a value indicating whether the current instance is encrypted with an RSA key.
        /// </summary>
        /// <remarks>
        /// If <see cref="Archive"/> is in read-mode, this property will return <see langword="true"/> if and only if the underlying stream
        /// was encrypted with an RSA key when it was written.
        /// If <see cref="Archive"/> is in write-mode, this property will return <see langword="true"/> if <see cref="RSAEncryptParameters"/>
        /// is not <see langword="null"/>.
        /// </remarks>
        public bool IsRSAEncrypted
        {
            get { return MausStream.IsRSAEncrypted; }
        }

        /// <summary>
        /// Gets and sets an RSA key used to encrypt or decrypt the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para>The current instance is not encrypted.</para>
        /// <para>-OR-</para>
        /// <para>The current instance is in read-mode, and is not RSA encrypted.</para>
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="Archive"/> is in read-mode, and the current instance has already been successfully decrypted.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>In a set operation, <see cref="Archive"/> is in write-mode, and the specified value does not represent a valid public or private key.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, <see cref="Archive"/> is in read-mode, and the specified value does not represent a valid private key.</para>
        /// </exception>
        public RsaKeyParameters RSAEncryptParameters
        {
            get { return MausStream.RSAEncryptParameters; }
            set
            {
                _ensureCanSetKey();
                MausStream.RSAEncryptParameters = value;
            }
        }
        #endregion

        private void _ensureCanSetKey()
        {
            if (_arch == null) throw new ObjectDisposedException(null, TextResources.ArchiveEntryDeleted);
            if (MausStream.EncryptionFormat == MausEncryptionFormat.None)
                throw new NotSupportedException(TextResources.NotEncrypted);
            if (_arch.Mode == MauZArchiveMode.Read && MausStream.IsDecrypted)
                throw new InvalidOperationException(TextResources.AlreadyDecryptedEntry);
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
        internal readonly DieFledermausStream MausStream;

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
            string path = Path;
            if (path == null) return base.ToString();
            return path;
        }
    }

    internal interface ICompressionFormat
    {
        MausCompressionFormat CompressionFormat { get; }
    }

    internal struct DeflateCompressionFormat : ICompressionFormat
    {
        public MausCompressionFormat CompressionFormat { get { return MausCompressionFormat.Deflate; } }

        public int CompressionLevel;
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
