﻿#region BSD license
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

namespace DieFledermaus
{
    /// <summary>
    /// Represents an empty directory in 
    /// </summary>
    public class DieFledermauZEmptyDirectory : DieFledermauZItem
    {
        internal DieFledermauZEmptyDirectory(DieFledermauZArchive archive, string path)
            : base(archive, path, new NoneCompressionFormat(), MausEncryptionFormat.None)
        {
        }

        internal DieFledermauZEmptyDirectory(DieFledermauZArchive archive, string path, DieFledermausStream stream, long offset)
            : base(archive, path, stream, offset)
        {
            CheckStream(MausStream);
        }

        private bool _enc;
        /// <summary>
        /// Gets and sets a value indicating whether the filename will be encrypted within the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The current stream is disposed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="DieFledermauZItem.Archive"/> is in read-only mode.
        /// </exception>
        /// <remarks>
        /// Setting this property to <c>true</c> will set <see cref="DieFledermauZItem.Key"/> to a randomly-generated value. Subsequently setting this property
        /// to <c>true</c> will set <see cref="DieFledermauZItem.Key"/> to <c>null</c>, and the old key will not be remembered or saved.
        /// </remarks>
        public bool EncryptPath
        {
            get { return _enc; }
            set
            {
                lock (_lock)
                {
                    EnsureCanWrite();
                    if (value && MausStream.EncryptionFormat == MausEncryptionFormat.None)
                    {
                        MausStream.Dispose();
                        MausStream = new DieFledermausStream(this, MausStream.Filename, _bufferStream, new NoneCompressionFormat(), MausEncryptionFormat.Aes);
                        MausStream.EncryptedOptions.Add(MausOptionToEncrypt.Filename);
                    }
                    else if (!value && MausStream.EncryptionFormat != MausEncryptionFormat.None)
                    {
                        MausStream.Dispose();
                        MausStream = new DieFledermausStream(this, MausStream.Filename, _bufferStream, new NoneCompressionFormat(), MausEncryptionFormat.None);
                    }
                    _enc = value;
                }
            }
        }

        /// <summary>
        /// Decrypts the current instance.
        /// </summary>
        /// <returns></returns>
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
        /// <see cref="DieFledermauZItem.Key"/> is not set to the correct value. It is safe to attempt to call <see cref="Decrypt()"/>
        /// again if this exception is caught.
        /// </exception>
        public override DieFledermauZItem Decrypt()
        {
            base.Decrypt();
            CheckStream(MausStream);
            return this;
        }

        internal static void CheckStream(DieFledermausStream stream)
        {
            const int ForwardSlashReadByte = '/';
            if (DieFledermausStream._textEncoding.GetByteCount(stream.Filename) > 254 || stream.ReadByte() != ForwardSlashReadByte || stream.ReadByte() >= 0)
                throw new InvalidDataException(TextResources.InvalidDataMaus);
        }

        internal override bool IsFilenameEncrypted
        {
            get { return _enc; }
        }

        internal override MausBufferStream GetWritten()
        {
            lock (_lock)
            {
                MausStream.WriteByte((byte)'/');
                return base.GetWritten();
            }
        }
    }
}