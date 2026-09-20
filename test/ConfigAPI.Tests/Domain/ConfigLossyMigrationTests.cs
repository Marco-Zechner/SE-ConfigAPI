using MarcoZechner.ConfigAPI.V2.Domain;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Domain
{
    [TestFixture]
    public sealed class ConfigLossyMigrationTests
    {
        [Test]
        public void Removed_Value_Is_Dropped_And_Requires_Backup()
        {
            var result = ConfigDefaultReconciler.Reconcile(
                Document(Entry("OldValue", Integer(10))),
                Document(Entry("OldValue", Integer(10))),
                Document());

            ConfigNode removedBaseline;
            ConfigNode removedPlayer;

            Assert.Multiple(() =>
            {
                Assert.That(
                    result.BaselineDefaults.TryGet(new ConfigValuePath("OldValue"), out removedBaseline),
                    Is.False);
                Assert.That(removedBaseline, Is.Null);

                Assert.That(
                    result.PlayerValues.TryGet(new ConfigValuePath("OldValue"), out removedPlayer),
                    Is.False);
                Assert.That(removedPlayer, Is.Null);

                Assert.That(result.RequiresBackup, Is.True);
                Assert.That(result.Changes.Count, Is.EqualTo(1));
                Assert.That(result.Changes[0].Kind, Is.EqualTo(ConfigDefaultChangeKind.RemovedValue));
                Assert.That(result.Changes[0].Path.Equals(new ConfigValuePath("OldValue")), Is.True);
                Assert.That(result.Changes[0].BaselineDefault.Equals(Integer(10)), Is.True);
                Assert.That(result.Changes[0].PlayerValue.Equals(Integer(10)), Is.True);
                Assert.That(result.Changes[0].CurrentDefault, Is.Null);
            });
        }

        [Test]
        public void Removed_Player_Override_Is_Dropped_From_Active_Config_But_Reported_For_Backup()
        {
            var result = ConfigDefaultReconciler.Reconcile(
                Document(Entry("OldValue", Integer(10))),
                Document(Entry("OldValue", Integer(15))),
                Document());

            Assert.Multiple(() =>
            {
                Assert.That(result.RequiresBackup, Is.True);
                Assert.That(result.Changes.Count, Is.EqualTo(1));
                Assert.That(result.Changes[0].Kind, Is.EqualTo(ConfigDefaultChangeKind.RemovedValue));
                Assert.That(result.Changes[0].BaselineDefault.Equals(Integer(10)), Is.True);
                Assert.That(result.Changes[0].PlayerValue.Equals(Integer(15)), Is.True);
                Assert.That(result.Changes[0].CurrentDefault, Is.Null);
            });
        }

        [Test]
        public void Removed_Nested_Value_Drops_Only_That_Value_And_Preserves_Recoverable_Override()
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
                        Entry("Height", Integer(100)))));

            var result = ConfigDefaultReconciler.Reconcile(
                baseline,
                player,
                currentDefaults);

            ConfigNode removedWidth;

            Assert.Multiple(() =>
            {
                Assert.That(result.RequiresBackup, Is.True);

                Assert.That(
                    result.PlayerValues.TryGet(new ConfigValuePath("Display", "Width"), out removedWidth),
                    Is.False);
                Assert.That(removedWidth, Is.Null);

                AssertValue(result.PlayerValues, Integer(125), "Display", "Height");
                AssertValue(result.BaselineDefaults, Integer(100), "Display", "Height");

                Assert.That(result.Changes.Count, Is.EqualTo(1));
                Assert.That(result.Changes[0].Kind, Is.EqualTo(ConfigDefaultChangeKind.RemovedValue));
                Assert.That(
                    result.Changes[0].Path.Equals(new ConfigValuePath("Display", "Width")),
                    Is.True);
                Assert.That(result.Changes[0].PlayerValue.Equals(Integer(150)), Is.True);
            });
        }

        [Test]
        public void Lossless_Default_Reconciliation_Does_Not_Require_Backup()
        {
            var result = ConfigDefaultReconciler.Reconcile(
                Document(Entry("Value", Integer(10))),
                Document(Entry("Value", Integer(15))),
                Document(Entry("Value", Integer(20))));

            Assert.That(result.RequiresBackup, Is.False);
        }

        [Test]
        public void Incompatible_Structure_Is_Regenerated_From_Current_Default_And_Requires_Backup()
        {
            var result = ConfigDefaultReconciler.Reconcile(
                Document(
                    Entry(
                        "Mode",
                        Object(
                            Entry("Name", String("basic"))))),
                Document(
                    Entry(
                        "Mode",
                        Object(
                            Entry("Name", String("custom"))))),
                Document(
                    Entry("Mode", String("new-format"))));

            Assert.Multiple(() =>
            {
                Assert.That(result.RequiresBackup, Is.True);
                AssertValue(result.BaselineDefaults, String("new-format"), "Mode");
                AssertValue(result.PlayerValues, String("new-format"), "Mode");

                Assert.That(result.Changes.Count, Is.EqualTo(1));
                Assert.That(
                    result.Changes[0].Kind,
                    Is.EqualTo(ConfigDefaultChangeKind.ResetIncompatibleStructure));
                Assert.That(result.Changes[0].Path.Equals(new ConfigValuePath("Mode")), Is.True);
                Assert.That(result.Changes[0].CurrentDefault.Equals(String("new-format")), Is.True);
            });
        }

        [Test]
        public void Incompatible_Branch_Does_Not_Discard_Compatible_Sibling_Override()
        {
            var baseline = Document(
                Entry(
                    "LegacyBranch",
                    Object(
                        Entry("Value", Integer(1)))),
                Entry("Sensitivity", Integer(10)));

            var player = Document(
                Entry(
                    "LegacyBranch",
                    Object(
                        Entry("Value", Integer(2)))),
                Entry("Sensitivity", Integer(15)));

            var currentDefaults = Document(
                Entry("LegacyBranch", String("replacement")),
                Entry("Sensitivity", Integer(10)));

            var result = ConfigDefaultReconciler.Reconcile(
                baseline,
                player,
                currentDefaults);

            Assert.Multiple(() =>
            {
                Assert.That(result.RequiresBackup, Is.True);

                AssertValue(result.PlayerValues, String("replacement"), "LegacyBranch");
                AssertValue(result.BaselineDefaults, String("replacement"), "LegacyBranch");

                AssertValue(result.PlayerValues, Integer(15), "Sensitivity");
                AssertValue(result.BaselineDefaults, Integer(10), "Sensitivity");
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
