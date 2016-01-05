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

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;

namespace DieFledermaus
{
    /// <summary>
    /// Interface for classes which use encryption.
    /// </summary>
    public interface IMausCrypt
    {
        /// <summary>
        /// Gets and sets the password used for the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current instance is not encrypted.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, the current instance is in read-mode and has already been successfully decrypted.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <see langword="null"/> and has a length of 0.
        /// </exception>
        string Password { get; set; }

        /// <summary>
        /// Gets the encryption format of the current instance.
        /// </summary>
        MausEncryptionFormat EncryptionFormat { get; }

        /// <summary>
        /// Gets and sets a binary key used to encrypt or decrypt the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current instance is not encrypted.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, the current instance is in read-mode and has already been successfully decrypted.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value has an invalid length according to <see cref="LegalKeySizes"/>.
        /// </exception>
        byte[] Key { get; set; }
        /// <summary>
        /// Gets and sets the initialization vector of the current instance, or <see langword="null"/> if <see cref="EncryptionFormat"/>
        /// is <see cref="MausEncryptionFormat.None"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para>In a set operation, <see cref="EncryptionFormat"/> is <see cref="MausEncryptionFormat.None"/>.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current instance is in read-mode.</para>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// In a set operation, the specified value is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the length of the specified value is not equal to the length specified by <see cref="BlockSize"/>.
        /// </exception>
        byte[] IV { get; set; }
        /// <summary>
        /// Gets and sets the key associated with the current instance, or <see langword="null"/> if <see cref="EncryptionFormat"/>
        /// is <see cref="MausEncryptionFormat.None"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para>In a set operation, <see cref="EncryptionFormat"/> is <see cref="MausEncryptionFormat.None"/>.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current instance is in read-mode.</para>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// In a set operation, the specified value is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the length of the specified value is not equal to the maximum length specified by <see cref="LegalKeySizes"/>.
        /// </exception>
        byte[] Salt { get; set; }

        /// <summary>
        /// Sets <see cref="Key"/> to a value derived from <see cref="Password"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current instance is not encrypted.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// <para>In a set operation, the current instance is in read-mode and has already been successfully decrypted.</para>
        /// <para>-OR-</para>
        /// <para><see cref="Password"/> is <see langword="null"/>.</para>
        /// </exception>
        void DeriveKey();

        /// <summary>
        /// Gets and sets the hash function used by the current instance. The default is <see cref="MausHashFunction.Sha256"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-mode.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// The specified value is not a valid <see cref="MausHashFunction"/> value.
        /// </exception>
        MausHashFunction HashFunction { get; set; }

        /// <summary>
        /// Gets the maximum number of bits in a single block of data, or 0 if <see cref="EncryptionFormat"/> is 
        /// <see cref="MausEncryptionFormat.None"/>.
        /// </summary>
        int BlockSize { get; }

        /// <summary>
        /// Gets a value indicating whether the current instance has been successfully decrypted.
        /// </summary>
        bool IsDecrypted { get; }

        /// <summary>
        /// Gets the HMAC of the current instance, or <see langword="null"/> if the current instance is in write-mode or is not encrypted.
        /// </summary>
        byte[] HMAC { get; }

        /// <summary>
        /// Gets and sets the number of PBKDF2 cycles used to generate the password, minus 9001.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para>In a set operation, the current instance is not encrypted.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current instance is in read-only mode.</para>
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// In a set operation, the specified value is less than 0 or is greater than <see cref="int.MaxValue"/> minus 9001.
        /// </exception>
        int PBKDF2CycleCount { get; set; }

        /// <summary>
        /// Gets a collection containing the valid values for <see cref="KeySize"/>,
        /// or <see langword="null"/> if <see cref="EncryptionFormat"/> is <see cref="MausEncryptionFormat.None"/>.
        /// </summary>
        KeySizeList LegalKeySizes { get; }

        /// <summary>
        /// Gets and sets the number of bits in the key.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para>In a set operation, the current instance is not encrypted.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current instance is in read-only mode.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, <see cref="Key"/> is not <see langword="null"/> and the specified value is not the proper length.</para>
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// In a set operation, <see cref="LegalKeySizes"/> does not contain the specified value.
        /// </exception>
        int KeySize { get; set; }

        /// <summary>
        /// Decrypts the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is disposed.
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// The underlying stream contains invalid or contradictory data.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para>The current instance is in write-only mode.</para>
        /// <para>-OR-</para>
        /// <para>The underlying stream contains structurally valid but unsupported or unknown options or data.</para>
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current instance is not encrypted.
        /// </exception>
        /// <exception cref="IOException">
        /// An I/O error occurred.
        /// </exception>
        /// <exception cref="CryptoException">
        /// <see cref="Password"/> is set to the wrong value. It is safe to call <see cref="Decrypt()"/> again if this exception is caught.
        /// </exception>
        void Decrypt();

        /// <summary>
        /// Raised when the current instance is reading or writing data, and the progress state meaningfully changes.
        /// </summary>
        event MausProgressEventHandler Progress;
    }

    internal interface IMausSign
    {
        RsaKeyParameters RSASignParameters { get; set; }

        string RSASignId { get; set; }

        byte[] RSASignIdBytes { get; set; }

        bool IsRSASigned { get; }

        bool IsRSASignVerified { get; }

        bool VerifyRSASignature();

        DsaKeyParameters DSASignParameters { get; set; }

        string DSASignId { get; set; }

        byte[] DSASignIdBytes { get; set; }

        bool IsDSASigned { get; }

        bool IsDSASignVerified { get; }

        bool VerifyDSASignature();

        ECKeyParameters ECDSASignParameters { get; set; }

        string ECDSASignId { get; set; }

        byte[] ECDSASignIdBytes { get; set; }

        bool IsECDSASigned { get; }

        bool IsECDSASignVerified { get; }

        bool VerifyECDSASignature();
    }

    internal interface IMausStream : IMausSign
    {
        MausCompressionFormat CompressionFormat { get; }

        DateTime? CreatedTime { get; }

        DateTime? ModifiedTime { get; }

        DieFledermausStream.SettableOptions EncryptedOptions { get; }

        byte[] Hash { get; }

        byte[] ComputeHash();
    }
}
