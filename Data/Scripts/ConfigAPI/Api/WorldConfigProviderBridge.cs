using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;
using Mz.Logging;

namespace MarcoZechner.ConfigAPI.V2.Api
{
    public sealed class WorldConfigProviderBridge : IDisposable
    {
        public const string RuntimeUnavailableError = "World config networking is not available yet.";

        private readonly ConfigConsumerRegistrationRegistry _registry;
        private readonly Logger _logger;
        private readonly Dictionary<string, Registration> _registrations = new Dictionary<string, Registration>(StringComparer.Ordinal);
        private readonly Dictionary<ulong, PendingRoute> _pendingRoutes = new Dictionary<ulong, PendingRoute>();
        private readonly List<QueuedOpen> _queuedOpens = new List<QueuedOpen>();
        private readonly Dictionary<ConfigIdentity, WorldConfigSnapshot> _serverSnapshots = new Dictionary<ConfigIdentity, WorldConfigSnapshot>();
        private readonly HashSet<string> _serverOpenedRoutes = new HashSet<string>(StringComparer.Ordinal);
        private WorldConfigNetworkRuntime _runtime;
        private WorldConfigClientNetworkAdapter _clientAdapter;
        private WorldConfigServerService _serverService;
        private WorldConfigServerNetworkAdapter _serverAdapter;
        private bool _isDisposed;

        public WorldConfigProviderBridge(ConfigConsumerRegistrationRegistry registry, Logger logger = null)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            _registry = registry;
            _logger = logger;
        }

        public Action Register(string consumerId, Guid registrationId, Action<IDictionary<string, object>> responseCallback)
        {
            ThrowIfDisposed();

            if (responseCallback == null)
                throw new ArgumentNullException(nameof(responseCallback));

            _registry.GetStorage(consumerId, registrationId);

            string normalizedConsumerId = consumerId.Trim();
            string key = RegistrationKey(normalizedConsumerId, registrationId);
            _registrations[key] = new Registration(key, normalizedConsumerId, registrationId, responseCallback);

            return () => Unregister(key);
        }

        public void Open(string consumerId, Guid registrationId, string configKey, string file, object defaultsPayload)
        {
            ThrowIfDisposed();

            Registration registration = GetRequiredRegistration(consumerId, registrationId);
            ConfigIdentity identity = CreateIdentity(registration.ConsumerId, configKey);

            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));

            ConfigDocument defaults = ConfigDocumentWireCodec.Decode(defaultsPayload);

            if (_runtime == null)
            {
                _queuedOpens.Add(new QueuedOpen(registration.Key, identity, file, defaults));
                return;
            }

            SendOpen(registration, identity, file, defaults);
        }

        public void Save(string consumerId, Guid registrationId, string configKey, object documentPayload)
        {
            ThrowIfDisposed();

            Registration registration = GetRequiredRegistration(consumerId, registrationId);
            ConfigIdentity identity = CreateIdentity(registration.ConsumerId, configKey);
            ConfigDocument document = ConfigDocumentWireCodec.Decode(documentPayload);

            if (_clientAdapter != null)
            {
                SaveClient(registration, identity, document);
                return;
            }

            if (_serverService != null && _serverAdapter != null)
            {
                SaveServer(registration, identity, document);
                return;
            }

            NotifySyntheticError(registration, identity, WorldConfigNetworkOperation.Save, RuntimeUnavailableError);
        }

        public void Reload(string consumerId, Guid registrationId, string configKey)
        {
            ThrowIfDisposed();

            Registration registration = GetRequiredRegistration(consumerId, registrationId);
            ConfigIdentity identity = CreateIdentity(registration.ConsumerId, configKey);

            if (_clientAdapter != null)
            {
                SendClientRequest(registration, identity, WorldConfigNetworkOperation.Reload, () => _clientAdapter.Reload(identity.OwnerId, identity.ConfigKey));
                return;
            }

            if (_serverService != null && _serverAdapter != null)
            {
                ReloadServer(registration, identity);
                return;
            }

            NotifySyntheticError(registration, identity, WorldConfigNetworkOperation.Reload, RuntimeUnavailableError);
        }

        public void LoadAndSwitch(string consumerId, Guid registrationId, string configKey, string file)
        {
            ThrowIfDisposed();

            Registration registration = GetRequiredRegistration(consumerId, registrationId);
            ConfigIdentity identity = CreateIdentity(registration.ConsumerId, configKey);

            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));

            if (_clientAdapter != null)
            {
                SendClientRequest(registration, identity, WorldConfigNetworkOperation.LoadAndSwitch, () => _clientAdapter.LoadAndSwitch(identity.OwnerId, identity.ConfigKey, file));
                return;
            }

            if (_serverService != null && _serverAdapter != null)
            {
                LoadAndSwitchServer(registration, identity, file);
                return;
            }

            NotifySyntheticError(registration, identity, WorldConfigNetworkOperation.LoadAndSwitch, RuntimeUnavailableError);
        }

        public void SaveAndSwitch(string consumerId, Guid registrationId, string configKey, string file, object documentPayload)
        {
            ThrowIfDisposed();

            Registration registration = GetRequiredRegistration(consumerId, registrationId);
            ConfigIdentity identity = CreateIdentity(registration.ConsumerId, configKey);

            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));

            ConfigDocument document = ConfigDocumentWireCodec.Decode(documentPayload);

            if (_clientAdapter != null)
            {
                try
                {
                    _clientAdapter.SetDraft(identity.OwnerId, identity.ConfigKey, document);
                    SendClientRequest(registration, identity, WorldConfigNetworkOperation.SaveAndSwitch, () => _clientAdapter.SaveAndSwitch(identity.OwnerId, identity.ConfigKey, file));
                }
                catch (Exception exception)
                {
                    NotifySyntheticError(registration, identity, WorldConfigNetworkOperation.SaveAndSwitch, exception.Message);
                }

                return;
            }

            if (_serverService != null && _serverAdapter != null)
            {
                SaveAndSwitchServer(registration, identity, file, document);
                return;
            }

            NotifySyntheticError(registration, identity, WorldConfigNetworkOperation.SaveAndSwitch, RuntimeUnavailableError);
        }

        public void Export(string consumerId, Guid registrationId, string configKey, string file, object documentPayload, bool overwrite)
        {
            ThrowIfDisposed();

            Registration registration = GetRequiredRegistration(consumerId, registrationId);
            ConfigIdentity identity = CreateIdentity(registration.ConsumerId, configKey);

            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));

            ConfigDocument document = ConfigDocumentWireCodec.Decode(documentPayload);

            if (_clientAdapter != null)
            {
                try
                {
                    _clientAdapter.SetDraft(identity.OwnerId, identity.ConfigKey, document);
                    SendClientRequest(registration, identity, WorldConfigNetworkOperation.Export, () => _clientAdapter.Export(identity.OwnerId, identity.ConfigKey, file, overwrite));
                }
                catch (Exception exception)
                {
                    NotifySyntheticError(registration, identity, WorldConfigNetworkOperation.Export, exception.Message);
                }

                return;
            }

            if (_serverService != null && _serverAdapter != null)
            {
                ExportServer(registration, identity, file, document, overwrite);
                return;
            }

            NotifySyntheticError(registration, identity, WorldConfigNetworkOperation.Export, RuntimeUnavailableError);
        }
        public void AttachRuntime(WorldConfigNetworkRuntime runtime)
        {
            ThrowIfDisposed();

            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));
            if (_runtime != null)
                throw new InvalidOperationException("A World config network runtime is already attached.");

            _runtime = runtime;
            _clientAdapter = runtime.ClientAdapter;
            _serverService = runtime.ServerService;
            _serverAdapter = runtime.ServerAdapter;

            if (_clientAdapter != null)
                _clientAdapter.ResponseReceived += OnClientResponseReceived;
            else if (_serverAdapter != null)
                _serverAdapter.ResponseSent += OnServerResponseSent;
            else
                throw new InvalidOperationException("The World config network runtime exposes neither a client nor a server adapter.");

            FlushQueuedOpens();
        }

        public void DetachRuntime()
        {
            if (_clientAdapter != null)
                _clientAdapter.ResponseReceived -= OnClientResponseReceived;
            if (_serverAdapter != null)
                _serverAdapter.ResponseSent -= OnServerResponseSent;

            _clientAdapter = null;
            _serverService = null;
            _serverAdapter = null;
            _runtime = null;
            _pendingRoutes.Clear();
            _serverSnapshots.Clear();
            _serverOpenedRoutes.Clear();
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            DetachRuntime();
            _queuedOpens.Clear();
            _registrations.Clear();
        }

        private void FlushQueuedOpens()
        {
            if (_runtime == null || _queuedOpens.Count == 0)
                return;

            QueuedOpen[] queued = _queuedOpens.ToArray();
            _queuedOpens.Clear();

            for (int index = 0; index < queued.Length; index++)
            {
                QueuedOpen item = queued[index];
                Registration registration;

                if (!_registrations.TryGetValue(item.RegistrationKey, out registration) || !IsCurrent(registration))
                    continue;

                SendOpen(registration, item.Identity, item.File, item.Defaults);
            }
        }

        private void SendOpen(Registration registration, ConfigIdentity identity, string file, ConfigDocument defaults)
        {
            if (_clientAdapter != null)
            {
                try
                {
                    ulong requestId = _clientAdapter.Open(identity.OwnerId, identity.ConfigKey, file, defaults);
                    _pendingRoutes[requestId] = new PendingRoute(registration.Key, identity);
                }
                catch (Exception exception)
                {
                    NotifySyntheticError(registration, identity, WorldConfigNetworkOperation.Open, exception.Message);
                }

                return;
            }

            if (_serverService != null && _serverAdapter != null)
            {
                try
                {
                    WorldConfigSnapshot snapshot = _serverService.Open(identity.OwnerId, identity.ConfigKey, file, defaults);
                    _serverSnapshots[snapshot.Identity] = snapshot;
                    _serverOpenedRoutes.Add(ServerOpenedRouteKey(registration.Key, snapshot.Identity));

                    var response = new WorldConfigNetworkResponse(
                        0UL, WorldConfigNetworkOperation.Open, WorldConfigNetworkResponseKind.Snapshot,
                        _serverAdapter.LocalPeerId, false, false, snapshot, null);

                    Notify(registration, snapshot.Identity, response, snapshot);
                }
                catch (Exception exception)
                {
                    NotifySyntheticError(registration, identity, WorldConfigNetworkOperation.Open, exception.Message);
                }

                return;
            }

            NotifySyntheticError(registration, identity, WorldConfigNetworkOperation.Open, RuntimeUnavailableError);
        }

        private void SaveClient(Registration registration, ConfigIdentity identity, ConfigDocument document)
        {
            try
            {
                _clientAdapter.SetDraft(identity.OwnerId, identity.ConfigKey, document);
                ulong requestId = _clientAdapter.Save(identity.OwnerId, identity.ConfigKey);
                _pendingRoutes[requestId] = new PendingRoute(registration.Key, identity);
            }
            catch (Exception exception)
            {
                NotifySyntheticError(registration, identity, WorldConfigNetworkOperation.Save, exception.Message);
            }
        }

        private void SaveServer(Registration registration, ConfigIdentity identity, ConfigDocument document)
        {
            string openedRoute = ServerOpenedRouteKey(registration.Key, identity);
            if (!_serverOpenedRoutes.Contains(openedRoute))
            {
                NotifySyntheticError(registration, identity, WorldConfigNetworkOperation.Save, "World config has not been opened: " + identity.OwnerId + "/" + identity.ConfigKey);
                return;
            }

            WorldConfigSnapshot current;
            if (!_serverSnapshots.TryGetValue(identity, out current))
            {
                NotifySyntheticError(registration, identity, WorldConfigNetworkOperation.Save, "Authoritative World config state is unavailable: " + identity.OwnerId + "/" + identity.ConfigKey);
                return;
            }

            try
            {
                WorldConfigAuthorityResult result = _serverService.Save(identity.OwnerId, identity.ConfigKey, current.ServerIteration, document);
                _serverSnapshots[identity] = result.Snapshot;

                var response = new WorldConfigNetworkResponse(
                    0UL, WorldConfigNetworkOperation.Save, WorldConfigNetworkResponseKind.Snapshot,
                    _serverAdapter.LocalPeerId, result.IsApplied, result.IsStale, result.Snapshot, null);

                if (result.IsApplied)
                    _serverAdapter.BroadcastAppliedResponse(response);
                else
                    Notify(registration, identity, response, result.Snapshot);
            }
            catch (Exception exception)
            {
                NotifySyntheticError(registration, identity, WorldConfigNetworkOperation.Save, exception.Message);
            }
        }

        private void SendClientRequest(Registration registration, ConfigIdentity identity, WorldConfigNetworkOperation operation, Func<ulong> send)
        {
            try
            {
                ulong requestId = send();
                _pendingRoutes[requestId] = new PendingRoute(registration.Key, identity);
            }
            catch (Exception exception)
            {
                NotifySyntheticError(registration, identity, operation, exception.Message);
            }
        }

        private void ReloadServer(Registration registration, ConfigIdentity identity)
        {
            WorldConfigSnapshot current = GetServerSnapshot(registration, identity, WorldConfigNetworkOperation.Reload);
            if (current == null)
                return;

            try
            {
                CompleteServerMutation(registration, identity, WorldConfigNetworkOperation.Reload, _serverService.Reload(identity.OwnerId, identity.ConfigKey, current.ServerIteration));
            }
            catch (Exception exception)
            {
                NotifySyntheticError(registration, identity, WorldConfigNetworkOperation.Reload, exception.Message);
            }
        }

        private void LoadAndSwitchServer(Registration registration, ConfigIdentity identity, string file)
        {
            WorldConfigSnapshot current = GetServerSnapshot(registration, identity, WorldConfigNetworkOperation.LoadAndSwitch);
            if (current == null)
                return;

            try
            {
                CompleteServerMutation(registration, identity, WorldConfigNetworkOperation.LoadAndSwitch, _serverService.LoadAndSwitch(identity.OwnerId, identity.ConfigKey, current.ServerIteration, file));
            }
            catch (Exception exception)
            {
                NotifySyntheticError(registration, identity, WorldConfigNetworkOperation.LoadAndSwitch, exception.Message);
            }
        }

        private void SaveAndSwitchServer(Registration registration, ConfigIdentity identity, string file, ConfigDocument document)
        {
            WorldConfigSnapshot current = GetServerSnapshot(registration, identity, WorldConfigNetworkOperation.SaveAndSwitch);
            if (current == null)
                return;

            try
            {
                CompleteServerMutation(registration, identity, WorldConfigNetworkOperation.SaveAndSwitch, _serverService.SaveAndSwitch(identity.OwnerId, identity.ConfigKey, current.ServerIteration, document, file));
            }
            catch (Exception exception)
            {
                NotifySyntheticError(registration, identity, WorldConfigNetworkOperation.SaveAndSwitch, exception.Message);
            }
        }

        private void ExportServer(Registration registration, ConfigIdentity identity, string file, ConfigDocument document, bool overwrite)
        {
            WorldConfigSnapshot current = GetServerSnapshot(registration, identity, WorldConfigNetworkOperation.Export);
            if (current == null)
                return;

            try
            {
                WorldConfigExport export = _serverService.Export(identity.OwnerId, identity.ConfigKey, document, file, overwrite);
                var response = new WorldConfigNetworkResponse(0UL, WorldConfigNetworkOperation.Export, WorldConfigNetworkResponseKind.Exported, _serverAdapter.LocalPeerId, false, false, export.Authoritative, null);
                Notify(registration, identity, response, export.Authoritative);
            }
            catch (Exception exception)
            {
                NotifySyntheticError(registration, identity, WorldConfigNetworkOperation.Export, exception.Message);
            }
        }

        private WorldConfigSnapshot GetServerSnapshot(Registration registration, ConfigIdentity identity, WorldConfigNetworkOperation operation)
        {
            string openedRoute = ServerOpenedRouteKey(registration.Key, identity);
            if (!_serverOpenedRoutes.Contains(openedRoute))
            {
                NotifySyntheticError(registration, identity, operation, "World config has not been opened: " + identity.OwnerId + "/" + identity.ConfigKey);
                return null;
            }

            WorldConfigSnapshot current;
            if (!_serverSnapshots.TryGetValue(identity, out current))
            {
                NotifySyntheticError(registration, identity, operation, "Authoritative World config state is unavailable: " + identity.OwnerId + "/" + identity.ConfigKey);
                return null;
            }

            return current;
        }

        private void CompleteServerMutation(Registration registration, ConfigIdentity identity, WorldConfigNetworkOperation operation, WorldConfigAuthorityResult result)
        {
            _serverSnapshots[identity] = result.Snapshot;

            var response = new WorldConfigNetworkResponse(0UL, operation, WorldConfigNetworkResponseKind.Snapshot, _serverAdapter.LocalPeerId, result.IsApplied, result.IsStale, result.Snapshot, null);

            if (result.IsApplied)
                _serverAdapter.BroadcastAppliedResponse(response);
            else
                Notify(registration, identity, response, result.Snapshot);
        }
        private void OnClientResponseReceived(WorldConfigNetworkResponse response)
        {
            if (response == null || _clientAdapter == null)
                return;

            PendingRoute pending;
            bool hasPending = _pendingRoutes.TryGetValue(response.RequestId, out pending);
            bool isOwnResponse = response.TriggeredBy == _clientAdapter.LocalPeerId;

            if (response.Snapshot != null)
            {
                WorldConfigClientState state;

                if (_clientAdapter.TryGetState(response.Snapshot.Identity.OwnerId, response.Snapshot.Identity.ConfigKey, out state))
                    NotifyIdentity(response.Snapshot.Identity, response, state.Authoritative);
            }
            else if (isOwnResponse && hasPending)
            {
                Registration registration;

                if (_registrations.TryGetValue(pending.RegistrationKey, out registration) && IsCurrent(registration))
                    Notify(registration, pending.Identity, response, null);
            }

            if (isOwnResponse && hasPending && (response.Snapshot == null || pending.Identity.Equals(response.Snapshot.Identity)))
                _pendingRoutes.Remove(response.RequestId);
        }

        private void OnServerResponseSent(WorldConfigNetworkResponse response)
        {
            if (response == null || response.Snapshot == null || !response.IsApplied)
                return;

            bool wasKnown = _serverSnapshots.ContainsKey(response.Snapshot.Identity);
            _serverSnapshots[response.Snapshot.Identity] = response.Snapshot;

            if (wasKnown)
                NotifyIdentity(response.Snapshot.Identity, response, response.Snapshot);
        }

        private void NotifyIdentity(ConfigIdentity identity, WorldConfigNetworkResponse response, WorldConfigSnapshot authoritative)
        {
            var registrations = new List<Registration>();

            foreach (Registration registration in _registrations.Values)
                if (string.Equals(registration.ConsumerId, identity.OwnerId, StringComparison.Ordinal) && IsCurrent(registration))
                    registrations.Add(registration);

            for (int index = 0; index < registrations.Count; index++)
                Notify(registrations[index], identity, response, authoritative);
        }

        private void NotifySyntheticError(Registration registration, ConfigIdentity identity, WorldConfigNetworkOperation operation, string error)
        {
            string message = string.IsNullOrWhiteSpace(error) ? "World config operation failed." : error;
            var response = new WorldConfigNetworkResponse(0UL, operation, WorldConfigNetworkResponseKind.Error, 0UL, false, false, null, message);
            Notify(registration, identity, response, null);
        }

        private void Notify(Registration registration, ConfigIdentity identity, WorldConfigNetworkResponse response, WorldConfigSnapshot authoritative)
        {
            var payload = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "ConfigKey", identity.ConfigKey },
                { "RequestId", response.RequestId },
                { "Operation", response.Operation.ToString() },
                { "TriggeredBy", response.TriggeredBy },
                { "IsApplied", response.IsApplied },
                { "IsStale", response.IsStale },
                { "Error", response.Error },
                { "ServerIteration", authoritative == null ? null : (object)authoritative.ServerIteration },
                { "CurrentFile", authoritative == null ? null : authoritative.CurrentFile },
                { "Document", authoritative == null ? null : ConfigDocumentWireCodec.Encode(authoritative.Document) },
            };

            try
            {
                registration.Callback(payload);
            }
            catch (Exception exception)
            {
                Log(LogLevel.Warning, "World config consumer callback failed for '" + registration.ConsumerId + "/" + identity.ConfigKey + "'.", exception);
            }
        }

        private Registration GetRequiredRegistration(string consumerId, Guid registrationId)
        {
            if (string.IsNullOrWhiteSpace(consumerId))
                throw new ArgumentException("Consumer ID must not be empty.", nameof(consumerId));

            string normalizedConsumerId = consumerId.Trim();
            string key = RegistrationKey(normalizedConsumerId, registrationId);
            Registration registration;

            if (!_registrations.TryGetValue(key, out registration))
                throw new InvalidOperationException("World config consumer is not registered: " + normalizedConsumerId);

            _registry.GetStorage(normalizedConsumerId, registrationId);
            return registration;
        }

        private bool IsCurrent(Registration registration)
        {
            try
            {
                _registry.GetStorage(registration.ConsumerId, registration.RegistrationId);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private void Unregister(string key)
        {
            _registrations.Remove(key);

            for (int index = _queuedOpens.Count - 1; index >= 0; index--)
                if (string.Equals(_queuedOpens[index].RegistrationKey, key, StringComparison.Ordinal))
                    _queuedOpens.RemoveAt(index);

            string prefix = key + "\n";
            var openedRoutes = new List<string>();

            foreach (string route in _serverOpenedRoutes)
                if (route.StartsWith(prefix, StringComparison.Ordinal))
                    openedRoutes.Add(route);

            for (int index = 0; index < openedRoutes.Count; index++)
                _serverOpenedRoutes.Remove(openedRoutes[index]);
        }

        private static ConfigIdentity CreateIdentity(string consumerId, string configKey)
        {
            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));

            return new ConfigIdentity(consumerId, configKey.Trim());
        }

        private static string RegistrationKey(string consumerId, Guid registrationId)
        {
            if (registrationId == Guid.Empty)
                throw new ArgumentException("Registration ID must not be empty.", nameof(registrationId));

            return consumerId + "\n" + registrationId.ToString("D");
        }

        private static string ServerOpenedRouteKey(string registrationKey, ConfigIdentity identity)
            => registrationKey + "\n" + identity.OwnerId + "\n" + identity.ConfigKey;

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

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new InvalidOperationException("World config provider bridge has been disposed.");
        }

        private sealed class Registration
        {
            public Registration(string key, string consumerId, Guid registrationId, Action<IDictionary<string, object>> callback)
            {
                Key = key;
                ConsumerId = consumerId;
                RegistrationId = registrationId;
                Callback = callback;
            }

            public string Key { get; }
            public string ConsumerId { get; }
            public Guid RegistrationId { get; }
            public Action<IDictionary<string, object>> Callback { get; }
        }

        private sealed class PendingRoute
        {
            public PendingRoute(string registrationKey, ConfigIdentity identity)
            {
                RegistrationKey = registrationKey;
                Identity = identity;
            }

            public string RegistrationKey { get; }
            public ConfigIdentity Identity { get; }
        }

        private sealed class QueuedOpen
        {
            public QueuedOpen(string registrationKey, ConfigIdentity identity, string file, ConfigDocument defaults)
            {
                RegistrationKey = registrationKey;
                Identity = identity;
                File = file;
                Defaults = defaults;
            }

            public string RegistrationKey { get; }
            public ConfigIdentity Identity { get; }
            public string File { get; }
            public ConfigDocument Defaults { get; }
        }
    }
}