namespace Mz.Networking.SpaceEngineers
{
    /// <summary>
    /// Extends a Space Engineers gateway with explicit delivery reliability.
    /// </summary>
    public interface ISpaceEngineersNetworkDeliveryGateway : ISpaceEngineersNetworkGateway
    {
        /// <summary>
        /// Sends serialized data to the authoritative server.
        /// </summary>
        bool SendToServer(ushort channelId, byte[] serialized, bool reliable);

        /// <summary>
        /// Sends serialized data to one multiplayer peer.
        /// </summary>
        bool SendToPeer(ushort channelId, byte[] serialized, ulong peerId, bool reliable);
    }
}
