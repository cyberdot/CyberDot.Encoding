using System.Security.Cryptography;
using AwesomeAssertions;
using TextEncoding = System.Text.Encoding;

namespace CyberDot.Encoding.Base84.Tests
{
    public class StreamTransformTests
    {
        private static readonly int[] ChunkSizes = [1, 2, 3, 4, 5, 7, 13, 64];

        // Base84's alphabet is pure ASCII, so the encoded text is equally valid under any
        // of these three encodings - the transforms support all of them.
        private static readonly TextEncoding[] SafeEncodings = [TextEncoding.UTF8, TextEncoding.Unicode, TextEncoding.UTF32];

        private static readonly (byte[] Decoded, string Encoded)[] Vectors =
        {
            (System.Array.Empty<byte>(), ""),
            (TextEncoding.UTF8.GetBytes("M"), "]A"),
            (TextEncoding.UTF8.GetBytes("Ma"), "tsD"),
            (TextEncoding.UTF8.GetBytes("Man"), "pRRM"),
            (TextEncoding.UTF8.GetBytes("Man "), ";dA^K"),
            (TextEncoding.UTF8.GetBytes("Hello, World!"), "s@Etk'#Qedrxz+hhA"),
            (new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff }, "rxQLrHG"),
            (
                TextEncoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog."),
                "wBB]KdJ}phb-#tmbNB^K!!J_K^3h=l_pj[nJXJLnkB3kkhkT_KM}r1P"
            ),
        };

        private static byte[] EncodeViaCryptoStream(byte[] data, TextEncoding? encoding = null)
        {
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, new ToBase84Transform(encoding), CryptoStreamMode.Write, leaveOpen: true))
            {
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();
            }

            return ms.ToArray();
        }

        private static byte[] DecodeViaCryptoStream(byte[] bytes, TextEncoding? encoding = null)
        {
            using var ms = new MemoryStream(bytes);
            using var cs = new CryptoStream(ms, new FromBase84Transform(encoding), CryptoStreamMode.Read);
            using var outMs = new MemoryStream();
            cs.CopyTo(outMs);
            return outMs.ToArray();
        }

        private static byte[] DriveTransform(ICryptoTransform transform, byte[] input, int chunkSize)
        {
            using var outMs = new MemoryStream();
            var i = 0;

            while (input.Length - i > chunkSize)
            {
                var buffer = new byte[chunkSize * 8 + 32];
                var written = transform.TransformBlock(input, i, chunkSize, buffer, 0);
                outMs.Write(buffer, 0, written);
                i += chunkSize;
            }

            var final = transform.TransformFinalBlock(input, i, input.Length - i);
            outMs.Write(final, 0, final.Length);

            return outMs.ToArray();
        }

        private static byte[] AsBytes(string value, TextEncoding? encoding = null) => (encoding ?? TextEncoding.UTF8).GetBytes(value);

        // --- Round trip against the known vectors (default: UTF-8) -----------------------------------------------

        [Fact]
        public void Should_stream_encode_known_vectors()
        {
            foreach (var (decoded, encoded) in Vectors)
            {
                EncodeViaCryptoStream(decoded).Should().BeEquivalentTo(AsBytes(encoded));
            }
        }

        [Fact]
        public void Should_stream_decode_known_vectors()
        {
            foreach (var (decoded, encoded) in Vectors)
            {
                DecodeViaCryptoStream(AsBytes(encoded)).Should().BeEquivalentTo(decoded);
            }
        }

        [Theory]
        [InlineData("AA/")]
        [InlineData("A A")]
        public void Should_throw_on_unrecognised_character_while_streaming(string input)
        {
            var act = () => DecodeViaCryptoStream(AsBytes(input));

            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData("A")]
        [InlineData("AE")]
        [InlineData("AAK")]
        [InlineData("AAAAAA")]
        public void Should_throw_on_invalid_padding_while_streaming(string input)
        {
            var act = () => DecodeViaCryptoStream(AsBytes(input));

            act.Should().Throw<ArgumentException>();
        }

        // --- Round trip across all three "safe" encodings ---------------------------------------------------------

        [Fact]
        public void Should_round_trip_known_vectors_across_all_safe_encodings()
        {
            foreach (var encoding in SafeEncodings)
            {
                foreach (var (decoded, encoded) in Vectors)
                {
                    EncodeViaCryptoStream(decoded, encoding)
                        .Should().BeEquivalentTo(AsBytes(encoded, encoding), $"encoding {encoding.EncodingName}");

                    DecodeViaCryptoStream(AsBytes(encoded, encoding), encoding)
                        .Should().BeEquivalentTo(decoded, $"encoding {encoding.EncodingName}");
                }
            }
        }

        // --- Chunked feeding: exercises bit-accumulator / group-buffering across calls ----------------------------

        [Fact]
        public void Should_encode_correctly_regardless_of_chunk_size()
        {
            foreach (var (decoded, encoded) in Vectors)
            {
                foreach (var encoding in SafeEncodings)
                {
                    var expectedBytes = AsBytes(encoded, encoding);

                    foreach (var chunkSize in ChunkSizes)
                    {
                        DriveTransform(new ToBase84Transform(encoding), decoded, chunkSize)
                            .Should().BeEquivalentTo(expectedBytes, $"chunk size {chunkSize}, encoding {encoding.EncodingName}");
                    }
                }
            }
        }

        [Fact]
        public void Should_decode_correctly_regardless_of_chunk_size()
        {
            foreach (var (decoded, encoded) in Vectors)
            {
                foreach (var encoding in SafeEncodings)
                {
                    var inputBytes = AsBytes(encoded, encoding);

                    foreach (var chunkSize in ChunkSizes)
                    {
                        DriveTransform(new FromBase84Transform(encoding), inputBytes, chunkSize)
                            .Should().BeEquivalentTo(decoded, $"chunk size {chunkSize}, encoding {encoding.EncodingName}");
                    }
                }
            }
        }

        // --- Round trip fuzzing over random data of varying lengths ------------------------------------------------

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(17)]
        [InlineData(256)]
        [InlineData(1000)]
        [InlineData(4097)]
        public void Should_round_trip_random_data_through_crypto_stream(int length)
        {
            var data = new byte[length];
            new Random(length).NextBytes(data);

            foreach (var encoding in SafeEncodings)
            {
                var encoded = EncodeViaCryptoStream(data, encoding);
                encoded.Should().BeEquivalentTo(AsBytes(Base84.Encode(data), encoding), $"encoding {encoding.EncodingName}");

                var decoded = DecodeViaCryptoStream(encoded, encoding);
                decoded.Should().BeEquivalentTo(data, $"encoding {encoding.EncodingName}");
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(17)]
        [InlineData(256)]
        public void Should_round_trip_random_data_with_odd_chunk_sizes(int length)
        {
            var data = new byte[length];
            new Random(length + 1).NextBytes(data);

            foreach (var encoding in SafeEncodings)
            {
                foreach (var chunkSize in ChunkSizes)
                {
                    var encoded = DriveTransform(new ToBase84Transform(encoding), data, chunkSize);
                    var decoded = DriveTransform(new FromBase84Transform(encoding), encoded, chunkSize);

                    decoded.Should().BeEquivalentTo(data, $"chunk size {chunkSize}, encoding {encoding.EncodingName}, length {length}");
                }
            }
        }

        // --- Transform metadata --------------------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(SafeEncodingsMemberData))]
        public void Should_expose_expected_metadata_on_encode(TextEncoding encoding)
        {
            var transform = new ToBase84Transform(encoding);

            transform.InputBlockSize.Should().Be(1);
            transform.OutputBlockSize.Should().BeGreaterThanOrEqualTo(5);
            transform.CanTransformMultipleBlocks.Should().BeTrue();
            transform.CanReuseTransform.Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(SafeEncodingsMemberData))]
        public void Should_expose_expected_metadata_on_decode(TextEncoding encoding)
        {
            var transform = new FromBase84Transform(encoding);

            transform.InputBlockSize.Should().Be(1);
            transform.OutputBlockSize.Should().Be(1);
            transform.CanTransformMultipleBlocks.Should().BeTrue();
            transform.CanReuseTransform.Should().BeTrue();
        }

        [Fact]
        public void Should_default_to_utf8_when_no_encoding_supplied()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };

            EncodeViaCryptoStream(data).Should().BeEquivalentTo(AsBytes(Base84.Encode(data), TextEncoding.UTF8));
        }

        public static TheoryData<TextEncoding> SafeEncodingsMemberData()
        {
            var data = new TheoryData<TextEncoding>();
            foreach (var encoding in SafeEncodings) data.Add(encoding);
            return data;
        }

        [Fact]
        public void Should_reuse_transform_instances_across_multiple_streams()
        {
            var toTransform = new ToBase84Transform();
            var fromTransform = new FromBase84Transform();

            var first = new byte[] { 1, 2, 3 };
            var second = new byte[] { 4, 5, 6, 7 };

            foreach (var data in new[] { first, second })
            {
                using var ms = new MemoryStream();
                using (var cs = new CryptoStream(ms, toTransform, CryptoStreamMode.Write, leaveOpen: true))
                {
                    cs.Write(data, 0, data.Length);
                    cs.FlushFinalBlock();
                }

                using var decodedMs = new MemoryStream(ms.ToArray());
                using var decodeCs = new CryptoStream(decodedMs, fromTransform, CryptoStreamMode.Read);
                using var outMs = new MemoryStream();
                decodeCs.CopyTo(outMs);

                outMs.ToArray().Should().BeEquivalentTo(data);
            }
        }
    }
}
