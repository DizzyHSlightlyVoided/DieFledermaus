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

namespace DieFledermaus
{
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
