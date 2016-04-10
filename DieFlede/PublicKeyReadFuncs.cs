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
using System.Collections.Generic;
using System.IO;
using System.Text;

using DieFledermaus.Cli.Globalization;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.EC;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;

namespace DieFledermaus.Cli
{
    internal static class PublicKeyReadFuncs
    {
        internal const string KeyFmtRSA = "ssh-rsa";
        internal const string KeyFmtDSA = "ssh-dss";
        internal const string KeyFmtECDSA = "ecdsa-sha2-";

        public static bool TryGetPublicKey(TextReader reader, BigInteger index, out AsymmetricKeyParameter publicKey)
        {
            BigInteger counter = BigInteger.ValueOf(-1);
            publicKey = null;
            foreach (Tuple<string, AsymmetricKeyParameter, string> curVal in AuthorizedKeysParser(reader))
            {
                counter = counter.Add(BigInteger.One);

                if (counter.CompareTo(index) < 0)
                    continue;

                if (curVal.Item2 == null)
                {
                    Console.Error.WriteLine(TextResources.UnknownKeyType, curVal.Item1);
                    return false;
                }

                publicKey = curVal.Item2;
                return true;
            }

            Console.Error.WriteLine(TextResources.IndexFile, index);
            return false;
        }

        private static IEnumerable<Tuple<string, AsymmetricKeyParameter, string>> AuthorizedKeysParser(TextReader reader)
        {
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();

                if (line.Length == 0 || line[0] == '#')
                    continue;

                string[] words = line.Split((char[])null, 3, StringSplitOptions.RemoveEmptyEntries);

                if (words.Length < 2) throw new InvalidDataException(TextResources.SignBadPublic);
                byte[] buffer;
                try
                {
                    buffer = Convert.FromBase64String(words[1]);
                }
                catch (FormatException)
                {
                    throw new InvalidDataException(TextResources.SignBadPublic);
                }

                int curPos = 0;
                string type = words[0], comment = words.Length == 2 ? string.Empty : words[2];

                if (type.Length > 64 || type != ReadString(buffer, ref curPos))
                    throw new InvalidDataException(TextResources.SignBadPublic);

                if (type == KeyFmtRSA)
                {
                    yield return new Tuple<string, AsymmetricKeyParameter, string>(type, ReadRSAParams(buffer, ref curPos), comment);
                    continue;
                }
                if (type == KeyFmtDSA)
                {
                    yield return new Tuple<string, AsymmetricKeyParameter, string>(type, ReadDSAParams(buffer, ref curPos), comment);
                    continue;
                }
                if (type.StartsWith(KeyFmtECDSA, StringComparison.OrdinalIgnoreCase))
                {
                    yield return new Tuple<string, AsymmetricKeyParameter, string>(type, ReadECParams(type, buffer, ref curPos), comment);
                    continue;
                }

                yield return new Tuple<string, AsymmetricKeyParameter, string>(type, null, comment);
            }
        }

        internal static RsaKeyParameters ReadRSAParams(byte[] buffer, ref int curPos)
        {
            BigInteger exp = ReadBigInteger(buffer, ref curPos);
            BigInteger mod = ReadBigInteger(buffer, ref curPos);
            if (curPos != buffer.Length) throw new InvalidDataException(TextResources.SignBadPublic);
            return new RsaKeyParameters(false, mod, exp);
        }

        internal static DsaPublicKeyParameters ReadDSAParams(byte[] buffer, ref int curPos)
        {
            BigInteger p = ReadBigInteger(buffer, ref curPos);
            BigInteger q = ReadBigInteger(buffer, ref curPos);
            BigInteger g = ReadBigInteger(buffer, ref curPos);
            BigInteger y = ReadBigInteger(buffer, ref curPos);
            if (curPos != buffer.Length) throw new InvalidDataException(TextResources.SignBadPublic);
            return new DsaPublicKeyParameters(y, new DsaParameters(p, q, g));
        }

        internal static ECPublicKeyParameters ReadECParams(string type, byte[] buffer, ref int curPos)
        {
            string s = ReadString(buffer, ref curPos);
            byte[] qBytes = ReadBuffer(buffer, ref curPos);

            const int typePrefixLen = 11;
            if (curPos != buffer.Length || !type.Substring(typePrefixLen).Equals(s, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(TextResources.SignBadPublic);

            X9ECParameters ecSpec;
            DerObjectIdentifier dId;

            if (s == "nistp256" || s == "nitsp384" || s == "nistp521")
            {
                dId = NistNamedCurves.GetOid("P-" + s.Substring(5));
                ecSpec = NistNamedCurves.GetByOid(dId);
                if (ecSpec == null)
                    throw new InvalidDataException(TextResources.SignBadPublic);
            }
            else
            {
                try
                {
                    dId = new DerObjectIdentifier(s);
                }
                catch
                {
                    throw new InvalidDataException(TextResources.SignBadPublic);
                }

                ecSpec = ECNamedCurveTable.GetByOid(dId);

                if (ecSpec == null)
                {
                    ecSpec = CustomNamedCurves.GetByOid(dId);

                    if (ecSpec == null)
                        throw new InvalidDataException(TextResources.UnknownCurve);
                }
            }

            try
            {
                return new ECPublicKeyParameters("ECDSA", new X9ECPoint(ecSpec.Curve, qBytes).Point, dId);
            }
            catch
            {
                throw new InvalidDataException(TextResources.SignBadPublic);
            }
        }

        internal static int ReadInt(byte[] buffer, ref int curPos)
        {
            if (curPos + sizeof(int) > buffer.Length)
                throw new InvalidDataException(TextResources.SignBadPublic);
            return (buffer[curPos++] << 24) | (buffer[curPos++] << 16) | (buffer[curPos++] << 8) | buffer[curPos++];
        }

        internal static string ReadString(byte[] buffer, ref int curPos)
        {
            int len = ReadInt(buffer, ref curPos);
            if (len <= 0 || curPos + len > buffer.Length)
                throw new InvalidDataException(TextResources.SignBadPublic);
            string returner = Encoding.UTF8.GetString(buffer, curPos, len);
            curPos += len;
            return returner;
        }

        internal static BigInteger ReadBigInteger(byte[] buffer, ref int curPos)
        {
            int len = ReadInt(buffer, ref curPos);
            if (len <= 0 || curPos + len > buffer.Length)
                throw new InvalidDataException(TextResources.SignBadPublic);
            BigInteger returner = new BigInteger(buffer, curPos, len);
            curPos += len;
            return returner;
        }

        internal static byte[] ReadBuffer(byte[] buffer, ref int curPos)
        {
            int len = ReadInt(buffer, ref curPos);
            if (len <= 0 || curPos + len > buffer.Length)
                throw new InvalidDataException(TextResources.SignBadPublic);

            byte[] returner = new byte[len];
            Array.Copy(buffer, curPos, returner, 0, len);
            curPos += len;
            return returner;
        }
    }
}
