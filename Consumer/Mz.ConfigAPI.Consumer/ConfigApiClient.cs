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
        public const string SaveWorldConfigEndpoint = "SaveWorldConfig";
        public const string ReloadWorldConfigEndpoint = "ReloadWorldConfig";
        public const string LoadAndSwitchWorldConfigEndpoint = "LoadAndSwitchWorldConfig";
        public const string SaveAndSwitchWorldConfigEndpoint = "SaveAndSwitchWorldConfig";
        public const string ExportWorldConfigEndpoint = "ExportWorldConfig";
        public const string ApplyPresetConfigEndpoint = "ApplyPresetConfig";
        public const string ApplyPresetWorldConfigEndpoint = "ApplyPresetWorldConfig";
        public const string SavePresetConfigEndpoint = "SavePresetConfig";
        public const string SavePresetWorldConfigEndpoint = "SavePresetWorldConfig";
        private readonly ApiDiscoveryConsumer _consumer;

        private readonly string _consumerId;
        private readonly Func<int, string, string> _read;
        private readonly Action<int, string, string> _write;
        private bool _isDisposed;
        private Exception _lastError;
        private Func<string, Guid, string, int, string, object, object> _openConfig;

        private Action _providerUnregister;
        private Guid _registrationId;
        private Func<string, Guid, string, int, string, object, object, object> _saveConfig;
        private Func<string, Guid, string, int, string, string, object, object> _applyPresetConfig;
        private Func<string, Guid, string, int, string, string, object, object, bool, object> _savePresetConfig;
        private Action<string, Guid, string, string, object> _openWorldConfig;
        private Action<string, Guid, string, object> _saveWorldConfig;
        private Action<string, Guid, string> _reloadWorldConfig;
        private Action<string, Guid, string, string> _loadAndSwitchWorldConfig;
        private Action<string, Guid, string, string, object> _saveAndSwitchWorldConfig;
        private Action<string, Guid, string, string, object, bool> _exportWorldConfig;
        private Action<string, Guid, string, string> _applyPresetWorldConfig;
        private Action<string, Guid, string, string, object, bool> _savePresetWorldConfig;
        private Action _providerWorldUnregister;

        public ConfigApiClient(
            IModMessageBus messageBus, string consumerId, string consumerDisplayName, SemanticVersion consumerModVersion,
            bool isRequired, string featureDescription, Func<int, string, string> read, Action<int, string, string> write)
        {
            if (messageBus == null)
                throw new ArgumentNullException(nameof(messageBus));

            if (string.IsNullOrWhiteSpace(consumerId))
                throw new ArgumentException("A stable consumer mod identifier is required.", nameof(consumerId));

            if (string.IsNullOrWhiteSpace(consumerDisplayName))
                throw new ArgumentException("A consumer display name is required.", nameof(consumerDisplayName));

            if (consumerModVersion == null)
                throw new ArgumentNullException(nameof(consumerModVersion));

            if (read == null)
                throw new ArgumentNullException(nameof(read));

            if (write == null)
                throw new ArgumentNullException(nameof(write));

            _consumerId = consumerId.Trim();
            _read = read;
            _write = write;

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
        public bool SupportsWorldConfigs => _providerWorldUnregister != null && _openWorldConfig != null && _saveWorldConfig != null;
        public bool SupportsWorldFileOperations => SupportsWorldConfigs && _reloadWorldConfig != null && _loadAndSwitchWorldConfig != null && _saveAndSwitchWorldConfig != null && _exportWorldConfig != null;
        public bool SupportsPresets => IsConnected && _applyPresetConfig != null;
        public bool SupportsWorldPresets => SupportsWorldConfigs && _applyPresetWorldConfig != null;
        public bool SupportsPresetSaving => IsConnected && _savePresetConfig != null;
        public bool SupportsWorldPresetSaving => SupportsWorldConfigs && _savePresetWorldConfig != null;

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
            var storage = new SpaceEngineersConfigTextStorage(
                new SpaceEngineersConfigApiStorageUtilities(),
                typeof(SpaceEngineersConfigTextStorage)
            );

            return new ConfigApiClient(messageBus, consumerId, consumerDisplayName, consumerModVersion,
                                       isRequired, featureDescription, storage.Read, storage.Write);
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

        public ConfigHandle<T> OpenHandle<T>(ConfigDefinition<T> definition, ConfigLocation location) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            T value = Open(definition, location);

            return new ConfigHandle<T>(this, definition, location, definition.DefaultFile, value);
        }


        public T Open<T>(ConfigDefinition<T> definition, ConfigLocation location) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            return definition.Deserialize(
                Open(definition.ConfigKey, location, definition.DefaultFile, 
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
                Save(definition.ConfigKey, location, definition.DefaultFile,
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

        public T SavePreset<T>(ConfigDefinition<T> definition, ConfigLocation location, string presetFile, T playerValues, bool overwrite = false) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            return definition.Deserialize(SavePreset(definition.ConfigKey, location, definition.DefaultFile, presetFile, definition.Serialize(definition.CreateDefaults()), definition.Serialize(playerValues), overwrite));
        }

        public ConfigDocument SavePreset(string configKey, ConfigLocation location, string canonicalFile, string presetFile, ConfigDocument currentDefaults, ConfigDocument playerValues, bool overwrite = false)
        {
            ThrowIfDisposed();
            EnsurePresetSavingConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));
            if (string.IsNullOrWhiteSpace(canonicalFile))
                throw new ArgumentException("Canonical config file must not be empty.", nameof(canonicalFile));
            if (string.IsNullOrWhiteSpace(presetFile))
                throw new ArgumentException("Preset file must not be empty.", nameof(presetFile));
            if (string.Equals(canonicalFile, presetFile, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Preset target must not be the canonical active config file: " + presetFile);
            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));
            if (playerValues == null)
                throw new ArgumentNullException(nameof(playerValues));

            object payload = _savePresetConfig(_consumerId, _registrationId, configKey.Trim(), ValidateLocation(location), canonicalFile, presetFile, ConfigDocumentWireCodec.Encode(currentDefaults), ConfigDocumentWireCodec.Encode(playerValues), overwrite);
            return ConfigDocumentWireCodec.Decode(payload);
        }
        public T ApplyPreset<T>(ConfigDefinition<T> definition, ConfigLocation location, string presetFile) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            return definition.Deserialize(ApplyPreset(definition.ConfigKey, location, definition.DefaultFile, presetFile, definition.Serialize(definition.CreateDefaults())));
        }

        public ConfigDocument ApplyPreset(string configKey, ConfigLocation location, string canonicalFile, string presetFile, ConfigDocument currentDefaults)
        {
            ThrowIfDisposed();
            EnsurePresetConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));
            if (string.IsNullOrWhiteSpace(canonicalFile))
                throw new ArgumentException("Canonical config file must not be empty.", nameof(canonicalFile));
            if (string.IsNullOrWhiteSpace(presetFile))
                throw new ArgumentException("Preset file must not be empty.", nameof(presetFile));
            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));

            object payload = _applyPresetConfig(_consumerId, _registrationId, configKey.Trim(), ValidateLocation(location), canonicalFile, presetFile, ConfigDocumentWireCodec.Encode(currentDefaults));
            return ConfigDocumentWireCodec.Decode(payload);
        }
        public void OpenWorld<T>(ConfigDefinition<T> definition) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            OpenWorld(definition.ConfigKey, definition.DefaultFile, definition.Serialize(definition.CreateDefaults()));
        }

        public void OpenWorld(string configKey, string file, ConfigDocument currentDefaults)
        {
            ThrowIfDisposed();
            EnsureWorldConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));
            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));
            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));

            _openWorldConfig(_consumerId, _registrationId, configKey.Trim(), file, ConfigDocumentWireCodec.Encode(currentDefaults));
        }

        public void SaveWorld<T>(ConfigDefinition<T> definition, T playerValues) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            SaveWorld(definition.ConfigKey, definition.Serialize(playerValues));
        }

        public void SaveWorld(string configKey, ConfigDocument playerValues)
        {
            ThrowIfDisposed();
            EnsureWorldConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));
            if (playerValues == null)
                throw new ArgumentNullException(nameof(playerValues));

            _saveWorldConfig(_consumerId, _registrationId, configKey.Trim(), ConfigDocumentWireCodec.Encode(playerValues));
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
            EnsureWorldFileOperationsConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));

            _reloadWorldConfig(_consumerId, _registrationId, configKey.Trim());
        }

        public void LoadAndSwitchWorld<T>(ConfigDefinition<T> definition, string file) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            LoadAndSwitchWorld(definition.ConfigKey, file);
        }

        public void LoadAndSwitchWorld(string configKey, string file)
        {
            ThrowIfDisposed();
            EnsureWorldFileOperationsConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));
            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));

            _loadAndSwitchWorldConfig(_consumerId, _registrationId, configKey.Trim(), file);
        }

        public void SaveAndSwitchWorld<T>(ConfigDefinition<T> definition, string file, T playerValues) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            SaveAndSwitchWorld(definition.ConfigKey, file, definition.Serialize(playerValues));
        }

        public void SaveAndSwitchWorld(string configKey, string file, ConfigDocument playerValues)
        {
            ThrowIfDisposed();
            EnsureWorldFileOperationsConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));
            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));
            if (playerValues == null)
                throw new ArgumentNullException(nameof(playerValues));

            _saveAndSwitchWorldConfig(_consumerId, _registrationId, configKey.Trim(), file, ConfigDocumentWireCodec.Encode(playerValues));
        }

        public void ExportWorld<T>(ConfigDefinition<T> definition, string file, T playerValues, bool overwrite = false) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            ExportWorld(definition.ConfigKey, file, definition.Serialize(playerValues), overwrite);
        }

        public void ExportWorld(string configKey, string file, ConfigDocument playerValues, bool overwrite = false)
        {
            ThrowIfDisposed();
            EnsureWorldFileOperationsConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));
            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));
            if (playerValues == null)
                throw new ArgumentNullException(nameof(playerValues));

            _exportWorldConfig(_consumerId, _registrationId, configKey.Trim(), file, ConfigDocumentWireCodec.Encode(playerValues), overwrite);
        }

        public void SavePresetWorld<T>(ConfigDefinition<T> definition, string presetFile, T playerValues, bool overwrite = false) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            SavePresetWorld(definition.ConfigKey, presetFile, definition.Serialize(playerValues), overwrite);
        }

        public void SavePresetWorld(string configKey, string presetFile, ConfigDocument playerValues, bool overwrite = false)
        {
            ThrowIfDisposed();
            EnsureWorldPresetSavingConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));
            if (string.IsNullOrWhiteSpace(presetFile))
                throw new ArgumentException("Preset file must not be empty.", nameof(presetFile));
            if (playerValues == null)
                throw new ArgumentNullException(nameof(playerValues));

            _savePresetWorldConfig(_consumerId, _registrationId, configKey.Trim(), presetFile, ConfigDocumentWireCodec.Encode(playerValues), overwrite);
        }
        public void ApplyPresetWorld<T>(ConfigDefinition<T> definition, string presetFile) where T : class
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            ApplyPresetWorld(definition.ConfigKey, presetFile);
        }

        public void ApplyPresetWorld(string configKey, string presetFile)
        {
            ThrowIfDisposed();
            EnsureWorldPresetConnected();

            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));
            if (string.IsNullOrWhiteSpace(presetFile))
                throw new ArgumentException("Preset file must not be empty.", nameof(presetFile));

            _applyPresetWorldConfig(_consumerId, _registrationId, configKey.Trim(), presetFile);
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
                Func<string, Guid, Func<int, string, string>, Action<int, string, string>, Action> registerConsumer;
                Func<string, Guid, string, int, string, object, object> openConfig;
                Func<string, Guid, string, int, string, object, object, object> saveConfig;
                Func<string, Guid, Action<IDictionary<string, object>>, Action> registerWorldConfig;
                Action<string, Guid, string, string, object> openWorldConfig;
                Action<string, Guid, string, object> saveWorldConfig;
                Action<string, Guid, string> reloadWorldConfig;
                Action<string, Guid, string, string> loadAndSwitchWorldConfig;
                Action<string, Guid, string, string, object> saveAndSwitchWorldConfig;
                Action<string, Guid, string, string, object, bool> exportWorldConfig;
                Func<string, Guid, string, int, string, string, object, object> applyPresetConfig;
                Action<string, Guid, string, string> applyPresetWorldConfig;
                Func<string, Guid, string, int, string, string, object, object, bool, object> savePresetConfig;
                Action<string, Guid, string, string, object, bool> savePresetWorldConfig;

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
                bool hasSaveWorldConfig = eventArgs.Connection.TryGetEndpoint(SaveWorldConfigEndpoint, out saveWorldConfig);
                bool hasReloadWorldConfig = eventArgs.Connection.TryGetEndpoint(ReloadWorldConfigEndpoint, out reloadWorldConfig);
                bool hasLoadAndSwitchWorldConfig = eventArgs.Connection.TryGetEndpoint(LoadAndSwitchWorldConfigEndpoint, out loadAndSwitchWorldConfig);
                bool hasSaveAndSwitchWorldConfig = eventArgs.Connection.TryGetEndpoint(SaveAndSwitchWorldConfigEndpoint, out saveAndSwitchWorldConfig);
                bool hasExportWorldConfig = eventArgs.Connection.TryGetEndpoint(ExportWorldConfigEndpoint, out exportWorldConfig);
                bool hasApplyPresetConfig = eventArgs.Connection.TryGetEndpoint(ApplyPresetConfigEndpoint, out applyPresetConfig);
                bool hasApplyPresetWorldConfig = eventArgs.Connection.TryGetEndpoint(ApplyPresetWorldConfigEndpoint, out applyPresetWorldConfig);
                bool hasSavePresetConfig = eventArgs.Connection.TryGetEndpoint(SavePresetConfigEndpoint, out savePresetConfig);
                bool hasSavePresetWorldConfig = eventArgs.Connection.TryGetEndpoint(SavePresetWorldConfigEndpoint, out savePresetWorldConfig);
                bool hasAnyWorldConfigEndpoint = hasRegisterWorldConfig || hasOpenWorldConfig || hasSaveWorldConfig;
                bool hasWorldConfigEndpoints = hasRegisterWorldConfig && hasOpenWorldConfig && hasSaveWorldConfig;
                bool hasAnyWorldFileOperationEndpoint = hasReloadWorldConfig || hasLoadAndSwitchWorldConfig || hasSaveAndSwitchWorldConfig || hasExportWorldConfig;
                bool hasWorldFileOperationEndpoints = hasReloadWorldConfig && hasLoadAndSwitchWorldConfig && hasSaveAndSwitchWorldConfig && hasExportWorldConfig;

                if (hasAnyWorldConfigEndpoint && !hasWorldConfigEndpoints)
                    throw new InvalidOperationException("The ConfigAPI provider exposes an incomplete World config endpoint set.");

                if (hasAnyWorldFileOperationEndpoint && (!hasWorldConfigEndpoints || !hasWorldFileOperationEndpoints))
                    throw new InvalidOperationException("The ConfigAPI provider exposes an incomplete World file-operation endpoint set.");

                if (hasApplyPresetWorldConfig && !hasWorldConfigEndpoints)
                    throw new InvalidOperationException("The ConfigAPI provider exposes a World preset endpoint without the required World config endpoint set.");

                if (hasSavePresetWorldConfig && !hasWorldConfigEndpoints)
                    throw new InvalidOperationException("The ConfigAPI provider exposes a World preset-saving endpoint without the required World config endpoint set.");

                var registrationId = Guid.NewGuid();

                Action unregister = registerConsumer(_consumerId, registrationId, _read, _write);

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
                    _saveWorldConfig = saveWorldConfig;

                    if (hasWorldFileOperationEndpoints)
                    {
                        _reloadWorldConfig = reloadWorldConfig;
                        _loadAndSwitchWorldConfig = loadAndSwitchWorldConfig;
                        _saveAndSwitchWorldConfig = saveAndSwitchWorldConfig;
                        _exportWorldConfig = exportWorldConfig;
                    }
                }

                _applyPresetConfig = hasApplyPresetConfig ? applyPresetConfig : null;
                _savePresetConfig = hasSavePresetConfig ? savePresetConfig : null;
                _applyPresetWorldConfig = hasApplyPresetWorldConfig ? applyPresetWorldConfig : null;
                _savePresetWorldConfig = hasSavePresetWorldConfig ? savePresetWorldConfig : null;
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
            _applyPresetConfig = null;
            _savePresetConfig = null;
            _openWorldConfig = null;
            _saveWorldConfig = null;
            _reloadWorldConfig = null;
            _loadAndSwitchWorldConfig = null;
            _saveAndSwitchWorldConfig = null;
            _exportWorldConfig = null;
            _applyPresetWorldConfig = null;
            _savePresetWorldConfig = null;
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

        private void EnsureWorldFileOperationsConnected()
        {
            EnsureWorldConnected();

            if (!SupportsWorldFileOperations)
                throw new InvalidOperationException("The connected ConfigAPI provider does not support World config file-operation endpoints.");
        }

        private void EnsurePresetSavingConnected()
        {
            EnsureConnected();

            if (!SupportsPresetSaving)
                throw new InvalidOperationException("The connected ConfigAPI provider does not support preset-saving endpoints.");
        }

        private void EnsureWorldPresetSavingConnected()
        {
            EnsureWorldConnected();

            if (!SupportsWorldPresetSaving)
                throw new InvalidOperationException("The connected ConfigAPI provider does not support World preset-saving endpoints.");
        }
        private void EnsurePresetConnected()
        {
            EnsureConnected();

            if (!SupportsPresets)
                throw new InvalidOperationException("The connected ConfigAPI provider does not support preset endpoints.");
        }

        private void EnsureWorldPresetConnected()
        {
            EnsureWorldConnected();

            if (!SupportsWorldPresets)
                throw new InvalidOperationException("The connected ConfigAPI provider does not support World preset endpoints.");
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
