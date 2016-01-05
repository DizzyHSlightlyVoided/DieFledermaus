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

using DieFledermaus.Globalization;
using Org.BouncyCastle.Crypto;

namespace DieFledermaus
{
    /// <summary>
    /// A <see cref="DieFledermauZArchive"/> entry with an encrypted filename, which is currently unknown whether it represents
    /// a file or an empty directory. Use <see cref="Decrypt()"/> after setting the key or password.
    /// </summary>
    public class DieFledermauZItemUnknown : DieFledermauZItem
    {
        internal DieFledermauZItemUnknown(DieFledermauZArchive archive, string originalPath, DieFledermausStream stream, long curOffset, long realOffset)
            : base(archive, null, originalPath, stream, curOffset, realOffset)
        {
        }

        internal override bool IsFilenameEncrypted
        {
            get { return true; }
        }

        /// <summary>
        /// Decrypts the current instance and replaces it in <see cref="DieFledermauZItem.Archive"/> with a properly decrypted instance. 
        /// </summary>
        /// <returns>Either a decrypted <see cref="DieFledermauZArchiveEntry"/> object, or a decrypted <see cref="DieFledermauZEmptyDirectory"/> object,
        /// which will replace the current instance in <see cref="DieFledermauZItem.Archive"/>.</returns>
        /// <exception cref="ObjectDisposedException">
        /// The current instance has already been successfully decrypted.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// <see cref="DieFledermauZItem.Archive"/> is in write-only mode.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current instance is not encrypted.
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// The stream contains invalid data.
        /// </exception>
        /// <exception cref="CryptoException">
        /// The password is not correct. It is safe to attempt to call <see cref="Decrypt()"/>
        /// again if this exception is caught.
        /// </exception>
        public override DieFledermauZItem Decrypt()
        {
            base.Decrypt();

            DieFledermauZItem returner;
            if (MausStream.Filename.EndsWith("/"))
            {
                DieFledermauZEmptyDirectory.CheckStream(MausStream);
                if (!DieFledermauZArchive.IsValidEmptyDirectoryPath(MausStream.Filename))
                    throw new InvalidDataException(TextResources.InvalidDataMaus);
                returner = new DieFledermauZEmptyDirectory(Archive, null, MausStream, Offset, RealOffset);
            }
            else returner = new DieFledermauZArchiveEntry(Archive, null, OriginalPath, MausStream, Offset, RealOffset);

            Archive.Entries.ReplaceElement(Archive.Entries.IndexOf(this), returner);
            _isDecrypted = true;
            DoDelete(false);
            return returner;
        }
    }
}
