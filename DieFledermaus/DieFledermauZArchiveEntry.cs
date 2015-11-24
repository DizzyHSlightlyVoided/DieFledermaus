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

namespace DieFledermaus
{
    /// <summary>
    /// Represents a single file entry in a <see cref="DieFledermauZArchive"/>.
    /// </summary>
    public class DieFledermauZArchiveEntry : DieFledermauZItem
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

        private MausBufferStream _writingStream;

        /// <summary>
        /// Opens the archive entry for writing.
        /// </summary>
        /// <returns>A writeable stream to which the data will be written..</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current instance has been deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para><see cref="DieFledermauZItem.Archive"/> is in read-only mode.</para>
        /// <para>-OR-</para>
        /// <para>The current instance has already been open for writing.</para>
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

        /// <summary>
        /// Decrypts the current instance.
        /// </summary>
        /// <returns>The current instance, in decrypted form.</returns>
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
        /// <see cref="DieFledermauZItem.Key"/> is not set to the correct value. It is safe to attempt to call <see cref="Decrypt()"/> or <see cref="OpenRead()"/>
        /// again if this exception is caught.
        /// </exception>
        public override DieFledermauZItem Decrypt()
        {
            lock (_lock)
            {
                DoDecrypt();
                return this;
            }
        }

        private void DoDecrypt()
        {
            base.Decrypt();
            if (_isDecrypted) return;
            if (_writingStream == null)
            {
                _writingStream = new MausBufferStream();
                MausStream.BufferCopyTo(_writingStream);
            }
            _isDecrypted = true;
        }

        /// <summary>
        /// Opens the archive entry for reading.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ObjectDisposedException">
        /// The current instance has been deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="DieFledermauZItem.Archive"/> is in write-only mode.
        /// </exception>
        public Stream OpenRead()
        {
            lock (_lock)
            {
                EnsureCanRead();

                if (_writingStream == null)
                {
                    if (MausStream.EncryptionFormat == MausEncryptionFormat.None)
                    {
                        SeekToFile();
                        _writingStream = new MausBufferStream();
                        MausStream.BufferCopyTo(_writingStream);
                    }
                    else DoDecrypt();
                }

                MausBufferStream mbs = new MausBufferStream();
                _writingStream.BufferCopyTo(mbs);
                return mbs;
            }
        }

        private void _writingStream_Disposing(object sender, EventArgs e)
        {
            _writingStream.Reset();
            _writingStream.BufferCopyTo(MausStream);
        }

        internal override MausBufferStream GetWritten()
        {
            lock (_lock)
            {
                if (_writingStream == null || _writingStream.CanRead)
                    throw new InvalidOperationException(string.Format(TextResources.ArchiveNotWritten, MausStream.Filename));
                return base.GetWritten();
            }
        }

        internal override void DoDelete()
        {
            if (_writingStream != null)
                _writingStream.Disposing -= _writingStream_Disposing;
            base.DoDelete();
        }
    }
}
