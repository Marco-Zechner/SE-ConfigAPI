using System.Linq;
using MarcoZechner.ConfigAPI.V2.Domain;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Domain
{
    [TestFixture]
    public sealed class ConfigDefaultReconcilerTests
    {
        [Test]
        public void Unchanged_Default_Preserves_Player_Override_Without_Change_Event()
        {
            var baseline = Document(Entry("Value", Integer(10)));
            var player = Document(Entry("Value", Integer(15)));
            var currentDefaults = Document(Entry("Value", Integer(10)));

            var result = ConfigDefaultReconciler.Reconcile(baseline, player, currentDefaults);

            Assert.Multiple(() =>
            {
                AssertValue(result.PlayerValues, Integer(15), "Value");
                AssertValue(result.BaselineDefaults, Integer(10), "Value");
                Assert.That(result.Changes.Count, Is.EqualTo(0));
            });
        }

        [Test]
        public void Changed_Default_Is_Applied_When_Player_Still_Matches_Baseline()
        {
            var baseline = Document(Entry("Value", Integer(10)));
            var player = Document(Entry("Value", Integer(10)));
            var currentDefaults = Document(Entry("Value", Integer(20)));

            var result = ConfigDefaultReconciler.Reconcile(baseline, player, currentDefaults);

            Assert.Multiple(() =>
            {
                AssertValue(result.PlayerValues, Integer(20), "Value");
                AssertValue(result.BaselineDefaults, Integer(20), "Value");

                Assert.That(result.Changes.Count, Is.EqualTo(1));
                Assert.That(result.Changes[0].Kind, Is.EqualTo(ConfigDefaultChangeKind.AppliedChangedDefault));
                Assert.That(result.Changes[0].Path.Equals(new ConfigValuePath("Value")), Is.True);
                Assert.That(result.Changes[0].BaselineDefault.Equals(Integer(10)), Is.True);
                Assert.That(result.Changes[0].PlayerValue.Equals(Integer(10)), Is.True);
                Assert.That(result.Changes[0].CurrentDefault.Equals(Integer(20)), Is.True);
            });
        }

        [Test]
        public void Changed_Default_Remains_Pending_When_Player_Overrode_Baseline()
        {
            var baseline = Document(Entry("Value", Integer(10)));
            var player = Document(Entry("Value", Integer(15)));
            var currentDefaults = Document(Entry("Value", Integer(20)));

            var result = ConfigDefaultReconciler.Reconcile(baseline, player, currentDefaults);

            Assert.Multiple(() =>
            {
                AssertValue(result.PlayerValues, Integer(15), "Value");
                AssertValue(result.BaselineDefaults, Integer(10), "Value");

                Assert.That(result.Changes.Count, Is.EqualTo(1));
                Assert.That(result.Changes[0].Kind, Is.EqualTo(ConfigDefaultChangeKind.PendingChangedDefault));
                Assert.That(result.Changes[0].BaselineDefault.Equals(Integer(10)), Is.True);
                Assert.That(result.Changes[0].PlayerValue.Equals(Integer(15)), Is.True);
                Assert.That(result.Changes[0].CurrentDefault.Equals(Integer(20)), Is.True);
            });
        }

        [Test]
        public void Pending_Override_Uses_Latest_Default_Without_Moving_Baseline()
        {
            var baseline = Document(Entry("Value", Integer(10)));
            var player = Document(Entry("Value", Integer(15)));
            var currentDefaults = Document(Entry("Value", Integer(30)));

            var result = ConfigDefaultReconciler.Reconcile(baseline, player, currentDefaults);

            Assert.Multiple(() =>
            {
                AssertValue(result.PlayerValues, Integer(15), "Value");
                AssertValue(result.BaselineDefaults, Integer(10), "Value");

                Assert.That(result.Changes.Count, Is.EqualTo(1));
                Assert.That(result.Changes[0].Kind, Is.EqualTo(ConfigDefaultChangeKind.PendingChangedDefault));
                Assert.That(result.Changes[0].BaselineDefault.Equals(Integer(10)), Is.True);
                Assert.That(result.Changes[0].PlayerValue.Equals(Integer(15)), Is.True);
                Assert.That(result.Changes[0].CurrentDefault.Equals(Integer(30)), Is.True);
            });
        }

        [Test]
        public void Newly_Introduced_Value_Is_Added_To_Player_And_Baseline()
        {
            var baseline = Document();
            var player = Document();
            var currentDefaults = Document(Entry("NewValue", Integer(5)));

            var result = ConfigDefaultReconciler.Reconcile(baseline, player, currentDefaults);

            Assert.Multiple(() =>
            {
                AssertValue(result.PlayerValues, Integer(5), "NewValue");
                AssertValue(result.BaselineDefaults, Integer(5), "NewValue");

                Assert.That(result.Changes.Count, Is.EqualTo(1));
                Assert.That(result.Changes[0].Kind, Is.EqualTo(ConfigDefaultChangeKind.AddedDefault));
                Assert.That(result.Changes[0].Path.Equals(new ConfigValuePath("NewValue")), Is.True);
                Assert.That(result.Changes[0].BaselineDefault, Is.Null);
                Assert.That(result.Changes[0].PlayerValue, Is.Null);
                Assert.That(result.Changes[0].CurrentDefault.Equals(Integer(5)), Is.True);
            });
        }

        [Test]
        public void Nested_Defaults_Reconcile_Per_Leaf()
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
                        Entry("Height", Integer(100)))));

            var currentDefaults = Document(
                Entry(
                    "Display",
                    Object(
                        Entry("Width", Integer(200)),
                        Entry("Height", Integer(200)))));

            var result = ConfigDefaultReconciler.Reconcile(baseline, player, currentDefaults);

            Assert.Multiple(() =>
            {
                AssertValue(result.PlayerValues, Integer(150), "Display", "Width");
                AssertValue(result.PlayerValues, Integer(200), "Display", "Height");

                AssertValue(result.BaselineDefaults, Integer(100), "Display", "Width");
                AssertValue(result.BaselineDefaults, Integer(200), "Display", "Height");

                Assert.That(
                    result.Changes.Count(x => x.Kind == ConfigDefaultChangeKind.AppliedChangedDefault),
                    Is.EqualTo(1));
                Assert.That(
                    result.Changes.Count(x => x.Kind == ConfigDefaultChangeKind.PendingChangedDefault),
                    Is.EqualTo(1));

                var width = result.Changes.Single(x => x.Path.Equals(new ConfigValuePath("Display", "Width")));
                var height = result.Changes.Single(x => x.Path.Equals(new ConfigValuePath("Display", "Height")));

                Assert.That(width.Kind, Is.EqualTo(ConfigDefaultChangeKind.PendingChangedDefault));
                Assert.That(height.Kind, Is.EqualTo(ConfigDefaultChangeKind.AppliedChangedDefault));
            });
        }

        [Test]
        public void Six_Changed_Defaults_Apply_Four_And_Preserve_Two_Overrides()
        {
            var baseline = Document(
                Entry("A", Integer(10)),
                Entry("B", Integer(10)),
                Entry("C", Integer(10)),
                Entry("D", Integer(10)),
                Entry("E", Integer(10)),
                Entry("F", Integer(10)));

            var player = Document(
                Entry("A", Integer(15)),
                Entry("B", Integer(16)),
                Entry("C", Integer(10)),
                Entry("D", Integer(10)),
                Entry("E", Integer(10)),
                Entry("F", Integer(10)));

            var currentDefaults = Document(
                Entry("A", Integer(20)),
                Entry("B", Integer(21)),
                Entry("C", Integer(22)),
                Entry("D", Integer(23)),
                Entry("E", Integer(24)),
                Entry("F", Integer(25)));

            var result = ConfigDefaultReconciler.Reconcile(baseline, player, currentDefaults);

            Assert.Multiple(() =>
            {
                AssertValue(result.PlayerValues, Integer(15), "A");
                AssertValue(result.PlayerValues, Integer(16), "B");
                AssertValue(result.PlayerValues, Integer(22), "C");
                AssertValue(result.PlayerValues, Integer(23), "D");
                AssertValue(result.PlayerValues, Integer(24), "E");
                AssertValue(result.PlayerValues, Integer(25), "F");

                AssertValue(result.BaselineDefaults, Integer(10), "A");
                AssertValue(result.BaselineDefaults, Integer(10), "B");
                AssertValue(result.BaselineDefaults, Integer(22), "C");
                AssertValue(result.BaselineDefaults, Integer(23), "D");
                AssertValue(result.BaselineDefaults, Integer(24), "E");
                AssertValue(result.BaselineDefaults, Integer(25), "F");

                Assert.That(
                    result.Changes.Count(x => x.Kind == ConfigDefaultChangeKind.AppliedChangedDefault),
                    Is.EqualTo(4));
                Assert.That(
                    result.Changes.Count(x => x.Kind == ConfigDefaultChangeKind.PendingChangedDefault),
                    Is.EqualTo(2));
            });
        }

        [Test]
        public void Array_Default_Is_Reconciled_As_One_Value()
        {
            var oldTags = new ConfigArrayNode(String("alpha"));
            var newTags = new ConfigArrayNode(String("alpha"), String("beta"));

            var baseline = Document(Entry("Tags", oldTags));
            var player = Document(Entry("Tags", oldTags));
            var currentDefaults = Document(Entry("Tags", newTags));

            var result = ConfigDefaultReconciler.Reconcile(baseline, player, currentDefaults);

            Assert.Multiple(() =>
            {
                AssertValue(result.PlayerValues, newTags, "Tags");
                AssertValue(result.BaselineDefaults, newTags, "Tags");
                Assert.That(result.Changes.Count, Is.EqualTo(1));
                Assert.That(result.Changes[0].Kind, Is.EqualTo(ConfigDefaultChangeKind.AppliedChangedDefault));
                Assert.That(result.Changes[0].Path.Equals(new ConfigValuePath("Tags")), Is.True);
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
