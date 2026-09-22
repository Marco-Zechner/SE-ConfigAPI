using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Domain;
using MarcoZechner.ConfigAPI.Persistence;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.Logging;
using Mz.SemanticVersioning;

namespace MarcoZechner.ConfigAPI.Api
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
        public const string ApplyWorldConfigEndpoint = "ApplyWorldConfig";
        public const string SaveWorldConfigEndpoint = "SaveWorldConfig";
        public const string ReloadWorldConfigEndpoint = "ReloadWorldConfig";
        public const string LoadWorldConfigEndpoint = "LoadWorldConfig";
        public const string SaveAsWorldConfigEndpoint = "SaveAsWorldConfig";
        public const string ListWorldConfigVariantsEndpoint = "ListWorldConfigVariants";

        private readonly Logger _logger;
        private readonly ApiDiscoveryProvider _provider;
        private readonly ConfigApiPersistenceService _persistence;
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
            _persistence = new ConfigApiPersistenceService(registry, clock, logger);
            _worldBridge = new WorldConfigProviderBridge(registry, logger);

            Func<string, Guid, Func<int, string, bool>, Func<int, string, string>, Action<int, string, string>, Func<int, string[]>, Action> registerConsumer = (consumerId, registrationId, exists, read, write, listKnown) =>
            {
                Log(LogLevel.Debug, $"Registering consumer '{consumerId}' with registration {registrationId}.");
                registry.Register(consumerId, registrationId, exists, read, write, listKnown);

                return () =>
                {
                    bool removed = registry.Unregister(consumerId, registrationId);
                    Log(removed ? LogLevel.Debug : LogLevel.Warning, $"Consumer unregister '{consumerId}' registration {registrationId}: removed={removed}.");
                };
            };

            Func<string, Guid, string, int, string, object, object> openConfig = _persistence.Open;
            Func<string, Guid, string, int, string, object, object, object> saveConfig = _persistence.Save;
            Func<string, Guid, Action<IDictionary<string, object>>, Action> registerWorldConfig = _worldBridge.Register;
            Action<string, Guid, string, object> openWorldConfig = _worldBridge.Open;
            Action<string, Guid, string, object> applyWorldConfig = _worldBridge.Apply;
            Action<string, Guid, string> saveWorldConfig = _worldBridge.Save;
            Action<string, Guid, string> reloadWorldConfig = _worldBridge.Reload;
            Action<string, Guid, string, string> loadWorldConfig = _worldBridge.Load;
            Action<string, Guid, string, string> saveAsWorldConfig = _worldBridge.SaveAs;
            Action<string, Guid, string> listWorldConfigVariants = _worldBridge.ListVariants;

            var endpoints = new Dictionary<string, Delegate>(StringComparer.Ordinal)
            {
                { RegisterConsumerEndpoint, registerConsumer },
                { OpenConfigEndpoint, openConfig },
                { SaveConfigEndpoint, saveConfig },
                { RegisterWorldConfigEndpoint, registerWorldConfig },
                { OpenWorldConfigEndpoint, openWorldConfig },
                { ApplyWorldConfigEndpoint, applyWorldConfig },
                { SaveWorldConfigEndpoint, saveWorldConfig },
                { ReloadWorldConfigEndpoint, reloadWorldConfig },
                { LoadWorldConfigEndpoint, loadWorldConfig },
                { SaveAsWorldConfigEndpoint, saveAsWorldConfig },
                { ListWorldConfigVariantsEndpoint, listWorldConfigVariants },
            };

            _provider = new ApiDiscoveryProvider(messageBus, new ApiModIdentity(ApiId, "ConfigAPI", modVersion), new ApiDescriptor(ApiId, new SemanticVersion(2, 3, 0)), endpoints);
        }

        public bool IsStarted => _provider.IsStarted;

        internal ConfigDocument OpenInternal(string consumerId, string configKey, ConfigLocation location, string file, ConfigDocument currentDefaults)
            => _persistence.OpenInternal(consumerId, configKey, location, file, currentDefaults);

        internal ConfigDocument SaveInternal(string consumerId, string configKey, ConfigLocation location, string file, ConfigDocument currentDefaults, ConfigDocument playerValues)
            => _persistence.SaveInternal(consumerId, configKey, location, file, currentDefaults, playerValues);

        internal Action RegisterWorldInternal(string consumerId, Guid registrationId, Action<IDictionary<string, object>> responseCallback)
            => _worldBridge.RegisterInternal(consumerId, registrationId, responseCallback);

        internal void OpenWorldInternal(string consumerId, Guid registrationId, string configKey, ConfigDocument defaults)
            => _worldBridge.Open(consumerId, registrationId, configKey, ConfigDocumentWireCodec.Encode(defaults));

        internal void ApplyWorldInternal(string consumerId, Guid registrationId, string configKey, ConfigDocument document)
            => _worldBridge.Apply(consumerId, registrationId, configKey, ConfigDocumentWireCodec.Encode(document));

        internal void SaveWorldInternal(string consumerId, Guid registrationId, string configKey)
            => _worldBridge.Save(consumerId, registrationId, configKey);

        public void Dispose()
        {
            Log(LogLevel.Debug, "Disposing ConfigAPI discovery provider.");
            _provider.Dispose();
            _worldBridge.Dispose();
        }

        public void AttachWorldRuntime(WorldConfigNetworkRuntime runtime) => _worldBridge.AttachRuntime(runtime);

        public void DetachWorldRuntime() => _worldBridge.DetachRuntime();

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
