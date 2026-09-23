using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Api;
using MarcoZechner.ConfigAPI.Domain;
using MarcoZechner.ConfigAPI.Persistence;
using Mz.Networking;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Api
{
    [TestFixture]
    public sealed class WorldConfigServerNetworkAdapterTests
    {
        [Test]
        public void Open_Uses_Corrected_Transport_Sender_And_Replies_Only_To_Requester()
        {
            TestRig rig = CreateRig();
            var request = new WorldConfigNetworkRequest(1UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL, null, Document(Entry("Value", Integer(10))), null);

            NetworkReceiveContext context = Receive(rig, request, 999UL, 111UL);
            WorldConfigNetworkResponse response = DecodePeerResponse(rig, 0);

            Assert.Multiple(() =>
            {
                Assert.That(context.OriginalSenderWasCorrected, Is.True);
                Assert.That(context.Envelope.OriginalSenderId, Is.EqualTo(111UL));
                Assert.That(rig.Transport.PeerMessages.Count, Is.EqualTo(1));
                Assert.That(rig.Transport.EveryoneMessages.Count, Is.EqualTo(0));
                Assert.That(rig.Transport.PeerMessages[0].PeerId, Is.EqualTo(111UL));
                Assert.That(response.RequestId, Is.EqualTo(1UL));
                Assert.That(response.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Snapshot));
                Assert.That(response.TriggeredBy, Is.EqualTo(111UL));
                Assert.That(response.IsChanged, Is.False);
                Assert.That(response.IsStale, Is.False);
                Assert.That(response.Snapshot.Revision, Is.EqualTo(0UL));
                Assert.That(response.Snapshot.CurrentVariant, Is.EqualTo("default"));
            });
        }

        [Test]
        public void Denied_Apply_Replies_To_Trusted_Requester_Without_Broadcast()
        {
            TestRig rig = CreateRig();
            Open(rig, 111UL);
            rig.Transport.Clear();

            var request = new WorldConfigNetworkRequest(2UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Apply, 0UL, null, null, Document(Entry("Value", Integer(20))));
            Receive(rig, request, 999UL, 111UL);
            WorldConfigNetworkResponse response = DecodePeerResponse(rig, 0);

            Assert.Multiple(() =>
            {
                Assert.That(rig.Authorization.CheckedPlayerIds, Is.EqualTo(new[] { 111UL }));
                Assert.That(rig.Transport.PeerMessages.Count, Is.EqualTo(1));
                Assert.That(rig.Transport.PeerMessages[0].PeerId, Is.EqualTo(111UL));
                Assert.That(rig.Transport.EveryoneMessages.Count, Is.EqualTo(0));
                Assert.That(response.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Error));
                Assert.That(response.TriggeredBy, Is.EqualTo(111UL));
                Assert.That(response.Error, Is.EqualTo(WorldConfigServerRequestHandler.PermissionDeniedError));
            });
        }

        [Test]
        public void Changed_Admin_Apply_Broadcasts_Authoritative_Response()
        {
            TestRig rig = CreateRig(222UL);
            Open(rig, 111UL);
            rig.Transport.Clear();

            var request = new WorldConfigNetworkRequest(3UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Apply, 0UL, null, null, Document(Entry("Value", Integer(20))));
            Receive(rig, request, 999UL, 222UL);

            Assert.Multiple(() =>
            {
                Assert.That(rig.Authorization.CheckedPlayerIds, Is.EqualTo(new[] { 222UL }));
                Assert.That(rig.Transport.PeerMessages.Count, Is.EqualTo(0));
                Assert.That(rig.Transport.EveryoneMessages.Count, Is.EqualTo(1));
                Assert.That(rig.Transport.EveryoneMessages[0].MessageType, Is.EqualTo(WorldConfigServerNetworkAdapter.ResponseMessageType));
                Assert.That(rig.Transport.EveryoneMessages[0].OriginalSenderId, Is.EqualTo(rig.Transport.LocalPeerId));
            });

            WorldConfigNetworkResponse response = WorldConfigNetworkCodec.DecodeResponse(rig.Transport.EveryoneMessages[0].Payload);

            Assert.Multiple(() =>
            {
                Assert.That(response.RequestId, Is.EqualTo(3UL));
                Assert.That(response.TriggeredBy, Is.EqualTo(222UL));
                Assert.That(response.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Snapshot));
                Assert.That(response.IsChanged, Is.True);
                Assert.That(response.IsStale, Is.False);
                Assert.That(response.Snapshot.Revision, Is.EqualTo(1UL));
                AssertDocumentValue(response.Snapshot.Stored, 10);
                AssertDocumentValue(response.Snapshot.Applied, 20);
                Assert.That(response.Snapshot.HasUnsavedChanges, Is.True);
            });
        }

        [Test]
        public void Stale_Admin_Apply_Replies_Only_To_Requester()
        {
            TestRig rig = CreateRig(222UL);
            Open(rig, 111UL);

            Receive(rig, new WorldConfigNetworkRequest(2UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Apply, 0UL, null, null, Document(Entry("Value", Integer(20)))), 222UL, 222UL);
            rig.Transport.Clear();

            Receive(rig, new WorldConfigNetworkRequest(3UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Apply, 0UL, null, null, Document(Entry("Value", Integer(30)))), 999UL, 222UL);
            WorldConfigNetworkResponse response = DecodePeerResponse(rig, 0);

            Assert.Multiple(() =>
            {
                Assert.That(rig.Transport.PeerMessages.Count, Is.EqualTo(1));
                Assert.That(rig.Transport.PeerMessages[0].PeerId, Is.EqualTo(222UL));
                Assert.That(rig.Transport.EveryoneMessages.Count, Is.EqualTo(0));
                Assert.That(response.IsChanged, Is.False);
                Assert.That(response.IsStale, Is.True);
                Assert.That(response.Snapshot.Revision, Is.EqualTo(1UL));
                AssertDocumentValue(response.Snapshot.Applied, 20);
            });
        }

        [Test]
        public void Missing_Variant_Load_Replies_With_Error_Instead_Of_Dropping_Request()
        {
            TestRig rig = CreateRig(222UL);
            Open(rig, 111UL);

            var request = new WorldConfigNetworkRequest(4UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Load, 0UL, "missing", null, null);
            Receive(rig, request, 999UL, 222UL);
            WorldConfigNetworkResponse response = DecodePeerResponse(rig, 0);

            Assert.Multiple(() =>
            {
                Assert.That(rig.Transport.PeerMessages.Count, Is.EqualTo(1));
                Assert.That(rig.Transport.PeerMessages[0].PeerId, Is.EqualTo(222UL));
                Assert.That(rig.Transport.EveryoneMessages.Count, Is.EqualTo(0));
                Assert.That(response.RequestId, Is.EqualTo(4UL));
                Assert.That(response.Operation, Is.EqualTo(WorldConfigNetworkOperation.Load));
                Assert.That(response.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Error));
                Assert.That(response.TriggeredBy, Is.EqualTo(222UL));
                Assert.That(response.IsChanged, Is.False);
                Assert.That(response.IsStale, Is.False);
                Assert.That(response.Snapshot, Is.Null);
                Assert.That(response.Error, Does.Contain("variant does not exist: missing"));
            });
        }
        [Test]
        public void Unchanged_Save_Replies_Only_To_Requester()
        {
            TestRig rig = CreateRig(222UL);
            Open(rig, 111UL);
            rig.Transport.Clear();

            Receive(rig, new WorldConfigNetworkRequest(2UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Save, 0UL, null, null, null), 222UL, 222UL);
            WorldConfigNetworkResponse response = DecodePeerResponse(rig, 0);

            Assert.Multiple(() =>
            {
                Assert.That(rig.Transport.PeerMessages.Count, Is.EqualTo(1));
                Assert.That(rig.Transport.EveryoneMessages.Count, Is.EqualTo(0));
                Assert.That(response.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Snapshot));
                Assert.That(response.IsChanged, Is.False);
                Assert.That(response.IsStale, Is.False);
                Assert.That(response.Snapshot.Revision, Is.EqualTo(0UL));
            });
        }

        [Test]
        public void Dispose_Unregisters_Request_Handler()
        {
            TestRig rig = CreateRig();
            rig.Adapter.Dispose();

            var request = new WorldConfigNetworkRequest(1UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL, null, Document(Entry("Value", Integer(10))), null);
            NetworkReceiveContext context;
            bool dispatched = rig.Endpoint.Receive(new NetworkEnvelope(WorldConfigServerNetworkAdapter.RequestMessageType, 111UL, false, WorldConfigNetworkCodec.EncodeRequest(request)), 111UL, false, out context);

            Assert.Multiple(() =>
            {
                Assert.That(dispatched, Is.False);
                Assert.That(context, Is.Null);
                Assert.That(rig.Transport.PeerMessages.Count, Is.EqualTo(0));
                Assert.That(rig.Transport.EveryoneMessages.Count, Is.EqualTo(0));
            });
        }

        private static TestRig CreateRig(params ulong[] admins)
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            registry.RegisterReadWriteStorage("Example.Mod", Guid.NewGuid(), storage.Read, storage.Write);

            var transport = new RecordingTransport();
            var endpoint = new NetworkEndpoint(transport);
            var authorization = new RecordingAuthorization(admins);
            var service = new WorldConfigServerService(registry, new FixedClock());
            var handler = new WorldConfigServerRequestHandler(service, authorization);
            var adapter = new WorldConfigServerNetworkAdapter(endpoint, transport, handler);

            return new TestRig(endpoint, transport, authorization, adapter);
        }

        private static void Open(TestRig rig, ulong requesterId)
        {
            Receive(rig, new WorldConfigNetworkRequest(1UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL, null, Document(Entry("Value", Integer(10))), null), requesterId, requesterId);
            rig.Transport.Clear();
        }

        private static NetworkReceiveContext Receive(TestRig rig, WorldConfigNetworkRequest request, ulong claimedSenderId, ulong trustedSenderId)
        {
            NetworkReceiveContext context;
            bool dispatched = rig.Endpoint.Receive(new NetworkEnvelope(WorldConfigServerNetworkAdapter.RequestMessageType, claimedSenderId, false, WorldConfigNetworkCodec.EncodeRequest(request)), trustedSenderId, false, out context);
            Assert.That(dispatched, Is.True);
            return context;
        }

        private static WorldConfigNetworkResponse DecodePeerResponse(TestRig rig, int index)
        {
            Assert.That(rig.Transport.PeerMessages[index].Envelope.MessageType, Is.EqualTo(WorldConfigServerNetworkAdapter.ResponseMessageType));
            return WorldConfigNetworkCodec.DecodeResponse(rig.Transport.PeerMessages[index].Envelope.Payload);
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
            public TestRig(NetworkEndpoint endpoint, RecordingTransport transport, RecordingAuthorization authorization, WorldConfigServerNetworkAdapter adapter)
            {
                Endpoint = endpoint;
                Transport = transport;
                Authorization = authorization;
                Adapter = adapter;
            }

            public NetworkEndpoint Endpoint { get; }
            public RecordingTransport Transport { get; }
            public RecordingAuthorization Authorization { get; }
            public WorldConfigServerNetworkAdapter Adapter { get; }
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

        private sealed class RecordingTransport : INetworkTransport
        {
            public bool IsServer => true;
            public ulong LocalPeerId => 777UL;
            public List<PeerMessage> PeerMessages { get; } = new List<PeerMessage>();
            public List<NetworkEnvelope> EveryoneMessages { get; } = new List<NetworkEnvelope>();

            public void SendToServer(NetworkEnvelope envelope)
            {
                throw new InvalidOperationException("Server adapter must not send requests to itself through the transport.");
            }
            public void SendToPeer(NetworkEnvelope envelope, ulong peerId) => PeerMessages.Add(new PeerMessage(envelope, peerId));
            public void SendToOthers(NetworkEnvelope envelope, ulong excludedPeerId)
            {
                throw new InvalidOperationException("World authoritative responses do not use request-envelope relay.");
            }
            public void SendToEveryone(NetworkEnvelope envelope) => EveryoneMessages.Add(envelope);

            public void Clear()
            {
                PeerMessages.Clear();
                EveryoneMessages.Clear();
            }
        }

        private sealed class PeerMessage
        {
            public PeerMessage(NetworkEnvelope envelope, ulong peerId)
            {
                Envelope = envelope;
                PeerId = peerId;
            }

            public NetworkEnvelope Envelope { get; }
            public ulong PeerId { get; }
        }

        private sealed class FixedClock : IConfigClock
        {
            public DateTime UtcNow => new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        }

        private sealed class MemoryStorage
        {
            private readonly Dictionary<string, string> _content = new Dictionary<string, string>(StringComparer.Ordinal);

            public string Read(int location, string file)
            {
                string content;
                return _content.TryGetValue(location + "|" + file, out content) ? content : null;
            }

            public void Write(int location, string file, string content)
            {
                _content[location + "|" + file] = content;
            }
        }
    }
}
