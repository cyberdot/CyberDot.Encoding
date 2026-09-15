using System;
using System.Collections.Generic;
using System.Text;
using static CyberDot.Encoding.Base84.Constants;

namespace CyberDot.Encoding.Base84
{
    /// <summary>
    /// Base84 is a binary-to-text encoding that uses 84 ASCII characters, all safe for file
    /// names on macOS, Linux and Windows. It works like Base91: every five characters can
    /// hold 31 or 32 input bits, so encoded output is about 25% larger than the input,
    /// against 33% for Base64.
    ///
    /// The alphabet has every printable ASCII character except space, the period, and the
    /// nine characters that Windows rejects in file names: &lt; &gt; : " / \ | ? *
    /// </summary>
    public static class Base84
    {
        private const string AlphabetChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!#$%&'()+,-;=@[]^_`{}~";
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

            var result = new StringBuilder(data.Length * GroupChars / 4 + GroupChars);
            ulong acc = 0;
            var numBits = 0;

            foreach (var x in data)
            {
                acc |= (ulong)x << numBits;
                numBits += BitsPerByte;

                if (numBits > GroupBits)
                {
                    var low = (uint)acc;
                    var bits = GroupBitsOf(low);
                    var v = bits > GroupBits ? low : low & GroupMask;
                    acc >>= bits;
                    numBits -= bits;
                    WriteDigits(result, v, GroupChars);
                }
            }

            if (numBits > 0)
            {
                var tail = (uint)acc;
                var chars = FitsOneChar(numBits, tail) ? 1 : TailChars[numBits];
                WriteDigits(result, tail, chars);
            }

            return result.ToString();
        }

        /// <summary>
        /// Decodes a Base84 encoded string back into a byte slice.
        /// Decoding is strict: it rejects any ending the encoder cannot produce.
        /// </summary>
        public static byte[] Decode(string data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var output = new List<byte>(data.Length * GroupBits / (GroupChars * BitsPerByte) + GroupChars);
            ulong acc = 0;
            var numBits = 0;
            var bits = 0;

            var i = 0;
            while (i < data.Length)
            {
                var chunkLen = Math.Min(GroupChars, data.Length - i);
                var v = ReadChunk(data, i, chunkLen);
                bits = chunkLen == GroupChars ? GroupBitsOf(v) : TailBitsOf(chunkLen, numBits, v);

                acc |= (ulong)v << numBits;
                numBits += bits;

                while (numBits >= BitsPerByte)
                {
                    output.Add((byte)acc);
                    acc >>= BitsPerByte;
                    numBits -= BitsPerByte;
                }

                i += GroupChars;
            }

            if (data.Length > 0 && data.Length % GroupChars == 0)
            {
                // Reject a final group with padding bits set, or one that fits in fewer characters.
                if (acc != 0 || bits - numBits <= MaxTailBits)
                {
                    throw new ArgumentException("Invalid Base84 padding.");
                }
            }

            return output.ToArray();
        }

        // The number of input bits that a group of five characters holds for this value.
        internal static int GroupBitsOf(uint v)
        {
            return (v & GroupMask) < ExtraBitLimit ? GroupBits + 1 : GroupBits;
        }

        // Whether the encoder writes a single character for this tail.
        internal static bool FitsOneChar(int bits, uint tail)
        {
            return bits <= 7 && tail < Radix;
        }

        // The number of input bits in a short final chunk, or an error for a chunk the encoder never writes.
        internal static int TailBitsOf(int chars, int pending, uint v)
        {
            var bits = TailBits[chars][pending];
            if (bits == 0 || v >> bits != 0)
            {
                throw new ArgumentException("Invalid Base84 padding.");
            }

            if (chars > 1 && FitsOneChar(bits, v))
            {
                throw new ArgumentException("Invalid Base84 padding.");
            }

            return bits;
        }

        internal static uint ReadChunk(string data, int start, int length)
        {
            uint v = 0;
            uint place = 1;
            for (var i = 0; i < length; i++)
            {
                v += Digit(data[start + i]) * place;
                place *= Radix;
            }

            return v;
        }

        internal static uint ReadChunk(char[] chunk, int length)
        {
            uint v = 0;
            uint place = 1;
            for (var i = 0; i < length; i++)
            {
                v += Digit(chunk[i]) * place;
                place *= Radix;
            }

            return v;
        }

        internal static uint Digit(char c)
        {
            var value = c < InverseMap.Length ? InverseMap[c] : None;
            if (value == None)
            {
                throw new ArgumentException("Unrecognised Base84 character: '" + c + "'.");
            }

            return value;
        }

        private static void WriteDigits(StringBuilder sb, uint value, int count)
        {
            for (var i = 0; i < count; i++)
            {
                sb.Append(Alphabet[value % Radix]);
                value /= Radix;
            }
        }
    }
}
