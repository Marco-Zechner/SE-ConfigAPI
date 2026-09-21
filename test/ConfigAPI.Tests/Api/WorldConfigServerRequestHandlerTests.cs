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
        public void Open_Is_Available_To_NonAdmin_And_Uses_Trusted_Requester_As_TriggeredBy()
        {
            TestRig rig = CreateRig();
            var handler = new WorldConfigServerRequestHandler(rig.Service, rig.Authorization);
            var request = new WorldConfigNetworkRequest(
                1UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL,
                "settings.toml", false, Document(Entry("Value", Integer(10))), null);

            WorldConfigNetworkResponse response = handler.Handle(111UL, request);

            Assert.Multiple(() =>
            {
                Assert.That(response.RequestId, Is.EqualTo(1UL));
                Assert.That(response.Operation, Is.EqualTo(WorldConfigNetworkOperation.Open));
                Assert.That(response.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Snapshot));
                Assert.That(response.TriggeredBy, Is.EqualTo(111UL));
                Assert.That(response.IsApplied, Is.False);
                Assert.That(response.IsStale, Is.False);
                Assert.That(response.Error, Is.Null);
                Assert.That(response.Snapshot.ServerIteration, Is.EqualTo(0UL));
                Assert.That(response.Snapshot.CurrentFile, Is.EqualTo("settings.toml"));
                AssertDocumentValue(response.Snapshot.Document, 10, "Value");
                Assert.That(rig.Authorization.CheckedPlayerIds.Count, Is.EqualTo(0));
            });
        }

        [Test]
        public void Save_By_NonAdmin_Is_Denied_Before_Persistence()
        {
            TestRig rig = CreateRig();
            var handler = new WorldConfigServerRequestHandler(rig.Service, rig.Authorization);

            handler.Handle(
                111UL,
                new WorldConfigNetworkRequest(
                    1UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL,
                    "settings.toml", false, Document(Entry("Value", Integer(10))), null));

            rig.Storage.ClearOperations();

            WorldConfigNetworkResponse response = handler.Handle(
                111UL,
                new WorldConfigNetworkRequest(
                    2UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Save, 0UL,
                    null, false, null, Document(Entry("Value", Integer(20)))));

            WorldConfigSnapshot stillCurrent = rig.Service.Open(
                "Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));

            Assert.Multiple(() =>
            {
                Assert.That(response.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Error));
                Assert.That(response.TriggeredBy, Is.EqualTo(111UL));
                Assert.That(response.IsApplied, Is.False);
                Assert.That(response.IsStale, Is.False);
                Assert.That(response.Snapshot, Is.Null);
                Assert.That(response.Error, Is.EqualTo("Permission denied: Only admins can perform this operation."));
                Assert.That(rig.Authorization.CheckedPlayerIds, Is.EqualTo(new[] { 111UL }));
                Assert.That(rig.Storage.TotalWrites, Is.EqualTo(0));
                Assert.That(rig.Storage.Get(2, "settings.toml"), Does.Contain("Value = 10"));
                Assert.That(stillCurrent.ServerIteration, Is.EqualTo(0UL));
                AssertDocumentValue(stillCurrent.Document, 10, "Value");
            });
        }

        [Test]
        public void Save_By_Admin_Persists_And_Returns_Applied_Authoritative_Snapshot()
        {
            TestRig rig = CreateRig(222UL);
            var handler = new WorldConfigServerRequestHandler(rig.Service, rig.Authorization);

            handler.Handle(
                111UL,
                new WorldConfigNetworkRequest(
                    1UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL,
                    "settings.toml", false, Document(Entry("Value", Integer(10))), null));

            rig.Storage.ClearOperations();

            WorldConfigNetworkResponse response = handler.Handle(
                222UL,
                new WorldConfigNetworkRequest(
                    2UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Save, 0UL,
                    null, false, null, Document(Entry("Value", Integer(20)))));

            Assert.Multiple(() =>
            {
                Assert.That(response.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Snapshot));
                Assert.That(response.TriggeredBy, Is.EqualTo(222UL));
                Assert.That(response.IsApplied, Is.True);
                Assert.That(response.IsStale, Is.False);
                Assert.That(response.Error, Is.Null);
                Assert.That(response.Snapshot.ServerIteration, Is.EqualTo(1UL));
                AssertDocumentValue(response.Snapshot.Document, 20, "Value");
                Assert.That(rig.Authorization.CheckedPlayerIds, Is.EqualTo(new[] { 222UL }));
                Assert.That(rig.Storage.TotalWrites, Is.EqualTo(2));
                Assert.That(rig.Storage.Get(2, "settings.toml"), Does.Contain("Value = 20"));
            });
        }

        [Test]
        public void Stale_Admin_Save_Returns_Current_Authority_Without_Writing()
        {
            TestRig rig = CreateRig(222UL);
            var handler = new WorldConfigServerRequestHandler(rig.Service, rig.Authorization);

            handler.Handle(
                111UL,
                new WorldConfigNetworkRequest(
                    1UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL,
                    "settings.toml", false, Document(Entry("Value", Integer(10))), null));

            handler.Handle(
                222UL,
                new WorldConfigNetworkRequest(
                    2UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Save, 0UL,
                    null, false, null, Document(Entry("Value", Integer(20)))));

            rig.Storage.ClearOperations();

            WorldConfigNetworkResponse response = handler.Handle(
                222UL,
                new WorldConfigNetworkRequest(
                    3UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Save, 0UL,
                    null, false, null, Document(Entry("Value", Integer(30)))));

            Assert.Multiple(() =>
            {
                Assert.That(response.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Snapshot));
                Assert.That(response.TriggeredBy, Is.EqualTo(222UL));
                Assert.That(response.IsApplied, Is.False);
                Assert.That(response.IsStale, Is.True);
                Assert.That(response.Snapshot.ServerIteration, Is.EqualTo(1UL));
                AssertDocumentValue(response.Snapshot.Document, 20, "Value");
                Assert.That(rig.Storage.TotalWrites, Is.EqualTo(0));
                Assert.That(rig.Storage.Get(2, "settings.toml"), Does.Contain("Value = 20"));
            });
        }

        [Test]
        public void Reload_Requires_Admin_And_Dispatches_To_Authoritative_Service()
        {
            TestRig rig = CreateRig(222UL);
            var handler = new WorldConfigServerRequestHandler(rig.Service, rig.Authorization);

            handler.Handle(111UL, new WorldConfigNetworkRequest(1UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL, "settings.toml", false, Document(Entry("Value", Integer(10))), null));
            rig.Storage.ClearOperations();

            var request = new WorldConfigNetworkRequest(4UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Reload, 0UL, null, false, null, null);
            WorldConfigNetworkResponse denied = handler.Handle(111UL, request);
            WorldConfigNetworkResponse admin = handler.Handle(222UL, request);

            Assert.Multiple(() =>
            {
                Assert.That(denied.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Error));
                Assert.That(denied.Error, Is.EqualTo("Permission denied: Only admins can perform this operation."));
                Assert.That(admin.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Snapshot));
                Assert.That(admin.TriggeredBy, Is.EqualTo(222UL));
                Assert.That(admin.IsApplied, Is.True);
                Assert.That(admin.IsStale, Is.False);
                Assert.That(admin.Snapshot.ServerIteration, Is.EqualTo(1UL));
                Assert.That(admin.Snapshot.CurrentFile, Is.EqualTo("settings.toml"));
                AssertDocumentValue(admin.Snapshot.Document, 10, "Value");
                Assert.That(rig.Authorization.CheckedPlayerIds, Is.EqualTo(new[] { 111UL, 222UL }));
                Assert.That(rig.Storage.TotalWrites, Is.EqualTo(0));
            });
        }

        [Test]
        public void Admin_File_Operations_Dispatch_With_Correct_Authority_And_Response_Kinds()
        {
            TestRig rig = CreateRig(222UL);
            var handler = new WorldConfigServerRequestHandler(rig.Service, rig.Authorization);

            handler.Handle(111UL, new WorldConfigNetworkRequest(1UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL, "settings.toml", false, Document(Entry("Value", Integer(10))), null));
            rig.Storage.Write(2, "alternate.toml", "Value = 30\n");
            rig.Storage.ClearOperations();

            WorldConfigNetworkResponse loaded = handler.Handle(222UL, new WorldConfigNetworkRequest(2UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.LoadAndSwitch, 0UL, "alternate.toml", false, null, null));
            WorldConfigNetworkResponse saved = handler.Handle(222UL, new WorldConfigNetworkRequest(3UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.SaveAndSwitch, 1UL, "third.toml", false, null, Document(Entry("Value", Integer(40)))));
            WorldConfigNetworkResponse exported = handler.Handle(222UL, new WorldConfigNetworkRequest(4UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Export, 999UL, "copy.toml", false, null, Document(Entry("Value", Integer(50)))));
            WorldConfigSnapshot stillCurrent = rig.Service.Open("Example.Mod", "Settings", "third.toml", Document(Entry("Value", Integer(10))));

            Assert.Multiple(() =>
            {
                Assert.That(loaded.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Snapshot));
                Assert.That(loaded.IsApplied, Is.True);
                Assert.That(loaded.Snapshot.ServerIteration, Is.EqualTo(1UL));
                Assert.That(loaded.Snapshot.CurrentFile, Is.EqualTo("alternate.toml"));
                AssertDocumentValue(loaded.Snapshot.Document, 30, "Value");

                Assert.That(saved.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Snapshot));
                Assert.That(saved.IsApplied, Is.True);
                Assert.That(saved.Snapshot.ServerIteration, Is.EqualTo(2UL));
                Assert.That(saved.Snapshot.CurrentFile, Is.EqualTo("third.toml"));
                AssertDocumentValue(saved.Snapshot.Document, 40, "Value");

                Assert.That(exported.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Exported));
                Assert.That(exported.TriggeredBy, Is.EqualTo(222UL));
                Assert.That(exported.IsApplied, Is.False);
                Assert.That(exported.IsStale, Is.False);
                Assert.That(exported.Snapshot.ServerIteration, Is.EqualTo(2UL));
                Assert.That(exported.Snapshot.CurrentFile, Is.EqualTo("third.toml"));
                AssertDocumentValue(exported.Snapshot.Document, 40, "Value");

                Assert.That(stillCurrent.ServerIteration, Is.EqualTo(2UL));
                Assert.That(stillCurrent.CurrentFile, Is.EqualTo("third.toml"));
                AssertDocumentValue(stillCurrent.Document, 40, "Value");
                Assert.That(rig.Storage.Get(2, "alternate.toml"), Does.Contain("Value = 30"));
                Assert.That(rig.Storage.Get(2, "third.toml"), Does.Contain("Value = 40"));
                Assert.That(rig.Storage.Get(2, "copy.toml"), Does.Contain("Value = 50"));
                Assert.That(rig.Authorization.CheckedPlayerIds, Is.EqualTo(new[] { 222UL, 222UL, 222UL }));
            });
        }

        [Test]
        public void File_Operations_Reject_Missing_Required_Request_Data_Without_Persistence()
        {
            TestRig rig = CreateRig(222UL);
            var handler = new WorldConfigServerRequestHandler(rig.Service, rig.Authorization);

            handler.Handle(111UL, new WorldConfigNetworkRequest(1UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL, "settings.toml", false, Document(Entry("Value", Integer(10))), null));
            rig.Storage.ClearOperations();

            WorldConfigNetworkResponse load = handler.Handle(222UL, new WorldConfigNetworkRequest(2UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.LoadAndSwitch, 0UL, null, false, null, null));
            WorldConfigNetworkResponse save = handler.Handle(222UL, new WorldConfigNetworkRequest(3UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.SaveAndSwitch, 0UL, "alternate.toml", false, null, null));
            WorldConfigNetworkResponse export = handler.Handle(222UL, new WorldConfigNetworkRequest(4UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Export, 0UL, "copy.toml", false, null, null));
            WorldConfigNetworkResponse preset = handler.Handle(222UL, new WorldConfigNetworkRequest(5UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.ApplyPreset, 0UL, null, false, null, null));
            WorldConfigNetworkResponse savePresetFile = handler.Handle(222UL, new WorldConfigNetworkRequest(6UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.SavePreset, 0UL, null, false, null, Document(Entry("Value", Integer(30)))));
            WorldConfigNetworkResponse savePresetDocument = handler.Handle(222UL, new WorldConfigNetworkRequest(7UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.SavePreset, 0UL, "preset.toml", false, null, null));

            Assert.Multiple(() =>
            {
                Assert.That(load.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Error));
                Assert.That(load.Error, Does.Contain("config file"));
                Assert.That(save.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Error));
                Assert.That(save.Error, Does.Contain("config document"));
                Assert.That(export.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Error));
                Assert.That(export.Error, Does.Contain("config document"));
                Assert.That(preset.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Error));
                Assert.That(preset.Error, Does.Contain("preset file"));
                Assert.That(savePresetFile.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Error));
                Assert.That(savePresetFile.Error, Does.Contain("preset file"));
                Assert.That(savePresetDocument.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Error));
                Assert.That(savePresetDocument.Error, Does.Contain("config document"));
                Assert.That(rig.Storage.TotalWrites, Is.EqualTo(0));
            });
        }

        [Test]
        public void SavePreset_By_Admin_Returns_Exported_Without_Mutating_Authority()
        {
            TestRig rig = CreateRig(222UL);
            var handler = new WorldConfigServerRequestHandler(rig.Service, rig.Authorization);

            handler.Handle(111UL, new WorldConfigNetworkRequest(1UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL, "settings.toml", false, Document(Entry("Value", Integer(10))), null));
            rig.Storage.ClearOperations();

            WorldConfigNetworkResponse response = handler.Handle(222UL, new WorldConfigNetworkRequest(2UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.SavePreset, 999UL, "preset.toml", false, null, Document(Entry("Value", Integer(40)))));

            Assert.Multiple(() =>
            {
                Assert.That(response.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Exported));
                Assert.That(response.Operation, Is.EqualTo(WorldConfigNetworkOperation.SavePreset));
                Assert.That(response.TriggeredBy, Is.EqualTo(222UL));
                Assert.That(response.IsApplied, Is.False);
                Assert.That(response.IsStale, Is.False);
                Assert.That(response.Snapshot.ServerIteration, Is.EqualTo(0UL));
                Assert.That(response.Snapshot.CurrentFile, Is.EqualTo("settings.toml"));
                AssertDocumentValue(response.Snapshot.Document, 10, "Value");
                Assert.That(rig.Storage.Get(2, "preset.toml"), Does.Contain("Value = 40"));
                Assert.That(rig.Authorization.CheckedPlayerIds, Is.EqualTo(new[] { 222UL }));
            });
        }
        [Test]
        public void ApplyPreset_By_Admin_Uses_Authoritative_Iteration_And_Preserves_Current_File()
        {
            TestRig rig = CreateRig(222UL);
            var handler = new WorldConfigServerRequestHandler(rig.Service, rig.Authorization);

            handler.Handle(111UL, new WorldConfigNetworkRequest(1UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL, "settings.toml", false, Document(Entry("Value", Integer(10))), null));
            rig.Storage.Write(2, "preset.toml", "Value = 30\n");
            rig.Storage.ClearOperations();

            WorldConfigNetworkResponse response = handler.Handle(222UL, new WorldConfigNetworkRequest(2UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.ApplyPreset, 0UL, "preset.toml", false, null, null));

            Assert.Multiple(() =>
            {
                Assert.That(response.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Snapshot));
                Assert.That(response.TriggeredBy, Is.EqualTo(222UL));
                Assert.That(response.IsApplied, Is.True);
                Assert.That(response.IsStale, Is.False);
                Assert.That(response.Snapshot.ServerIteration, Is.EqualTo(1UL));
                Assert.That(response.Snapshot.CurrentFile, Is.EqualTo("settings.toml"));
                AssertDocumentValue(response.Snapshot.Document, 30, "Value");
                Assert.That(rig.Storage.Get(2, "settings.toml"), Does.Contain("Value = 30"));
                Assert.That(rig.Storage.Get(2, "preset.toml"), Is.EqualTo("Value = 30\n"));
                Assert.That(rig.Storage.TotalWrites, Is.EqualTo(2));
                Assert.That(rig.Authorization.CheckedPlayerIds, Is.EqualTo(new[] { 222UL }));
            });
        }
        [Test]
        public void Handler_Rejects_Null_Request()
        {
            TestRig rig = CreateRig();
            var handler = new WorldConfigServerRequestHandler(rig.Service, rig.Authorization);

            Assert.Throws<ArgumentNullException>(() => handler.Handle(111UL, null));
        }

        private static TestRig CreateRig(params ulong[] admins)
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            registry.Register("Example.Mod", Guid.NewGuid(), storage.Read, storage.Write);

            return new TestRig(
                new WorldConfigServerService(registry, new FixedClock()),
                new RecordingAuthorization(admins),
                storage);
        }

        private static ConfigDocument Document(params ConfigObjectEntry[] entries)
            => new ConfigDocument(new ConfigObjectNode(entries));

        private static ConfigObjectEntry Entry(string name, ConfigNode value)
            => new ConfigObjectEntry(name, value);

        private static ConfigScalarNode Integer(long value) => ConfigScalarNode.Integer(value);

        private static void AssertDocumentValue(ConfigDocument document, long expected, params string[] path)
        {
            ConfigNode actual;
            Assert.That(document.TryGet(new ConfigValuePath(path), out actual), Is.True);
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