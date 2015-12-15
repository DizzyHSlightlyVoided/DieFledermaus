using System;
using System.Collections;
using System.IO;
using System.Text;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.TeleTrust;
using Org.BouncyCastle.Asn1.Utilities;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;

namespace DieFledermaus
{
    class RsaSigningProvider : ISigner
    {
        public RsaSigningProvider(IDigest digest, DerObjectIdentifier digestOid)
        {
            _digest = digest;
            _id = new AlgorithmIdentifier(digestOid, DerNull.Instance);
        }

        private IDigest _digest;
        private AlgorithmIdentifier _id;
        private RsaBlindedEngine _engine = new RsaBlindedEngine();
        private Pkcs7Padding _padding = new Pkcs7Padding();

        public string AlgorithmName
        {
            get { return _digest.AlgorithmName + "withRSA"; }
        }

        private bool _forSigning;

        public void Init(bool forSigning, ICipherParameters parameters)
        {
            AsymmetricKeyParameter key = (AsymmetricKeyParameter)parameters;

            if (key.IsPrivate != forSigning)
            {
                if (key.IsPrivate)
                    throw new ArgumentException("Private key needed.", nameof(parameters));
                throw new ArgumentException("Public key needed for verification.", nameof(parameters));
            }
            _forSigning = forSigning;
            Reset();
            _engine.Init(forSigning, parameters);
        }

        public void BlockUpdate(byte[] input, int inOff, int length)
        {
            _digest.BlockUpdate(input, inOff, length);
        }

        public void Update(byte input)
        {
            _digest.Update(input);
        }

        public void Reset()
        {
            _hash = null;
            _digest.Reset();
        }

        private byte[] _hash;

        public byte[] GetFinalHash()
        {
            if (_hash == null)
            {
                _hash = new byte[_digest.GetDigestSize()];
                _digest.DoFinal(_hash, 0);
            }
            return _hash;
        }

        public byte[] GenerateSignature()
        {

            byte[] message = Pkcs7Provider.AddPadding(GetDerEncoded(GetFinalHash()), _engine.GetInputBlockSize());

            return _engine.ProcessBlock(message, 0, message.Length);
        }

        public bool VerifySignature(byte[] signature)
        {
            byte[] hash = new byte[_digest.GetDigestSize()];

            _digest.DoFinal(hash, 0);

            return VerifyHash(hash, signature);
        }

        public bool VerifyHash(byte[] hash, byte[] signature)
        {
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
            byte[] expected = GetDerEncoded(hash);

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

        private byte[] GetDerEncoded(byte[] hash)
        {
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
