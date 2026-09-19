using System;
using System.Collections.Generic;
using System.Text;
using static CyberDot.Encoding.Base91.Constants;

namespace CyberDot.Encoding.Base91
{
    /// <summary>
    /// Base91 (basE91) is a binary-to-text encoding that uses 91 of the 94 printable ASCII
    /// characters - excluding space, apostrophe, hyphen and backslash, which are left free so
    /// encoded text stays easy to quote or embed. It packs input bits into a queue and drains
    /// a pair of characters at a time, each pair holding 13 or 14 input bits, so encoded
    /// output is about 23% larger than the input, against 33% for Base64.
    ///
    /// Unlike Base64/Base85-style encodings, Base91 has no fixed-width character group, so
    /// there is no canonical padding to validate on decode: bits left over from a final,
    /// unpaired character are simply dropped, exactly as the reference implementation does.
    /// </summary>
    public static class Base91
    {
        private const string AlphabetChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!#$%&()*+,./:;<=>?@[]^_`{|}~\"";
        private const byte None = 0xff;

        internal static readonly char[] Alphabet = AlphabetChars.ToCharArray();
        internal static readonly byte[] InverseMap = BuildInverseMap();

        private static byte[] BuildInverseMap()
        {
            var map = new byte[256];
            for (var i = 0; i < map.Length; i++)
            {
                map[i] = None;
            }

            for (var i = 0; i < Alphabet.Length; i++)
            {
                map[Alphabet[i]] = (byte)i;
            }

            return map;
        }

        public static string Encode(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var result = new StringBuilder(data.Length + data.Length / 4 + 2);
            uint queue = 0;
            var numBits = 0;

            foreach (var b in data)
            {
                queue |= (uint)b << numBits;
                numBits += BitsPerByte;

                if (numBits > QueueFlushThreshold)
                {
                    var bits = NextPairBits(queue, out var v);
                    result.Append(Alphabet[v % Radix]);
                    result.Append(Alphabet[v / Radix]);
                    queue >>= bits;
                    numBits -= bits;
                }
            }

            if (numBits > 0)
            {
                result.Append(Alphabet[queue % Radix]);
                if (!FitsOneChar(numBits, queue))
                {
                    result.Append(Alphabet[queue / Radix]);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Decodes a Base91 encoded string back into a byte slice. Since Base91 has no
        /// canonical padding, this only rejects characters outside the Base91 alphabet - any
        /// other input decodes, though data that was never validly encoded may not round trip.
        /// </summary>
        public static byte[] Decode(string data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var output = new List<byte>(data.Length * 13 / 16 + 2);
            uint queue = 0;
            var numBits = 0;
            var value = -1;

            foreach (var c in data)
            {
                var digit = Digit(c);

                if (value < 0)
                {
                    value = digit;
                    continue;
                }

                value += digit * Radix;
                queue |= (uint)value << numBits;
                numBits += PairBitsOf((uint)value);

                do
                {
                    output.Add((byte)queue);
                    queue >>= BitsPerByte;
                    numBits -= BitsPerByte;
                } while (numBits > SingleCharBitLimit);

                value = -1;
            }

            if (value >= 0)
            {
                output.Add((byte)(queue | (uint)(value << numBits)));
            }

            return output.ToArray();
        }

        // The number of input bits held by the next output pair, and the value to split
        // across its two characters, given the current state of the bit queue.
        internal static int NextPairBits(uint queue, out uint value)
        {
            value = queue & LowBitsMask;
            if (value > SplitValue)
            {
                return QueueFlushThreshold;
            }

            value = queue & WideBitsMask;
            return QueueFlushThreshold + 1;
        }

        // The number of input bits a just-combined character pair represents.
        internal static int PairBitsOf(uint value)
        {
            return (value & LowBitsMask) > SplitValue ? QueueFlushThreshold : QueueFlushThreshold + 1;
        }

        // Whether the encoder writes a single trailing character for this final queue state.
        internal static bool FitsOneChar(int numBits, uint queue)
        {
            return numBits <= SingleCharBitLimit && queue <= SplitValue;
        }

        internal static int Digit(char c)
        {
            var value = c < InverseMap.Length ? InverseMap[c] : None;
            if (value == None)
            {
                throw new ArgumentException("Unrecognised Base91 character: '" + c + "'.");
            }

            return value;
        }
    }
}
