using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Api;
using MarcoZechner.ConfigAPI.Domain;
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

            ulong requestId = rig.Adapter.Open("Example.Mod", "Settings", defaults);
            WorldConfigNetworkRequest request = DecodeRequest(rig, 0);

            Assert.Multiple(() =>
            {
                Assert.That(requestId, Is.EqualTo(1UL));
                Assert.That(request.RequestId, Is.EqualTo(1UL));
                Assert.That(request.ConsumerId, Is.EqualTo("Example.Mod"));
                Assert.That(request.ConfigKey, Is.EqualTo("Settings"));
                Assert.That(request.Operation, Is.EqualTo(WorldConfigNetworkOperation.Open));
                Assert.That(request.ExpectedRevision, Is.EqualTo(0UL));
                Assert.That(request.Variant, Is.Null);
                Assert.That(request.Defaults, Is.EqualTo(defaults));
                Assert.That(request.Document, Is.Null);
                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(1));
            });

            ReceiveResponse(rig, SnapshotResponse(requestId, WorldConfigNetworkOperation.Open, rig.Transport.LocalPeerId, false, false, Snapshot(10, 10, 0UL, "default")));

            WorldConfigClientState state;
            Assert.That(rig.Adapter.TryGetState("Example.Mod", "Settings", out state), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(0));
                Assert.That(state.Authoritative.Revision, Is.EqualTo(0UL));
                Assert.That(state.Authoritative.CurrentVariant, Is.EqualTo("default"));
                AssertDocumentValue(state.Authoritative.Stored, 10);
                AssertDocumentValue(state.Authoritative.Applied, 10);
                AssertDocumentValue(state.Draft, 10);
            });
        }

        [Test]
        public void Bootstrap_Seed_Is_Provisional_And_Correlated_Open_Replaces_Lower_Revision()
        {
            var bootstrap = new MemoryBootstrapStore(Snapshot(30, 30, 5UL, "combat"));
            TestRig rig = CreateRig(bootstrap);

            WorldConfigSnapshot seededSnapshot;
            Assert.That(rig.Adapter.TrySeedBootstrap("Example.Mod", "Settings", out seededSnapshot), Is.True);

            ulong requestId = rig.Adapter.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));
            ReceiveResponse(rig, SnapshotResponse(requestId, WorldConfigNetworkOperation.Open, rig.Transport.LocalPeerId, false, false, Snapshot(40, 40, 0UL, "default")));

            WorldConfigClientState state;
            Assert.That(rig.Adapter.TryGetState("Example.Mod", "Settings", out state), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(seededSnapshot.Revision, Is.EqualTo(5UL));
                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(0));
                Assert.That(state.Authoritative.Revision, Is.EqualTo(0UL));
                Assert.That(state.Authoritative.CurrentVariant, Is.EqualTo("default"));
                AssertDocumentValue(state.Authoritative.Applied, 40);
                AssertDocumentValue(state.Draft, 40);
            });
        }

        [Test]
        public void Correlated_Open_Preserves_Draft_Edited_After_Bootstrap_Seed()
        {
            TestRig rig = CreateRig(new MemoryBootstrapStore(Snapshot(30, 30, 5UL, "combat")));

            WorldConfigSnapshot bootstrap;
            Assert.That(rig.Adapter.TrySeedBootstrap("Example.Mod", "Settings", out bootstrap), Is.True);
            rig.Adapter.SetDraft("Example.Mod", "Settings", Document(Entry("Value", Integer(35))));

            ulong requestId = rig.Adapter.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));
            ReceiveResponse(rig, SnapshotResponse(requestId, WorldConfigNetworkOperation.Open, rig.Transport.LocalPeerId, false, false, Snapshot(40, 40, 0UL, "default")));

            WorldConfigClientState state;
            Assert.That(rig.Adapter.TryGetState("Example.Mod", "Settings", out state), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(state.Authoritative.Revision, Is.EqualTo(0UL));
                AssertDocumentValue(state.Authoritative.Applied, 40);
                AssertDocumentValue(state.Draft, 35);
                Assert.That(state.HasDraftChanges, Is.True);
                Assert.That(state.IsDraftStale, Is.True);
            });
        }

        [Test]
        public void Canonical_Mutation_Requests_Use_Current_Revision_And_Expected_Payloads()
        {
            TestRig rig = CreateRig();
            Open(rig, 10, 4UL);

            rig.Adapter.SetDraft("Example.Mod", "Settings", Document(Entry("Value", Integer(25))));
            ulong applyId = rig.Adapter.Apply("Example.Mod", "Settings");
            ulong saveId = rig.Adapter.Save("Example.Mod", "Settings");
            ulong reloadId = rig.Adapter.Reload("Example.Mod", "Settings");
            ulong loadId = rig.Adapter.Load("Example.Mod", "Settings", "combat");
            ulong saveAsId = rig.Adapter.SaveAs("Example.Mod", "Settings", "cargo_2");
            ulong listId = rig.Adapter.ListVariants("Example.Mod", "Settings");

            Assert.That(rig.Transport.ServerMessages.Count, Is.EqualTo(6));

            WorldConfigNetworkRequest apply = DecodeRequest(rig, 0);
            WorldConfigNetworkRequest save = DecodeRequest(rig, 1);
            WorldConfigNetworkRequest reload = DecodeRequest(rig, 2);
            WorldConfigNetworkRequest load = DecodeRequest(rig, 3);
            WorldConfigNetworkRequest saveAs = DecodeRequest(rig, 4);
            WorldConfigNetworkRequest list = DecodeRequest(rig, 5);

            Assert.Multiple(() =>
            {
                Assert.That(applyId, Is.EqualTo(2UL));
                Assert.That(apply.Operation, Is.EqualTo(WorldConfigNetworkOperation.Apply));
                Assert.That(apply.ExpectedRevision, Is.EqualTo(4UL));
                AssertDocumentValue(apply.Document, 25);

                Assert.That(saveId, Is.EqualTo(3UL));
                Assert.That(save.Operation, Is.EqualTo(WorldConfigNetworkOperation.Save));
                Assert.That(save.ExpectedRevision, Is.EqualTo(4UL));
                Assert.That(save.Document, Is.Null);

                Assert.That(reloadId, Is.EqualTo(4UL));
                Assert.That(reload.Operation, Is.EqualTo(WorldConfigNetworkOperation.Reload));
                Assert.That(reload.ExpectedRevision, Is.EqualTo(4UL));
                Assert.That(reload.Variant, Is.Null);

                Assert.That(loadId, Is.EqualTo(5UL));
                Assert.That(load.Operation, Is.EqualTo(WorldConfigNetworkOperation.Load));
                Assert.That(load.ExpectedRevision, Is.EqualTo(4UL));
                Assert.That(load.Variant, Is.EqualTo("combat"));

                Assert.That(saveAsId, Is.EqualTo(6UL));
                Assert.That(saveAs.Operation, Is.EqualTo(WorldConfigNetworkOperation.SaveAs));
                Assert.That(saveAs.ExpectedRevision, Is.EqualTo(4UL));
                Assert.That(saveAs.Variant, Is.EqualTo("cargo_2"));

                Assert.That(listId, Is.EqualTo(7UL));
                Assert.That(list.Operation, Is.EqualTo(WorldConfigNetworkOperation.ListVariants));
                Assert.That(list.ExpectedRevision, Is.EqualTo(0UL));
                Assert.That(list.Variant, Is.Null);

                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(6));
            });
        }

        [Test]
        public void Other_Client_Broadcast_Updates_Authority_Preserves_Draft_And_Does_Not_Consume_Local_Request()
        {
            TestRig rig = CreateRig();
            Open(rig, 10, 0UL);

            rig.Adapter.SetDraft("Example.Mod", "Settings", Document(Entry("Value", Integer(15))));
            ulong localApplyRequestId = rig.Adapter.Apply("Example.Mod", "Settings");

            ReceiveResponse(rig, SnapshotResponse(localApplyRequestId, WorldConfigNetworkOperation.Apply, 222UL, true, false, Snapshot(10, 20, 1UL, "default")));

            WorldConfigClientState afterBroadcast;
            Assert.That(rig.Adapter.TryGetState("Example.Mod", "Settings", out afterBroadcast), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(1));
                Assert.That(afterBroadcast.Authoritative.Revision, Is.EqualTo(1UL));
                AssertDocumentValue(afterBroadcast.Authoritative.Applied, 20);
                AssertDocumentValue(afterBroadcast.Draft, 15);
            });

            ReceiveResponse(rig, SnapshotResponse(localApplyRequestId, WorldConfigNetworkOperation.Apply, rig.Transport.LocalPeerId, false, true, Snapshot(10, 20, 1UL, "default")));

            WorldConfigClientState afterStaleReply;
            Assert.That(rig.Adapter.TryGetState("Example.Mod", "Settings", out afterStaleReply), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(0));
                Assert.That(afterStaleReply.Authoritative.Revision, Is.EqualTo(1UL));
                AssertDocumentValue(afterStaleReply.Authoritative.Applied, 20);
                AssertDocumentValue(afterStaleReply.Draft, 15);
            });
        }

        [Test]
        public void Error_Response_Correlates_Without_Mutating_Client_State()
        {
            TestRig rig = CreateRig();
            Open(rig, 10, 0UL);
            rig.Adapter.SetDraft("Example.Mod", "Settings", Document(Entry("Value", Integer(15))));

            ulong requestId = rig.Adapter.Apply("Example.Mod", "Settings");
            ReceiveResponse(rig, new WorldConfigNetworkResponse(requestId, WorldConfigNetworkOperation.Apply, WorldConfigNetworkResponseKind.Error, rig.Transport.LocalPeerId, false, false, null, null, WorldConfigServerRequestHandler.PermissionDeniedError));

            WorldConfigClientState state;
            Assert.That(rig.Adapter.TryGetState("Example.Mod", "Settings", out state), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(0));
                Assert.That(state.Authoritative.Revision, Is.EqualTo(0UL));
                AssertDocumentValue(state.Authoritative.Applied, 10);
                AssertDocumentValue(state.Draft, 15);
            });
        }

        [Test]
        public void Older_Authoritative_Broadcast_Does_Not_Regress_Client_State()
        {
            TestRig rig = CreateRig();
            Open(rig, 30, 3UL);

            ReceiveResponse(rig, SnapshotResponse(91UL, WorldConfigNetworkOperation.Apply, 222UL, true, false, Snapshot(30, 40, 4UL, "default")));
            ReceiveResponse(rig, SnapshotResponse(92UL, WorldConfigNetworkOperation.Apply, 333UL, true, false, Snapshot(20, 20, 2UL, "default")));

            WorldConfigClientState state;
            Assert.That(rig.Adapter.TryGetState("Example.Mod", "Settings", out state), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(state.Authoritative.Revision, Is.EqualTo(4UL));
                AssertDocumentValue(state.Authoritative.Applied, 40);
            });
        }

        [Test]
        public void Load_Response_Updates_Authority_And_Preserves_Edited_Draft()
        {
            TestRig rig = CreateRig();
            Open(rig, 10, 4UL);
            rig.Adapter.SetDraft("Example.Mod", "Settings", Document(Entry("Value", Integer(15))));

            ulong requestId = rig.Adapter.Load("Example.Mod", "Settings", "combat");
            ReceiveResponse(rig, SnapshotResponse(requestId, WorldConfigNetworkOperation.Load, rig.Transport.LocalPeerId, true, false, Snapshot(30, 30, 5UL, "combat")));

            WorldConfigClientState state;
            Assert.That(rig.Adapter.TryGetState("Example.Mod", "Settings", out state), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(0));
                Assert.That(state.Authoritative.Revision, Is.EqualTo(5UL));
                Assert.That(state.Authoritative.CurrentVariant, Is.EqualTo("combat"));
                AssertDocumentValue(state.Authoritative.Applied, 30);
                AssertDocumentValue(state.Draft, 15);
                Assert.That(state.IsDraftStale, Is.True);
            });
        }

        [Test]
        public void Variant_List_Response_Consumes_Correlated_Request_Without_Mutating_State()
        {
            TestRig rig = CreateRig();
            Open(rig, 10, 4UL);

            ulong requestId = rig.Adapter.ListVariants("Example.Mod", "Settings");
            ReceiveResponse(rig, new WorldConfigNetworkResponse(requestId, WorldConfigNetworkOperation.ListVariants, WorldConfigNetworkResponseKind.Variants, rig.Transport.LocalPeerId, false, false, null, new[] { "combat", "default" }, null));

            WorldConfigClientState state;
            Assert.That(rig.Adapter.TryGetState("Example.Mod", "Settings", out state), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(rig.Adapter.PendingRequestCount, Is.EqualTo(0));
                Assert.That(state.Authoritative.Revision, Is.EqualTo(4UL));
                Assert.That(state.Authoritative.CurrentVariant, Is.EqualTo("default"));
                AssertDocumentValue(state.Authoritative.Applied, 10);
            });
        }

        [Test]
        public void Dispose_Unregisters_Response_Handler_And_Rejects_New_Requests()
        {
            TestRig rig = CreateRig();
            rig.Adapter.Dispose();

            NetworkReceiveContext context;
            bool dispatched = rig.Endpoint.Receive(new NetworkEnvelope(WorldConfigServerNetworkAdapter.ResponseMessageType, rig.ServerPeerId, false, WorldConfigNetworkCodec.EncodeResponse(SnapshotResponse(1UL, WorldConfigNetworkOperation.Open, rig.Transport.LocalPeerId, false, false, Snapshot(10, 10, 0UL, "default")))), rig.ServerPeerId, true, out context);

            Assert.Multiple(() =>
            {
                Assert.That(dispatched, Is.False);
                Assert.That(context, Is.Null);
                Assert.Throws<InvalidOperationException>(() => rig.Adapter.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10)))));
            });
        }

        private static TestRig CreateRig(IWorldConfigBootstrapStore bootstrapStore = null)
        {
            var transport = new RecordingClientTransport();
            var endpoint = new NetworkEndpoint(transport);
            var adapter = bootstrapStore == null ? new WorldConfigClientNetworkAdapter(endpoint, transport) : new WorldConfigClientNetworkAdapter(endpoint, transport, bootstrapStore);
            return new TestRig(endpoint, transport, adapter, 777UL);
        }

        private static void Open(TestRig rig, long value, ulong revision)
        {
            ulong requestId = rig.Adapter.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));
            ReceiveResponse(rig, SnapshotResponse(requestId, WorldConfigNetworkOperation.Open, rig.Transport.LocalPeerId, false, false, Snapshot(value, value, revision, "default")));
            rig.Transport.ServerMessages.Clear();
        }

        private static WorldConfigNetworkRequest DecodeRequest(TestRig rig, int index) => WorldConfigNetworkCodec.DecodeRequest(rig.Transport.ServerMessages[index].Payload);

        private static WorldConfigNetworkResponse SnapshotResponse(ulong requestId, WorldConfigNetworkOperation operation, ulong triggeredBy, bool changed, bool stale, WorldConfigSnapshot snapshot)
            => new WorldConfigNetworkResponse(requestId, operation, WorldConfigNetworkResponseKind.Snapshot, triggeredBy, changed, stale, snapshot, null, null);

        private static void ReceiveResponse(TestRig rig, WorldConfigNetworkResponse response)
        {
            NetworkReceiveContext context;
            bool dispatched = rig.Endpoint.Receive(new NetworkEnvelope(WorldConfigServerNetworkAdapter.ResponseMessageType, rig.ServerPeerId, false, WorldConfigNetworkCodec.EncodeResponse(response)), rig.ServerPeerId, true, out context);
            Assert.That(dispatched, Is.True);
            Assert.That(context.TransportSenderIsServer, Is.True);
        }

        private static WorldConfigSnapshot Snapshot(long stored, long applied, ulong revision, string variant)
            => new WorldConfigSnapshot(new ConfigIdentity("Example.Mod", "Settings"), Document(Entry("Value", Integer(stored))), Document(Entry("Value", Integer(applied))), revision, variant);

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
                snapshot = identity.Equals(_snapshot.Identity) ? _snapshot : null;
                return snapshot != null;
            }

            public void Write(WorldConfigSnapshot snapshot)
            {
            }
        }

        private sealed class RecordingClientTransport : INetworkTransport
        {
            public bool IsServer => false;
            public ulong LocalPeerId => 111UL;
            public List<NetworkEnvelope> ServerMessages { get; } = new List<NetworkEnvelope>();

            public void SendToServer(NetworkEnvelope envelope) => ServerMessages.Add(envelope);
            public void SendToPeer(NetworkEnvelope envelope, ulong peerId)
            {
                throw new InvalidOperationException("Client transport must not send directly to peers.");
            }
            public void SendToOthers(NetworkEnvelope envelope, ulong excludedPeerId)
            {
                throw new InvalidOperationException("Client transport must not broadcast.");
            }
            public void SendToEveryone(NetworkEnvelope envelope)
            {
                throw new InvalidOperationException("Client transport must not broadcast.");
            }
        }
    }
}
