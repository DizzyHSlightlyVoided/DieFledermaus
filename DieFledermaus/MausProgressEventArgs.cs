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
using SevenZip;

namespace DieFledermaus
{
    /// <summary>
    /// Contains information for the <see cref="DieFledermausStream.Progress"/> event.
    /// </summary>
    public class MausProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Creates a new instance with the specified state and no input or output sizes.
        /// </summary>
        /// <param name="state">The current progress state.</param>
        public MausProgressEventArgs(MausProgressState state)
        {
            _state = state;
            _inSize = _outSize = -1;
        }

        /// <summary>
        /// Creates a new instance with the specified values.
        /// </summary>
        /// <param name="state">The current progress state.</param>
        /// <param name="inSize">The input size of the processed data, or -1 if unknown.</param>
        /// <param name="outSize">The output size of the processed data, or -1 if unknown.</param>
        public MausProgressEventArgs(MausProgressState state, long inSize, long outSize)
        {
            _state = state;
            _inSize = inSize;
            _outSize = outSize;
        }

        private MausProgressState _state;
        /// <summary>
        /// Gets the state of the current progress.
        /// </summary>
        public MausProgressState State { get { return _state; } }

        private long _inSize;
        /// <summary>
        /// Gets the input size of the processed data, or -1 if unknown.
        /// </summary>
        public long InputSize { get { return _inSize; } }

        private long _outSize;
        /// <summary>
        /// Gets the output size of the processed data, or -1 if unknown.
        /// </summary>
        public long OutputSize { get { return _outSize; } }
    }

    /// <summary>
    /// Information about the current state of a <see cref="DieFledermausStream"/>.
    /// </summary>
    public enum MausProgressState
    {
        /// <summary>
        /// No current progress, or progress is unknown.
        /// </summary>
        None,
        /// <summary>
        /// Compressing the uncompressed data.
        /// </summary>
        Compressing,
        /// <summary>
        /// Compressing the uncompressed data. Input and output sizes are specified.
        /// </summary>
        CompressingWithSize,
        /// <summary>
        /// Deriving the key from the password.
        /// </summary>
        BuildingKey,
        /// <summary>
        /// Signing the hash of the uncompressed data using <see cref="DieFledermausStream.RSASignParameters"/>.
        /// </summary>
        SigningRSA,
        /// <summary>
        /// Computing the hash of the uncompressed data.
        /// </summary>
        ComputingHash,
        /// <summary>
        /// Writing header information.
        /// </summary>
        WritingHead,
        /// <summary>
        /// Encrypting the compressed data.
        /// </summary>
        Encrypting,
        /// <summary>
        /// Computing the HMAC of the encrypted data.
        /// </summary>
        ComputingHMAC,
        /// <summary>
        /// Compressing entries.
        /// </summary>
        ArchiveCompressingEntries,
        /// <summary>
        /// Finalizing entries.
        /// </summary>
        ArchiveBuildingEntries,
        /// <summary>
        /// The stream is done writing.
        /// </summary>
        CompletedWriting = int.MinValue,
        /// <summary>
        /// The stream is reading data from the underlying stream.
        /// </summary>
        LoadingData = DieFledermausStream.Max16Bit,
        /// <summary>
        /// Decompressing the compressed data.
        /// </summary>
        Decompressing = Compressing | LoadingData,
        /// <summary>
        /// Decompressing the compressed data, and input and output sizes are specified.
        /// </summary>
        DecompressingWithSize = CompressingWithSize | LoadingData,
        /// <summary>
        /// Verifying the RSA signature.
        /// </summary>
        VerifyingRSASignature = SigningRSA | LoadingData,
        /// <summary>
        /// Verifying the hash of the decompressed data.
        /// </summary>
        VerifyingHash = ComputingHash | LoadingData,
        /// <summary>
        /// Decrypting the encrypted data.
        /// </summary>
        Decrypting = Encrypting | LoadingData,
        /// <summary>
        /// Verifying the HMAC of the decrypted data.
        /// </summary>
        VerifyingHMAC = ComputingHMAC | LoadingData,
        /// <summary>
        /// The stream is done loading data.
        /// </summary>
        CompletedLoading = CompletedWriting | LoadingData,
    }

    /// <summary>
    /// Delegate for the <see cref="DieFledermausStream.Progress"/> event.
    /// </summary>
    /// <param name="sender">The object which raised the event.</param>
    /// <param name="e">A <see cref="MausProgressEventArgs"/> object containing information about the event.</param>
    public delegate void MausProgressEventHandler(object sender, MausProgressEventArgs e);

    internal interface IMausProgress : ICodeProgress
    {
        event MausProgressEventHandler Progress;

        void OnProgress(MausProgressState state);
    }
}
