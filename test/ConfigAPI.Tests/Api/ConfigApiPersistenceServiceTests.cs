using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Api;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;
using MarcoZechner.ConfigAPI.V2.Serialization;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.SemanticVersioning;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Api
{
    [TestFixture]
    public sealed class ConfigApiPersistenceServiceTests
    {
        [Test]
        public void Open_Missing_Local_Config_Persists_Defaults_With_Consumer_Owned_Identity()
        {
            var registrationId = Guid.NewGuid();
            var storage = new ConsumerStorage();
            var registry = RegisteredRegistry("Example.Mod", registrationId, storage);
            var service = new ConfigApiPersistenceService(registry, Clock());
            var defaults = Document(Entry("Value", Integer(10)));

            object resultPayload = service.Open(
                "Example.Mod",
                registrationId,
                "Settings",
                0,
                "settings.toml",
                ConfigDocumentWireCodec.Encode(defaults));

            var result = ConfigDocumentWireCodec.Decode(resultPayload);
            var active = storage.Get(0, "settings.toml");
            var provenanceSource = storage.Get(0, "settings.toml.configapi.provenance");
            var provenance = ConfigProvenanceCodec.Decode(provenanceSource);

            Assert.Multiple(() =>
            {
                Assert.That(result.Equals(defaults), Is.True);
                Assert.That(active, Is.Not.Null);
                Assert.That(provenance.Identity.OwnerId, Is.EqualTo("Example.Mod"));
                Assert.That(provenance.Identity.ConfigKey, Is.EqualTo("Settings"));
                Assert.That(provenance.BaselineDefaults.Equals(defaults), Is.True);
                Assert.That(storage.WriteCount(0), Is.EqualTo(2));
                Assert.That(storage.WriteCount(1), Is.EqualTo(0));
                Assert.That(storage.WriteCount(2), Is.EqualTo(0));
            });
        }

        [Test]
        public void Open_Existing_Global_Config_Reconciles_Changed_Default_And_Persists_Result()
        {
            var registrationId = Guid.NewGuid();
            var storage = new ConsumerStorage();
            var registry = RegisteredRegistry("Example.Mod", registrationId, storage);
            var service = new ConfigApiPersistenceService(registry, Clock());
            var identity = new ConfigIdentity("Example.Mod", "Settings");
            var oldDefaults = Document(Entry("Value", Integer(10)));
            var currentDefaults = Document(Entry("Value", Integer(20)));

            storage.Set(1, "settings.toml", "Value = 10\n");
            storage.Set(
                1,
                "settings.toml.configapi.provenance",
                ConfigProvenanceCodec.Encode(new ConfigProvenance(identity, oldDefaults)));
            storage.ClearOperations();

            object resultPayload = service.Open(
                "Example.Mod",
                registrationId,
                "Settings",
                1,
                "settings.toml",
                ConfigDocumentWireCodec.Encode(currentDefaults));

            var result = ConfigDocumentWireCodec.Decode(resultPayload);
            var active = storage.Get(1, "settings.toml");
            var provenance = ConfigProvenanceCodec.Decode(
                storage.Get(1, "settings.toml.configapi.provenance"));

            Assert.Multiple(() =>
            {
                AssertDocumentValue(result, 20, "Value");
                Assert.That(active, Does.Contain("Value = 20"));
                AssertDocumentValue(provenance.BaselineDefaults, 20, "Value");
                Assert.That(storage.WriteCount(0), Is.EqualTo(0));
                Assert.That(storage.WriteCount(1), Is.EqualTo(2));
                Assert.That(storage.WriteCount(2), Is.EqualTo(0));
            });
        }

        [Test]
        public void Save_Persists_Explicit_Player_Values_Without_Moving_Default_Baseline()
        {
            var registrationId = Guid.NewGuid();
            var storage = new ConsumerStorage();
            var registry = RegisteredRegistry("Example.Mod", registrationId, storage);
            var service = new ConfigApiPersistenceService(registry, Clock());
            var defaults = Document(Entry("Value", Integer(10)));
            var edited = Document(Entry("Value", Integer(25)));

            service.Open(
                "Example.Mod",
                registrationId,
                "Settings",
                0,
                "settings.toml",
                ConfigDocumentWireCodec.Encode(defaults));

            storage.ClearOperations();

            object resultPayload = service.Save(
                "Example.Mod",
                registrationId,
                "Settings",
                0,
                "settings.toml",
                ConfigDocumentWireCodec.Encode(defaults),
                ConfigDocumentWireCodec.Encode(edited));

            var result = ConfigDocumentWireCodec.Decode(resultPayload);
            var persisted = ConfigTomlSourceDecoder.Decode(
                storage.Get(0, "settings.toml"),
                defaults);
            var provenance = ConfigProvenanceCodec.Decode(
                storage.Get(0, "settings.toml.configapi.provenance"));

            Assert.Multiple(() =>
            {
                Assert.That(result.Equals(edited), Is.True);
                Assert.That(persisted.Equals(edited), Is.True);
                Assert.That(provenance.BaselineDefaults.Equals(defaults), Is.True);
                Assert.That(storage.WriteCount(0), Is.EqualTo(2));
            });
        }

        [Test]
        public void Save_Rejects_Player_Values_The_Schema_Reconciler_Would_Alter()
        {
            var registrationId = Guid.NewGuid();
            var storage = new ConsumerStorage();
            var registry = RegisteredRegistry("Example.Mod", registrationId, storage);
            var service = new ConfigApiPersistenceService(registry, Clock());
            var defaults = Document(Entry("Value", Integer(10)));

            service.Open(
                "Example.Mod",
                registrationId,
                "Settings",
                0,
                "settings.toml",
                ConfigDocumentWireCodec.Encode(defaults));

            string activeBefore = storage.Get(0, "settings.toml");
            string provenanceBefore =
                storage.Get(0, "settings.toml.configapi.provenance");

            storage.ClearOperations();

            var incompatible =
                Document(
                    Entry(
                        "Value",
                        ConfigScalarNode.String("wrong-kind")));

            var missing =
                Document();

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(() =>
                    service.Save(
                        "Example.Mod",
                        registrationId,
                        "Settings",
                        0,
                        "settings.toml",
                        ConfigDocumentWireCodec.Encode(defaults),
                        ConfigDocumentWireCodec.Encode(incompatible)));

                Assert.Throws<ArgumentException>(() =>
                    service.Save(
                        "Example.Mod",
                        registrationId,
                        "Settings",
                        0,
                        "settings.toml",
                        ConfigDocumentWireCodec.Encode(defaults),
                        ConfigDocumentWireCodec.Encode(missing)));

                Assert.That(storage.WriteCount(0), Is.EqualTo(0));
                Assert.That(storage.Get(0, "settings.toml"), Is.EqualTo(activeBefore));
                Assert.That(
                    storage.Get(0, "settings.toml.configapi.provenance"),
                    Is.EqualTo(provenanceBefore));
            });
        }

        [Test]
        public void Open_Rejects_Stale_Registration_And_Unsupported_Location()
        {
            var registrationId = Guid.NewGuid();
            var storage = new ConsumerStorage();
            var registry = RegisteredRegistry("Example.Mod", registrationId, storage);
            var service = new ConfigApiPersistenceService(registry, Clock());
            object defaults = ConfigDocumentWireCodec.Encode(Document());

            Assert.Multiple(() =>
            {
                Assert.Throws<InvalidOperationException>(() =>
                    service.Open(
                        "Example.Mod",
                        Guid.NewGuid(),
                        "Settings",
                        0,
                        "settings.toml",
                        defaults));

                Assert.Throws<ArgumentException>(() =>
                    service.Open(
                        "Example.Mod",
                        registrationId,
                        "Settings",
                        3,
                        "settings.toml",
                        defaults));
            });
        }

        [Test]
        public void Direct_Open_And_Save_Reject_World_Location()
        {
            var registrationId = Guid.NewGuid();
            var storage = new ConsumerStorage();
            var registry = RegisteredRegistry("Example.Mod", registrationId, storage);
            var service = new ConfigApiPersistenceService(registry, Clock());
            object defaults = ConfigDocumentWireCodec.Encode(
                Document(Entry("Value", Integer(10))));

            Assert.Multiple(() =>
            {
                Assert.Throws<InvalidOperationException>(() =>
                    service.Open(
                        "Example.Mod",
                        registrationId,
                        "Settings",
                        2,
                        "settings.toml",
                        defaults));

                Assert.Throws<InvalidOperationException>(() =>
                    service.Save(
                        "Example.Mod",
                        registrationId,
                        "Settings",
                        2,
                        "settings.toml",
                        defaults,
                        defaults));

                Assert.That(storage.WriteCount(2), Is.EqualTo(0));
                Assert.That(storage.Get(2, "settings.toml"), Is.Null);
            });
        }

        [Test]
        public void Provider_Publishes_Exact_Open_And_Save_Endpoints()
        {
            var bus = new RecordingModMessageBus();
            var registry = new ConfigConsumerRegistrationRegistry();
            var provider = new ConfigApiProvider(
                bus,
                registry,
                Clock(),
                new SemanticVersion(1, 2, 3));

            provider.Start();

            ApiAnnouncement announcement;
            Assert.That(
                ApiDiscoveryWireProtocol.TryParseAnnouncement(
                    bus.SentPayloads[0],
                    out announcement),
                Is.True);

            Assert.That(announcement.Endpoints.Count, Is.EqualTo(3));

            var open = announcement.Endpoints[ConfigApiProvider.OpenConfigEndpoint] as
                Func<string, Guid, string, int, string, object, object>;

            var save = announcement.Endpoints[ConfigApiProvider.SaveConfigEndpoint] as
                Func<string, Guid, string, int, string, object, object, object>;

            Assert.Multiple(() =>
            {
                Assert.That(open, Is.Not.Null);
                Assert.That(save, Is.Not.Null);
            });

            provider.Dispose();
        }

        private static ConfigConsumerRegistrationRegistry RegisteredRegistry(
            string consumerId,
            Guid registrationId,
            ConsumerStorage storage)
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            registry.Register(consumerId, registrationId, storage.Read, storage.Write);
            return registry;
        }

        private static FixedClock Clock()
        {
            return new FixedClock(
                new DateTime(2026, 9, 2, 1, 0, 0, DateTimeKind.Utc));
        }

        private static ConfigDocument Document(params ConfigObjectEntry[] entries)
        {
            return new ConfigDocument(new ConfigObjectNode(entries));
        }

        private static ConfigObjectEntry Entry(string name, ConfigNode value)
        {
            return new ConfigObjectEntry(name, value);
        }

        private static ConfigScalarNode Integer(long value)
        {
            return ConfigScalarNode.Integer(value);
        }

        private static void AssertDocumentValue(
            ConfigDocument document,
            long expected,
            params string[] path)
        {
            ConfigNode actual;

            Assert.That(
                document.TryGet(new ConfigValuePath(path), out actual),
                Is.True);

            Assert.That(
                actual.Equals(Integer(expected)),
                Is.True);
        }

        private sealed class FixedClock : IConfigClock
        {
            public DateTime UtcNow { get; private set; }

            public FixedClock(DateTime utcNow)
            {
                UtcNow = utcNow;
            }
        }

        private sealed class ConsumerStorage
        {
            private readonly Dictionary<string, string> _content =
                new Dictionary<string, string>(StringComparer.Ordinal);

            private readonly List<string> _operations =
                new List<string>();

            public string Read(int location, string file)
            {
                _operations.Add("READ|" + location + "|" + file);

                string content;
                return _content.TryGetValue(Key(location, file), out content)
                    ? content
                    : null;
            }

            public void Write(int location, string file, string content)
            {
                _operations.Add("WRITE|" + location + "|" + file);
                _content[Key(location, file)] = content;
            }

            public void Set(int location, string file, string content)
            {
                _content[Key(location, file)] = content;
            }

            public string Get(int location, string file)
            {
                string content;
                return _content.TryGetValue(Key(location, file), out content)
                    ? content
                    : null;
            }

            public int WriteCount(int location)
            {
                var prefix = "WRITE|" + location + "|";
                var count = 0;

                for (var i = 0; i < _operations.Count; i++)
                {
                    if (_operations[i].StartsWith(prefix, StringComparison.Ordinal))
                        count++;
                }

                return count;
            }

            public void ClearOperations()
            {
                _operations.Clear();
            }

            private static string Key(int location, string file)
            {
                return location + "|" + file;
            }
        }

        private sealed class RecordingModMessageBus : IModMessageBus
        {
            private readonly Dictionary<long, List<Action<object>>> _handlers =
                new Dictionary<long, List<Action<object>>>();

            public List<object> SentPayloads { get; private set; }

            public RecordingModMessageBus()
            {
                SentPayloads = new List<object>();
            }

            public void RegisterHandler(long channelId, Action<object> handler)
            {
                List<Action<object>> handlers;

                if (!_handlers.TryGetValue(channelId, out handlers))
                {
                    handlers = new List<Action<object>>();
                    _handlers.Add(channelId, handlers);
                }

                handlers.Add(handler);
            }

            public void UnregisterHandler(long channelId, Action<object> handler)
            {
                List<Action<object>> handlers;

                if (_handlers.TryGetValue(channelId, out handlers))
                    handlers.Remove(handler);
            }

            public void Send(long channelId, object payload)
            {
                SentPayloads.Add(payload);

                List<Action<object>> handlers;
                if (!_handlers.TryGetValue(channelId, out handlers))
                    return;

                Action<object>[] snapshot = handlers.ToArray();

                for (var i = 0; i < snapshot.Length; i++)
                    snapshot[i](payload);
            }
        }
    }
}
