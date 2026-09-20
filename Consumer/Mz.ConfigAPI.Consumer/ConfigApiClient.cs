using System;
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

                var registrationId = Guid.NewGuid();

                Action unregister = registerConsumer(_consumerId, registrationId, _read, _write);

                if (unregister == null)
                    throw new InvalidOperationException("The ConfigAPI provider returned no unregister action.");

                _providerUnregister = unregister;
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
            _openConfig = null;
            _saveConfig = null;
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
