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
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace DieFledermaus
{
    internal static class RsaSigningProvider
    {
        public static byte[] GenerateSignature(byte[] hash, RsaKeyParameters keyParam, DerObjectIdentifier derId)
        {
            RsaBlindedEngine _engine = new RsaBlindedEngine();
            _engine.Init(true, keyParam);

            byte[] message = Pkcs7Provider.AddPadding(GetDerEncoded(hash, derId), _engine.GetInputBlockSize());

            return _engine.ProcessBlock(message, 0, message.Length);
        }

        public static bool VerifyHash(byte[] hash, byte[] signature, RsaKeyParameters keyParam, DerObjectIdentifier derId)
        {
            RsaBlindedEngine _engine = new RsaBlindedEngine();
            _engine.Init(false, keyParam);

            byte[] sig;
            try
            {
                sig = Pkcs7Provider.RemovePadding(_engine.ProcessBlock(signature, 0, signature.Length), _engine.GetOutputBlockSize());
            }
            catch (Exception)
            {
                return false;
            }
            if (sig.Length == hash.Length)
                return DieFledermausStream.CompareBytes(hash, sig);
            byte[] expected = GetDerEncoded(hash, derId);

            if (sig.Length == expected.Length)
                return DieFledermausStream.CompareBytes(expected, sig);

            if (sig.Length != expected.Length - 2)
                return false;

            int sigOffset = sig.Length - hash.Length - 2;
            int expectedOffset = expected.Length - hash.Length - 2;

            expected[1] -= 2;      // adjust lengths
            expected[3] -= 2;

            for (int i = 0; i < hash.Length; i++)
            {
                if (sig[sigOffset + i] != expected[expectedOffset + i])
                    return false;
            }

            for (int i = 0; i < sigOffset; i++)
            {
                if (sig[i] != expected[i])  // check header less NULL
                    return false;
            }

            return true;
        }

        private static byte[] GetDerEncoded(byte[] hash, DerObjectIdentifier derId)
        {
            AlgorithmIdentifier _id = new AlgorithmIdentifier(derId, DerNull.Instance);
            DigestInfo dInfo = new DigestInfo(_id, hash);

            return dInfo.GetDerEncoded();
        }
    }

    internal static class Pkcs7Provider
    {
        public static byte[] AddPadding(byte[] unpadded, int blockSize)
        {
            int oldLen = unpadded.Length;
            int addCount = blockSize - (oldLen % blockSize);

            byte[] paddedMessage = new byte[oldLen + addCount];
            Array.Copy(unpadded, paddedMessage, oldLen);

            for (int i = oldLen; i < paddedMessage.Length; i++)
                paddedMessage[i] = (byte)addCount;
            return paddedMessage;
        }

        public static byte[] RemovePadding(byte[] padded, int blockSize)
        {
            if (padded.Length < blockSize || (padded.Length % blockSize) != 0)
                return padded;

            byte padCount = padded[padded.Length - 1];
            if (padCount > blockSize || padCount == 0)
                return padded;

            for (int i = 2; i <= padCount; i++)
            {
                if (padded[padded.Length - i] != padCount)
                    return padded;
            }

            byte[] unpaddedMessage = new byte[padded.Length - padCount];

            Array.Copy(padded, unpaddedMessage, unpaddedMessage.Length);
            return unpaddedMessage;
        }
    }
}
