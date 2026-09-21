using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Persistence;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.Logging;
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
        public const string RegisterWorldConfigEndpoint = "RegisterWorldConfig";
        public const string OpenWorldConfigEndpoint = "OpenWorldConfig";
        public const string SaveWorldConfigEndpoint = "SaveWorldConfig";
        public const string ReloadWorldConfigEndpoint = "ReloadWorldConfig";
        public const string LoadAndSwitchWorldConfigEndpoint = "LoadAndSwitchWorldConfig";
        public const string SaveAndSwitchWorldConfigEndpoint = "SaveAndSwitchWorldConfig";
        public const string ExportWorldConfigEndpoint = "ExportWorldConfig";

        private readonly Logger _logger;
        private readonly ApiDiscoveryProvider _provider;
        private readonly WorldConfigProviderBridge _worldBridge;

        public ConfigApiProvider(IModMessageBus messageBus, ConfigConsumerRegistrationRegistry registry, SemanticVersion modVersion)
            : this(messageBus, registry, new SystemConfigClock(), modVersion, null) { }

        public ConfigApiProvider(IModMessageBus messageBus, ConfigConsumerRegistrationRegistry registry, SemanticVersion modVersion, Logger logger)
            : this(messageBus, registry, new SystemConfigClock(), modVersion, logger) { }

        public ConfigApiProvider(IModMessageBus messageBus, ConfigConsumerRegistrationRegistry registry, IConfigClock clock, SemanticVersion modVersion)
            : this(messageBus, registry, clock, modVersion, null) { }

        public ConfigApiProvider(IModMessageBus messageBus, ConfigConsumerRegistrationRegistry registry, IConfigClock clock, SemanticVersion modVersion, Logger logger)
        {
            if (messageBus == null)
                throw new ArgumentNullException(nameof(messageBus));

            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            if (clock == null)
                throw new ArgumentNullException(nameof(clock));

            if (modVersion == null)
                throw new ArgumentNullException(nameof(modVersion));

            _logger = logger;

            var persistence = new ConfigApiPersistenceService(registry, clock, logger);
            _worldBridge = new WorldConfigProviderBridge(registry, logger);

            Func<string, Guid, Func<int, string, string>, Action<int, string, string>, Action> registerConsumer = (consumerId, registrationId, read, write) =>
            {
                Log(LogLevel.Debug, $"Registering consumer '{consumerId}' with registration {registrationId}.");
                registry.Register(consumerId, registrationId, read, write);

                return () =>
                {
                    bool removed = registry.Unregister(consumerId, registrationId);
                    Log(removed ? LogLevel.Debug : LogLevel.Warning, $"Consumer unregister '{consumerId}' registration {registrationId}: removed={removed}.");
                };
            };

            Func<string, Guid, string, int, string, object, object> openConfig = persistence.Open;
            Func<string, Guid, string, int, string, object, object, object> saveConfig = persistence.Save;
            Func<string, Guid, Action<IDictionary<string, object>>, Action> registerWorldConfig = _worldBridge.Register;
            Action<string, Guid, string, string, object> openWorldConfig = _worldBridge.Open;
            Action<string, Guid, string, object> saveWorldConfig = _worldBridge.Save;
            Action<string, Guid, string> reloadWorldConfig = _worldBridge.Reload;
            Action<string, Guid, string, string> loadAndSwitchWorldConfig = _worldBridge.LoadAndSwitch;
            Action<string, Guid, string, string, object> saveAndSwitchWorldConfig = _worldBridge.SaveAndSwitch;
            Action<string, Guid, string, string, object, bool> exportWorldConfig = _worldBridge.Export;

            var endpoints = new Dictionary<string, Delegate>(StringComparer.Ordinal)
            {
                { RegisterConsumerEndpoint, registerConsumer },
                { OpenConfigEndpoint, openConfig },
                { SaveConfigEndpoint, saveConfig },
                { RegisterWorldConfigEndpoint, registerWorldConfig },
                { OpenWorldConfigEndpoint, openWorldConfig },
                { SaveWorldConfigEndpoint, saveWorldConfig },
                { ReloadWorldConfigEndpoint, reloadWorldConfig },
                { LoadAndSwitchWorldConfigEndpoint, loadAndSwitchWorldConfig },
                { SaveAndSwitchWorldConfigEndpoint, saveAndSwitchWorldConfig },
                { ExportWorldConfigEndpoint, exportWorldConfig },
            };

            _provider = new ApiDiscoveryProvider(
                messageBus,
                new ApiModIdentity(ApiId, "ConfigAPI", modVersion),
                new ApiDescriptor(ApiId, new SemanticVersion(2, 2, 0)),
                endpoints);
        }

        public bool IsStarted => _provider.IsStarted;

        public void Dispose()
        {
            Log(LogLevel.Debug, "Disposing ConfigAPI discovery provider.");
            _provider.Dispose();
            _worldBridge.Dispose();
        }

        public void AttachWorldRuntime(WorldConfigNetworkRuntime runtime)
        {
            _worldBridge.AttachRuntime(runtime);
        }

        public void DetachWorldRuntime()
        {
            _worldBridge.DetachRuntime();
        }

        public void Start()
        {
            Log(LogLevel.Debug, "Starting ConfigAPI discovery provider.");
            _provider.Start();
            Log(LogLevel.Information, "ConfigAPI discovery provider started.");
        }

        public void Announce()
        {
            Log(LogLevel.Trace, "Announcing ConfigAPI provider.");
            _provider.Announce();
        }

        public void Stop()
        {
            Log(LogLevel.Debug, "Stopping ConfigAPI discovery provider.");
            _provider.Stop();
            Log(LogLevel.Information, "ConfigAPI discovery provider stopped.");
        }

        private void Log(LogLevel level, string message, Exception exception = null)
        {
            if (_logger == null)
                return;

            try
            {
                _logger.Write(level, message, exception);
            }
            catch
            {
            }
        }

    }
}