namespace Mz.Networking
{
    /// <summary>
    /// Sends validated network envelopes through a concrete multiplayer
    /// transport.
    /// </summary>
    public interface INetworkTransport
    {
        /// <summary>
        /// Gets whether the local endpoint is the authoritative server.
        /// </summary>
        bool IsServer { get; }

        /// <summary>
        /// Gets the local transport peer identity.
        /// </summary>
        ulong LocalPeerId { get; }

        /// <summary>
        /// Sends an envelope to the authoritative server.
        /// </summary>
        void SendToServer(NetworkEnvelope envelope);

        /// <summary>
        /// Sends an envelope to one peer.
        /// </summary>
        void SendToPeer(NetworkEnvelope envelope, ulong peerId);

        /// <summary>
        /// Sends an envelope to all peers except one.
        /// </summary>
        void SendToOthers(NetworkEnvelope envelope, ulong excludedPeerId);

        /// <summary>
        /// Sends an envelope to all peers.
        /// </summary>
        void SendToEveryone(NetworkEnvelope envelope);
    }
}