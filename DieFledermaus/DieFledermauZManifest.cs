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
                throw new InvalidDataException(TextResources.InvalidDataMauZ);
        }

        private MausBufferStream _readStream;

        internal void LoadData(DieFledermauZItem[] entries)
        {
            if (_readStream == null)
            {
                SeekToFile();
                _readStream = new MausBufferStream();
                MausStream.BufferCopyTo(_readStream);
            }
            if (!_readStream.CanRead)
                return;

            _readStream.Reset();

            using (BinaryReader reader = new BinaryReader(_readStream))
            {
                bool[] results = new bool[entries.Length];

                if (reader.ReadInt32() != _sigAll) throw new InvalidDataException(TextResources.InvalidDataMauZ);
                long itemCount = reader.ReadInt64();
                if (itemCount != results.Length - 1) throw new InvalidDataException(TextResources.InvalidDataMauZ);

                long curOffset = 0;

                for (long i = 0; i < itemCount; i++)
                {
                    if (reader.ReadInt32() != _sigCur) throw new InvalidDataException(TextResources.InvalidDataMauZ);
                    long index = reader.ReadInt64();
                    if (results[index]) throw new InvalidDataException(TextResources.InvalidDataMauZ);
                    results[index] = true;
                    var entry = entries[index];

                    if (entry == this || !DieFledermauZArchive.GetString(reader, ref curOffset).Equals(entry.OriginalPath, StringComparison.Ordinal))
                        throw new InvalidDataException(TextResources.InvalidDataMauZ);

                    byte[] hash;
                    if (entry.EncryptionFormat == MausEncryptionFormat.None)
                        hash = entry.Hash;
                    else
                        hash = entry.HMAC;
                    byte[] buffer = DieFledermausStream.ReadBytes(reader, hash.Length);

                    if (!DieFledermausStream.CompareBytes(hash, buffer))
                        throw new InvalidDataException(TextResources.InvalidDataMauZ);
                }
            }
        }

        internal override bool IsFilenameEncrypted
        {
            get { return false; }
        }

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

        public byte[] RSASignIdBytes
        {
            get { return MausStream.RSASignIdBytes; }
            set { MausStream.RSASignIdBytes = value; }
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

        public byte[] DSASignIdBytes
        {
            get { return MausStream.DSASignIdBytes; }
            set { MausStream.DSASignIdBytes = value; }
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

        public byte[] ECDSASignIdBytes
        {
            get { return MausStream.ECDSASignIdBytes; }
            set { MausStream.ECDSASignIdBytes = value; }
        }

        public bool IsECDSASigned { get { return MausStream.IsECDSASigned; } }

        public bool IsECDSASignVerified { get { return MausStream.IsECDSASignVerified; } }

        public bool VerifyECDSASignature()
        {
            return MausStream.VerifyECDSASignature();
        }
        #endregion

        const int _sigAll = 0x47495303, _sigCur = 0x67697303;

        internal MausBufferStream BuildSelf(DieFledermauZItem[] entries, byte[][] paths)
        {
            using (BinaryWriter writer = new BinaryWriter(MausStream))
            {
                writer.Write(_sigAll);
                writer.Write(entries.LongLength);

                for (long i = 0; i < entries.LongLength; i++)
                {
                    writer.Write(_sigCur);
                    writer.Write(i);
                    writer.Write((byte)paths[i].Length);
                    writer.Write(paths[i]);

                    if (entries[i].EncryptionFormat == MausEncryptionFormat.None)
                        writer.Write(entries[i].Hash);
                    else
                        writer.Write(entries[i].HMAC);
                }

                return GetWritten();
            }
        }
    }
}
