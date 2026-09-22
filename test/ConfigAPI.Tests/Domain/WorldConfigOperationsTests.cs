using System;
using MarcoZechner.ConfigAPI.Domain;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Domain
{
    [TestFixture]
    public sealed class WorldConfigOperationsTests
    {
        [Test]
        public void Apply_Changes_Applied_State_Without_Changing_Stored_State_Or_Variant()
        {
            var current = Snapshot(10, 10, 7UL, "default");
            var draft = Document(Entry("Value", Integer(15)));
            WorldConfigAuthorityResult result = WorldConfigOperations.Apply(current, 7UL, draft);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsChanged, Is.True);
                Assert.That(result.IsStale, Is.False);
                Assert.That(result.Snapshot.Revision, Is.EqualTo(8UL));
                Assert.That(result.Snapshot.CurrentVariant, Is.EqualTo("default"));
                AssertValue(result.Snapshot.Stored, Integer(10), "Value");
                AssertValue(result.Snapshot.Applied, Integer(15), "Value");
                Assert.That(result.Snapshot.HasUnsavedChanges, Is.True);
            });
        }

        [Test]
        public void Save_Commits_Applied_State_Without_Changing_Runtime_Value_Or_Variant()
        {
            var current = Snapshot(10, 15, 7UL, "default");
            WorldConfigAuthorityResult result = WorldConfigOperations.Save(current, 7UL);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsChanged, Is.True);
                Assert.That(result.IsStale, Is.False);
                Assert.That(result.Snapshot.Revision, Is.EqualTo(8UL));
                Assert.That(result.Snapshot.CurrentVariant, Is.EqualTo("default"));
                AssertValue(result.Snapshot.Stored, Integer(15), "Value");
                AssertValue(result.Snapshot.Applied, Integer(15), "Value");
                Assert.That(result.Snapshot.HasUnsavedChanges, Is.False);
            });
        }

        [Test]
        public void Apply_And_Save_Reject_Stale_Revision_Without_Changing_Authority()
        {
            var current = Snapshot(10, 15, 7UL, "default");
            WorldConfigAuthorityResult apply = WorldConfigOperations.Apply(current, 6UL, Document(Entry("Value", Integer(20))));
            WorldConfigAuthorityResult save = WorldConfigOperations.Save(current, 6UL);

            Assert.Multiple(() =>
            {
                Assert.That(apply.IsChanged, Is.False);
                Assert.That(apply.IsStale, Is.True);
                Assert.That(ReferenceEquals(apply.Snapshot, current), Is.True);
                Assert.That(save.IsChanged, Is.False);
                Assert.That(save.IsStale, Is.True);
                Assert.That(ReferenceEquals(save.Snapshot, current), Is.True);
            });
        }

        [Test]
        public void Reload_Replaces_Stored_And_Applied_State_And_Preserves_Variant()
        {
            var current = Snapshot(10, 15, 7UL, "combat");
            var loaded = Document(Entry("Value", Integer(30)));
            WorldConfigAuthorityResult result = WorldConfigOperations.Reload(current, 7UL, loaded);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsChanged, Is.True);
                Assert.That(result.IsStale, Is.False);
                Assert.That(result.Snapshot.Revision, Is.EqualTo(8UL));
                Assert.That(result.Snapshot.CurrentVariant, Is.EqualTo("combat"));
                AssertValue(result.Snapshot.Stored, Integer(30), "Value");
                AssertValue(result.Snapshot.Applied, Integer(30), "Value");
                Assert.That(result.Snapshot.HasUnsavedChanges, Is.False);
            });
        }

        [Test]
        public void Load_Replaces_Stored_And_Applied_State_And_Switches_Variant()
        {
            var current = Snapshot(10, 15, 7UL, "default");
            var loaded = Document(Entry("Value", Integer(30)));
            WorldConfigAuthorityResult result = WorldConfigOperations.Load(current, 7UL, loaded, "combat");

            Assert.Multiple(() =>
            {
                Assert.That(result.IsChanged, Is.True);
                Assert.That(result.IsStale, Is.False);
                Assert.That(result.Snapshot.Revision, Is.EqualTo(8UL));
                Assert.That(result.Snapshot.CurrentVariant, Is.EqualTo("combat"));
                AssertValue(result.Snapshot.Stored, Integer(30), "Value");
                AssertValue(result.Snapshot.Applied, Integer(30), "Value");
                Assert.That(result.Snapshot.HasUnsavedChanges, Is.False);
            });
        }

        [Test]
        public void SaveAs_Commits_Applied_State_And_Switches_Variant()
        {
            var current = Snapshot(10, 15, 7UL, "default");
            WorldConfigAuthorityResult result = WorldConfigOperations.SaveAs(current, 7UL, "cargo_2");

            Assert.Multiple(() =>
            {
                Assert.That(result.IsChanged, Is.True);
                Assert.That(result.IsStale, Is.False);
                Assert.That(result.Snapshot.Revision, Is.EqualTo(8UL));
                Assert.That(result.Snapshot.CurrentVariant, Is.EqualTo("cargo_2"));
                AssertValue(result.Snapshot.Stored, Integer(15), "Value");
                AssertValue(result.Snapshot.Applied, Integer(15), "Value");
                Assert.That(result.Snapshot.HasUnsavedChanges, Is.False);
            });
        }

        [Test]
        public void Reload_Load_And_SaveAs_Reject_Stale_Revision()
        {
            var current = Snapshot(10, 15, 7UL, "default");
            var loaded = Document(Entry("Value", Integer(30)));
            WorldConfigAuthorityResult reload = WorldConfigOperations.Reload(current, 6UL, loaded);
            WorldConfigAuthorityResult load = WorldConfigOperations.Load(current, 6UL, loaded, "combat");
            WorldConfigAuthorityResult saveAs = WorldConfigOperations.SaveAs(current, 6UL, "combat");

            Assert.Multiple(() =>
            {
                Assert.That(reload.IsChanged, Is.False);
                Assert.That(reload.IsStale, Is.True);
                Assert.That(ReferenceEquals(reload.Snapshot, current), Is.True);
                Assert.That(load.IsChanged, Is.False);
                Assert.That(load.IsStale, Is.True);
                Assert.That(ReferenceEquals(load.Snapshot, current), Is.True);
                Assert.That(saveAs.IsChanged, Is.False);
                Assert.That(saveAs.IsStale, Is.True);
                Assert.That(ReferenceEquals(saveAs.Snapshot, current), Is.True);
            });
        }

        [Test]
        public void Operations_Reject_Null_Required_State_And_Documents()
        {
            var current = Snapshot(10, 10, 7UL, "default");
            var document = Document();

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() => WorldConfigOperations.Apply(null, 7UL, document));
                Assert.Throws<ArgumentNullException>(() => WorldConfigOperations.Apply(current, 7UL, null));
                Assert.Throws<ArgumentNullException>(() => WorldConfigOperations.Save(null, 7UL));
                Assert.Throws<ArgumentNullException>(() => WorldConfigOperations.Reload(null, 7UL, document));
                Assert.Throws<ArgumentNullException>(() => WorldConfigOperations.Reload(current, 7UL, null));
                Assert.Throws<ArgumentNullException>(() => WorldConfigOperations.Load(null, 7UL, document, "combat"));
                Assert.Throws<ArgumentNullException>(() => WorldConfigOperations.Load(current, 7UL, null, "combat"));
                Assert.Throws<ArgumentNullException>(() => WorldConfigOperations.SaveAs(null, 7UL, "combat"));
            });
        }

        [Test]
        public void Variant_Operations_Reject_Empty_Variant()
        {
            var current = Snapshot(10, 10, 7UL, "default");
            var document = Document();

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(() => WorldConfigOperations.Load(current, 7UL, document, null));
                Assert.Throws<ArgumentException>(() => WorldConfigOperations.Load(current, 7UL, document, " "));
                Assert.Throws<ArgumentException>(() => WorldConfigOperations.SaveAs(current, 7UL, null));
                Assert.Throws<ArgumentException>(() => WorldConfigOperations.SaveAs(current, 7UL, " "));
            });
        }

        private static WorldConfigSnapshot Snapshot(long storedValue, long appliedValue, ulong revision, string variant)
        {
            return new WorldConfigSnapshot(
                new ConfigIdentity("12345", "ServerSettings"),
                Document(Entry("Value", Integer(storedValue))),
                Document(Entry("Value", Integer(appliedValue))),
                revision,
                variant);
        }

        private static ConfigDocument Document(params ConfigObjectEntry[] entries) => new ConfigDocument(new ConfigObjectNode(entries));
        private static ConfigObjectEntry Entry(string name, ConfigNode value) => new ConfigObjectEntry(name, value);
        private static ConfigScalarNode Integer(long value) => ConfigScalarNode.Integer(value);

        private static void AssertValue(ConfigDocument document, ConfigNode expected, params string[] path)
        {
            ConfigNode actual;
            Assert.That(document.TryGet(new ConfigValuePath(path), out actual), Is.True);
            Assert.That(actual.Equals(expected), Is.True);
        }
    }
}
