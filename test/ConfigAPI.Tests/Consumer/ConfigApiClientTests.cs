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
