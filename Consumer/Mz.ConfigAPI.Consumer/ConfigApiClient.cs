using System;
using System.Collections.Generic;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.SemanticVersioning;

namespace Mz.ConfigApi
{
    public sealed class ConfigApiClient : IDisposable
    {
        public const string ProviderApiId = "MarcoZechner.ConfigAPI";
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
        private readonly ApiDiscoveryConsumer _consumer;

        private readonly string _consumerId;
        private readonly Func<int, string, bool> _exists;
        private readonly Func<int, string, string> _read;
        private readonly Action<int, string, string> _write;
        private readonly Func<int, string[]> _listKnown;
        private bool _isDisposed;
        private Exception _lastError;
        private Func<string, Guid, string, int, string, object, object> _openConfig;

        private Action _providerUnregister;
        private Guid _registrationId;
        private Func<string, Guid, string, int, string, object, object, object> _saveConfig;
        private Action<string, Guid, string, object> _openWorldConfig;
        private Action<string, Guid, string, object> _applyWorldConfig;
        private Action<string, Guid, string> _saveWorldConfig;
        private Action<string, Guid, string> _reloadWorldConfig;
        private Action<string, Guid, string, string> _loadWorldConfig;
        private Action<string, Guid, string, string> _saveAsWorldConfig;
        private Action<string, Guid, string> _listWorldConfigVariants;
        private Action _providerWorldUnregister;

        public ConfigApiClient(
            IModMessageBus messageBus, string consumerId, string consumerDisplayName, SemanticVersion consumerModVersion,
            bool isRequired, string featureDescription, Func<int, string, bool> exists, Func<int, string, string> read,
            Action<int, string, string> write, Func<int, string[]> listKnown)
        {
            if (messageBus == null)
                throw new ArgumentNullException(nameof(messageBus));

            if (string.IsNullOrWhiteSpace(consumerId))
                throw new ArgumentException("A stable consumer mod identifier is required.", nameof(consumerId));

            if (string.IsNullOrWhiteSpace(consumerDisplayName))
                throw new ArgumentException("A consumer display name is required.", nameof(consumerDisplayName));

            if (consumerModVersion == null)
                throw new ArgumentNullException(nameof(consumerModVersion));

            if (exists == null)
                throw new ArgumentNullException(nameof(exists));

            if (read == null)
                throw new ArgumentNullException(nameof(read));

            if (write == null)
                throw new ArgumentNullException(nameof(write));

            if (listKnown == null)
                throw new ArgumentNullException(nameof(listKnown));

            _consumerId = consumerId.Trim();
            _exists = exists;
            _read = read;
            _write = write;
            _listKnown = listKnown;

            var dependency = new ApiDependencyDescriptor(
                new ApiModIdentity(
                    _consumerId,
                    consumerDisplayName.Trim(),
                    consumerModVersion
                ),
                new ApiRequirement(
                    ProviderApiId,
                    new ApiVersionRange(ApiVersionFile.MinimumProviderApiVersion, null)
                ),
                isRequired
                    ? ApiDependencyKind.Required
                    : ApiDependencyKind.Optional,
                featureDescription
            );

            _consumer = new ApiDiscoveryConsumer(messageBus, dependency);

            _consumer.Connected += OnConnected;
            _consumer.Disconnected += OnDisconnected;
        }

        public bool IsStarted => _consumer.IsStarted;

        public bool IsConnected => _providerUnregister != null;

        public SemanticVersion ProviderModVersion { get; private set; }
        public SemanticVersion ProviderApiVersion { get; private set; }
        public bool SupportsWorldConfigs => _providerWorldUnregister != null && _openWorldConfig != null && _applyWorldConfig != null && _saveWorldConfig != null && _reloadWorldConfig != null && _loadWorldConfig != null && _saveAsWorldConfig != null && _listWorldConfigVariants != null;

        public Exception LastError => _lastError ?? _consumer.LastError;

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            ReleaseProviderRegistration();

            _consumer.Connected -= OnConnected;
            _consumer.Disconnected -= OnDisconnected;
            _consumer.Dispose();

            ClearConnection();
        }

        public event Action Connected;
        public event Action Disconnected;
        public event Action<WorldConfigResponse> WorldConfigResponseReceived;

        public static ConfigApiClient CreateForSpaceEngineers(
            IModMessageBus messageBus, string consumerId, string consumerDisplayName, SemanticVersion consumerModVersion,
            bool isRequired, string featureDescription)
        {
            var storage = SpaceEngineersConfigTextStorage.Create(consumerId, typeof(SpaceEngineersConfigTextStorage));

            return new ConfigApiClient(messageBus, consumerId, consumerDisplayName, consumerModVersion,
                                       isRequired, featureDescription, storage.Exists, storage.Read, storage.Write, storage.ListKnown);
        }
        public void Start()
        {
            ThrowIfDisposed();

            if (IsStarted)
                return;

            _lastError = null;
            _consumer.Start();

            if (!_consumer.IsConnected)
                _consumer.RequestDiscovery();
        }

        public Guid RequestDiscovery()
        {
            ThrowIfDisposed();
            _lastError = null;

            return _consumer.RequestDiscovery();
        }

        public Guid Rediscover()
        {
            ThrowIfDisposed();
            _lastError = null;

            return _consumer.Rediscover();
        }

        internal bool StorageExists(ConfigLocation location, string file)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));

            return _exists(ValidateLocation(location), file);
        }

        internal string[] ListKnownFiles(ConfigLocation location)
        {
            ThrowIfDisposed();
            string[] files = _listKnown(ValidateLocation(location));
            return files ?? new string[0];
        }
        public ConfigHandle<T> OpenHandle<T>(ConfigDefinition<T> definition, ConfigLocation location) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            ConfigDocument defaults = definition.Serialize(definition.CreateDefaults());
            ConfigDocument stored = Open(definition.ConfigKey, location, definition.GetVariantFile(ConfigDefinition<T>.DefaultVariant), defaults);
            return new ConfigHandle<T>(this, definition, location, ConfigDefinition<T>.DefaultVariant, defaults, stored);
        }
        public T Open<T>(ConfigDefinition<T> definition, ConfigLocation location) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            return definition.Deserialize(
                Open(definition.ConfigKey, location, definition.GetVariantFile(ConfigDefinition<T>.DefaultVariant),
                    definition.Serialize(definition.CreateDefaults())
                )
            );
        }

        public ConfigDocument Open(string configKey, ConfigLocation location, string file, ConfigDocument currentDefaults)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));

            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));

            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));

            object payload = _openConfig(_consumerId, _registrationId, configKey.Trim(), ValidateLocation(location), 
                                         file, ConfigDocumentWireCodec.Encode(currentDefaults));

            return ConfigDocumentWireCodec.Decode(payload);
        }


        public T Save<T>(ConfigDefinition<T> definition, ConfigLocation location, T playerValues) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            return definition.Deserialize(
                Save(definition.ConfigKey, location, definition.GetVariantFile(ConfigDefinition<T>.DefaultVariant),
                     definition.Serialize(definition.CreateDefaults()),
                     definition.Serialize(playerValues)
                )
            );
        }

        public ConfigDocument Save(string configKey, ConfigLocation location, string file, 
                                   ConfigDocument currentDefaults, ConfigDocument playerValues)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));

            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));

            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));

            if (playerValues == null)
                throw new ArgumentNullException(nameof(playerValues));

            object payload = _saveConfig(_consumerId, _registrationId, configKey.Trim(), ValidateLocation(location), file,
                                         ConfigDocumentWireCodec.Encode(currentDefaults), ConfigDocumentWireCodec.Encode(playerValues));

            return ConfigDocumentWireCodec.Decode(payload);
        }

        public void OpenWorld<T>(ConfigDefinition<T> definition) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            OpenWorld(definition.ConfigKey, definition.Serialize(definition.CreateDefaults()));
        }

        public void OpenWorld(string configKey, ConfigDocument currentDefaults)
        {
            ThrowIfDisposed();
            EnsureWorldConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));
            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));

            _openWorldConfig(_consumerId, _registrationId, configKey.Trim(), ConfigDocumentWireCodec.Encode(currentDefaults));
        }

        public void ApplyWorld<T>(ConfigDefinition<T> definition, T draft) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            ApplyWorld(definition.ConfigKey, definition.Serialize(draft));
        }

        public void ApplyWorld(string configKey, ConfigDocument draft)
        {
            ThrowIfDisposed();
            EnsureWorldConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));
            if (draft == null)
                throw new ArgumentNullException(nameof(draft));

            _applyWorldConfig(_consumerId, _registrationId, configKey.Trim(), ConfigDocumentWireCodec.Encode(draft));
        }

        public void SaveWorld<T>(ConfigDefinition<T> definition) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            SaveWorld(definition.ConfigKey);
        }

        public void SaveWorld(string configKey)
        {
            ThrowIfDisposed();
            EnsureWorldConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));

            _saveWorldConfig(_consumerId, _registrationId, configKey.Trim());
        }

        public void ReloadWorld<T>(ConfigDefinition<T> definition) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            ReloadWorld(definition.ConfigKey);
        }

        public void ReloadWorld(string configKey)
        {
            ThrowIfDisposed();
            EnsureWorldConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));

            _reloadWorldConfig(_consumerId, _registrationId, configKey.Trim());
        }

        public void LoadWorld<T>(ConfigDefinition<T> definition, string variant) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            LoadWorld(definition.ConfigKey, variant);
        }

        public void LoadWorld(string configKey, string variant)
        {
            ThrowIfDisposed();
            EnsureWorldConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));
            if (string.IsNullOrWhiteSpace(variant))
                throw new ArgumentException("Config variant must not be empty.", nameof(variant));

            _loadWorldConfig(_consumerId, _registrationId, configKey.Trim(), variant);
        }

        public void SaveAsWorld<T>(ConfigDefinition<T> definition, string variant) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            SaveAsWorld(definition.ConfigKey, variant);
        }

        public void SaveAsWorld(string configKey, string variant)
        {
            ThrowIfDisposed();
            EnsureWorldConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));
            if (string.IsNullOrWhiteSpace(variant))
                throw new ArgumentException("Config variant must not be empty.", nameof(variant));

            _saveAsWorldConfig(_consumerId, _registrationId, configKey.Trim(), variant);
        }

        public void ListWorldVariants<T>(ConfigDefinition<T> definition) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            ListWorldVariants(definition.ConfigKey);
        }

        public void ListWorldVariants(string configKey)
        {
            ThrowIfDisposed();
            EnsureWorldConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));

            _listWorldConfigVariants(_consumerId, _registrationId, configKey.Trim());
        }

        public void Stop()
        {
            ThrowIfDisposed();

            ReleaseProviderRegistration();
            ClearConnection();
            _consumer.Stop();
        }

        private void OnConnected(ApiConnectedEventArgs eventArgs)
        {
            try
            {
                Func<string, Guid, Func<int, string, bool>, Func<int, string, string>, Action<int, string, string>, Func<int, string[]>, Action> registerConsumer;
                Func<string, Guid, string, int, string, object, object> openConfig;
                Func<string, Guid, string, int, string, object, object, object> saveConfig;
                Func<string, Guid, Action<IDictionary<string, object>>, Action> registerWorldConfig;
                Action<string, Guid, string, object> openWorldConfig;
                Action<string, Guid, string, object> applyWorldConfig;
                Action<string, Guid, string> saveWorldConfig;
                Action<string, Guid, string> reloadWorldConfig;
                Action<string, Guid, string, string> loadWorldConfig;
                Action<string, Guid, string, string> saveAsWorldConfig;
                Action<string, Guid, string> listWorldConfigVariants;

                if (!eventArgs.Connection.TryGetEndpoint(RegisterConsumerEndpoint, out registerConsumer))
                {
                    RejectConnection(RegisterConsumerEndpoint);
                    return;
                }

                if (!eventArgs.Connection.TryGetEndpoint(OpenConfigEndpoint, out openConfig))
                {
                    RejectConnection(OpenConfigEndpoint);
                    return;
                }

                if (!eventArgs.Connection.TryGetEndpoint(SaveConfigEndpoint, out saveConfig))
                {
                    RejectConnection(SaveConfigEndpoint);
                    return;
                }

                bool hasRegisterWorldConfig = eventArgs.Connection.TryGetEndpoint(RegisterWorldConfigEndpoint, out registerWorldConfig);
                bool hasOpenWorldConfig = eventArgs.Connection.TryGetEndpoint(OpenWorldConfigEndpoint, out openWorldConfig);
                bool hasApplyWorldConfig = eventArgs.Connection.TryGetEndpoint(ApplyWorldConfigEndpoint, out applyWorldConfig);
                bool hasSaveWorldConfig = eventArgs.Connection.TryGetEndpoint(SaveWorldConfigEndpoint, out saveWorldConfig);
                bool hasReloadWorldConfig = eventArgs.Connection.TryGetEndpoint(ReloadWorldConfigEndpoint, out reloadWorldConfig);
                bool hasLoadWorldConfig = eventArgs.Connection.TryGetEndpoint(LoadWorldConfigEndpoint, out loadWorldConfig);
                bool hasSaveAsWorldConfig = eventArgs.Connection.TryGetEndpoint(SaveAsWorldConfigEndpoint, out saveAsWorldConfig);
                bool hasListWorldConfigVariants = eventArgs.Connection.TryGetEndpoint(ListWorldConfigVariantsEndpoint, out listWorldConfigVariants);
                bool hasAnyWorldConfigEndpoint = hasRegisterWorldConfig || hasOpenWorldConfig || hasApplyWorldConfig || hasSaveWorldConfig || hasReloadWorldConfig || hasLoadWorldConfig || hasSaveAsWorldConfig || hasListWorldConfigVariants;
                bool hasWorldConfigEndpoints = hasRegisterWorldConfig && hasOpenWorldConfig && hasApplyWorldConfig && hasSaveWorldConfig && hasReloadWorldConfig && hasLoadWorldConfig && hasSaveAsWorldConfig && hasListWorldConfigVariants;

                if (hasAnyWorldConfigEndpoint && !hasWorldConfigEndpoints)
                    throw new InvalidOperationException("The ConfigAPI provider exposes an incomplete canonical World config endpoint set.");

                var registrationId = Guid.NewGuid();
                Action unregister = registerConsumer(_consumerId, registrationId, _exists, _read, _write, _listKnown);

                if (unregister == null)
                    throw new InvalidOperationException("The ConfigAPI provider returned no unregister action.");

                _providerUnregister = unregister;

                if (hasWorldConfigEndpoints)
                {
                    Action worldUnregister = registerWorldConfig(_consumerId, registrationId, OnWorldConfigResponse);
                    if (worldUnregister == null)
                        throw new InvalidOperationException("The ConfigAPI provider returned no World config unregister action.");

                    _providerWorldUnregister = worldUnregister;
                    _openWorldConfig = openWorldConfig;
                    _applyWorldConfig = applyWorldConfig;
                    _saveWorldConfig = saveWorldConfig;
                    _reloadWorldConfig = reloadWorldConfig;
                    _loadWorldConfig = loadWorldConfig;
                    _saveAsWorldConfig = saveAsWorldConfig;
                    _listWorldConfigVariants = listWorldConfigVariants;
                }

                _openConfig = openConfig;
                _saveConfig = saveConfig;
                _registrationId = registrationId;
                ProviderModVersion = eventArgs.Connection.Provider.Version;
                ProviderApiVersion = eventArgs.Connection.Descriptor.Version;
                _lastError = null;

                RaiseConnected();
            }
            catch (Exception exception)
            {
                _lastError = exception;
                ReleaseProviderRegistration();
                ClearConnection();
                _consumer.Disconnect();
            }
        }

        private void OnDisconnected(ApiDisconnectedEventArgs eventArgs)
        {
            ReleaseProviderRegistration();
            ClearConnection();
            RaiseDisconnected();
        }

        private void ReleaseProviderRegistration()
        {
            Action worldUnregister = _providerWorldUnregister;
            _providerWorldUnregister = null;

            if (worldUnregister != null)
            {
                try
                {
                    worldUnregister();
                }
                catch (Exception exception)
                {
                    _lastError = exception;
                }
            }

            Action unregister = _providerUnregister;
            _providerUnregister = null;

            if (unregister == null)
                return;

            try
            {
                unregister();
            }
            catch (Exception exception)
            {
                _lastError = exception;
            }
        }

        private void ClearConnection()
        {
            _providerUnregister = null;
            _providerWorldUnregister = null;
            _openConfig = null;
            _saveConfig = null;
            _openWorldConfig = null;
            _applyWorldConfig = null;
            _saveWorldConfig = null;
            _reloadWorldConfig = null;
            _loadWorldConfig = null;
            _saveAsWorldConfig = null;
            _listWorldConfigVariants = null;
            _registrationId = Guid.Empty;
            ProviderModVersion = null;
            ProviderApiVersion = null;
        }

        private void RejectConnection(string endpoint)
        {
            _lastError = new InvalidOperationException($"The ConfigAPI provider is missing the exact {endpoint} endpoint.");

            _consumer.Disconnect();
        }

        private void EnsureConnected()
        {
            if (!IsConnected || _openConfig == null || _saveConfig == null || _registrationId == Guid.Empty)
                throw new InvalidOperationException("The ConfigAPI client is not connected.");
        }

        private void EnsureWorldConnected()
        {
            EnsureConnected();

            if (!SupportsWorldConfigs)
                throw new InvalidOperationException("The connected ConfigAPI provider does not support server-authoritative World config endpoints.");
        }

        private void OnWorldConfigResponse(IDictionary<string, object> payload)
        {
            try
            {
                RaiseWorldConfigResponseReceived(WorldConfigResponse.FromPayload(payload));
            }
            catch (Exception exception)
            {
                if (_lastError == null)
                    _lastError = exception;
            }
        }

        private void RaiseWorldConfigResponseReceived(WorldConfigResponse response)
        {
            Action<WorldConfigResponse> handlers = WorldConfigResponseReceived;
            if (handlers == null)
                return;

            foreach (Action<WorldConfigResponse> handler in handlers.GetInvocationList())
                try
                {
                    handler(response);
                }
                catch (Exception exception)
                {
                    if (_lastError == null)
                        _lastError = exception;
                }
        }

        private static int ValidateLocation(ConfigLocation location)
        {
            switch (location)
            {
                case ConfigLocation.Local:
                case ConfigLocation.Global:
                    return (int)location;

                case ConfigLocation.World:
                    throw new InvalidOperationException(
                        "World configs require the server-authoritative ConfigAPI path and cannot use direct Open or Save."
                    );

                default:
                    throw new ArgumentException($"Unsupported ConfigAPI storage location: {location}", nameof(location));
            }
        }

        private void RaiseConnected()
        {
            Action handler = Connected;

            if (handler == null)
                return;

            foreach (Action subscriber in handler.GetInvocationList())
                try
                {
                    subscriber();
                }
                catch (Exception exception)
                {
                    if (_lastError == null)
                        _lastError = exception;
                }
        }

        private void RaiseDisconnected()
        {
            Action handler = Disconnected;

            if (handler == null)
                return;

            foreach (Action subscriber in handler.GetInvocationList())
                try
                {
                    subscriber();
                }
                catch (Exception exception)
                {
                    if (_lastError == null)
                        _lastError = exception;
                }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new InvalidOperationException("The ConfigAPI client has been disposed.");
        }
    }
}
