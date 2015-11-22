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

        private Dictionary<string, DieFledermauZArchiveEntry> _entries = new Dictionary<string, DieFledermauZArchiveEntry>(StringComparer.Ordinal);
        private Dictionary<DieFledermauZArchiveEntry, string> _entryKeys = new Dictionary<DieFledermauZArchiveEntry, string>();

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

                    throw new NotImplementedException();
                default:
                    throw new InvalidEnumArgumentException(nameof(mode), (int)mode, typeof(MauZArchiveMode));
            }
            Mode = mode;
            _baseStream = stream;
            _leaveOpen = leaveOpen;
            _entryDict = new EntryDictionary(this);
        }

        internal void Delete(DieFledermauZItem item)
        {
            var entry = item as DieFledermauZArchiveEntry;
            if (item != null)
            {
                _entries.Remove(entry.Path);
                _entryKeys.Remove(entry);
            }
        }

        private EntryDictionary _entryDict;
        /// <summary>
        /// Gets a dictionary containing all entries in the current archive.
        /// </summary>
        public EntryDictionary Entries { get { return _entryDict; } }

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

            if (_entries.Keys.Any(pathSep.OtherBeginsWith))
                throw new ArgumentException(TextResources.ArchivePathExistingDir, nameof(path));

            if (_entries.Keys.Any(pathSep.BeginsWith))
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

            if (_entries.ContainsKey(path))
                throw new ArgumentException(TextResources.ArchiveExists, nameof(path));

            DieFledermauZArchiveEntry entry = new DieFledermauZArchiveEntry(this, path, compFormat, encryptionFormat);
            _entries.Add(path, entry);
            _entryKeys.Add(entry, path);
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

            DieFledermauZArchiveEntry[] entries = _entries.Values.ToArray();
            MausBufferStream[] entryStreams = new MausBufferStream[entries.Length];
            byte[][] paths = new byte[entries.Length][];

            for (int i = 0; i < entries.Length; i++)
            {
                var curEntry = entries[i];

                MausBufferStream curStream = curEntry.GetWritten();
                entryStreams[i] = curStream;

                string path;
                if (curEntry.EncryptedOptions != null && curEntry.EncryptedOptions.Contains(MausOptionToEncrypt.Filename))
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
                    Debug.Assert(curOffset == _baseStream.Length, "Offset mismatch", "Expected offset: {0}, actual offset: {1}", curOffset, _baseStream.Length);
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

                Debug.Assert(curOffset == _baseStream.Length, "All-offset mismatch", "Expected offset: {0}, actual offset: {1}", curOffset, _baseStream.Length);
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
            Debug.Assert(length == _baseStream.Length, "Length mismatch", "Expected length: {0}, actual length: {1}", length, _baseStream.Length);
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        ~DieFledermauZArchive()
        {
            Dispose(false);
        }

        /// <summary>
        /// A dictionary of all entries in a <see cref="DieFledermauZArchive"/>.
        /// </summary>
        [DebuggerTypeProxy(typeof(DebugView))]
        [DebuggerDisplay(DieFledermausStream.CollectionDebuggerDisplay)]
        public sealed class EntryDictionary : IDictionary<string, DieFledermauZArchiveEntry>, IDictionary
#if IREADONLY
            , IReadOnlyDictionary<string, DieFledermauZArchiveEntry>
#endif
        {
            private DieFledermauZArchive _archive;

            internal EntryDictionary(DieFledermauZArchive archive)
            {
                _archive = archive;
                _keys = new KeyCollection(this);
                _values = new ValueCollection(this);
            }

            /// <summary>
            /// Gets the element with the specified key.
            /// </summary>
            /// <param name="key">The key of the element to get.</param>
            /// <exception cref="ArgumentNullException">
            /// <paramref name="key"/> is <c>null</c>.
            /// </exception>
            /// <exception cref="KeyNotFoundException">
            /// <paramref name="key"/> was not found in the dictionary.
            /// </exception>
            public DieFledermauZArchiveEntry this[string key]
            {
                get { return _archive._entries[key]; }
            }

            DieFledermauZArchiveEntry IDictionary<string, DieFledermauZArchiveEntry>.this[string key]
            {
                get { return _archive._entries[key]; }
                set { throw new NotSupportedException(TextResources.CollectReadOnly); }
            }

            object IDictionary.this[object key]
            {
                get { return ((IDictionary)_archive._entries)[key]; }
                set { throw new NotSupportedException(TextResources.CollectReadOnly); }
            }

            /// <summary>
            /// Gets the number of elements contained in the dictionary.
            /// </summary>
            public int Count { get { return _archive._entries.Count; } }

            private KeyCollection _keys;
            /// <summary>
            /// Gets a collection containing all keys in the dictionary.
            /// </summary>
            public KeyCollection Keys { get { return _keys; } }

            ICollection<string> IDictionary<string, DieFledermauZArchiveEntry>.Keys { get { return _keys; } }
            ICollection IDictionary.Keys { get { return _keys; } }
#if IREADONLY
            IEnumerable<string> IReadOnlyDictionary<string, DieFledermauZArchiveEntry>.Keys { get { return _keys; } }
#endif
            private ValueCollection _values;
            /// <summary>
            /// Gets a collection containing all values in the dictionary.
            /// </summary>
            public ValueCollection Values { get { return _values; } }

            ICollection<DieFledermauZArchiveEntry> IDictionary<string, DieFledermauZArchiveEntry>.Values { get { return _values; } }
            ICollection IDictionary.Values { get { return _values; } }
#if IREADONLY
            IEnumerable<DieFledermauZArchiveEntry> IReadOnlyDictionary<string, DieFledermauZArchiveEntry>.Values { get { return _values; } }
#endif

            bool ICollection<KeyValuePair<string, DieFledermauZArchiveEntry>>.IsReadOnly
            {
                get { return true; }
            }

            bool IDictionary.IsReadOnly
            {
                get { return true; }
            }

            bool IDictionary.IsFixedSize
            {
                get { return false; }
            }

            object ICollection.SyncRoot
            {
                get { return ((IDictionary)_archive._entries).SyncRoot; }
            }

            bool ICollection.IsSynchronized
            {
                get { return false; }
            }

            /// <summary>
            /// Determines whether the specified key exists in the dictionary.
            /// </summary>
            /// <param name="key">The key to search for in the dictionary.</param>
            /// <returns><c>true</c> if <paramref name="key"/> was found; <c>false</c> otherwise.</returns>
            /// <exception cref="ArgumentNullException">
            /// <paramref name="key"/> is <c>null</c>.
            /// </exception>
            public bool ContainsKey(string key)
            {
                return _archive._entries.ContainsKey(key);
            }

            /// <summary>
            /// Determines whether the specified value exists in the dictionary.
            /// </summary>
            /// <param name="value">The value to search for in the dictionary.</param>
            /// <returns><c>true</c> if <paramref name="value"/> was found; <c>false</c> otherwise.</returns>
            public bool ContainsValue(DieFledermauZArchiveEntry value)
            {
                return value != null && _archive._entryKeys.ContainsKey(value);
            }

            /// <summary>
            /// Gets the key associated with the specified value.
            /// </summary>
            /// <param name="key">The key to search for in the dictionary.</param>
            /// <param name="value">When this method returns, contains the value associated with <paramref name="key"/>, or <c>null</c> if
            /// <paramref name="key"/> was not found. This parameter is passed uninitialized.</param>
            /// <returns><c>true</c> if <paramref name="key"/> was found; <c>false</c> otherwise.</returns>
            /// <exception cref="ArgumentNullException">
            /// <paramref name="key"/> is <c>null</c>.
            /// </exception>
            public bool TryGetValue(string key, out DieFledermauZArchiveEntry value)
            {
                return _archive._entries.TryGetValue(key, out value);
            }

            /// <summary>
            /// Gets the value associated with the specified key.
            /// </summary>
            /// <param name="value">The value to search for in the dictionary.</param>
            /// <param name="key">When this method returns, contains the key associated with <paramref name="value"/>, or <c>null</c> if
            /// <paramref name="value"/> was not found. This parameter is passed uninitialized.</param>
            /// <returns><c>true</c> if <paramref name="value"/> was found; <c>false</c> otherwise.</returns>
            /// <exception cref="ArgumentNullException">
            /// <paramref name="value"/> is <c>null</c>.
            /// </exception>
            public bool TryGetKey(DieFledermauZArchiveEntry value, out string key)
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                return _archive._entryKeys.TryGetValue(value, out key);
            }

            void ICollection<KeyValuePair<string, DieFledermauZArchiveEntry>>.CopyTo(KeyValuePair<string, DieFledermauZArchiveEntry>[] array, int arrayIndex)
            {
                ((ICollection<KeyValuePair<string, DieFledermauZArchiveEntry>>)_archive._entries).CopyTo(array, arrayIndex);
            }

            /// <summary>
            /// Returns an enumerator which iterates through the dictionary.
            /// </summary>
            /// <returns>An enumerator which iterates through the dictionary.</returns>
            public Enumerator GetEnumerator()
            {
                return new Enumerator(this, true);
            }

            IEnumerator<KeyValuePair<string, DieFledermauZArchiveEntry>> IEnumerable<KeyValuePair<string, DieFledermauZArchiveEntry>>.GetEnumerator()
            {
                return GetEnumerator();
            }

            IDictionaryEnumerator IDictionary.GetEnumerator()
            {
                return new Enumerator(this, true);
            }

            #region Not Supported
            void ICollection<KeyValuePair<string, DieFledermauZArchiveEntry>>.Add(KeyValuePair<string, DieFledermauZArchiveEntry> item)
            {
                throw new NotSupportedException(TextResources.CollectReadOnly);
            }

            void IDictionary<string, DieFledermauZArchiveEntry>.Add(string key, DieFledermauZArchiveEntry value)
            {
                throw new NotSupportedException(TextResources.CollectReadOnly);
            }

            void ICollection<KeyValuePair<string, DieFledermauZArchiveEntry>>.Clear()
            {
                throw new NotSupportedException(TextResources.CollectReadOnly);
            }

            bool ICollection<KeyValuePair<string, DieFledermauZArchiveEntry>>.Contains(KeyValuePair<string, DieFledermauZArchiveEntry> item)
            {
                return ((ICollection<KeyValuePair<string, DieFledermauZArchiveEntry>>)_archive._entries).Contains(item);
            }

            bool ICollection<KeyValuePair<string, DieFledermauZArchiveEntry>>.Remove(KeyValuePair<string, DieFledermauZArchiveEntry> item)
            {
                throw new NotSupportedException(TextResources.CollectReadOnly);
            }

            bool IDictionary<string, DieFledermauZArchiveEntry>.Remove(string key)
            {
                throw new NotSupportedException(TextResources.CollectReadOnly);
            }

            bool IDictionary.Contains(object key)
            {
                throw new NotImplementedException();
            }

            void IDictionary.Add(object key, object value)
            {
                throw new NotImplementedException();
            }

            void IDictionary.Clear()
            {
                throw new NotImplementedException();
            }

            void IDictionary.Remove(object key)
            {
                throw new NotImplementedException();
            }

            void ICollection.CopyTo(Array array, int index)
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }
            #endregion

            /// <summary>
            /// An enumerator which iterates through the dictionary.
            /// </summary>
            public struct Enumerator : IEnumerator<KeyValuePair<string, DieFledermauZArchiveEntry>>, IDictionaryEnumerator
            {
                private bool _generic;

                private IEnumerator<KeyValuePair<string, DieFledermauZArchiveEntry>> _enum;
                private KeyValuePair<string, DieFledermauZArchiveEntry> _current;
                private DictionaryEntry _curEntry;
                private object _curObj;

                internal Enumerator(EntryDictionary dict, bool generic)
                {
                    _enum = dict._archive._entries.GetEnumerator();
                    _current = default(KeyValuePair<string, DieFledermauZArchiveEntry>);
                    _curEntry = default(DictionaryEntry);
                    _curObj = null;
                    _generic = generic;
                }

                /// <summary>
                /// Gets the element at the current position in the enumerator.
                /// </summary>
                public KeyValuePair<string, DieFledermauZArchiveEntry> Current { get { return _current; } }

                object IEnumerator.Current { get { return _curObj; } }

                DictionaryEntry IDictionaryEnumerator.Entry { get { return _curEntry; } }

                object IDictionaryEnumerator.Key
                {
                    get { return _curEntry.Key; }
                }

                object IDictionaryEnumerator.Value
                {
                    get { return _curEntry.Value; }
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
                    if (_enum == null) return false;

                    if (!_enum.MoveNext())
                    {
                        Dispose();
                        return false;
                    }

                    _current = _enum.Current;
                    _curEntry = new DictionaryEntry(_curEntry.Key, _curEntry.Value);
                    _curObj = _generic ? (object)_current : _curEntry;
                    return true;
                }

                void IEnumerator.Reset()
                {
                    _enum.Reset();
                }
            }

            /// <summary>
            /// A collection of all keys in the dictionary.
            /// </summary>
            [DebuggerTypeProxy(typeof(KeyDebugView))]
            [DebuggerDisplay(DieFledermausStream.CollectionDebuggerDisplay)]
            public class KeyCollection : ICollection<string>, ICollection
#if IREADONLY
                , IReadOnlyCollection<string>
#endif
            {
                private EntryDictionary _dict;

                internal KeyCollection(EntryDictionary dict)
                {
                    _dict = dict;
                }

                /// <summary>
                /// Gets the number of elements in the collection.
                /// </summary>
                public int Count { get { return _dict._archive._entries.Count; } }

                bool ICollection<string>.IsReadOnly { get { return true; } }

                /// <summary>
                /// Determines if the specified key exists in the current instance.
                /// </summary>
                /// <param name="key">The key to search for in the collection.</param>
                /// <returns><c>true</c> if <paramref name="key"/> was found; <c>false</c> otherwise.</returns>
                public bool Contains(string key)
                {
                    return key != null && _dict.ContainsKey(key);
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
                    _dict._archive._entries.Keys.CopyTo(array, index);
                }

                object ICollection.SyncRoot
                {
                    get { return ((IDictionary)_dict).SyncRoot; }
                }

                bool ICollection.IsSynchronized
                {
                    get { return false; }
                }

                void ICollection.CopyTo(Array array, int index)
                {
                    ((ICollection)_dict._archive._entries.Keys).CopyTo(array, index);
                }

                #region Not Supported
                void ICollection<string>.Add(string item)
                {
                    throw new NotSupportedException(TextResources.CollectReadOnly);
                }

                void ICollection<string>.Clear()
                {
                    throw new NotSupportedException(TextResources.CollectReadOnly);
                }

                bool ICollection<string>.Remove(string item)
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

                    internal Enumerator(KeyCollection keys)
                    {
                        _enum = keys._dict._archive._entries.Keys.GetEnumerator();
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

                private class KeyDebugView
                {
                    private KeyCollection _col;

                    public KeyDebugView(KeyCollection col)
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

            /// <summary>
            /// A collection of all values in the dictionary.
            /// </summary>
            [DebuggerTypeProxy(typeof(ValueDebugView))]
            [DebuggerDisplay(DieFledermausStream.CollectionDebuggerDisplay)]
            public class ValueCollection : ICollection<DieFledermauZArchiveEntry>, ICollection
#if IREADONLY
                , IReadOnlyCollection<DieFledermauZArchiveEntry>
#endif
            {
                private EntryDictionary _dict;

                internal ValueCollection(EntryDictionary dict)
                {
                    _dict = dict;
                }

                /// <summary>
                /// Gets the number of elements in the collection.
                /// </summary>
                public int Count { get { return _dict._archive._entries.Count; } }

                bool ICollection<DieFledermauZArchiveEntry>.IsReadOnly { get { return true; } }

                /// <summary>
                /// Determines if the specified value exists in the current instance.
                /// </summary>
                /// <param name="value">The value to search for in the collection.</param>
                /// <returns><c>true</c> if <paramref name="value"/> was found; <c>false</c> otherwise.</returns>
                public bool Contains(DieFledermauZArchiveEntry value)
                {
                    return value != null && _dict.ContainsValue(value);
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
                public void CopyTo(DieFledermauZArchiveEntry[] array, int index)
                {
                    _dict._archive._entries.Values.CopyTo(array, index);
                }

                object ICollection.SyncRoot
                {
                    get { return ((IDictionary)_dict).SyncRoot; }
                }

                bool ICollection.IsSynchronized
                {
                    get { return false; }
                }

                void ICollection.CopyTo(Array array, int index)
                {
                    ((ICollection)_dict._archive._entries.Keys).CopyTo(array, index);
                }

                #region Not Supported
                void ICollection<DieFledermauZArchiveEntry>.Add(DieFledermauZArchiveEntry item)
                {
                    throw new NotSupportedException(TextResources.CollectReadOnly);
                }

                void ICollection<DieFledermauZArchiveEntry>.Clear()
                {
                    throw new NotSupportedException(TextResources.CollectReadOnly);
                }

                bool ICollection<DieFledermauZArchiveEntry>.Remove(DieFledermauZArchiveEntry item)
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

                IEnumerator<DieFledermauZArchiveEntry> IEnumerable<DieFledermauZArchiveEntry>.GetEnumerator()
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
                public struct Enumerator : IEnumerator<DieFledermauZArchiveEntry>
                {
                    private IEnumerator<DieFledermauZArchiveEntry> _enum;

                    internal Enumerator(ValueCollection values)
                    {
                        _enum = values._dict._archive._entries.Values.GetEnumerator();
                    }

                    /// <summary>
                    /// Gets the element at the current position in the enumerator.
                    /// </summary>
                    public DieFledermauZArchiveEntry Current
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

                private class ValueDebugView
                {
                    private ValueCollection _col;

                    public ValueDebugView(ValueCollection col)
                    {
                        _col = col;
                    }

                    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
                    public DieFledermauZArchiveEntry[] Items
                    {
                        get { return _col.ToArray(); }
                    }
                }
            }

            private class DebugView
            {
                private EntryDictionary _col;

                public DebugView(EntryDictionary col)
                {
                    _col = col;
                }

                [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
                public KeyValuePair<string, DieFledermauZArchiveEntry>[] Items
                {
                    get { return _col.ToArray(); }
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
