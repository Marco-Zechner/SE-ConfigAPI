using MarcoZechner.ConfigAPI.V2.Domain;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Domain
{
    [TestFixture]
    public sealed class ConfigTemporalReconciliationTests
    {
        [Test]
        public void Changed_Temporal_Default_Auto_Applies_When_Player_Is_Untouched()
        {
            var oldValue = LocalDate(2026, 9, 1);
            var newValue = LocalDate(2026, 9, 2);

            var result = ConfigDefaultReconciler.Reconcile(
                Document(Entry("Date", oldValue)),
                Document(Entry("Date", oldValue)),
                Document(Entry("Date", newValue)));

            Assert.Multiple(() =>
            {
                Assert.That(result.RequiresBackup, Is.False);
                Assert.That(result.Changes.Count, Is.EqualTo(1));
                Assert.That(
                    result.Changes[0].Kind,
                    Is.EqualTo(ConfigDefaultChangeKind.AppliedChangedDefault));

                AssertValue(result.BaselineDefaults, newValue, "Date");
                AssertValue(result.PlayerValues, newValue, "Date");
            });
        }

        [Test]
        public void Temporal_Kind_Change_Is_Structurally_Incompatible()
        {
            var oldValue = LocalDate(2026, 9, 1);
            var replacement = ConfigScalarNode.LocalDateTime(
                new ConfigLocalDateTime(
                    new ConfigLocalDate(2026, 9, 1),
                    new ConfigLocalTime(12, 0, 0)));

            var result = ConfigDefaultReconciler.Reconcile(
                Document(Entry("When", oldValue)),
                Document(Entry("When", oldValue)),
                Document(Entry("When", replacement)));

            Assert.Multiple(() =>
            {
                Assert.That(result.RequiresBackup, Is.True);
                Assert.That(result.Changes.Count, Is.EqualTo(1));
                Assert.That(
                    result.Changes[0].Kind,
                    Is.EqualTo(ConfigDefaultChangeKind.ResetIncompatibleStructure));

                AssertValue(result.BaselineDefaults, replacement, "When");
                AssertValue(result.PlayerValues, replacement, "When");
            });
        }

        private static ConfigScalarNode LocalDate(int year, int month, int day)
        {
            return ConfigScalarNode.LocalDate(new ConfigLocalDate(year, month, day));
        }

        private static ConfigDocument Document(params ConfigObjectEntry[] entries)
        {
            return new ConfigDocument(new ConfigObjectNode(entries));
        }

        private static ConfigObjectEntry Entry(string name, ConfigNode value)
        {
            return new ConfigObjectEntry(name, value);
        }

        private static void AssertValue(ConfigDocument document, ConfigNode expected, params string[] path)
        {
            ConfigNode actual;
            Assert.That(document.TryGet(new ConfigValuePath(path), out actual), Is.True);
            Assert.That(actual.Equals(expected), Is.True);
        }
    }
}