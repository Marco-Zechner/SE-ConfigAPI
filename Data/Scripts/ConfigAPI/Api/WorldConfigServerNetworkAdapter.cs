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
            byte[] payload = WorldConfigNetworkCodec.EncodeResponse(response);

            if (response.Kind == WorldConfigNetworkResponseKind.Snapshot && response.IsApplied)
            {
                var envelope = new NetworkEnvelope(ResponseMessageType, _transport.LocalPeerId, false, payload);
                _transport.SendToEveryone(envelope);
                return;
            }

            _endpoint.SendToPlayer(ResponseMessageType, payload, requesterId);
        }
    }
}