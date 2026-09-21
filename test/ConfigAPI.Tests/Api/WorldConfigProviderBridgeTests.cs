using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Api;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;
using Mz.Networking;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Api
{
    [TestFixture]
    public sealed class WorldConfigProviderBridgeTests
    {
        [Test]
        public void Queued_Server_Open_Flushes_When_Runtime_Attaches()
        {
            TestRig rig = CreateRig(false);
            rig.Bridge.Open("Example.Mod", rig.RegistrationId, "Settings", "settings.toml", Encode(Document(Entry("Value", Integer(10)))));

            Assert.That(rig.Responses.Count, Is.EqualTo(0));

            rig.AttachRuntime();

            Assert.Multiple(() =>
            {
                Assert.That(rig.Responses.Count, Is.EqualTo(1));
                Assert.That(rig.Responses[0]["Operation"], Is.EqualTo("Open"));
                Assert.That(rig.Responses[0]["Error"], Is.Null);
                Assert.That(rig.Responses[0]["ServerIteration"], Is.EqualTo(0UL));
                Assert.That(rig.Responses[0]["CurrentFile"], Is.EqualTo("settings.toml"));
                AssertDocumentValue(DecodeDocument(rig.Responses[0]), 10, "Value");
                Assert.That(rig.Transport.EveryoneMessages.Count, Is.EqualTo(0));
            });

            rig.Dispose();
        }

        [Test]
        public void Trusted_Server_Save_Persists_Broadcasts_And_Notifies_Local_Consumer()
        {
            TestRig rig = CreateRig(true);
            rig.Bridge.Open("Example.Mod", rig.RegistrationId, "Settings", "settings.toml", Encode(Document(Entry("Value", Integer(10)))));
            rig.Responses.Clear();
            rig.Transport.Clear();

            rig.Bridge.Save("Example.Mod", rig.RegistrationId, "Settings", Encode(Document(Entry("Value", Integer(20)))));

            Assert.Multiple(() =>
            {
                Assert.That(rig.Responses.Count, Is.EqualTo(1));
                Assert.That(rig.Responses[0]["Operation"], Is.EqualTo("Save"));
                Assert.That(rig.Responses[0]["IsApplied"], Is.EqualTo(true));
                Assert.That(rig.Responses[0]["IsStale"], Is.EqualTo(false));
                Assert.That(rig.Responses[0]["ServerIteration"], Is.EqualTo(1UL));
                AssertDocumentValue(DecodeDocument(rig.Responses[0]), 20, "Value");
                Assert.That(rig.Storage.Get(2, "settings.toml"), Does.Contain("Value = 20"));
                Assert.That(rig.Transport.EveryoneMessages.Count, Is.EqualTo(1));
            });

            WorldConfigNetworkResponse broadcast = WorldConfigNetworkCodec.DecodeResponse(rig.Transport.EveryoneMessages[0].Payload);

            Assert.Multiple(() =>
            {
                Assert.That(broadcast.TriggeredBy, Is.EqualTo(rig.Transport.LocalPeerId));
                Assert.That(broadcast.IsApplied, Is.True);
                Assert.That(broadcast.Snapshot.ServerIteration, Is.EqualTo(1UL));
                AssertDocumentValue(broadcast.Snapshot.Document, 20, "Value");
            });

            rig.Dispose();
        }

        [Test]
        public void Trusted_Server_File_Operations_Broadcast_Mutations_And_Keep_Export_Local()
        {
            TestRig rig = CreateRig(true);
            rig.Bridge.Open("Example.Mod", rig.RegistrationId, "Settings", "settings.toml", Encode(Document(Entry("Value", Integer(10)))));
            rig.Storage.Write(2, "alternate.toml", "Value = 30\n");
            rig.Responses.Clear();
            rig.Transport.Clear();

            rig.Bridge.LoadAndSwitch("Example.Mod", rig.RegistrationId, "Settings", "alternate.toml");
            rig.Bridge.SaveAndSwitch("Example.Mod", rig.RegistrationId, "Settings", "saved.toml", Encode(Document(Entry("Value", Integer(40)))));
            rig.Storage.Write(2, "saved.toml", "Value = 45\n");
            rig.Bridge.Reload("Example.Mod", rig.RegistrationId, "Settings");
            rig.Bridge.Export("Example.Mod", rig.RegistrationId, "Settings", "copy.toml", Encode(Document(Entry("Value", Integer(50)))), false);

            Assert.Multiple(() =>
            {
                Assert.That(rig.Responses.Count, Is.EqualTo(4));

                Assert.That(rig.Responses[0]["Operation"], Is.EqualTo("LoadAndSwitch"));
                Assert.That(rig.Responses[0]["IsApplied"], Is.EqualTo(true));
                Assert.That(rig.Responses[0]["ServerIteration"], Is.EqualTo(1UL));
                Assert.That(rig.Responses[0]["CurrentFile"], Is.EqualTo("alternate.toml"));
                AssertDocumentValue(DecodeDocument(rig.Responses[0]), 30, "Value");

                Assert.That(rig.Responses[1]["Operation"], Is.EqualTo("SaveAndSwitch"));
                Assert.That(rig.Responses[1]["IsApplied"], Is.EqualTo(true));
                Assert.That(rig.Responses[1]["ServerIteration"], Is.EqualTo(2UL));
                Assert.That(rig.Responses[1]["CurrentFile"], Is.EqualTo("saved.toml"));
                AssertDocumentValue(DecodeDocument(rig.Responses[1]), 40, "Value");

                Assert.That(rig.Responses[2]["Operation"], Is.EqualTo("Reload"));
                Assert.That(rig.Responses[2]["IsApplied"], Is.EqualTo(true));
                Assert.That(rig.Responses[2]["ServerIteration"], Is.EqualTo(3UL));
                Assert.That(rig.Responses[2]["CurrentFile"], Is.EqualTo("saved.toml"));
                AssertDocumentValue(DecodeDocument(rig.Responses[2]), 45, "Value");

                Assert.That(rig.Responses[3]["Operation"], Is.EqualTo("Export"));
                Assert.That(rig.Responses[3]["IsApplied"], Is.EqualTo(false));
                Assert.That(rig.Responses[3]["IsStale"], Is.EqualTo(false));
                Assert.That(rig.Responses[3]["ServerIteration"], Is.EqualTo(3UL));
                Assert.That(rig.Responses[3]["CurrentFile"], Is.EqualTo("saved.toml"));
                AssertDocumentValue(DecodeDocument(rig.Responses[3]), 45, "Value");

                Assert.That(rig.Transport.EveryoneMessages.Count, Is.EqualTo(3), "Only authoritative mutations should broadcast.");
                Assert.That(rig.Storage.Get(2, "copy.toml"), Does.Contain("Value = 50"));
            });

            rig.Dispose();
        }

        [Test]
        public void Remote_Save_Broadcast_Updates_Server_Local_Consumer()
        {
            TestRig rig = CreateRig(true);
            rig.Bridge.Open("Example.Mod", rig.RegistrationId, "Settings", "settings.toml", Encode(Document(Entry("Value", Integer(10)))));
            rig.Responses.Clear();
            rig.Transport.Clear();

            var request = new WorldConfigNetworkRequest(
                41UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Save, 0UL,
                null, false, null, Document(Entry("Value", Integer(30))));

            NetworkReceiveContext context;
            bool dispatched = rig.Endpoint.Receive(
                new NetworkEnvelope(WorldConfigServerNetworkAdapter.RequestMessageType, 222UL, false, WorldConfigNetworkCodec.EncodeRequest(request)),
                222UL, false, out context);

            Assert.Multiple(() =>
            {
                Assert.That(dispatched, Is.True);
                Assert.That(rig.Responses.Count, Is.EqualTo(1));
                Assert.That(rig.Responses[0]["TriggeredBy"], Is.EqualTo(222UL));
                Assert.That(rig.Responses[0]["IsApplied"], Is.EqualTo(true));
                Assert.That(rig.Responses[0]["ServerIteration"], Is.EqualTo(1UL));
                AssertDocumentValue(DecodeDocument(rig.Responses[0]), 30, "Value");
                Assert.That(rig.Transport.EveryoneMessages.Count, Is.EqualTo(1));
            });

            rig.Dispose();
        }

        [Test]
        public void Remote_Open_Does_Not_Notify_Already_Opened_Server_Local_Consumer()
        {
            TestRig rig = CreateRig(true);
            rig.Bridge.Open("Example.Mod", rig.RegistrationId, "Settings", "settings.toml", Encode(Document(Entry("Value", Integer(10)))));
            rig.Responses.Clear();
            rig.Transport.Clear();

            var request = new WorldConfigNetworkRequest(
                40UL, "Example.Mod", "Settings", WorldConfigNetworkOperation.Open, 0UL,
                "settings.toml", false, Document(Entry("Value", Integer(10))), null);

            NetworkReceiveContext context;
            bool dispatched = rig.Endpoint.Receive(
                new NetworkEnvelope(WorldConfigServerNetworkAdapter.RequestMessageType, 222UL, false, WorldConfigNetworkCodec.EncodeRequest(request)),
                222UL, false, out context);

            Assert.Multiple(() =>
            {
                Assert.That(dispatched, Is.True);
                Assert.That(rig.Responses.Count, Is.EqualTo(0), "A remote Open does not change authority and must not notify the server-local consumer.");
                Assert.That(rig.Transport.PeerMessages.Count, Is.EqualTo(1));
                Assert.That(rig.Transport.EveryoneMessages.Count, Is.EqualTo(0));
            });

            rig.Dispose();
        }

        [Test]
        public void Server_Save_Before_Local_Open_Returns_Error_Without_Broadcast()
        {
            TestRig rig = CreateRig(true);

            rig.Bridge.Save("Example.Mod", rig.RegistrationId, "Settings", Encode(Document(Entry("Value", Integer(20)))));

            Assert.Multiple(() =>
            {
                Assert.That(rig.Responses.Count, Is.EqualTo(1));
                Assert.That(rig.Responses[0]["Operation"], Is.EqualTo("Save"));
                Assert.That(rig.Responses[0]["Error"], Does.Contain("has not been opened"));
                Assert.That(rig.Responses[0]["Document"], Is.Null);
                Assert.That(rig.Transport.EveryoneMessages.Count, Is.EqualTo(0));
            });

            rig.Dispose();
        }

        [Test]
        public void Client_Open_Notifies_Bootstrap_Before_Sending_Authoritative_Request()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            Guid registrationId = Guid.NewGuid();
            registry.Register("Example.Mod", registrationId, storage.Read, storage.Write);

            var identity = new ConfigIdentity("Example.Mod", "Settings");
            var bootstrap = new MemoryBootstrapStore(new WorldConfigSnapshot(identity, Document(Entry("Value", Integer(30))), 5UL, "settings.toml"));
            var transport = new RecordingClientTransport();
            var endpoint = new NetworkEndpoint(transport);
            var runtime = new WorldConfigNetworkRuntime(endpoint, transport, registry, new FixedClock(), new AllowAllAuthorization(), bootstrap);
            var bridge = new WorldConfigProviderBridge(registry);
            var responses = new List<IDictionary<string, object>>();
            Action unregister = bridge.Register("Example.Mod", registrationId, responses.Add);
            bridge.AttachRuntime(runtime);

            try
            {
                bridge.Open("Example.Mod", registrationId, "Settings", "settings.toml", Encode(Document(Entry("Value", Integer(10)))));

                Assert.Multiple(() =>
                {
                    Assert.That(responses.Count, Is.EqualTo(1));
                    Assert.That(responses[0]["Operation"], Is.EqualTo("Open"));
                    Assert.That(responses[0]["RequestId"], Is.EqualTo(0UL));
                    Assert.That(responses[0]["ServerIteration"], Is.EqualTo(5UL));
                    AssertDocumentValue(DecodeDocument(responses[0]), 30, "Value");
                    Assert.That(transport.ServerMessages.Count, Is.EqualTo(1));
                });
            }
            finally
            {
                bridge.DetachRuntime();
                unregister();
                bridge.Dispose();
                runtime.Dispose();
            }
        }

        [Test]
        public void Other_Client_Error_With_Colliding_Request_Id_Does_Not_Notify_Local_Consumer()
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            Guid registrationId = Guid.NewGuid();
            registry.Register("Example.Mod", registrationId, storage.Read, storage.Write);

            var transport = new RecordingClientTransport();
            var endpoint = new NetworkEndpoint(transport);
            var runtime = new WorldConfigNetworkRuntime(endpoint, transport, registry, new FixedClock(), new AllowAllAuthorization());
            var bridge = new WorldConfigProviderBridge(registry);
            var responses = new List<IDictionary<string, object>>();
            Action unregister = bridge.Register("Example.Mod", registrationId, responses.Add);
            bridge.AttachRuntime(runtime);

            try
            {
                bridge.Open("Example.Mod", registrationId, "Settings", "settings.toml", Encode(Document(Entry("Value", Integer(10)))));
                WorldConfigNetworkRequest openRequest = WorldConfigNetworkCodec.DecodeRequest(transport.ServerMessages[0].Payload);
                var snapshot = new WorldConfigSnapshot(new ConfigIdentity("Example.Mod", "Settings"), Document(Entry("Value", Integer(10))), 0UL, "settings.toml");

                NetworkReceiveContext context;
                bool opened = endpoint.Receive(
                    new NetworkEnvelope(
                        WorldConfigServerNetworkAdapter.ResponseMessageType, 777UL, false,
                        WorldConfigNetworkCodec.EncodeResponse(new WorldConfigNetworkResponse(
                            openRequest.RequestId, WorldConfigNetworkOperation.Open, WorldConfigNetworkResponseKind.Snapshot,
                            transport.LocalPeerId, false, false, snapshot, null))),
                    777UL, true, out context);

                Assert.That(opened, Is.True);
                Assert.That(responses.Count, Is.EqualTo(1));
                responses.Clear();

                bridge.Save("Example.Mod", registrationId, "Settings", Encode(Document(Entry("Value", Integer(20)))));
                WorldConfigNetworkRequest saveRequest = WorldConfigNetworkCodec.DecodeRequest(transport.ServerMessages[1].Payload);

                bool dispatched = endpoint.Receive(
                    new NetworkEnvelope(
                        WorldConfigServerNetworkAdapter.ResponseMessageType, 777UL, false,
                        WorldConfigNetworkCodec.EncodeResponse(new WorldConfigNetworkResponse(
                            saveRequest.RequestId, WorldConfigNetworkOperation.Save, WorldConfigNetworkResponseKind.Error,
                            222UL, false, false, null, "Synthetic other-client error."))),
                    777UL, true, out context);

                Assert.Multiple(() =>
                {
                    Assert.That(dispatched, Is.True);
                    Assert.That(responses.Count, Is.EqualTo(0), "Another client's colliding request ID must not route its error to our consumer.");
                });
            }
            finally
            {
                bridge.DetachRuntime();
                unregister();
                bridge.Dispose();
                runtime.Dispose();
            }
        }

        private static TestRig CreateRig(bool attachRuntime)
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            var storage = new MemoryStorage();
            Guid registrationId = Guid.NewGuid();
            registry.Register("Example.Mod", registrationId, storage.Read, storage.Write);

            var transport = new RecordingServerTransport();
            var endpoint = new NetworkEndpoint(transport);
            var runtime = new WorldConfigNetworkRuntime(endpoint, transport, registry, new FixedClock(), new AllowAllAuthorization());
            var bridge = new WorldConfigProviderBridge(registry);
            var responses = new List<IDictionary<string, object>>();
            Action unregister = bridge.Register("Example.Mod", registrationId, responses.Add);
            var rig = new TestRig(registrationId, storage, transport, endpoint, runtime, bridge, unregister, responses);

            if (attachRuntime)
                rig.AttachRuntime();

            return rig;
        }

        private static object Encode(ConfigDocument document) => ConfigDocumentWireCodec.Encode(document);

        private static ConfigDocument DecodeDocument(IDictionary<string, object> response)
            => ConfigDocumentWireCodec.Decode(response["Document"]);

        private static ConfigDocument Document(params ConfigObjectEntry[] entries) => new ConfigDocument(new ConfigObjectNode(entries));
        private static ConfigObjectEntry Entry(string name, ConfigNode value) => new ConfigObjectEntry(name, value);
        private static ConfigScalarNode Integer(long value) => ConfigScalarNode.Integer(value);

        private static void AssertDocumentValue(ConfigDocument document, long expected, params string[] path)
        {
            ConfigNode actual;
            Assert.That(document.TryGet(new ConfigValuePath(path), out actual), Is.True);
            Assert.That(actual.Equals(Integer(expected)), Is.True);
        }

        private sealed class TestRig : IDisposable
        {
            private readonly WorldConfigNetworkRuntime _runtime;
            private readonly Action _unregister;
            private bool _runtimeAttached;

            public TestRig(Guid registrationId, MemoryStorage storage, RecordingServerTransport transport, NetworkEndpoint endpoint, WorldConfigNetworkRuntime runtime,
                           WorldConfigProviderBridge bridge, Action unregister, List<IDictionary<string, object>> responses)
            {
                RegistrationId = registrationId;
                Storage = storage;
                Transport = transport;
                Endpoint = endpoint;
                _runtime = runtime;
                Bridge = bridge;
                _unregister = unregister;
                Responses = responses;
            }

            public Guid RegistrationId { get; }
            public MemoryStorage Storage { get; }
            public RecordingServerTransport Transport { get; }
            public NetworkEndpoint Endpoint { get; }
            public WorldConfigProviderBridge Bridge { get; }
            public List<IDictionary<string, object>> Responses { get; }

            public void AttachRuntime()
            {
                Bridge.AttachRuntime(_runtime);
                _runtimeAttached = true;
            }

            public void Dispose()
            {
                if (_runtimeAttached)
                    Bridge.DetachRuntime();

                _unregister();
                Bridge.Dispose();
                _runtime.Dispose();
            }
        }

        private sealed class AllowAllAuthorization : IWorldConfigAuthorization
        {
            public bool IsAdmin(ulong playerId) => true;
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

            public bool TryRead(ConfigIdentity identity, out WorldConfigSnapshot snapshot)
            {
                snapshot = _snapshot != null && _snapshot.Identity.Equals(identity) ? _snapshot : null;
                return snapshot != null;
            }

            public void Write(WorldConfigSnapshot snapshot) { }
        }

        private sealed class RecordingServerTransport : INetworkTransport
        {
            public bool IsServer => true;
            public ulong LocalPeerId => 777UL;
            public List<PeerMessage> PeerMessages { get; } = new List<PeerMessage>();
            public List<NetworkEnvelope> EveryoneMessages { get; } = new List<NetworkEnvelope>();

            public void SendToServer(NetworkEnvelope envelope)
            {
                throw new InvalidOperationException("Server runtime must not send requests to itself through the transport.");
            }
            public void SendToPeer(NetworkEnvelope envelope, ulong peerId) => PeerMessages.Add(new PeerMessage(envelope, peerId));
            public void SendToOthers(NetworkEnvelope envelope, ulong excludedPeerId)
            {
                throw new InvalidOperationException("World runtime does not relay request envelopes.");
            }
            public void SendToEveryone(NetworkEnvelope envelope) => EveryoneMessages.Add(envelope);

            public void Clear()
            {
                PeerMessages.Clear();
                EveryoneMessages.Clear();
            }
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

        public sealed class MemoryStorage
        {
            private readonly Dictionary<string, string> _content = new Dictionary<string, string>(StringComparer.Ordinal);

            public string Read(int location, string file)
            {
                string content;
                return _content.TryGetValue(Key(location, file), out content) ? content : null;
            }

            public void Write(int location, string file, string content) => _content[Key(location, file)] = content;

            public string Get(int location, string file)
            {
                string content;
                return _content.TryGetValue(Key(location, file), out content) ? content : null;
            }

            private static string Key(int location, string file) => location + "|" + file;
        }
    }
}