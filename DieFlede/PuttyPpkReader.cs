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
using System.Globalization;
using System.IO;
using System.Text;

using DieFledermaus.Cli.Globalization;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.IO;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;

namespace DieFledermaus.Cli
{
    /// <summary>
    /// Loads PuTTY .PPK files (currently only does SSH-2 files, not SSH1)
    /// </summary>
    internal class PuttyPpkReader
    {
        private Stream _stream;
        private IPasswordFinder _passFinder;

        public PuttyPpkReader(Stream stream, IPasswordFinder passwordFinder)
        {
            _stream = stream;
            _passFinder = passwordFinder;
        }

        internal const string PpkFileHeader2 = "PuTTY-User-Key-File-2: ";
        internal const string EncryptHeader = "Encryption: ";
        internal const string CommentHeader = "Comment: ";
        internal const string PubLinesHead = "Public-Lines: ";
        internal const string PrivLinesHead = "Private-Lines: ";
        internal const string PrivMacHead = "Private-MAC: ";
        internal const string EncFmtNone = "none";
        internal const string EncFmtAes = "aes256-cbc";
        internal const int LineLen = 64;

        internal const string PpkFileHeader1 = "SSH PRIVATE KEY FILE FORMAT 1.1";

        private string _comment;
        public string Comment { get { return _comment; } }
        private string _encType;
        public string EncType { get { return _encType; } }
        private string _keyType;
        private byte[] _pubBytes;
        public byte[] _privBytesEncrypted;
        private byte[] _privBytes;
        private byte[] _mac;

        public bool Init()
        {
            using (StreamReader reader = new StreamReader(_stream, Encoding.UTF8, true, Program.BufferSize, true))
            {
                string line = reader.ReadLine();

                if (line.Equals(PpkFileHeader1, StringComparison.OrdinalIgnoreCase))
                    throw new NotSupportedException(TextResources.BadPpkVersion);
                else if (!line.StartsWith(PpkFileHeader2, StringComparison.Ordinal))
                    return false;
                else
                {
                    string keyType = line.Substring(PpkFileHeader2.Length);
                    if (keyType.Equals(Program.KeyFmtRSA, StringComparison.Ordinal) || keyType.Equals(Program.KeyFmtDSA, StringComparison.Ordinal) ||
                        keyType.StartsWith(Program.KeyFmtECDSA, StringComparison.Ordinal))
                        _keyType = keyType;
                    else
                        throw new InvalidDataException();
                }

                line = reader.ReadLine();
                {
                    if (!line.StartsWith(EncryptHeader, StringComparison.Ordinal))
                        throw new InvalidDataException();

                    _encType = line.Substring(EncryptHeader.Length);
                }
                line = reader.ReadLine();
                {
                    if (!line.StartsWith(CommentHeader, StringComparison.Ordinal))
                        throw new InvalidDataException();

                    _comment = line.Substring(CommentHeader.Length);
                }
                line = reader.ReadLine();
                {
                    int pubLines;
                    if (!line.StartsWith(PubLinesHead, StringComparison.Ordinal) ||
                        !int.TryParse(line.Substring(PubLinesHead.Length), NumberStyles.None, NumberFormatInfo.InvariantInfo, out pubLines))
                        throw new InvalidDataException();

                    _pubBytes = ReadLines(pubLines, reader);
                }
                line = reader.ReadLine();
                {
                    int privLines;
                    if (!line.StartsWith(PrivLinesHead, StringComparison.Ordinal) ||
                        !int.TryParse(line.Substring(PrivLinesHead.Length), NumberStyles.None, NumberFormatInfo.InvariantInfo, out privLines))
                        throw new InvalidDataException();

                    _privBytesEncrypted = ReadLines(privLines, reader);
                }
                line = reader.ReadLine();
                {
                    const int MacLineLen = 53;
                    const int StartLineLen = MacLineLen - 40;
                    const int ByteCount = 20;
                    if (!line.StartsWith(PrivMacHead, StringComparison.Ordinal) || line.Length != MacLineLen)
                        throw new InvalidDataException();

                    _mac = new byte[ByteCount];

                    for (int i = 0; i < ByteCount; i++)
                    {
                        byte curByte;
                        if (!byte.TryParse(line.Substring(StartLineLen + (i << 1), 2), NumberStyles.AllowHexSpecifier, NumberFormatInfo.InvariantInfo, out curByte))
                            throw new InvalidDataException();
                        _mac[i] = curByte;
                    }
                }
                return true;
            }
        }

        public AsymmetricKeyParameter ReadPublicKey()
        {
            if (_encType.Equals(EncFmtNone, StringComparison.Ordinal))
                return ReadKeyPair().Public;

            return _readPublicKey();
        }

        private AsymmetricKeyParameter _readPublicKey()
        {
            int offset = 0;
            string type = Program.ReadString(_pubBytes, ref offset);
            if (!type.Equals(_keyType, StringComparison.Ordinal))
                throw new InvalidDataException();

            if (_keyType.Equals(Program.KeyFmtRSA, StringComparison.Ordinal))
                return Program.ReadRSAParams(_pubBytes, ref offset);
            if (_keyType.StartsWith(Program.KeyFmtECDSA, StringComparison.Ordinal))
                return Program.ReadECParams(_keyType, _pubBytes, ref offset);

            return Program.ReadDSAParams(_pubBytes, ref offset);
        }

        public AsymmetricCipherKeyPair ReadKeyPair()
        {
            byte[] generatedMac;
            if (!_encType.Equals(EncFmtNone, StringComparison.Ordinal))
            {
                string passphrase = new string(_passFinder.GetPassword());
                byte[] key = GetKey(passphrase);

                var blockCipher = new CbcBlockCipher(new AesEngine());
                if ((_privBytesEncrypted.Length % blockCipher.GetBlockSize()) != 0)
                    throw new InvalidDataException();
                blockCipher.Init(false, new ParametersWithIV(new KeyParameter(key), new byte[blockCipher.GetBlockSize()]));

                using (CipherStream cs = new CipherStream(new MemoryStream(_privBytesEncrypted), new BufferedBlockCipher(blockCipher), null))
                using (MemoryStream outStream = new MemoryStream())
                {
                    cs.CopyTo(outStream);
                    _privBytes = outStream.ToArray();
                }
                generatedMac = GetMac(passphrase, _keyType, _encType, _comment, _pubBytes, _privBytes);

                for (int i = 0; i < _mac.Length; i++)
                {
                    if (generatedMac[i] != _mac[i])
                        throw new InvalidCipherTextException(TextResources.BadPassword);
                }
            }
            else
            {
                _privBytes = _privBytesEncrypted;
                generatedMac = GetMac(string.Empty, _keyType, _encType, _comment, _pubBytes, _privBytes);

                for (int i = 0; i < _mac.Length; i++)
                {
                    if (generatedMac[i] != _mac[i])
                        throw new InvalidDataException();
                }
            }

            AsymmetricKeyParameter pubKey = _readPublicKey();
            if (pubKey == null) throw new InvalidDataException();

            if (_keyType.Equals(Program.KeyFmtRSA, StringComparison.Ordinal))
            {
                RsaKeyParameters pubRsa = (RsaKeyParameters)pubKey;
                RsaPrivateCrtKeyParameters privRsa;

                int offset = 0;
                BigInteger privEx = Program.ReadBigInteger(_privBytes, ref offset);
                BigInteger p = Program.ReadBigInteger(_privBytes, ref offset);
                BigInteger q = Program.ReadBigInteger(_privBytes, ref offset);
                BigInteger qInv = Program.ReadBigInteger(_privBytes, ref offset);

                if (offset < _privBytes.Length && _encType.Equals(EncFmtNone, StringComparison.Ordinal))
                    throw new InvalidDataException();

                if (qInv.CompareTo(q.ModInverse(p)) != 0 || pubRsa.Modulus.CompareTo(p.Multiply(q)) != 0)
                    throw new InvalidDataException();

                BigInteger pSub1 = p.Subtract(BigInteger.One);
                BigInteger qSub1 = q.Subtract(BigInteger.One);
                BigInteger gcd = pSub1.Gcd(qSub1);
                BigInteger lcm = pSub1.Divide(gcd).Multiply(qSub1);

                BigInteger d = pubRsa.Exponent.ModInverse(lcm);

                BigInteger dP = d.Remainder(pSub1);
                BigInteger dQ = d.Remainder(qSub1);
                privRsa = new RsaPrivateCrtKeyParameters(pubRsa.Modulus, pubRsa.Exponent, privEx, p, q, dP, dQ, qInv);

                return new AsymmetricCipherKeyPair(pubRsa, privRsa);
            }
            else if (_keyType.StartsWith(Program.KeyFmtECDSA, StringComparison.Ordinal))
            {
                ECPublicKeyParameters pubEC = (ECPublicKeyParameters)pubKey;
                int offset = 0;
                BigInteger d = Program.ReadBigInteger(_privBytes, ref offset);

                if (!pubEC.Q.Equals(new Org.BouncyCastle.Math.EC.Multiplier.FixedPointCombMultiplier().Multiply(pubEC.Parameters.G, d)))
                    throw new InvalidDataException();

                ECPrivateKeyParameters privEC = new ECPrivateKeyParameters(pubEC.AlgorithmName, d, pubEC.PublicKeyParamSet);

                return new AsymmetricCipherKeyPair(pubEC, privEC);
            }
            else //DSA
            {
                DsaPublicKeyParameters pubDsa = (DsaPublicKeyParameters)pubKey;
                DsaPrivateKeyParameters privDsa;
                {
                    int offset = 0;
                    BigInteger x = Program.ReadBigInteger(_privBytes, ref offset);
                    if (offset < _privBytes.Length && _encType.Equals(EncFmtNone, StringComparison.Ordinal))
                        throw new InvalidDataException();

                    if (pubDsa.Y.CompareTo(pubDsa.Parameters.G.ModPow(x, pubDsa.Parameters.P)) != 0)
                        throw new InvalidDataException();

                    privDsa = new DsaPrivateKeyParameters(x, pubDsa.Parameters);
                }
                return new AsymmetricCipherKeyPair(pubDsa, privDsa);
            }
        }

        private static readonly byte[] _keyZero = new byte[4], _keyOne = { 0, 0, 0, 1 };

        internal static byte[] GetKey(string passphrase)
        {
            byte[] passBytes = Encoding.UTF8.GetBytes(passphrase);
            byte[] bothDigests;

            Sha1Digest digest = new Sha1Digest();
            digest.BlockUpdate(_keyZero, 0, 4);
            digest.BlockUpdate(passBytes, 0, passBytes.Length);
            bothDigests = new byte[digest.GetDigestSize() << 1];
            digest.DoFinal(bothDigests, 0);

            digest.Reset();
            digest.BlockUpdate(_keyOne, 0, 4);
            digest.BlockUpdate(passBytes, 0, passBytes.Length);

            digest.DoFinal(bothDigests, digest.GetDigestSize());

            byte[] key = new byte[32];
            Array.Copy(bothDigests, key, 32);
            return key;
        }

        internal static byte[] GetMac(string passphrase, string _keyType, string _encType, string _comment, byte[] _pubBytes, byte[] _privBytes)
        {
            byte[] key;
            {
                Sha1Digest sha1 = new Sha1Digest();
                byte[] buffer = Encoding.UTF8.GetBytes("putty-private-key-file-mac-key" + passphrase);
                sha1.BlockUpdate(buffer, 0, buffer.Length);
                key = new byte[sha1.GetDigestSize()];
                sha1.DoFinal(key, 0);
            }
            byte[] blob;
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                WriteBigEndian(writer, _keyType);
                WriteBigEndian(writer, _encType);
                WriteBigEndian(writer, _comment);
                WriteBigEndian(writer, _pubBytes);
                WriteBigEndian(writer, _privBytes);
                blob = ms.ToArray();
            }

            HMac hMac = new HMac(new Sha1Digest());
            hMac.Init(new KeyParameter(key));
            hMac.BlockUpdate(blob, 0, blob.Length);

            byte[] output = new byte[hMac.GetMacSize()];
            hMac.DoFinal(output, 0);

            return output;
        }

        private static void WriteBigEndian(BinaryWriter writer, int value)
        {
            writer.Write(new byte[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value });
        }

        private static void WriteBigEndian(BinaryWriter writer, byte[] value)
        {
            WriteBigEndian(writer, value.Length);
            writer.Write(value);
        }

        private static void WriteBigEndian(BinaryWriter writer, BigInteger value)
        {
            byte[] exp = value.ToByteArray();
            WriteBigEndian(writer, exp.Length);
            writer.Write(exp);
        }

        private static void WriteBigEndian(BinaryWriter writer, string value)
        {
            WriteBigEndian(writer, Encoding.UTF8.GetByteCount(value));
            writer.Write(value.ToCharArray());
        }

        private byte[] ReadLines(int lineCount, StreamReader reader)
        {
            StringBuilder lineBuilder = new StringBuilder();
            for (int i = 0; i < lineCount; i++)
            {
                string curLine = reader.ReadLine();
                if (curLine.Length > LineLen)
                    throw new InvalidDataException();

                lineBuilder.Append(curLine);
            }
            string result = lineBuilder.ToString();
            try
            {
                return Convert.FromBase64String(result);
            }
            catch
            {
                throw new InvalidDataException();
            }
        }
    }
}
