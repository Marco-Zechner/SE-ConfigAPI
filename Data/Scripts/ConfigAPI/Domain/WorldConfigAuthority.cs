using System;

namespace MarcoZechner.ConfigAPI.V2.Domain
{
    public sealed class WorldConfigAuthorityResult
    {
        public bool IsApplied { get; }
        public bool IsStale { get; }
        public WorldConfigSnapshot Snapshot { get; }

        internal WorldConfigAuthorityResult(bool isApplied, bool isStale, WorldConfigSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            IsApplied = isApplied;
            IsStale = isStale;
            Snapshot = snapshot;
        }
    }

    public static class WorldConfigAuthority
    {
        public static WorldConfigAuthorityResult Apply(WorldConfigSnapshot current, ulong baseRevision, ConfigDocument document, string currentFile)
            => Update(current, baseRevision, document, document, currentFile);

        public static WorldConfigAuthorityResult Update(WorldConfigSnapshot current, ulong baseRevision, ConfigDocument stored, ConfigDocument applied, string currentFile)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));
            if (stored == null)
                throw new ArgumentNullException(nameof(stored));
            if (applied == null)
                throw new ArgumentNullException(nameof(applied));

            if (baseRevision != current.Revision)
                return new WorldConfigAuthorityResult(false, true, current);

            if (current.Revision == ulong.MaxValue)
                throw new InvalidOperationException("World config revision cannot be incremented.");

            var snapshot = new WorldConfigSnapshot(current.Identity, stored, applied, current.Revision + 1UL, currentFile);
            return new WorldConfigAuthorityResult(true, false, snapshot);
        }

    }
}
