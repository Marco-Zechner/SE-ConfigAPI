using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Domain;
using MarcoZechner.ConfigAPI.Persistence;
using MarcoZechner.ConfigAPI.Serialization;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Persistence
{
    [TestFixture]
    public sealed class ConfigPersistedStateWriterTests
    {
        [Test]
        public void Write_Commits_Active_Toml_Before_Shared_Defaults()
        {
            var identity = Identity();
            var defaults = Document(Entry("Value", Integer(10)));
            var storage = new RecordingStorage();
            ConfigPersistedLoadResult loadResult = Load(storage, identity, defaults);
            storage.Operations.Clear();

            ConfigPersistedWriteResult result = new ConfigPersistedStateWriter(storage, Clock()).Write(ConfigLocation.World, loadResult, defaults);
            ConfigDefaultsEntry entry = ReadDefaultsEntry(result.DefaultsSource, "settings.toml");

            Assert.Multiple(() =>
            {
                Assert.That(storage.Operations, Is.EqualTo(new[] { "WRITE|World|settings.toml", "WRITE|World|.defaults" }));
                Assert.That(result.BackupFile, Is.Null);
                Assert.That(result.UsedCanonicalRegeneration, Is.False);
                Assert.That(storage.Get(ConfigLocation.World, "settings.toml"), Is.EqualTo(result.ActiveSource));
                Assert.That(storage.Get(ConfigLocation.World, result.DefaultsFile), Is.EqualTo(result.DefaultsSource));
                Assert.That(entry.Identity.Equals(identity), Is.True);
                Assert.That(entry.BaselineDefaults.Equals(defaults), Is.True);
            });
        }

        [Test]
        public void Lossy_Write_Backs_Up_Exact_Original_Before_Active_And_Shared_Defaults()
        {
            var identity = Identity();
            var baseline = Document(Entry("Legacy", Object(Entry("Value", Integer(9)))));
            var currentDefaults = Document(Entry("Known", Integer(1)));
            const string original = "[Legacy]\nValue = 9\n";
            var storage = new RecordingStorage();
            storage.Set(ConfigLocation.World, "settings.toml", original);
            SetDefaults(storage, identity, baseline);

            ConfigPersistedLoadResult loadResult = Load(storage, identity, currentDefaults);
            storage.Operations.Clear();

            ConfigPersistedWriteResult result = new ConfigPersistedStateWriter(storage, Clock()).Write(ConfigLocation.World, loadResult, currentDefaults);

            Assert.Multiple(() =>
            {
                Assert.That(result.UsedCanonicalRegeneration, Is.True);
                Assert.That(result.BackupFile, Is.EqualTo("settings.toml.20260901T190000.0000000Z.bak"));
                Assert.That(storage.Operations, Is.EqualTo(new[]
                {
                    "READ|World|settings.toml",
                    "READ|World|settings.toml.20260901T190000.0000000Z.bak",
                    "WRITE|World|settings.toml.20260901T190000.0000000Z.bak",
                    "WRITE|World|settings.toml",
                    "WRITE|World|.defaults"
                }));
                Assert.That(storage.Get(ConfigLocation.World, result.BackupFile), Is.EqualTo(original));
                Assert.That(storage.Get(ConfigLocation.World, "settings.toml"), Does.Not.Contain("Legacy"));
            });
        }

        [Test]
        public void Active_Write_Failure_Does_Not_Advance_Shared_Defaults()
        {
            var defaults = Document(Entry("Value", Integer(10)));
            var storage = new RecordingStorage();
            ConfigPersistedLoadResult loadResult = Load(storage, Identity(), defaults);
            storage.Operations.Clear();
            storage.ThrowOnWriteFile = "settings.toml";

            Assert.Throws<InvalidOperationException>(() => new ConfigPersistedStateWriter(storage, Clock()).Write(ConfigLocation.World, loadResult, defaults));

            Assert.Multiple(() =>
            {
                Assert.That(storage.Operations, Is.EqualTo(new[] { "WRITE|World|settings.toml" }));
                Assert.That(storage.Get(ConfigLocation.World, ".defaults"), Is.Null);
            });
        }

        [Test]
        public void Defaults_Write_Failure_Happens_Only_After_Active_Commit()
        {
            var defaults = Document(Entry("Value", Integer(10)));
            var storage = new RecordingStorage();
            ConfigPersistedLoadResult loadResult = Load(storage, Identity(), defaults);
            storage.Operations.Clear();
            storage.ThrowOnWriteFile = ".defaults";

            Assert.Throws<InvalidOperationException>(() => new ConfigPersistedStateWriter(storage, Clock()).Write(ConfigLocation.World, loadResult, defaults));

            Assert.Multiple(() =>
            {
                Assert.That(storage.Operations, Is.EqualTo(new[] { "WRITE|World|settings.toml", "WRITE|World|.defaults" }));
                Assert.That(storage.Get(ConfigLocation.World, "settings.toml"), Is.Not.Null);
                Assert.That(storage.Get(ConfigLocation.World, ".defaults"), Is.Null);
            });
        }

        [Test]
        public void Write_Preserves_Other_Entries_In_Shared_Defaults()
        {
            var storage = new RecordingStorage();
            var identity = Identity();
            var settingsDefaults = Document(Entry("Value", Integer(10)));
            var tuningDefaults = Document(Entry("Value", Integer(20)));

            var store = new ConfigDefaultsStore()
                .With(new ConfigDefaultsEntry("settings.toml", identity, settingsDefaults))
                .With(new ConfigDefaultsEntry("tuning.toml", new ConfigIdentity("12345", "Tuning"), tuningDefaults));

            storage.Set(ConfigLocation.World, "settings.toml", "Value = 10\n");
            storage.Set(ConfigLocation.World, ".defaults", ConfigDefaultsStoreCodec.Encode(store));

            ConfigPersistedLoadResult loadResult = Load(storage, identity, settingsDefaults);
            new ConfigPersistedStateWriter(storage, Clock()).Write(ConfigLocation.World, loadResult, settingsDefaults);

            ConfigDefaultsStore decoded = ConfigDefaultsStoreCodec.Decode(storage.Get(ConfigLocation.World, ".defaults"));
            ConfigDefaultsEntry tuning;
            Assert.Multiple(() =>
            {
                Assert.That(decoded.TryGet("tuning.toml", out tuning), Is.True);
                Assert.That(tuning.Identity.ConfigKey, Is.EqualTo("Tuning"));
                Assert.That(tuning.BaselineDefaults.Equals(tuningDefaults), Is.True);
            });
        }

        [Test]
        public void Writer_Rejects_Null_Dependencies_And_Arguments()
        {
            var storage = new RecordingStorage();
            var clock = Clock();
            var writer = new ConfigPersistedStateWriter(storage, clock);

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(() => new ConfigPersistedStateWriter(null, clock));
                Assert.Throws<ArgumentNullException>(() => new ConfigPersistedStateWriter(storage, null));
                Assert.Throws<ArgumentNullException>(() => writer.Write(ConfigLocation.Local, null, Document()));

                ConfigPersistedLoadResult loadResult = Load(storage, Identity(), Document());
                Assert.Throws<ArgumentNullException>(() => writer.Write(ConfigLocation.Local, loadResult, null));
            });
        }

        private static ConfigPersistedLoadResult Load(RecordingStorage storage, ConfigIdentity identity, ConfigDocument currentDefaults)
        {
            return new ConfigPersistedStateLoader(storage).Load(ConfigLocation.World, "settings.toml", identity, currentDefaults);
        }

        private static ConfigDefaultsEntry ReadDefaultsEntry(string source, string file)
        {
            ConfigDefaultsStore store = ConfigDefaultsStoreCodec.Decode(source);
            ConfigDefaultsEntry entry;
            Assert.That(store.TryGet(file, out entry), Is.True);
            return entry;
        }

        private static void SetDefaults(RecordingStorage storage, ConfigIdentity identity, ConfigDocument baseline)
        {
            var store = new ConfigDefaultsStore().With(new ConfigDefaultsEntry("settings.toml", identity, baseline));
            storage.Set(ConfigLocation.World, ".defaults", ConfigDefaultsStoreCodec.Encode(store));
        }

        private static FixedClock Clock() { return new FixedClock(new DateTime(2026, 9, 1, 19, 0, 0, DateTimeKind.Utc)); }
        private static ConfigIdentity Identity() { return new ConfigIdentity("12345", "Settings"); }
        private static ConfigDocument Document(params ConfigObjectEntry[] entries) { return new ConfigDocument(Object(entries)); }
        private static ConfigObjectNode Object(params ConfigObjectEntry[] entries) { return new ConfigObjectNode(entries); }
        private static ConfigObjectEntry Entry(string name, ConfigNode value) { return new ConfigObjectEntry(name, value); }
        private static ConfigScalarNode Integer(long value) { return ConfigScalarNode.Integer(value); }

        private sealed class FixedClock : IConfigClock
        {
            public DateTime UtcNow { get; private set; }
            public FixedClock(DateTime utcNow) { UtcNow = utcNow; }
        }

        private sealed class RecordingStorage : IConfigTextStorage
        {
            private readonly Dictionary<string, string> _content = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly List<string> Operations = new List<string>();
            public string ThrowOnWriteFile;

            public void Set(ConfigLocation location, string file, string content) { _content[CreateKey(location, file)] = content; }

            public string Get(ConfigLocation location, string file)
            {
                if (file == null) return null;
                string content;
                return _content.TryGetValue(CreateKey(location, file), out content) ? content : null;
            }

            public string Read(ConfigLocation location, string file)
            {
                Operations.Add("READ|" + location + "|" + file);
                return Get(location, file);
            }

            public void Write(ConfigLocation location, string file, string content)
            {
                Operations.Add("WRITE|" + location + "|" + file);
                if (string.Equals(file, ThrowOnWriteFile, StringComparison.Ordinal))
                    throw new InvalidOperationException("Simulated storage write failure.");
                Set(location, file, content);
            }

            private static string CreateKey(ConfigLocation location, string file) { return location + "|" + file; }
        }
    }
}
