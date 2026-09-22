using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Api;
using MarcoZechner.ConfigAPI.Domain;
using MarcoZechner.ConfigAPI.Persistence;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Api
{
    [TestFixture]
    public sealed class WorldConfigServerServiceTests
    {
        [Test]
        public void Open_Uses_Current_Indexed_Server_Registration_And_Default_Variant()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var oldStorage = new MemoryStorage();
            var serverStorage = new MemoryStorage();
            serverStorage.Set(2, "Settings.default.toml", "Value = 41\n");

            Register(registry, oldStorage);
            Register(registry, serverStorage);

            var service = new WorldConfigServerService(registry, new FixedClock());
            WorldConfigSnapshot snapshot = service.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Revision, Is.EqualTo(0UL));
                Assert.That(snapshot.CurrentVariant, Is.EqualTo("default"));
                AssertDocumentValue(snapshot.Stored, 41);
                AssertDocumentValue(snapshot.Applied, 41);
                Assert.That(snapshot.HasUnsavedChanges, Is.False);
                Assert.That(oldStorage.TotalWrites, Is.EqualTo(0));
                Assert.That(serverStorage.Get(2, "Settings.default.toml.configapi.provenance"), Is.Not.Null);
            });
        }

        [Test]
        public void Open_Restores_Bootstrap_Variant_And_Revision_But_Loads_World_Storage()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            storage.Set(2, "Settings.combat.toml", "Value = 55\n");
            Register(registry, storage);

            var identity = new ConfigIdentity("Example.Mod", "Settings");
            var bootstrap = new MemoryBootstrapStore(new WorldConfigSnapshot(identity, Document(Entry("Value", Integer(999))), Document(Entry("Value", Integer(999))), 7UL, "combat"));
            var service = new WorldConfigServerService(registry, new FixedClock(), bootstrap);

            WorldConfigSnapshot opened = service.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));

            Assert.Multiple(() =>
            {
                Assert.That(opened.Revision, Is.EqualTo(7UL));
                Assert.That(opened.CurrentVariant, Is.EqualTo("combat"));
                AssertDocumentValue(opened.Stored, 55);
                AssertDocumentValue(opened.Applied, 55);
                Assert.That(bootstrap.LastWritten, Is.Not.Null);
                Assert.That(bootstrap.LastWritten.Revision, Is.EqualTo(7UL));
                Assert.That(bootstrap.LastWritten.CurrentVariant, Is.EqualTo("combat"));
                AssertDocumentValue(bootstrap.LastWritten.Applied, 55);
            });
        }

        [Test]
        public void Apply_Changes_Applied_Only_Without_Writing()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            Register(registry, storage);

            var service = new WorldConfigServerService(registry, new FixedClock());
            service.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));
            storage.ClearOperations();

            WorldConfigAuthorityResult result = service.Apply("Example.Mod", "Settings", 0UL, Document(Entry("Value", Integer(20))));

            Assert.Multiple(() =>
            {
                Assert.That(result.IsChanged, Is.True);
                Assert.That(result.IsStale, Is.False);
                Assert.That(result.Snapshot.Revision, Is.EqualTo(1UL));
                Assert.That(result.Snapshot.CurrentVariant, Is.EqualTo("default"));
                AssertDocumentValue(result.Snapshot.Stored, 10);
                AssertDocumentValue(result.Snapshot.Applied, 20);
                Assert.That(result.Snapshot.HasUnsavedChanges, Is.True);
                Assert.That(storage.Get(2, "Settings.default.toml"), Does.Contain("Value = 10"));
                Assert.That(storage.TotalWrites, Is.EqualTo(0));
            });
        }

        [Test]
        public void Apply_Invalid_Draft_Does_Not_Change_State_Or_Storage()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            Register(registry, storage);

            var service = new WorldConfigServerService(registry, new FixedClock());
            WorldConfigSnapshot opened = service.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));
            storage.ClearOperations();

            Assert.Throws<ArgumentException>(() => service.Apply("Example.Mod", "Settings", 0UL, Document(Entry("Value", ConfigScalarNode.String("wrong-kind")))));
            WorldConfigSnapshot current = service.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));

            Assert.Multiple(() =>
            {
                Assert.That(current, Is.SameAs(opened));
                Assert.That(current.Revision, Is.EqualTo(0UL));
                AssertDocumentValue(current.Stored, 10);
                AssertDocumentValue(current.Applied, 10);
                Assert.That(storage.TotalWrites, Is.EqualTo(0));
            });
        }

        [Test]
        public void Save_Persists_Applied_And_Stale_Mutations_Do_Not_Write()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            Register(registry, storage);

            var service = new WorldConfigServerService(registry, new FixedClock());
            service.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));
            service.Apply("Example.Mod", "Settings", 0UL, Document(Entry("Value", Integer(20))));
            storage.ClearOperations();

            WorldConfigAuthorityResult saved = service.Save("Example.Mod", "Settings", 1UL);

            Assert.Multiple(() =>
            {
                Assert.That(saved.IsChanged, Is.True);
                Assert.That(saved.IsStale, Is.False);
                Assert.That(saved.Snapshot.Revision, Is.EqualTo(2UL));
                AssertDocumentValue(saved.Snapshot.Stored, 20);
                AssertDocumentValue(saved.Snapshot.Applied, 20);
                Assert.That(saved.Snapshot.HasUnsavedChanges, Is.False);
                Assert.That(storage.Get(2, "Settings.default.toml"), Does.Contain("Value = 20"));
                Assert.That(storage.TotalWrites, Is.EqualTo(2));
            });

            storage.ClearOperations();
            WorldConfigAuthorityResult staleApply = service.Apply("Example.Mod", "Settings", 1UL, Document(Entry("Value", Integer(30))));
            WorldConfigAuthorityResult staleSave = service.Save("Example.Mod", "Settings", 1UL);

            Assert.Multiple(() =>
            {
                Assert.That(staleApply.IsChanged, Is.False);
                Assert.That(staleApply.IsStale, Is.True);
                Assert.That(staleSave.IsChanged, Is.False);
                Assert.That(staleSave.IsStale, Is.True);
                Assert.That(staleSave.Snapshot.Revision, Is.EqualTo(2UL));
                AssertDocumentValue(staleSave.Snapshot.Applied, 20);
                Assert.That(storage.TotalWrites, Is.EqualTo(0));
            });
        }

        [Test]
        public void Save_Without_Unsaved_Changes_Is_Authoritative_NoOp()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            Register(registry, storage);

            var service = new WorldConfigServerService(registry, new FixedClock());
            WorldConfigSnapshot opened = service.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));
            storage.ClearOperations();

            WorldConfigAuthorityResult result = service.Save("Example.Mod", "Settings", 0UL);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsChanged, Is.False);
                Assert.That(result.IsStale, Is.False);
                Assert.That(result.Snapshot, Is.SameAs(opened));
                Assert.That(result.Snapshot.Revision, Is.EqualTo(0UL));
                Assert.That(storage.TotalWrites, Is.EqualTo(0));
            });
        }

        [Test]
        public void Reload_Refreshes_Current_Variant_And_Advances_Revision()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            storage.Set(2, "Settings.default.toml", "Value = 41\n");
            Register(registry, storage);

            var service = new WorldConfigServerService(registry, new FixedClock());
            service.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));
            storage.Set(2, "Settings.default.toml", "Value = 50\n");
            storage.ClearOperations();

            WorldConfigAuthorityResult result = service.Reload("Example.Mod", "Settings", 0UL);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsChanged, Is.True);
                Assert.That(result.IsStale, Is.False);
                Assert.That(result.Snapshot.Revision, Is.EqualTo(1UL));
                Assert.That(result.Snapshot.CurrentVariant, Is.EqualTo("default"));
                AssertDocumentValue(result.Snapshot.Stored, 50);
                AssertDocumentValue(result.Snapshot.Applied, 50);
                Assert.That(result.Snapshot.HasUnsavedChanges, Is.False);
            });
        }

        [Test]
        public void Load_Uses_Derived_Variant_File_And_Is_Failure_Atomic()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            Register(registry, storage);

            var service = new WorldConfigServerService(registry, new FixedClock());
            WorldConfigSnapshot opened = service.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));
            storage.Set(2, "Settings.combat.toml", "Value = 30\n");
            storage.ClearOperations();

            Assert.Throws<InvalidOperationException>(() => service.Load("Example.Mod", "Settings", 0UL, "missing"));
            WorldConfigSnapshot afterMissing = service.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));

            Assert.Multiple(() =>
            {
                Assert.That(afterMissing, Is.SameAs(opened));
                Assert.That(afterMissing.Revision, Is.EqualTo(0UL));
                Assert.That(afterMissing.CurrentVariant, Is.EqualTo("default"));
                Assert.That(storage.TotalWrites, Is.EqualTo(0));
            });

            WorldConfigAuthorityResult loaded = service.Load("Example.Mod", "Settings", 0UL, " combat ");

            Assert.Multiple(() =>
            {
                Assert.That(loaded.IsChanged, Is.True);
                Assert.That(loaded.IsStale, Is.False);
                Assert.That(loaded.Snapshot.Revision, Is.EqualTo(1UL));
                Assert.That(loaded.Snapshot.CurrentVariant, Is.EqualTo("combat"));
                AssertDocumentValue(loaded.Snapshot.Stored, 30);
                AssertDocumentValue(loaded.Snapshot.Applied, 30);
                Assert.That(loaded.Snapshot.HasUnsavedChanges, Is.False);
                Assert.That(storage.Get(2, "Settings.combat.toml.configapi.provenance"), Is.Not.Null);
            });
        }

        [Test]
        public void SaveAs_Persists_Applied_Rejects_Collision_And_Switches_After_Success()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            Register(registry, storage);

            var service = new WorldConfigServerService(registry, new FixedClock());
            service.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));
            service.Apply("Example.Mod", "Settings", 0UL, Document(Entry("Value", Integer(20))));
            storage.Set(2, "Settings.combat.toml", "Value = 30\n");
            storage.ClearOperations();

            Assert.Throws<InvalidOperationException>(() => service.SaveAs("Example.Mod", "Settings", 1UL, "combat"));
            WorldConfigSnapshot afterCollision = service.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));

            Assert.Multiple(() =>
            {
                Assert.That(afterCollision.Revision, Is.EqualTo(1UL));
                Assert.That(afterCollision.CurrentVariant, Is.EqualTo("default"));
                AssertDocumentValue(afterCollision.Stored, 10);
                AssertDocumentValue(afterCollision.Applied, 20);
                Assert.That(afterCollision.HasUnsavedChanges, Is.True);
                Assert.That(storage.TotalWrites, Is.EqualTo(0));
            });

            WorldConfigAuthorityResult saved = service.SaveAs("Example.Mod", "Settings", 1UL, " cargo_2 ");

            Assert.Multiple(() =>
            {
                Assert.That(saved.IsChanged, Is.True);
                Assert.That(saved.IsStale, Is.False);
                Assert.That(saved.Snapshot.Revision, Is.EqualTo(2UL));
                Assert.That(saved.Snapshot.CurrentVariant, Is.EqualTo("cargo_2"));
                AssertDocumentValue(saved.Snapshot.Stored, 20);
                AssertDocumentValue(saved.Snapshot.Applied, 20);
                Assert.That(saved.Snapshot.HasUnsavedChanges, Is.False);
                Assert.That(storage.Get(2, "Settings.default.toml"), Does.Contain("Value = 10"));
                Assert.That(storage.Get(2, "Settings.cargo_2.toml"), Does.Contain("Value = 20"));
                Assert.That(storage.Get(2, "Settings.cargo_2.toml.configapi.provenance"), Is.Not.Null);
            });
        }

        [Test]
        public void ListVariants_Filters_And_Sorts_Exact_Derived_Files()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            Register(registry, storage);

            var service = new WorldConfigServerService(registry, new FixedClock());
            service.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));
            storage.Set(2, "Settings.combat.toml", "Value = 20\n");
            storage.Set(2, "Settings.cargo_2.toml", "Value = 30\n");
            storage.Set(2, "Settings.bad.name.toml", "Value = 40\n");
            storage.Set(2, "Settings..toml", "Value = 50\n");
            storage.Set(2, "Settings. spaced .toml", "Value = 60\n");
            storage.Set(2, "Settings.combat.toml.bak", "Value = 70\n");
            storage.Set(2, "Other.default.toml", "Value = 80\n");

            Assert.Multiple(() =>
            {
                Assert.That(service.ListVariants("Example.Mod", "Settings"), Is.EqualTo(new[] { "cargo_2", "combat", "default" }));
                Assert.Throws<ArgumentException>(() => service.SaveAs("Example.Mod", "Settings", 0UL, "combat.v2"));
            });
        }

        private static void Register(ConfigConsumerRegistrationRegistry registry, MemoryStorage storage)
            => registry.Register("Example.Mod", Guid.NewGuid(), storage.Exists, storage.Read, storage.Write, storage.ListKnown);

        private static ConfigDocument Document(params ConfigObjectEntry[] entries) => new ConfigDocument(new ConfigObjectNode(entries));
        private static ConfigObjectEntry Entry(string name, ConfigNode value) => new ConfigObjectEntry(name, value);
        private static ConfigScalarNode Integer(long value) => ConfigScalarNode.Integer(value);

        private static void AssertDocumentValue(ConfigDocument document, long expected)
        {
            ConfigNode actual;
            Assert.That(document.TryGet(new ConfigValuePath("Value"), out actual), Is.True);
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
            public bool Exists(int location, string file) => _content.ContainsKey(Key(location, file));

            public string[] ListKnown(int location)
            {
                string prefix = location + "|";
                var files = new List<string>();

                foreach (string key in _content.Keys)
                    if (key.StartsWith(prefix, StringComparison.Ordinal))
                        files.Add(key.Substring(prefix.Length));

                files.Sort(StringComparer.Ordinal);
                return files.ToArray();
            }

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
