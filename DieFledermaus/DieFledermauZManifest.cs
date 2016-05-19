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
using System.IO;
using System.Linq;

using DieFledermaus.Globalization;

using Org.BouncyCastle.Crypto.Parameters;

namespace DieFledermaus
{
    internal class DieFledermauZManifest : DieFledermauZItem, IMausSign
    {
        internal const string Filename = "/Manifest.dat";
        internal static readonly byte[] FilenameBytes = Filename.Select(i => (byte)i).ToArray();

        internal DieFledermauZManifest(DieFledermauZArchive archive)
            : base(archive, Filename, new NoneCompressionFormat(), MausEncryptionFormat.None)
        {
        }

        internal DieFledermauZManifest(DieFledermauZArchive archive, DieFledermausStream stream, long curOffset, long realOffset)
            : base(archive, Filename, Filename, stream, curOffset, realOffset)
        {
            if (stream.EncryptionFormat != MausEncryptionFormat.None || stream.CompressionFormat != MausCompressionFormat.None ||
                stream.Comment != null || stream.CreatedTime.HasValue || stream.ModifiedTime.HasValue)
                throw new InvalidDataException(TextResources.ManifestBad);
        }

        private MausBufferStream _readStream;

        internal void LoadData(DieFledermauZItem[] entries)
        {
            if (_readStream == null)
            {
                SeekToFile();
                _readStream = new MausBufferStream();
                MausStream.BufferCopyTo(_readStream);
                _readStream.Reset();
            }
            if (!_readStream.CanRead)
                return;
            _readStream.Reset();

            _manifest = new MauZManifest(_readStream, entries);
            _readStream.Close();
        }

        internal override bool IsFilenameEncrypted
        {
            get { return false; }
        }

        private MauZManifest _manifest;
        public MauZManifest Manifest { get { return _manifest; } }

        #region RSA Signature
        public RsaKeyParameters RSASignParameters
        {
            get { return MausStream.RSASignParameters; }
            set { MausStream.RSASignParameters = value; }
        }

        public string RSASignId
        {
            get { return MausStream.RSASignId; }
            set { MausStream.RSASignId = value; }
        }

        public bool IsRSASigned { get { return MausStream.IsRSASigned; } }

        public bool IsRSASignVerified { get { return MausStream.IsRSASignVerified; } }

        public bool VerifyRSASignature()
        {
            return MausStream.VerifyRSASignature();
        }
        #endregion

        #region DSA Signature
        public DsaKeyParameters DSASignParameters
        {
            get { return MausStream.DSASignParameters; }
            set { MausStream.DSASignParameters = value; }
        }

        public string DSASignId
        {
            get { return MausStream.DSASignId; }
            set { MausStream.DSASignId = value; }
        }

        public bool IsDSASigned { get { return MausStream.IsDSASigned; } }

        public bool IsDSASignVerified { get { return MausStream.IsDSASignVerified; } }

        public bool VerifyDSASignature()
        {
            return MausStream.VerifyDSASignature();
        }
        #endregion

        #region ECDSA Signature
        public ECKeyParameters ECDSASignParameters
        {
            get { return MausStream.ECDSASignParameters; }
            set { MausStream.ECDSASignParameters = value; }
        }

        public string ECDSASignId
        {
            get { return MausStream.ECDSASignId; }
            set { MausStream.ECDSASignId = value; }
        }

        public bool IsECDSASigned { get { return MausStream.IsECDSASigned; } }

        public bool IsECDSASignVerified { get { return MausStream.IsECDSASignVerified; } }

        public bool VerifyECDSASignature()
        {
            return MausStream.VerifyECDSASignature();
        }
        #endregion

        internal MausBufferStream BuildSelf(DieFledermauZItem[] entries, byte[][] paths)
        {
            _manifest = new MauZManifest(entries, paths);

            _manifest.Write(MausStream);
            return GetWritten();
        }
    }

    /// <summary>
    /// Represents a DieFledermauZ manifest file.
    /// </summary>
    public class MauZManifest : IList<MauZManifestEntry>, IList
#if IREADONLY
        , IReadOnlyList<MauZManifestEntry>
#endif
    {
        const int _sigAll = 0x47495303, _sigCur = 0x67697303;
        internal const ushort _versionShort = 1, _versionShortMin = 1;

        private MauZManifestEntry[] _entries;

        #region Constructors
        internal MauZManifest(DieFledermauZItem[] entries, byte[][] paths)
        {
            _entries = new MauZManifestEntry[entries.Length];

            long length = entries.Length;

            for (long i = 0; i < entries.Length; i++)
                _entries[i] = new MauZManifestEntry(paths[i], entries[i].CompressedHash);
        }

        /// <summary>
        /// Creates a new instance using entries derived from the specified collection.
        /// </summary>
        /// <param name="collection">The collection whose <see cref="DieFledermausStream.Filename"/> and <see cref="DieFledermausStream.CompressedHash"/>
        /// values will be copied to the new collection.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="collection"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para><paramref name="collection"/> contains one or more <see langword="null"/> elements or elements with <see langword="null"/>
        /// <see cref="DieFledermausStream.Filename"/> or <see cref="DieFledermausStream.CompressedHash"/> values.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="collection"/> is empty.</para>
        /// </exception>
        public MauZManifest(IEnumerable<DieFledermausStream> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            _entries = collection.Select(GetEntry).ToArray();
            if (_entries.Length == 0)
                throw new ArgumentException(TextResources.CollectEmpty, nameof(collection));
        }

        private static MauZManifestEntry GetEntry(DieFledermausStream stream)
        {
            const string collection = "collection";

            if (stream == null)
                throw new ArgumentException(TextResources.CollectContainsNull, collection);
            string filename = stream.Filename;
            if (filename == null)
                throw new ArgumentException(TextResources.CollectManifestNullFilename, collection);
            byte[] hash = stream.CompressedHash;
            if (hash == null)
                throw new ArgumentException(TextResources.CollectManifestNullHash, collection);

            return new MauZManifestEntry(filename, hash);
        }

        /// <summary>
        /// Creates a new instance using entries derived from the specified collection.
        /// </summary>
        /// <param name="collection">The collection whose <see cref="DieFledermauZItem.Path"/> and <see cref="DieFledermauZItem.CompressedHash"/>
        /// values will be copied to the new collection.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="collection"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para><paramref name="collection"/> contains one or more <see langword="null"/> elements or elements with <see langword="null"/>
        /// <see cref="DieFledermauZItem.Path"/> or <see cref="DieFledermauZItem.CompressedHash"/> values.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="collection"/> is empty.</para>
        /// </exception>
        public MauZManifest(IEnumerable<DieFledermauZItem> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            _entries = collection.Select(GetEntry).ToArray();
            if (_entries.Length == 0)
                throw new ArgumentException(TextResources.CollectEmpty, nameof(collection));
        }

        private static MauZManifestEntry GetEntry(DieFledermauZItem item)
        {
            const string collection = "collection";

            if (item == null)
                throw new ArgumentException(TextResources.CollectContainsNull, collection);

            string path = item.Path;
            if (path == null)
                throw new ArgumentException(TextResources.CollectManifestNullPath, collection);

            byte[] hash = item.CompressedHash;
            if (hash == null)
                throw new ArgumentException(TextResources.CollectManifestNullHash, collection);

            return new MauZManifestEntry(path, hash);
        }

        /// <summary>
        /// Creates a new instance containing elements copied from the specified collection.
        /// </summary>
        /// <param name="collection">The collection whose elements will be copied to the new list.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="collection"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para><paramref name="collection"/> contains one or more elements with <see cref="MauZManifestEntry.IsValid"/> set to <see langword="false"/>.</para>
        /// <para>-OR-</para>
        /// <para><paramref name="collection"/> is empty.</para>
        /// </exception>
        public MauZManifest(IEnumerable<MauZManifestEntry> collection)
        {
            if (collection is MauZManifest)
                _entries = collection.ToArray();
            else if (collection == null)
                throw new ArgumentNullException(nameof(collection));
            else
            {
                _entries = collection.Select(GetEntry).ToArray();

                if (_entries.Length == 0)
                    throw new ArgumentException(TextResources.CollectEmpty, nameof(collection));
            }
        }

        private static MauZManifestEntry GetEntry(MauZManifestEntry item)
        {
            const string collection = "collection";

            if (!item.IsValid)
                throw new ArgumentException(TextResources.CollectManifestInvalid, collection);

            return item;
        }

        /// <summary>
        /// Loads a new instance from the specified stream.
        /// </summary>
        /// <param name="stream">The stream from which the manifest will be loaded.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support reading.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public MauZManifest(Stream stream)
            : this(stream, null)
        {
        }

        internal MauZManifest(Stream stream, DieFledermauZItem[] entries)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            DieFledermausStream.CheckStreamRead(stream);

            using (Use7BinaryReader reader = new Use7BinaryReader(stream, true))
            {
                if (reader.ReadInt32() != _sigAll)
                    throw new InvalidDataException(entries == null ? TextResources.ManifestCurBad : TextResources.ManifestBad);
                ushort _version = reader.ReadUInt16();
                if (_version < _versionShortMin)
                    throw new InvalidDataException(entries == null ? TextResources.ManifestCurVersionTooLow : TextResources.ManifestVersionTooLow);
                if (_version > _versionShort)
                    throw new InvalidDataException(entries == null ? TextResources.ManifestCurVersionTooHigh : TextResources.ManifestVersionTooHigh);

                long itemCount = reader.ReadInt64();
                if (itemCount <= 0 || (entries != null && itemCount != entries.Length - 1))
                    throw new InvalidDataException(TextResources.ManifestBad);

                _entries = new MauZManifestEntry[itemCount];

                for (long i = 0; i < itemCount; i++)
                {
                    if (reader.ReadInt32() != _sigCur)
                        throw new InvalidDataException(entries == null ? TextResources.ManifestCurBad : TextResources.ManifestBad);
                    long index = reader.ReadInt64();

                    if (index < 0 || index >= itemCount || _entries[index].IsValid)
                        throw new InvalidDataException(entries == null ? TextResources.ManifestCurBad : TextResources.ManifestBad);

                    byte[] gotPath = DieFledermausStream.ReadBytes8Bit(reader);
                    byte[] gotHash = DieFledermausStream.ReadBytes8Bit(reader);

                    _entries[index] = new MauZManifestEntry(gotPath, gotHash);

                    if (entries != null)
                    {
                        var curEntry = entries[index];

                        string path = DieFledermausStream._textEncoding.GetString(gotPath);

                        if (path != curEntry.OriginalPath || !DieFledermausStream.CompareBytes(curEntry.CompressedHash, gotHash))
                            throw new InvalidDataException(entries == null ? TextResources.ManifestCurBad : TextResources.ManifestBad);
                    }
                }
            }
        }
        #endregion

        /// <summary>
        /// Gets the element at the specified index.
        /// </summary>
        /// <param name="index">The index of the element to get.</param>
        /// <returns>The element at <paramref name="index"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than 0 or is greater than or equal to <see cref="Count"/>.
        /// </exception>
        public MauZManifestEntry this[int index]
        {
            get
            {
                if (index < 0 || index >= _entries.Length)
                    throw new ArgumentOutOfRangeException(nameof(index), TextResources.OutOfRangeIndex);
                return _entries[index];
            }
        }

        MauZManifestEntry IList<MauZManifestEntry>.this[int index]
        {
            get { return this[index]; }
            set { throw new NotSupportedException(TextResources.CollectReadOnly); }
        }

        object IList.this[int index]
        {
            get { return this[index]; }
            set { throw new NotSupportedException(TextResources.CollectReadOnly); }
        }

        /// <summary>
        /// Gets the number of elements contained in the current list.
        /// </summary>
        public int Count { get { return _entries.Length; } }


        bool ICollection<MauZManifestEntry>.IsReadOnly { get { return true; } }

        bool IList.IsReadOnly { get { return true; } }

        bool IList.IsFixedSize { get { return true; } }

        private object _syncRoot;
        object ICollection.SyncRoot
        {
            get
            {
                if (_syncRoot == null)
                    System.Threading.Interlocked.CompareExchange(ref _syncRoot, new object(), null);
                return _syncRoot;
            }
        }

        bool ICollection.IsSynchronized { get { return true; } }

        /// <summary>
        /// Determines if the specified value exists in the list.
        /// </summary>
        /// <param name="value">The value to search for in the list.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> was found; <see langword="false"/> otherwise.</returns>
        public bool Contains(MauZManifestEntry value)
        {
            return IndexOf(value) >= 0;
        }

        bool IList.Contains(object value)
        {
            if (value is MauZManifestEntry)
                return Contains((MauZManifestEntry)value);
            return false;
        }

        /// <summary>
        /// Gets the index of the specified value.
        /// </summary>
        /// <param name="value">The value to search for in the list.</param>
        /// <returns>The index of <paramref name="value"/>, if found; otherwise, -1.</returns>
        public int IndexOf(MauZManifestEntry value)
        {
            return Array.IndexOf(_entries, value);
        }

        int IList.IndexOf(object value)
        {
            if (value is MauZManifestEntry)
                return IndexOf((MauZManifestEntry)value);
            return -1;
        }

        /// <summary>
        /// Copies all elements in the collection to the specified array, starting at the specified index.
        /// </summary>
        /// <param name="array">The array to which the collection will be copied. The array must have zero-based indexing.</param>
        /// <param name="arrayIndex">The index in <paramref name="array"/> at which copying begins.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="array"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="arrayIndex"/> is less than 0.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="arrayIndex"/> plus <see cref="Count"/> is greater than the length of <paramref name="array"/>.
        /// </exception>
        public void CopyTo(MauZManifestEntry[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex), TextResources.OutOfRangeLessThanZero);
            if (arrayIndex + _entries.Length > array.Length)
                throw new ArgumentException(TextResources.BadIndexRange);

            Array.Copy(_entries, 0, array, arrayIndex, _entries.Length);
        }

        void ICollection.CopyTo(Array array, int index)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            if (array.Rank != 1 || array.GetLowerBound(0) != 0)
                throw new ArgumentException(TextResources.CollectBadArray, nameof(array));
            try
            {
                _entries.CopyTo(array, index);
            }
            catch (ArrayTypeMismatchException)
            {
                throw new ArgumentException(TextResources.CollectBadArrayType, nameof(array));
            }
        }

        /// <summary>
        /// Returns an enumerator which iterates through the collection.
        /// </summary>
        /// <returns>An enumerator which iterates through the collection.</returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<MauZManifestEntry> IEnumerable<MauZManifestEntry>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Writes the current manifest to the specified stream.
        /// </summary>
        /// <param name="stream">The stream to which the current instance will be written.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="stream"/> does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// <paramref name="stream"/> is closed.
        /// </exception>
        public void Write(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            DieFledermausStream.CheckStreamWrite(stream);
            using (Use7BinaryWriter writer = new Use7BinaryWriter(stream, true))
            {
                long length = _entries.Length;
                writer.Write(_sigAll);
                writer.Write(_versionShort);
                writer.Write(length);

                for (long i = 0; i < length; i++)
                {
                    var curEntry = _entries[i];
                    writer.Write(_sigCur);
                    writer.Write(i);

                    byte[] curPath = DieFledermausStream._textEncoding.GetBytes(curEntry.Path);
                    writer.Write((byte)curPath.Length);
                    writer.Write(curPath);
                    byte[] curHash = curEntry.Hash;
                    writer.Write((byte)curHash.Length);
                    writer.Write(curHash);
                }
            }
        }

        #region Not Supported
        void IList<MauZManifestEntry>.Insert(int index, MauZManifestEntry item)
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }

        void IList<MauZManifestEntry>.RemoveAt(int index)
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }

        void ICollection<MauZManifestEntry>.Add(MauZManifestEntry item)
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }

        void ICollection<MauZManifestEntry>.Clear()
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }

        bool ICollection<MauZManifestEntry>.Remove(MauZManifestEntry item)
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

        void IList.Insert(int index, object value)
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }

        void IList.Remove(object value)
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }

        void IList.RemoveAt(int index)
        {
            throw new NotSupportedException(TextResources.CollectReadOnly);
        }
        #endregion

        /// <summary>
        /// An enumerator which iterates through the collection.
        /// </summary>
        public struct Enumerator : IEnumerator<MauZManifestEntry>
        {
            private int _curIndex;
            private MauZManifestEntry _current;
            private MauZManifestEntry[] _array;

            internal Enumerator(MauZManifest manifest)
            {
                _curIndex = -1;
                _current = default(MauZManifestEntry);
                _array = manifest._entries;
            }

            /// <summary>
            /// Gets the element at the current position in the enumerator.
            /// </summary>
            public MauZManifestEntry Current
            {
                get { return _current; }
            }

            object IEnumerator.Current
            {
                get { return _current; }
            }

            /// <summary>
            /// Disposes of the current instance.
            /// </summary>
            public void Dispose()
            {
                this = default(Enumerator);
            }

            /// <summary>
            /// Advances the enumerator to the next position in the collection.
            /// </summary>
            /// <returns><see langword="true"/> if the enumerator was successfully advanced; 
            /// <see langword="false"/> if the enumerator has passed the end of the collection.</returns>
            public bool MoveNext()
            {
                if (_array == null) return false;
                if (++_curIndex >= _array.Length)
                {
                    Dispose();
                    return false;
                }
                _current = _array[_curIndex];
                return true;
            }

            void IEnumerator.Reset()
            {
                throw new InvalidOperationException();
            }
        }
    }

    /// <summary>
    /// Represents a single entry in a DieFledermauZ manifest.
    /// </summary>
    public struct MauZManifestEntry : IEquatable<MauZManifestEntry>
    {
        internal MauZManifestEntry(byte[] path, byte[] hash)
        {
            _path = DieFledermausStream._textEncoding.GetString(path);
            _hash = hash;
        }

        /// <summary>
        /// Creates a new instance using the specified values.
        /// </summary>
        /// <param name="path">The path of the manifest entry.</param>
        /// <param name="hash">The compressed hash or HMAC of the manifest entry.</param>
        public MauZManifestEntry(string path, byte[] hash)
        {
            _path = path;

            if (hash == null)
                _hash = null;
            else
                _hash = (byte[])hash.Clone();
        }

        /// <summary>
        /// Gets a value if the current instante is valid. Returns <see langword="true"/> if both 
        /// <see cref="Path"/> and <see cref="Hash"/> are not <see langword="null"/> and have a length greater than 0;
        /// <see langword="false"/> otherwise.
        /// </summary>
        public bool IsValid { get { return _path != null && _path.Length != 0 && _hash != null && _hash.Length != 0; } }

        private string _path;
        /// <summary>
        /// Gets the path or filename of the current manifest entry.
        /// </summary>
        public string Path { get { return _path; } }

        private byte[] _hash;
        /// <summary>
        /// Gets the hash of the current manifest entry.
        /// </summary>
        public byte[] Hash { get { return _hash == null ? null : (byte[])_hash.Clone(); } }

        /// <summary>
        /// Determines if the current value is equal to the specified other <see cref="MauZManifestEntry"/> value.
        /// </summary>
        /// <param name="other">The other <see cref="MauZManifestEntry"/> to compare.</param>
        /// <returns><see langword="true"/> if the current instance is equal to <paramref name="other"/>; <see langword="false"/> otherwise.</returns>
        public bool Equals(MauZManifestEntry other)
        {
            return _path == other._path && DieFledermausStream.CompareBytes(_hash, other._hash);
        }

        /// <summary>
        /// Determines if the current value is equal to the specified other object.
        /// </summary>
        /// <param name="obj">The other object to compare.</param>
        /// <returns><see langword="true"/> if <paramref name="obj"/> is a <see cref="MauZManifestEntry"/> equal to the current value;
        /// <see langword="false"/> otherwise.</returns>
        public override bool Equals(object obj)
        {
            return obj is MauZManifestEntry && Equals((MauZManifestEntry)obj);
        }

        /// <summary>
        /// Returns a hash code for the current value.
        /// </summary>
        /// <returns>A hash code for the current value.</returns>
        public override int GetHashCode()
        {
            int hash = _path == null ? 0 : _path.GetHashCode();

            return hash + DieFledermausStream.GetStructHashCode(_hash);
        }

        /// <summary>
        /// Determines equality of two <see cref="MauZManifestEntry"/> objects.
        /// </summary>
        /// <param name="m1">A <see cref="MauZManifestEntry"/> to compare.</param>
        /// <param name="m2">A <see cref="MauZManifestEntry"/> to compare.</param>
        /// <returns><see langword="true"/> if <paramref name="m1"/> is equal to <paramref name="m2"/>; <see langword="false"/> otherwise.</returns>
        public static bool operator ==(MauZManifestEntry m1, MauZManifestEntry m2)
        {
            return m1.Equals(m2);
        }

        /// <summary>
        /// Determines inequality of two <see cref="MauZManifestEntry"/> objects.
        /// </summary>
        /// <param name="m1">A <see cref="MauZManifestEntry"/> to compare.</param>
        /// <param name="m2">A <see cref="MauZManifestEntry"/> to compare.</param>
        /// <returns><see langword="true"/> if <paramref name="m1"/> is not equal to <paramref name="m2"/>; <see langword="false"/> otherwise.</returns>
        public static bool operator !=(MauZManifestEntry m1, MauZManifestEntry m2)
        {
            return !m1.Equals(m2);
        }
    }
}
