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
using System.Security;
using System.Security.Cryptography;

using DieFledermaus.Globalization;

namespace DieFledermaus
{
    /// <summary>
    /// Represents a single entry in a <see cref="DieFledermauZArchive"/>.
    /// </summary>
    public abstract class DieFledermauZItem
    {
        internal DieFledermauZItem(DieFledermauZArchive archive, string path, ICompressionFormat compFormat, MausEncryptionFormat encFormat)
        {
            _bufferStream = new MausBufferStream();
            MausStream = new DieFledermausStream(this, path, _bufferStream, compFormat ?? new DeflateCompressionFormat(), encFormat);
            _arch = archive;
        }

        internal DieFledermauZItem(DieFledermauZArchive archive, string path, DieFledermausStream stream, long curOffset)
        {
            _arch = archive;
            MausStream = stream;
            Offset = curOffset;
            MausStream._entry = this;
        }

        internal readonly long Offset;

        internal long HeadLength { get { return MausStream.HeadLength; } }

        internal object _lock = new object();

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

        private bool _isDecrypted;
        internal bool IsDecrypted { get { return _isDecrypted; } }

        /// <summary>
        /// Decrypts the current instance.
        /// </summary>
        /// <returns></returns>
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
        /// <see cref="Key"/> is not set to the correct value. It is safe to attempt to call <see cref="Decrypt()"/>
        /// again if this exception is caught.
        /// </exception>
        public virtual DieFledermauZItem Decrypt()
        {
            EnsureCanRead();
            if (MausStream.EncryptionFormat == MausEncryptionFormat.None)
                throw new InvalidOperationException(TextResources.NotEncrypted);
            if (_isDecrypted) return this;

            if (_arch.BaseStream.CanSeek && !MausStream.DataIsLoaded)
                _arch.BaseStream.Seek(Offset + _arch.StreamOffset + MausStream.HeadLength, SeekOrigin.Begin);

            MausStream.LoadData();
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
            _isDecrypted = true;

            return this;
        }

        /// <summary>
        /// Gets the encryption format of the current instance.
        /// </summary>
        public MausEncryptionFormat EncryptionFormat { get { return MausStream.EncryptionFormat; } }

        /// <summary>
        /// Gets and sets the encryption key for the current instance, or <c>null</c> if the current instance is not encrypted.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// In a set operation, the current instance has been deleted.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// <para>In a set operation, the current instance is not encrypted.</para>
        /// <para>-OR-</para>
        /// <para>In a set operation, <see cref="Archive"/> is in read-mode, and the current instance has already been successfully decoded.</para>
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
            MausStream.SetPassword(password);
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
            MausStream.SetPassword(password);
        }

        private void _ensureCanSetKey()
        {
            if (_arch == null) throw new ObjectDisposedException(TextResources.ArchiveEntryDeleted);
            if (MausStream.EncryptionFormat == MausEncryptionFormat.None)
                throw new InvalidOperationException(TextResources.NotEncrypted);
            if (_arch.Mode == MauZArchiveMode.Read && MausStream.HeaderIsProcessed)
                throw new InvalidOperationException(TextResources.AlreadyDecryptedArchive);
        }

        internal void EnsureCanWrite()
        {
            if (_arch == null) throw new ObjectDisposedException(TextResources.ArchiveEntryDeleted);
            _arch.EnsureCanWrite();
        }

        internal void EnsureCanRead()
        {
            if (_arch == null) throw new ObjectDisposedException(TextResources.ArchiveEntryDeleted);
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
                DoDelete();
            }
        }

        internal virtual void DoDelete()
        {
            _arch = null;
            MausStream.Close();
            _bufferStream.Close();
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