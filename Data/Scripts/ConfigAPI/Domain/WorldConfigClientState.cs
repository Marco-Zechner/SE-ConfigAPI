using System;

namespace MarcoZechner.ConfigAPI.V2.Domain
{
    public sealed class WorldConfigClientState
    {
        public WorldConfigSnapshot Authoritative { get; }
        public ConfigDocument Draft { get; }
        public ConfigDocument DraftBaseApplied { get; }
        public ulong DraftBaseRevision { get; }
        public bool HasDraftChanges => !Draft.Equals(Authoritative.Applied);
        public bool IsDraftStale => DraftBaseRevision != Authoritative.Revision;

        private WorldConfigClientState(WorldConfigSnapshot authoritative, ConfigDocument draft, ConfigDocument draftBaseApplied, ulong draftBaseRevision)
        {
            if (authoritative == null)
                throw new ArgumentNullException(nameof(authoritative));
            if (draft == null)
                throw new ArgumentNullException(nameof(draft));
            if (draftBaseApplied == null)
                throw new ArgumentNullException(nameof(draftBaseApplied));

            Authoritative = authoritative;
            Draft = draft;
            DraftBaseApplied = draftBaseApplied;
            DraftBaseRevision = draftBaseRevision;
        }

        public static WorldConfigClientState Create(WorldConfigSnapshot authoritative)
        {
            if (authoritative == null)
                throw new ArgumentNullException(nameof(authoritative));

            return new WorldConfigClientState(authoritative, authoritative.Applied, authoritative.Applied, authoritative.Revision);
        }

        public WorldConfigClientState WithDraft(ConfigDocument draft)
        {
            if (draft == null)
                throw new ArgumentNullException(nameof(draft));

            return new WorldConfigClientState(Authoritative, draft, DraftBaseApplied, DraftBaseRevision);
        }

        public WorldConfigClientState ApplyAuthoritative(WorldConfigSnapshot authoritative)
        {
            if (authoritative == null)
                throw new ArgumentNullException(nameof(authoritative));
            if (!Authoritative.Identity.Equals(authoritative.Identity))
                throw new ArgumentException("Authoritative snapshot belongs to a different config.", nameof(authoritative));

            if (!HasDraftChanges || Draft.Equals(authoritative.Applied))
                return new WorldConfigClientState(authoritative, authoritative.Applied, authoritative.Applied, authoritative.Revision);

            return new WorldConfigClientState(authoritative, Draft, DraftBaseApplied, DraftBaseRevision);
        }

        public WorldConfigClientState ResetDraftToAuthoritative()
            => new WorldConfigClientState(Authoritative, Authoritative.Applied, Authoritative.Applied, Authoritative.Revision);
    }
}
