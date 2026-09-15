namespace CyberDot.Encoding.Base84
{
    // Numeric tables ported from the reference Zig implementation (RefImplementation.txt).
    // Base84 packs input bits into groups of five characters instead of a fixed number of
    // bits per character: five base-84 digits (84^5 ≈ 2^31.96) can always hold 31 input
    // bits, and can often stretch to hold 32. These tables answer the two questions that
    // arise from that variable width: how many characters does a short final chunk need,
    // and how many bits does a short final chunk of "n" characters actually hold.
    internal static class Constants
    {
        public const int BitsPerByte = 8;
        public const int Radix = 84;

        // A group of five characters is the natural encoding unit.
        public const int GroupChars = 5;

        // The number of input bits that five characters can always hold: floor(log2(84^5)).
        public const int GroupBits = 31;
        public const uint GroupMask = (1u << GroupBits) - 1;

        // A group holds a 32nd bit whenever its low 31 bits, taken alone, are below this
        // limit: 84^5 - 2^31. Below the limit, the full 32-bit value still fits under 84^5.
        public const uint ExtraBitLimit = 2034635776;

        // The most bits that four characters can ever hold (floor(log2(84^4))). Used to spot
        // a final five-character group that could have been written with fewer characters.
        public const int MaxTailBits = 25;

        // tail_chars[bits]: the number of characters the encoder writes for a tail of
        // "bits" pending input bits (0-31). This is the smallest character count whose
        // guaranteed capacity covers "bits".
        public static readonly int[] TailChars =
        {
            0, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 5,
        };

        // tail_bits[chars][pending]: the number of input bits held by a short final chunk of
        // "chars" characters, given "pending" bits (0-7) were already waiting in the byte
        // accumulator. Zero marks a (chars, pending) combination the encoder never produces.
        public static readonly int[][] TailBits =
        {
            new[] { 0, 0, 0, 0, 0, 0, 0, 0 },
            new[] { 0, 7, 6, 5, 4, 3, 2, 1 },
            new[] { 8, 7, 0, 0, 12, 11, 10, 9 },
            new[] { 16, 15, 14, 13, 0, 19, 18, 17 },
            new[] { 24, 23, 22, 21, 20, 0, 0, 25 },
        };
    }
}
