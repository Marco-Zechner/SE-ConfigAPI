using System;

namespace MarcoZechner.ConfigAPI.V2.Domain
{
    public sealed class WorldConfigSnapshot
    {
        public ConfigIdentity Identity { get; }
        public ConfigDocument Stored { get; }
        public ConfigDocument Applied { get; }
        public ulong Revision { get; }
        public string CurrentVariant { get; }
        public bool HasUnsavedChanges => !Applied.Equals(Stored);

        public WorldConfigSnapshot(ConfigIdentity identity, ConfigDocument stored, ConfigDocument applied, ulong revision, string currentVariant)
        {
            if (identity == null)
                throw new ArgumentNullException(nameof(identity));
            if (stored == null)
                throw new ArgumentNullException(nameof(stored));
            if (applied == null)
                throw new ArgumentNullException(nameof(applied));
            if (string.IsNullOrWhiteSpace(currentVariant))
                throw new ArgumentException("Current variant must not be empty.", nameof(currentVariant));
            if (currentVariant.IndexOf('.') >= 0)
                throw new ArgumentException("Current variant must not contain '.'.", nameof(currentVariant));

            Identity = identity;
            Stored = stored;
            Applied = applied;
            Revision = revision;
            CurrentVariant = currentVariant;
        }
    }
}