using System;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Serialization;
using Mz.Toml;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Serialization
{
    [TestFixture]
    public sealed class ConfigTomlDocumentCodecTests
    {
        [Test]
        public void Codec_Rejects_Null_Documents()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    ConfigTomlDocumentCodec.ToTomlDocument(null));

                Assert.Throws<ArgumentNullException>(() =>
                    ConfigTomlDocumentCodec.FromTomlDocument(null));
            });
        }

        [Test]
        public void ToTomlDocument_Maps_Config_Scalars_Objects_And_Arrays_In_Order()
        {
            var config = Document(
                Entry("Enabled", ConfigScalarNode.Boolean(true)),
                Entry("Count", ConfigScalarNode.Integer(12)),
                Entry("Ratio", ConfigScalarNode.Float(1.5)),
                Entry("Name", ConfigScalarNode.String("Flight")),
                Entry(
                    "Nested",
                    new ConfigObjectNode(
                        Entry("Mode", ConfigScalarNode.String("Auto")))),
                Entry(
                    "Values",
                    new ConfigArrayNode(
                        ConfigScalarNode.Integer(1),
                        ConfigScalarNode.String("two"),
                        ConfigScalarNode.Boolean(false))));

            var toml = ConfigTomlDocumentCodec.ToTomlDocument(config);

            var nested = (TomlTable)toml.Root["Nested"];
            var values = (TomlArray)toml.Root["Values"];

            Assert.Multiple(() =>
            {
                Assert.That(toml.Root.Keys, Is.EqualTo(new[]
                {
                    "Enabled",
                    "Count",
                    "Ratio",
                    "Name",
                    "Nested",
                    "Values"
                }));

                Assert.That(((TomlValue)toml.Root["Enabled"]).AsBoolean(), Is.True);
                Assert.That(((TomlValue)toml.Root["Count"]).AsInteger(), Is.EqualTo(12));
                Assert.That(((TomlValue)toml.Root["Ratio"]).AsFloat(), Is.EqualTo(1.5));
                Assert.That(((TomlValue)toml.Root["Name"]).AsString(), Is.EqualTo("Flight"));
                Assert.That(((TomlValue)nested["Mode"]).AsString(), Is.EqualTo("Auto"));

                Assert.That(values.Count, Is.EqualTo(3));
                Assert.That(((TomlValue)values[0]).AsInteger(), Is.EqualTo(1));
                Assert.That(((TomlValue)values[1]).AsString(), Is.EqualTo("two"));
                Assert.That(((TomlValue)values[2]).AsBoolean(), Is.False);
            });
        }

        [Test]
        public void FromTomlDocument_Maps_Parsed_Structure_And_Preserves_Table_Order()
        {
            var toml = Toml.Parse(
                "Enabled = true\n" +
                "Count = 4\n" +
                "Tags = [\"alpha\", 2, false]\n" +
                "[Nested]\n" +
                "Name = \"Example\"\n");

            var config = ConfigTomlDocumentCodec.FromTomlDocument(toml);

            ConfigNode enabled;
            ConfigNode count;
            ConfigNode tagsNode;
            ConfigNode nestedName;

            Assert.Multiple(() =>
            {
                Assert.That(config.Root.Entries.Count, Is.EqualTo(4));
                Assert.That(config.Root.Entries[0].Name, Is.EqualTo("Enabled"));
                Assert.That(config.Root.Entries[1].Name, Is.EqualTo("Count"));
                Assert.That(config.Root.Entries[2].Name, Is.EqualTo("Tags"));
                Assert.That(config.Root.Entries[3].Name, Is.EqualTo("Nested"));

                Assert.That(config.TryGet(new ConfigValuePath("Enabled"), out enabled), Is.True);
                Assert.That(enabled.Equals(ConfigScalarNode.Boolean(true)), Is.True);

                Assert.That(config.TryGet(new ConfigValuePath("Count"), out count), Is.True);
                Assert.That(count.Equals(ConfigScalarNode.Integer(4)), Is.True);

                Assert.That(config.TryGet(new ConfigValuePath("Tags"), out tagsNode), Is.True);
                var tags = (ConfigArrayNode)tagsNode;
                Assert.That(tags.Items.Count, Is.EqualTo(3));
                Assert.That(tags.Items[0].Equals(ConfigScalarNode.String("alpha")), Is.True);
                Assert.That(tags.Items[1].Equals(ConfigScalarNode.Integer(2)), Is.True);
                Assert.That(tags.Items[2].Equals(ConfigScalarNode.Boolean(false)), Is.True);

                Assert.That(
                    config.TryGet(new ConfigValuePath("Nested", "Name"), out nestedName),
                    Is.True);
                Assert.That(nestedName.Equals(ConfigScalarNode.String("Example")), Is.True);
            });
        }

        [Test]
        public void Config_To_Canonical_Toml_And_Back_RoundTrips_Supported_Structure()
        {
            var original = Document(
                Entry(
                    "Items",
                    new ConfigArrayNode(
                        new ConfigObjectNode(
                            Entry("Name", ConfigScalarNode.String("First")),
                            Entry("Weight", ConfigScalarNode.Float(2.5))),
                        new ConfigObjectNode(
                            Entry("Name", ConfigScalarNode.String("Second")),
                            Entry("Weight", ConfigScalarNode.Float(4.0))))),
                Entry(
                    "Settings",
                    new ConfigObjectNode(
                        Entry("Enabled", ConfigScalarNode.Boolean(true)),
                        Entry("Limit", ConfigScalarNode.Integer(25)))));

            var generated = ConfigTomlDocumentCodec.ToTomlDocument(original);
            var text = Toml.Write(generated);
            var reparsed = Toml.Parse(text);
            var roundTripped = ConfigTomlDocumentCodec.FromTomlDocument(reparsed);

            Assert.That(roundTripped.Equals(original), Is.True);
        }

        [Test]
        public void ToTomlDocument_Rejects_Config_Null_Explicitly()
        {
            var config = Document(Entry("Optional", ConfigNullNode.Instance));

            var exception = Assert.Throws<NotSupportedException>(() =>
                ConfigTomlDocumentCodec.ToTomlDocument(config));

            Assert.That(exception.Message, Does.Contain("null"));
        }

        [Test]
        public void Temporal_Values_RoundTrip_Without_String_Coercion()
        {
            var parsed = Toml.Parse(
                "Offset = 1979-05-27T07:32:00.123-00:00\n" +
                "LocalDateTime = 1979-05-27T07:32:00.456\n" +
                "LocalDate = 1979-05-27\n" +
                "LocalTime = 07:32:00.789\n");

            var config = ConfigTomlDocumentCodec.FromTomlDocument(parsed);
            var generated = ConfigTomlDocumentCodec.ToTomlDocument(config);
            var text = Toml.Write(generated);
            var roundTripped = ConfigTomlDocumentCodec.FromTomlDocument(Toml.Parse(text));

            ConfigNode offsetNode;
            ConfigNode localDateTimeNode;
            ConfigNode localDateNode;
            ConfigNode localTimeNode;

            Assert.Multiple(() =>
            {
                Assert.That(config.TryGet(new ConfigValuePath("Offset"), out offsetNode), Is.True);
                Assert.That(((ConfigScalarNode)offsetNode).Kind, Is.EqualTo(ConfigScalarKind.OffsetDateTime));

                var offset = (ConfigOffsetDateTime)((ConfigScalarNode)offsetNode).Value;
                Assert.That(offset.Date.Equals(new ConfigLocalDate(1979, 5, 27)), Is.True);
                Assert.That(offset.Time.Equals(new ConfigLocalTime(7, 32, 0, "123")), Is.True);
                Assert.That(offset.OffsetMinutes, Is.EqualTo(0));
                Assert.That(offset.IsUnknownLocalOffset, Is.True);

                Assert.That(config.TryGet(new ConfigValuePath("LocalDateTime"), out localDateTimeNode), Is.True);
                Assert.That(((ConfigScalarNode)localDateTimeNode).Kind, Is.EqualTo(ConfigScalarKind.LocalDateTime));

                Assert.That(config.TryGet(new ConfigValuePath("LocalDate"), out localDateNode), Is.True);
                Assert.That(((ConfigScalarNode)localDateNode).Kind, Is.EqualTo(ConfigScalarKind.LocalDate));

                Assert.That(config.TryGet(new ConfigValuePath("LocalTime"), out localTimeNode), Is.True);
                Assert.That(((ConfigScalarNode)localTimeNode).Kind, Is.EqualTo(ConfigScalarKind.LocalTime));

                Assert.That(text, Does.Contain("1979-05-27T07:32:00.123-00:00"));
                Assert.That(roundTripped.Equals(config), Is.True);
            });
        }
        [Test]
        public void FromTomlDocument_Rejects_Keys_That_Cannot_Form_Config_Value_Paths()
        {
            var toml = Toml.Parse("\"\" = 1\n");

            var exception = Assert.Throws<NotSupportedException>(() =>
                ConfigTomlDocumentCodec.FromTomlDocument(toml));

            Assert.That(exception.Message, Does.Contain("key"));
        }

        private static ConfigDocument Document(params ConfigObjectEntry[] entries)
        {
            return new ConfigDocument(new ConfigObjectNode(entries));
        }

        private static ConfigObjectEntry Entry(string name, ConfigNode value)
        {
            return new ConfigObjectEntry(name, value);
        }
    }
}