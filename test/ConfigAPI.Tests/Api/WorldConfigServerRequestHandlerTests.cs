using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Api;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Api
{
    [TestFixture]
    public sealed class WorldConfigServerRequestHandlerTests
    {
        [Test]
        public void Open_Is_Available_To_NonAdmin_And_Uses_Trusted_Requester()
        {
            TestRig rig = CreateRig();
            var handler = new WorldConfigServerRequestHandler(rig.Service, rig.Authorization);
            WorldConfigNetworkResponse response = handler.Handle(111UL, OpenRequest(1UL));

            Assert.Multiple(() =>
            {
                Assert.That(response.RequestId, Is.EqualTo(1UL));
                Assert.That(response.Operation, Is.EqualTo(WorldConfigNetworkOperation.Open));
                Assert.That(response.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Snapshot));
                Assert.That(response.TriggeredBy, Is.EqualTo(111UL));
                Assert.That(response.IsChanged, Is.False);
                Assert.That(response.IsStale, Is.False);
                Assert.That(response.Error, Is.Null);
                Assert.That(response.Snapshot.Revision, Is.EqualTo(0UL));
                Assert.That(response.Snapshot.CurrentVariant, Is.EqualTo("default"));
                AssertDocumentValue(response.Snapshot.Applied, 10);
                Assert.That(rig.Authorization.CheckedPlayerIds.Count, Is.EqualTo(0));
            });
        }

        [Test]
        public void NonAdmin_Canonical_Operation_Is_Denied_Before_Authority_Changes()
        {
            TestRig rig = CreateRig();
            var handler = new WorldConfigServerRequestHandler(rig.Service, rig.Authorization);
            handler.Handle(111UL, OpenRequest(1UL));
            rig.Storage.ClearOperations();

            WorldConfigNetworkResponse response = handler.Handle(111UL, new WorldConfigNetworkRequest(2UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Apply, 0UL, null, null, Document(Entry("Value", Integer(20)))));
            WorldConfigSnapshot current = rig.Service.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));

            Assert.Multiple(() =>
            {
                Assert.That(response.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Error));
                Assert.That(response.TriggeredBy, Is.EqualTo(111UL));
                Assert.That(response.IsChanged, Is.False);
                Assert.That(response.IsStale, Is.False);
                Assert.That(response.Snapshot, Is.Null);
                Assert.That(response.Error, Is.EqualTo(WorldConfigServerRequestHandler.PermissionDeniedError));
                Assert.That(rig.Authorization.CheckedPlayerIds, Is.EqualTo(new[] { 111UL }));
                Assert.That(current.Revision, Is.EqualTo(0UL));
                AssertDocumentValue(current.Applied, 10);
                Assert.That(rig.Storage.TotalWrites, Is.EqualTo(0));
            });
        }

        [Test]
        public void Admin_Apply_Save_Load_SaveAs_Reload_And_ListVariants_Dispatch_Canonically()
        {
            TestRig rig = CreateRig(222UL);
            var handler = new WorldConfigServerRequestHandler(rig.Service, rig.Authorization);
            handler.Handle(111UL, OpenRequest(1UL));
            rig.Storage.Set(2, "Settings.combat.toml", "Value = 30\n");
            rig.Storage.ClearOperations();

            WorldConfigNetworkResponse applied = handler.Handle(222UL, new WorldConfigNetworkRequest(2UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Apply, 0UL, null, null, Document(Entry("Value", Integer(20)))));
            WorldConfigNetworkResponse saved = handler.Handle(222UL, new WorldConfigNetworkRequest(3UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Save, 1UL, null, null, null));
            WorldConfigNetworkResponse loaded = handler.Handle(222UL, new WorldConfigNetworkRequest(4UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Load, 2UL, "combat", null, null));
            WorldConfigNetworkResponse savedAs = handler.Handle(222UL, new WorldConfigNetworkRequest(5UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.SaveAs, 3UL, "cargo", null, null));

            rig.Storage.Set(2, "Settings.cargo.toml", "Value = 45\n");
            WorldConfigNetworkResponse reloaded = handler.Handle(222UL, new WorldConfigNetworkRequest(6UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Reload, 4UL, null, null, null));
            WorldConfigNetworkResponse variants = handler.Handle(222UL, new WorldConfigNetworkRequest(7UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.ListVariants, 0UL, null, null, null));

            Assert.Multiple(() =>
            {
                Assert.That(applied.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Snapshot));
                Assert.That(applied.IsChanged, Is.True);
                Assert.That(applied.Snapshot.Revision, Is.EqualTo(1UL));
                AssertDocumentValue(applied.Snapshot.Stored, 10);
                AssertDocumentValue(applied.Snapshot.Applied, 20);

                Assert.That(saved.IsChanged, Is.True);
                Assert.That(saved.Snapshot.Revision, Is.EqualTo(2UL));
                AssertDocumentValue(saved.Snapshot.Stored, 20);
                Assert.That(saved.Snapshot.HasUnsavedChanges, Is.False);

                Assert.That(loaded.IsChanged, Is.True);
                Assert.That(loaded.Snapshot.Revision, Is.EqualTo(3UL));
                Assert.That(loaded.Snapshot.CurrentVariant, Is.EqualTo("combat"));
                AssertDocumentValue(loaded.Snapshot.Applied, 30);

                Assert.That(savedAs.IsChanged, Is.True);
                Assert.That(savedAs.Snapshot.Revision, Is.EqualTo(4UL));
                Assert.That(savedAs.Snapshot.CurrentVariant, Is.EqualTo("cargo"));
                AssertDocumentValue(savedAs.Snapshot.Stored, 30);

                Assert.That(reloaded.IsChanged, Is.True);
                Assert.That(reloaded.Snapshot.Revision, Is.EqualTo(5UL));
                Assert.That(reloaded.Snapshot.CurrentVariant, Is.EqualTo("cargo"));
                AssertDocumentValue(reloaded.Snapshot.Applied, 45);

                Assert.That(variants.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Variants));
                Assert.That(variants.IsChanged, Is.False);
                Assert.That(variants.IsStale, Is.False);
                Assert.That(variants.Snapshot, Is.Null);
                Assert.That(variants.Variants, Is.EqualTo(new[] { "cargo", "combat", "default" }));
                Assert.That(rig.Authorization.CheckedPlayerIds, Is.EqualTo(new[] { 222UL, 222UL, 222UL, 222UL, 222UL, 222UL }));
            });
        }

        [Test]
        public void Stale_Admin_Apply_Returns_Current_Authority_Without_Writing()
        {
            TestRig rig = CreateRig(222UL);
            var handler = new WorldConfigServerRequestHandler(rig.Service, rig.Authorization);
            handler.Handle(111UL, OpenRequest(1UL));
            handler.Handle(222UL, new WorldConfigNetworkRequest(2UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Apply, 0UL, null, null, Document(Entry("Value", Integer(20)))));
            rig.Storage.ClearOperations();

            WorldConfigNetworkResponse response = handler.Handle(222UL, new WorldConfigNetworkRequest(3UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Apply, 0UL, null, null, Document(Entry("Value", Integer(30)))));

            Assert.Multiple(() =>
            {
                Assert.That(response.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Snapshot));
                Assert.That(response.IsChanged, Is.False);
                Assert.That(response.IsStale, Is.True);
                Assert.That(response.Snapshot.Revision, Is.EqualTo(1UL));
                AssertDocumentValue(response.Snapshot.Applied, 20);
                Assert.That(rig.Storage.TotalWrites, Is.EqualTo(0));
            });
        }

        [Test]
        public void Missing_Apply_And_Variant_Data_Return_Errors_Without_Persistence()
        {
            TestRig rig = CreateRig(222UL);
            var handler = new WorldConfigServerRequestHandler(rig.Service, rig.Authorization);
            handler.Handle(111UL, OpenRequest(1UL));
            rig.Storage.ClearOperations();

            WorldConfigNetworkResponse apply = handler.Handle(222UL, new WorldConfigNetworkRequest(2UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Apply, 0UL, null, null, null));
            WorldConfigNetworkResponse load = handler.Handle(222UL, new WorldConfigNetworkRequest(3UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Load, 0UL, null, null, null));
            WorldConfigNetworkResponse saveAs = handler.Handle(222UL, new WorldConfigNetworkRequest(4UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.SaveAs, 0UL, " ", null, null));

            Assert.Multiple(() =>
            {
                Assert.That(apply.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Error));
                Assert.That(apply.Error, Does.Contain("config document"));
                Assert.That(load.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Error));
                Assert.That(load.Error, Does.Contain("variant"));
                Assert.That(saveAs.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Error));
                Assert.That(saveAs.Error, Does.Contain("variant"));
                Assert.That(rig.Storage.TotalWrites, Is.EqualTo(0));
            });
        }

        [Test]
        public void ListVariants_Is_Admin_Gated_Like_Other_NonOpen_Operations()
        {
            TestRig rig = CreateRig();
            var handler = new WorldConfigServerRequestHandler(rig.Service, rig.Authorization);
            handler.Handle(111UL, OpenRequest(1UL));

            WorldConfigNetworkResponse response = handler.Handle(111UL, new WorldConfigNetworkRequest(2UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.ListVariants, 0UL, null, null, null));

            Assert.Multiple(() =>
            {
                Assert.That(response.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Error));
                Assert.That(response.Error, Is.EqualTo(WorldConfigServerRequestHandler.PermissionDeniedError));
                Assert.That(rig.Authorization.CheckedPlayerIds, Is.EqualTo(new[] { 111UL }));
            });
        }

        [Test]
        public void Handler_Rejects_Null_Request()
        {
            TestRig rig = CreateRig();
            var handler = new WorldConfigServerRequestHandler(rig.Service, rig.Authorization);
            Assert.Throws<ArgumentNullException>(() => handler.Handle(111UL, null));
        }

        private static WorldConfigNetworkRequest OpenRequest(ulong requestId)
            => new WorldConfigNetworkRequest(requestId, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL, null, Document(Entry("Value", Integer(10))), null);

        private static TestRig CreateRig(params ulong[] admins)
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            registry.Register("Example.Mod", Guid.NewGuid(), storage.Exists, storage.Read, storage.Write, storage.ListKnown);
            return new TestRig(new WorldConfigServerService(registry, new FixedClock()), new RecordingAuthorization(admins), storage);
        }

        private static ConfigDocument Document(params ConfigObjectEntry[] entries) => new ConfigDocument(new ConfigObjectNode(entries));
        private static ConfigObjectEntry Entry(string name, ConfigNode value) => new ConfigObjectEntry(name, value);
        private static ConfigScalarNode Integer(long value) => ConfigScalarNode.Integer(value);

        private static void AssertDocumentValue(ConfigDocument document, long expected)
        {
            ConfigNode actual;
            Assert.That(document.TryGet(new ConfigValuePath("Value"), out actual), Is.True);
            Assert.That(actual.Equals(Integer(expected)), Is.True);
        }

        private sealed class TestRig
        {
            public TestRig(WorldConfigServerService service, RecordingAuthorization authorization, MemoryStorage storage)
            {
                Service = service;
                Authorization = authorization;
                Storage = storage;
            }

            public WorldConfigServerService Service { get; }
            public RecordingAuthorization Authorization { get; }
            public MemoryStorage Storage { get; }
        }

        private sealed class RecordingAuthorization : IWorldConfigAuthorization
        {
            private readonly HashSet<ulong> _admins;

            public RecordingAuthorization(IEnumerable<ulong> admins)
            {
                _admins = new HashSet<ulong>(admins);
            }

            public List<ulong> CheckedPlayerIds { get; } = new List<ulong>();

            public bool IsAdmin(ulong playerId)
            {
                CheckedPlayerIds.Add(playerId);
                return _admins.Contains(playerId);
            }
        }

        private sealed class FixedClock : IConfigClock
        {
            public DateTime UtcNow => new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
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
            public void ClearOperations() => TotalWrites = 0;
            private static string Key(int location, string file) => location + "|" + file;
        }
    }
}
