using System;
using System.Text;

namespace Mz.Networking.SpaceEngineers
{
    internal enum SpaceEngineersNetworkWireStatus
    {
        Success = 0,
        ForeignPacket = 1,
        NetworkMismatch = 2,
        UnsupportedWireVersion = 3,
        MalformedWirePacket = 4
    }

    internal sealed class SpaceEngineersNetworkWireDecodeResult
    {
        public SpaceEngineersNetworkWireStatus Status { get; }

        public byte[] SerializedEnvelope { get; }

        public string ObservedNetworkId { get; }

        public Exception Exception { get; }

        private SpaceEngineersNetworkWireDecodeResult(
            SpaceEngineersNetworkWireStatus status, byte[] serializedEnvelope, string observedNetworkId, Exception exception)
        {
            Status = status;
            SerializedEnvelope = serializedEnvelope;
            ObservedNetworkId = observedNetworkId;
            Exception = exception;
        }

        public static SpaceEngineersNetworkWireDecodeResult Success(byte[] serializedEnvelope, string observedNetworkId)
            => new SpaceEngineersNetworkWireDecodeResult(
                SpaceEngineersNetworkWireStatus.Success, serializedEnvelope, observedNetworkId, null);

        public static SpaceEngineersNetworkWireDecodeResult Failure(
            SpaceEngineersNetworkWireStatus status, string observedNetworkId, string message)
            => new SpaceEngineersNetworkWireDecodeResult(status, null, observedNetworkId, new InvalidOperationException(message));
    }

    internal static class SpaceEngineersNetworkWireCodec
    {
        private const byte CurrentVersion = 1;
        private const int MagicLength = 4;
        private const int VersionOffset = MagicLength;
        private const int NetworkIdLengthOffset = VersionOffset + 1;
        private const int FixedHeaderLength = NetworkIdLengthOffset + 2;
        private const int PayloadLengthSize = 4;

        private static readonly byte[] _magic = { 0x4D, 0x5A, 0x4E, 0x57 };

        private static readonly UTF8Encoding _strictUtf8 = new UTF8Encoding(false, true);

        public static byte[] Encode(string networkId, byte[] serializedEnvelope)
        {
            if (serializedEnvelope == null)
                throw new ArgumentNullException(nameof(serializedEnvelope));

            string normalizedNetworkId = SpaceEngineersNetworkIdentity.Normalize(networkId);
            byte[] networkIdBytes = _strictUtf8.GetBytes(normalizedNetworkId);
            int payloadLengthOffset = FixedHeaderLength + networkIdBytes.Length;
            int totalLength = payloadLengthOffset + PayloadLengthSize + serializedEnvelope.Length;
            var packet = new byte[totalLength];

            Array.Copy(_magic, 0, packet, 0, MagicLength);
            packet[VersionOffset] = CurrentVersion;
            WriteUInt16(packet, NetworkIdLengthOffset, (ushort)networkIdBytes.Length);
            Array.Copy(networkIdBytes, 0, packet, FixedHeaderLength, networkIdBytes.Length);
            WriteInt32(packet, payloadLengthOffset, serializedEnvelope.Length);
            Array.Copy(serializedEnvelope, 0, 
                       packet, payloadLengthOffset + PayloadLengthSize, serializedEnvelope.Length);

            return packet;
        }

        public static SpaceEngineersNetworkWireDecodeResult Decode(byte[] packet, string expectedNetworkId)
        {
            if (packet == null)
                throw new ArgumentNullException(nameof(packet));

            string normalizedExpectedNetworkId = SpaceEngineersNetworkIdentity.Normalize(expectedNetworkId);

            if (packet.Length < MagicLength || !HasMagic(packet))
                return SpaceEngineersNetworkWireDecodeResult.Failure(
                    SpaceEngineersNetworkWireStatus.ForeignPacket, null,
                    "The packet does not contain the Mz.Networking wire magic."
                );

            if (packet.Length < FixedHeaderLength)
                return SpaceEngineersNetworkWireDecodeResult.Failure(
                    SpaceEngineersNetworkWireStatus.MalformedWirePacket, null,
                    "The Mz.Networking wire header is truncated before its network ID."
                );

            ushort networkIdLength = ReadUInt16(packet, NetworkIdLengthOffset);

            if (networkIdLength == 0 || networkIdLength > SpaceEngineersNetworkIdentity.MaximumNetworkIdBytes)
                return SpaceEngineersNetworkWireDecodeResult.Failure(
                    SpaceEngineersNetworkWireStatus.MalformedWirePacket, null,
                    "The Mz.Networking wire network ID length is invalid."
                );

            int payloadLengthOffset = FixedHeaderLength + networkIdLength;

            if (packet.Length < payloadLengthOffset + PayloadLengthSize)
                return SpaceEngineersNetworkWireDecodeResult.Failure(
                    SpaceEngineersNetworkWireStatus.MalformedWirePacket, null,
                    "The Mz.Networking wire packet is truncated before its payload length."
                );

            string observedNetworkId;

            try
            {
                observedNetworkId = _strictUtf8.GetString(packet, FixedHeaderLength, networkIdLength);
            }
            catch (DecoderFallbackException)
            {
                return SpaceEngineersNetworkWireDecodeResult.Failure(
                    SpaceEngineersNetworkWireStatus.MalformedWirePacket, null,
                    "The Mz.Networking wire network ID is not valid UTF-8."
                );
            }

            bool networkMatches = string.Equals(normalizedExpectedNetworkId, observedNetworkId, StringComparison.Ordinal);

            if (!networkMatches)
                return SpaceEngineersNetworkWireDecodeResult.Failure(
                    SpaceEngineersNetworkWireStatus.NetworkMismatch, observedNetworkId,
                    "The packet belongs to another Mz.Networking network ID."
                );
            
            if (packet[VersionOffset] != CurrentVersion)
                return SpaceEngineersNetworkWireDecodeResult.Failure(
                    SpaceEngineersNetworkWireStatus.UnsupportedWireVersion, observedNetworkId,
                    "The packet uses an unsupported Mz.Networking wire version."
                );

            int payloadLength = ReadInt32(packet, payloadLengthOffset);

            if (payloadLength < 0 || packet.Length != payloadLengthOffset + PayloadLengthSize + payloadLength)
                return SpaceEngineersNetworkWireDecodeResult.Failure(
                    SpaceEngineersNetworkWireStatus.MalformedWirePacket, observedNetworkId,
                    "The Mz.Networking wire payload length does not match the packet."
                );

            var serializedEnvelope = new byte[payloadLength];

            Array.Copy(packet, payloadLengthOffset + PayloadLengthSize,
                       serializedEnvelope, 0, payloadLength);

            return SpaceEngineersNetworkWireDecodeResult.Success(serializedEnvelope, observedNetworkId);
        }

        private static bool HasMagic(byte[] packet)
        {
            for (var index = 0; index < MagicLength; index++)
                if (packet[index] != _magic[index])
                    return false;

            return true;
        }

        private static ushort ReadUInt16(byte[] source, int offset)
            => (ushort)( source[offset] | source[offset + 1] << 8 );

        private static int ReadInt32(byte[] source, int offset)
            => source[offset] | source[offset + 1] << 8 | source[offset + 2] << 16 | source[offset + 3] << 24;

        private static void WriteUInt16(byte[] target, int offset, ushort value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteInt32(byte[] target, int offset, int value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }
    }
}
