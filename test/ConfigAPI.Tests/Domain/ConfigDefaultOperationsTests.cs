using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Domain;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Domain
{
    [TestFixture]
    public sealed class ConfigDefaultOperationsTests
    {
        [Test]
        public void RevertToDefault_Replaces_Player_Override_And_Advances_Baseline()
        {
            var baseline = Document(Entry("Value", Integer(10)));
            var player = Document(Entry("Value", Integer(15)));
            var currentDefaults = Document(Entry("Value", Integer(20)));

            var result = ConfigDefaultOperations.RevertToDefault(
                baseline,
                player,
                currentDefaults,
                new ConfigValuePath("Value"));

            Assert.Multiple(() =>
            {
                AssertValue(result.BaselineDefaults, Integer(20), "Value");
                AssertValue(result.PlayerValues, Integer(20), "Value");

                AssertValue(baseline, Integer(10), "Value");
                AssertValue(player, Integer(15), "Value");
            });
        }

        [Test]
        public void RevertToDefault_Uses_Current_Default_Even_When_Default_Did_Not_Change()
        {
            var baseline = Document(Entry("Value", Integer(10)));
            var player = Document(Entry("Value", Integer(15)));
            var currentDefaults = Document(Entry("Value", Integer(10)));

            var result = ConfigDefaultOperations.RevertToDefault(
                baseline,
                player,
                currentDefaults,
                new ConfigValuePath("Value"));

            Assert.Multiple(() =>
            {
                AssertValue(result.BaselineDefaults, Integer(10), "Value");
                AssertValue(result.PlayerValues, Integer(10), "Value");
            });
        }

        [Test]
        public void RevertToDefault_Works_For_Nested_Value()
        {
            var baseline = Document(
                Entry(
                    "Display",
                    Object(
                        Entry("Width", Integer(100)),
                        Entry("Height", Integer(100)))));

            var player = Document(
                Entry(
                    "Display",
                    Object(
                        Entry("Width", Integer(150)),
                        Entry("Height", Integer(125)))));

            var currentDefaults = Document(
                Entry(
                    "Display",
                    Object(
                        Entry("Width", Integer(200)),
                        Entry("Height", Integer(100)))));

            var result = ConfigDefaultOperations.RevertToDefault(
                baseline,
                player,
                currentDefaults,
                new ConfigValuePath("Display", "Width"));

            Assert.Multiple(() =>
            {
                AssertValue(result.BaselineDefaults, Integer(200), "Display", "Width");
                AssertValue(result.PlayerValues, Integer(200), "Display", "Width");

                AssertValue(result.BaselineDefaults, Integer(100), "Display", "Height");
                AssertValue(result.PlayerValues, Integer(125), "Display", "Height");
            });
        }

        [Test]
        public void RevertToDefault_Reuses_ConfigDocument_Copy_On_Write_Semantics()
        {
            var baseline = Document(
                Entry("Value", Integer(10)),
                Entry("Other", Object(Entry("Enabled", Boolean(true)))));

            var player = Document(
                Entry("Value", Integer(15)),
                Entry("Other", Object(Entry("Enabled", Boolean(true)))));

            var currentDefaults = Document(
                Entry("Value", Integer(20)),
                Entry("Other", Object(Entry("Enabled", Boolean(true)))));

            ConfigNode originalBaselineOther;
            ConfigNode originalPlayerOther;

            Assert.That(
                baseline.TryGet(new ConfigValuePath("Other"), out originalBaselineOther),
                Is.True);
            Assert.That(
                player.TryGet(new ConfigValuePath("Other"), out originalPlayerOther),
                Is.True);

            var result = ConfigDefaultOperations.RevertToDefault(
                baseline,
                player,
                currentDefaults,
                new ConfigValuePath("Value"));

            ConfigNode updatedBaselineOther;
            ConfigNode updatedPlayerOther;

            Assert.That(
                result.BaselineDefaults.TryGet(new ConfigValuePath("Other"), out updatedBaselineOther),
                Is.True);
            Assert.That(
                result.PlayerValues.TryGet(new ConfigValuePath("Other"), out updatedPlayerOther),
                Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(ReferenceEquals(originalBaselineOther, updatedBaselineOther), Is.True);
                Assert.That(ReferenceEquals(originalPlayerOther, updatedPlayerOther), Is.True);
            });
        }

        [Test]
        public void RevertToDefault_Treats_Array_As_One_Value()
        {
            var baselineTags = new ConfigArrayNode(String("alpha"));
            var playerTags = new ConfigArrayNode(String("custom"));
            var currentTags = new ConfigArrayNode(String("alpha"), String("beta"));

            var result = ConfigDefaultOperations.RevertToDefault(
                Document(Entry("Tags", baselineTags)),
                Document(Entry("Tags", playerTags)),
                Document(Entry("Tags", currentTags)),
                new ConfigValuePath("Tags"));

            Assert.Multiple(() =>
            {
                AssertValue(result.BaselineDefaults, currentTags, "Tags");
                AssertValue(result.PlayerValues, currentTags, "Tags");
            });
        }

        [Test]
        public void RevertToDefault_Rejects_Path_Missing_From_Current_Defaults()
        {
            Assert.Throws<KeyNotFoundException>(() =>
                ConfigDefaultOperations.RevertToDefault(
                    Document(Entry("OldValue", Integer(10))),
                    Document(Entry("OldValue", Integer(15))),
                    Document(),
                    new ConfigValuePath("OldValue")));
        }

        [Test]
        public void RevertToDefault_Rejects_Inconsistent_Baseline_Or_Player_Structure()
        {
            var currentDefaults = Document(Entry("Value", Integer(20)));

            Assert.Multiple(() =>
            {
                Assert.Throws<KeyNotFoundException>(() =>
                    ConfigDefaultOperations.RevertToDefault(
                        Document(),
                        Document(Entry("Value", Integer(15))),
                        currentDefaults,
                        new ConfigValuePath("Value")));

                Assert.Throws<KeyNotFoundException>(() =>
                    ConfigDefaultOperations.RevertToDefault(
                        Document(Entry("Value", Integer(10))),
                        Document(),
                        currentDefaults,
                        new ConfigValuePath("Value")));
            });
        }

        [Test]
        public void RevertToDefault_Rejects_Null_Inputs()
        {
            var document = Document(Entry("Value", Integer(10)));
            var path = new ConfigValuePath("Value");

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    ConfigDefaultOperations.RevertToDefault(null, document, document, path));

                Assert.Throws<ArgumentNullException>(() =>
                    ConfigDefaultOperations.RevertToDefault(document, null, document, path));

                Assert.Throws<ArgumentNullException>(() =>
                    ConfigDefaultOperations.RevertToDefault(document, document, null, path));

                Assert.Throws<ArgumentNullException>(() =>
                    ConfigDefaultOperations.RevertToDefault(document, document, document, null));
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

        private static ConfigObjectNode Object(params ConfigObjectEntry[] entries)
        {
            return new ConfigObjectNode(entries);
        }

        private static ConfigScalarNode Integer(long value)
        {
            return ConfigScalarNode.Integer(value);
        }

        private static ConfigScalarNode Boolean(bool value)
        {
            return ConfigScalarNode.Boolean(value);
        }

        private static ConfigScalarNode String(string value)
        {
            return ConfigScalarNode.String(value);
        }

        private static void AssertValue(ConfigDocument document, ConfigNode expected, params string[] path)
        {
            ConfigNode actual;
            Assert.That(document.TryGet(new ConfigValuePath(path), out actual), Is.True);
            Assert.That(actual.Equals(expected), Is.True);
        }
    }
}
