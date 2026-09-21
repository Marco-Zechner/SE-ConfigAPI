using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Api;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Api
{
    [TestFixture]
    public sealed class WorldConfigServerServiceTests
    {
        [Test]
        public void Open_Uses_Current_Server_Registration_And_World_Storage()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var oldStorage = new MemoryStorage();
            var serverStorage = new MemoryStorage();
            serverStorage.Set(2, "settings.toml", "Value = 41\n");

            registry.Register("Example.Mod", Guid.NewGuid(), oldStorage.Read, oldStorage.Write);
            registry.Register("Example.Mod", Guid.NewGuid(), serverStorage.Read, serverStorage.Write);

            var service = new WorldConfigServerService(registry, new FixedClock());
            WorldConfigSnapshot snapshot = service.Open("Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.ServerIteration, Is.EqualTo(0UL));
                Assert.That(snapshot.CurrentFile, Is.EqualTo("settings.toml"));
                AssertDocumentValue(snapshot.Document, 41, "Value");
                Assert.That(oldStorage.TotalWrites, Is.EqualTo(0));
                Assert.That(serverStorage.Get(2, "settings.toml.configapi.provenance"), Is.Not.Null);
            });
        }

        [Test]
        public void Open_Restores_Bootstrap_File_And_Iteration_While_World_Storage_Remains_Authoritative()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            storage.Set(2, "settings.toml", "Value = 41\n");
            storage.Set(2, "alternate.toml", "Value = 55\n");
            registry.Register("Example.Mod", Guid.NewGuid(), storage.Read, storage.Write);

            var identity = new ConfigIdentity("Example.Mod", "Settings");
            var bootstrap = new MemoryBootstrapStore(new WorldConfigSnapshot(identity, Document(Entry("Value", Integer(999))), 7UL, "alternate.toml"));
            var service = new WorldConfigServerService(registry, new FixedClock(), bootstrap);

            WorldConfigSnapshot opened = service.Open("Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));

            Assert.Multiple(() =>
            {
                Assert.That(opened.ServerIteration, Is.EqualTo(7UL));
                Assert.That(opened.CurrentFile, Is.EqualTo("alternate.toml"));
                AssertDocumentValue(opened.Document, 55, "Value");
                Assert.That(bootstrap.LastWritten, Is.Not.Null);
                Assert.That(bootstrap.LastWritten.ServerIteration, Is.EqualTo(7UL));
                Assert.That(bootstrap.LastWritten.CurrentFile, Is.EqualTo("alternate.toml"));
                AssertDocumentValue(bootstrap.LastWritten.Document, 55, "Value");
            });
        }

        [Test]
        public void Save_Matching_Iteration_Persists_World_And_Increments_Authority()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            registry.Register("Example.Mod", Guid.NewGuid(), storage.Read, storage.Write);

            var service = new WorldConfigServerService(registry, new FixedClock());
            service.Open("Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));
            storage.ClearOperations();

            WorldConfigAuthorityResult result = service.Save(
                "Example.Mod", "Settings", 0UL, Document(Entry("Value", Integer(20))));

            Assert.Multiple(() =>
            {
                Assert.That(result.IsApplied, Is.True);
                Assert.That(result.IsStale, Is.False);
                Assert.That(result.Snapshot.ServerIteration, Is.EqualTo(1UL));
                Assert.That(result.Snapshot.CurrentFile, Is.EqualTo("settings.toml"));
                AssertDocumentValue(result.Snapshot.Document, 20, "Value");
                Assert.That(storage.Get(2, "settings.toml"), Does.Contain("Value = 20"));
                Assert.That(storage.Get(2, "settings.toml.configapi.provenance"), Is.Not.Null);
                Assert.That(storage.TotalWrites, Is.EqualTo(2));
            });
        }

        [Test]
        public void Save_Stale_Iteration_Returns_Current_Authority_Without_Writing()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            registry.Register("Example.Mod", Guid.NewGuid(), storage.Read, storage.Write);

            var service = new WorldConfigServerService(registry, new FixedClock());
            service.Open("Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));
            service.Save("Example.Mod", "Settings", 0UL, Document(Entry("Value", Integer(20))));
            storage.ClearOperations();

            WorldConfigAuthorityResult result = service.Save(
                "Example.Mod", "Settings", 0UL, Document(Entry("Value", Integer(30))));

            Assert.Multiple(() =>
            {
                Assert.That(result.IsApplied, Is.False);
                Assert.That(result.IsStale, Is.True);
                Assert.That(result.Snapshot.ServerIteration, Is.EqualTo(1UL));
                AssertDocumentValue(result.Snapshot.Document, 20, "Value");
                Assert.That(storage.Get(2, "settings.toml"), Does.Contain("Value = 20"));
                Assert.That(storage.TotalWrites, Is.EqualTo(0));
            });
        }

        [Test]
        public void Save_Invalid_Draft_Fails_Without_Changing_Authority_Or_Storage()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            registry.Register("Example.Mod", Guid.NewGuid(), storage.Read, storage.Write);

            var service = new WorldConfigServerService(registry, new FixedClock());
            WorldConfigSnapshot opened = service.Open(
                "Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));

            string sourceBefore = storage.Get(2, "settings.toml");
            string provenanceBefore = storage.Get(2, "settings.toml.configapi.provenance");
            storage.ClearOperations();

            Assert.Throws<ArgumentException>(() =>
                service.Save("Example.Mod", "Settings", 0UL, Document(Entry("Value", String("wrong-kind")))));

            WorldConfigSnapshot stillCurrent = service.Open(
                "Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));

            Assert.Multiple(() =>
            {
                Assert.That(ReferenceEquals(stillCurrent, opened), Is.True);
                Assert.That(stillCurrent.ServerIteration, Is.EqualTo(0UL));
                AssertDocumentValue(stillCurrent.Document, 10, "Value");
                Assert.That(storage.Get(2, "settings.toml"), Is.EqualTo(sourceBefore));
                Assert.That(storage.Get(2, "settings.toml.configapi.provenance"), Is.EqualTo(provenanceBefore));
                Assert.That(storage.TotalWrites, Is.EqualTo(0));
            });
        }

        [Test]
        public void Reload_Loads_Current_File_And_Advances_Authority()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            registry.Register("Example.Mod", Guid.NewGuid(), storage.Read, storage.Write);
            storage.Set(2, "settings.toml", "Value = 41\n");

            var service = new WorldConfigServerService(registry, new FixedClock());
            service.Open("Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));
            storage.Set(2, "settings.toml", "Value = 50\n");
            storage.ClearOperations();

            WorldConfigAuthorityResult result = service.Reload("Example.Mod", "Settings", 0UL);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsApplied, Is.True);
                Assert.That(result.IsStale, Is.False);
                Assert.That(result.Snapshot.ServerIteration, Is.EqualTo(1UL));
                Assert.That(result.Snapshot.CurrentFile, Is.EqualTo("settings.toml"));
                AssertDocumentValue(result.Snapshot.Document, 50, "Value");
                Assert.That(storage.TotalWrites, Is.EqualTo(0));
            });
        }

        [Test]
        public void LoadAndSwitch_Loads_Target_Persists_Provenance_And_Switches()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            registry.Register("Example.Mod", Guid.NewGuid(), storage.Read, storage.Write);

            var service = new WorldConfigServerService(registry, new FixedClock());
            service.Open("Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));
            storage.Set(2, "alternate.toml", "Value = 30\n");
            storage.ClearOperations();

            WorldConfigAuthorityResult result = service.LoadAndSwitch("Example.Mod", "Settings", 0UL, "alternate.toml");

            Assert.Multiple(() =>
            {
                Assert.That(result.IsApplied, Is.True);
                Assert.That(result.IsStale, Is.False);
                Assert.That(result.Snapshot.ServerIteration, Is.EqualTo(1UL));
                Assert.That(result.Snapshot.CurrentFile, Is.EqualTo("alternate.toml"));
                AssertDocumentValue(result.Snapshot.Document, 30, "Value");
                Assert.That(storage.Get(2, "alternate.toml"), Does.Contain("Value = 30"));
                Assert.That(storage.Get(2, "alternate.toml.configapi.provenance"), Is.Not.Null);
                Assert.That(storage.TotalWrites, Is.EqualTo(2));
            });
        }

        [Test]
        public void LoadAndSwitch_Invalid_Target_Fails_Without_Changing_Authority_Or_Storage()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            registry.Register("Example.Mod", Guid.NewGuid(), storage.Read, storage.Write);

            var service = new WorldConfigServerService(registry, new FixedClock());
            WorldConfigSnapshot opened = service.Open("Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));
            storage.Set(2, "broken.toml", "Value = [\n");
            storage.ClearOperations();

            Assert.Throws<ArgumentException>(() => service.LoadAndSwitch("Example.Mod", "Settings", 0UL, "broken.toml"));

            WorldConfigSnapshot stillCurrent = service.Open("Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));

            Assert.Multiple(() =>
            {
                Assert.That(ReferenceEquals(stillCurrent, opened), Is.True);
                Assert.That(stillCurrent.ServerIteration, Is.EqualTo(0UL));
                Assert.That(stillCurrent.CurrentFile, Is.EqualTo("settings.toml"));
                AssertDocumentValue(stillCurrent.Document, 10, "Value");
                Assert.That(storage.TotalWrites, Is.EqualTo(0));
            });
        }

        [Test]
        public void SaveAndSwitch_Persists_Target_And_Switches_Authority()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            registry.Register("Example.Mod", Guid.NewGuid(), storage.Read, storage.Write);

            var service = new WorldConfigServerService(registry, new FixedClock());
            service.Open("Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));
            storage.ClearOperations();

            WorldConfigAuthorityResult result = service.SaveAndSwitch(
                "Example.Mod", "Settings", 0UL, Document(Entry("Value", Integer(25))), "alternate.toml");

            Assert.Multiple(() =>
            {
                Assert.That(result.IsApplied, Is.True);
                Assert.That(result.IsStale, Is.False);
                Assert.That(result.Snapshot.ServerIteration, Is.EqualTo(1UL));
                Assert.That(result.Snapshot.CurrentFile, Is.EqualTo("alternate.toml"));
                AssertDocumentValue(result.Snapshot.Document, 25, "Value");
                Assert.That(storage.Get(2, "settings.toml"), Does.Contain("Value = 10"));
                Assert.That(storage.Get(2, "alternate.toml"), Does.Contain("Value = 25"));
                Assert.That(storage.Get(2, "alternate.toml.configapi.provenance"), Is.Not.Null);
                Assert.That(storage.TotalWrites, Is.EqualTo(2));
            });
        }

        [Test]
        public void ApplyPreset_Copies_Preset_Into_Current_File_Without_Switching()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            registry.Register("Example.Mod", Guid.NewGuid(), storage.Read, storage.Write);

            var service = new WorldConfigServerService(registry, new FixedClock());
            service.Open("Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));
            storage.Set(2, "preset.toml", "Value = 30\n");
            storage.ClearOperations();

            WorldConfigAuthorityResult result = service.ApplyPreset("Example.Mod", "Settings", 0UL, "preset.toml");

            Assert.Multiple(() =>
            {
                Assert.That(result.IsApplied, Is.True);
                Assert.That(result.IsStale, Is.False);
                Assert.That(result.Snapshot.ServerIteration, Is.EqualTo(1UL));
                Assert.That(result.Snapshot.CurrentFile, Is.EqualTo("settings.toml"));
                AssertDocumentValue(result.Snapshot.Document, 30, "Value");
                Assert.That(storage.Get(2, "settings.toml"), Does.Contain("Value = 30"));
                Assert.That(storage.Get(2, "settings.toml.configapi.provenance"), Is.Not.Null);
                Assert.That(storage.Get(2, "preset.toml"), Is.EqualTo("Value = 30\n"));
                Assert.That(storage.Get(2, "preset.toml.configapi.provenance"), Is.Null);
                Assert.That(storage.TotalWrites, Is.EqualTo(2));
            });
        }
        [Test]
        public void Export_Writes_Target_Without_Mutating_Authority_And_Respects_Overwrite()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            registry.Register("Example.Mod", Guid.NewGuid(), storage.Read, storage.Write);

            var service = new WorldConfigServerService(registry, new FixedClock());
            WorldConfigSnapshot opened = service.Open("Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));
            storage.ClearOperations();

            WorldConfigExport export = service.Export(
                "Example.Mod", "Settings", Document(Entry("Value", Integer(40))), "copy.toml", false);

            WorldConfigSnapshot stillCurrent = service.Open(
                "Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));

            Assert.Multiple(() =>
            {
                Assert.That(ReferenceEquals(export.Authoritative, opened), Is.True);
                Assert.That(ReferenceEquals(stillCurrent, opened), Is.True);
                Assert.That(stillCurrent.ServerIteration, Is.EqualTo(0UL));
                Assert.That(stillCurrent.CurrentFile, Is.EqualTo("settings.toml"));
                AssertDocumentValue(stillCurrent.Document, 10, "Value");
                Assert.That(storage.Get(2, "copy.toml"), Does.Contain("Value = 40"));
                Assert.That(storage.Get(2, "copy.toml.configapi.provenance"), Is.Not.Null);
                Assert.That(storage.TotalWrites, Is.EqualTo(2));
            });

            storage.ClearOperations();

            Assert.Throws<InvalidOperationException>(() =>
                service.Export("Example.Mod", "Settings", Document(Entry("Value", Integer(50))), "copy.toml", false));

            Assert.Multiple(() =>
            {
                Assert.That(storage.Get(2, "copy.toml"), Does.Contain("Value = 40"));
                Assert.That(storage.TotalWrites, Is.EqualTo(0));

                WorldConfigSnapshot afterRejectedExport = service.Open(
                    "Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));

                Assert.That(ReferenceEquals(afterRejectedExport, opened), Is.True);
                Assert.That(afterRejectedExport.ServerIteration, Is.EqualTo(0UL));
                AssertDocumentValue(afterRejectedExport.Document, 10, "Value");
            });
        }

        private static ConfigDocument Document(params ConfigObjectEntry[] entries)
            => new ConfigDocument(new ConfigObjectNode(entries));

        private static ConfigObjectEntry Entry(string name, ConfigNode value)
            => new ConfigObjectEntry(name, value);

        private static ConfigScalarNode Integer(long value) => ConfigScalarNode.Integer(value);

        private static ConfigScalarNode String(string value) => ConfigScalarNode.String(value);

        private static void AssertDocumentValue(ConfigDocument document, long expected, params string[] path)
        {
            ConfigNode actual;
            Assert.That(document.TryGet(new ConfigValuePath(path), out actual), Is.True);
            Assert.That(actual.Equals(Integer(expected)), Is.True);
        }

        private sealed class FixedClock : IConfigClock
        {
            public DateTime UtcNow => new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        }

        private sealed class MemoryBootstrapStore : IWorldConfigBootstrapStore
        {
            private readonly WorldConfigSnapshot _snapshot;

            public MemoryBootstrapStore(WorldConfigSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public WorldConfigSnapshot LastWritten { get; private set; }

            public bool TryRead(ConfigIdentity identity, out WorldConfigSnapshot snapshot)
            {
                snapshot = _snapshot != null && _snapshot.Identity.Equals(identity) ? _snapshot : null;
                return snapshot != null;
            }

            public void Write(WorldConfigSnapshot snapshot) => LastWritten = snapshot;
        }

        private sealed class MemoryStorage
        {
            private readonly Dictionary<string, string> _content = new Dictionary<string, string>(StringComparer.Ordinal);

            public int TotalWrites { get; private set; }

            public string Read(int location, string file)
            {
                string content;
                return _content.TryGetValue(Key(location, file), out content) ? content : null;
            }

            public void Write(int location, string file, string content)
            {
                _content[Key(location, file)] = content;
                TotalWrites++;
            }

            public void Set(int location, string file, string content) => _content[Key(location, file)] = content;

            public string Get(int location, string file)
            {
                string content;
                return _content.TryGetValue(Key(location, file), out content) ? content : null;
            }

            public void ClearOperations() => TotalWrites = 0;

            private static string Key(int location, string file) => location + "|" + file;
        }
    }
}
