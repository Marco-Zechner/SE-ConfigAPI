using MarcoZechner.ConfigAPI.V2.Domain;

namespace MarcoZechner.ConfigAPI.V2.Api
{
    public interface IWorldConfigBootstrapStore
    {
        bool TryRead(ConfigIdentity identity, out WorldConfigSnapshot snapshot);
        void Write(WorldConfigSnapshot snapshot);
    }

    internal sealed class NullWorldConfigBootstrapStore : IWorldConfigBootstrapStore
    {
        public static readonly NullWorldConfigBootstrapStore Instance = new NullWorldConfigBootstrapStore();

        private NullWorldConfigBootstrapStore() { }

        public bool TryRead(ConfigIdentity identity, out WorldConfigSnapshot snapshot)
        {
            snapshot = null;
            return false;
        }

        public void Write(WorldConfigSnapshot snapshot) { }
    }
}