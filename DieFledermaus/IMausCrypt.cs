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
        /// The current instance is not encrypted or is in read-mode.
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
        /// The current instance is not encrypted or is in read-mode.
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
        /// The current instance is not encrypted or is in read-mode.
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
        /// Gets a value indicating whether the current instance is encrypted with an RSA key.
        /// </summary>
        bool IsRSAEncrypted { get; }

        /// <summary>
        /// Gets and sets an RSA key used to encrypt or decrypt the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is closed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para>The current instance is not encrypted.</para>
        /// <para>-OR-</para>
        /// <para>The current instance is in read-mode, and is not RSA encrypted.</para>
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, the current instance is in read-mode and has already been successfully decrypted.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>In a set operation, the current instance is in write-mode, and the specified value does not represent a valid public or private key.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current instance is in read-mode, and the specified value does not represent a valid private key.</para>
        /// </exception>
        RsaKeyParameters RSAEncryptParameters { get; set; }

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
        /// Gets the loaded hash code of the compressed version of the current instance and options,
        /// the HMAC of the current instance if the current instance is encrypted,
        /// or <see langword="null"/> if the current instance is in write-mode.
        /// </summary>
        byte[] CompressedHash { get; }

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

        /// <summary>
        /// Gets and sets a comment on the current instance. Also sets the value of <see cref="CommentBytes"/> using UTF-8.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current instance is in read-only mode.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <see langword="null"/>, and has a length which is equal to 0 or which is greater than 65536 UTF-8 bytes.
        /// </exception>
        string Comment { get; set; }

        /// <summary>
        /// Gets and sets a comment on the current instance. Also sets the value of <see cref="Comment"/> using UTF-8.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current instance is in read-only mode.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not <see langword="null"/>, and has a length which is equal to 0 or which is greater than 65536.
        /// </exception>
        byte[] CommentBytes { get; set; }

        /// <summary>
        /// Gets and sets options for saving <see cref="Comment"/>/<see cref="CommentBytes"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current instance is in read-only mode.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// In a set operation, the specified value is not a valid <see cref="MausSavingOptions"/> value.
        /// </exception>
        MausSavingOptions CommentSaving { get; set; }
    }

    /// <summary>
    /// Interface for classes which may be signed using RSA, DSA, or ECDSA.
    /// </summary>
    public interface IMausSign : IMausCrypt
    {
        /// <summary>
        /// Gets and sets an RSA key used to sign the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-mode, and is not signed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, the current instance is in read-mode, and has already been verified.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>In a set operation, the current instance is in write-mode, and the specified value does not represent a valid private key.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current instance is in read-mode, and the specified value does not represent a valid public or private key.</para>
        /// </exception>
        RsaKeyParameters RSASignParameters { get; set; }

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
        string RSASignId { get; set; }

        /// <summary>
        /// Gets and sets a binary value which is used to identify the value of <see cref="RSASignParameters"/>.
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
        byte[] RSASignIdBytes { get; set; }

        /// <summary>
        /// Gets a value indicating whether the current instance is signed using RSA.
        /// If the current instance is in write-mode, returns <see langword="true"/> if and only if <see cref="RSASignParameters"/> is not <see langword="null"/>.
        /// </summary>
        bool IsRSASigned { get; }

        /// <summary>
        /// Gets a value indicating whether the current instance is in read-mode and was signed using <see cref="RSASignParameters"/>.
        /// </summary>
        bool IsRSASignVerified { get; }

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
        /// <exception cref="InvalidDataException">
        /// The stream contains invalid data.
        /// </exception>
        bool VerifyRSASignature();
        /// <summary>
        /// Gets and sets a DSA key used to sign the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-mode, and is not signed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, the current instance is in read-mode, and has already been verified.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>In a set operation, the current instance is in write-mode, and the specified value does not represent a valid private key.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current instance is in read-mode, and the specified value does not represent a valid public or private key.</para>
        /// </exception>
        DsaKeyParameters DSASignParameters { get; set; }

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
        string DSASignId { get; set; }

        /// <summary>
        /// Gets and sets a binary value which is used to identify the value of <see cref="DSASignParameters"/>.
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
        byte[] DSASignIdBytes { get; set; }

        /// <summary>
        /// Gets a value indicating whether the current instance is signed using DSA.
        /// If the current instance is in write-mode, returns <see langword="true"/> if and only if <see cref="DSASignParameters"/> is not <see langword="null"/>.
        /// </summary>
        bool IsDSASigned { get; }

        /// <summary>
        /// Gets a value indicating whether the current instance is in read-mode and was signed using <see cref="DSASignParameters"/>.
        /// </summary>
        bool IsDSASignVerified { get; }

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
        /// <exception cref="InvalidDataException">
        /// The stream contains invalid data.
        /// </exception>
        bool VerifyDSASignature();
        /// <summary>
        /// Gets and sets an ECECDSA key used to sign the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-mode, and is not signed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// In a set operation, the current instance is in read-mode, and has already been verified.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>In a set operation, the current instance is in write-mode, and the specified value does not represent a valid private key.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the current instance is in read-mode, and the specified value does not represent a valid public or private key.</para>
        /// </exception>
        ECKeyParameters ECDSASignParameters { get; set; }

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
        string ECDSASignId { get; set; }

        /// <summary>
        /// Gets and sets a binary value which is used to identify the value of <see cref="ECDSASignParameters"/>.
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
        byte[] ECDSASignIdBytes { get; set; }

        /// <summary>
        /// Gets a value indicating whether the current instance is signed using ECDSA.
        /// If the current instance is in write-mode, returns <see langword="true"/> if and only if <see cref="ECDSASignParameters"/> is not <see langword="null"/>.
        /// </summary>
        bool IsECDSASigned { get; }

        /// <summary>
        /// Gets a value indicating whether the current instance is in read-mode and was signed using <see cref="ECDSASignParameters"/>.
        /// </summary>
        bool IsECDSASignVerified { get; }

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
        /// <exception cref="InvalidDataException">
        /// The stream contains invalid data.
        /// </exception>
        bool VerifyECDSASignature();
    }

    /// <summary>
    /// An interface for classes which allow direct access to a DieFledermaus stream.
    /// </summary>
    public interface IMausStream : IMausSign
    {
        /// <summary>
        /// Gets and sets the compression format of the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current instance is in read-only mode.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// In a set operation, the specified value is not a valid <see cref="MausCompressionFormat"/> value.
        /// </exception>
        MausCompressionFormat CompressionFormat { get; set; }

        /// <summary>
        /// Gets and sets options for saving <see cref="CompressionFormat"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-only mode.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// In a set operation, the specified value is not a valid <see cref="MausSavingOptions"/> value.
        /// </exception>
        MausSavingOptions CompressionFormatSaving { get; set; }

        /// <summary>
        /// Gets and sets the compression level. If <see cref="CompressionFormat"/> is <see cref="MausCompressionFormat.Deflate"/>,
        /// returns a value between 0 and 9 inclusive; if <see cref="CompressionFormat"/> is <see cref="MausCompressionFormat.Lzma"/>,
        /// returns the dictionary size in bytes; if the current instance is uncompressed or is in read-mode, returns 0.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-only mode or is not compressed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// In a set operation, the specified value is less than 0 or is greater than 9 if <see cref="CompressionFormat"/> is <see cref="MausCompressionFormat.Deflate"/>,
        /// or is nonzero and is less than <see cref="LzmaDictionarySize.MinValue"/> or is greater than <see cref="LzmaDictionarySize.MaxValue"/> if
        /// <see cref="CompressionFormat"/> is <see cref="MausCompressionFormat.Lzma"/>.
        /// </exception>
        int CompressionLevel { get; set; }

        /// <summary>
        /// Sets <see cref="CompressionLevel"/> using the corresponding <see cref="LzmaDictionarySize"/> value.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current instance is in read-only mode, or <see cref="CompressionFormat"/> is not <see cref="MausCompressionFormat.Lzma"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The specified value is nonzero and is less than <see cref="LzmaDictionarySize.MinValue"/> or greater than <see cref="LzmaDictionarySize.MaxValue"/>.
        /// </exception>
        void SetCompressionLevel(LzmaDictionarySize value);

        /// <summary>
        /// Gets and sets the time at which the file was created.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current instance is in read-only mode.
        /// </exception>
        DateTime? CreatedTime { get; }

        /// <summary>
        /// Gets and sets options for saving <see cref="CreatedTime"/>
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current instance is in read-only mode.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// In a set operation, the specified value is not a valid <see cref="MausSavingOptions"/> value.
        /// </exception>
        MausSavingOptions CreatedTimeSaving { get; set; }

        /// <summary>
        /// Gets and sets the time at which the file was last modified.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current instance is in read-only mode.
        /// </exception>
        DateTime? ModifiedTime { get; }

        /// <summary>
        /// Gets and sets options for saving <see cref="ModifiedTime"/>
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current instance is in read-only mode.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// In a set operation, the specified value is not a valid <see cref="MausSavingOptions"/> value.
        /// </exception>
        MausSavingOptions ModifiedTimeSaving { get; set; }

        /// <summary>
        /// Gets the hash of the unencrypted data, or <see langword="null"/> if the current instance is in write-mode.
        /// </summary>
        byte[] Hash { get; }

        /// <summary>
        /// Computes the hash of the unencrypted data.
        /// </summary>
        /// <returns>The hash of the </returns>
        /// <exception cref="ObjectDisposedException">
        /// The current instance is disposed.
        /// </exception>
        /// <exception cref="CryptoException">
        /// The current instance is in read-mode, and either <see cref="IMausCrypt.Key"/> or <see cref="IMausCrypt.Password"/> is incorrect.
        /// It is safe to attempt to call <see cref="IMausCrypt.Decrypt()"/> or <see cref="ComputeHash()"/> again if this exception is caught.
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// The current instance is in read-mode, and contains invalid data.
        /// </exception>
        /// <exception cref="IOException">
        /// An I/O error occurred.
        /// </exception>
        byte[] ComputeHash();
    }
}
