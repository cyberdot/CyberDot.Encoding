using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using static CyberDot.Encoding.Base91.Constants;

namespace CyberDot.Encoding.Base91
{
    /// <summary>
    /// Streams raw bytes into their Base91 representation, serialised using whichever
    /// <see cref="Encoding"/> is supplied to the constructor (UTF-8 by default). Since the
    /// Base91 alphabet is pure ASCII, the resulting text is equally valid under any of them.
    /// Intended for use with <see cref="CryptoStream"/>.
    /// </summary>
    public class ToBase91Transform : ICryptoTransform
    {
        private readonly System.Text.Encoding encoding;

        // Bit accumulator state, carried across TransformBlock calls: a pair of characters
        // holds 13 or 14 input bits, so almost every input byte leaves a partial pair pending.
        private uint queue;
        private int numBits;

        public ToBase91Transform(System.Text.Encoding encoding = null)
        {
            this.encoding = encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            OutputBlockSize = this.encoding.GetMaxByteCount(2);
        }

        public bool CanReuseTransform => true;
        public bool CanTransformMultipleBlocks => true;
        public int InputBlockSize => 1;
        public int OutputBlockSize { get; }

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
            queue = 0;
            numBits = 0;
            return result;
        }

        private byte[] ProcessBytes(byte[] inputBuffer, int inputOffset, int inputCount, bool isFinal)
        {
            if (inputBuffer == null)
            {
                throw new ArgumentNullException(nameof(inputBuffer));
            }

            if (inputOffset < 0 || inputCount < 0 || inputOffset + inputCount > inputBuffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(inputCount));
            }

            var chars = new List<char>(inputCount * 2);
            var end = inputOffset + inputCount;

            for (var i = inputOffset; i < end; i++)
            {
                queue |= (uint)inputBuffer[i] << numBits;
                numBits += BitsPerByte;

                if (numBits > QueueFlushThreshold)
                {
                    var bits = Base91.NextPairBits(queue, out var v);
                    chars.Add(Base91.Alphabet[v % Radix]);
                    chars.Add(Base91.Alphabet[v / Radix]);
                    queue >>= bits;
                    numBits -= bits;
                }
            }

            if (isFinal && numBits > 0)
            {
                chars.Add(Base91.Alphabet[queue % Radix]);
                if (!Base91.FitsOneChar(numBits, queue))
                {
                    chars.Add(Base91.Alphabet[queue / Radix]);
                }
            }

            return chars.Count == 0 ? Array.Empty<byte>() : encoding.GetBytes(chars.ToArray());
        }
    }
}
