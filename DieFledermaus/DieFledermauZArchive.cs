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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Globalization;

using DieFledermaus.Globalization;

namespace DieFledermaus
{
    /// <summary>
    /// Represents a DieFledermauZ archive file.
    /// </summary>
    public class DieFledermauZArchive : IDisposable
    {
        private const int _mHead = 0x5a75416d, _mFoot = 0x6d41755a;
        private const int _allEntries = 0x54414403, _curEntry = 0x74616403, _allOffsets = 0x52455603, _curOffset = 0x72657603;
        private const ushort _versionShort = 10, _minVersionShort = _versionShort;

        private bool _leaveOpen;
        private Stream _baseStream;
        /// <summary>
        /// Gets the underlying stream used by the current instance.
        /// </summary>
        public Stream BaseStream { get { return _baseStream; } }

        private bool _headerGotten;

        private readonly List<DieFledermauZItem> _entries = new List<DieFledermauZItem>();
        private readonly Dictionary<string, int> _entryDict = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// Creates a new instance using the specified options.
        /// </summary>
        /// <param name="stream">The stream containing the DieFledermauZ archive.</param>
        /// <param name="mode">Indicates options for accessing the stream.</param>
        /// <param name="leaveOpen"><c>true</c> to leave <paramref name="stream"/> open when the current instance is disposed;
        /// <c>false</c> to close <paramref name="stream"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para><paramref name="mode"/> is <see cref="MauZArchiveMode.Create"/>, and <paramref name="stream"/> does not support writing.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="mode"/> is <see cref="MauZArchiveMode.Read"/>, and <paramref name="stream"/> does not support reading.</para>
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public DieFledermauZArchive(Stream stream, MauZArchiveMode mode, bool leaveOpen)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            switch (mode)
            {
                case MauZArchiveMode.Create:
                    DieFledermausStream.CheckWrite(stream);
                    break;
                case MauZArchiveMode.Read:
                    DieFledermausStream.CheckRead(stream);

                    if (stream.CanSeek)
                    {
                        //TODO: Seeking through the stream
                    }
                    else
                    {
                        //TODO: Copy the stream into a MausBufferStream
                    }
                    _headerGotten = true;
                    throw new NotImplementedException();
                default:
                    throw new InvalidEnumArgumentException(nameof(mode), (int)mode, typeof(MauZArchiveMode));
            }
            Mode = mode;
            _baseStream = stream;
            _leaveOpen = leaveOpen;
            _entriesRO = new EntryList(this);
        }

        internal void Delete(DieFledermauZItem item)
        {
            int index = _entries.IndexOf(item);
            _entries.RemoveAt(index);
            _entryDict.Remove(item.Path);
            for (int i = index; i < _entries.Count; i++)
                _entryDict[_entries[i].Path] = i;
        }

        private readonly EntryList _entriesRO;
        /// <summary>
        /// Gets a collection containing all entries in the current archive.
        /// </summary>
        public EntryList Entries { get { return _entriesRO; } }

        internal readonly MauZArchiveMode Mode;

        internal void EnsureCanWrite()
        {
            if (_baseStream == null)
                throw new ObjectDisposedException(null, TextResources.ArchiveClosed);
            if (Mode == MauZArchiveMode.Read)
                throw new NotSupportedException(TextResources.ArchiveReadMode);
        }

        internal void EnsureCanRead()
        {
            if (_baseStream == null)
                throw new ObjectDisposedException(null, TextResources.ArchiveClosed);
            if (Mode == MauZArchiveMode.Create)
                throw new NotSupportedException(TextResources.ArchiveWriteMode);
        }

        /// <summary>
        /// Adds a new <see cref="DieFledermauZArchiveEntry"/> to the current archive.
        /// </summary>
        /// <param name="path">The path to the entry within the archive's file structure.</param>
        /// <param name="compressionFormat">The compression format of the archive entry.</param>
        /// <param name="encryptionFormat">The encryption format of the archive entry.</param>
        /// <returns>The newly-created <see cref="DieFledermauZArchiveEntry"/> entry.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="path"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidEnumArgumentException">
        /// <para><paramref name="compressionFormat"/> is not a valid <see cref="MausCompressionFormat"/> value.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="encryptionFormat"/> is not a valid <see cref="MausEncryptionFormat"/> value.</para>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para><paramref name="path"/> is not a valid file path.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="path"/> already exists.</para>
        /// </exception>
        public DieFledermauZArchiveEntry Create(string path, MausCompressionFormat compressionFormat, MausEncryptionFormat encryptionFormat)
        {
            EnsureCanWrite();
            ICompressionFormat compFormat;

            switch (compressionFormat)
            {
                case MausCompressionFormat.Deflate:
#if COMPLVL
                    compFormat = new DeflateCompressionFormat() { CompressionLevel = 0 };
#else
                    compFormat = new DeflateCompressionFormat();
#endif
                    break;
                case MausCompressionFormat.None:
                    compFormat = new NoneCompressionFormat();
                    break;
                case MausCompressionFormat.Lzma:
                    compFormat = new LzmaCompressionFormat() { DictionarySize = LzmaDictionarySize.Default };
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(compressionFormat), (int)compressionFormat, typeof(MausCompressionFormat));
            }

            return AddEntry(path, compFormat, encryptionFormat);
        }

        private DieFledermauZArchiveEntry AddEntry(string path, ICompressionFormat compFormat, MausEncryptionFormat encryptionFormat)
        {
            PathSeparator pathSep = new PathSeparator(path);

            if (_entryDict.ContainsKey(path))
                throw new ArgumentException(TextResources.ArchiveExists, nameof(path));

            if (_entryDict.Keys.Any(pathSep.OtherBeginsWith))
                throw new ArgumentException(TextResources.ArchivePathExistingDir, nameof(path));

            if (_entryDict.Keys.Any(pathSep.BeginsWith))
                throw new ArgumentException(TextResources.ArchivePathExistingFileAsDir, nameof(path));

            switch (encryptionFormat)
            {
                case MausEncryptionFormat.Aes:
                case MausEncryptionFormat.None:
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(encryptionFormat), (int)encryptionFormat, typeof(MausEncryptionFormat));
            }

            DieFledermausStream.IsValidFilename(path, true, true, nameof(path));

            DieFledermauZArchiveEntry entry = new DieFledermauZArchiveEntry(this, path, compFormat, encryptionFormat);
            _entryDict.Add(path, _entries.Count);
            _entries.Add(entry);
            return entry;
        }

        private class PathSeparator
        {
            private string _basePath;

            public PathSeparator(string basePath)
            {
                _basePath = basePath;
            }

            private static bool _beginsWith(string basePath, string other)
            {
                return other.StartsWith(basePath, StringComparison.Ordinal) && (basePath.Length == other.Length || other[basePath.Length] == '/');
            }

            public bool BeginsWith(string other)
            {
                return _beginsWith(_basePath, other);
            }

            public bool OtherBeginsWith(string other)
            {
                return _beginsWith(other, _basePath);
            }
        }

        /// <summary>
        /// Determines if the specified value is a valid value for a file path.
        /// </summary>
        /// <param name="path">The value to test.</param>
        /// <returns><c>true</c> if <paramref name="path"/> is a valid path; <c>false</c> if an element in <paramref name="path"/> has a length of 0, has a length
        /// greater than 256 UTF-8 bytes, contains unpaired surrogate characters, contains non-whitespace control characters (non-whitespace characters
        /// between <c>\u0000</c> and <c>\u001f</c> inclusive, or between <c>\u007f</c> and <c>\u009f</c> inclusive), contains only whitespace,
        /// or is "." or ".." (the "current directory" and "parent directory" identifiers).</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="path"/> is <c>null</c>.
        /// </exception>
        public static bool IsValidFilePath(string path)
        {
            return DieFledermausStream.IsValidFilename(path, false, true, nameof(path));
        }

        private const int maxLenEDir = 253;

        /// <summary>
        /// Determines if the specified value is a valid value for an empty directory path.
        /// </summary>
        /// <param name="path">The value to test.</param>
        /// <returns><c>true</c> if <paramref name="path"/> is a valid path; <c>false</c> if an element in <paramref name="path"/> has a length of 0, has a length
        /// greater than 256 UTF-8 bytes, contains unpaired surrogate characters, contains non-whitespace control characters (non-whitespace characters
        /// between <c>\u0000</c> and <c>\u001f</c> inclusive, or between <c>\u007f</c> and <c>\u009f</c> inclusive), contains only whitespace,
        /// or is "." or ".." (the "current directory" and "parent directory" identifiers).</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="path"/> is <c>null</c>.
        /// </exception>
        public static bool IsValidEmptyDirectoryPath(string path)
        {
            return IsValidEmptyDirectoryPath(path, false);
        }

        private static bool IsValidEmptyDirectoryPath(string path, bool throwOnInvalid)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            int end = path[path.Length - 1];
            if (path[end] == '/')
                path = path.Substring(0, end);

            if (DieFledermausStream._textEncoding.GetByteCount(path) > maxLenEDir)
            {
                if (throwOnInvalid)
                    throw new ArgumentException(TextResources.FilenameEDirLengthLong, nameof(path));
                return false;
            }

            return DieFledermausStream.IsValidFilename(path, throwOnInvalid, true, nameof(path));
        }

        /// <summary>
        /// Releases all resources used by the current instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_baseStream == null)
                return;
            try
            {
                if (!disposing)
                    return;

                try
                {
                    WriteFile();
                }
                finally
                {
                    if (!_leaveOpen)
                        _baseStream.Dispose();
                }
            }
            finally
            {
                _baseStream = null;
            }
        }

        private void WriteFile()
        {
            long length = 52;

            DieFledermauZItem[] entries = _entries.ToArray();
            MausBufferStream[] entryStreams = new MausBufferStream[entries.Length];
            byte[][] paths = new byte[entries.Length][];

            for (int i = 0; i < entries.Length; i++)
            {
                var curEntry = entries[i];

                MausBufferStream curStream = curEntry.GetWritten();
                entryStreams[i] = curStream;

                string path;
                if (curEntry.IsFilenameEncrypted)
                    path = "/V" + i.ToString(NumberFormatInfo.InvariantInfo);
                else
                    path = curEntry.Path;
                byte[] curPath = DieFledermausStream._textEncoding.GetBytes(path);
                paths[i] = curPath;

                length += 42L + curStream.Length + (curPath.Length * 2);
            }

#if NOLEAVEOPEN
            BinaryWriter writer = new BinaryWriter(_baseStream);
#else
            using (BinaryWriter writer = new BinaryWriter(_baseStream, DieFledermausStream._textEncoding, true))
#endif
            {
                writer.Write(_mHead);
                writer.Write(_versionShort);

                writer.Write(length);
                long curOffset = 28;
                writer.Write((ushort)0); //TODO: Options, add to curOffset
                writer.Write(entries.LongLength);

                writer.Write(_allEntries);

                long[] offsets = new long[entries.Length];

                for (long i = 0; i < entries.LongLength; i++)
                {
                    var curStream = entryStreams[i];
                    var curEntry = entries[i];
                    offsets[i] = curOffset;
                    writer.Write(_curEntry);
                    writer.Write(i);

                    byte[] pathBytes = paths[i];

                    writer.Write((byte)pathBytes.Length);
                    writer.Write(pathBytes);

                    curStream.BufferCopyTo(_baseStream);

                    curOffset += 13L + pathBytes.Length + curStream.Length;

                    curStream.Dispose();
                    curEntry.DoDelete();
                }

                writer.Write(_allOffsets);

                for (long i = 0; i < entries.LongLength; i++)
                {
                    byte[] pathBytes = paths[i];
                    writer.Write(_curOffset);
                    writer.Write(i);
                    writer.Write((byte)pathBytes.Length);
                    writer.Write(pathBytes);
                    writer.Write(0L);
                    writer.Write(offsets[i]);
                }

                writer.Write(0L);
                writer.Write(curOffset);
                writer.Write(_mFoot);
            }
#if NOLEAVEOPEN
            writer.Flush();
#endif
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        ~DieFledermauZArchive()
        {
            Dispose(false);
        }

        /// <summary>
        /// Represents a list of <see cref="DieFledermauZItem"/> objects.
        /// </summary>
        [DebuggerDisplay(DieFledermausStream.CollectionDebuggerDisplay)]
        [DebuggerTypeProxy(typeof(DebugView))]
        public class EntryList : IList<DieFledermauZItem>, IList
#if IREADONLY
            , IReadOnlyList<DieFledermauZItem>
#endif
        {
            private DieFledermauZArchive _archive;

            internal EntryList(DieFledermauZArchive archive)
            {
                _archive = archive;
            }

            /// <summary>
            /// Get the element at the specified index.
            /// </summary>
            /// <param name="index">The index of the element to get.</param>
            /// <exception cref="ArgumentOutOfRangeException">
            /// <paramref name="index"/> is less than 0 or is greater than <see cref="Count"/>.
            /// </exception>
            public DieFledermauZItem this[int index]
            {
                get { return _archive._entries[index]; }
            }

            DieFledermauZItem IList<DieFledermauZItem>.this[int index]
            {
                get { return _archive._entries[index]; }
                set { throw new NotSupportedException(TextResources.CollectReadOnly); }
            }

            object IList.this[int index]
            {
                get { return _archive._entries[index]; }
                set { throw new NotSupportedException(TextResources.CollectReadOnly); }
            }

            /// <summary>
            /// Gets the number of elements in the list.
            /// </summary>
            public int Count { get { return _archive._entries.Count; } }

            /// <summary>
            /// Gets the entry associated with the specified path.
            /// </summary>
            /// <param name="path">The path to search for in the archive.</param>
            /// <param name="value">When this method returns, contains the value associated with <paramref name="path"/>, or <c>null</c>
            /// if <paramref name="path"/> was not found. This parameter is passed uninitialized.
            /// </param>
            /// <returns><c>true</c> if <paramref name="path"/> was found; <c>false</c> otherwise.</returns>
            /// <exception cref="ArgumentNullException">
            /// <paramref name="path"/> is <c>null</c>.
            /// </exception>
            public bool TryGetEntry(string path, out DieFledermauZItem value)
            {
                if (path == null)
                    throw new ArgumentNullException(nameof(path));

                int index;
                if (_archive._entryDict.TryGetValue(path, out index) || _archive._entryDict.TryGetValue(path + "/", out index))
                {
                    value = this[index];
                    return true;
                }
                value = null;
                return false;
            }

            /// <summary>
            /// Gets a value indicating whether the current instance is entirely frozen against all further changes.
            /// </summary>
            public bool IsFrozen
            {
                get
                {
                    return _archive._baseStream == null || (_archive.Mode != MauZArchiveMode.Create && _archive._headerGotten);
                }
            }

            bool ICollection.IsSynchronized { get { return IsFrozen; } }

            object ICollection.SyncRoot { get { return ((IList)_archive._entries).SyncRoot; } }

            bool IList.IsFixedSize { get { return true; } }

            bool IList.IsReadOnly { get { return true; } }

            bool ICollection<DieFledermauZItem>.IsReadOnly { get { return true; } }

            /// <summary>
            /// Returns the index of the specified element.
            /// </summary>
            /// <param name="item">The element to search for in the list.</param>
            /// <returns>The index of <paramref name="item"/>, if found; otherwise, <c>null</c>.</returns>
            public int IndexOf(DieFledermauZItem item)
            {
                if (item == null) return -1;
                return _archive._entries.IndexOf(item);
            }

            int IList.IndexOf(object value)
            {
                return IndexOf(value as DieFledermauZItem);
            }

            /// <summary>
            /// Determines whether the specified element exists in the list.
            /// </summary>
            /// <param name="item">The element to search for in the list.</param>
            /// <returns><c>true</c> if <paramref name="item"/> was found; <c>false</c> otherwise.</returns>
            public bool Contains(DieFledermauZItem item)
            {
                return item != null && _archive._entries.Contains(item);
            }

            bool IList.Contains(object value)
            {
                return Contains(value as DieFledermauZItem);
            }

            /// <summary>
            /// Copies all elements in the collection to the specified array, starting at the specified index.
            /// </summary>
            /// <param name="array">The array to which the collection will be copied. The array must have zero-based indexing.</param>
            /// <param name="index">The index in <paramref name="array"/> at which copying begins.</param>
            /// <exception cref="ArgumentNullException">
            /// <paramref name="array"/> is <c>null</c>.
            /// </exception>
            /// <exception cref="ArgumentOutOfRangeException">
            /// <paramref name="index"/> is less than 0.
            /// </exception>
            /// <exception cref="ArgumentException">
            /// <paramref name="index"/> plus <see cref="Count"/> is greater than the length of <paramref name="array"/>.
            /// </exception>
            public void CopyTo(DieFledermauZItem[] array, int index)
            {
                _archive._entries.CopyTo(array, index);
            }

            void ICollection.CopyTo(Array array, int index)
            {
                ((ICollection)_archive._entries).CopyTo(array, index);
            }

            #region Not Supported
            void IList<DieFledermauZItem>.Insert(int index, DieFledermauZItem item)
            {
                throw new NotSupportedException(TextResources.CollectReadOnly);
            }

            void IList<DieFledermauZItem>.RemoveAt(int index)
            {
                throw new NotSupportedException(TextResources.CollectReadOnly);
            }

            void ICollection<DieFledermauZItem>.Add(DieFledermauZItem item)
            {
                throw new NotSupportedException(TextResources.CollectReadOnly);
            }

            bool ICollection<DieFledermauZItem>.Remove(DieFledermauZItem item)
            {
                throw new NotSupportedException(TextResources.CollectReadOnly);
            }

            void ICollection<DieFledermauZItem>.Clear()
            {
                throw new NotSupportedException(TextResources.CollectReadOnly);
            }

            int IList.Add(object value)
            {
                throw new NotSupportedException(TextResources.CollectReadOnly);
            }

            void IList.Clear()
            {
                throw new NotSupportedException(TextResources.CollectReadOnly);
            }

            void IList.Remove(object value)
            {
                throw new NotSupportedException(TextResources.CollectReadOnly);
            }

            void IList.Insert(int index, object value)
            {
                throw new NotSupportedException(TextResources.CollectReadOnly);
            }

            void IList.RemoveAt(int index)
            {
                throw new NotSupportedException(TextResources.CollectReadOnly);
            }
            #endregion

            /// <summary>
            /// Returns an enumerator which iterates through the collection.
            /// </summary>
            /// <returns>An enumerator which iterates through the collection.</returns>
            public Enumerator GetEnumerator()
            {
                return new Enumerator(this);
            }

            IEnumerator<DieFledermauZItem> IEnumerable<DieFledermauZItem>.GetEnumerator()
            {
                return GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            /// <summary>
            /// An enumerator which iterates through the collection.
            /// </summary>
            public struct Enumerator : IEnumerator<DieFledermauZItem>
            {
                private IEnumerator<DieFledermauZItem> _enum;

                internal Enumerator(EntryList list)
                {
                    _enum = list._archive._entries.GetEnumerator();
                }

                /// <summary>
                /// Gets the element at the current position in the enumerator.
                /// </summary>
                public DieFledermauZItem Current
                {
                    get { return _enum.Current; }
                }

                object IEnumerator.Current
                {
                    get { return _enum.Current; }
                }

                /// <summary>
                /// Disposes of the current instance.
                /// </summary>
                public void Dispose()
                {
                    if (_enum == null) return;
                    _enum.Dispose();
                    this = default(Enumerator);
                }

                /// <summary>
                /// Advances the enumerator to the next position in the collection.
                /// </summary>
                /// <returns><c>true</c> if the enumerator was successfully advanced; 
                /// <c>false</c> if the enumerator has passed the end of the collection.</returns>
                public bool MoveNext()
                {
                    if (_enum == null)
                        return false;
                    if (_enum.MoveNext())
                        return true;

                    Dispose();
                    return false;
                }

                void IEnumerator.Reset()
                {
                    _enum.Reset();
                }
            }

            private class DebugView
            {
                private EntryList _col;

                public DebugView(EntryList col)
                {
                    _col = col;
                }

                [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
                public DieFledermauZItem[] Items
                {
                    get { return _col.ToArray(); }
                }
            }

            /// <summary>
            /// A collection containing the paths of all entries in the collection.
            /// </summary>
            [DebuggerDisplay(DieFledermausStream.CollectionDebuggerDisplay)]
            [DebuggerTypeProxy(typeof(PathDebugView))]
            public class PathCollection : IList<string>, IList
#if IREADONLY
                , IReadOnlyList<string>
#endif
            {
                private EntryList _list;

                internal PathCollection(EntryList list)
                {
                    _list = list;
                }

                /// <summary>
                /// Gets the element at the specified index.
                /// </summary>
                /// <param name="index">The element at the specified index.</param>
                /// <exception cref="ArgumentOutOfRangeException">
                /// <paramref name="index"/> is less than 0 or is greater than or equal to <see cref="Count"/>.
                /// </exception>
                public string this[int index]
                {
                    get { return _list[index].Path; }
                }

                string IList<string>.this[int index]
                {
                    get { return _list[index].Path; }
                    set { throw new NotSupportedException(TextResources.CollectReadOnly); }
                }

                object IList.this[int index]
                {
                    get { return this[index]; }
                    set { throw new NotSupportedException(TextResources.CollectReadOnly); }
                }

                /// <summary>
                /// Gets the number of elements contained in the list.
                /// </summary>
                public int Count { get { return _list.Count; } }

                /// <summary>
                /// Gets a value indicating whether the current instance is entirely frozen against all further changes.
                /// </summary>
                public bool IsFrozen { get { return _list.IsFrozen; } }

                bool IList.IsFixedSize
                {
                    get { return true; }
                }

                bool IList.IsReadOnly
                {
                    get { return true; }
                }

                bool ICollection<string>.IsReadOnly
                {
                    get { return true; }
                }

                bool ICollection.IsSynchronized
                {
                    get { return _list.IsFrozen; }
                }

                object ICollection.SyncRoot
                {
                    get { return ((IList)_list).SyncRoot; }
                }

                /// <summary>
                /// Returns the index of the specified path.
                /// </summary>
                /// <param name="path">The path to search for in the list.</param>
                /// <returns>The index of <paramref name="path"/>, if found; otherwise, -1.</returns>
                public int IndexOf(string path)
                {
                    if (path == null) return -1;
                    return _list._archive._entries.FindIndex(i => path.Equals(i.Path, StringComparison.Ordinal));
                }

                int IList.IndexOf(object value)
                {
                    return IndexOf(value as string);
                }

                /// <summary>
                /// Gets a value indicating whether the specified path exists in the list.
                /// </summary>
                /// <param name="path">The path to search for in the list.</param>
                /// <returns><c>true</c> if <paramref name="path"/> was found; <c>false</c> otherwise.</returns>
                public bool Contains(string path)
                {
                    return IndexOf(path) >= 0;
                }

                bool IList.Contains(object value)
                {
                    return Contains(value as string);
                }

                /// <summary>
                /// Copies all elements in the collection to the specified array, starting at the specified index.
                /// </summary>
                /// <param name="array">The array to which the collection will be copied. The array must have zero-based indexing.</param>
                /// <param name="index">The index in <paramref name="array"/> at which copying begins.</param>
                /// <exception cref="ArgumentNullException">
                /// <paramref name="array"/> is <c>null</c>.
                /// </exception>
                /// <exception cref="ArgumentOutOfRangeException">
                /// <paramref name="index"/> is less than 0.
                /// </exception>
                /// <exception cref="ArgumentException">
                /// <paramref name="index"/> plus <see cref="Count"/> is greater than the length of <paramref name="array"/>.
                /// </exception>
                public void CopyTo(string[] array, int index)
                {
                    _list._archive._entryDict.Keys.CopyTo(array, index);
                }

                void ICollection.CopyTo(Array array, int index)
                {
                    ((ICollection)_list._archive._entryDict.Keys).CopyTo(array, index);
                }

                #region Not Supported
                int IList.Add(object value)
                {
                    throw new NotSupportedException(TextResources.CollectReadOnly);
                }

                void ICollection<string>.Add(string item)
                {
                    throw new NotSupportedException(TextResources.CollectReadOnly);
                }

                void IList.Clear()
                {
                    throw new NotSupportedException(TextResources.CollectReadOnly);
                }

                void ICollection<string>.Clear()
                {
                    throw new NotSupportedException(TextResources.CollectReadOnly);
                }

                void IList.Insert(int index, object value)
                {
                    throw new NotSupportedException(TextResources.CollectReadOnly);
                }

                void IList<string>.Insert(int index, string item)
                {
                    throw new NotSupportedException(TextResources.CollectReadOnly);
                }

                void IList.Remove(object value)
                {
                    throw new NotSupportedException(TextResources.CollectReadOnly);
                }

                bool ICollection<string>.Remove(string item)
                {
                    throw new NotSupportedException(TextResources.CollectReadOnly);
                }

                void IList.RemoveAt(int index)
                {
                    throw new NotSupportedException(TextResources.CollectReadOnly);
                }

                void IList<string>.RemoveAt(int index)
                {
                    throw new NotSupportedException(TextResources.CollectReadOnly);
                }
                #endregion

                /// <summary>
                /// Returns an enumerator which iterates through the collection.
                /// </summary>
                /// <returns>An enumerator which iterates through the collection.</returns>
                public Enumerator GetEnumerator()
                {
                    return new Enumerator(this);
                }

                IEnumerator<string> IEnumerable<string>.GetEnumerator()
                {
                    return GetEnumerator();
                }

                IEnumerator IEnumerable.GetEnumerator()
                {
                    return GetEnumerator();
                }

                /// <summary>
                /// An enumerator which iterates through the collection.
                /// </summary>
                public struct Enumerator : IEnumerator<string>
                {
                    private IEnumerator<string> _enum;

                    internal Enumerator(PathCollection keys)
                    {
                        _enum = keys._list._archive._entryDict.Keys.GetEnumerator();
                    }

                    /// <summary>
                    /// Gets the element at the current position in the enumerator.
                    /// </summary>
                    public string Current
                    {
                        get { return _enum.Current; }
                    }

                    object IEnumerator.Current
                    {
                        get { return _enum.Current; }
                    }

                    /// <summary>
                    /// Disposes of the current instance.
                    /// </summary>
                    public void Dispose()
                    {
                        if (_enum == null) return;
                        _enum.Dispose();
                        this = default(Enumerator);
                    }

                    /// <summary>
                    /// Advances the enumerator to the next position in the collection.
                    /// </summary>
                    /// <returns><c>true</c> if the enumerator was successfully advanced; 
                    /// <c>false</c> if the enumerator has passed the end of the collection.</returns>
                    public bool MoveNext()
                    {
                        if (_enum == null)
                            return false;
                        if (_enum.MoveNext())
                            return true;

                        Dispose();
                        return false;
                    }

                    void IEnumerator.Reset()
                    {
                        _enum.Reset();
                    }
                }

                private class PathDebugView
                {
                    private PathCollection _col;

                    public PathDebugView(PathCollection col)
                    {
                        _col = col;
                    }

                    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
                    public string[] Items
                    {
                        get { return _col.ToArray(); }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Indicates options for a <see cref="DieFledermauZArchive"/>.
    /// </summary>
    public enum MauZArchiveMode
    {
        /// <summary>
        /// The <see cref="DieFledermauZArchive"/> is in write-only mode.
        /// </summary>
        Create,
        /// <summary>
        /// The <see cref="DieFledermauZArchive"/> is in read-only mode.
        /// </summary>
        Read,
    }
}
