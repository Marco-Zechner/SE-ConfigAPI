using System;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Serialization;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Serialization
{
    [TestFixture]
    public sealed class ConfigTomlSourceDecoderTests
    {
        [Test]
        public void Decode_Projects_Known_Disabled_Assignment_As_Null()
        {
            var defaults = Document(
                Entry("Optional", ConfigScalarNode.String("default")),
                Entry("Count", ConfigScalarNode.Integer(1)));

            var decoded = ConfigTomlSourceDecoder.Decode(
                "#!Optional = \"previous\"\n" +
                "Count = 5\n",
                defaults);

            Assert.Multiple(() =>
            {
                AssertValue(decoded, ConfigNullNode.Instance, "Optional");
                AssertValue(decoded, ConfigScalarNode.Integer(5), "Count");
            });
        }

        [Test]
        public void Decode_Creates_Missing_Object_Path_For_Known_Disabled_Assignment()
        {
            var defaults = Document(
                Entry(
                    "Section",
                    new ConfigObjectNode(
                        Entry("Optional", ConfigScalarNode.Integer(10)))));

            var decoded = ConfigTomlSourceDecoder.Decode(
                "[Section]\n" +
                "#!Optional = 7\n",
                defaults);

            AssertValue(
                decoded,
                ConfigNullNode.Instance,
                "Section",
                "Optional");
        }

        [Test]
        public void Decode_Leaves_Unknown_Disabled_Assignment_Outside_Config_Document()
        {
            var defaults = Document(
                Entry("Known", ConfigScalarNode.Integer(1)));

            var decoded = ConfigTomlSourceDecoder.Decode(
                "#!Legacy = 99\n" +
                "Known = 2\n",
                defaults);

            ConfigNode ignored;

            Assert.Multiple(() =>
            {
                AssertValue(decoded, ConfigScalarNode.Integer(2), "Known");

                Assert.That(
                    decoded.TryGet(
                        new ConfigValuePath("Legacy"),
                        out ignored),
                    Is.False);
            });
        }

        [Test]
        public void Decode_Rejects_Ambiguous_Known_Disabled_Assignment()
        {
            var defaults = Document(
                Entry("Value", ConfigScalarNode.Integer(1)));

            Assert.Multiple(() =>
            {
                Assert.Throws<InvalidOperationException>(() =>
                    ConfigTomlSourceDecoder.Decode(
                        "Value = 2\n" +
                        "#!Value = 3\n",
                        defaults));

                Assert.Throws<InvalidOperationException>(() =>
                    ConfigTomlSourceDecoder.Decode(
                        "#!Value = 2\n" +
                        "#!Value = 3\n",
                        defaults));
            });
        }

        [Test]
        public void Decode_Rejects_Incompatible_Preserved_Disabled_Value()
        {
            var defaults = Document(
                Entry("Value", ConfigScalarNode.Integer(1)));

            Assert.Throws<NotSupportedException>(() =>
                ConfigTomlSourceDecoder.Decode(
                    "#!Value = \"old\"\n",
                    defaults));
        }

        [Test]
        public void Decode_Treats_Null_Default_As_Structurally_Permissive()
        {
            var defaults = Document(
                Entry("Optional", ConfigNullNode.Instance));

            var decoded = ConfigTomlSourceDecoder.Decode(
                "#!Optional = { old = [1, 2, 3] }\n",
                defaults);

            AssertValue(
                decoded,
                ConfigNullNode.Instance,
                "Optional");
        }

        [Test]
        public void Decode_Rejects_Disabled_Assignment_Inside_Array_Of_Tables()
        {
            var defaults = Document(
                Entry(
                    "Items",
                    new ConfigArrayNode(
                        new ConfigObjectNode(
                            Entry("Name", ConfigScalarNode.String("default"))))));

            Assert.Throws<NotSupportedException>(() =>
                ConfigTomlSourceDecoder.Decode(
                    "[[Items]]\n" +
                    "#!Name = \"old\"\n",
                    defaults));
        }

        [Test]
        public void Decode_Allows_Ordinary_Array_Of_Tables_Semantics()
        {
            var defaults = Document(
                Entry(
                    "Items",
                    new ConfigArrayNode(
                        new ConfigObjectNode(
                            Entry("Name", ConfigScalarNode.String("default"))))));

            var decoded = ConfigTomlSourceDecoder.Decode(
                "[[Items]]\n" +
                "Name = \"first\"\n" +
                "[[Items]]\n" +
                "Name = \"second\"\n",
                defaults);

            ConfigNode itemsNode;
            Assert.That(
                decoded.TryGet(
                    new ConfigValuePath("Items"),
                    out itemsNode),
                Is.True);

            var items = (ConfigArrayNode)itemsNode;

            Assert.Multiple(() =>
            {
                Assert.That(items.Items.Count, Is.EqualTo(2));

                Assert.That(
                    items.Items[0],
                    Is.EqualTo(
                        new ConfigObjectNode(
                            Entry(
                                "Name",
                                ConfigScalarNode.String("first")))));

                Assert.That(
                    items.Items[1],
                    Is.EqualTo(
                        new ConfigObjectNode(
                            Entry(
                                "Name",
                                ConfigScalarNode.String("second")))));
            });
        }

        [Test]
        public void Decode_Rejects_Disabled_Path_That_Traverses_Active_Scalar()
        {
            var defaults = Document(
                Entry(
                    "Section",
                    new ConfigObjectNode(
                        Entry("Child", ConfigScalarNode.Integer(1)))));

            Assert.Throws<NotSupportedException>(() =>
                ConfigTomlSourceDecoder.Decode(
                    "Section = 5\n" +
                    "#!Section.Child = 7\n",
                    defaults));
        }

        [Test]
        public void Decode_Rejects_Invalid_Source_And_Null_Arguments()
        {
            var defaults = Document(
                Entry("Value", ConfigScalarNode.Integer(1)));

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    ConfigTomlSourceDecoder.Decode(
                        null,
                        defaults));

                Assert.Throws<ArgumentNullException>(() =>
                    ConfigTomlSourceDecoder.Decode(
                        "Value = 1\n",
                        null));

                Assert.Throws<ArgumentException>(() =>
                    ConfigTomlSourceDecoder.Decode(
                        "Value =",
                        defaults));
            });
        }

        private static ConfigDocument Document(params ConfigObjectEntry[] entries)
        {
            return new ConfigDocument(new ConfigObjectNode(entries));
        }

        private static ConfigObjectEntry Entry(string name, ConfigNode value)
        {
            return new ConfigObjectEntry(name, value);
        }

        private static void AssertValue(
            ConfigDocument document,
            ConfigNode expected,
            params string[] path)
        {
            ConfigNode actual;

            Assert.That(
                document.TryGet(
                    new ConfigValuePath(path),
                    out actual),
                Is.True);

            Assert.That(actual.Equals(expected), Is.True);
        }
    }
}