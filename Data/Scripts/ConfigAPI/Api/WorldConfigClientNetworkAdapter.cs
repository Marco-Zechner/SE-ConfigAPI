using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Domain;
using Mz.Networking;

namespace MarcoZechner.ConfigAPI.Api
{
    public sealed class WorldConfigClientNetworkAdapter : IDisposable
    {
        private readonly NetworkEndpoint _endpoint;
        private readonly INetworkTransport _transport;
        private readonly IWorldConfigBootstrapStore _bootstrapStore;
        private readonly Dictionary<ConfigIdentity, WorldConfigClientState> _states = new Dictionary<ConfigIdentity, WorldConfigClientState>();
        private readonly Dictionary<ulong, PendingRequest> _pendingRequests = new Dictionary<ulong, PendingRequest>();
        private readonly HashSet<ConfigIdentity> _provisionalBootstraps = new HashSet<ConfigIdentity>();
        private readonly HashSet<ConfigIdentity> _editedBootstrapDrafts = new HashSet<ConfigIdentity>();
        private readonly NetworkMessageSubscription _responseSubscription;
        private ulong _nextRequestId = 1UL;
        private bool _isDisposed;

        public WorldConfigClientNetworkAdapter(NetworkEndpoint endpoint, INetworkTransport transport)
            : this(endpoint, transport, NullWorldConfigBootstrapStore.Instance) { }

        public WorldConfigClientNetworkAdapter(NetworkEndpoint endpoint, INetworkTransport transport, IWorldConfigBootstrapStore bootstrapStore)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));
            if (transport == null)
                throw new ArgumentNullException(nameof(transport));
            if (bootstrapStore == null)
                throw new ArgumentNullException(nameof(bootstrapStore));
            if (transport.IsServer)
                throw new ArgumentException("World config client networking requires a client transport.", nameof(transport));

            _endpoint = endpoint;
            _transport = transport;
            _bootstrapStore = bootstrapStore;
            _responseSubscription = endpoint.RegisterHandler(WorldConfigServerNetworkAdapter.ResponseMessageType, HandleResponse);
        }

        public int PendingRequestCount => _pendingRequests.Count;
        public ulong LocalPeerId => _transport.LocalPeerId;
        public event Action<WorldConfigNetworkResponse> ResponseReceived;

        public ulong Open(string consumerId, string configKey, ConfigDocument defaults)
        {
            ThrowIfDisposed();

            ConfigIdentity identity = CreateIdentity(consumerId, configKey);
            if (defaults == null)
                throw new ArgumentNullException(nameof(defaults));

            ulong requestId = AllocateRequestId();
            var request = new WorldConfigNetworkRequest(requestId, identity.OwnerId, identity.ConfigKey, WorldConfigNetworkOperation.Open, 0UL, null, defaults, null);
            Send(request, new PendingRequest(identity, WorldConfigNetworkOperation.Open));
            return requestId;
        }

        public bool TrySeedBootstrap(string consumerId, string configKey, out WorldConfigSnapshot snapshot)
        {
            ThrowIfDisposed();

            ConfigIdentity identity = CreateIdentity(consumerId, configKey);
            snapshot = null;

            if (_states.ContainsKey(identity))
                return false;

            if (!_bootstrapStore.TryRead(identity, out snapshot) || snapshot == null || !identity.Equals(snapshot.Identity))
            {
                snapshot = null;
                return false;
            }

            _states[identity] = WorldConfigClientState.Create(snapshot);
            _provisionalBootstraps.Add(identity);
            _editedBootstrapDrafts.Remove(identity);
            return true;
        }

        public ulong Apply(string consumerId, string configKey)
        {
            ThrowIfDisposed();

            ConfigIdentity identity = CreateIdentity(consumerId, configKey);
            WorldConfigClientState state = GetRequiredState(identity);
            ulong requestId = AllocateRequestId();
            var request = new WorldConfigNetworkRequest(requestId, identity.OwnerId, identity.ConfigKey, WorldConfigNetworkOperation.Apply, state.Authoritative.Revision, null, null, state.Draft);
            Send(request, new PendingRequest(identity, WorldConfigNetworkOperation.Apply));
            return requestId;
        }

        public ulong Save(string consumerId, string configKey)
        {
            ThrowIfDisposed();

            ConfigIdentity identity = CreateIdentity(consumerId, configKey);
            WorldConfigClientState state = GetRequiredState(identity);
            ulong requestId = AllocateRequestId();
            var request = new WorldConfigNetworkRequest(requestId, identity.OwnerId, identity.ConfigKey, WorldConfigNetworkOperation.Save, state.Authoritative.Revision, null, null, null);
            Send(request, new PendingRequest(identity, WorldConfigNetworkOperation.Save));
            return requestId;
        }

        public ulong Reload(string consumerId, string configKey)
        {
            ThrowIfDisposed();

            ConfigIdentity identity = CreateIdentity(consumerId, configKey);
            WorldConfigClientState state = GetRequiredState(identity);
            ulong requestId = AllocateRequestId();
            var request = new WorldConfigNetworkRequest(requestId, identity.OwnerId, identity.ConfigKey, WorldConfigNetworkOperation.Reload, state.Authoritative.Revision, null, null, null);
            Send(request, new PendingRequest(identity, WorldConfigNetworkOperation.Reload));
            return requestId;
        }

        public ulong Load(string consumerId, string configKey, string variant)
        {
            ThrowIfDisposed();
            RequireVariant(variant);

            ConfigIdentity identity = CreateIdentity(consumerId, configKey);
            WorldConfigClientState state = GetRequiredState(identity);
            ulong requestId = AllocateRequestId();
            var request = new WorldConfigNetworkRequest(requestId, identity.OwnerId, identity.ConfigKey, WorldConfigNetworkOperation.Load, state.Authoritative.Revision, variant, null, null);
            Send(request, new PendingRequest(identity, WorldConfigNetworkOperation.Load));
            return requestId;
        }

        public ulong SaveAs(string consumerId, string configKey, string variant)
        {
            ThrowIfDisposed();
            RequireVariant(variant);

            ConfigIdentity identity = CreateIdentity(consumerId, configKey);
            WorldConfigClientState state = GetRequiredState(identity);
            ulong requestId = AllocateRequestId();
            var request = new WorldConfigNetworkRequest(requestId, identity.OwnerId, identity.ConfigKey, WorldConfigNetworkOperation.SaveAs, state.Authoritative.Revision, variant, null, null);
            Send(request, new PendingRequest(identity, WorldConfigNetworkOperation.SaveAs));
            return requestId;
        }

        public ulong ListVariants(string consumerId, string configKey)
        {
            ThrowIfDisposed();

            ConfigIdentity identity = CreateIdentity(consumerId, configKey);
            GetRequiredState(identity);
            ulong requestId = AllocateRequestId();
            var request = new WorldConfigNetworkRequest(requestId, identity.OwnerId, identity.ConfigKey, WorldConfigNetworkOperation.ListVariants, 0UL, null, null, null);
            Send(request, new PendingRequest(identity, WorldConfigNetworkOperation.ListVariants));
            return requestId;
        }

        public WorldConfigClientState SetDraft(string consumerId, string configKey, ConfigDocument draft)
        {
            ThrowIfDisposed();

            if (draft == null)
                throw new ArgumentNullException(nameof(draft));

            ConfigIdentity identity = CreateIdentity(consumerId, configKey);
            WorldConfigClientState state = GetRequiredState(identity).WithDraft(draft);
            _states[identity] = state;

            if (_provisionalBootstraps.Contains(identity))
                _editedBootstrapDrafts.Add(identity);

            return state;
        }

        public bool TryGetState(string consumerId, string configKey, out WorldConfigClientState state)
        {
            ThrowIfDisposed();
            return _states.TryGetValue(CreateIdentity(consumerId, configKey), out state);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _responseSubscription.Dispose();
            _pendingRequests.Clear();
            _states.Clear();
            _provisionalBootstraps.Clear();
            _editedBootstrapDrafts.Clear();
        }

        private void Send(WorldConfigNetworkRequest request, PendingRequest pending)
        {
            _pendingRequests.Add(request.RequestId, pending);

            try
            {
                _endpoint.SendToServer(WorldConfigServerNetworkAdapter.RequestMessageType, WorldConfigNetworkCodec.EncodeRequest(request));
            }
            catch
            {
                _pendingRequests.Remove(request.RequestId);
                throw;
            }
        }

        private void HandleResponse(NetworkReceiveContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.IsServer)
                throw new InvalidOperationException("World config client responses must be handled by a client endpoint.");
            if (!context.TransportSenderIsServer)
                throw new InvalidOperationException("World config client responses must come from the authoritative server.");

            WorldConfigNetworkResponse response = WorldConfigNetworkCodec.DecodeResponse(context.Envelope.Payload);
            bool isOwnResponse = response.TriggeredBy == _transport.LocalPeerId;

            PendingRequest pending = null;
            if (isOwnResponse && _pendingRequests.TryGetValue(response.RequestId, out pending))
            {
                if (pending.Operation != response.Operation)
                    throw new InvalidOperationException("World config response operation does not match the pending request.");
                if (response.Snapshot != null && !pending.Identity.Equals(response.Snapshot.Identity))
                    throw new InvalidOperationException("World config response snapshot identity does not match the pending request.");
            }

            ApplySnapshot(response.Snapshot, pending);

            if (isOwnResponse && pending != null)
                _pendingRequests.Remove(response.RequestId);

            RaiseResponseReceived(response);
        }

        private void RaiseResponseReceived(WorldConfigNetworkResponse response)
        {
            Action<WorldConfigNetworkResponse> handlers = ResponseReceived;
            if (handlers != null)
                handlers(response);
        }

        private void ApplySnapshot(WorldConfigSnapshot snapshot, PendingRequest pending)
        {
            if (snapshot == null)
                return;

            WorldConfigClientState state;
            if (_states.TryGetValue(snapshot.Identity, out state))
            {
                bool reconcilesBootstrap = pending != null &&
                                           pending.Operation == WorldConfigNetworkOperation.Open &&
                                           pending.Identity.Equals(snapshot.Identity) &&
                                           _provisionalBootstraps.Contains(snapshot.Identity);

                if (reconcilesBootstrap)
                {
                    _states[snapshot.Identity] = _editedBootstrapDrafts.Contains(snapshot.Identity)
                        ? state.ApplyAuthoritative(snapshot)
                        : WorldConfigClientState.Create(snapshot);

                    _provisionalBootstraps.Remove(snapshot.Identity);
                    _editedBootstrapDrafts.Remove(snapshot.Identity);
                    return;
                }

                if (snapshot.Revision < state.Authoritative.Revision)
                    return;

                _states[snapshot.Identity] = state.ApplyAuthoritative(snapshot);
                return;
            }

            if (pending != null && pending.Operation == WorldConfigNetworkOperation.Open && pending.Identity.Equals(snapshot.Identity))
                _states.Add(snapshot.Identity, WorldConfigClientState.Create(snapshot));
        }

        private WorldConfigClientState GetRequiredState(ConfigIdentity identity)
        {
            WorldConfigClientState state;
            if (!_states.TryGetValue(identity, out state))
                throw new InvalidOperationException("World config has not been opened: " + identity.OwnerId + "/" + identity.ConfigKey);

            return state;
        }

        private ulong AllocateRequestId()
        {
            ulong start = _nextRequestId;

            do
            {
                ulong candidate = _nextRequestId++;
                if (_nextRequestId == 0UL)
                    _nextRequestId = 1UL;

                if (candidate != 0UL && !_pendingRequests.ContainsKey(candidate))
                    return candidate;
            }
            while (_nextRequestId != start);

            throw new InvalidOperationException("No World config request IDs are available.");
        }

        private static ConfigIdentity CreateIdentity(string consumerId, string configKey)
        {
            if (string.IsNullOrWhiteSpace(consumerId))
                throw new ArgumentException("Consumer ID must not be empty.", nameof(consumerId));
            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));

            string normalizedConfigKey = configKey.Trim();
            if (normalizedConfigKey.IndexOf('.') >= 0)
                throw new ArgumentException("Config key must not contain '.'.", nameof(configKey));

            return new ConfigIdentity(consumerId.Trim(), normalizedConfigKey);
        }

        private static void RequireVariant(string variant)
        {
            if (string.IsNullOrWhiteSpace(variant))
                throw new ArgumentException("Variant must not be empty.", nameof(variant));
            if (variant.Trim().IndexOf('.') >= 0)
                throw new ArgumentException("Variant must not contain '.'.", nameof(variant));
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new InvalidOperationException("World config client network adapter has been disposed.");
        }

        private sealed class PendingRequest
        {
            public PendingRequest(ConfigIdentity identity, WorldConfigNetworkOperation operation)
            {
                Identity = identity;
                Operation = operation;
            }

            public ConfigIdentity Identity { get; }
            public WorldConfigNetworkOperation Operation { get; }
        }
    }
}