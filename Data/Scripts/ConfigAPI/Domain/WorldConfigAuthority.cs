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
        public static WorldConfigAuthorityResult Apply(WorldConfigSnapshot current, ulong baseIteration, 
                                                       ConfigDocument document, string currentFile)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));

            if (document == null)
                throw new ArgumentNullException(nameof(document));

            if (baseIteration != current.ServerIteration)
                return new WorldConfigAuthorityResult(false, true, current);

            if (current.ServerIteration == ulong.MaxValue)
                throw new InvalidOperationException("Server iteration cannot be incremented.");

            var snapshot = new WorldConfigSnapshot(current.Identity, document, current.ServerIteration + 1UL, currentFile);

            return new WorldConfigAuthorityResult(true, false, snapshot);
        }
    }
}
