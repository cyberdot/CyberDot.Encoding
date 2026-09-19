namespace CyberDot.Encoding.Base91
{
    // Numeric constants ported from the reference basE91 algorithm (refimplementation.txt).
    // Base91 has no fixed-width character group like Base64/Base85: it packs input bits into
    // a queue and drains a pair of characters at a time, each pair holding 13 or 14 bits
    // depending on the combined value. Because of that, decoding cannot validate padding the
    // way Base84/Base2048 do - any leftover bits in a final, unpaired character are simply
    // dropped, exactly as the reference implementation does.
    internal static class Constants
    {
        public const int BitsPerByte = 8;
        public const int Radix = 91;

        // Once the bit queue holds more than this many bits, a pair of characters is drained.
        public const int QueueFlushThreshold = 13;

        // Mask covering the low QueueFlushThreshold bits of the queue.
        public const uint LowBitsMask = (1u << QueueFlushThreshold) - 1;

        // Mask covering one more bit than LowBitsMask, used when the low-bits value alone is
        // too small to have come from a QueueFlushThreshold + 1 bit chunk.
        public const uint WideBitsMask = (1u << (QueueFlushThreshold + 1)) - 1;

        // A low-bits value at or below this could only have come from a wider (14-bit) chunk,
        // so the encoder/decoder fall back to WideBitsMask instead of LowBitsMask.
        public const int SplitValue = 88;

        // When flushing a final partial queue, a single trailing character suffices only when
        // strictly fewer than this many bits remain (and the queue's value is small enough -
        // see SplitValue - to round-trip through one character alone).
        public const int SingleCharBitLimit = 7;
    }
}
