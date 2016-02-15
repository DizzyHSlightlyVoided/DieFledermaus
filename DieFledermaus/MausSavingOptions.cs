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
    /// <summary>
    /// Specifies whether a particular value is saved in the primary (unencrypted) format, the secondary (encrypted) format, or both.
    /// </summary>
    /// <remarks>
    /// <para>A <see cref="DieFledermausStream"/> divides the format into two sections: the primary format, which is always transmitted in plaintext; and
    /// the secondary format, which is encrypted along with the compressed data and which is used for the purpose of computing the value of
    /// <see cref="DieFledermausStream.CompressedHash"/> (and thus for the purpose of private-key signing). The primary format must include certain values
    /// such as <see cref="DieFledermausStream.HashFunction"/>, because i.e. if the stream is encrypted, it's impossible to determine how to decrypt it
    /// otherwise.</para>
    /// <para><see cref="DieFledermauZArchive"/> only has the equivalent of a primary format, unless it is encrypted.</para>
    /// </remarks>
    [Flags]
    public enum MausSavingOptions
    {
        /// <summary>
        /// The default value, <see cref="SecondaryOnly"/>.
        /// </summary>
        Default,
        /// <summary>
        /// The option is stored only in the primary format. This option is not recommended for <see cref="DieFledermausStream"/>, because
        /// the value will not be taken into consideration for the purpose of computing <see cref="DieFledermausStream.CompressedHash"/>.
        /// </summary>
        PrimaryOnly,
        /// <summary>
        /// The option is stored only in the secondary format. The default value.
        /// </summary>
        SecondaryOnly,
        /// <summary>
        /// The option is stored in both the primary and secondary formats. Recommended for redundancy and/or for when the <see cref="DieFledermausStream"/>
        /// or <see cref="DieFledermauZArchive"/> is encrypted, but you want to transmit the value in plaintext.
        /// </summary>
        Both,
    }
}
