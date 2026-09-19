using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Persistence;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.SemanticVersioning;

namespace MarcoZechner.ConfigAPI.V2.Api
{
    public sealed class ConfigApiProvider : IDisposable
    {
        public const long DiscoveryChannelId = ApiProtocolChannels.Discovery;
        public const string ApiId = "MarcoZechner.ConfigAPI";
        public const string RegisterConsumerEndpoint = "RegisterConsumer";
        public const string OpenConfigEndpoint = "OpenConfig";
        public const string SaveConfigEndpoint = "SaveConfig";

        private readonly ApiDiscoveryProvider _provider;

        public ConfigApiProvider(IModMessageBus messageBus, ConfigConsumerRegistrationRegistry registry, SemanticVersion modVersion)
            : this(messageBus, registry, new SystemConfigClock(), modVersion) { }

        public ConfigApiProvider(IModMessageBus messageBus, ConfigConsumerRegistrationRegistry registry, 
                                 IConfigClock clock, SemanticVersion modVersion)
        {
            if (messageBus == null)
                throw new ArgumentNullException(nameof(messageBus));

            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            if (clock == null)
                throw new ArgumentNullException(nameof(clock));

            if (modVersion == null)
                throw new ArgumentNullException(nameof(modVersion));

            var persistence = new ConfigApiPersistenceService(registry, clock);

            Func<string, Guid, Func<int, string, string>, Action<int, string, string>,
                Action> registerConsumer = (consumerId, registrationId, read, write) =>
            {
                registry.Register(consumerId, registrationId, read, write);

                return () => registry.Unregister(consumerId, registrationId);
            };

            Func<string, Guid, string, int, string, object, object> openConfig = persistence.Open;

            Func<string, Guid, string, int, string, object, object, object> saveConfig = persistence.Save;

            var endpoints = new Dictionary<string, Delegate>(StringComparer.Ordinal)
            {
                { RegisterConsumerEndpoint, registerConsumer },
                { OpenConfigEndpoint, openConfig },
                { SaveConfigEndpoint, saveConfig },
            };

            _provider = new ApiDiscoveryProvider(
                messageBus,
                new ApiModIdentity(ApiId, "ConfigAPI", modVersion),
                new ApiDescriptor(ApiId, new SemanticVersion(2, 0, 0)),
                endpoints
            );
        }

        public bool IsStarted => _provider.IsStarted;

        public void Dispose() => _provider.Dispose();

        public void Start() => _provider.Start();

        public void Announce() => _provider.Announce();

        public void Stop() => _provider.Stop();

        private sealed class SystemConfigClock : IConfigClock
        {
            public DateTime UtcNow => DateTime.UtcNow;
        }
    }
}
