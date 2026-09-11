using System;
using System.Collections.Generic;
using System.Linq;

namespace Mz.Networking.SpaceEngineers
{
    /// <summary>
    /// Sends network envelopes over the currently active Space Engineers
    /// secure-message channel.
    /// </summary>
    public sealed class SpaceEngineersNetworkTransport : INetworkDeliveryTransport
    {
        private const int MaximumUnreliableMessageSize = 1024;

        private readonly ISpaceEngineersNetworkGateway _gateway;

        /// <summary>
        /// Creates a transport using the legacy unframed envelope wire.
        /// </summary>
        public SpaceEngineersNetworkTransport(ISpaceEngineersNetworkGateway gateway, ushort channelId)
        {
            if (gateway == null)
                throw new ArgumentNullException(nameof(gateway));

            _gateway = gateway;
            ChannelId = channelId;
        }

        /// <summary>
        /// Creates a transport over one secure-message channel and stable
        /// application network identity.
        /// </summary>
        public SpaceEngineersNetworkTransport(ISpaceEngineersNetworkGateway gateway, ushort channelId, string networkId)
            : this(gateway, channelId)
        {
            NetworkId = SpaceEngineersNetworkIdentity.Normalize(networkId);
            UsesWireIdentity = true;
        }

        /// <inheritdoc />
        public bool IsServer => _gateway.IsServer;

        /// <inheritdoc />
        public ulong LocalPeerId => _gateway.LocalPeerId;

        /// <summary>
        /// Gets the currently active secure-message channel.
        /// </summary>
        public ushort ChannelId { get; private set; }

        /// <summary>
        /// Gets whether outgoing packets use the versioned Mz.Networking wire.
        /// </summary>
        public bool UsesWireIdentity { get; }

        /// <summary>
        /// Gets the stable application network identity, or null for legacy
        /// unframed transport.
        /// </summary>
        public string NetworkId { get; }

        /// <inheritdoc />
        public void SendToServer(NetworkEnvelope envelope) 
            => SendToServer(envelope, NetworkDeliveryMode.Reliable);

        /// <inheritdoc />
        public void SendToServer(NetworkEnvelope envelope, NetworkDeliveryMode deliveryMode)
        {
            byte[] serialized = Serialize(envelope, deliveryMode);

            if (!SendToServer(serialized, deliveryMode))
                throw new InvalidOperationException("Space Engineers rejected the network message sent to the server.");
        }

        /// <inheritdoc />
        public void SendToPeer(NetworkEnvelope envelope, ulong peerId)
            => SendToPeer(envelope, peerId, NetworkDeliveryMode.Reliable);

        /// <inheritdoc />
        public void SendToPeer(NetworkEnvelope envelope, ulong peerId, NetworkDeliveryMode deliveryMode)
        {
            EnsureServer();

            byte[] serialized = Serialize(envelope, deliveryMode);

            SendSerializedToPeer(serialized, peerId, deliveryMode);
        }

        /// <inheritdoc />
        public void SendToOthers(NetworkEnvelope envelope, ulong excludedPeerId)
            => SendToOthers(envelope, excludedPeerId, NetworkDeliveryMode.Reliable);

        /// <inheritdoc />
        public void SendToOthers(NetworkEnvelope envelope, ulong excludedPeerId, NetworkDeliveryMode deliveryMode)
        {
            EnsureServer();

            byte[] serialized = Serialize(envelope, deliveryMode);
            var playerIds = new List<ulong>();

            _gateway.GetPlayerIds(playerIds);

            foreach (ulong peerId in playerIds.Where(peerId => peerId != LocalPeerId && peerId != excludedPeerId))
                SendSerializedToPeer(serialized, peerId, deliveryMode);
        }

        /// <inheritdoc />
        public void SendToEveryone(NetworkEnvelope envelope)
            => SendToEveryone(envelope, NetworkDeliveryMode.Reliable);

        /// <inheritdoc />
        public void SendToEveryone(NetworkEnvelope envelope, NetworkDeliveryMode deliveryMode)
        {
            EnsureServer();

            byte[] serialized = Serialize(envelope, deliveryMode);
            var playerIds = new List<ulong>();

            _gateway.GetPlayerIds(playerIds);

            foreach (ulong peerId in playerIds.Where(peerId => peerId != LocalPeerId))
                SendSerializedToPeer(serialized, peerId, deliveryMode);
        }

        internal void ChangeChannel(ushort channelId) 
            => ChannelId = channelId;

        private byte[] Serialize(NetworkEnvelope envelope, NetworkDeliveryMode deliveryMode)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            EnsureDeliveryMode(deliveryMode);

            byte[] serialized = _gateway.Serialize(envelope);

            if (serialized == null)
                throw new InvalidOperationException("The Space Engineers network gateway returned no serialized envelope.");

            if (UsesWireIdentity)
                serialized = SpaceEngineersNetworkWireCodec.Encode(NetworkId, serialized);

            if (deliveryMode == NetworkDeliveryMode.Unreliable && serialized.Length > MaximumUnreliableMessageSize)
                throw new InvalidOperationException("An unreliable Space Engineers network message cannot exceed 1024 bytes.");

            return serialized;
        }

        private bool SendToServer(byte[] serialized, NetworkDeliveryMode deliveryMode)
        {
            var deliveryGateway = _gateway as ISpaceEngineersNetworkDeliveryGateway;

            if (deliveryGateway != null)
                return deliveryGateway.SendToServer(ChannelId, serialized, deliveryMode == NetworkDeliveryMode.Reliable);

            EnsureLegacyGatewaySupports(deliveryMode);
            return _gateway.SendToServer(ChannelId, serialized);
        }

        private void SendSerializedToPeer(byte[] serialized, ulong peerId, NetworkDeliveryMode deliveryMode)
        {
            var deliveryGateway = _gateway as ISpaceEngineersNetworkDeliveryGateway;
            bool sent;

            if (deliveryGateway != null)
                sent = deliveryGateway.SendToPeer(ChannelId, serialized, peerId, deliveryMode == NetworkDeliveryMode.Reliable);
            else
            {
                EnsureLegacyGatewaySupports(deliveryMode);
                sent = _gateway.SendToPeer(ChannelId, serialized, peerId);
            }

            if (!sent)
                throw new InvalidOperationException($"Space Engineers rejected the network message sent to peer {peerId}.");
        }

        private static void EnsureDeliveryMode(NetworkDeliveryMode deliveryMode)
        {
            if (deliveryMode < NetworkDeliveryMode.Reliable || deliveryMode > NetworkDeliveryMode.Unreliable)
                throw new ArgumentException("The delivery mode is outside the supported range.", nameof(deliveryMode));
        }

        private static void EnsureLegacyGatewaySupports(NetworkDeliveryMode deliveryMode)
        {
            if (deliveryMode != NetworkDeliveryMode.Reliable)
                throw new NotSupportedException("The configured Space Engineers network gateway does not support explicit unreliable delivery.");
        }

        private void EnsureServer()
        {
            if (!IsServer)
                throw new InvalidOperationException("Only the authoritative server can send network messages directly to players.");
        }
    }
}
