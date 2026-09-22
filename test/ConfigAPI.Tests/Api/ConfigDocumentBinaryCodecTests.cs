using System;
using MarcoZechner.ConfigAPI.Api;
using MarcoZechner.ConfigAPI.Domain;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Api
{
    [TestFixture]
    public sealed class ConfigDocumentBinaryCodecTests
    {
        [Test]
        public void Encode_And_Decode_Round_Trip_All_Semantic_Kinds()
        {
            var date = new ConfigLocalDate(2026, 9, 20);
            var time = new ConfigLocalTime(23, 59, 60, "0012300");
            var document = Document(
                Entry("Null", ConfigNullNode.Instance),
                Entry("Boolean", ConfigScalarNode.Boolean(true)),
                Entry("Integer", ConfigScalarNode.Integer(-9223372036854775807L)),
                Entry("Float", ConfigScalarNode.Float(1.5)),
                Entry("String", ConfigScalarNode.String("h\u00e9llo \u4e16\u754c")),
                Entry("OffsetDateTime", ConfigScalarNode.OffsetDateTime(new ConfigOffsetDateTime(date, time, -90))),
                Entry("UnknownOffset", ConfigScalarNode.OffsetDateTime(new ConfigOffsetDateTime(date, time, 0, true))),
                Entry("LocalDateTime", ConfigScalarNode.LocalDateTime(new ConfigLocalDateTime(date, time))),
                Entry("LocalDate", ConfigScalarNode.LocalDate(date)),
                Entry("LocalTime", ConfigScalarNode.LocalTime(time)),
                Entry("Nested", new ConfigObjectNode(Entry("Value", ConfigScalarNode.Integer(42)))),
                Entry("Array", new ConfigArrayNode(
                    ConfigScalarNode.String("first"),
                    ConfigNullNode.Instance,
                    new ConfigObjectNode(Entry("NestedBoolean", ConfigScalarNode.Boolean(false))))));

            byte[] encoded = ConfigDocumentBinaryCodec.Encode(document);
            ConfigDocument decoded = ConfigDocumentBinaryCodec.Decode(encoded);

            Assert.That(decoded, Is.EqualTo(document));
        }

        [Test]
        public void Encode_Is_Deterministic_And_Preserves_Object_Entry_Order()
        {
            var document = Document(
                Entry("Second", ConfigScalarNode.Integer(2)),
                Entry("First", ConfigScalarNode.Integer(1)),
                Entry("Third", ConfigScalarNode.String("three")));

            byte[] first = ConfigDocumentBinaryCodec.Encode(document);
            byte[] second = ConfigDocumentBinaryCodec.Encode(document);
            ConfigDocument decoded = ConfigDocumentBinaryCodec.Decode(first);

            Assert.Multiple(() =>
            {
                Assert.That(second, Is.EqualTo(first));
                Assert.That(decoded.Root.Entries.Count, Is.EqualTo(3));
                Assert.That(decoded.Root.Entries[0].Name, Is.EqualTo("Second"));
                Assert.That(decoded.Root.Entries[1].Name, Is.EqualTo("First"));
                Assert.That(decoded.Root.Entries[2].Name, Is.EqualTo("Third"));
            });
        }

        [Test]
        public void Decode_Rejects_Truncated_And_Trailing_Data()
        {
            byte[] encoded = ConfigDocumentBinaryCodec.Encode(
                Document(Entry("Value", ConfigScalarNode.String("payload"))));

            for (var length = 0; length < encoded.Length; length++)
            {
                var truncated = new byte[length];
                Array.Copy(encoded, truncated, length);
                Assert.Throws<ArgumentException>(() => ConfigDocumentBinaryCodec.Decode(truncated), "Accepted truncated payload length " + length);
            }

            var trailing = new byte[encoded.Length + 1];
            Array.Copy(encoded, trailing, encoded.Length);
            trailing[trailing.Length - 1] = 0x7f;

            Assert.Throws<ArgumentException>(() => ConfigDocumentBinaryCodec.Decode(trailing));
        }

        [Test]
        public void Decode_Rejects_Unsupported_Version()
        {
            byte[] encoded = ConfigDocumentBinaryCodec.Encode(Document());
            Assert.That(encoded.Length, Is.GreaterThan(4));

            encoded[4] = 0x7f;

            ArgumentException exception = Assert.Throws<ArgumentException>(() => ConfigDocumentBinaryCodec.Decode(encoded));
            Assert.That(exception.Message, Does.Contain("version").IgnoreCase);
        }

        [Test]
        public void Encode_And_Decode_Reject_Null()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() => ConfigDocumentBinaryCodec.Encode(null));
                Assert.Throws<ArgumentNullException>(() => ConfigDocumentBinaryCodec.Decode(null));
            });
        }

        private static ConfigDocument Document(params ConfigObjectEntry[] entries) => new ConfigDocument(new ConfigObjectNode(entries));
        private static ConfigObjectEntry Entry(string name, ConfigNode value) => new ConfigObjectEntry(name, value);
    }
}
