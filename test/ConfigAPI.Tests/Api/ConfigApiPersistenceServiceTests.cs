using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Api;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;
using MarcoZechner.ConfigAPI.V2.Serialization;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.Logging;
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
        public void Same_Consumer_Can_Persist_Multiple_Independent_Configs_With_Distinct_Files()
        {
            var registrationId = Guid.NewGuid();
            var storage = new ConsumerStorage();
            var registry = RegisteredRegistry("Example.Mod", registrationId, storage);
            var service = new ConfigApiPersistenceService(registry, Clock());
            var settingsDefaults = Document(Entry("Value", Integer(10)));
            var tuningDefaults = Document(Entry("Value", Integer(20)));
            var settingsEdited = Document(Entry("Value", Integer(15)));
            var tuningEdited = Document(Entry("Value", Integer(25)));

            service.Open("Example.Mod", registrationId, "Settings", 0, "settings.toml", ConfigDocumentWireCodec.Encode(settingsDefaults));
            service.Open("Example.Mod", registrationId, "Tuning", 0, "tuning.toml", ConfigDocumentWireCodec.Encode(tuningDefaults));
            service.Save("Example.Mod", registrationId, "Settings", 0, "settings.toml", ConfigDocumentWireCodec.Encode(settingsDefaults), ConfigDocumentWireCodec.Encode(settingsEdited));
            service.Save("Example.Mod", registrationId, "Tuning", 0, "tuning.toml", ConfigDocumentWireCodec.Encode(tuningDefaults), ConfigDocumentWireCodec.Encode(tuningEdited));

            ConfigDocument settingsReloaded = ConfigDocumentWireCodec.Decode(service.Open("Example.Mod", registrationId, "Settings", 0, "settings.toml", ConfigDocumentWireCodec.Encode(settingsDefaults)));
            ConfigDocument tuningReloaded = ConfigDocumentWireCodec.Decode(service.Open("Example.Mod", registrationId, "Tuning", 0, "tuning.toml", ConfigDocumentWireCodec.Encode(tuningDefaults)));
            ConfigProvenance settingsProvenance = ConfigProvenanceCodec.Decode(storage.Get(0, "settings.toml.configapi.provenance"));
            ConfigProvenance tuningProvenance = ConfigProvenanceCodec.Decode(storage.Get(0, "tuning.toml.configapi.provenance"));

            Assert.Multiple(() =>
            {
                AssertDocumentValue(settingsReloaded, 15, "Value");
                AssertDocumentValue(tuningReloaded, 25, "Value");
                Assert.That(storage.Get(0, "settings.toml"), Does.Contain("Value = 15"));
                Assert.That(storage.Get(0, "tuning.toml"), Does.Contain("Value = 25"));
                Assert.That(settingsProvenance.Identity.OwnerId, Is.EqualTo("Example.Mod"));
                Assert.That(settingsProvenance.Identity.ConfigKey, Is.EqualTo("Settings"));
                Assert.That(tuningProvenance.Identity.OwnerId, Is.EqualTo("Example.Mod"));
                Assert.That(tuningProvenance.Identity.ConfigKey, Is.EqualTo("Tuning"));
            });
        }

        [Test]
        public void Same_File_Cannot_Be_Reused_By_A_Different_Config_Key()
        {
            var registrationId = Guid.NewGuid();
            var storage = new ConsumerStorage();
            var registry = RegisteredRegistry("Example.Mod", registrationId, storage);
            var service = new ConfigApiPersistenceService(registry, Clock());
            var settingsDefaults = Document(Entry("Value", Integer(10)));
            var tuningDefaults = Document(Entry("Value", Integer(20)));

            service.Open("Example.Mod", registrationId, "Settings", 0, "shared.toml", ConfigDocumentWireCodec.Encode(settingsDefaults));
            string activeBefore = storage.Get(0, "shared.toml");
            string provenanceBefore = storage.Get(0, "shared.toml.configapi.provenance");
            storage.ClearOperations();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                service.Open("Example.Mod", registrationId, "Tuning", 0, "shared.toml", ConfigDocumentWireCodec.Encode(tuningDefaults)));

            ConfigProvenance provenanceAfter = ConfigProvenanceCodec.Decode(storage.Get(0, "shared.toml.configapi.provenance"));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Is.EqualTo("Config provenance identity does not match the requested config identity."));
                Assert.That(storage.WriteCount(0), Is.EqualTo(0));
                Assert.That(storage.Get(0, "shared.toml"), Is.EqualTo(activeBefore));
                Assert.That(storage.Get(0, "shared.toml.configapi.provenance"), Is.EqualTo(provenanceBefore));
                Assert.That(provenanceAfter.Identity.OwnerId, Is.EqualTo("Example.Mod"));
                Assert.That(provenanceAfter.Identity.ConfigKey, Is.EqualTo("Settings"));
            });
        }

        [Test]
        public void Local_And_Global_Configs_With_The_Same_File_Remain_Independent()
        {
            var registrationId = Guid.NewGuid();
            var storage = new ConsumerStorage();
            var registry = RegisteredRegistry("Example.Mod", registrationId, storage);
            var service = new ConfigApiPersistenceService(registry, Clock());
            var defaults = Document(Entry("Value", Integer(10)));
            var localEdited = Document(Entry("Value", Integer(20)));
            var globalEdited = Document(Entry("Value", Integer(30)));

            service.Open("Example.Mod", registrationId, "Settings", 0, "settings.toml", ConfigDocumentWireCodec.Encode(defaults));
            service.Open("Example.Mod", registrationId, "Settings", 1, "settings.toml", ConfigDocumentWireCodec.Encode(defaults));
            service.Save("Example.Mod", registrationId, "Settings", 0, "settings.toml", ConfigDocumentWireCodec.Encode(defaults), ConfigDocumentWireCodec.Encode(localEdited));
            service.Save("Example.Mod", registrationId, "Settings", 1, "settings.toml", ConfigDocumentWireCodec.Encode(defaults), ConfigDocumentWireCodec.Encode(globalEdited));

            ConfigDocument localReloaded = ConfigDocumentWireCodec.Decode(service.Open("Example.Mod", registrationId, "Settings", 0, "settings.toml", ConfigDocumentWireCodec.Encode(defaults)));
            ConfigDocument globalReloaded = ConfigDocumentWireCodec.Decode(service.Open("Example.Mod", registrationId, "Settings", 1, "settings.toml", ConfigDocumentWireCodec.Encode(defaults)));

            Assert.Multiple(() =>
            {
                AssertDocumentValue(localReloaded, 20, "Value");
                AssertDocumentValue(globalReloaded, 30, "Value");
                Assert.That(storage.Get(0, "settings.toml"), Does.Contain("Value = 20"));
                Assert.That(storage.Get(1, "settings.toml"), Does.Contain("Value = 30"));
                Assert.That(storage.Get(0, "settings.toml.configapi.provenance"), Is.Not.Null);
                Assert.That(storage.Get(1, "settings.toml.configapi.provenance"), Is.Not.Null);
            });
        }

        [Test]
        public void Local_And_Global_Configs_Survive_Fresh_Service_And_Registration()
        {
            var storage = new ConsumerStorage();
            var defaults = Document(Entry("Value", Integer(10)));
            var localEdited = Document(Entry("Value", Integer(20)));
            var globalEdited = Document(Entry("Value", Integer(30)));

            Guid firstRegistrationId = Guid.NewGuid();
            var firstRegistry = RegisteredRegistry("Example.Mod", firstRegistrationId, storage);
            var firstService = new ConfigApiPersistenceService(firstRegistry, Clock());

            firstService.Open("Example.Mod", firstRegistrationId, "Settings", 0, "settings.toml", ConfigDocumentWireCodec.Encode(defaults));
            firstService.Open("Example.Mod", firstRegistrationId, "Settings", 1, "settings.toml", ConfigDocumentWireCodec.Encode(defaults));
            firstService.Save("Example.Mod", firstRegistrationId, "Settings", 0, "settings.toml", ConfigDocumentWireCodec.Encode(defaults), ConfigDocumentWireCodec.Encode(localEdited));
            firstService.Save("Example.Mod", firstRegistrationId, "Settings", 1, "settings.toml", ConfigDocumentWireCodec.Encode(defaults), ConfigDocumentWireCodec.Encode(globalEdited));

            string localProvenanceBefore = storage.Get(0, "settings.toml.configapi.provenance");
            string globalProvenanceBefore = storage.Get(1, "settings.toml.configapi.provenance");
            storage.ClearOperations();

            Guid secondRegistrationId = Guid.NewGuid();
            var secondRegistry = RegisteredRegistry("Example.Mod", secondRegistrationId, storage);
            var secondService = new ConfigApiPersistenceService(secondRegistry, Clock());

            ConfigDocument localReloaded = ConfigDocumentWireCodec.Decode(secondService.Open("Example.Mod", secondRegistrationId, "Settings", 0, "settings.toml", ConfigDocumentWireCodec.Encode(defaults)));
            ConfigDocument globalReloaded = ConfigDocumentWireCodec.Decode(secondService.Open("Example.Mod", secondRegistrationId, "Settings", 1, "settings.toml", ConfigDocumentWireCodec.Encode(defaults)));

            Assert.Multiple(() =>
            {
                AssertDocumentValue(localReloaded, 20, "Value");
                AssertDocumentValue(globalReloaded, 30, "Value");
                Assert.That(storage.Get(0, "settings.toml"), Does.Contain("Value = 20"));
                Assert.That(storage.Get(1, "settings.toml"), Does.Contain("Value = 30"));
                Assert.That(storage.Get(0, "settings.toml.configapi.provenance"), Is.EqualTo(localProvenanceBefore));
                Assert.That(storage.Get(1, "settings.toml.configapi.provenance"), Is.EqualTo(globalProvenanceBefore));
                Assert.That(storage.WriteCount(0), Is.EqualTo(0));
                Assert.That(storage.WriteCount(1), Is.EqualTo(0));
            });
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Open_Malformed_Persisted_Toml_Fails_Without_Rewriting_Storage(int location)
        {
            var registrationId = Guid.NewGuid();
            var storage = new ConsumerStorage();
            var registry = RegisteredRegistry("Example.Mod", registrationId, storage);
            var service = new ConfigApiPersistenceService(registry, Clock());
            var defaults = Document(Entry("Value", Integer(10)));
            var identity = new ConfigIdentity("Example.Mod", "Settings");
            const string malformed = "Value = [\n";
            string provenance = ConfigProvenanceCodec.Encode(new ConfigProvenance(identity, defaults));

            storage.Set(location, "settings.toml", malformed);
            storage.Set(location, "settings.toml.configapi.provenance", provenance);
            storage.ClearOperations();

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                service.Open("Example.Mod", registrationId, "Settings", location, "settings.toml", ConfigDocumentWireCodec.Encode(defaults)));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.StartWith("Source must be valid TOML."));
                Assert.That(storage.WriteCount(location), Is.EqualTo(0));
                Assert.That(storage.Get(location, "settings.toml"), Is.EqualTo(malformed));
                Assert.That(storage.Get(location, "settings.toml.configapi.provenance"), Is.EqualTo(provenance));
                Assert.That(storage.WriteCount(location == 0 ? 1 : 0), Is.EqualTo(0));
            });
        }

        [Test]
        public void Full_Semantic_Document_RoundTrips_Through_Persistence_Including_Null()
        {
            var registrationId = Guid.NewGuid();
            var storage = new ConsumerStorage();
            var registry = RegisteredRegistry("Example.Mod", registrationId, storage);
            var service = new ConfigApiPersistenceService(registry, Clock());

            var defaultDate = new ConfigLocalDate(2026, 9, 20);
            var defaultTime = new ConfigLocalTime(12, 34, 56, "123");
            var editedDate = new ConfigLocalDate(2027, 1, 2);
            var editedTime = new ConfigLocalTime(3, 4, 5, "6789");

            var defaults = Document(
                Entry("Enabled", ConfigScalarNode.Boolean(true)),
                Entry("Count", Integer(10)),
                Entry("Ratio", ConfigScalarNode.Float(1.25)),
                Entry("Name", ConfigScalarNode.String("default")),
                Entry("Offset", ConfigScalarNode.OffsetDateTime(new ConfigOffsetDateTime(defaultDate, defaultTime, -90))),
                Entry("LocalDateTime", ConfigScalarNode.LocalDateTime(new ConfigLocalDateTime(defaultDate, defaultTime))),
                Entry("LocalDate", ConfigScalarNode.LocalDate(defaultDate)),
                Entry("LocalTime", ConfigScalarNode.LocalTime(defaultTime)),
                Entry("Nested", new ConfigObjectNode(Entry("Threshold", Integer(5)), Entry("Label", ConfigScalarNode.String("base")))),
                Entry("Items", new ConfigArrayNode(Integer(1), ConfigScalarNode.String("two"), ConfigScalarNode.Boolean(false))),
                Entry("Optional", ConfigScalarNode.String("fallback")));

            var edited = Document(
                Entry("Enabled", ConfigScalarNode.Boolean(false)),
                Entry("Count", Integer(-42)),
                Entry("Ratio", ConfigScalarNode.Float(9.5)),
                Entry("Name", ConfigScalarNode.String("edited")),
                Entry("Offset", ConfigScalarNode.OffsetDateTime(new ConfigOffsetDateTime(editedDate, editedTime, 120))),
                Entry("LocalDateTime", ConfigScalarNode.LocalDateTime(new ConfigLocalDateTime(editedDate, editedTime))),
                Entry("LocalDate", ConfigScalarNode.LocalDate(editedDate)),
                Entry("LocalTime", ConfigScalarNode.LocalTime(editedTime)),
                Entry("Nested", new ConfigObjectNode(Entry("Threshold", Integer(25)), Entry("Label", ConfigScalarNode.String("changed")))),
                Entry("Items", new ConfigArrayNode(Integer(7), ConfigScalarNode.String("eight"), ConfigScalarNode.Boolean(true))),
                Entry("Optional", ConfigNullNode.Instance));

            ConfigDocument initiallyOpened = ConfigDocumentWireCodec.Decode(
                service.Open("Example.Mod", registrationId, "Settings", 0, "semantic.toml", ConfigDocumentWireCodec.Encode(defaults)));

            ConfigDocument saved = ConfigDocumentWireCodec.Decode(
                service.Save("Example.Mod", registrationId, "Settings", 0, "semantic.toml", ConfigDocumentWireCodec.Encode(defaults), ConfigDocumentWireCodec.Encode(edited)));

            ConfigDocument reloaded = ConfigDocumentWireCodec.Decode(
                service.Open("Example.Mod", registrationId, "Settings", 0, "semantic.toml", ConfigDocumentWireCodec.Encode(defaults)));

            string activeSource = storage.Get(0, "semantic.toml");

            Assert.Multiple(() =>
            {
                Assert.That(initiallyOpened.Equals(defaults), Is.True);
                Assert.That(saved.Equals(edited), Is.True);
                Assert.That(reloaded.Equals(edited), Is.True);
                Assert.That(activeSource, Does.Contain("#!Optional = \"fallback\""));
                Assert.That(activeSource, Does.Contain("2027-01-02"));
                Assert.That(activeSource, Does.Contain("9.5"));
                Assert.That(storage.Get(0, "semantic.toml.configapi.provenance"), Is.Not.Null);
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
        public void Save_Unrepresentable_Semantic_Null_Fails_Without_Writing_Storage()
        {
            var registrationId = Guid.NewGuid();
            var storage = new ConsumerStorage();
            var registry = RegisteredRegistry("Example.Mod", registrationId, storage);
            var service = new ConfigApiPersistenceService(registry, Clock());
            var values = Document(Entry("Optional", ConfigNullNode.Instance));
            object payload = ConfigDocumentWireCodec.Encode(values);

            NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
                service.Save("Example.Mod", registrationId, "Settings", 0, "settings.toml", payload, payload));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("semantic null"));
                Assert.That(storage.WriteCount(0), Is.EqualTo(0));
                Assert.That(storage.Get(0, "settings.toml"), Is.Null);
                Assert.That(storage.Get(0, "settings.toml.configapi.provenance"), Is.Null);
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

            Assert.That(announcement.Endpoints.Count, Is.EqualTo(10));

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

        [Test]
        public void Logging_Failures_Do_Not_Break_Open_Or_Save()
        {
            var registrationId = Guid.NewGuid();
            var storage = new ConsumerStorage();
            var registry = RegisteredRegistry("Example.Mod", registrationId, storage);
            var logger = new Logger("ConfigAPI.Tests", new ThrowingLogSink(), LogLevel.Trace);
            var service = new ConfigApiPersistenceService(registry, Clock(), logger);
            var defaults = Document(Entry("Value", Integer(10)));
            var edited = Document(Entry("Value", Integer(25)));
            object openPayload = null;
            object savePayload = null;

            Assert.DoesNotThrow(() => openPayload = service.Open(
                "Example.Mod", registrationId, "Settings", 0, "settings.toml", ConfigDocumentWireCodec.Encode(defaults)));

            Assert.DoesNotThrow(() => savePayload = service.Save(
                "Example.Mod", registrationId, "Settings", 0, "settings.toml",
                ConfigDocumentWireCodec.Encode(defaults), ConfigDocumentWireCodec.Encode(edited)));

            Assert.Multiple(() =>
            {
                Assert.That(ConfigDocumentWireCodec.Decode(openPayload).Equals(defaults), Is.True);
                Assert.That(ConfigDocumentWireCodec.Decode(savePayload).Equals(edited), Is.True);
                Assert.That(storage.Get(0, "settings.toml"), Does.Contain("Value = 25"));
            });
        }

        private sealed class ThrowingLogSink : ILogSink
        {
            public void Write(LogEntry entry)
            {
                throw new InvalidOperationException("Synthetic logging failure.");
            }
        }
        private static ConfigConsumerRegistrationRegistry RegisteredRegistry(
            string consumerId,
            Guid registrationId,
            ConsumerStorage storage)
        {
            var registry = new ConfigConsumerRegistrationRegistry();
            registry.RegisterReadWriteStorage(consumerId, registrationId, storage.Read, storage.Write);
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
