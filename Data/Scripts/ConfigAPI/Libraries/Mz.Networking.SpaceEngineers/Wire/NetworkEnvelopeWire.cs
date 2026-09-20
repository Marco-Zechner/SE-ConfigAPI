using ProtoBuf;

namespace Mz.Networking.SpaceEngineers
{
    [ProtoContract]
    internal sealed class NetworkEnvelopeWire
    {
        [ProtoMember(1)]
        public string MessageType { get; set; }

        [ProtoMember(2)]
        public ulong OriginalSenderId { get; set; }

        [ProtoMember(3)]
        public bool IsRelay { get; set; }

        [ProtoMember(4)]
        public byte[] Payload { get; set; }
    }
}
