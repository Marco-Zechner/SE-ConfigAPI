namespace MarcoZechner.ConfigAPI.Main.NetworkCore
{
    public interface IWorldConfigClientSink
    {
        void OnServerWorldUpdate(WorldConfigPacket packet);
    }
}