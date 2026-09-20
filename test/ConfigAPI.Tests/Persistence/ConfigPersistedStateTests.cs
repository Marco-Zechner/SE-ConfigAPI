using System;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Persistence
{
    [TestFixture]
    public sealed class ConfigPersistedStateTests
    {
        [Test]
        public void Persisted_State_Captures_Identity_Player_Values_Baseline_And_Current_File()
        {
            var identity = new ConfigIdentity("12345", "Settings");
            var player = Document(Entry("Value", Integer(15)));
            var baseline = Document(Entry("Value", Integer(10)));

            var state = new ConfigPersistedState(
                identity,
                player,
                baseline,
                "settings.toml");

            Assert.Multiple(() =>
            {
                Assert.That(ReferenceEquals(state.Identity, identity), Is.True);
                Assert.That(ReferenceEquals(state.PlayerValues, player), Is.True);
                Assert.That(ReferenceEquals(state.BaselineDefaults, baseline), Is.True);
                Assert.That(state.CurrentFile, Is.EqualTo("settings.toml"));
            });
        }

        [Test]
        public void Persisted_State_Rejects_Null_Required_Values()
        {
            var identity = new ConfigIdentity("12345", "Settings");
            var document = Document();

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new ConfigPersistedState(
                        null,
                        document,
                        document,
                        null));

                Assert.Throws<ArgumentNullException>(() =>
                    new ConfigPersistedState(
                        identity,
                        null,
                        document,
                        null));

                Assert.Throws<ArgumentNullException>(() =>
                    new ConfigPersistedState(
                        identity,
                        document,
                        null,
                        null));
            });
        }

        [Test]
        public void Reconcile_Auto_Applies_Changed_Default_For_Untouched_Player_Value()
        {
            var state = State(
                Document(Entry("Value", Integer(10))),
                Document(Entry("Value", Integer(10))));

            var result = ConfigPersistedStateReconciler.Reconcile(
                state,
                Document(Entry("Value", Integer(20))));

            Assert.Multiple(() =>
            {
                Assert.That(result.RequiresBackup, Is.False);
                Assert.That(result.Changes.Count, Is.EqualTo(1));
                Assert.That(
                    result.Changes[0].Kind,
                    Is.EqualTo(ConfigDefaultChangeKind.AppliedChangedDefault));

                Assert.That(ReferenceEquals(result.State.Identity, state.Identity), Is.True);
                Assert.That(result.State.CurrentFile, Is.EqualTo("settings.toml"));

                AssertValue(result.State.PlayerValues, Integer(20), "Value");
                AssertValue(result.State.BaselineDefaults, Integer(20), "Value");
            });
        }

        [Test]
        public void Reconcile_Preserves_Player_Override_And_Old_Baseline_When_Default_Changes()
        {
            var state = State(
                Document(Entry("Value", Integer(15))),
                Document(Entry("Value", Integer(10))));

            var result = ConfigPersistedStateReconciler.Reconcile(
                state,
                Document(Entry("Value", Integer(20))));

            Assert.Multiple(() =>
            {
                Assert.That(result.RequiresBackup, Is.False);
                Assert.That(result.Changes.Count, Is.EqualTo(1));
                Assert.That(
                    result.Changes[0].Kind,
                    Is.EqualTo(ConfigDefaultChangeKind.PendingChangedDefault));

                AssertValue(result.State.PlayerValues, Integer(15), "Value");
                AssertValue(result.State.BaselineDefaults, Integer(10), "Value");
            });
        }

        [Test]
        public void Reconcile_Lossy_Removal_Drops_Active_Value_And_Requires_Backup()
        {
            var state = State(
                Document(
                    Entry("Value", Integer(15)),
                    Entry("Removed", Integer(99))),
                Document(
                    Entry("Value", Integer(10)),
                    Entry("Removed", Integer(50))));

            var result = ConfigPersistedStateReconciler.Reconcile(
                state,
                Document(Entry("Value", Integer(20))));

            ConfigNode ignored;

            Assert.Multiple(() =>
            {
                Assert.That(result.RequiresBackup, Is.True);
                Assert.That(
                    result.State.PlayerValues.TryGet(
                        new ConfigValuePath("Removed"),
                        out ignored),
                    Is.False);

                Assert.That(
                    result.State.BaselineDefaults.TryGet(
                        new ConfigValuePath("Removed"),
                        out ignored),
                    Is.False);

                Assert.That(
                    HasChange(
                        result,
                        ConfigDefaultChangeKind.RemovedValue,
                        "Removed"),
                    Is.True);

                AssertValue(result.State.PlayerValues, Integer(15), "Value");
                AssertValue(result.State.BaselineDefaults, Integer(10), "Value");
            });
        }

        [Test]
        public void Reconcile_Does_Not_Mutate_Input_Persisted_State()
        {
            var player = Document(Entry("Value", Integer(10)));
            var baseline = Document(Entry("Value", Integer(10)));
            var state = State(player, baseline);

            var result = ConfigPersistedStateReconciler.Reconcile(
                state,
                Document(Entry("Value", Integer(20))));

            Assert.Multiple(() =>
            {
                Assert.That(ReferenceEquals(state.PlayerValues, player), Is.True);
                Assert.That(ReferenceEquals(state.BaselineDefaults, baseline), Is.True);

                AssertValue(state.PlayerValues, Integer(10), "Value");
                AssertValue(state.BaselineDefaults, Integer(10), "Value");

                AssertValue(result.State.PlayerValues, Integer(20), "Value");
                AssertValue(result.State.BaselineDefaults, Integer(20), "Value");
            });
        }

        [Test]
        public void Reconciler_Rejects_Null_State_Or_Current_Defaults()
        {
            var state = State(Document(), Document());

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    ConfigPersistedStateReconciler.Reconcile(
                        null,
                        Document()));

                Assert.Throws<ArgumentNullException>(() =>
                    ConfigPersistedStateReconciler.Reconcile(
                        state,
                        null));
            });
        }

        private static ConfigPersistedState State(
            ConfigDocument player,
            ConfigDocument baseline)
        {
            return new ConfigPersistedState(
                new ConfigIdentity("12345", "Settings"),
                player,
                baseline,
                "settings.toml");
        }

        private static bool HasChange(
            ConfigPersistedStateReconciliationResult result,
            ConfigDefaultChangeKind kind,
            params string[] path)
        {
            var expectedPath = new ConfigValuePath(path);

            foreach (var change in result.Changes)
            {
                if (change.Kind == kind && change.Path.Equals(expectedPath))
                    return true;
            }

            return false;
        }

        private static ConfigDocument Document(params ConfigObjectEntry[] entries)
        {
            return new ConfigDocument(new ConfigObjectNode(entries));
        }

        private static ConfigObjectEntry Entry(string name, ConfigNode value)
        {
            return new ConfigObjectEntry(name, value);
        }

        private static ConfigScalarNode Integer(long value)
        {
            return ConfigScalarNode.Integer(value);
        }

        private static void AssertValue(
            ConfigDocument document,
            ConfigNode expected,
            params string[] path)
        {
            ConfigNode actual;
            Assert.That(
                document.TryGet(new ConfigValuePath(path), out actual),
                Is.True);

            Assert.That(actual.Equals(expected), Is.True);
        }
    }
}
