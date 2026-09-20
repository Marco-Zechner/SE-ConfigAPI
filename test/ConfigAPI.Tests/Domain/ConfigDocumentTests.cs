using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Domain;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Domain
{
    [TestFixture]
    public sealed class ConfigDocumentTests
    {
        [Test]
        public void Constructor_Rejects_Null_Root()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigDocument(null));
        }

        [Test]
        public void TryGet_Resolves_Nested_Object_Path_Ordinally()
        {
            var expected = ConfigScalarNode.Integer(1920);
            var document = CreateDocument(expected);

            ConfigNode actual;
            ConfigNode wrongCase;

            Assert.Multiple(() =>
            {
                Assert.That(
                    document.TryGet(new ConfigValuePath("Settings", "Display", "Width"), out actual),
                    Is.True);
                Assert.That(actual.Equals(expected), Is.True);

                Assert.That(
                    document.TryGet(new ConfigValuePath("settings", "Display", "Width"), out wrongCase),
                    Is.False);
                Assert.That(wrongCase, Is.Null);
            });
        }

        [Test]
        public void TryGet_Returns_False_For_Missing_Path()
        {
            var document = CreateDocument(ConfigScalarNode.Integer(1920));

            ConfigNode missing;

            Assert.Multiple(() =>
            {
                Assert.That(
                    document.TryGet(new ConfigValuePath("Settings", "Display", "Missing"), out missing),
                    Is.False);
                Assert.That(missing, Is.Null);
            });
        }

        [Test]
        public void TryGet_Does_Not_Traverse_Through_Non_Object_Node()
        {
            var document = CreateDocument(ConfigScalarNode.Integer(1920));

            ConfigNode missing;

            Assert.Multiple(() =>
            {
                Assert.That(
                    document.TryGet(new ConfigValuePath("Settings", "Display", "Width", "Value"), out missing),
                    Is.False);
                Assert.That(missing, Is.Null);
            });
        }

        [Test]
        public void WithValue_Replaces_Nested_Value_Without_Mutating_Original()
        {
            var originalWidth = ConfigScalarNode.Integer(1920);
            var replacementWidth = ConfigScalarNode.Integer(2560);
            var original = CreateDocument(originalWidth);

            var updated = original.WithValue(
                new ConfigValuePath("Settings", "Display", "Width"),
                replacementWidth);

            ConfigNode originalActual;
            ConfigNode updatedActual;

            Assert.Multiple(() =>
            {
                Assert.That(
                    original.TryGet(new ConfigValuePath("Settings", "Display", "Width"), out originalActual),
                    Is.True);
                Assert.That(originalActual.Equals(originalWidth), Is.True);

                Assert.That(
                    updated.TryGet(new ConfigValuePath("Settings", "Display", "Width"), out updatedActual),
                    Is.True);
                Assert.That(updatedActual.Equals(replacementWidth), Is.True);

                Assert.That(ReferenceEquals(original, updated), Is.False);
            });
        }

        [Test]
        public void WithValue_Reuses_Untouched_Immutable_Branches()
        {
            var original = CreateDocument(ConfigScalarNode.Integer(1920));

            ConfigNode originalNetwork;
            Assert.That(
                original.TryGet(new ConfigValuePath("Settings", "Network"), out originalNetwork),
                Is.True);

            var updated = original.WithValue(
                new ConfigValuePath("Settings", "Display", "Width"),
                ConfigScalarNode.Integer(2560));

            ConfigNode updatedNetwork;
            Assert.That(
                updated.TryGet(new ConfigValuePath("Settings", "Network"), out updatedNetwork),
                Is.True);

            Assert.That(ReferenceEquals(originalNetwork, updatedNetwork), Is.True);
        }

        [Test]
        public void WithValue_Rejects_Missing_Path()
        {
            var document = CreateDocument(ConfigScalarNode.Integer(1920));

            Assert.Throws<KeyNotFoundException>(() =>
                document.WithValue(
                    new ConfigValuePath("Settings", "Display", "Missing"),
                    ConfigScalarNode.Integer(1)));
        }

        [Test]
        public void WithValue_Rejects_Null_Value()
        {
            var document = CreateDocument(ConfigScalarNode.Integer(1920));

            Assert.Throws<ArgumentNullException>(() =>
                document.WithValue(
                    new ConfigValuePath("Settings", "Display", "Width"),
                    null));
        }

        [Test]
        public void Documents_Compare_By_Semantic_Root_Not_Object_Entry_Order()
        {
            var first = new ConfigDocument(
                new ConfigObjectNode(
                    new ConfigObjectEntry("Enabled", ConfigScalarNode.Boolean(true)),
                    new ConfigObjectEntry("Mode", ConfigScalarNode.String("Basic"))));

            var reordered = new ConfigDocument(
                new ConfigObjectNode(
                    new ConfigObjectEntry("Mode", ConfigScalarNode.String("Basic")),
                    new ConfigObjectEntry("Enabled", ConfigScalarNode.Boolean(true))));

            Assert.Multiple(() =>
            {
                Assert.That(first.Equals(reordered), Is.True);
                Assert.That(first.GetHashCode(), Is.EqualTo(reordered.GetHashCode()));
            });
        }

        private static ConfigDocument CreateDocument(ConfigNode width)
        {
            return new ConfigDocument(
                new ConfigObjectNode(
                    new ConfigObjectEntry(
                        "Settings",
                        new ConfigObjectNode(
                            new ConfigObjectEntry(
                                "Display",
                                new ConfigObjectNode(
                                    new ConfigObjectEntry("Width", width),
                                    new ConfigObjectEntry("Height", ConfigScalarNode.Integer(1080)))),
                            new ConfigObjectEntry(
                                "Network",
                                new ConfigObjectNode(
                                    new ConfigObjectEntry("Enabled", ConfigScalarNode.Boolean(true))))))));
        }
    }
}
