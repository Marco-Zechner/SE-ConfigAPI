using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Api;
using MarcoZechner.ConfigAPI.V2.Domain;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.SemanticVersioning;
using NUnit.Framework;

namespace MarcoZechner.ConfigAPI.Tests.V2.Api
{
    [TestFixture]
    public sealed class ConfigApiProviderTests
    {
        [Test]
        public void Start_Publishes_Exact_RegisterConsumer_Endpoint()
        {
            var bus = new RecordingModMessageBus();
            var registry = new ConfigConsumerRegistrationRegistry();
            var modVersion = new SemanticVersion(0, 0, 0);
            var provider = new ConfigApiProvider(bus, registry, modVersion);

            provider.Start();

            Assert.That(bus.RegistrationCount, Is.EqualTo(1));
            Assert.That(bus.LastChannelId, Is.EqualTo(ApiProtocolChannels.Discovery));
            Assert.That(bus.SentPayloads.Count, Is.EqualTo(1));

            ApiAnnouncement announcement;

            Assert.That(
                ApiDiscoveryWireProtocol.TryParseAnnouncement(
                    bus.SentPayloads[0],
                    out announcement),
                Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(announcement.Provider.Id, Is.EqualTo("MarcoZechner.ConfigAPI"));
                Assert.That(announcement.Provider.DisplayName, Is.EqualTo("ConfigAPI"));
                Assert.That(announcement.Provider.Version.ToString(), Is.EqualTo("0.0.0"));
                Assert.That(announcement.Descriptor.ApiId, Is.EqualTo("MarcoZechner.ConfigAPI"));
                Assert.That(announcement.Descriptor.Version.ToString(), Is.EqualTo("2.0.0"));
                Assert.That(announcement.Endpoints.Count, Is.EqualTo(3));
            });

            Delegate endpoint =
                announcement.Endpoints[ConfigApiProvider.RegisterConsumerEndpoint];

            var register =
                endpoint as Func<
                    string,
                    Guid,
                    Func<int, string, string>,
                    Action<int, string, string>,
                    Action>;

            Assert.That(register, Is.Not.Null);

            var registrationId = Guid.NewGuid();
            var written = string.Empty;

            Action unregister =
                register(
                    "Example.Mod",
                    registrationId,
                    (location, file) => location + "|" + file,
                    (location, file, content) =>
                        written = location + "|" + file + "|" + content);

            var storage = registry.GetStorage("Example.Mod", registrationId);

            Assert.That(
                storage.Read(ConfigLocation.Local, "config.toml"),
                Is.EqualTo("0|config.toml"));

            storage.Write(ConfigLocation.World, "world.toml", "content");

            Assert.That(written, Is.EqualTo("2|world.toml|content"));

            unregister();

            Assert.Throws<InvalidOperationException>(
                () => registry.GetStorage("Example.Mod", registrationId));

            provider.Dispose();

            Assert.That(bus.UnregistrationCount, Is.EqualTo(1));
        }

        [Test]
        public void Start_And_Dispose_Are_Idempotent()
        {
            var bus = new RecordingModMessageBus();
            var provider =
                new ConfigApiProvider(
                    bus,
                    new ConfigConsumerRegistrationRegistry(),
                    new SemanticVersion(1, 2, 3));

            provider.Start();
            provider.Start();

            Assert.That(bus.RegistrationCount, Is.EqualTo(1));

            provider.Dispose();
            provider.Dispose();

            Assert.That(bus.UnregistrationCount, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_Rejects_Null_Dependencies()
        {
            var bus = new RecordingModMessageBus();
            var registry = new ConfigConsumerRegistrationRegistry();
            var version = new SemanticVersion(1, 0, 0);

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentNullException>(
                    () => new ConfigApiProvider(null, registry, version));

                Assert.Throws<ArgumentNullException>(
                    () => new ConfigApiProvider(bus, null, version));

                Assert.Throws<ArgumentNullException>(
                    () => new ConfigApiProvider(bus, registry, null));
            });
        }

        private sealed class RecordingModMessageBus : IModMessageBus
        {
            private readonly Dictionary<long, List<Action<object>>> _handlers =
                new Dictionary<long, List<Action<object>>>();

            public int RegistrationCount { get; private set; }
            public int UnregistrationCount { get; private set; }
            public long LastChannelId { get; private set; }
            public List<object> SentPayloads { get; } = new List<object>();

            public void RegisterHandler(long channelId, Action<object> handler)
            {
                List<Action<object>> handlers;

                if (!_handlers.TryGetValue(channelId, out handlers))
                {
                    handlers = new List<Action<object>>();
                    _handlers.Add(channelId, handlers);
                }

                handlers.Add(handler);
                RegistrationCount++;
            }

            public void UnregisterHandler(long channelId, Action<object> handler)
            {
                List<Action<object>> handlers;

                if (_handlers.TryGetValue(channelId, out handlers)
                    && handlers.Remove(handler))
                {
                    UnregistrationCount++;
                }
            }

            public void Send(long channelId, object payload)
            {
                LastChannelId = channelId;
                SentPayloads.Add(payload);

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
