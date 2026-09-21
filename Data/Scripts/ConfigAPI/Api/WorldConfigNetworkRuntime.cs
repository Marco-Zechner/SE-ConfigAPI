using System;
using MarcoZechner.ConfigAPI.V2.Persistence;
using Mz.Networking;

namespace MarcoZechner.ConfigAPI.V2.Api
{
    public sealed class WorldConfigNetworkRuntime : IDisposable
    {
        private bool _isDisposed;

        public WorldConfigNetworkRuntime(NetworkEndpoint endpoint, INetworkTransport transport, ConfigConsumerRegistrationRegistry registry, IConfigClock clock, IWorldConfigAuthorization authorization)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));
            if (transport == null)
                throw new ArgumentNullException(nameof(transport));
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            if (clock == null)
                throw new ArgumentNullException(nameof(clock));
            if (authorization == null)
                throw new ArgumentNullException(nameof(authorization));

            IsServer = transport.IsServer;

            if (IsServer)
            {
                ServerService = new WorldConfigServerService(registry, clock);
                ServerAdapter = new WorldConfigServerNetworkAdapter(endpoint, transport, new WorldConfigServerRequestHandler(ServerService, authorization));
            }
            else
            {
                ClientAdapter = new WorldConfigClientNetworkAdapter(endpoint, transport);
            }
        }

        public bool IsServer { get; private set; }
        public WorldConfigServerService ServerService { get; private set; }
        public WorldConfigServerNetworkAdapter ServerAdapter { get; private set; }
        public WorldConfigClientNetworkAdapter ClientAdapter { get; private set; }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            if (ClientAdapter != null)
                ClientAdapter.Dispose();

            if (ServerAdapter != null)
                ServerAdapter.Dispose();
        }
    }
}