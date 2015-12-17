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
using System.Security.Cryptography;

using DieFledermaus.Globalization;
using Org.BouncyCastle.Crypto.Parameters;

namespace DieFledermaus
{
    /// <summary>
    /// Represents a single file entry in a <see cref="DieFledermauZArchive"/>.
    /// </summary>
    public class DieFledermauZArchiveEntry : DieFledermauZItem, IMausStream
    {
        internal DieFledermauZArchiveEntry(DieFledermauZArchive archive, string path, ICompressionFormat compFormat, MausEncryptionFormat encFormat)
            : base(archive, path, compFormat, encFormat)
        {
        }

        internal DieFledermauZArchiveEntry(DieFledermauZArchive archive, string path, DieFledermausStream stream, long curOffset, long realOffset)
            : base(archive, path, stream, curOffset, realOffset)
        {
        }

        internal override bool IsFilenameEncrypted
        {
            get { return MausStream.EncryptedOptions != null && MausStream.EncryptedOptions.Contains(MausOptionToEncrypt.Filename); }
        }

        /// <summary>
        /// Gets the compression format of the current instance.
        /// </summary>
        public MausCompressionFormat CompressionFormat { get { return MausStream.CompressionFormat; } }

        /// <summary>
        /// Gets a collection containing options which should be encrypted, or <c>null</c> if the current entry is not encrypted.
        /// </summary>
        public DieFledermausStream.SettableOptions EncryptedOptions { get { return MausStream.EncryptedOptions; } }

        /// <summary>
        /// Gets and sets the time at which the underlying file was created, or <c>null</c> to specify no creation time.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance has been deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-only mode.
        /// </exception>
        public DateTime? CreatedTime
        {
            get { return MausStream.CreatedTime; }
            set
            {
                EnsureCanWrite();
                MausStream.CreatedTime = value;
            }
        }

        /// <summary>
        /// Gets and sets the time at which the underlying file was last modified, or <c>null</c> to specify no modification time.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-only mode.
        /// </exception>
        public DateTime? ModifiedTime
        {
            get { return MausStream.ModifiedTime; }
            set
            {
                EnsureCanWrite();
                MausStream.ModifiedTime = value;
            }
        }

        /// <summary>
        /// Gets and sets an RSA key used to encrypt or decrypt the key of the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para>The current instance is not encrypted.</para>
        /// <para>-OR-</para>
        /// <para>The <see cref="DieFledermauZItem.Archive"/> is in read-mode, and the current instance does not have an RSA-encrypted key.</para>
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="DieFledermauZItem.Archive"/> is in read-mode and the current instance has already been successfully decrypted.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>In a set operation, <see cref="DieFledermauZItem.Archive"/> is in write-mode, and the specified value is not a valid public key.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, <see cref="DieFledermauZItem.Archive"/> is in read-mode, and the specified value is not a valid private key.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, both the specified value and <see cref="RSASignParameters"/> are not <c>null</c>, and both refer to the same key.</para>
        /// </exception>
        public override RsaKeyParameters RSAKeyParameters
        {
            get { return MausStream.RSAKeyParameters; }
            set { base.RSAKeyParameters = value; }
        }

        #region RSA Signature
        /// <summary>
        /// Gets and sets an RSA key used to sign the current entry.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, <see cref="DieFledermauZItem.Archive"/> is in read-mode, and the current instance is not signed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="DieFledermauZItem.Archive"/> is in read-mode, and the current instance is already verified.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>In a set operation, <see cref="DieFledermauZItem.Archive"/> is in write-mode,
        /// and the specified value does not represent a valid private key.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, <see cref="DieFledermauZItem.Archive"/> is in read-mode,
        /// and the specified value does not represent a valid public key.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, both the specified value and <see cref="RSAKeyParameters"/> are not <c>null</c>,
        /// and both refer to the same key.</para>
        /// </exception>
        public RsaKeyParameters RSASignParameters
        {
            get { return MausStream.RSASignParameters; }
            set
            {
                if (_arch == null) throw new ObjectDisposedException(null, TextResources.ArchiveEntryDeleted);
                MausStream.RSASignParameters = value;
            }
        }

        /// <summary>
        /// Gets and sets a string which is used to identify the value of <see cref="RSASignParameters"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, <see cref="DieFledermauZItem.Archive"/> is in read-mode.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="RSASignParameters"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <c>null</c> and has a length equal to 0 or greater than 65536 UTF-8 bytes.
        /// </exception>
        public string RSASignId
        {
            get { return MausStream.RSASignId; }
            set
            {
                EnsureCanWrite();
                MausStream.RSASignId = value;
            }
        }

        /// <summary>
        /// Gets and set a binary value which is used to identify the value of <see cref="RSASignParameters"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, <see cref="DieFledermauZItem.Archive"/> is in read-mode.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="RSASignParameters"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <c>null</c> and has a length equal to 0 or greater than 65536.
        /// </exception>
        public byte[] RSASignIdBytes
        {
            get { return MausStream.RSASignIdBytes; }
            set
            {
                EnsureCanWrite();
                MausStream.RSASignIdBytes = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current instance is signed using an RSA private key.
        /// </summary>
        /// <remarks>
        /// If <see cref="DieFledermauZItem.Archive"/> is in read-mode, this property will return <c>true</c> if and only if the current entry was 
        /// signed when it was written.
        /// If <see cref="DieFledermauZItem.Archive"/> is in write-mode, this property will return <c>true</c> if <see cref="RSASignParameters"/>
        /// is not <c>null</c>.
        /// </remarks>
        public bool IsRSASigned { get { return MausStream.IsRSASigned; } }

        /// <summary>
        /// Gets a value indicating whether the current instance has been successfully verified using <see cref="RSASignParameters"/>.
        /// </summary>
        public bool IsRSASignVerified { get { return MausStream.IsRSASignVerified; } }

        /// <summary>
        /// Tests whether <see cref="RSASignParameters"/> is valid.
        /// </summary>
        /// <returns><c>true</c> if <see cref="RSASignParameters"/> is set to the correct public key; <c>false</c> if the current instance is not 
        /// signed, or if <see cref="RSASignParameters"/> is not set to the correct value.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="DieFledermauZItem.Archive"/> is in write-only mode.
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// The stream contains invalid data.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// <see cref="RSASignParameters"/> is set to an entirely invalid value.
        /// </exception>
        public bool VerifyRSASignature()
        {
            lock (_lock)
            {
                LoadData();
                return MausStream.VerifyRSASignature();
            }
        }
        #endregion

        #region DSA Signature
        /// <summary>
        /// Gets and sets a DSA key used to sign the current entry.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, <see cref="DieFledermauZItem.Archive"/> is in read-mode, and the current instance is not signed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="DieFledermauZItem.Archive"/> is in read-mode, and the current instance is already verified.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>In a set operation, <see cref="DieFledermauZItem.Archive"/> is in write-mode,
        /// and the specified value does not represent a valid private key.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, <see cref="DieFledermauZItem.Archive"/> is in read-mode,
        /// and the specified value does not represent a valid public key.</para>
        /// </exception>
        public DsaKeyParameters DSASignParameters
        {
            get { return MausStream.DSASignParameters; }
            set
            {
                if (_arch == null) throw new ObjectDisposedException(null, TextResources.ArchiveEntryDeleted);
                MausStream.DSASignParameters = value;
            }
        }

        /// <summary>
        /// Gets and sets a string which is used to identify the value of <see cref="DSASignParameters"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, <see cref="DieFledermauZItem.Archive"/> is in read-mode.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="DSASignParameters"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <c>null</c> and has a length equal to 0 or greater than 65536 UTF-8 bytes.
        /// </exception>
        public string DSASignId
        {
            get { return MausStream.DSASignId; }
            set
            {
                EnsureCanWrite();
                MausStream.DSASignId = value;
            }
        }

        /// <summary>
        /// Gets and set a binary value which is used to identify the value of <see cref="DSASignParameters"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, <see cref="DieFledermauZItem.Archive"/> is in read-mode.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="DSASignParameters"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <c>null</c> and has a length equal to 0 or greater than 65536.
        /// </exception>
        public byte[] DSASignIdBytes
        {
            get { return MausStream.DSASignIdBytes; }
            set
            {
                EnsureCanWrite();
                MausStream.DSASignIdBytes = value;
            }
        }
        
        /// <summary>
        /// Gets a value indicating whether the current instance is signed using a DSA private key.
        /// </summary>
        /// <remarks>
        /// If <see cref="DieFledermauZItem.Archive"/> is in read-mode, this property will return <c>true</c> if and only if the current entry was 
        /// signed when it was written.
        /// If <see cref="DieFledermauZItem.Archive"/> is in write-mode, this property will return <c>true</c> if <see cref="DSASignParameters"/>
        /// is not <c>null</c>.
        /// </remarks>
        public bool IsDSASigned { get { return MausStream.IsDSASigned; } }

        /// <summary>
        /// Gets a value indicating whether the current instance has been successfully verified using <see cref="DSASignParameters"/>.
        /// </summary>
        public bool IsDSASignVerified { get { return MausStream.IsDSASignVerified; } }

        /// <summary>
        /// Tests whether <see cref="DSASignParameters"/> is valid.
        /// </summary>
        /// <returns><c>true</c> if <see cref="DSASignParameters"/> is set to the correct public key; <c>false</c> if the current instance is not 
        /// signed, or if <see cref="DSASignParameters"/> is not set to the correct value.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="DieFledermauZItem.Archive"/> is in write-only mode.
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// The stream contains invalid data.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// <see cref="DSASignParameters"/> is set to an entirely invalid value.
        /// </exception>
        public bool VerifyDSASignature()
        {
            lock (_lock)
            {
                LoadData();
                return MausStream.VerifyDSASignature();
            }
        }
        #endregion

        #region ECDSA Signature
        /// <summary>
        /// Gets and sets an ECDSA key used to sign the current entry.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, <see cref="DieFledermauZItem.Archive"/> is in read-mode, and the current instance is not signed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="DieFledermauZItem.Archive"/> is in read-mode, and the current instance is already verified.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>In a set operation, <see cref="DieFledermauZItem.Archive"/> is in write-mode,
        /// and the specified value does not represent a valid private key.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, <see cref="DieFledermauZItem.Archive"/> is in read-mode,
        /// and the specified value does not represent a valid public key.</para>
        /// </exception>
        public ECKeyParameters ECDSASignParameters
        {
            get { return MausStream.ECDSASignParameters; }
            set
            {
                if (_arch == null) throw new ObjectDisposedException(null, TextResources.ArchiveEntryDeleted);
                MausStream.ECDSASignParameters = value;
            }
        }

        /// <summary>
        /// Gets and sets a string which is used to identify the value of <see cref="ECDSASignParameters"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, <see cref="DieFledermauZItem.Archive"/> is in read-mode.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="ECDSASignParameters"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <c>null</c> and has a length equal to 0 or greater than 65536 UTF-8 bytes.
        /// </exception>
        public string ECDSASignId
        {
            get { return MausStream.ECDSASignId; }
            set
            {
                EnsureCanWrite();
                MausStream.ECDSASignId = value;
            }
        }

        /// <summary>
        /// Gets and set a binary value which is used to identify the value of <see cref="ECDSASignParameters"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, <see cref="DieFledermauZItem.Archive"/> is in read-mode.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, <see cref="ECDSASignParameters"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <c>null</c> and has a length equal to 0 or greater than 65536.
        /// </exception>
        public byte[] ECDSASignIdBytes
        {
            get { return MausStream.ECDSASignIdBytes; }
            set
            {
                EnsureCanWrite();
                MausStream.ECDSASignIdBytes = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current instance is signed using a ECDSA private key.
        /// </summary>
        /// <remarks>
        /// If <see cref="DieFledermauZItem.Archive"/> is in read-mode, this property will return <c>true</c> if and only if the current entry was 
        /// signed when it was written.
        /// If <see cref="DieFledermauZItem.Archive"/> is in write-mode, this property will return <c>true</c> if <see cref="ECDSASignParameters"/>
        /// is not <c>null</c>.
        /// </remarks>
        public bool IsECDSASigned { get { return MausStream.IsECDSASigned; } }

        /// <summary>
        /// Gets a value indicating whether the current instance has been successfully verified using <see cref="ECDSASignParameters"/>.
        /// </summary>
        public bool IsECDSASignVerified { get { return MausStream.IsECDSASignVerified; } }

        /// <summary>
        /// Tests whether <see cref="ECDSASignParameters"/> is valid.
        /// </summary>
        /// <returns><c>true</c> if <see cref="ECDSASignParameters"/> is set to the correct public key; <c>false</c> if the current instance is not 
        /// signed, or if <see cref="ECDSASignParameters"/> is not set to the correct value.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="DieFledermauZItem.Archive"/> is in write-only mode.
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// The stream contains invalid data.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// <see cref="ECDSASignParameters"/> is set to an entirely invalid value.
        /// </exception>
        public bool VerifyECDSASignature()
        {
            lock (_lock)
            {
                LoadData();
                return MausStream.VerifyECDSASignature();
            }
        }
        #endregion

        private MausBufferStream _writingStream;

        /// <summary>
        /// Opens the archive entry for writing.
        /// </summary>
        /// <returns>A writeable stream.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current instance has been deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="DieFledermauZItem.Archive"/> is in read-only mode.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// <para>The current instance is already open for writing.</para>
        /// <para>-OR-</para>
        /// <para>The current instance was previously open for writing, and data was written to the stream before it was closed.</para>
        /// </exception>
        public Stream OpenWrite()
        {
            lock (_lock)
            {
                EnsureCanWrite();
                if (_writingStream != null)
                    throw new InvalidOperationException(TextResources.ArchiveAlreadyWritten);

                _writingStream = new MausBufferStream();
                _writingStream.Disposing += _writingStream_Disposing;
                return _writingStream;
            }
        }

        private void _writingStream_Disposing(object sender, DisposeEventArgs e)
        {
            if (e.Length == 0)
            {
                _writingStream = null;
                return;
            }
            _writingStream.Reset();
            _writingStream.BufferCopyTo(MausStream, false);
            MausBufferStream newStream = new MausBufferStream();
            _writingStream.Reset();
            newStream.Prepend(_writingStream);
            _writingStream = newStream;
        }

        /// <summary>
        /// Decrypts the current instance.
        /// </summary>
        /// <returns>The current instance.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current instance has been deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="DieFledermauZItem.Archive"/> is in write-only mode.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current instance is not encrypted.
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// The stream contains invalid data.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// Either the password is not correct, or <see cref="RSASignParameters"/> is not <c>null</c> and is not set to the correct value.
        /// It is safe to attempt to call <see cref="Decrypt()"/> or <see cref="OpenRead()"/> again if this exception is caught.
        /// </exception>
        public override DieFledermauZItem Decrypt()
        {
            EnsureCanRead();
            lock (_lock)
            {
                return DoDecrypt();
            }
        }

        private DieFledermauZItem DoDecrypt()
        {
            if (_isDecrypted) return this;
            base.Decrypt();
            _isDecrypted = true;
            return this;
        }

        /// <summary>
        /// Opens the archive entry for reading.
        /// </summary>
        /// <returns>A readable stream containing the decompressed file.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current instance has been deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="DieFledermauZItem.Archive"/> is in write-only mode.
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// The stream contains invalid data.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// Either the password is not correct, or <see cref="RSASignParameters"/> is not <c>null</c> and is not set to the correct value.
        /// It is safe to attempt to call <see cref="Decrypt()"/> or <see cref="OpenRead()"/> again if this exception is caught.
        /// </exception>
        public Stream OpenRead()
        {
            lock (_lock)
            {
                EnsureCanRead();

                LoadData();

                MausBufferStream mbs = new MausBufferStream();
                _writingStream.BufferCopyTo(mbs, false);
                return mbs;
            }
        }

        private void LoadData()
        {
            if (_writingStream != null)
                return;
            if (MausStream.EncryptionFormat == MausEncryptionFormat.None)
                SeekToFile();
            else
                DoDecrypt();
            _writingStream = new MausBufferStream();
            MausStream.BufferCopyTo(_writingStream);
        }

        /// <summary>
        /// Returns the hash of the uncompressed data.
        /// </summary>
        /// <returns>The hash of the uncompressed data.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current instance has been deleted.
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// <see cref="DieFledermauZItem.Archive"/> is in read-mode and the stream contains invalid data.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// <see cref="DieFledermauZItem.Archive"/> is in read-mode and 
        /// either the password is not correct, or <see cref="RSASignParameters"/> is not <c>null</c> and is not set to the correct value.
        /// It is safe to attempt to call <see cref="Decrypt()"/> or <see cref="OpenRead()"/> again if this exception is caught.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// <see cref="DieFledermauZItem.Archive"/> is in write-mode, and the current instance has either not yet been written to, or the
        /// stream called from <see cref="OpenWrite()"/> has not yet been closed.
        /// </exception>
        /// <remarks>When <see cref="DieFledermauZItem.Archive"/> is in read-mode, returns the same value as <see cref="DieFledermauZItem.Hash"/>.
        /// In write-mode, this method computes the hash from the written data.</remarks>
        public byte[] ComputeHash()
        {
            if (_arch == null)
                throw new ObjectDisposedException(null, TextResources.ArchiveEntryDeleted);

            if (_arch.Mode == MauZArchiveMode.Read)
            {
                LoadData();
                return MausStream.Hash;
            }

            if (_writingStream == null || _writingStream.CanWrite)
                throw new InvalidOperationException(string.Format(TextResources.ArchiveNotWritten, Path));

            _writingStream.Reset();
            return DieFledermausStream.ComputeHash(_writingStream, HashFunction);
        }

        internal override MausBufferStream GetWritten()
        {
            lock (_lock)
            {
                if (_writingStream == null || _writingStream.CanWrite)
                    throw new InvalidOperationException(string.Format(TextResources.ArchiveNotWritten, MausStream.Filename));
                return base.GetWritten();
            }
        }

        internal override void DoDelete(bool deleteMaus)
        {
            if (_writingStream != null)
                _writingStream.Disposing -= _writingStream_Disposing;
            base.DoDelete(deleteMaus);
        }
    }
}
