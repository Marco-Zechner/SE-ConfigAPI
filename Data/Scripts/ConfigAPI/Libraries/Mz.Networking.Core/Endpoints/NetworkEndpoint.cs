using System;

namespace Mz.Networking
{
    /// <summary>
    /// Exposes transport-independent network send, receive, registration, and
    /// relay operations to a mod.
    /// </summary>
    public sealed class NetworkEndpoint
    {
        private readonly INetworkTransport _transport;
        private readonly NetworkMessageDispatcher _dispatcher;

        /// <summary>
        /// Creates a network endpoint over the supplied transport.
        /// </summary>
        public NetworkEndpoint(INetworkTransport transport)
        {
            if (transport == null)
                throw new ArgumentNullException(nameof(transport));

            _transport = transport;
            _dispatcher = new NetworkMessageDispatcher();
        }

        /// <summary>
        /// Registers one application message handler.
        /// </summary>
        public NetworkMessageSubscription RegisterHandler(string messageType, Action<NetworkReceiveContext> handler)
            => _dispatcher.RegisterHandler(messageType, handler);

        /// <summary>
        /// Sends an application message reliably to the authoritative server.
        /// </summary>
        public void SendToServer(string messageType, byte[] payload)
            => SendToServer(messageType, payload, NetworkDeliveryMode.Reliable);

        /// <summary>
        /// Sends an application message to the authoritative server.
        /// </summary>
        public void SendToServer(string messageType, byte[] payload, NetworkDeliveryMode deliveryMode)
        {
            EnsureDeliveryMode(deliveryMode);

            var envelope = new NetworkEnvelope(messageType, _transport.LocalPeerId, false, payload);

            if (_transport.IsServer)
            {
                NetworkReceiveContext ignored;

                Receive(envelope, _transport.LocalPeerId, true, out ignored);
                return;
            }

            SendToServer(envelope, deliveryMode);
        }

        /// <summary>
        /// Sends an application message reliably from the server to one peer.
        /// </summary>
        public void SendToPlayer(string messageType, byte[] payload, ulong peerId)
            => SendToPlayer(messageType, payload, peerId, NetworkDeliveryMode.Reliable);

        /// <summary>
        /// Sends an application message from the server to one peer.
        /// </summary>
        public void SendToPlayer(string messageType, byte[] payload, ulong peerId, NetworkDeliveryMode deliveryMode)
        {
            EnsureServer();
            EnsureDeliveryMode(deliveryMode);

            var envelope = new NetworkEnvelope(messageType, _transport.LocalPeerId, false, payload);

            SendToPeer(envelope, peerId, deliveryMode);
        }

        /// <summary>
        /// Processes an envelope received from the concrete transport.
        /// </summary>
        public bool Receive(NetworkEnvelope envelope, ulong transportSenderId, bool transportSenderIsServer, out NetworkReceiveContext context)
            => Receive(envelope, transportSenderId, transportSenderIsServer, null, out context);

        /// <summary>
        /// Processes an envelope and observes exceptions thrown directly by its
        /// registered application handler before the original exception is rethrown.
        /// </summary>
        public bool Receive(
            NetworkEnvelope envelope, ulong transportSenderId, bool transportSenderIsServer,
            Action<Exception> handlerFailureObserver, out NetworkReceiveContext context)
        {
            if (!_transport.IsServer && !transportSenderIsServer)
                throw new InvalidOperationException("A client can only accept network messages sent by the authoritative server.");

            bool dispatched = _dispatcher.TryDispatch(envelope, transportSenderId, _transport.IsServer, transportSenderIsServer, 
                                                      handlerFailureObserver, out context);

            if (!dispatched)
                return false;

            if (_transport.IsServer)
                Relay(context);

            return true;
        }

        private void Relay(NetworkReceiveContext context)
        {
            if (context.RelayMode == NetworkRelayMode.None)
                return;

            NetworkEnvelope relayEnvelope = context.Envelope.WithRelay(true);

            switch (context.RelayMode)
            {
                case NetworkRelayMode.ToOthers:
                    SendToOthers(relayEnvelope, context.Envelope.OriginalSenderId, context.RelayDeliveryMode);
                    break;

                case NetworkRelayMode.ToEveryone:
                    SendToEveryone(relayEnvelope, context.RelayDeliveryMode);
                    break;

                case NetworkRelayMode.ReturnToSender:
                    SendToPeer(relayEnvelope, context.Envelope.OriginalSenderId, context.RelayDeliveryMode);
                    break;

                default:
                    throw new InvalidOperationException("The receive handler selected an unsupported relay mode.");
            }
        }

        private void SendToServer(NetworkEnvelope envelope, NetworkDeliveryMode deliveryMode)
        {
            var deliveryTransport = _transport as INetworkDeliveryTransport;

            if (deliveryTransport != null)
            {
                deliveryTransport.SendToServer(envelope, deliveryMode);
                return;
            }

            EnsureLegacyTransportSupports(deliveryMode);
            _transport.SendToServer(envelope);
        }

        private void SendToPeer(NetworkEnvelope envelope, ulong peerId, NetworkDeliveryMode deliveryMode)
        {
            var deliveryTransport = _transport as INetworkDeliveryTransport;

            if (deliveryTransport != null)
            {
                deliveryTransport.SendToPeer(envelope, peerId, deliveryMode);
                return;
            }

            EnsureLegacyTransportSupports(deliveryMode);
            _transport.SendToPeer(envelope, peerId);
        }

        private void SendToOthers(NetworkEnvelope envelope, ulong excludedPeerId, NetworkDeliveryMode deliveryMode)
        {
            var deliveryTransport = _transport as INetworkDeliveryTransport;

            if (deliveryTransport != null)
            {
                deliveryTransport.SendToOthers(envelope, excludedPeerId, deliveryMode);
                return;
            }

            EnsureLegacyTransportSupports(deliveryMode);
            _transport.SendToOthers(envelope, excludedPeerId);
        }

        private void SendToEveryone(NetworkEnvelope envelope, NetworkDeliveryMode deliveryMode)
        {
            var deliveryTransport = _transport as INetworkDeliveryTransport;

            if (deliveryTransport != null)
            {
                deliveryTransport.SendToEveryone(envelope, deliveryMode);
                return;
            }

            EnsureLegacyTransportSupports(deliveryMode);
            _transport.SendToEveryone(envelope);
        }

        private static void EnsureDeliveryMode(NetworkDeliveryMode deliveryMode)
        {
            if (deliveryMode < NetworkDeliveryMode.Reliable || deliveryMode > NetworkDeliveryMode.Unreliable)
                throw new ArgumentException("The delivery mode is outside the supported range.", nameof(deliveryMode));
        }

        private static void EnsureLegacyTransportSupports(NetworkDeliveryMode deliveryMode)
        {
            if (deliveryMode != NetworkDeliveryMode.Reliable)
                throw new NotSupportedException("The configured network transport does not support explicit unreliable delivery.");
        }

        private void EnsureServer()
        {
            if (!_transport.IsServer)
                throw new InvalidOperationException("Only the authoritative server can send messages directly to another player.");
        }
    }
}
