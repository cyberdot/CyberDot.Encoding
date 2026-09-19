using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using static CyberDot.Encoding.Base91.Constants;

namespace CyberDot.Encoding.Base91
{
    /// <summary>
    /// Streams a Base91 sequence back into the original raw bytes. Since the Base91 alphabet
    /// is pure ASCII, the input text may validly have been serialised with any
    /// <see cref="Encoding"/>; pass the matching one to the constructor (UTF-8 by default).
    /// Intended for use with <see cref="CryptoStream"/>.
    /// </summary>
    public class FromBase91Transform : ICryptoTransform
    {
        private readonly Decoder decoder;

        // Bit accumulator and dangling-character state, carried across TransformBlock calls.
        private uint queue;
        private int numBits;
        private int value = -1;

        public FromBase91Transform(System.Text.Encoding encoding = null)
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
            queue = 0;
            numBits = 0;
            value = -1;
        }

        // The Decoder buffers any incomplete multibyte sequence internally across calls (and,
        // with a throwing Encoding, rejects malformed/truncated input), so we only need to
        // track our own bit-accumulator state here.
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
                var digit = Base91.Digit(chars[i]);

                if (value < 0)
                {
                    value = digit;
                    continue;
                }

                value += digit * Radix;
                queue |= (uint)value << numBits;
                numBits += Base91.PairBitsOf((uint)value);

                do
                {
                    output.Add((byte)queue);
                    queue >>= BitsPerByte;
                    numBits -= BitsPerByte;
                } while (numBits > SingleCharBitLimit);

                value = -1;
            }

            if (isFinal && value >= 0)
            {
                output.Add((byte)(queue | (uint)(value << numBits)));
            }

            return output.ToArray();
        }
    }
}
