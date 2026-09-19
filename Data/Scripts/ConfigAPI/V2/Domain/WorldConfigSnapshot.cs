using System;

namespace MarcoZechner.ConfigAPI.V2.Domain
{
    public sealed class WorldConfigSnapshot
    {
        public ConfigIdentity Identity { get; }
        public ConfigDocument Document { get; }
        public ulong ServerIteration { get; }
        public string CurrentFile { get; }

        public WorldConfigSnapshot(ConfigIdentity identity, ConfigDocument document, ulong serverIteration, string currentFile)
        {
            if (identity == null)
                throw new ArgumentNullException(nameof(identity));

            if (document == null)
                throw new ArgumentNullException(nameof(document));

            Identity = identity;
            Document = document;
            ServerIteration = serverIteration;
            CurrentFile = currentFile;
        }
    }
}
