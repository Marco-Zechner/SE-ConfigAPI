using System;

namespace MarcoZechner.ConfigAPI.Domain
{
    public sealed class WorldConfigAuthorityResult
    {
        public bool IsChanged { get; }
        public bool IsStale { get; }
        public WorldConfigSnapshot Snapshot { get; }

        internal WorldConfigAuthorityResult(bool isChanged, bool isStale, WorldConfigSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            IsChanged = isChanged;
            IsStale = isStale;
            Snapshot = snapshot;
        }
    }

    public static class WorldConfigAuthority
    {
        public static WorldConfigAuthorityResult Update(WorldConfigSnapshot current, ulong expectedRevision, ConfigDocument stored, ConfigDocument applied, string currentVariant)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));
            if (stored == null)
                throw new ArgumentNullException(nameof(stored));
            if (applied == null)
                throw new ArgumentNullException(nameof(applied));

            if (expectedRevision != current.Revision)
                return new WorldConfigAuthorityResult(false, true, current);

            if (current.Revision == ulong.MaxValue)
                throw new InvalidOperationException("World config revision cannot be incremented.");

            var snapshot = new WorldConfigSnapshot(current.Identity, stored, applied, current.Revision + 1UL, currentVariant);
            return new WorldConfigAuthorityResult(true, false, snapshot);
        }
    }
}