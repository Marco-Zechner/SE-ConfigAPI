using System;
using MarcoZechner.ConfigAPI.V2.Domain;
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
        public void Snapshot_Captures_Authoritative_Config_State()
        {
            var identity = new ConfigIdentity("12345", "ServerSettings");
            var document = Document(Entry("Value", Integer(10)));

            var snapshot = new WorldConfigSnapshot(
                identity,
                document,
                7UL,
                "server.toml");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Identity.Equals(identity), Is.True);
                Assert.That(snapshot.Document.Equals(document), Is.True);
                Assert.That(snapshot.ServerIteration, Is.EqualTo(7UL));
                Assert.That(snapshot.CurrentFile, Is.EqualTo("server.toml"));
            });
        }

        [Test]
        public void Snapshot_Rejects_Null_Identity_And_Document()
        {
            var identity = new ConfigIdentity("12345", "ServerSettings");
            var document = Document();

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new WorldConfigSnapshot(null, document, 0UL, null));

                Assert.Throws<ArgumentNullException>(() =>
                    new WorldConfigSnapshot(identity, null, 0UL, null));
            });
        }

        [Test]
        public void Client_State_Starts_Draft_From_Authoritative_Document()
        {
            var snapshot = Snapshot(10, 3UL);

            var state = WorldConfigClientState.Create(snapshot);

            Assert.Multiple(() =>
            {
                Assert.That(ReferenceEquals(state.Authoritative, snapshot), Is.True);
                Assert.That(ReferenceEquals(state.Draft, snapshot.Document), Is.True);
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
                Assert.That(edited.Draft.Equals(draft), Is.True);

                AssertValue(snapshot.Document, Integer(10), "Value");
                AssertValue(state.Draft, Integer(10), "Value");
            });
        }

        [Test]
        public void New_Authoritative_Snapshot_Does_Not_Overwrite_Edited_Draft()
        {
            var original = Snapshot(10, 3UL);
            var editedDraft = Document(Entry("Value", Integer(15)));

            var state = WorldConfigClientState
                .Create(original)
                .WithDraft(editedDraft);

            var newer = new WorldConfigSnapshot(
                original.Identity,
                Document(Entry("Value", Integer(20))),
                4UL,
                "server.toml");

            var updated = state.ApplyAuthoritative(newer);

            Assert.Multiple(() =>
            {
                Assert.That(ReferenceEquals(updated.Authoritative, newer), Is.True);
                Assert.That(ReferenceEquals(updated.Draft, editedDraft), Is.True);
                AssertValue(updated.Authoritative.Document, Integer(20), "Value");
                AssertValue(updated.Draft, Integer(15), "Value");
            });
        }

        [Test]
        public void Client_State_Rejects_Authoritative_Snapshot_For_Different_Config()
        {
            var state = WorldConfigClientState.Create(Snapshot(10, 3UL));

            var other = new WorldConfigSnapshot(
                new ConfigIdentity("12345", "OtherSettings"),
                Document(Entry("Value", Integer(20))),
                4UL,
                "server.toml");

            Assert.Throws<ArgumentException>(() => state.ApplyAuthoritative(other));
        }

        [Test]
        public void ResetDraftToAuthoritative_Discards_Local_Draft_Edit()
        {
            var snapshot = Snapshot(10, 3UL);

            var state = WorldConfigClientState
                .Create(snapshot)
                .WithDraft(Document(Entry("Value", Integer(15))));

            var reset = state.ResetDraftToAuthoritative();

            Assert.Multiple(() =>
            {
                Assert.That(ReferenceEquals(reset.Authoritative, snapshot), Is.True);
                Assert.That(ReferenceEquals(reset.Draft, snapshot.Document), Is.True);
            });
        }

        [Test]
        public void Matching_Base_Iteration_Applies_Authoritative_Mutation_And_Increments_Revision()
        {
            var current = Snapshot(10, 7UL);
            var replacement = Document(Entry("Value", Integer(20)));

            var result = WorldConfigAuthority.Apply(
                current,
                7UL,
                replacement,
                "alternate.toml");

            Assert.Multiple(() =>
            {
                Assert.That(result.IsApplied, Is.True);
                Assert.That(result.IsStale, Is.False);
                Assert.That(result.Snapshot.ServerIteration, Is.EqualTo(8UL));
                Assert.That(result.Snapshot.CurrentFile, Is.EqualTo("alternate.toml"));
                Assert.That(result.Snapshot.Identity.Equals(current.Identity), Is.True);
                Assert.That(result.Snapshot.Document.Equals(replacement), Is.True);
            });
        }

        [Test]
        public void Stale_Base_Iteration_Is_Rejected_Without_Changing_Authoritative_State()
        {
            var current = Snapshot(10, 7UL);
            var replacement = Document(Entry("Value", Integer(20)));

            var result = WorldConfigAuthority.Apply(
                current,
                6UL,
                replacement,
                "alternate.toml");

            Assert.Multiple(() =>
            {
                Assert.That(result.IsApplied, Is.False);
                Assert.That(result.IsStale, Is.True);
                Assert.That(ReferenceEquals(result.Snapshot, current), Is.True);
                Assert.That(result.Snapshot.ServerIteration, Is.EqualTo(7UL));
                Assert.That(result.Snapshot.CurrentFile, Is.EqualTo("server.toml"));
                AssertValue(result.Snapshot.Document, Integer(10), "Value");
            });
        }

        [Test]
        public void Applying_Mutation_Rejects_Null_State_Or_Document()
        {
            var current = Snapshot(10, 7UL);

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    WorldConfigAuthority.Apply(
                        null,
                        7UL,
                        Document(),
                        "server.toml"));

                Assert.Throws<ArgumentNullException>(() =>
                    WorldConfigAuthority.Apply(
                        current,
                        7UL,
                        null,
                        "server.toml"));
            });
        }

        [Test]
        public void Applying_Mutation_Rejects_Server_Iteration_Overflow()
        {
            var current = new WorldConfigSnapshot(
                new ConfigIdentity("12345", "ServerSettings"),
                Document(Entry("Value", Integer(10))),
                ulong.MaxValue,
                "server.toml");

            Assert.Throws<InvalidOperationException>(() =>
                WorldConfigAuthority.Apply(
                    current,
                    ulong.MaxValue,
                    Document(Entry("Value", Integer(20))),
                    "server.toml"));
        }

        private static WorldConfigSnapshot Snapshot(long value, ulong iteration)
        {
            return new WorldConfigSnapshot(
                new ConfigIdentity("12345", "ServerSettings"),
                Document(Entry("Value", Integer(value))),
                iteration,
                "server.toml");
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

        private static void AssertValue(ConfigDocument document, ConfigNode expected, params string[] path)
        {
            ConfigNode actual;
            Assert.That(document.TryGet(new ConfigValuePath(path), out actual), Is.True);
            Assert.That(actual.Equals(expected), Is.True);
        }
    }
}
