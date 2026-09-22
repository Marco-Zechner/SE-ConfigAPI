using System;
using MarcoZechner.ConfigAPI.Domain;
using MarcoZechner.ConfigAPI.Persistence;
using MarcoZechner.ConfigAPI.Serialization;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Serialization
{
    [TestFixture]
    public sealed class ConfigDefaultsStoreCodecTests
    {
        [Test]
        public void Encode_Is_Deterministic_Regardless_Of_Insertion_Order()
        {
            var settings = new ConfigDefaultsEntry("Settings.default.toml", new ConfigIdentity("Example.Mod", "Settings"), Document(Entry("Value", Integer(10))));
            var tuning = new ConfigDefaultsEntry("Tuning.default.toml", new ConfigIdentity("Example.Mod", "Tuning"), Document(Entry("Value", Integer(20))));

            string first = ConfigDefaultsStoreCodec.Encode(new ConfigDefaultsStore().With(tuning).With(settings));
            string second = ConfigDefaultsStoreCodec.Encode(new ConfigDefaultsStore().With(settings).With(tuning));

            Assert.That(first, Is.EqualTo(second));
        }

        [Test]
        public void Roundtrip_Preserves_File_Identity_And_All_Document_Node_Kinds()
        {
            var baseline = Document(
                Entry("Null", ConfigNullNode.Instance),
                Entry("Boolean", ConfigScalarNode.Boolean(true)),
                Entry("Integer", ConfigScalarNode.Integer(long.MinValue)),
                Entry("Float", ConfigScalarNode.Float(-0.0)),
                Entry("String", ConfigScalarNode.String("line 1\nline 2")),
                Entry("LocalDate", ConfigScalarNode.LocalDate(new ConfigLocalDate(2026, 9, 22))),
                Entry("LocalTime", ConfigScalarNode.LocalTime(new ConfigLocalTime(23, 59, 60, "123456789"))),
                Entry("LocalDateTime", ConfigScalarNode.LocalDateTime(new ConfigLocalDateTime(new ConfigLocalDate(2026, 9, 22), new ConfigLocalTime(12, 34, 56, "42")))),
                Entry("OffsetDateTime", ConfigScalarNode.OffsetDateTime(new ConfigOffsetDateTime(new ConfigLocalDate(2026, 9, 22), new ConfigLocalTime(12, 34, 56, "9"), -90))),
                Entry("Array", new ConfigArrayNode(ConfigScalarNode.String("a"), ConfigNullNode.Instance, new ConfigObjectNode(Entry("Nested", ConfigScalarNode.Float(double.PositiveInfinity))))));

            var expected = new ConfigDefaultsEntry("Settings.default.toml", new ConfigIdentity("owner\nid", "Settings"), baseline);
            ConfigDefaultsStore decoded = ConfigDefaultsStoreCodec.Decode(ConfigDefaultsStoreCodec.Encode(new ConfigDefaultsStore().With(expected)));
            ConfigDefaultsEntry actual = null;

            Assert.Multiple(() =>
            {
                Assert.That(decoded.TryGet("Settings.default.toml", out actual), Is.True);
                Assert.That(actual.Identity.Equals(expected.Identity), Is.True);
                Assert.That(actual.BaselineDefaults.Equals(baseline), Is.True);
            });

            ConfigNode floatNode;
            Assert.That(actual.BaselineDefaults.TryGet(new ConfigValuePath("Float"), out floatNode), Is.True);
            Assert.That(BitConverter.DoubleToInt64Bits((double)((ConfigScalarNode)floatNode).Value), Is.EqualTo(BitConverter.DoubleToInt64Bits(-0.0)));
        }

        [Test]
        public void Decode_Rejects_Invalid_Base64_Magic_Version_Truncation_And_Trailing_Data()
        {
            string encoded = ConfigDefaultsStoreCodec.Encode(new ConfigDefaultsStore().With(
                new ConfigDefaultsEntry("Settings.default.toml", new ConfigIdentity("owner", "Settings"), Document(Entry("Value", Integer(10))))));

            byte[] invalidMagic = Convert.FromBase64String(encoded);
            invalidMagic[0] ^= 0xff;

            byte[] unsupportedVersion = Convert.FromBase64String(encoded);
            unsupportedVersion[4] = 0x7f;

            byte[] complete = Convert.FromBase64String(encoded);
            var truncated = new byte[complete.Length - 1];
            Buffer.BlockCopy(complete, 0, truncated, 0, truncated.Length);

            var trailing = new byte[complete.Length + 1];
            Buffer.BlockCopy(complete, 0, trailing, 0, complete.Length);
            trailing[trailing.Length - 1] = 1;

            Assert.Multiple(() =>
            {
                Assert.Throws<FormatException>(() => ConfigDefaultsStoreCodec.Decode("not-base64"));
                Assert.Throws<FormatException>(() => ConfigDefaultsStoreCodec.Decode(Convert.ToBase64String(invalidMagic)));
                Assert.Throws<NotSupportedException>(() => ConfigDefaultsStoreCodec.Decode(Convert.ToBase64String(unsupportedVersion)));
                Assert.Throws<FormatException>(() => ConfigDefaultsStoreCodec.Decode(Convert.ToBase64String(truncated)));
                Assert.Throws<FormatException>(() => ConfigDefaultsStoreCodec.Decode(Convert.ToBase64String(trailing)));
            });
        }

        [Test]
        public void Encode_Contains_Explicit_Magic_And_Version()
        {
            byte[] payload = Convert.FromBase64String(ConfigDefaultsStoreCodec.Encode(new ConfigDefaultsStore()));

            Assert.Multiple(() =>
            {
                Assert.That(payload.Length, Is.GreaterThanOrEqualTo(9));
                Assert.That(payload[0], Is.EqualTo(0x4D));
                Assert.That(payload[1], Is.EqualTo(0x5A));
                Assert.That(payload[2], Is.EqualTo(0x44));
                Assert.That(payload[3], Is.EqualTo(0x46));
                Assert.That(payload[4], Is.EqualTo(1));
            });
        }

        [Test]
        public void Decode_Rejects_Impossible_Entry_Count_Before_Allocation()
        {
            byte[] payload = Convert.FromBase64String(ConfigDefaultsStoreCodec.Encode(new ConfigDefaultsStore()));
            payload[5] = 0x01;
            payload[6] = 0x00;
            payload[7] = 0x01;
            payload[8] = 0x00;

            Assert.Throws<FormatException>(() => ConfigDefaultsStoreCodec.Decode(Convert.ToBase64String(payload)));
        }

        [Test]
        public void Decode_Rejects_Duplicate_File_Entries()
        {
            var identity = new ConfigIdentity("owner", "Settings");
            var store = new ConfigDefaultsStore()
                .With(new ConfigDefaultsEntry("a.toml", identity, Document(Entry("Value", Integer(1)))))
                .With(new ConfigDefaultsEntry("b.toml", identity, Document(Entry("Value", Integer(2)))));

            byte[] payload = Convert.FromBase64String(ConfigDefaultsStoreCodec.Encode(store));
            int secondFile = FindAscii(payload, "b.toml");
            Assert.That(secondFile, Is.GreaterThanOrEqualTo(0));
            payload[secondFile] = (byte)'a';

            Assert.Throws<FormatException>(() => ConfigDefaultsStoreCodec.Decode(Convert.ToBase64String(payload)));
        }

        [Test]
        public void Decode_Rejects_Invalid_Utf8_File_Name()
        {
            var store = new ConfigDefaultsStore().With(
                new ConfigDefaultsEntry("x.toml", new ConfigIdentity("owner", "Settings"), Document()));

            byte[] payload = Convert.FromBase64String(ConfigDefaultsStoreCodec.Encode(store));
            int file = FindAscii(payload, "x.toml");
            Assert.That(file, Is.GreaterThanOrEqualTo(0));
            payload[file] = 0xff;

            Assert.Throws<FormatException>(() => ConfigDefaultsStoreCodec.Decode(Convert.ToBase64String(payload)));
        }

        [Test]
        public void Store_And_Codec_Reject_Invalid_Arguments()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(() => new ConfigDefaultsEntry(" ", new ConfigIdentity("owner", "Settings"), Document()));
                Assert.Throws<ArgumentNullException>(() => new ConfigDefaultsEntry("Settings.default.toml", null, Document()));
                Assert.Throws<ArgumentNullException>(() => new ConfigDefaultsEntry("Settings.default.toml", new ConfigIdentity("owner", "Settings"), null));
                Assert.Throws<ArgumentNullException>(() => ConfigDefaultsStoreCodec.Encode(null));
                Assert.Throws<ArgumentNullException>(() => ConfigDefaultsStoreCodec.Decode(null));
            });
        }

        private static int FindAscii(byte[] payload, string value)
        {
            byte[] expected = System.Text.Encoding.ASCII.GetBytes(value);

            for (var offset = 0; offset <= payload.Length - expected.Length; offset++)
            {
                var matches = true;

                for (var index = 0; index < expected.Length; index++)
                {
                    if (payload[offset + index] == expected[index])
                        continue;

                    matches = false;
                    break;
                }

                if (matches)
                    return offset;
            }

            return -1;
        }

        private static ConfigDocument Document(params ConfigObjectEntry[] entries) { return new ConfigDocument(new ConfigObjectNode(entries)); }
        private static ConfigObjectEntry Entry(string name, ConfigNode value) { return new ConfigObjectEntry(name, value); }
        private static ConfigScalarNode Integer(long value) { return ConfigScalarNode.Integer(value); }
    }
}
