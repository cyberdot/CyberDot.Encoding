using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using static CyberDot.Encoding.Base84.Constants;

namespace CyberDot.Encoding.Base84
{
    /// <summary>
    /// Streams raw bytes into their Base84 representation, serialised using whichever
    /// <see cref="Encoding"/> is supplied to the constructor (UTF-8 by default). Since the
    /// Base84 alphabet is pure ASCII, the resulting text is equally valid under any of
    /// them. Intended for use with <see cref="CryptoStream"/>.
    /// </summary>
    public class ToBase84Transform : ICryptoTransform
    {
        private readonly System.Text.Encoding encoding;

        // Bit accumulator state, carried across TransformBlock calls: a group of five
        // characters holds 31 or 32 input bits, so almost every input byte leaves a
        // partial group pending.
        private ulong acc;
        private int numBits;

        public ToBase84Transform(System.Text.Encoding encoding = null)
        {
            this.encoding = encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            OutputBlockSize = this.encoding.GetMaxByteCount(GroupChars);
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
            acc = 0;
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

            var output = new List<byte>(inputCount * 2);
            var end = inputOffset + inputCount;

            for (var i = inputOffset; i < end; i++)
            {
                acc |= (ulong)inputBuffer[i] << numBits;
                numBits += BitsPerByte;

                if (numBits > GroupBits)
                {
                    var low = (uint)acc;
                    var bits = Base84.GroupBitsOf(low);
                    var v = bits > GroupBits ? low : low & GroupMask;
                    acc >>= bits;
                    numBits -= bits;
                    WriteDigits(v, GroupChars, output);
                }
            }

            if (isFinal && numBits > 0)
            {
                var tail = (uint)acc;
                var chars = Base84.FitsOneChar(numBits, tail) ? 1 : TailChars[numBits];
                WriteDigits(tail, chars, output);
            }

            return output.ToArray();
        }

        private void WriteDigits(uint value, int count, List<byte> output)
        {
            var chars = new char[count];
            for (var i = 0; i < count; i++)
            {
                chars[i] = Base84.Alphabet[value % Radix];
                value /= Radix;
            }

            output.AddRange(encoding.GetBytes(chars));
        }
    }
}
