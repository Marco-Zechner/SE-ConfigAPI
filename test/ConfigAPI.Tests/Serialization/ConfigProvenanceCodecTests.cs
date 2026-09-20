using System;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;
using MarcoZechner.ConfigAPI.V2.Serialization;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Serialization
{
    [TestFixture]
    public sealed class ConfigProvenanceCodecTests
    {
        [Test]
        public void Encode_Is_Deterministic_And_Contains_Explicit_Version()
        {
            var provenance = new ConfigProvenance(
                new ConfigIdentity(
                    "12345",
                    "Settings"),
                Document(
                    Entry(
                        "Value",
                        ConfigScalarNode.Integer(10)),
                    Entry(
                        "Optional",
                        ConfigNullNode.Instance)));

            var encoded =
                ConfigProvenanceCodec.Encode(
                    provenance);

            Assert.That(
                encoded,
                Is.EqualTo(
                    "CONFIGAPI-PROVENANCE:1;" +
                    "S5:12345" +
                    "S8:Settings" +
                    "O2;" +
                    "S5:ValueI10;" +
                    "S8:OptionalN;"));
        }

        [Test]
        public void Roundtrip_Preserves_All_Node_Kinds_And_Object_Order()
        {
            var baseline = Document(
                Entry(
                    "Null",
                    ConfigNullNode.Instance),
                Entry(
                    "Boolean",
                    ConfigScalarNode.Boolean(true)),
                Entry(
                    "Integer",
                    ConfigScalarNode.Integer(long.MinValue)),
                Entry(
                    "Float",
                    ConfigScalarNode.Float(-0.0)),
                Entry(
                    "String",
                    ConfigScalarNode.String(
                        "line 1\nline 2:;")),
                Entry(
                    "LocalDate",
                    ConfigScalarNode.LocalDate(
                        new ConfigLocalDate(
                            0,
                            2,
                            29))),
                Entry(
                    "LocalTime",
                    ConfigScalarNode.LocalTime(
                        new ConfigLocalTime(
                            23,
                            59,
                            60,
                            "12345678901234567890"))),
                Entry(
                    "LocalDateTime",
                    ConfigScalarNode.LocalDateTime(
                        new ConfigLocalDateTime(
                            new ConfigLocalDate(
                                2026,
                                9,
                                1),
                            new ConfigLocalTime(
                                19,
                                20,
                                30,
                                "0042")))),
                Entry(
                    "OffsetDateTime",
                    ConfigScalarNode.OffsetDateTime(
                        new ConfigOffsetDateTime(
                            new ConfigLocalDate(
                                2026,
                                9,
                                1),
                            new ConfigLocalTime(
                                19,
                                20,
                                30,
                                "9"),
                            0,
                            true))),
                Entry(
                    "Array",
                    new ConfigArrayNode(
                        ConfigScalarNode.String("a"),
                        ConfigNullNode.Instance,
                        new ConfigObjectNode(
                            Entry(
                                "Nested",
                                ConfigScalarNode.Float(
                                    double.PositiveInfinity))))));

            var provenance = new ConfigProvenance(
                new ConfigIdentity(
                    "owner\nid",
                    "config:key"),
                baseline);

            var decoded =
                ConfigProvenanceCodec.Decode(
                    ConfigProvenanceCodec.Encode(
                        provenance));

            Assert.Multiple(() =>
            {
                Assert.That(
                    decoded.Identity.Equals(
                        provenance.Identity),
                    Is.True);

                Assert.That(
                    decoded.BaselineDefaults.Equals(
                        baseline),
                    Is.True);

                Assert.That(
                    decoded.BaselineDefaults.Root.Entries[0].Name,
                    Is.EqualTo("Null"));

                Assert.That(
                    decoded.BaselineDefaults.Root.Entries[9].Name,
                    Is.EqualTo("Array"));
            });

            ConfigNode floatNode;

            Assert.That(
                decoded.BaselineDefaults.TryGet(
                    new ConfigValuePath("Float"),
                    out floatNode),
                Is.True);

            var bits =
                BitConverter.DoubleToInt64Bits(
                    (double)((ConfigScalarNode)floatNode).Value);

            Assert.That(
                bits,
                Is.EqualTo(
                    BitConverter.DoubleToInt64Bits(-0.0)));
        }

        [Test]
        public void Roundtrip_Preserves_Float_Bit_Pattern()
        {
            const long bits =
                unchecked((long)0x7FF8000000000042UL);

            var value =
                BitConverter.Int64BitsToDouble(bits);

            var provenance = new ConfigProvenance(
                new ConfigIdentity(
                    "owner",
                    "config"),
                Document(
                    Entry(
                        "Value",
                        ConfigScalarNode.Float(value))));

            var decoded =
                ConfigProvenanceCodec.Decode(
                    ConfigProvenanceCodec.Encode(
                        provenance));

            ConfigNode decodedNode;

            Assert.That(
                decoded.BaselineDefaults.TryGet(
                    new ConfigValuePath("Value"),
                    out decodedNode),
                Is.True);

            Assert.That(
                BitConverter.DoubleToInt64Bits(
                    (double)((ConfigScalarNode)decodedNode).Value),
                Is.EqualTo(bits));
        }

        [Test]
        public void Decode_Rejects_Unsupported_Version_And_Invalid_Header()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<NotSupportedException>(() =>
                    ConfigProvenanceCodec.Decode(
                        "CONFIGAPI-PROVENANCE:2;"));

                Assert.Throws<FormatException>(() =>
                    ConfigProvenanceCodec.Decode(
                        "not-provenance"));
            });
        }

        [Test]
        public void Decode_Rejects_Truncation_Invalid_Root_And_Trailing_Data()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<FormatException>(() =>
                    ConfigProvenanceCodec.Decode(
                        "CONFIGAPI-PROVENANCE:1;" +
                        "S5:owner" +
                        "S6:config" +
                        "O1;" +
                        "S5:Value" +
                        "S10:short"));

                Assert.Throws<FormatException>(() =>
                    ConfigProvenanceCodec.Decode(
                        "CONFIGAPI-PROVENANCE:1;" +
                        "S5:owner" +
                        "S6:config" +
                        "I1;"));

                Assert.Throws<FormatException>(() =>
                    ConfigProvenanceCodec.Decode(
                        "CONFIGAPI-PROVENANCE:1;" +
                        "S5:owner" +
                        "S6:config" +
                        "O0;" +
                        "extra"));
            });
        }

        [Test]
        public void Decode_Rejects_Invalid_Identity_Duplicate_Keys_And_Unknown_Node()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<FormatException>(() =>
                    ConfigProvenanceCodec.Decode(
                        "CONFIGAPI-PROVENANCE:1;" +
                        "S1: " +
                        "S6:config" +
                        "O0;"));

                Assert.Throws<FormatException>(() =>
                    ConfigProvenanceCodec.Decode(
                        "CONFIGAPI-PROVENANCE:1;" +
                        "S5:owner" +
                        "S6:config" +
                        "O2;" +
                        "S1:AI1;" +
                        "S1:AI2;"));

                Assert.Throws<FormatException>(() =>
                    ConfigProvenanceCodec.Decode(
                        "CONFIGAPI-PROVENANCE:1;" +
                        "S5:owner" +
                        "S6:config" +
                        "O1;" +
                        "S1:AX"));
            });
        }

        [Test]
        public void Decode_Rejects_Impossible_Collection_Count_Before_Allocation()
        {
            Assert.Throws<FormatException>(() =>
                ConfigProvenanceCodec.Decode(
                    "CONFIGAPI-PROVENANCE:1;" +
                    "S5:owner" +
                    "S6:config" +
                    "O1;" +
                    "S1:A" +
                    "A2147483647;"));
        }
        [Test]
        public void Codec_Rejects_Null_Arguments()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    ConfigProvenanceCodec.Encode(null));

                Assert.Throws<ArgumentNullException>(() =>
                    ConfigProvenanceCodec.Decode(null));

                Assert.Throws<ArgumentNullException>(() =>
                    new ConfigProvenance(
                        null,
                        Document()));

                Assert.Throws<ArgumentNullException>(() =>
                    new ConfigProvenance(
                        new ConfigIdentity(
                            "owner",
                            "config"),
                        null));
            });
        }

        private static ConfigDocument Document(
            params ConfigObjectEntry[] entries)
        {
            return new ConfigDocument(
                new ConfigObjectNode(entries));
        }

        private static ConfigObjectEntry Entry(
            string name,
            ConfigNode value)
        {
            return new ConfigObjectEntry(
                name,
                value);
        }
    }
}
