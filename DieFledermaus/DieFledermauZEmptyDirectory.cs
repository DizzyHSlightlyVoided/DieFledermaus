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

using DieFledermaus.Globalization;
using Org.BouncyCastle.Crypto;

namespace DieFledermaus
{
    /// <summary>
    /// Represents an empty directory in 
    /// </summary>
    public class DieFledermauZEmptyDirectory : DieFledermauZItem
    {
        internal DieFledermauZEmptyDirectory(DieFledermauZArchive archive, string path, MausEncryptionFormat encryptionFormat)
            : base(archive, path, new NoneCompressionFormat(), encryptionFormat)
        {
        }

        internal DieFledermauZEmptyDirectory(DieFledermauZArchive archive, string path, DieFledermausStream stream, long offset, long realOffset)
            : base(archive, path, path, stream, offset, realOffset)
        {
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
        /// <exception cref="CryptoException">
        /// The password is not correct. It is safe to attempt to call <see cref="Decrypt()"/>
        /// again if this exception is caught.
        /// </exception>
        public override DieFledermauZItem Decrypt()
        {
            base.Decrypt();
            if (_isDecrypted) return this;
            CheckStream(MausStream);
            _isDecrypted = true;
            return this;
        }

        internal static void CheckStream(DieFledermausStream stream)
        {
            const int ForwardSlashReadByte = '/';

            if (HasNonDirValues(stream) || DieFledermausStream._textEncoding.GetByteCount(stream.Filename) > byte.MaxValue ||
                stream.ReadByte() != ForwardSlashReadByte || stream.ReadByte() >= 0)
                throw new InvalidDataException(TextResources.InvalidDataMaus);
        }

        internal static bool HasNonDirValues(DieFledermausStream stream)
        {
            return stream.ModifiedTime.HasValue || stream.CreatedTime.HasValue || stream.IsRSASigned;
        }

        internal override bool IsFilenameEncrypted
        {
            get
            {
                return MausStream.EncryptionFormat != MausEncryptionFormat.None &&
                    DieFledermausStream.SetSavingOption(MausStream.FilenameSaving) == MausSavingOptions.SecondaryOnly;
            }
        }

        internal override MausBufferStream GetWritten()
        {
            MausStream.WriteByte((byte)'/');
            return base.GetWritten();
        }
    }
}
