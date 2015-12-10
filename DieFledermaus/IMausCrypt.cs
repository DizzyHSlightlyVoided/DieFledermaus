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
using System.Security;
using System.Security.Cryptography;

namespace DieFledermaus
{
    /// <summary>
    /// Interface for classes which use encryption.
    /// </summary>
    public interface IMausCrypt
    {
        /// <summary>
        /// Gets the encryption format of the current instance.
        /// </summary>
        MausEncryptionFormat EncryptionFormat { get; }

        /// <summary>
        /// Gets and sets the initialization vector of the current instance, or <c>null</c> if <see cref="EncryptionFormat"/> is <see cref="MausEncryptionFormat.None"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, <see cref="EncryptionFormat"/> is <see cref="MausEncryptionFormat.None"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// In a set operation, the specified value is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not the length specified by <see cref="BlockSize"/>.
        /// </exception>
        byte[] IV { get; set; }
        /// <summary>
        /// Gets and sets the key associated with the current instance, or <c>null</c> if <see cref="EncryptionFormat"/> is <see cref="MausEncryptionFormat.None"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, <see cref="EncryptionFormat"/> is <see cref="MausEncryptionFormat.None"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// In a set operation, the specified value is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// In a set operation, the specified value is not the maximum length specified by <see cref="KeySizes"/>.
        /// </exception>
        byte[] Salt { get; set; }

        /// <summary>
        /// Gets and sets a value indicating whether the current instance uses SHA-3. If <c>false</c>, the current instance uses SHA-512 (SHA-2).
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// In a set operation, the current instance is in read-mode.
        /// </exception>
        bool UseSha3 { get; set; }

        /// <summary>
        /// Gets the number of bits in a single block of data, or 0 if <see cref="EncryptionFormat"/> is <see cref="MausEncryptionFormat.None"/>.
        /// </summary>
        int BlockSize { get; }

        /// <summary>
        /// Gets a value indicating whether the current instance has been successfully decrypted.
        /// </summary>
        bool IsDecrypted { get; }

        /// <summary>
        /// Gets a <see cref="System.Security.Cryptography.KeySizes"/> object specifying the length.
        /// </summary>
        KeySizes KeySizes { get; }

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
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// In a set operation, the specified value is invalid according to <see cref="KeySizes"/>.
        /// </exception>
        int KeySize { get; set; }

        /// <summary>
        /// Gets and sets the password used for the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// <para>The current instance is disposed.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, the specified value is disposed.</para>
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current instance is not encrypted.
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
        /// <remarks>
        /// A set operation will dispose of the previous value, as will disposing of the current instance.
        /// </remarks>
        SecureString Password { get; set; }

        /// <summary>
        /// Sets the password used for the current instance.
        /// </summary>
        /// <param name="password">The password to set.</param>
        /// <exception cref="ObjectDisposedException">
        /// <para>The current instance is disposed.</para>
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The current instance is not encrypted.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current instance is in read-mode and has already been successfully decrypted.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="password"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="password"/> has a length of 0.
        /// </exception>
        /// <remarks>
        /// This method will dispose of any previous value of <see cref="Password"/>
        /// </remarks>
        void SetPassword(string password);

        /// <summary>
        /// Decrypts the current instance.
        /// </summary>
        void Decrypt();

        /// <summary>
        /// Determines whether the specified value is a valid length for the key, in bits.
        /// </summary>
        /// <param name="bitCount">The number of bits to test.</param>
        /// <returns><c>true</c> if <paramref name="bitCount"/> is a valid bit count according to <see cref="KeySizes"/>;
        /// <c>false</c> if <paramref name="bitCount"/> is invalid, or if the current instance is not encrypted.</returns>
        bool IsValidKeyBitSize(int bitCount);

        /// <summary>
        /// Determines whether the specified value is a valid length for the key, in bytes.
        /// </summary>
        /// <param name="byteCount">The number of bytes to test.</param>
        /// <returns><c>true</c> if <paramref name="byteCount"/> is a valid bit count according to <see cref="KeySizes"/>;
        /// <c>false</c> if <paramref name="byteCount"/> is invalid, or if the current instance is not encrypted.</returns>
        bool IsValidKeyByteSize(int byteCount);
    }
}
