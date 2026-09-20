namespace Mz.Networking
{
    /// <summary>
    /// Extends a network transport with explicit delivery-mode selection.
    /// </summary>
    public interface INetworkDeliveryTransport : INetworkTransport
    {
        /// <summary>
        /// Sends an envelope to the authoritative server.
        /// </summary>
        void SendToServer(NetworkEnvelope envelope, NetworkDeliveryMode deliveryMode);

        /// <summary>
        /// Sends an envelope to one peer.
        /// </summary>
        void SendToPeer(NetworkEnvelope envelope, ulong peerId, NetworkDeliveryMode deliveryMode);

        /// <summary>
        /// Sends an envelope to all peers except one.
        /// </summary>
        void SendToOthers(NetworkEnvelope envelope, ulong excludedPeerId, NetworkDeliveryMode deliveryMode);

        /// <summary>
        /// Sends an envelope to all peers.
        /// </summary>
        void SendToEveryone(NetworkEnvelope envelope, NetworkDeliveryMode deliveryMode);
    }
}
