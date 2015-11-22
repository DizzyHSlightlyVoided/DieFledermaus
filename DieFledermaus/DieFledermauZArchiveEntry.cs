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
using System.Security;

using DieFledermaus.Globalization;

namespace DieFledermaus
{
    /// <summary>
    /// Represents a single entry in a <see cref="DieFledermauZArchive"/>.
    /// </summary>
    public class DieFledermauZArchiveEntry
    {
        internal DieFledermauZArchiveEntry(DieFledermauZArchive archive, string path, ICompressionFormat compFormat, MausEncryptionFormat encFormat)
        {
            _path = path;
            _bufferStream = new QuickBufferStream();
            _mausStream = new DieFledermausStream(this, _bufferStream, compFormat ?? new DeflateCompressionFormat(), encFormat);
        }

        private object _lock = new object();

        private DieFledermauZArchive _arch;
        /// <summary>
        /// Gets the <see cref="DieFledermauZArchive"/> containing the current instance, or <c>null</c> if
        /// the current instance has been deleted.
        /// </summary>
        public DieFledermauZArchive Archive { get { return _arch; } }

        private string _path;
        /// <summary>
        /// Gets the path of the current instance within the archive.
        /// </summary>
        public string Path { get { return _path; } }

        /// <summary>
        /// Gets the encryption format of the current instance.
        /// </summary>
        public MausEncryptionFormat EncryptionFormat { get { return _mausStream.EncryptionFormat; } }

        /// <summary>
        /// Gets the compression format of the current instance.
        /// </summary>
        public MausCompressionFormat CompressionFormat { get { return _mausStream.CompressionFormat; } }

        /// <summary>
        /// Gets and sets the encryption key for the current instance, or <c>null</c> if the current instance is not encrypted.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance has been deleted.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// <para>The current instance is false.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, <see cref="Archive"/> is in read-mode, and the current instance has already been successfully decoded.</para>
        /// </exception>
        public byte[] Key
        {
            get { return _mausStream.Key; }
            set
            {
                _ensureCanSetKey();

                _mausStream.Key = value;
            }
        }

        /// <summary>
        /// Sets <see cref="Key"/> to a value derived from the specified password.
        /// </summary>
        /// <param name="password">The password to set.</param>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance has been deleted.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// <para>The current instance is false.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, <see cref="Archive"/> is in read-mode, and the current instance has already been successfully decoded.</para>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="password"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="password"/> has a length of 0.
        /// </exception>
        public void SetPassword(string password)
        {
            _ensureCanSetKey();
            _mausStream.SetPassword(password);
        }

        /// <summary>
        /// Sets <see cref="Key"/> to a value derived from the specified password.
        /// </summary>
        /// <param name="password">The password to set.</param>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance has been deleted.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// <para>The current instance is false.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, <see cref="Archive"/> is in read-mode, and the current instance has already been successfully decoded.</para>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="password"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="password"/> has a length of 0.
        /// </exception>
        public void SetPassword(SecureString password)
        {
            _ensureCanSetKey();
            _mausStream.SetPassword(password);
        }

        /// <summary>
        /// Gets a collection containing options which should be encrypted, or <c>null</c> if the current entry is not encrypted.
        /// </summary>
        public DieFledermausStream.SettableOptions EncryptedOptions { get { return _mausStream.EncryptedOptions; } }

        private void _ensureCanSetKey()
        {
            if (_arch == null) throw new ObjectDisposedException(TextResources.ArchiveEntryDeleted);
            if (_mausStream.EncryptionFormat == MausEncryptionFormat.None)
                throw new InvalidOperationException(TextResources.NotEncrypted);
            if (_arch.Mode == MauZArchiveMode.Read && _mausStream.HeaderIsProcessed)
                throw new InvalidOperationException(TextResources.AlreadyDecryptedArchive);
        }

        private void EnsureCanWrite()
        {
            if (_arch == null) throw new ObjectDisposedException(TextResources.ArchiveEntryDeleted);
            _arch.EnsureCanWrite();
        }

        private void EnsureCanRead()
        {
            if (_arch == null) throw new ObjectDisposedException(TextResources.ArchiveEntryDeleted);
            _arch.EnsureCanRead();
        }

        private readonly QuickBufferStream _bufferStream;
        private readonly DieFledermausStream _mausStream;
        private QuickBufferStream _writingStream;

        /// <summary>
        /// Opens the archive entry for writing.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ObjectDisposedException">
        /// The current instance has been deleted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <para><see cref="Archive"/> is in read-only mode.</para>
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

                _writingStream = new QuickBufferStream();
                _writingStream.Disposing += _writingStream_Disposing;
                return _writingStream;
            }
        }

        private void _writingStream_Disposing(object sender, EventArgs e)
        {
            _writingStream.Reset();
            _writingStream.BufferCopyTo(_mausStream);
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
                _arch = null;
                if (_writingStream != null)
                    _writingStream.Disposing -= _writingStream_Disposing;
            }
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
