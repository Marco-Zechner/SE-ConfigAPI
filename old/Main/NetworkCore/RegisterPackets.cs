using MarcoZechner.ConfigAPI.Main.NetworkCore;
using ProtoBuf;

namespace Digi.NetworkLib
{
    // tag numbers in ProtoInclude collide with numbers from ProtoMember in the same class, therefore they must be unique.
    // use something high
    [ProtoInclude(1000, typeof(WorldConfigPacket))]
    public abstract partial class PacketBase
    {
    }
}