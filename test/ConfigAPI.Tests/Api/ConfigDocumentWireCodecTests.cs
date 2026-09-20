using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Api;
using MarcoZechner.ConfigAPI.V2.Domain;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Api
{
    [TestFixture]
    public sealed class ConfigDocumentWireCodecTests
    {
        [Test]
        public void Encode_And_Decode_Round_Trip_All_Core_Node_Kinds()
        {
            var document =
                new ConfigDocument(
                    new ConfigObjectNode(
                        new ConfigObjectEntry(
                            "Enabled",
                            ConfigScalarNode.Boolean(true)),
                        new ConfigObjectEntry(
                            "Count",
                            ConfigScalarNode.Integer(42)),
                        new ConfigObjectEntry(
                            "Ratio",
                            ConfigScalarNode.Float(1.5)),
                        new ConfigObjectEntry(
                            "Name",
                            ConfigScalarNode.String("example")),
                        new ConfigObjectEntry(
                            "Optional",
                            ConfigNullNode.Instance),
                        new ConfigObjectEntry(
                            "Nested",
                            new ConfigObjectNode(
                                new ConfigObjectEntry(
                                    "Value",
                                    ConfigScalarNode.Integer(-5)))),
                        new ConfigObjectEntry(
                            "Items",
                            new ConfigArrayNode(
                                ConfigScalarNode.String("first"),
                                ConfigNullNode.Instance,
                                ConfigScalarNode.Boolean(false)))));

            object encoded =
                ConfigDocumentWireCodec.Encode(document);

            ConfigDocument decoded =
                ConfigDocumentWireCodec.Decode(encoded);

            Assert.That(decoded, Is.EqualTo(document));
        }

        [Test]
        public void Encode_And_Decode_Round_Trip_Temporal_Scalars()
        {
            var date =
                new ConfigLocalDate(
                    2026,
                    9,
                    1);

            var time =
                new ConfigLocalTime(
                    23,
                    14,
                    15,
                    "123400");

            var document =
                new ConfigDocument(
                    new ConfigObjectNode(
                        new ConfigObjectEntry(
                            "Offset",
                            ConfigScalarNode.OffsetDateTime(
                                new ConfigOffsetDateTime(
                                    date,
                                    time,
                                    -90))),
                        new ConfigObjectEntry(
                            "UnknownOffset",
                            ConfigScalarNode.OffsetDateTime(
                                new ConfigOffsetDateTime(
                                    date,
                                    time,
                                    0,
                                    true))),
                        new ConfigObjectEntry(
                            "LocalDateTime",
                            ConfigScalarNode.LocalDateTime(
                                new ConfigLocalDateTime(
                                    date,
                                    time))),
                        new ConfigObjectEntry(
                            "LocalDate",
                            ConfigScalarNode.LocalDate(date)),
                        new ConfigObjectEntry(
                            "LocalTime",
                            ConfigScalarNode.LocalTime(time))));

            object encoded =
                ConfigDocumentWireCodec.Encode(document);

            ConfigDocument decoded =
                ConfigDocumentWireCodec.Decode(encoded);

            Assert.That(decoded, Is.EqualTo(document));
        }

        [Test]
        public void Encoded_Object_Uses_Ordered_Entry_Array()
        {
            var document =
                new ConfigDocument(
                    new ConfigObjectNode(
                        new ConfigObjectEntry(
                            "Second",
                            ConfigScalarNode.Integer(2)),
                        new ConfigObjectEntry(
                            "First",
                            ConfigScalarNode.Integer(1))));

            var encoded =
                ConfigDocumentWireCodec.Encode(document)
                    as IDictionary<string, object>;

            Assert.That(encoded, Is.Not.Null);
            Assert.That(encoded["Kind"], Is.EqualTo("Object"));

            var entries =
                encoded["Entries"] as object[];

            Assert.That(entries, Is.Not.Null);
            Assert.That(entries.Length, Is.EqualTo(2));

            var second =
                entries[0] as IDictionary<string, object>;

            var first =
                entries[1] as IDictionary<string, object>;

            Assert.Multiple(() =>
            {
                Assert.That(second, Is.Not.Null);
                Assert.That(first, Is.Not.Null);
                Assert.That(second["Name"], Is.EqualTo("Second"));
                Assert.That(first["Name"], Is.EqualTo("First"));
            });
        }

        [Test]
        public void Decode_Rejects_Unknown_Node_Kind()
        {
            var encoded =
                new Dictionary<string, object>(
                    StringComparer.Ordinal)
                {
                    { "Kind", "Mystery" }
                };

            var exception =
                Assert.Throws<ArgumentException>(
                    () => ConfigDocumentWireCodec.Decode(encoded));

            Assert.That(
                exception.Message,
                Does.Contain("Mystery"));
        }

        [Test]
        public void Decode_Rejects_Malformed_Object_Entry()
        {
            var encoded =
                new Dictionary<string, object>(
                    StringComparer.Ordinal)
                {
                    { "Kind", "Object" },
                    {
                        "Entries",
                        new object[]
                        {
                            new Dictionary<string, object>(
                                StringComparer.Ordinal)
                            {
                                { "Name", "Value" }
                            }
                        }
                    }
                };

            Assert.Throws<ArgumentException>(
                () => ConfigDocumentWireCodec.Decode(encoded));
        }

        [Test]
        public void Encode_And_Decode_Reject_Null_Documents_And_Payloads()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(
                    () => ConfigDocumentWireCodec.Encode(null));

                Assert.Throws<ArgumentNullException>(
                    () => ConfigDocumentWireCodec.Decode(null));
            });
        }
    }
}
