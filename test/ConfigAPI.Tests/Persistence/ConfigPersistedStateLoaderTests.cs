using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;
using MarcoZechner.ConfigAPI.V2.Serialization;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Persistence
{
    [TestFixture]
    public sealed class ConfigPersistedStateLoaderTests
    {
        [Test]
        public void Load_With_Provenance_Reconciles_Historical_Defaults()
        {
            var identity =
                new ConfigIdentity(
                    "12345",
                    "Settings");

            var currentDefaults =
                Document(
                    Entry("Untouched", Integer(20)),
                    Entry("Override", Integer(20)));

            var provenance =
                new ConfigProvenance(
                    identity,
                    Document(
                        Entry("Untouched", Integer(10)),
                        Entry("Override", Integer(10))));

            var storage =
                new MemoryStorage();

            storage.Set(
                ConfigLocation.World,
                "settings.toml",
                "Untouched = 10\n" +
                "Override = 15\n");

            storage.Set(
                ConfigLocation.World,
                "settings.toml.configapi.provenance",
                ConfigProvenanceCodec.Encode(
                    provenance));

            var result =
                new ConfigPersistedStateLoader(
                    storage)
                .Load(
                    ConfigLocation.World,
                    "settings.toml",
                    identity,
                    currentDefaults);

            Assert.Multiple(() =>
            {
                Assert.That(
                    result.WasActiveFileMissing,
                    Is.False);

                Assert.That(
                    result.WasProvenanceMissing,
                    Is.False);

                Assert.That(
                    result.RequiresBackup,
                    Is.False);

                Assert.That(
                    result.Changes.Count,
                    Is.EqualTo(2));

                Assert.That(
                    HasChange(
                        result,
                        ConfigDefaultChangeKind.AppliedChangedDefault,
                        "Untouched"),
                    Is.True);

                Assert.That(
                    HasChange(
                        result,
                        ConfigDefaultChangeKind.PendingChangedDefault,
                        "Override"),
                    Is.True);

                AssertValue(
                    result.State.PlayerValues,
                    Integer(20),
                    "Untouched");

                AssertValue(
                    result.State.BaselineDefaults,
                    Integer(20),
                    "Untouched");

                AssertValue(
                    result.State.PlayerValues,
                    Integer(15),
                    "Override");

                AssertValue(
                    result.State.BaselineDefaults,
                    Integer(10),
                    "Override");
            });
        }

        [Test]
        public void Missing_Provenance_Preserves_Existing_Known_Value_And_Fills_Missing_Default()
        {
            var identity =
                new ConfigIdentity(
                    "12345",
                    "Settings");

            var currentDefaults =
                Document(
                    Entry("Existing", Integer(20)),
                    Entry("Added", Integer(30)));

            var storage =
                new MemoryStorage();

            storage.Set(
                ConfigLocation.Local,
                "settings.toml",
                "Existing = 15\n");

            var result =
                new ConfigPersistedStateLoader(
                    storage)
                .Load(
                    ConfigLocation.Local,
                    "settings.toml",
                    identity,
                    currentDefaults);

            Assert.Multiple(() =>
            {
                Assert.That(
                    result.WasProvenanceMissing,
                    Is.True);

                Assert.That(
                    result.RequiresBackup,
                    Is.False);

                Assert.That(
                    result.Changes.Count,
                    Is.EqualTo(0));

                AssertValue(
                    result.State.PlayerValues,
                    Integer(15),
                    "Existing");

                AssertValue(
                    result.State.PlayerValues,
                    Integer(30),
                    "Added");

                AssertValue(
                    result.State.BaselineDefaults,
                    Integer(20),
                    "Existing");

                AssertValue(
                    result.State.BaselineDefaults,
                    Integer(30),
                    "Added");
            });
        }

        [Test]
        public void Missing_Provenance_Fills_Missing_Known_Nested_Field_Without_Masking_Existing_Object()
        {
            var identity =
                new ConfigIdentity(
                    "12345",
                    "Settings");

            var currentDefaults =
                Document(
                    Entry(
                        "Section",
                        new ConfigObjectNode(
                            Entry("Existing", Integer(20)),
                            Entry("Added", Integer(30)))));

            var storage =
                new MemoryStorage();

            storage.Set(
                ConfigLocation.Global,
                "settings.toml",
                "[Section]\n" +
                "Existing = 15\n");

            var result =
                new ConfigPersistedStateLoader(
                    storage)
                .Load(
                    ConfigLocation.Global,
                    "settings.toml",
                    identity,
                    currentDefaults);

            Assert.Multiple(() =>
            {
                Assert.That(
                    result.RequiresBackup,
                    Is.False);

                AssertValue(
                    result.State.PlayerValues,
                    Integer(15),
                    "Section",
                    "Existing");

                AssertValue(
                    result.State.PlayerValues,
                    Integer(30),
                    "Section",
                    "Added");
            });
        }

        [Test]
        public void Missing_Provenance_Does_Not_Hide_Unknown_Player_Data()
        {
            var identity =
                new ConfigIdentity(
                    "12345",
                    "Settings");

            var storage =
                new MemoryStorage();

            storage.Set(
                ConfigLocation.World,
                "settings.toml",
                "Known = 5\n" +
                "Legacy = 99\n");

            var result =
                new ConfigPersistedStateLoader(
                    storage)
                .Load(
                    ConfigLocation.World,
                    "settings.toml",
                    identity,
                    Document(
                        Entry("Known", Integer(10))));

            ConfigNode ignored;

            Assert.Multiple(() =>
            {
                Assert.That(
                    result.RequiresBackup,
                    Is.True);

                Assert.That(
                    HasChange(
                        result,
                        ConfigDefaultChangeKind.RemovedValue,
                        "Legacy"),
                    Is.True);

                Assert.That(
                    result.State.PlayerValues.TryGet(
                        new ConfigValuePath("Legacy"),
                        out ignored),
                    Is.False);

                AssertValue(
                    result.State.PlayerValues,
                    Integer(5),
                    "Known");
            });
        }

        [Test]
        public void Missing_Active_And_Provenance_Bootstraps_Current_Defaults()
        {
            var identity =
                new ConfigIdentity(
                    "12345",
                    "Settings");

            var defaults =
                Document(
                    Entry("Value", Integer(10)));

            var result =
                new ConfigPersistedStateLoader(
                    new MemoryStorage())
                .Load(
                    ConfigLocation.World,
                    "settings.toml",
                    identity,
                    defaults);

            Assert.Multiple(() =>
            {
                Assert.That(
                    result.WasActiveFileMissing,
                    Is.True);

                Assert.That(
                    result.WasProvenanceMissing,
                    Is.True);

                Assert.That(
                    result.ActiveSource,
                    Is.Null);

                Assert.That(
                    result.RequiresBackup,
                    Is.False);

                Assert.That(
                    result.Changes.Count,
                    Is.EqualTo(0));

                Assert.That(
                    result.State.PlayerValues.Equals(
                        defaults),
                    Is.True);

                Assert.That(
                    result.State.BaselineDefaults.Equals(
                        defaults),
                    Is.True);
            });
        }

        [Test]
        public void Load_Rejects_Orphaned_Malformed_Or_Identity_Mismatched_Provenance()
        {
            var identity =
                new ConfigIdentity(
                    "12345",
                    "Settings");

            var defaults =
                Document(
                    Entry("Value", Integer(10)));

            var orphaned =
                new MemoryStorage();

            orphaned.Set(
                ConfigLocation.World,
                "settings.toml.configapi.provenance",
                ConfigProvenanceCodec.Encode(
                    new ConfigProvenance(
                        identity,
                        defaults)));

            var malformed =
                new MemoryStorage();

            malformed.Set(
                ConfigLocation.World,
                "settings.toml",
                "Value = 10\n");

            malformed.Set(
                ConfigLocation.World,
                "settings.toml.configapi.provenance",
                "not-provenance");

            var mismatched =
                new MemoryStorage();

            mismatched.Set(
                ConfigLocation.World,
                "settings.toml",
                "Value = 10\n");

            mismatched.Set(
                ConfigLocation.World,
                "settings.toml.configapi.provenance",
                ConfigProvenanceCodec.Encode(
                    new ConfigProvenance(
                        new ConfigIdentity(
                            "other",
                            "Settings"),
                        defaults)));

            Assert.Multiple(() =>
            {
                Assert.Throws<InvalidOperationException>(() =>
                    new ConfigPersistedStateLoader(
                        orphaned)
                    .Load(
                        ConfigLocation.World,
                        "settings.toml",
                        identity,
                        defaults));

                Assert.Throws<FormatException>(() =>
                    new ConfigPersistedStateLoader(
                        malformed)
                    .Load(
                        ConfigLocation.World,
                        "settings.toml",
                        identity,
                        defaults));

                Assert.Throws<InvalidOperationException>(() =>
                    new ConfigPersistedStateLoader(
                        mismatched)
                    .Load(
                        ConfigLocation.World,
                        "settings.toml",
                        identity,
                        defaults));
            });
        }

        [Test]
        public void Load_Reads_Active_And_Provenance_From_Same_Location()
        {
            var identity =
                new ConfigIdentity(
                    "owner",
                    "config");

            var storage =
                new MemoryStorage();

            storage.Set(
                ConfigLocation.Global,
                "custom.toml",
                "Value = 3\n");

            var result =
                new ConfigPersistedStateLoader(
                    storage)
                .Load(
                    ConfigLocation.Global,
                    "custom.toml",
                    identity,
                    Document(
                        Entry("Value", Integer(1))));

            Assert.Multiple(() =>
            {
                Assert.That(
                    result.ProvenanceFile,
                    Is.EqualTo(
                        "custom.toml.configapi.provenance"));

                Assert.That(
                    storage.Operations.Count,
                    Is.EqualTo(2));

                Assert.That(
                    storage.Operations[0],
                    Is.EqualTo(
                        "READ|Global|custom.toml"));

                Assert.That(
                    storage.Operations[1],
                    Is.EqualTo(
                        "READ|Global|custom.toml.configapi.provenance"));
            });
        }

        [Test]
        public void Loader_Rejects_Invalid_Dependencies_And_Arguments()
        {
            var identity =
                new ConfigIdentity(
                    "owner",
                    "config");

            var defaults =
                Document();

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new ConfigPersistedStateLoader(
                        null));

                Assert.Throws<ArgumentException>(() =>
                    ConfigPersistedStateLoader.GetProvenanceFile(
                        "   "));

                var loader =
                    new ConfigPersistedStateLoader(
                        new MemoryStorage());

                Assert.Throws<ArgumentException>(() =>
                    loader.Load(
                        ConfigLocation.Local,
                        null,
                        identity,
                        defaults));

                Assert.Throws<ArgumentNullException>(() =>
                    loader.Load(
                        ConfigLocation.Local,
                        "settings.toml",
                        null,
                        defaults));

                Assert.Throws<ArgumentNullException>(() =>
                    loader.Load(
                        ConfigLocation.Local,
                        "settings.toml",
                        identity,
                        null));
            });
        }

        private static bool HasChange(
            ConfigPersistedLoadResult result,
            ConfigDefaultChangeKind kind,
            params string[] path)
        {
            var expected =
                new ConfigValuePath(path);

            for (var i = 0;
                i < result.Changes.Count;
                i++)
            {
                var change =
                    result.Changes[i];

                if (change.Kind == kind &&
                    change.Path.Equals(expected))
                {
                    return true;
                }
            }

            return false;
        }

        private static ConfigDocument Document(
            params ConfigObjectEntry[] entries)
        {
            return new ConfigDocument(
                new ConfigObjectNode(entries));
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
            return ConfigScalarNode.Integer(
                value);
        }

        private static void AssertValue(
            ConfigDocument document,
            ConfigNode expected,
            params string[] path)
        {
            ConfigNode actual;

            Assert.That(
                document.TryGet(
                    new ConfigValuePath(path),
                    out actual),
                Is.True);

            Assert.That(
                actual.Equals(expected),
                Is.True);
        }

        private sealed class MemoryStorage :
            IConfigTextStorage
        {
            private readonly Dictionary<string, string>
                _content =
                    new Dictionary<string, string>(
                        StringComparer.Ordinal);

            public readonly List<string> Operations =
                new List<string>();

            public void Set(
                ConfigLocation location,
                string file,
                string content)
            {
                _content[
                    Key(location, file)] =
                    content;
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

                string content;

                return _content.TryGetValue(
                    Key(location, file),
                    out content)
                    ? content
                    : null;
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

                _content[
                    Key(location, file)] =
                    content;
            }

            private static string Key(
                ConfigLocation location,
                string file)
            {
                return
                    location +
                    "|" +
                    file;
            }
        }
    }
}
