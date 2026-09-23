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
    public sealed class WorldConfigNetworkRuntimeTests
    {
        [Test]
        public void Server_Transport_Creates_Authoritative_Runtime_And_Handles_Open()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            registry.Register("Example.Mod", Guid.NewGuid(), storage.Exists, storage.Read, storage.Write, storage.ListKnown);

            var transport = new RecordingServerTransport();
            var endpoint = new NetworkEndpoint(transport);
            var runtime = new WorldConfigNetworkRuntime(endpoint, transport, registry, new FixedClock(), new AllowAllAuthorization());

            Assert.Multiple(() =>
            {
                Assert.That(runtime.IsServer, Is.True);
                Assert.That(runtime.ServerService, Is.Not.Null);
                Assert.That(runtime.ServerAdapter, Is.Not.Null);
                Assert.That(runtime.ClientAdapter, Is.Null);
            });

            var request = new WorldConfigNetworkRequest(1UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL, null, Document(Entry("Value", Integer(10))), null);
            NetworkReceiveContext context;
            bool dispatched = endpoint.Receive(new NetworkEnvelope(WorldConfigServerNetworkAdapter.RequestMessageType, 999UL, false, WorldConfigNetworkCodec.EncodeRequest(request)), 111UL, false, out context);

            Assert.Multiple(() =>
            {
                Assert.That(dispatched, Is.True);
                Assert.That(context.Envelope.OriginalSenderId, Is.EqualTo(111UL));
                Assert.That(transport.PeerMessages.Count, Is.EqualTo(1));
                Assert.That(transport.PeerMessages[0].PeerId, Is.EqualTo(111UL));
            });

            WorldConfigNetworkResponse response = WorldConfigNetworkCodec.DecodeResponse(transport.PeerMessages[0].Envelope.Payload);

            Assert.Multiple(() =>
            {
                Assert.That(response.RequestId, Is.EqualTo(1UL));
                Assert.That(response.Kind, Is.EqualTo(WorldConfigNetworkResponseKind.Snapshot));
                Assert.That(response.TriggeredBy, Is.EqualTo(111UL));
                Assert.That(response.Snapshot.Identity.OwnerId, Is.EqualTo("Example.Mod"));
                Assert.That(response.Snapshot.Identity.ConfigKey, Is.EqualTo("Settings"));
                Assert.That(response.Snapshot.CurrentVariant, Is.EqualTo("default"));
            });

            runtime.Dispose();

            bool dispatchedAfterDispose = endpoint.Receive(new NetworkEnvelope(WorldConfigServerNetworkAdapter.RequestMessageType, 111UL, false, WorldConfigNetworkCodec.EncodeRequest(request)), 111UL, false, out context);
            Assert.That(dispatchedAfterDispose, Is.False);
        }

        [Test]
        public void Client_Transport_Creates_Client_Runtime_And_Sends_Canonical_Open()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var transport = new RecordingClientTransport();
            var endpoint = new NetworkEndpoint(transport);
            var runtime = new WorldConfigNetworkRuntime(endpoint, transport, registry, new FixedClock(), new AllowAllAuthorization());

            Assert.Multiple(() =>
            {
                Assert.That(runtime.IsServer, Is.False);
                Assert.That(runtime.ServerService, Is.Null);
                Assert.That(runtime.ServerAdapter, Is.Null);
                Assert.That(runtime.ClientAdapter, Is.Not.Null);
            });

            ulong requestId = runtime.ClientAdapter.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));

            Assert.That(requestId, Is.EqualTo(1UL));
            Assert.That(transport.ServerMessages.Count, Is.EqualTo(1));

            WorldConfigNetworkRequest request = WorldConfigNetworkCodec.DecodeRequest(transport.ServerMessages[0].Payload);

            Assert.Multiple(() =>
            {
                Assert.That(request.ConsumerId, Is.EqualTo("Example.Mod"));
                Assert.That(request.ConfigKey, Is.EqualTo("Settings"));
                Assert.That(request.Operation, Is.EqualTo(WorldConfigNetworkOperation.Open));
                Assert.That(request.ExpectedRevision, Is.EqualTo(0UL));
                Assert.That(request.Variant, Is.Null);
                Assert.That(request.Defaults, Is.Not.Null);
            });

            runtime.Dispose();
            Assert.Throws<InvalidOperationException>(() => runtime.ClientAdapter.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10)))));
        }

        [Test]
        public void Runtime_Forwards_Bootstrap_Store_To_Server_And_Client()
        {
            var identity = new ConfigIdentity("Example.Mod", "Settings");
            var bootstrap = new MemoryBootstrapStore(new WorldConfigSnapshot(identity, Document(Entry("Value", Integer(30))), Document(Entry("Value", Integer(30))), 5UL, "default"));

            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            storage.Set(2, "Settings.default.toml", "Value = 40\n");
            registry.Register("Example.Mod", Guid.NewGuid(), storage.Exists, storage.Read, storage.Write, storage.ListKnown);

            var serverTransport = new RecordingServerTransport();
            var serverEndpoint = new NetworkEndpoint(serverTransport);
            var serverRuntime = new WorldConfigNetworkRuntime(serverEndpoint, serverTransport, registry, new FixedClock(), new AllowAllAuthorization(), bootstrap);

            WorldConfigSnapshot serverSnapshot = serverRuntime.ServerService.Open("Example.Mod", "Settings", Document(Entry("Value", Integer(10))));

            Assert.Multiple(() =>
            {
                Assert.That(serverSnapshot.Revision, Is.EqualTo(5UL));
                Assert.That(serverSnapshot.CurrentVariant, Is.EqualTo("default"));
                AssertDocumentValue(serverSnapshot.Applied, 40);
                Assert.That(bootstrap.Current.Revision, Is.EqualTo(5UL));
                AssertDocumentValue(bootstrap.Current.Applied, 40);
            });

            var clientTransport = new RecordingClientTransport();
            var clientEndpoint = new NetworkEndpoint(clientTransport);
            var clientRuntime = new WorldConfigNetworkRuntime(clientEndpoint, clientTransport, registry, new FixedClock(), new AllowAllAuthorization(), bootstrap);

            WorldConfigSnapshot clientBootstrap;
            Assert.That(clientRuntime.ClientAdapter.TrySeedBootstrap("Example.Mod", "Settings", out clientBootstrap), Is.True);

            WorldConfigClientState clientState;
            Assert.That(clientRuntime.ClientAdapter.TryGetState("Example.Mod", "Settings", out clientState), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(clientBootstrap.Revision, Is.EqualTo(5UL));
                Assert.That(clientState.Authoritative.CurrentVariant, Is.EqualTo("default"));
                AssertDocumentValue(clientState.Authoritative.Applied, 40);
            });

            clientRuntime.Dispose();
            serverRuntime.Dispose();
        }

        private static void AssertDocumentValue(ConfigDocument document, long expected)
        {
            ConfigNode value;
            Assert.That(document.TryGet(new ConfigValuePath("Value"), out value), Is.True);
            Assert.That(value, Is.EqualTo(Integer(expected)));
        }

        private sealed class MemoryBootstrapStore : IWorldConfigBootstrapStore
        {
            public MemoryBootstrapStore(WorldConfigSnapshot snapshot)
            {
                Current = snapshot;
            }

            public WorldConfigSnapshot Current { get; private set; }

            public bool TryRead(ConfigIdentity identity, out WorldConfigSnapshot snapshot)
            {
                snapshot = Current != null && Current.Identity.Equals(identity) ? Current : null;
                return snapshot != null;
            }

            public void Write(WorldConfigSnapshot snapshot)
            {
                Current = snapshot;
            }
        }

        private static ConfigDocument Document(params ConfigObjectEntry[] entries) => new ConfigDocument(new ConfigObjectNode(entries));
        private static ConfigObjectEntry Entry(string name, ConfigNode value) => new ConfigObjectEntry(name, value);
        private static ConfigScalarNode Integer(long value) => ConfigScalarNode.Integer(value);

        private sealed class AllowAllAuthorization : IWorldConfigAuthorization
        {
            public bool IsAdmin(ulong playerId) => true;
        }

        private sealed class FixedClock : IConfigClock
        {
            public DateTime UtcNow => new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        }

        private sealed class MemoryStorage
        {
            private readonly Dictionary<string, string> _content = new Dictionary<string, string>(StringComparer.Ordinal);

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

            public void Write(int location, string file, string content) => _content[Key(location, file)] = content;
            public void Set(int location, string file, string content) => _content[Key(location, file)] = content;
            private static string Key(int location, string file) => location + "|" + file;
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

        private sealed class RecordingServerTransport : INetworkTransport
        {
            public bool IsServer => true;
            public ulong LocalPeerId => 777UL;
            public List<PeerMessage> PeerMessages { get; } = new List<PeerMessage>();

            public void SendToServer(NetworkEnvelope envelope)
            {
                throw new InvalidOperationException("Server runtime must not send requests to itself through the transport.");
            }

            public void SendToPeer(NetworkEnvelope envelope, ulong peerId) => PeerMessages.Add(new PeerMessage(envelope, peerId));

            public void SendToOthers(NetworkEnvelope envelope, ulong excludedPeerId)
            {
                throw new InvalidOperationException("World runtime does not relay request envelopes.");
            }

            public void SendToEveryone(NetworkEnvelope envelope)
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
                throw new InvalidOperationException("Client runtime cannot send directly to peers.");
            }

            public void SendToOthers(NetworkEnvelope envelope, ulong excludedPeerId)
            {
                throw new InvalidOperationException("Client runtime cannot broadcast.");
            }

            public void SendToEveryone(NetworkEnvelope envelope)
            {
                throw new InvalidOperationException("Client runtime cannot broadcast.");
            }
        }
    }
}
