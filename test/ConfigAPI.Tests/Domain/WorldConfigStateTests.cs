using System;
using MarcoZechner.ConfigAPI.Domain;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Domain
{
    [TestFixture]
    public sealed class WorldConfigStateTests
    {
        [Test]
        public void ConfigLocation_Uses_Local_Global_World_Vocabulary()
        {
            Assert.Multiple(() =>
            {
                Assert.That((int)ConfigLocation.Local, Is.EqualTo(0));
                Assert.That((int)ConfigLocation.Global, Is.EqualTo(1));
                Assert.That((int)ConfigLocation.World, Is.EqualTo(2));
            });
        }

        [Test]
        public void Snapshot_Captures_Stored_Applied_Revision_And_Current_Variant()
        {
            var identity = new ConfigIdentity("12345", "ServerSettings");
            var stored = Document(Entry("Value", Integer(10)));
            var applied = Document(Entry("Value", Integer(15)));
            var snapshot = new WorldConfigSnapshot(identity, stored, applied, 7UL, "combat");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Identity.Equals(identity), Is.True);
                Assert.That(snapshot.Stored.Equals(stored), Is.True);
                Assert.That(snapshot.Applied.Equals(applied), Is.True);
                Assert.That(snapshot.Revision, Is.EqualTo(7UL));
                Assert.That(snapshot.CurrentVariant, Is.EqualTo("combat"));
                Assert.That(snapshot.HasUnsavedChanges, Is.True);
            });
        }

        [Test]
        public void Snapshot_Reports_No_Unsaved_Changes_When_Stored_Equals_Applied()
        {
            var document = Document(Entry("Value", Integer(10)));
            var snapshot = new WorldConfigSnapshot(new ConfigIdentity("12345", "ServerSettings"), document, document, 7UL, "default");

            Assert.That(snapshot.HasUnsavedChanges, Is.False);
        }

        [Test]
        public void Snapshot_Rejects_Invalid_Required_State()
        {
            var identity = new ConfigIdentity("12345", "ServerSettings");
            var document = Document();

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() => new WorldConfigSnapshot(null, document, document, 0UL, "default"));
                Assert.Throws<ArgumentNullException>(() => new WorldConfigSnapshot(identity, null, document, 0UL, "default"));
                Assert.Throws<ArgumentNullException>(() => new WorldConfigSnapshot(identity, document, null, 0UL, "default"));
                Assert.Throws<ArgumentException>(() => new WorldConfigSnapshot(identity, document, document, 0UL, null));
                Assert.Throws<ArgumentException>(() => new WorldConfigSnapshot(identity, document, document, 0UL, " "));
                Assert.Throws<ArgumentException>(() => new WorldConfigSnapshot(identity, document, document, 0UL, "bad.variant"));
            });
        }

        [Test]
        public void Client_State_Starts_Draft_From_Authoritative_Applied_State()
        {
            var snapshot = Snapshot(10, 3UL);
            var state = WorldConfigClientState.Create(snapshot);

            Assert.Multiple(() =>
            {
                Assert.That(ReferenceEquals(state.Authoritative, snapshot), Is.True);
                Assert.That(ReferenceEquals(state.Draft, snapshot.Applied), Is.True);
                Assert.That(ReferenceEquals(state.DraftBaseApplied, snapshot.Applied), Is.True);
                Assert.That(state.DraftBaseRevision, Is.EqualTo(3UL));
                Assert.That(state.HasDraftChanges, Is.False);
                Assert.That(state.IsDraftStale, Is.False);
            });
        }

        [Test]
        public void Client_Draft_Can_Change_Without_Mutating_Authoritative_State()
        {
            var snapshot = Snapshot(10, 3UL);
            var state = WorldConfigClientState.Create(snapshot);
            var draft = Document(Entry("Value", Integer(15)));
            var edited = state.WithDraft(draft);

            Assert.Multiple(() =>
            {
                Assert.That(ReferenceEquals(edited.Authoritative, snapshot), Is.True);
                Assert.That(ReferenceEquals(edited.Draft, draft), Is.True);
                Assert.That(ReferenceEquals(edited.DraftBaseApplied, snapshot.Applied), Is.True);
                Assert.That(edited.DraftBaseRevision, Is.EqualTo(3UL));
                Assert.That(edited.HasDraftChanges, Is.True);
                Assert.That(edited.IsDraftStale, Is.False);
                AssertValue(snapshot.Applied, Integer(10), "Value");
            });
        }

        [Test]
        public void New_Authoritative_Snapshot_Does_Not_Overwrite_Edited_Draft()
        {
            var original = Snapshot(10, 3UL);
            var draft = Document(Entry("Value", Integer(15)));
            var state = WorldConfigClientState.Create(original).WithDraft(draft);
            var newer = new WorldConfigSnapshot(original.Identity, original.Stored, Document(Entry("Value", Integer(20))), 4UL, "default");
            var updated = state.ApplyAuthoritative(newer);

            Assert.Multiple(() =>
            {
                Assert.That(ReferenceEquals(updated.Authoritative, newer), Is.True);
                Assert.That(ReferenceEquals(updated.Draft, draft), Is.True);
                Assert.That(ReferenceEquals(updated.DraftBaseApplied, original.Applied), Is.True);
                Assert.That(updated.DraftBaseRevision, Is.EqualTo(3UL));
                Assert.That(updated.HasDraftChanges, Is.True);
                Assert.That(updated.IsDraftStale, Is.True);
            });
        }

        [Test]
        public void Clean_Client_Draft_Follows_New_Authoritative_Applied_State()
        {
            var original = Snapshot(10, 3UL);
            var state = WorldConfigClientState.Create(original);
            var applied = Document(Entry("Value", Integer(20)));
            var newer = new WorldConfigSnapshot(original.Identity, original.Stored, applied, 4UL, "combat");
            var updated = state.ApplyAuthoritative(newer);

            Assert.Multiple(() =>
            {
                Assert.That(ReferenceEquals(updated.Authoritative, newer), Is.True);
                Assert.That(ReferenceEquals(updated.Draft, applied), Is.True);
                Assert.That(ReferenceEquals(updated.DraftBaseApplied, applied), Is.True);
                Assert.That(updated.DraftBaseRevision, Is.EqualTo(4UL));
                Assert.That(updated.HasDraftChanges, Is.False);
                Assert.That(updated.IsDraftStale, Is.False);
            });
        }

        [Test]
        public void Matching_Authoritative_Applied_Clears_Edited_Draft_Staleness()
        {
            var original = Snapshot(10, 3UL);
            var draft = Document(Entry("Value", Integer(15)));
            var state = WorldConfigClientState.Create(original).WithDraft(draft);
            var newer = new WorldConfigSnapshot(original.Identity, original.Stored, draft, 4UL, "default");
            var updated = state.ApplyAuthoritative(newer);

            Assert.Multiple(() =>
            {
                Assert.That(ReferenceEquals(updated.Authoritative, newer), Is.True);
                Assert.That(ReferenceEquals(updated.Draft, draft), Is.True);
                Assert.That(ReferenceEquals(updated.DraftBaseApplied, draft), Is.True);
                Assert.That(updated.DraftBaseRevision, Is.EqualTo(4UL));
                Assert.That(updated.HasDraftChanges, Is.False);
                Assert.That(updated.IsDraftStale, Is.False);
            });
        }

        [Test]
        public void Client_State_Rejects_Authoritative_Snapshot_For_Different_Config()
        {
            var state = WorldConfigClientState.Create(Snapshot(10, 3UL));
            var otherDocument = Document(Entry("Value", Integer(20)));
            var other = new WorldConfigSnapshot(new ConfigIdentity("12345", "OtherSettings"), otherDocument, otherDocument, 4UL, "default");

            Assert.Throws<ArgumentException>(() => state.ApplyAuthoritative(other));
        }

        [Test]
        public void ResetDraftToAuthoritative_Discards_Local_Draft_Edit()
        {
            var snapshot = Snapshot(10, 3UL);
            var state = WorldConfigClientState.Create(snapshot).WithDraft(Document(Entry("Value", Integer(15))));
            var reset = state.ResetDraftToAuthoritative();

            Assert.Multiple(() =>
            {
                Assert.That(ReferenceEquals(reset.Authoritative, snapshot), Is.True);
                Assert.That(ReferenceEquals(reset.Draft, snapshot.Applied), Is.True);
                Assert.That(ReferenceEquals(reset.DraftBaseApplied, snapshot.Applied), Is.True);
                Assert.That(reset.DraftBaseRevision, Is.EqualTo(snapshot.Revision));
                Assert.That(reset.HasDraftChanges, Is.False);
                Assert.That(reset.IsDraftStale, Is.False);
            });
        }

        [Test]
        public void Matching_Revision_Updates_Authority_And_Increments_Revision()
        {
            var current = Snapshot(10, 7UL);
            var stored = Document(Entry("Value", Integer(20)));
            var applied = Document(Entry("Value", Integer(25)));
            var result = WorldConfigAuthority.Update(current, 7UL, stored, applied, "combat");

            Assert.Multiple(() =>
            {
                Assert.That(result.IsChanged, Is.True);
                Assert.That(result.IsStale, Is.False);
                Assert.That(result.Snapshot.Revision, Is.EqualTo(8UL));
                Assert.That(result.Snapshot.CurrentVariant, Is.EqualTo("combat"));
                Assert.That(result.Snapshot.Identity.Equals(current.Identity), Is.True);
                Assert.That(result.Snapshot.Stored.Equals(stored), Is.True);
                Assert.That(result.Snapshot.Applied.Equals(applied), Is.True);
            });
        }

        [Test]
        public void Stale_Revision_Is_Rejected_Without_Changing_Authoritative_State()
        {
            var current = Snapshot(10, 7UL);
            var replacement = Document(Entry("Value", Integer(20)));
            var result = WorldConfigAuthority.Update(current, 6UL, replacement, replacement, "combat");

            Assert.Multiple(() =>
            {
                Assert.That(result.IsChanged, Is.False);
                Assert.That(result.IsStale, Is.True);
                Assert.That(ReferenceEquals(result.Snapshot, current), Is.True);
            });
        }

        [Test]
        public void Authority_Update_Rejects_Null_Required_State()
        {
            var current = Snapshot(10, 7UL);
            var document = Document();

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() => WorldConfigAuthority.Update(null, 7UL, document, document, "default"));
                Assert.Throws<ArgumentNullException>(() => WorldConfigAuthority.Update(current, 7UL, null, document, "default"));
                Assert.Throws<ArgumentNullException>(() => WorldConfigAuthority.Update(current, 7UL, document, null, "default"));
            });
        }

        [Test]
        public void Authority_Update_Rejects_Revision_Overflow()
        {
            var document = Document(Entry("Value", Integer(10)));
            var current = new WorldConfigSnapshot(new ConfigIdentity("12345", "ServerSettings"), document, document, ulong.MaxValue, "default");

            Assert.Throws<InvalidOperationException>(() => WorldConfigAuthority.Update(current, ulong.MaxValue, document, document, "default"));
        }

        private static WorldConfigSnapshot Snapshot(long value, ulong revision)
        {
            var document = Document(Entry("Value", Integer(value)));
            return new WorldConfigSnapshot(new ConfigIdentity("12345", "ServerSettings"), document, document, revision, "default");
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
