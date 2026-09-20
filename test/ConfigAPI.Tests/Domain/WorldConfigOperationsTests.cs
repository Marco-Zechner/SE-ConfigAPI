using System;
using MarcoZechner.ConfigAPI.V2.Domain;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Domain
{
    [TestFixture]
    public sealed class WorldConfigOperationsTests
    {
        [Test]
        public void Reload_Replaces_Authoritative_Document_And_Keeps_Current_File()
        {
            var current = Snapshot(10, 7UL, "server.toml");
            var loaded = Document(Entry("Value", Integer(20)));

            var result = WorldConfigOperations.Reload(
                current,
                7UL,
                loaded);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsApplied, Is.True);
                Assert.That(result.IsStale, Is.False);
                Assert.That(result.Snapshot.ServerIteration, Is.EqualTo(8UL));
                Assert.That(result.Snapshot.CurrentFile, Is.EqualTo("server.toml"));
                Assert.That(result.Snapshot.Document.Equals(loaded), Is.True);
            });
        }

        [Test]
        public void Reload_Rejects_Stale_Base_Iteration()
        {
            var current = Snapshot(10, 7UL, "server.toml");

            var result = WorldConfigOperations.Reload(
                current,
                6UL,
                Document(Entry("Value", Integer(20))));

            Assert.Multiple(() =>
            {
                Assert.That(result.IsApplied, Is.False);
                Assert.That(result.IsStale, Is.True);
                Assert.That(ReferenceEquals(result.Snapshot, current), Is.True);
            });
        }

        [Test]
        public void LoadAndSwitch_Replaces_Document_And_Current_File()
        {
            var current = Snapshot(10, 7UL, "server.toml");
            var loaded = Document(Entry("Value", Integer(30)));

            var result = WorldConfigOperations.LoadAndSwitch(
                current,
                7UL,
                loaded,
                "alternate.toml");

            Assert.Multiple(() =>
            {
                Assert.That(result.IsApplied, Is.True);
                Assert.That(result.Snapshot.ServerIteration, Is.EqualTo(8UL));
                Assert.That(result.Snapshot.CurrentFile, Is.EqualTo("alternate.toml"));
                Assert.That(result.Snapshot.Document.Equals(loaded), Is.True);
            });
        }

        [Test]
        public void Save_Applies_Submitted_Draft_And_Keeps_Current_File()
        {
            var current = Snapshot(10, 7UL, "server.toml");
            var draft = Document(Entry("Value", Integer(15)));

            var result = WorldConfigOperations.Save(
                current,
                7UL,
                draft);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsApplied, Is.True);
                Assert.That(result.Snapshot.ServerIteration, Is.EqualTo(8UL));
                Assert.That(result.Snapshot.CurrentFile, Is.EqualTo("server.toml"));
                Assert.That(result.Snapshot.Document.Equals(draft), Is.True);
            });
        }

        [Test]
        public void SaveAndSwitch_Applies_Draft_And_Switches_Current_File()
        {
            var current = Snapshot(10, 7UL, "server.toml");
            var draft = Document(Entry("Value", Integer(15)));

            var result = WorldConfigOperations.SaveAndSwitch(
                current,
                7UL,
                draft,
                "alternate.toml");

            Assert.Multiple(() =>
            {
                Assert.That(result.IsApplied, Is.True);
                Assert.That(result.Snapshot.ServerIteration, Is.EqualTo(8UL));
                Assert.That(result.Snapshot.CurrentFile, Is.EqualTo("alternate.toml"));
                Assert.That(result.Snapshot.Document.Equals(draft), Is.True);
            });
        }

        [Test]
        public void SaveAndSwitch_Rejects_Stale_Base_Iteration()
        {
            var current = Snapshot(10, 7UL, "server.toml");

            var result = WorldConfigOperations.SaveAndSwitch(
                current,
                6UL,
                Document(Entry("Value", Integer(15))),
                "alternate.toml");

            Assert.Multiple(() =>
            {
                Assert.That(result.IsApplied, Is.False);
                Assert.That(result.IsStale, Is.True);
                Assert.That(ReferenceEquals(result.Snapshot, current), Is.True);
            });
        }

        [Test]
        public void Export_Is_NonAuthoritative_And_Preserves_Exact_Snapshot()
        {
            var current = Snapshot(10, 7UL, "server.toml");
            var draft = Document(Entry("Value", Integer(15)));

            var export = WorldConfigOperations.Export(
                current,
                draft,
                "copy.toml",
                true);

            Assert.Multiple(() =>
            {
                Assert.That(ReferenceEquals(export.Authoritative, current), Is.True);
                Assert.That(export.Authoritative.ServerIteration, Is.EqualTo(7UL));
                Assert.That(export.Authoritative.CurrentFile, Is.EqualTo("server.toml"));
                AssertValue(export.Authoritative.Document, Integer(10), "Value");

                Assert.That(ReferenceEquals(export.Document, draft), Is.True);
                Assert.That(export.File, Is.EqualTo("copy.toml"));
                Assert.That(export.Overwrite, Is.True);
            });
        }

        [Test]
        public void Export_Has_No_Base_Iteration_And_Can_Export_An_Independent_Draft()
        {
            var current = Snapshot(10, 99UL, "server.toml");
            var draft = Document(Entry("Value", Integer(123)));

            var export = WorldConfigOperations.Export(
                current,
                draft,
                "draft-copy.toml",
                false);

            Assert.Multiple(() =>
            {
                Assert.That(export.Authoritative.ServerIteration, Is.EqualTo(99UL));
                Assert.That(export.Document.Equals(draft), Is.True);
                Assert.That(export.File, Is.EqualTo("draft-copy.toml"));
                Assert.That(export.Overwrite, Is.False);
            });
        }

        [Test]
        public void Operations_Reject_Null_Required_State_And_Documents()
        {
            var current = Snapshot(10, 7UL, "server.toml");
            var document = Document();

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    WorldConfigOperations.Reload(null, 7UL, document));

                Assert.Throws<ArgumentNullException>(() =>
                    WorldConfigOperations.Reload(current, 7UL, null));

                Assert.Throws<ArgumentNullException>(() =>
                    WorldConfigOperations.LoadAndSwitch(null, 7UL, document, "other.toml"));

                Assert.Throws<ArgumentNullException>(() =>
                    WorldConfigOperations.LoadAndSwitch(current, 7UL, null, "other.toml"));

                Assert.Throws<ArgumentNullException>(() =>
                    WorldConfigOperations.Save(null, 7UL, document));

                Assert.Throws<ArgumentNullException>(() =>
                    WorldConfigOperations.Save(current, 7UL, null));

                Assert.Throws<ArgumentNullException>(() =>
                    WorldConfigOperations.SaveAndSwitch(null, 7UL, document, "other.toml"));

                Assert.Throws<ArgumentNullException>(() =>
                    WorldConfigOperations.SaveAndSwitch(current, 7UL, null, "other.toml"));

                Assert.Throws<ArgumentNullException>(() =>
                    WorldConfigOperations.Export(null, document, "copy.toml", false));

                Assert.Throws<ArgumentNullException>(() =>
                    WorldConfigOperations.Export(current, null, "copy.toml", false));
            });
        }

        private static WorldConfigSnapshot Snapshot(long value, ulong iteration, string currentFile)
        {
            return new WorldConfigSnapshot(
                new ConfigIdentity("12345", "ServerSettings"),
                Document(Entry("Value", Integer(value))),
                iteration,
                currentFile);
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
