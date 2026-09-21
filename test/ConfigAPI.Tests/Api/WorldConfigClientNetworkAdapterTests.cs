using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Api;
using MarcoZechner.ConfigAPI.V2.Domain;
using Mz.Networking;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Api
{
    [TestFixture]
    public sealed class WorldConfigClientNetworkAdapterTests
    {
        [Test]
        public void Open_Sends_Correlated_Request_And_Bootstraps_Client_State()
        {
            TestRig rig = CreateRig();
            ConfigDocument defaults = Document(Entry("Value", Integer(10)));

            ulong requestId = rig.Adapter.Open("Example.Mod", "Settings", "settings.toml", defaults);

            Assert.That(requestId, Is.EqualTo(1UL));
            Assert.That(rig.Transport.ServerMessages.Count, Is.EqualTo(1));

            WorldConfigNetworkRequest request = WorldConfigNetworkCodec.DecodeRequest(rig.Transport.ServerMessages[0].Payload);

            Assert.Multiple(() =>
            {
                Assert.That(request.RequestId, Is.EqualTo(1UL));
                Assert.That(request.ConsumerId, Is.EqualTo("Example.Mod"));
                Assert.That(request.ConfigKey, Is.EqualTo("Settings"));
                Assert.That(request.Operation, Is.EqualTo(WorldConfigNetworkOperation.Open));
                Assert.That(request.BaseIteration, Is.EqualTo(0UL));
                Assert.That(request.File, Is.EqualTo("settings.toml"));
                Assert.That(request.Defaults, Is.EqualTo(defaults));
                Assert.That(request.Document, Is.Null);
                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(1));
            });

            WorldConfigSnapshot snapshot = Snapshot(10, 0UL);
            ReceiveResponse(rig, new WorldConfigNetworkResponse(1UL, WorldConfigNetworkOperation.Open, WorldConfigNetworkResponseKind.Snapshot, rig.Transport.LocalPeerId, false, false, snapshot, null));

            WorldConfigClientState state;
            Assert.That(rig.Adapter.TryGetState("Example.Mod", "Settings", out state), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(0));
                Assert.That(state.Authoritative.ServerIteration, Is.EqualTo(0UL));
                AssertDocumentValue(state.Authoritative.Document, 10, "Value");
                AssertDocumentValue(state.Draft, 10, "Value");
            });
        }

        [Test]
        public void Bootstrap_Seed_Is_Provisional_And_Correlated_Open_Replaces_It_Even_At_Lower_Iteration()
        {
            var bootstrap = new MemoryBootstrapStore(Snapshot(30, 5UL, "alternate.toml"));
            var transport = new RecordingClientTransport();
            var endpoint = new NetworkEndpoint(transport);
            var adapter = new WorldConfigClientNetworkAdapter(endpoint, transport, bootstrap);
            var rig = new TestRig(endpoint, transport, adapter, 777UL);

            WorldConfigSnapshot bootstrapSnapshot;
            Assert.That(adapter.TrySeedBootstrap("Example.Mod", "Settings", out bootstrapSnapshot), Is.True);

            WorldConfigClientState seeded;
            Assert.That(adapter.TryGetState("Example.Mod", "Settings", out seeded), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(bootstrapSnapshot.ServerIteration, Is.EqualTo(5UL));
                Assert.That(seeded.Authoritative.CurrentFile, Is.EqualTo("alternate.toml"));
                AssertDocumentValue(seeded.Authoritative.Document, 30, "Value");
                AssertDocumentValue(seeded.Draft, 30, "Value");
                Assert.That(transport.ServerMessages.Count, Is.EqualTo(0));
            });

            ulong requestId = adapter.Open("Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));
            ReceiveResponse(rig, new WorldConfigNetworkResponse(requestId, WorldConfigNetworkOperation.Open, WorldConfigNetworkResponseKind.Snapshot, transport.LocalPeerId, false, false, Snapshot(40, 0UL), null));

            WorldConfigClientState reconciled;
            Assert.That(adapter.TryGetState("Example.Mod", "Settings", out reconciled), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(adapter.PendingRequestCount, Is.EqualTo(0));
                Assert.That(reconciled.Authoritative.ServerIteration, Is.EqualTo(0UL));
                Assert.That(reconciled.Authoritative.CurrentFile, Is.EqualTo("settings.toml"));
                AssertDocumentValue(reconciled.Authoritative.Document, 40, "Value");
                AssertDocumentValue(reconciled.Draft, 40, "Value");
            });
        }

        [Test]
        public void Correlated_Open_Preserves_Draft_Edited_After_Bootstrap_Seed()
        {
            var bootstrap = new MemoryBootstrapStore(Snapshot(30, 5UL));
            var transport = new RecordingClientTransport();
            var endpoint = new NetworkEndpoint(transport);
            var adapter = new WorldConfigClientNetworkAdapter(endpoint, transport, bootstrap);
            var rig = new TestRig(endpoint, transport, adapter, 777UL);

            WorldConfigSnapshot bootstrapSnapshot;
            Assert.That(adapter.TrySeedBootstrap("Example.Mod", "Settings", out bootstrapSnapshot), Is.True);
            adapter.SetDraft("Example.Mod", "Settings", Document(Entry("Value", Integer(35))));

            ulong requestId = adapter.Open("Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));
            ReceiveResponse(rig, new WorldConfigNetworkResponse(requestId, WorldConfigNetworkOperation.Open, WorldConfigNetworkResponseKind.Snapshot, transport.LocalPeerId, false, false, Snapshot(40, 0UL), null));

            WorldConfigClientState state;
            Assert.That(adapter.TryGetState("Example.Mod", "Settings", out state), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(state.Authoritative.ServerIteration, Is.EqualTo(0UL));
                AssertDocumentValue(state.Authoritative.Document, 40, "Value");
                AssertDocumentValue(state.Draft, 35, "Value");
            });
        }

        [Test]
        public void Other_Client_Broadcast_Updates_Authority_Preserves_Draft_And_Does_Not_Consume_Local_Request()
        {
            TestRig rig = CreateRig();
            Open(rig, 10, 0UL);

            rig.Adapter.SetDraft("Example.Mod", "Settings", Document(Entry("Value", Integer(15))));
            ulong localSaveRequestId = rig.Adapter.Save("Example.Mod", "Settings");

            Assert.That(localSaveRequestId, Is.EqualTo(2UL));
            Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(1));

            ReceiveResponse(rig, new WorldConfigNetworkResponse(2UL, WorldConfigNetworkOperation.Save, WorldConfigNetworkResponseKind.Snapshot, 222UL, true, false, Snapshot(20, 1UL), null));

            WorldConfigClientState afterOtherClient;
            Assert.That(rig.Adapter.TryGetState("Example.Mod", "Settings", out afterOtherClient), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(1), "Another client's colliding request ID must not consume our pending request.");
                Assert.That(afterOtherClient.Authoritative.ServerIteration, Is.EqualTo(1UL));
                AssertDocumentValue(afterOtherClient.Authoritative.Document, 20, "Value");
                AssertDocumentValue(afterOtherClient.Draft, 15, "Value");
            });

            ReceiveResponse(rig, new WorldConfigNetworkResponse(2UL, WorldConfigNetworkOperation.Save, WorldConfigNetworkResponseKind.Snapshot, rig.Transport.LocalPeerId, false, true, Snapshot(20, 1UL), null));

            WorldConfigClientState afterStaleReply;
            Assert.That(rig.Adapter.TryGetState("Example.Mod", "Settings", out afterStaleReply), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(0));
                Assert.That(afterStaleReply.Authoritative.ServerIteration, Is.EqualTo(1UL));
                AssertDocumentValue(afterStaleReply.Authoritative.Document, 20, "Value");
                AssertDocumentValue(afterStaleReply.Draft, 15, "Value");
            });
        }

        [Test]
        public void Error_Response_Correlates_Without_Mutating_Client_State()
        {
            TestRig rig = CreateRig();
            Open(rig, 10, 0UL);

            rig.Adapter.SetDraft("Example.Mod", "Settings", Document(Entry("Value", Integer(15))));
            ulong requestId = rig.Adapter.Save("Example.Mod", "Settings");

            ReceiveResponse(rig, new WorldConfigNetworkResponse(requestId, WorldConfigNetworkOperation.Save, WorldConfigNetworkResponseKind.Error, rig.Transport.LocalPeerId, false, false, null, WorldConfigServerRequestHandler.PermissionDeniedError));

            WorldConfigClientState state;
            Assert.That(rig.Adapter.TryGetState("Example.Mod", "Settings", out state), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(0));
                Assert.That(state.Authoritative.ServerIteration, Is.EqualTo(0UL));
                AssertDocumentValue(state.Authoritative.Document, 10, "Value");
                AssertDocumentValue(state.Draft, 15, "Value");
            });
        }

        [Test]
        public void Older_Authoritative_Broadcast_Does_Not_Regress_Client_State()
        {
            TestRig rig = CreateRig();
            Open(rig, 30, 3UL);

            ReceiveResponse(rig, new WorldConfigNetworkResponse(91UL, WorldConfigNetworkOperation.Save, WorldConfigNetworkResponseKind.Snapshot, 222UL, true, false, Snapshot(40, 4UL), null));
            ReceiveResponse(rig, new WorldConfigNetworkResponse(92UL, WorldConfigNetworkOperation.Save, WorldConfigNetworkResponseKind.Snapshot, 333UL, true, false, Snapshot(20, 2UL), null));

            WorldConfigClientState state;
            Assert.That(rig.Adapter.TryGetState("Example.Mod", "Settings", out state), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(state.Authoritative.ServerIteration, Is.EqualTo(4UL));
                AssertDocumentValue(state.Authoritative.Document, 40, "Value");
            });
        }

        [Test]
        public void Dispose_Unregisters_Response_Handler_And_Rejects_New_Requests()
        {
            TestRig rig = CreateRig();
            rig.Adapter.Dispose();

            NetworkReceiveContext context;
            bool dispatched = rig.Endpoint.Receive(
                new NetworkEnvelope(
                    WorldConfigServerNetworkAdapter.ResponseMessageType,
                    rig.ServerPeerId,
                    false,
                    WorldConfigNetworkCodec.EncodeResponse(
                        new WorldConfigNetworkResponse(1UL, WorldConfigNetworkOperation.Open, WorldConfigNetworkResponseKind.Snapshot, rig.Transport.LocalPeerId, false, false, Snapshot(10, 0UL), null))),
                rig.ServerPeerId,
                true,
                out context);

            Assert.Multiple(() =>
            {
                Assert.That(dispatched, Is.False);
                Assert.That(context, Is.Null);
                Assert.Throws<InvalidOperationException>(() => rig.Adapter.Open("Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10)))));
            });
        }

        [Test]
        public void Back_To_Back_Saves_Before_Response_Use_The_Same_Base_Iteration()
        {
            TestRig rig = CreateRig();
            Open(rig, 10, 4UL);

            rig.Adapter.SetDraft("Example.Mod", "Settings", Document(Entry("Value", Integer(20))));
            ulong firstRequestId = rig.Adapter.Save("Example.Mod", "Settings");
            rig.Adapter.SetDraft("Example.Mod", "Settings", Document(Entry("Value", Integer(30))));
            ulong secondRequestId = rig.Adapter.Save("Example.Mod", "Settings");

            Assert.That(rig.Transport.ServerMessages.Count, Is.EqualTo(2));
            WorldConfigNetworkRequest first = WorldConfigNetworkCodec.DecodeRequest(rig.Transport.ServerMessages[0].Payload);
            WorldConfigNetworkRequest second = WorldConfigNetworkCodec.DecodeRequest(rig.Transport.ServerMessages[1].Payload);

            Assert.Multiple(() =>
            {
                Assert.That(firstRequestId, Is.EqualTo(2UL));
                Assert.That(secondRequestId, Is.EqualTo(3UL));
                Assert.That(first.BaseIteration, Is.EqualTo(4UL));
                Assert.That(second.BaseIteration, Is.EqualTo(4UL));
                AssertDocumentValue(first.Document, 20, "Value");
                AssertDocumentValue(second.Document, 30, "Value");
                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(2));
            });
        }

        [Test]
        public void File_Operation_Requests_Use_Current_Authority_And_Draft()
        {
            TestRig rig = CreateRig();
            Open(rig, 10, 4UL);
            rig.Adapter.SetDraft("Example.Mod", "Settings", Document(Entry("Value", Integer(25))));

            ulong reloadId = rig.Adapter.Reload("Example.Mod", "Settings");
            ulong loadId = rig.Adapter.LoadAndSwitch("Example.Mod", "Settings", "alternate.toml");
            ulong saveId = rig.Adapter.SaveAndSwitch("Example.Mod", "Settings", "saved.toml");
            ulong exportId = rig.Adapter.Export("Example.Mod", "Settings", "copy.toml", true);

            Assert.That(rig.Transport.ServerMessages.Count, Is.EqualTo(4));

            WorldConfigNetworkRequest reload = WorldConfigNetworkCodec.DecodeRequest(rig.Transport.ServerMessages[0].Payload);
            WorldConfigNetworkRequest load = WorldConfigNetworkCodec.DecodeRequest(rig.Transport.ServerMessages[1].Payload);
            WorldConfigNetworkRequest save = WorldConfigNetworkCodec.DecodeRequest(rig.Transport.ServerMessages[2].Payload);
            WorldConfigNetworkRequest export = WorldConfigNetworkCodec.DecodeRequest(rig.Transport.ServerMessages[3].Payload);

            Assert.Multiple(() =>
            {
                Assert.That(reloadId, Is.EqualTo(2UL));
                Assert.That(reload.Operation, Is.EqualTo(WorldConfigNetworkOperation.Reload));
                Assert.That(reload.BaseIteration, Is.EqualTo(4UL));
                Assert.That(reload.File, Is.Null);
                Assert.That(reload.Document, Is.Null);

                Assert.That(loadId, Is.EqualTo(3UL));
                Assert.That(load.Operation, Is.EqualTo(WorldConfigNetworkOperation.LoadAndSwitch));
                Assert.That(load.BaseIteration, Is.EqualTo(4UL));
                Assert.That(load.File, Is.EqualTo("alternate.toml"));
                Assert.That(load.Document, Is.Null);

                Assert.That(saveId, Is.EqualTo(4UL));
                Assert.That(save.Operation, Is.EqualTo(WorldConfigNetworkOperation.SaveAndSwitch));
                Assert.That(save.BaseIteration, Is.EqualTo(4UL));
                Assert.That(save.File, Is.EqualTo("saved.toml"));
                AssertDocumentValue(save.Document, 25, "Value");

                Assert.That(exportId, Is.EqualTo(5UL));
                Assert.That(export.Operation, Is.EqualTo(WorldConfigNetworkOperation.Export));
                Assert.That(export.BaseIteration, Is.EqualTo(4UL));
                Assert.That(export.File, Is.EqualTo("copy.toml"));
                Assert.That(export.Overwrite, Is.True);
                AssertDocumentValue(export.Document, 25, "Value");

                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(4));
            });
        }

        [Test]
        public void ApplyPreset_Request_Uses_Current_Authority_And_Preset_File()
        {
            TestRig rig = CreateRig();
            Open(rig, 10, 4UL);

            ulong requestId = rig.Adapter.ApplyPreset("Example.Mod", "Settings", "preset.toml");

            Assert.That(rig.Transport.ServerMessages.Count, Is.EqualTo(1));
            WorldConfigNetworkRequest request = WorldConfigNetworkCodec.DecodeRequest(rig.Transport.ServerMessages[0].Payload);

            Assert.Multiple(() =>
            {
                Assert.That(requestId, Is.EqualTo(2UL));
                Assert.That(request.Operation, Is.EqualTo(WorldConfigNetworkOperation.ApplyPreset));
                Assert.That(request.BaseIteration, Is.EqualTo(4UL));
                Assert.That(request.File, Is.EqualTo("preset.toml"));
                Assert.That(request.Overwrite, Is.False);
                Assert.That(request.Defaults, Is.Null);
                Assert.That(request.Document, Is.Null);
                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(1));
            });
        }
        [Test]
        public void Applied_File_Switch_Updates_Authority_And_Preserves_Edited_Draft()
        {
            TestRig rig = CreateRig();
            Open(rig, 10, 4UL);
            rig.Adapter.SetDraft("Example.Mod", "Settings", Document(Entry("Value", Integer(15))));

            ulong requestId = rig.Adapter.LoadAndSwitch("Example.Mod", "Settings", "alternate.toml");
            ReceiveResponse(rig, new WorldConfigNetworkResponse(requestId, WorldConfigNetworkOperation.LoadAndSwitch, WorldConfigNetworkResponseKind.Snapshot, rig.Transport.LocalPeerId, true, false, Snapshot(30, 5UL, "alternate.toml"), null));

            WorldConfigClientState state;
            Assert.That(rig.Adapter.TryGetState("Example.Mod", "Settings", out state), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(0));
                Assert.That(state.Authoritative.ServerIteration, Is.EqualTo(5UL));
                Assert.That(state.Authoritative.CurrentFile, Is.EqualTo("alternate.toml"));
                AssertDocumentValue(state.Authoritative.Document, 30, "Value");
                AssertDocumentValue(state.Draft, 15, "Value");
            });
        }

        [Test]
        public void Exported_Response_Consumes_Request_Without_Changing_Authoritative_State_Or_Draft()
        {
            TestRig rig = CreateRig();
            Open(rig, 10, 4UL);
            rig.Adapter.SetDraft("Example.Mod", "Settings", Document(Entry("Value", Integer(40))));

            ulong requestId = rig.Adapter.Export("Example.Mod", "Settings", "copy.toml", false);
            ReceiveResponse(rig, new WorldConfigNetworkResponse(requestId, WorldConfigNetworkOperation.Export, WorldConfigNetworkResponseKind.Exported, rig.Transport.LocalPeerId, false, false, Snapshot(10, 4UL), null));

            WorldConfigClientState state;
            Assert.That(rig.Adapter.TryGetState("Example.Mod", "Settings", out state), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(0));
                Assert.That(state.Authoritative.ServerIteration, Is.EqualTo(4UL));
                Assert.That(state.Authoritative.CurrentFile, Is.EqualTo("settings.toml"));
                AssertDocumentValue(state.Authoritative.Document, 10, "Value");
                AssertDocumentValue(state.Draft, 40, "Value");
            });
        }

        private static TestRig CreateRig()
        {
            var transport = new RecordingClientTransport();
            var endpoint = new NetworkEndpoint(transport);
            var adapter = new WorldConfigClientNetworkAdapter(endpoint, transport);
            return new TestRig(endpoint, transport, adapter, 777UL);
        }

        private static void Open(TestRig rig, long value, ulong iteration)
        {
            ulong requestId = rig.Adapter.Open("Example.Mod", "Settings", "settings.toml", Document(Entry("Value", Integer(10))));
            ReceiveResponse(rig, new WorldConfigNetworkResponse(requestId, WorldConfigNetworkOperation.Open, WorldConfigNetworkResponseKind.Snapshot, rig.Transport.LocalPeerId, false, false, Snapshot(value, iteration), null));
            rig.Transport.ServerMessages.Clear();
        }

        private static void ReceiveResponse(TestRig rig, WorldConfigNetworkResponse response)
        {
            NetworkReceiveContext context;
            bool dispatched = rig.Endpoint.Receive(
                new NetworkEnvelope(WorldConfigServerNetworkAdapter.ResponseMessageType, rig.ServerPeerId, false, WorldConfigNetworkCodec.EncodeResponse(response)),
                rig.ServerPeerId,
                true,
                out context);

            Assert.That(dispatched, Is.True);
            Assert.That(context.TransportSenderIsServer, Is.True);
        }

        private static WorldConfigSnapshot Snapshot(long value, ulong iteration, string file = "settings.toml")
            => new WorldConfigSnapshot(new ConfigIdentity("Example.Mod", "Settings"), Document(Entry("Value", Integer(value))), iteration, file);

        private static ConfigDocument Document(params ConfigObjectEntry[] entries) => new ConfigDocument(new ConfigObjectNode(entries));
        private static ConfigObjectEntry Entry(string name, ConfigNode value) => new ConfigObjectEntry(name, value);
        private static ConfigScalarNode Integer(long value) => ConfigScalarNode.Integer(value);

        private static void AssertDocumentValue(ConfigDocument document, long expected, params string[] path)
        {
            ConfigNode actual;
            Assert.That(document.TryGet(new ConfigValuePath(path), out actual), Is.True);
            Assert.That(actual.Equals(Integer(expected)), Is.True);
        }

        private sealed class TestRig
        {
            public TestRig(NetworkEndpoint endpoint, RecordingClientTransport transport, WorldConfigClientNetworkAdapter adapter, ulong serverPeerId)
            {
                Endpoint = endpoint;
                Transport = transport;
                Adapter = adapter;
                ServerPeerId = serverPeerId;
            }

            public NetworkEndpoint Endpoint { get; }
            public RecordingClientTransport Transport { get; }
            public WorldConfigClientNetworkAdapter Adapter { get; }
            public ulong ServerPeerId { get; }
        }

        private sealed class MemoryBootstrapStore : IWorldConfigBootstrapStore
        {
            private readonly WorldConfigSnapshot _snapshot;

            public MemoryBootstrapStore(WorldConfigSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public bool TryRead(ConfigIdentity identity, out WorldConfigSnapshot snapshot)
            {
                snapshot = _snapshot != null && _snapshot.Identity.Equals(identity) ? _snapshot : null;
                return snapshot != null;
            }

            public void Write(WorldConfigSnapshot snapshot) { }
        }

        private sealed class RecordingClientTransport : INetworkTransport
        {
            public bool IsServer => false;
            public ulong LocalPeerId => 111UL;
            public List<NetworkEnvelope> ServerMessages { get; } = new List<NetworkEnvelope>();

            public void SendToServer(NetworkEnvelope envelope) => ServerMessages.Add(envelope);
            public void SendToPeer(NetworkEnvelope envelope, ulong peerId) { throw new InvalidOperationException("Client transport cannot send directly to peers."); }
            public void SendToOthers(NetworkEnvelope envelope, ulong excludedPeerId) { throw new InvalidOperationException("Client transport cannot broadcast."); }
            public void SendToEveryone(NetworkEnvelope envelope) { throw new InvalidOperationException("Client transport cannot broadcast."); }
        }
    }
}
