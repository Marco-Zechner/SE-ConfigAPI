using System;

namespace MarcoZechner.ConfigAPI.V2.Domain
{
    public sealed class WorldConfigSnapshot
    {
        public ConfigIdentity Identity { get; }
        public ConfigDocument Stored { get; }
        public ConfigDocument Applied { get; }
        public ConfigDocument Document => Applied;
        public ulong Revision => ServerIteration;
        public ulong ServerIteration { get; }
        public string CurrentFile { get; }
        public bool HasUnsavedChanges => !Applied.Equals(Stored);

        public WorldConfigSnapshot(ConfigIdentity identity, ConfigDocument document, ulong serverIteration, string currentFile)
            : this(identity, document, document, serverIteration, currentFile) { }

        public WorldConfigSnapshot(ConfigIdentity identity, ConfigDocument stored, ConfigDocument applied, ulong serverIteration, string currentFile)
        {
            if (identity == null)
                throw new ArgumentNullException(nameof(identity));
            if (stored == null)
                throw new ArgumentNullException(nameof(stored));
            if (applied == null)
                throw new ArgumentNullException(nameof(applied));

            Identity = identity;
            Stored = stored;
            Applied = applied;
            ServerIteration = serverIteration;
            CurrentFile = currentFile;
        }
    }
}
