using AwesomeAssertions;

namespace CyberDot.Encoding.Base91.Tests
{
    public class Base91Tests
    {
        // Vectors below were generated from a direct Python port of the reference basE91
        // algorithm (refimplementation.txt); "test" -> "fPNKd" also matches the canonical
        // basE91 example from Joachim Henke's original implementation.
        [Fact]
        public void Should_encode_and_decode_known_vectors()
        {
            AssertVector("", "");
            AssertVector("M", "=A");
            AssertVector("Ma", "8DD");
            AssertVector("Man", "8D$J");
            AssertVector("Man ", "8D$JI");
            AssertVector("test", "fPNKd");
            AssertVector("Hello, World!", ">OwJh>}AQ;r@@Y?F");
            AssertVector("The quick brown fox jumps over the lazy dog.",
                "nX^Iz?T1s!2t:aRn#o>vf>6C9#`##mlLK#_1:Wzv;RG!,a%q3Lc=5gA");
        }

        [Fact]
        public void Should_encode_and_decode_runs_of_zero_bytes()
        {
            AssertVector(new byte[] { 0x00 }, "AA");
            AssertVector(new byte[] { 0x00, 0x00 }, "AAA");
            AssertVector(new byte[] { 0x00, 0x00, 0x00 }, "AAAA");
            AssertVector(new byte[] { 0x00, 0x00, 0x00, 0x00 }, "AAAAA");
            AssertVector(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 }, "AAAAAA");
        }

        [Fact]
        public void Should_encode_and_decode_runs_of_ff_bytes()
        {
            AssertVector(new byte[] { 0xff }, "/C");
            AssertVector(new byte[] { 0xff, 0xff }, "B\"H");
            AssertVector(new byte[] { 0xff, 0xff, 0xff }, "B\"tW");
            AssertVector(new byte[] { 0xff, 0xff, 0xff, 0xff }, "B\"B\"#");
            AssertVector(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff }, "B\"B\"B\"B");
        }

        [Fact]
        public void Should_encode_and_decode_mixed_bytes()
        {
            AssertVector(new byte[] { 0x00, 0xe0, 0xff, 0x01 }, "C\"tWA");
            AssertVector(
                new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f },
                ":C#(:C?hVB$MSiVEwndB");
        }

        [Theory]
        [InlineData("AA ")]
        [InlineData("AA'")]
        [InlineData("AA-")]
        [InlineData("AA\\")]
        [InlineData("AA\x00")]
        [InlineData("AA\x7f")]
        public void Should_throw_on_unrecognised_character(string input)
        {
            var act = () => Base91.Decode(input);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Should_throw_on_null_input()
        {
            var encodeAct = () => Base91.Encode(null);
            var decodeAct = () => Base91.Decode(null);

            encodeAct.Should().Throw<ArgumentNullException>();
            decodeAct.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Should_round_trip_all_single_bytes()
        {
            for (var b = 0; b <= 255; b++)
            {
                var data = new[] { (byte)b };

                var encoded = Base91.Encode(data);
                Base91.Decode(encoded).Should().BeEquivalentTo(data);
            }
        }

        [Fact]
        public void Should_round_trip_all_byte_pairs()
        {
            for (var a = 0; a <= 255; a++)
            {
                for (var b = 0; b <= 255; b++)
                {
                    var data = new[] { (byte)a, (byte)b };

                    var encoded = Base91.Encode(data);
                    Base91.Decode(encoded).Should().BeEquivalentTo(data);
                }
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(17)]
        [InlineData(31)]
        [InlineData(32)]
        [InlineData(33)]
        [InlineData(256)]
        public void Should_round_trip_random_data(int length)
        {
            var data = new byte[length];
            new Random(length).NextBytes(data);

            var encoded = Base91.Encode(data);
            Base91.Decode(encoded).Should().BeEquivalentTo(data);
        }

        [Fact]
        public void Should_round_trip_many_random_lengths()
        {
            var random = new Random(12345);
            var buffer = new byte[256];

            for (var i = 0; i < 5000; i++)
            {
                var len = random.Next(0, buffer.Length + 1);
                random.NextBytes(buffer);
                var data = new byte[len];
                Array.Copy(buffer, data, len);

                var encoded = Base91.Encode(data);
                Base91.Decode(encoded).Should().BeEquivalentTo(data);
            }
        }

        // Unlike Base84/Base2048, Base91 has no fixed-width character group, so decoding
        // cannot validate padding - any string built purely from alphabet characters decodes
        // without throwing, even if it was never produced by Encode.
        [Fact]
        public void Should_never_throw_when_decoding_any_string_of_alphabet_characters()
        {
            var random = new Random(999);
            var buffer = new char[64];

            for (var i = 0; i < 2000; i++)
            {
                var len = random.Next(0, buffer.Length + 1);
                for (var j = 0; j < len; j++)
                {
                    buffer[j] = AlphabetChars[random.Next(AlphabetChars.Length)];
                }

                var candidate = new string(buffer, 0, len);

                var act = () => Base91.Decode(candidate);

                act.Should().NotThrow();
            }
        }

        private const string AlphabetChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!#$%&()*+,./:;<=>?@[]^_`{|}~\"";

        private static void AssertVector(string decoded, string encoded)
        {
            AssertVector(System.Text.Encoding.UTF8.GetBytes(decoded), encoded);
        }

        private static void AssertVector(byte[] decoded, string encoded)
        {
            Base91.Encode(decoded).Should().Be(encoded);
            Base91.Decode(encoded).Should().BeEquivalentTo(decoded);
        }
    }
}
