using System;
using Mz.Networking;

namespace MarcoZechner.ConfigAPI.V2.Api
{
    public sealed class WorldConfigServerNetworkAdapter : IDisposable
    {
        public const string RequestMessageType = "configapi.world.request";
        public const string ResponseMessageType = "configapi.world.response";

        private readonly NetworkEndpoint _endpoint;
        private readonly INetworkTransport _transport;
        private readonly WorldConfigServerRequestHandler _handler;
        private readonly NetworkMessageSubscription _requestSubscription;
        private bool _isDisposed;

        public WorldConfigServerNetworkAdapter(NetworkEndpoint endpoint, INetworkTransport transport, WorldConfigServerRequestHandler handler)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));
            if (transport == null)
                throw new ArgumentNullException(nameof(transport));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            if (!transport.IsServer)
                throw new ArgumentException("World config server networking requires an authoritative server transport.", nameof(transport));

            _endpoint = endpoint;
            _transport = transport;
            _handler = handler;
            _requestSubscription = endpoint.RegisterHandler(RequestMessageType, HandleRequest);
        }

        public ulong LocalPeerId => _transport.LocalPeerId;
        public event Action<WorldConfigNetworkResponse> ResponseSent;

        public void BroadcastChangedResponse(WorldConfigNetworkResponse response)
        {
            ThrowIfDisposed();

            if (response == null)
                throw new ArgumentNullException(nameof(response));
            if (response.Kind != WorldConfigNetworkResponseKind.Snapshot || !response.IsChanged)
                throw new ArgumentException("Only changed authoritative snapshot responses can be broadcast.", nameof(response));

            byte[] payload = WorldConfigNetworkCodec.EncodeResponse(response);
            var envelope = new NetworkEnvelope(ResponseMessageType, _transport.LocalPeerId, false, payload);
            _transport.SendToEveryone(envelope);
            RaiseResponseSent(response);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _requestSubscription.Dispose();
        }

        private void HandleRequest(NetworkReceiveContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!context.IsServer)
                throw new InvalidOperationException("World config requests must be handled by the authoritative server.");

            WorldConfigNetworkRequest request = WorldConfigNetworkCodec.DecodeRequest(context.Envelope.Payload);
            ulong requesterId = context.Envelope.OriginalSenderId;
            WorldConfigNetworkResponse response = _handler.Handle(requesterId, request);

            if (response.Kind == WorldConfigNetworkResponseKind.Snapshot && response.IsChanged)
            {
                BroadcastChangedResponse(response);
                return;
            }

            byte[] payload = WorldConfigNetworkCodec.EncodeResponse(response);
            _endpoint.SendToPlayer(ResponseMessageType, payload, requesterId);
            RaiseResponseSent(response);
        }

        private void RaiseResponseSent(WorldConfigNetworkResponse response)
        {
            Action<WorldConfigNetworkResponse> handlers = ResponseSent;
            if (handlers == null)
                return;

            foreach (Action<WorldConfigNetworkResponse> handler in handlers.GetInvocationList())
                try
                {
                    handler(response);
                }
                catch
                {
                }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new InvalidOperationException("World config server network adapter has been disposed.");
        }
    }
}