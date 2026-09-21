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

        private static WorldConfigSnapshot Snapshot(long value, ulong iteration)
            => new WorldConfigSnapshot(new ConfigIdentity("Example.Mod", "Settings"), Document(Entry("Value", Integer(value))), iteration, "settings.toml");

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
