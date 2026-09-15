using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using static CyberDot.Encoding.Base84.Constants;

namespace CyberDot.Encoding.Base84
{
    /// <summary>
    /// Streams a Base84 sequence back into the original raw bytes. Since the Base84
    /// alphabet is pure ASCII, the input text may validly have been serialised with any
    /// <see cref="Encoding"/>; pass the matching one to the constructor (UTF-8 by default).
    /// Intended for use with <see cref="CryptoStream"/>.
    /// </summary>
    public class FromBase84Transform : ICryptoTransform
    {
        private readonly Decoder decoder;

        // Characters are only ever decoded five at a time, so at most four can be pending
        // between calls, waiting on the rest of their group.
        private readonly char[] pending = new char[GroupChars];
        private int pendingCount;

        // Byte accumulator state, carried across TransformBlock calls.
        private ulong acc;
        private int numBits;

        // The bit width of the most recently decoded chunk, needed - together with
        // totalChars - to validate the padding of a final full group once the stream ends.
        private int bits;
        private long totalChars;

        public FromBase84Transform(System.Text.Encoding encoding = null)
        {
            decoder = (encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)).GetDecoder();
        }

        public bool CanReuseTransform => true;
        public bool CanTransformMultipleBlocks => true;
        public int InputBlockSize => 1;
        public int OutputBlockSize => 1;

        public void Dispose() { }

        public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
        {
            var output = ProcessBytes(inputBuffer, inputOffset, inputCount, isFinal: false);
            Array.Copy(output, 0, outputBuffer, outputOffset, output.Length);
            return output.Length;
        }

        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            var result = ProcessBytes(inputBuffer, inputOffset, inputCount, isFinal: true);
            Reset();
            return result;
        }

        private void Reset()
        {
            decoder.Reset();
            pendingCount = 0;
            acc = 0;
            numBits = 0;
            bits = 0;
            totalChars = 0;
        }

        // The Decoder buffers any incomplete multibyte sequence internally across calls
        // (and, with a throwing Encoding, rejects malformed/truncated input), so we only
        // need to track our own group-buffering and bit-accumulator state here.
        private byte[] ProcessBytes(byte[] inputBuffer, int inputOffset, int inputCount, bool isFinal)
        {
            if (inputBuffer == null)
            {
                throw new ArgumentNullException(nameof(inputBuffer));
            }

            var maxChars = decoder.GetCharCount(inputBuffer, inputOffset, inputCount, isFinal);
            var chars = maxChars == 0 ? Array.Empty<char>() : new char[maxChars];
            var charCount = decoder.GetChars(inputBuffer, inputOffset, inputCount, chars, 0, isFinal);

            var output = new List<byte>(charCount);

            for (var i = 0; i < charCount; i++)
            {
                pending[pendingCount++] = chars[i];
                totalChars++;

                if (pendingCount == GroupChars)
                {
                    HandleChunk(GroupChars, isFullGroup: true, output);
                    pendingCount = 0;
                }
            }

            if (isFinal)
            {
                if (pendingCount > 0)
                {
                    HandleChunk(pendingCount, isFullGroup: false, output);
                    pendingCount = 0;
                }

                if (totalChars > 0 && totalChars % GroupChars == 0)
                {
                    // Reject a final group with padding bits set, or one that fits in fewer characters.
                    if (acc != 0 || bits - numBits <= MaxTailBits)
                    {
                        throw new ArgumentException("Invalid Base84 padding.");
                    }
                }
            }

            return output.ToArray();
        }

        private void HandleChunk(int chunkLen, bool isFullGroup, List<byte> output)
        {
            var v = Base84.ReadChunk(pending, chunkLen);
            bits = isFullGroup ? Base84.GroupBitsOf(v) : Base84.TailBitsOf(chunkLen, numBits, v);

            acc |= (ulong)v << numBits;
            numBits += bits;

            while (numBits >= BitsPerByte)
            {
                output.Add((byte)acc);
                acc >>= BitsPerByte;
                numBits -= BitsPerByte;
            }
        }
    }
}
