using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace Mz.Networking.SpaceEngineers
{
    /// <summary>
    /// Uses the active Space Engineers ModAPI multiplayer and binary
    /// serialization services.
    /// </summary>
    public sealed class SpaceEngineersNetworkGateway : ISpaceEngineersNetworkDeliveryGateway, 
                                                       ISpaceEngineersNetworkDiagnosticGateway,
                                                       ISpaceEngineersNetworkSchedulingGateway
    {
        /// <inheritdoc />
        public bool IsServer => GetMultiplayer().IsServer;

        /// <inheritdoc />
        public ulong LocalPeerId => GetMultiplayer().MyId;

        /// <inheritdoc />
        public void RegisterSecureMessageHandler(ushort channelId, Action<ushort, byte[], ulong, bool> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            GetMultiplayer().RegisterSecureMessageHandler(channelId, handler);
        }

        /// <inheritdoc />
        public void UnregisterSecureMessageHandler(ushort channelId, Action<ushort, byte[], ulong, bool> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            GetMultiplayer().UnregisterSecureMessageHandler(channelId, handler);
        }

        /// <inheritdoc />
        public void InvokeOnGameThread(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            GetUtilities().InvokeOnGameThread(action);
        }

        /// <inheritdoc />
        public byte[] Serialize(NetworkEnvelope envelope)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            var wire = new NetworkEnvelopeWire
            {
                MessageType = envelope.MessageType,
                OriginalSenderId = envelope.OriginalSenderId,
                IsRelay = envelope.IsRelay,
                Payload = envelope.Payload
            };

            return GetUtilities().SerializeToBinary(wire);
        }

        /// <inheritdoc />
        public NetworkEnvelope Deserialize(byte[] serialized)
        {
            if (serialized == null)
                throw new ArgumentNullException(nameof(serialized));

            var wire = GetUtilities().SerializeFromBinary<NetworkEnvelopeWire>(serialized);

            if (wire == null)
                throw new InvalidOperationException("The serialized network envelope was empty.");

            if (wire.Payload == null)
                throw new InvalidOperationException("The serialized network envelope had no payload.");

            return new NetworkEnvelope(wire.MessageType, wire.OriginalSenderId, wire.IsRelay, wire.Payload);
        }

        /// <inheritdoc />
        public bool SendToServer(ushort channelId, byte[] serialized)
            => SendToServer(channelId, serialized, true);

        /// <inheritdoc />
        public bool SendToServer(ushort channelId, byte[] serialized, bool reliable)
        {
            if (serialized == null)
                throw new ArgumentNullException(nameof(serialized));

            return GetMultiplayer().SendMessageToServer(channelId, serialized, reliable);
        }

        /// <inheritdoc />
        public bool SendToPeer(ushort channelId, byte[] serialized, ulong peerId)
            => SendToPeer(channelId, serialized, peerId, true);

        /// <inheritdoc />
        public bool SendToPeer(ushort channelId, byte[] serialized, ulong peerId, bool reliable)
        {
            if (serialized == null)
                throw new ArgumentNullException(nameof(serialized));

            return GetMultiplayer().SendMessageTo(channelId, serialized, peerId, reliable);
        }

        /// <inheritdoc />
        public void GetPlayerIds(List<ulong> playerIds)
        {
            if (playerIds == null)
                throw new ArgumentNullException(nameof(playerIds));

            IMyPlayerCollection playerCollection = GetMultiplayer().Players;

            if (playerCollection == null)
                throw new InvalidOperationException("Space Engineers player information is unavailable.");

            var players = new List<IMyPlayer>();
            playerCollection.GetPlayers(players);

            playerIds.AddRange(from player in players where player != null select player.SteamUserId);
        }

        /// <inheritdoc />
        public bool TryDeserializeString(byte[] serialized, out string value)
        {
            value = null;

            if (serialized == null)
                return false;

            try
            {
                value = GetUtilities().SerializeFromBinary<string>(serialized);

                return value != null;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        private static IMyMultiplayer GetMultiplayer()
        {
            IMyMultiplayer multiplayer = MyAPIGateway.Multiplayer;

            if (multiplayer == null)
                throw new InvalidOperationException(
                    "Space Engineers multiplayer is unavailable. Use networking during the active session lifecycle.");

            return multiplayer;
        }

        private static IMyUtilities GetUtilities()
        {
            IMyUtilities utilities = MyAPIGateway.Utilities;

            if (utilities == null)
                throw new InvalidOperationException(
                    "Space Engineers utilities are unavailable. Use networking during the active session lifecycle.");

            return utilities;
        }
    }
}
