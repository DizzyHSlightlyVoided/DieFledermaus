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
using System.Security.Cryptography;

namespace DieFledermaus
{
    partial class DieFledermausStream
    {
        private byte[] ComputeHash(Stream inputStream)
        {
            using (SHA512Managed shaHash = new SHA512Managed())
                return shaHash.ComputeHash(inputStream);
        }

        private byte[] ComputeHmac(Stream inputStream)
        {
            using (HMACSHA512 hmac = new HMACSHA512(_key))
                return hmac.ComputeHash(inputStream);
        }

        private byte[] FillBuffer(int length)
        {
            byte[] buffer = new byte[length];
#if NOCRYPTOCLOSE
            RandomNumberGenerator rng = RandomNumberGenerator.Create();
#else
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
#endif
            {
                rng.GetBytes(buffer);
            }
            return buffer;
        }

        private void _setKeySizes(int keySize)
        {
            if (keySize <= 0)
                _keySizes = null;
            else
                _keySizes = new KeySizes(keySize, keySize, 0);
        }

        private SymmetricAlgorithm GetAlgorithm()
        {
            SymmetricAlgorithm alg = Aes.Create();
            alg.Key = _key;
            alg.IV = _iv;
            return alg;
        }

        private QuickBufferStream Decrypt()
        {
            QuickBufferStream output = new QuickBufferStream();

            using (SymmetricAlgorithm alg = GetAlgorithm())
            using (ICryptoTransform transform = alg.CreateDecryptor())
            {
                CryptoStream cs = new CryptoStream(output, transform, CryptoStreamMode.Write);
                _bufferStream.BufferCopyTo(cs);
                cs.FlushFinalBlock();
            }
            output.Reset();
            return output;
        }

        private QuickBufferStream Encrypt()
        {
            QuickBufferStream output = new QuickBufferStream();
            byte[] firstBuffer = new byte[_key.Length + _iv.Length];
            Array.Copy(_salt, firstBuffer, _key.Length);
            Array.Copy(_iv, 0, firstBuffer, _key.Length, _iv.Length);

            output.Prepend(firstBuffer);

            using (SymmetricAlgorithm alg = GetAlgorithm())
            using (ICryptoTransform transform = alg.CreateEncryptor())
            {
                CryptoStream cs = new CryptoStream(output, transform, CryptoStreamMode.Write);

                _bufferStream.BufferCopyTo(cs);
                cs.FlushFinalBlock();
            }
            output.Reset();
            return output;
        }

        private KeySizes _keySizes;
        /// <summary>
        /// Gets a <see cref="System.Security.Cryptography.KeySizes"/> object indicating all valid key sizes
        /// for the current encryption, or <c>null</c> if the current stream is not encrypted.
        /// </summary>
        public KeySizes KeySizes { get { return _keySizes; } }
    }
}
