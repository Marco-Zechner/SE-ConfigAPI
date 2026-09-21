using System;
using System.Collections.Generic;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.ConfigApi;
using Mz.SemanticVersioning;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Consumer
{
    [TestFixture]
    public sealed class ConfigApiClientTests
    {
        [Test]
        public void Facade_Version_Matches_Current_Changelog()
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    ApiVersionFile.MinimumProviderApiVersion.ToString(),
                    Is.EqualTo("2.0.0"));

                Assert.That(
                    ApiVersionFile.Changelog.CurrentVersion.ToString(),
                    Is.EqualTo(ApiVersionFile.VersionString));

                Assert.That(
                    ApiVersionFile.Changelog.Current.Version.ToString(),
                    Is.EqualTo(ApiVersionFile.VersionString));
            });
        }

        [Test]
        public void Start_Discovers_Provider_And_Registers_Consumer_Callbacks()
        {
            var bus = new RecordingModMessageBus();
            string observedConsumerId = null;
            Guid observedRegistrationId = Guid.Empty;
            Func<int, string, string> observedRead = null;
            Action<int, string, string> observedWrite = null;
            var unregisterCount = 0;

            var provider =
                CreateProvider(
                    bus,
                    new SemanticVersion(2, 0, 0),
                    ValidEndpoints(
                        delegate(
                            string consumerId,
                            Guid registrationId,
                            Func<int, string, string> read,
                            Action<int, string, string> write)
                        {
                            observedConsumerId = consumerId;
                            observedRegistrationId = registrationId;
                            observedRead = read;
                            observedWrite = write;

                            return delegate
                            {
                                unregisterCount++;
                            };
                        }));

            provider.Start();

            var written = string.Empty;

            var client =
                CreateClient(
                    bus,
                    (location, file) => location + "|" + file,
                    (location, file, content) =>
                        written = location + "|" + file + "|" + content);

            client.Start();

            Assert.Multiple(() =>
            {
                Assert.That(client.IsConnected, Is.True);
                Assert.That(client.ProviderModVersion.ToString(), Is.EqualTo("0.1.0"));
                Assert.That(client.ProviderApiVersion.ToString(), Is.EqualTo("2.0.0"));
                Assert.That(observedConsumerId, Is.EqualTo("Example.Mod"));
                Assert.That(observedRegistrationId, Is.Not.EqualTo(Guid.Empty));
                Assert.That(observedRead, Is.Not.Null);
                Assert.That(observedWrite, Is.Not.Null);
            });

            Assert.That(
                observedRead(0, "config.toml"),
                Is.EqualTo("0|config.toml"));

            observedWrite(2, "world.toml", "content");

            Assert.That(
                written,
                Is.EqualTo("2|world.toml|content"));

            client.Dispose();

            Assert.That(unregisterCount, Is.EqualTo(1));

            provider.Dispose();
        }

        [Test]
        public void Provider_Reconnect_Uses_New_Registration_Id()
        {
            var bus = new RecordingModMessageBus();
            var registrationIds = new List<Guid>();
            var unregisterCount = 0;

            var firstProvider =
                CreateProvider(
                    bus,
                    new SemanticVersion(2, 0, 0),
                    ValidEndpoints(
                        delegate(
                            string consumerId,
                            Guid registrationId,
                            Func<int, string, string> read,
                            Action<int, string, string> write)
                        {
                            registrationIds.Add(registrationId);

                            return delegate
                            {
                                unregisterCount++;
                            };
                        }));

            firstProvider.Start();

            var client =
                CreateClient(
                    bus,
                    (location, file) => null,
                    (location, file, content) => { });

            client.Start();

            Assert.That(client.IsConnected, Is.True);
            Assert.That(registrationIds.Count, Is.EqualTo(1));

            firstProvider.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(client.IsConnected, Is.False);
                Assert.That(unregisterCount, Is.EqualTo(1));
            });

            var secondProvider =
                CreateProvider(
                    bus,
                    new SemanticVersion(2, 1, 0),
                    ValidEndpoints(
                        delegate(
                            string consumerId,
                            Guid registrationId,
                            Func<int, string, string> read,
                            Action<int, string, string> write)
                        {
                            registrationIds.Add(registrationId);

                            return delegate
                            {
                                unregisterCount++;
                            };
                        }));

            secondProvider.Start();

            Assert.Multiple(() =>
            {
                Assert.That(client.IsConnected, Is.True);
                Assert.That(client.ProviderApiVersion.ToString(), Is.EqualTo("2.1.0"));
                Assert.That(registrationIds.Count, Is.EqualTo(2));
                Assert.That(registrationIds[0], Is.Not.EqualTo(Guid.Empty));
                Assert.That(registrationIds[1], Is.Not.EqualTo(Guid.Empty));
                Assert.That(registrationIds[1], Is.Not.EqualTo(registrationIds[0]));
            });

            client.Dispose();

            Assert.That(unregisterCount, Is.EqualTo(2));

            secondProvider.Dispose();
        }

        [Test]
        public void Accepts_Newer_Provider_Without_Upper_Version_Ceiling()
        {
            var bus = new RecordingModMessageBus();

            var provider =
                CreateProvider(
                    bus,
                    new SemanticVersion(9, 0, 0),
                    ValidEndpoints(
                        delegate(
                            string consumerId,
                            Guid registrationId,
                            Func<int, string, string> read,
                            Action<int, string, string> write)
                        {
                            return delegate { };
                        }));

            provider.Start();

            var client =
                CreateClient(
                    bus,
                    (location, file) => null,
                    (location, file, content) => { });

            client.Start();

            Assert.Multiple(() =>
            {
                Assert.That(client.IsConnected, Is.True);
                Assert.That(client.ProviderApiVersion.ToString(), Is.EqualTo("9.0.0"));
            });

            client.Dispose();
            provider.Dispose();
        }

        [Test]
        public void Rejects_Provider_Missing_Exact_RegisterConsumer_Endpoint()
        {
            var bus = new RecordingModMessageBus();

            var provider =
                CreateProvider(
                    bus,
                    new SemanticVersion(2, 0, 0),
                    new Dictionary<string, Delegate>(StringComparer.Ordinal));

            provider.Start();

            var client =
                CreateClient(
                    bus,
                    (location, file) => null,
                    (location, file, content) => { });

            client.Start();

            Assert.Multiple(() =>
            {
                Assert.That(client.IsConnected, Is.False);
                Assert.That(client.LastError, Is.Not.Null);
                Assert.That(
                    client.LastError.Message,
                    Does.Contain("RegisterConsumer"));
            });

            client.Dispose();
            provider.Dispose();
        }

        [Test]
        public void Handle_SwitchFile_Reloads_Requested_File_And_Keeps_Using_It()
        {
            var bus = new RecordingModMessageBus();
            var openedFiles = new List<string>();
            var endpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });

            endpoints["OpenConfig"] = new Func<string, Guid, string, int, string, object, object>(
                delegate(string consumerId, Guid registrationId, string configKey, int location, string file, object defaults)
                {
                    openedFiles.Add(file);
                    return defaults;
                });

            var provider = CreateProvider(bus, new SemanticVersion(2, 0, 0), endpoints);
            provider.Start();

            var client = CreateClient(bus, (location, file) => null, (location, file, value) => { });
            client.Start();

            var definition = new ConfigDefinition<ConfigDocument>("Settings", "settings.toml", () => new ConfigDocument(), value => value, document => document);
            ConfigHandle<ConfigDocument> handle = client.OpenHandle(definition, ConfigLocation.Local);

            handle.SwitchFile("alternate.toml");
            handle.Reload();

            Assert.Multiple(() =>
            {
                Assert.That(handle.CurrentFile, Is.EqualTo("alternate.toml"));
                Assert.That(openedFiles, Is.EqualTo(new[] { "settings.toml", "alternate.toml", "alternate.toml" }));
            });

            client.Dispose();
            provider.Dispose();
        }

        [Test]
        public void Handle_SwitchFile_Failure_Keeps_Previous_File_And_Value()
        {
            var bus = new RecordingModMessageBus();
            var openedFiles = new List<string>();
            var endpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });

            endpoints["OpenConfig"] = new Func<string, Guid, string, int, string, object, object>(
                delegate(string consumerId, Guid registrationId, string configKey, int location, string file, object defaults)
                {
                    openedFiles.Add(file);

                    if (file == "alternate.toml")
                        throw new InvalidOperationException("Synthetic open failure.");

                    return defaults;
                });

            var provider = CreateProvider(bus, new SemanticVersion(2, 0, 0), endpoints);
            provider.Start();

            var client = CreateClient(bus, (location, file) => null, (location, file, value) => { });
            client.Start();

            var definition = new ConfigDefinition<ConfigDocument>("Settings", "settings.toml", () => new ConfigDocument(), value => value, document => document);
            ConfigHandle<ConfigDocument> handle = client.OpenHandle(definition, ConfigLocation.Local);
            ConfigDocument previousValue = handle.Value;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => handle.SwitchFile("alternate.toml"));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Is.EqualTo("Synthetic open failure."));
                Assert.That(handle.CurrentFile, Is.EqualTo("settings.toml"));
                Assert.That(handle.Value, Is.SameAs(previousValue));
                Assert.That(openedFiles, Is.EqualTo(new[] { "settings.toml", "alternate.toml" }));
            });

            client.Dispose();
            provider.Dispose();
        }

        [Test]
        public void Typed_Open_Delegate_Failures_Stop_At_Expected_Provider_Boundary()
        {
            var bus = new RecordingModMessageBus();
            var openCount = 0;
            var endpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });

            endpoints["OpenConfig"] = new Func<string, Guid, string, int, string, object, object>(
                delegate(string consumerId, Guid registrationId, string configKey, int location, string file, object defaults)
                {
                    openCount++;
                    return defaults;
                });

            var provider = CreateProvider(bus, new SemanticVersion(2, 0, 0), endpoints);
            provider.Start();

            var client = CreateClient(bus, (location, file) => null, (location, file, value) => { });
            client.Start();

            var throwingDefaults = new ConfigDefinition<ConfigDocument>("Settings", "settings.toml", () => { throw new InvalidOperationException("Synthetic default failure."); }, value => value, document => document);
            var nullDefaults = new ConfigDefinition<ConfigDocument>("Settings", "settings.toml", () => null, value => value, document => document);
            var throwingSerializer = new ConfigDefinition<ConfigDocument>("Settings", "settings.toml", () => new ConfigDocument(), value => { throw new InvalidOperationException("Synthetic serializer failure."); }, document => document);
            var nullSerializer = new ConfigDefinition<ConfigDocument>("Settings", "settings.toml", () => new ConfigDocument(), value => null, document => document);
            var throwingDeserializer = new ConfigDefinition<ConfigDocument>("Settings", "settings.toml", () => new ConfigDocument(), value => value, document => { throw new InvalidOperationException("Synthetic deserializer failure."); });
            var nullDeserializer = new ConfigDefinition<ConfigDocument>("Settings", "settings.toml", () => new ConfigDocument(), value => value, document => null);

            InvalidOperationException defaultException = Assert.Throws<InvalidOperationException>(() => client.Open(throwingDefaults, ConfigLocation.Local));
            InvalidOperationException nullDefaultException = Assert.Throws<InvalidOperationException>(() => client.Open(nullDefaults, ConfigLocation.Local));
            InvalidOperationException serializerException = Assert.Throws<InvalidOperationException>(() => client.Open(throwingSerializer, ConfigLocation.Local));
            InvalidOperationException nullSerializerException = Assert.Throws<InvalidOperationException>(() => client.Open(nullSerializer, ConfigLocation.Local));

            Assert.Multiple(() =>
            {
                Assert.That(defaultException.Message, Is.EqualTo("Synthetic default failure."));
                Assert.That(nullDefaultException.Message, Does.Contain("default factory returned null"));
                Assert.That(serializerException.Message, Is.EqualTo("Synthetic serializer failure."));
                Assert.That(nullSerializerException.Message, Does.Contain("serializer returned null"));
                Assert.That(openCount, Is.EqualTo(0));
            });

            InvalidOperationException deserializerException = Assert.Throws<InvalidOperationException>(() => client.Open(throwingDeserializer, ConfigLocation.Local));
            Assert.That(openCount, Is.EqualTo(1));

            InvalidOperationException nullDeserializerException = Assert.Throws<InvalidOperationException>(() => client.Open(nullDeserializer, ConfigLocation.Local));

            Assert.Multiple(() =>
            {
                Assert.That(deserializerException.Message, Is.EqualTo("Synthetic deserializer failure."));
                Assert.That(nullDeserializerException.Message, Does.Contain("deserializer returned null"));
                Assert.That(openCount, Is.EqualTo(2));
            });

            client.Dispose();
            provider.Dispose();
        }

        [Test]
        public void Typed_Save_Delegate_Failures_Stop_At_Expected_Provider_Boundary()
        {
            var bus = new RecordingModMessageBus();
            var saveCount = 0;
            var endpoints = ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; });

            endpoints["SaveConfig"] = new Func<string, Guid, string, int, string, object, object, object>(
                delegate(string consumerId, Guid registrationId, string configKey, int location, string file, object defaults, object encodedPlayerValues)
                {
                    saveCount++;
                    return encodedPlayerValues;
                });

            var provider = CreateProvider(bus, new SemanticVersion(2, 0, 0), endpoints);
            provider.Start();

            var client = CreateClient(bus, (location, file) => null, (location, file, value) => { });
            client.Start();
            var playerValues = new ConfigDocument();

            var throwingDefaults = new ConfigDefinition<ConfigDocument>("Settings", "settings.toml", () => { throw new InvalidOperationException("Synthetic default failure."); }, value => value, document => document);
            var nullDefaults = new ConfigDefinition<ConfigDocument>("Settings", "settings.toml", () => null, value => value, document => document);
            var throwingSerializer = new ConfigDefinition<ConfigDocument>("Settings", "settings.toml", () => new ConfigDocument(), value => { throw new InvalidOperationException("Synthetic serializer failure."); }, document => document);
            var nullSerializer = new ConfigDefinition<ConfigDocument>("Settings", "settings.toml", () => new ConfigDocument(), value => null, document => document);
            var playerSerializeCount = 0;
            var throwingPlayerSerializer = new ConfigDefinition<ConfigDocument>("Settings", "settings.toml", () => new ConfigDocument(), value => { playerSerializeCount++; if (playerSerializeCount == 2) throw new InvalidOperationException("Synthetic player serializer failure."); return value; }, document => document);
            var throwingDeserializer = new ConfigDefinition<ConfigDocument>("Settings", "settings.toml", () => new ConfigDocument(), value => value, document => { throw new InvalidOperationException("Synthetic deserializer failure."); });
            var nullDeserializer = new ConfigDefinition<ConfigDocument>("Settings", "settings.toml", () => new ConfigDocument(), value => value, document => null);

            InvalidOperationException defaultException = Assert.Throws<InvalidOperationException>(() => client.Save(throwingDefaults, ConfigLocation.Local, playerValues));
            InvalidOperationException nullDefaultException = Assert.Throws<InvalidOperationException>(() => client.Save(nullDefaults, ConfigLocation.Local, playerValues));
            InvalidOperationException serializerException = Assert.Throws<InvalidOperationException>(() => client.Save(throwingSerializer, ConfigLocation.Local, playerValues));
            InvalidOperationException nullSerializerException = Assert.Throws<InvalidOperationException>(() => client.Save(nullSerializer, ConfigLocation.Local, playerValues));
            InvalidOperationException playerSerializerException = Assert.Throws<InvalidOperationException>(() => client.Save(throwingPlayerSerializer, ConfigLocation.Local, playerValues));

            Assert.Multiple(() =>
            {
                Assert.That(defaultException.Message, Is.EqualTo("Synthetic default failure."));
                Assert.That(nullDefaultException.Message, Does.Contain("default factory returned null"));
                Assert.That(serializerException.Message, Is.EqualTo("Synthetic serializer failure."));
                Assert.That(nullSerializerException.Message, Does.Contain("serializer returned null"));
                Assert.That(playerSerializerException.Message, Is.EqualTo("Synthetic player serializer failure."));
                Assert.That(playerSerializeCount, Is.EqualTo(2));
                Assert.That(saveCount, Is.EqualTo(0));
            });

            InvalidOperationException deserializerException = Assert.Throws<InvalidOperationException>(() => client.Save(throwingDeserializer, ConfigLocation.Local, playerValues));
            Assert.That(saveCount, Is.EqualTo(1));

            InvalidOperationException nullDeserializerException = Assert.Throws<InvalidOperationException>(() => client.Save(nullDeserializer, ConfigLocation.Local, playerValues));

            Assert.Multiple(() =>
            {
                Assert.That(deserializerException.Message, Is.EqualTo("Synthetic deserializer failure."));
                Assert.That(nullDeserializerException.Message, Does.Contain("deserializer returned null"));
                Assert.That(saveCount, Is.EqualTo(2));
            });

            client.Dispose();
            provider.Dispose();
        }

        [Test]
        public void World_Endpoints_Are_Optional_And_Surface_Asynchronous_Response()
        {
            var bus = new RecordingModMessageBus();
            Action<IDictionary<string, object>> worldCallback = null;
            Guid worldRegistrationId = Guid.Empty;
            var worldUnregisterCount = 0;
            var opened = false;

            IDictionary<string, Delegate> endpoints = ValidEndpoints(
                delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write)
                {
                    return delegate { };
                });

            endpoints["RegisterWorldConfig"] = new Func<string, Guid, Action<IDictionary<string, object>>, Action>(
                delegate(string consumerId, Guid registrationId, Action<IDictionary<string, object>> callback)
                {
                    worldRegistrationId = registrationId;
                    worldCallback = callback;
                    return delegate { worldUnregisterCount++; };
                });

            endpoints["OpenWorldConfig"] = new Action<string, Guid, string, string, object>(
                delegate(string consumerId, Guid registrationId, string configKey, string file, object defaults)
                {
                    opened = true;
                    Assert.That(registrationId, Is.EqualTo(worldRegistrationId));
                    Assert.That(configKey, Is.EqualTo("Settings"));
                    Assert.That(file, Is.EqualTo("settings.toml"));

                    worldCallback(
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            { "ConfigKey", "Settings" },
                            { "RequestId", 17UL },
                            { "Operation", "Open" },
                            { "TriggeredBy", 222UL },
                            { "IsApplied", false },
                            { "IsStale", false },
                            { "Error", null },
                            { "ServerIteration", 4UL },
                            { "CurrentFile", "settings.toml" },
                            { "Document", ConfigDocumentWireCodec.Encode(new ConfigDocument()) },
                        });
                });

            endpoints["SaveWorldConfig"] = new Action<string, Guid, string, object>(
                delegate(string consumerId, Guid registrationId, string configKey, object document) { });

            var provider = CreateProvider(bus, new SemanticVersion(2, 1, 0), endpoints);
            provider.Start();

            var client = CreateClient(bus, (location, file) => null, (location, file, content) => { });
            WorldConfigResponse observed = null;
            client.WorldConfigResponseReceived += delegate(WorldConfigResponse response) { observed = response; };
            client.Start();

            Assert.Multiple(() =>
            {
                Assert.That(client.IsConnected, Is.True);
                Assert.That(client.SupportsWorldConfigs, Is.True);
                Assert.That(worldRegistrationId, Is.Not.EqualTo(Guid.Empty));
                Assert.That(worldCallback, Is.Not.Null);
            });

            client.OpenWorld("Settings", "settings.toml", new ConfigDocument());

            Assert.Multiple(() =>
            {
                Assert.That(opened, Is.True);
                Assert.That(observed, Is.Not.Null);
                Assert.That(observed.ConfigKey, Is.EqualTo("Settings"));
                Assert.That(observed.RequestId, Is.EqualTo(17UL));
                Assert.That(observed.Operation, Is.EqualTo(WorldConfigOperation.Open));
                Assert.That(observed.TriggeredBy, Is.EqualTo(222UL));
                Assert.That(observed.IsApplied, Is.False);
                Assert.That(observed.IsStale, Is.False);
                Assert.That(observed.IsError, Is.False);
                Assert.That(observed.HasSnapshot, Is.True);
                Assert.That(observed.ServerIteration, Is.EqualTo(4UL));
                Assert.That(observed.CurrentFile, Is.EqualTo("settings.toml"));
                Assert.That(observed.Document, Is.Not.Null);
            });

            client.Dispose();
            Assert.That(worldUnregisterCount, Is.EqualTo(1));
            provider.Dispose();

            var legacyBus = new RecordingModMessageBus();
            var legacyProvider = CreateProvider(
                legacyBus,
                new SemanticVersion(2, 0, 0),
                ValidEndpoints(delegate(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write) { return delegate { }; }));

            legacyProvider.Start();

            var legacyClient = CreateClient(legacyBus, (location, file) => null, (location, file, content) => { });
            legacyClient.Start();

            Assert.Multiple(() =>
            {
                Assert.That(legacyClient.IsConnected, Is.True);
                Assert.That(legacyClient.SupportsWorldConfigs, Is.False);
                Assert.Throws<InvalidOperationException>(() => legacyClient.OpenWorld("Settings", "settings.toml", new ConfigDocument()));
            });

            legacyClient.Dispose();
            legacyProvider.Dispose();
        }

        [Test]
        public void Constructor_Rejects_Invalid_Consumer_Identity_And_Callbacks()
        {
            var bus = new RecordingModMessageBus();
            var version = new SemanticVersion(1, 2, 3);
            Func<int, string, string> read = (location, file) => null;
            Action<int, string, string> write = (location, file, content) => { };

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(
                    () => new ConfigApiClient(
                        null,
                        "Example.Mod",
                        "Example Mod",
                        version,
                        true,
                        "Config",
                        read,
                        write));

                Assert.Throws<ArgumentException>(
                    () => new ConfigApiClient(
                        bus,
                        " ",
                        "Example Mod",
                        version,
                        true,
                        "Config",
                        read,
                        write));

                Assert.Throws<ArgumentException>(
                    () => new ConfigApiClient(
                        bus,
                        "Example.Mod",
                        " ",
                        version,
                        true,
                        "Config",
                        read,
                        write));

                Assert.Throws<ArgumentNullException>(
                    () => new ConfigApiClient(
                        bus,
                        "Example.Mod",
                        "Example Mod",
                        null,
                        true,
                        "Config",
                        read,
                        write));

                Assert.Throws<ArgumentNullException>(
                    () => new ConfigApiClient(
                        bus,
                        "Example.Mod",
                        "Example Mod",
                        version,
                        true,
                        "Config",
                        null,
                        write));

                Assert.Throws<ArgumentNullException>(
                    () => new ConfigApiClient(
                        bus,
                        "Example.Mod",
                        "Example Mod",
                        version,
                        true,
                        "Config",
                        read,
                        null));
            });
        }

        private static ConfigApiClient CreateClient(
            IModMessageBus bus,
            Func<int, string, string> read,
            Action<int, string, string> write)
        {
            return new ConfigApiClient(
                bus,
                "Example.Mod",
                "Example Mod",
                new SemanticVersion(2, 3, 4),
                true,
                "Uses ConfigAPI for configuration.",
                read,
                write);
        }

        private static ApiDiscoveryProvider CreateProvider(
            IModMessageBus bus,
            SemanticVersion apiVersion,
            IDictionary<string, Delegate> endpoints)
        {
            return new ApiDiscoveryProvider(
                bus,
                new ApiModIdentity(
                    "MarcoZechner.ConfigAPI",
                    "ConfigAPI",
                    new SemanticVersion(0, 1, 0)),
                new ApiDescriptor(
                    "MarcoZechner.ConfigAPI",
                    apiVersion),
                endpoints);
        }

        private static IDictionary<string, Delegate> ValidEndpoints(
            Func<
                string,
                Guid,
                Func<int, string, string>,
                Action<int, string, string>,
                Action> registerConsumer)
        {
            return new Dictionary<string, Delegate>(StringComparer.Ordinal)
            {
                {
                    "RegisterConsumer",
                    registerConsumer
                },
                {
                    "OpenConfig",
                    new Func<
                        string,
                        Guid,
                        string,
                        int,
                        string,
                        object,
                        object>(
                        delegate(
                            string consumerId,
                            Guid registrationId,
                            string configKey,
                            int location,
                            string file,
                            object defaults)
                        {
                            return defaults;
                        })
                },
                {
                    "SaveConfig",
                    new Func<
                        string,
                        Guid,
                        string,
                        int,
                        string,
                        object,
                        object,
                        object>(
                        delegate(
                            string consumerId,
                            Guid registrationId,
                            string configKey,
                            int location,
                            string file,
                            object defaults,
                            object playerValues)
                        {
                            return playerValues;
                        })
                },
                {
                    "LoadAndSwitchConfig",
                    new Func<
                        string,
                        Guid,
                        string,
                        int,
                        string,
                        string,
                        object,
                        object>(
                        delegate(
                            string consumerId,
                            Guid registrationId,
                            string configKey,
                            int location,
                            string currentFile,
                            string targetFile,
                            object defaults)
                        {
                            return defaults;
                        })
                }
            };
        }

        private sealed class RecordingModMessageBus : IModMessageBus
        {
            private readonly Dictionary<long, List<Action<object>>> _handlers =
                new Dictionary<long, List<Action<object>>>();

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
                List<Action<object>> handlers;

                if (!_handlers.TryGetValue(channelId, out handlers))
                    return;

                Action<object>[] snapshot = handlers.ToArray();

                for (var index = 0; index < snapshot.Length; index++)
                    snapshot[index](payload);
            }
        }
    }
}
