using System;
using System.Collections.Generic;

namespace Mz.Networking.SpaceEngineers
{
    /// <summary>
    /// Provides the Space Engineers multiplayer operations required by the
    /// networking adapter.
    /// </summary>
    public interface ISpaceEngineersNetworkGateway
    {
        /// <summary>
        /// Gets whether the local game instance is the authoritative server.
        /// </summary>
        bool IsServer { get; }

        /// <summary>
        /// Gets the local multiplayer peer identity.
        /// </summary>
        ulong LocalPeerId { get; }

        /// <summary>
        /// Registers a secure multiplayer message handler.
        /// </summary>
        void RegisterSecureMessageHandler(ushort channelId, Action<ushort, byte[], ulong, bool> handler);

        /// <summary>
        /// Removes an exact secure multiplayer message handler registration.
        /// </summary>
        void UnregisterSecureMessageHandler(ushort channelId, Action<ushort, byte[], ulong, bool> handler);

        /// <summary>
        /// Serializes a transport-independent network envelope.
        /// </summary>
        byte[] Serialize(NetworkEnvelope envelope);

        /// <summary>
        /// Deserializes a transport-independent network envelope.
        /// </summary>
        NetworkEnvelope Deserialize(byte[] serialized);

        /// <summary>
        /// Sends serialized data to the authoritative server.
        /// </summary>
        bool SendToServer(ushort channelId, byte[] serialized);

        /// <summary>
        /// Sends serialized data to one multiplayer peer.
        /// </summary>
        bool SendToPeer(ushort channelId, byte[] serialized, ulong peerId);

        /// <summary>
        /// Appends the currently connected player peer identities.
        /// </summary>
        void GetPlayerIds(List<ulong> playerIds);
    }
}
