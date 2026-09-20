using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;
using MarcoZechner.ConfigAPI.V2.Serialization;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Persistence
{
    [TestFixture]
    public sealed class ConfigPersistedStateWriterTests
    {
        [Test]
        public void Write_Commits_Active_Toml_Before_Provenance()
        {
            var identity = Identity();
            var defaults =
                Document(
                    Entry("Value", Integer(10)));

            var storage = new RecordingStorage();
            var loadResult =
                Load(
                    storage,
                    identity,
                    defaults);

            storage.Operations.Clear();

            var writer =
                new ConfigPersistedStateWriter(
                    storage,
                    Clock());

            var result =
                writer.Write(
                    ConfigLocation.World,
                    loadResult,
                    defaults);

            Assert.Multiple(() =>
            {
                Assert.That(
                    storage.Operations,
                    Is.EqualTo(
                        new[]
                        {
                            "WRITE|World|settings.toml",
                            "WRITE|World|settings.toml.configapi.provenance"
                        }));

                Assert.That(
                    result.BackupFile,
                    Is.Null);

                Assert.That(
                    result.UsedCanonicalRegeneration,
                    Is.False);

                Assert.That(
                    storage.Get(
                        ConfigLocation.World,
                        "settings.toml"),
                    Is.EqualTo(
                        result.ActiveSource));

                Assert.That(
                    storage.Get(
                        ConfigLocation.World,
                        result.ProvenanceFile),
                    Is.EqualTo(
                        result.ProvenanceSource));

                Assert.That(
                    ConfigProvenanceCodec.Decode(
                        result.ProvenanceSource)
                        .BaselineDefaults
                        .Equals(defaults),
                    Is.True);
            });
        }

        [Test]
        public void Lossy_Write_Backs_Up_Exact_Original_Before_Active_And_Provenance()
        {
            var identity = Identity();
            var baseline =
                Document(
                    Entry(
                        "Legacy",
                        Object(
                            Entry(
                                "Value",
                                Integer(9)))));

            var currentDefaults =
                Document(
                    Entry("Known", Integer(1)));

            const string original =
                "[Legacy]\n" +
                "Value = 9\n";

            var storage = new RecordingStorage();
            storage.Set(
                ConfigLocation.World,
                "settings.toml",
                original);

            SetProvenance(
                storage,
                identity,
                baseline);

            var loadResult =
                Load(
                    storage,
                    identity,
                    currentDefaults);

            storage.Operations.Clear();

            var writer =
                new ConfigPersistedStateWriter(
                    storage,
                    Clock());

            var result =
                writer.Write(
                    ConfigLocation.World,
                    loadResult,
                    currentDefaults);

            Assert.Multiple(() =>
            {
                Assert.That(
                    result.UsedCanonicalRegeneration,
                    Is.True);

                Assert.That(
                    result.BackupFile,
                    Is.EqualTo(
                        "settings.toml.20260901T190000.0000000Z.bak"));

                Assert.That(
                    storage.Operations,
                    Is.EqualTo(
                        new[]
                        {
                            "READ|World|settings.toml",
                            "READ|World|settings.toml.20260901T190000.0000000Z.bak",
                            "WRITE|World|settings.toml.20260901T190000.0000000Z.bak",
                            "WRITE|World|settings.toml",
                            "WRITE|World|settings.toml.configapi.provenance"
                        }));

                Assert.That(
                    storage.Get(
                        ConfigLocation.World,
                        result.BackupFile),
                    Is.EqualTo(original));

                Assert.That(
                    storage.Get(
                        ConfigLocation.World,
                        "settings.toml"),
                    Does.Not.Contain("Legacy"));
            });
        }

        [Test]
        public void Active_Write_Failure_Does_Not_Advance_Provenance()
        {
            var identity = Identity();
            var defaults =
                Document(
                    Entry("Value", Integer(10)));

            var storage = new RecordingStorage();
            var loadResult =
                Load(
                    storage,
                    identity,
                    defaults);

            storage.Operations.Clear();
            storage.ThrowOnWriteFile =
                "settings.toml";

            var writer =
                new ConfigPersistedStateWriter(
                    storage,
                    Clock());

            Assert.Throws<InvalidOperationException>(() =>
                writer.Write(
                    ConfigLocation.World,
                    loadResult,
                    defaults));

            Assert.Multiple(() =>
            {
                Assert.That(
                    storage.Operations,
                    Is.EqualTo(
                        new[]
                        {
                            "WRITE|World|settings.toml"
                        }));

                Assert.That(
                    storage.Get(
                        ConfigLocation.World,
                        "settings.toml.configapi.provenance"),
                    Is.Null);
            });
        }

        [Test]
        public void Provenance_Write_Failure_Happens_Only_After_Active_Commit()
        {
            var identity = Identity();
            var defaults =
                Document(
                    Entry("Value", Integer(10)));

            var storage = new RecordingStorage();
            var loadResult =
                Load(
                    storage,
                    identity,
                    defaults);

            storage.Operations.Clear();
            storage.ThrowOnWriteFile =
                "settings.toml.configapi.provenance";

            var writer =
                new ConfigPersistedStateWriter(
                    storage,
                    Clock());

            Assert.Throws<InvalidOperationException>(() =>
                writer.Write(
                    ConfigLocation.World,
                    loadResult,
                    defaults));

            Assert.Multiple(() =>
            {
                Assert.That(
                    storage.Operations,
                    Is.EqualTo(
                        new[]
                        {
                            "WRITE|World|settings.toml",
                            "WRITE|World|settings.toml.configapi.provenance"
                        }));

                Assert.That(
                    storage.Get(
                        ConfigLocation.World,
                        "settings.toml"),
                    Is.Not.Null);

                Assert.That(
                    storage.Get(
                        ConfigLocation.World,
                        "settings.toml.configapi.provenance"),
                    Is.Null);
            });
        }

        [Test]
        public void Writer_Rejects_Null_Dependencies_And_Arguments()
        {
            var storage = new RecordingStorage();
            var clock = Clock();
            var writer =
                new ConfigPersistedStateWriter(
                    storage,
                    clock);

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new ConfigPersistedStateWriter(
                        null,
                        clock));

                Assert.Throws<ArgumentNullException>(() =>
                    new ConfigPersistedStateWriter(
                        storage,
                        null));

                Assert.Throws<ArgumentNullException>(() =>
                    writer.Write(
                        ConfigLocation.Local,
                        null,
                        Document()));

                var loadResult =
                    Load(
                        storage,
                        Identity(),
                        Document());

                Assert.Throws<ArgumentNullException>(() =>
                    writer.Write(
                        ConfigLocation.Local,
                        loadResult,
                        null));
            });
        }

        private static ConfigPersistedLoadResult Load(
            RecordingStorage storage,
            ConfigIdentity identity,
            ConfigDocument currentDefaults)
        {
            return new ConfigPersistedStateLoader(
                storage)
                .Load(
                    ConfigLocation.World,
                    "settings.toml",
                    identity,
                    currentDefaults);
        }

        private static void SetProvenance(
            RecordingStorage storage,
            ConfigIdentity identity,
            ConfigDocument baseline)
        {
            storage.Set(
                ConfigLocation.World,
                "settings.toml.configapi.provenance",
                ConfigProvenanceCodec.Encode(
                    new ConfigProvenance(
                        identity,
                        baseline)));
        }

        private static FixedClock Clock()
        {
            return new FixedClock(
                new DateTime(
                    2026,
                    9,
                    1,
                    19,
                    0,
                    0,
                    DateTimeKind.Utc));
        }

        private static ConfigIdentity Identity()
        {
            return new ConfigIdentity(
                "12345",
                "Settings");
        }

        private static ConfigDocument Document(
            params ConfigObjectEntry[] entries)
        {
            return new ConfigDocument(
                Object(entries));
        }

        private static ConfigObjectNode Object(
            params ConfigObjectEntry[] entries)
        {
            return new ConfigObjectNode(entries);
        }

        private static ConfigObjectEntry Entry(
            string name,
            ConfigNode value)
        {
            return new ConfigObjectEntry(
                name,
                value);
        }

        private static ConfigScalarNode Integer(
            long value)
        {
            return ConfigScalarNode.Integer(value);
        }

        private sealed class FixedClock : IConfigClock
        {
            public DateTime UtcNow { get; }

            public FixedClock(
                DateTime utcNow)
            {
                UtcNow = utcNow;
            }
        }

        private sealed class RecordingStorage : IConfigTextStorage
        {
            private readonly Dictionary<string, string> _content =
                new Dictionary<string, string>(
                    StringComparer.Ordinal);

            public readonly List<string> Operations =
                new List<string>();

            public string ThrowOnWriteFile;

            public void Set(
                ConfigLocation location,
                string file,
                string content)
            {
                _content[
                    CreateKey(
                        location,
                        file)] = content;
            }

            public string Get(
                ConfigLocation location,
                string file)
            {
                if (file == null)
                    return null;

                string content;

                return _content.TryGetValue(
                    CreateKey(
                        location,
                        file),
                    out content)
                    ? content
                    : null;
            }

            public string Read(
                ConfigLocation location,
                string file)
            {
                Operations.Add(
                    "READ|" +
                    location +
                    "|" +
                    file);

                return Get(
                    location,
                    file);
            }

            public void Write(
                ConfigLocation location,
                string file,
                string content)
            {
                Operations.Add(
                    "WRITE|" +
                    location +
                    "|" +
                    file);

                if (string.Equals(
                    file,
                    ThrowOnWriteFile,
                    StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Simulated storage write failure.");
                }

                Set(
                    location,
                    file,
                    content);
            }

            private static string CreateKey(
                ConfigLocation location,
                string file)
            {
                return location +
                    "|" +
                    file;
            }
        }
    }
}
