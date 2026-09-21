using System;
using System.Collections.Generic;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.ConfigApi;
using Mz.SemanticVersioning;

namespace MarcoZechner.ConfigAPI.V2.Api
{
    internal sealed class WorldConfigFileOperationSmokeClient : IDisposable
    {
        private const string ConsumerId = "ConfigAPI.WorldFileSmoke";

        private readonly ApiDiscoveryConsumer _consumer;
        private readonly SpaceEngineersConfigTextStorage _storage;
        private Guid _registrationId;
        private Action _providerUnregister;
        private Action _worldUnregister;
        private Action<string, Guid, string, string, object> _open;
        private Action<string, Guid, string> _reload;
        private Action<string, Guid, string, string> _loadAndSwitch;
        private Action<string, Guid, string, string, object> _saveAndSwitch;
        private Action<string, Guid, string, string, object, bool> _export;
        private bool _isDisposed;

        public WorldConfigFileOperationSmokeClient(IModMessageBus messageBus, SemanticVersion modVersion)
        {
            if (messageBus == null)
                throw new ArgumentNullException(nameof(messageBus));
            if (modVersion == null)
                throw new ArgumentNullException(nameof(modVersion));

            _storage = new SpaceEngineersConfigTextStorage(new SpaceEngineersConfigApiStorageUtilities(), typeof(WorldConfigFileOperationSmokeClient));

            var dependency = new ApiDependencyDescriptor(
                new ApiModIdentity(ConsumerId, "ConfigAPI World File Smoke", modVersion),
                new ApiRequirement(ConfigApiProvider.ApiId, new ApiVersionRange(new SemanticVersion(2, 2, 0), null)),
                ApiDependencyKind.Optional,
                "Exercises ConfigAPI 2.2 World file operations before the consumer package is published.");

            _consumer = new ApiDiscoveryConsumer(messageBus, dependency);
            _consumer.Connected += OnConnected;
            _consumer.Disconnected += OnDisconnected;
        }

        public bool IsConnected => _registrationId != Guid.Empty && _providerUnregister != null && _worldUnregister != null && _open != null && _reload != null && _loadAndSwitch != null && _saveAndSwitch != null && _export != null;
        public Exception LastError { get; private set; }

        public event Action<WorldConfigResponse> ResponseReceived;

        public void Start()
        {
            ThrowIfDisposed();
            _consumer.Start();
            _consumer.RequestDiscovery();
        }

        public void Open(string configKey, string file, ConfigDocument defaults)
        {
            EnsureConnected();
            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));
            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));
            if (defaults == null)
                throw new ArgumentNullException(nameof(defaults));

            _open(ConsumerId, _registrationId, configKey.Trim(), file, Mz.ConfigApi.ConfigDocumentWireCodec.Encode(defaults));
        }

        public void Reload(string configKey)
        {
            EnsureConnected();
            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));

            _reload(ConsumerId, _registrationId, configKey.Trim());
        }

        public void LoadAndSwitch(string configKey, string file)
        {
            EnsureConnected();
            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));
            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));

            _loadAndSwitch(ConsumerId, _registrationId, configKey.Trim(), file);
        }

        public void SaveAndSwitch(string configKey, string file, ConfigDocument document)
        {
            EnsureConnected();
            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));
            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            _saveAndSwitch(ConsumerId, _registrationId, configKey.Trim(), file, Mz.ConfigApi.ConfigDocumentWireCodec.Encode(document));
        }

        public void Export(string configKey, string file, ConfigDocument document, bool overwrite)
        {
            EnsureConnected();
            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));
            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            _export(ConsumerId, _registrationId, configKey.Trim(), file, Mz.ConfigApi.ConfigDocumentWireCodec.Encode(document), overwrite);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            ReleaseRegistrations();
            _consumer.Connected -= OnConnected;
            _consumer.Disconnected -= OnDisconnected;
            _consumer.Dispose();
            ClearEndpoints();
        }

        private void OnConnected(ApiConnectedEventArgs eventArgs)
        {
            try
            {
                Func<string, Guid, Func<int, string, string>, Action<int, string, string>, Action> registerConsumer = RequiredEndpoint<Func<string, Guid, Func<int, string, string>, Action<int, string, string>, Action>>(eventArgs.Connection, ConfigApiProvider.RegisterConsumerEndpoint);
                Func<string, Guid, Action<IDictionary<string, object>>, Action> registerWorld = RequiredEndpoint<Func<string, Guid, Action<IDictionary<string, object>>, Action>>(eventArgs.Connection, ConfigApiProvider.RegisterWorldConfigEndpoint);
                Action<string, Guid, string, string, object> open = RequiredEndpoint<Action<string, Guid, string, string, object>>(eventArgs.Connection, ConfigApiProvider.OpenWorldConfigEndpoint);
                Action<string, Guid, string> reload = RequiredEndpoint<Action<string, Guid, string>>(eventArgs.Connection, ConfigApiProvider.ReloadWorldConfigEndpoint);
                Action<string, Guid, string, string> loadAndSwitch = RequiredEndpoint<Action<string, Guid, string, string>>(eventArgs.Connection, ConfigApiProvider.LoadAndSwitchWorldConfigEndpoint);
                Action<string, Guid, string, string, object> saveAndSwitch = RequiredEndpoint<Action<string, Guid, string, string, object>>(eventArgs.Connection, ConfigApiProvider.SaveAndSwitchWorldConfigEndpoint);
                Action<string, Guid, string, string, object, bool> export = RequiredEndpoint<Action<string, Guid, string, string, object, bool>>(eventArgs.Connection, ConfigApiProvider.ExportWorldConfigEndpoint);

                var registrationId = Guid.NewGuid();
                Action providerUnregister = registerConsumer(ConsumerId, registrationId, _storage.Read, _storage.Write);
                if (providerUnregister == null)
                    throw new InvalidOperationException("The ConfigAPI provider returned no diagnostic consumer unregister action.");

                Action worldUnregister;
                try
                {
                    worldUnregister = registerWorld(ConsumerId, registrationId, OnResponse);
                }
                catch
                {
                    providerUnregister();
                    throw;
                }

                if (worldUnregister == null)
                {
                    providerUnregister();
                    throw new InvalidOperationException("The ConfigAPI provider returned no diagnostic World unregister action.");
                }

                _registrationId = registrationId;
                _providerUnregister = providerUnregister;
                _worldUnregister = worldUnregister;
                _open = open;
                _reload = reload;
                _loadAndSwitch = loadAndSwitch;
                _saveAndSwitch = saveAndSwitch;
                _export = export;
                LastError = null;
            }
            catch (Exception exception)
            {
                LastError = exception;
                ReleaseRegistrations();
                ClearEndpoints();
                _consumer.Disconnect();
            }
        }

        private void OnDisconnected(ApiDisconnectedEventArgs eventArgs)
        {
            ReleaseRegistrations();
            ClearEndpoints();
        }

        private void OnResponse(IDictionary<string, object> payload)
        {
            try
            {
                WorldConfigResponse response = WorldConfigResponse.FromPayload(payload);
                Action<WorldConfigResponse> handlers = ResponseReceived;
                if (handlers == null)
                    return;

                foreach (Action<WorldConfigResponse> handler in handlers.GetInvocationList())
                    try
                    {
                        handler(response);
                    }
                    catch (Exception exception)
                    {
                        if (LastError == null)
                            LastError = exception;
                    }
            }
            catch (Exception exception)
            {
                if (LastError == null)
                    LastError = exception;
            }
        }

        private void ReleaseRegistrations()
        {
            Action worldUnregister = _worldUnregister;
            Action providerUnregister = _providerUnregister;
            _worldUnregister = null;
            _providerUnregister = null;
            _registrationId = Guid.Empty;

            try
            {
                worldUnregister?.Invoke();
            }
            catch (Exception exception)
            {
                if (LastError == null)
                    LastError = exception;
            }

            try
            {
                providerUnregister?.Invoke();
            }
            catch (Exception exception)
            {
                if (LastError == null)
                    LastError = exception;
            }
        }

        private void ClearEndpoints()
        {
            _open = null;
            _reload = null;
            _loadAndSwitch = null;
            _saveAndSwitch = null;
            _export = null;
        }

        private void EnsureConnected()
        {
            ThrowIfDisposed();
            if (!IsConnected)
                throw new InvalidOperationException("The ConfigAPI 2.2 World file-operation smoke client is not connected.");
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new InvalidOperationException("The ConfigAPI 2.2 World file-operation smoke client has been disposed.");
        }

        private static TDelegate RequiredEndpoint<TDelegate>(ApiConnection connection, string endpointName) where TDelegate : class
        {
            TDelegate endpoint;
            if (!connection.TryGetEndpoint(endpointName, out endpoint))
                throw new InvalidOperationException("The ConfigAPI provider is missing the exact " + endpointName + " endpoint.");

            return endpoint;
        }
    }
}