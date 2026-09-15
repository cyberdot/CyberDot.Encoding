using AwesomeAssertions;

namespace CyberDot.Encoding.Base84.Tests
{
    public class Base84Tests
    {
        // Vectors below are taken from the reference Zig implementation's test suite.
        [Fact]
        public void Should_encode_and_decode_known_vectors()
        {
            AssertVector("", "");
            AssertVector("M", "]A");
            AssertVector("Ma", "tsD");
            AssertVector("Man", "pRRM");
            AssertVector("Man ", ";dA^K");
            AssertVector("Hello, World!", "s@Etk'#Qedrxz+hhA");
            AssertVector("The quick brown fox jumps over the lazy dog.",
                "wBB]KdJ}phb-#tmbNB^K!!J_K^3h=l_pj[nJXJLnkB3kkhkT_KM}r1P");
        }

        [Fact]
        public void Should_encode_and_decode_runs_of_zero_bytes()
        {
            AssertVector(new byte[] { 0x00 }, "AA");
            AssertVector(new byte[] { 0x00, 0x00 }, "AAA");
            AssertVector(new byte[] { 0x00, 0x00, 0x00 }, "AAAA");
            AssertVector(new byte[] { 0x00, 0x00, 0x00, 0x00 }, "AAAAA");
            AssertVector(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 }, "AAAAAAA");
        }

        [Fact]
        public void Should_encode_and_decode_runs_of_ff_bytes()
        {
            AssertVector(new byte[] { 0xff }, "DD");
            AssertVector(new byte[] { 0xff, 0xff }, "PYJ");
            AssertVector(new byte[] { 0xff, 0xff, 0xff }, "#8Zc");
            AssertVector(new byte[] { 0xff, 0xff, 0xff, 0xff }, "rxQLrB");
            AssertVector(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff }, "rxQLrHG");
        }

        [Fact]
        public void Should_encode_and_decode_mixed_bytes()
        {
            AssertVector(new byte[] { 0x00, 0xe0, 0xff, 0x01 }, "AYy4A");
            AssertVector(
                new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f },
                "8%LBB${'eC(No8D-dMGF");
        }

        [Theory]
        [InlineData("AA/")]
        [InlineData("A A")]
        [InlineData("AA\x00")]
        [InlineData("AA\xff")]
        [InlineData("AA<")]
        public void Should_throw_on_unrecognised_character(string input)
        {
            var act = () => Base84.Decode(input);

            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData("A")]
        [InlineData("AE")]
        [InlineData("AAK")]
        [InlineData("AAAd")]
        [InlineData("rxQLr")]
        [InlineData("oi'-o")]
        [InlineData("AAAAAA")]
        [InlineData("rxQLrrxQLrrxQLrrxQLrrxQLrrxQLrrxQLrAA")]
        public void Should_throw_on_invalid_padding(string input)
        {
            var act = () => Base84.Decode(input);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Should_throw_on_null_input()
        {
            var encodeAct = () => Base84.Encode(null);
            var decodeAct = () => Base84.Decode(null);

            encodeAct.Should().Throw<ArgumentNullException>();
            decodeAct.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Should_round_trip_all_single_bytes()
        {
            for (var b = 0; b <= 255; b++)
            {
                var data = new[] { (byte)b };

                var encoded = Base84.Encode(data);
                Base84.Decode(encoded).Should().BeEquivalentTo(data);
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

                    var encoded = Base84.Encode(data);
                    Base84.Decode(encoded).Should().BeEquivalentTo(data);
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

            var encoded = Base84.Encode(data);
            Base84.Decode(encoded).Should().BeEquivalentTo(data);
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

                var encoded = Base84.Encode(data);
                Base84.Decode(encoded).Should().BeEquivalentTo(data);
            }
        }

        private const string AlphabetChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!#$%&'()+,-;=@[]^_`{}~";

        [Fact]
        public void Should_reject_altered_encoded_data_or_accept_it_and_round_trip()
        {
            // Any mutation of a validly-encoded string must either be rejected as invalid
            // padding, or - if it happens to still be well-formed - round trip cleanly
            // through a fresh encode.
            var random = new Random(999);
            var buffer = new byte[32];

            for (var i = 0; i < 5000; i++)
            {
                var len = random.Next(0, buffer.Length + 1);
                random.NextBytes(buffer);
                var data = new byte[len];
                Array.Copy(buffer, data, len);

                var encodedChars = Base84.Encode(data).ToCharArray();
                var alphabetChar = AlphabetChars[random.Next(AlphabetChars.Length)];

                switch (random.Next(3))
                {
                    case 0 when encodedChars.Length > 0:
                        encodedChars[random.Next(encodedChars.Length)] = alphabetChar;
                        break;
                    case 1:
                        Array.Resize(ref encodedChars, encodedChars.Length + 1);
                        encodedChars[encodedChars.Length - 1] = alphabetChar;
                        break;
                    case 2 when encodedChars.Length > 0:
                        Array.Resize(ref encodedChars, encodedChars.Length - 1);
                        break;
                }

                var altered = new string(encodedChars);

                byte[] decoded;
                try
                {
                    decoded = Base84.Decode(altered);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                Base84.Encode(decoded).Should().Be(altered);
            }
        }

        private static void AssertVector(string decoded, string encoded)
        {
            AssertVector(System.Text.Encoding.UTF8.GetBytes(decoded), encoded);
        }

        private static void AssertVector(byte[] decoded, string encoded)
        {
            Base84.Encode(decoded).Should().Be(encoded);
            Base84.Decode(encoded).Should().BeEquivalentTo(decoded);
        }
    }
}
