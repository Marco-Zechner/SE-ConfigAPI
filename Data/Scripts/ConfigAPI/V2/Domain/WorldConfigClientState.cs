using System;

namespace MarcoZechner.ConfigAPI.V2.Domain
{
    public sealed class WorldConfigClientState
    {
        public WorldConfigSnapshot Authoritative { get; }
        public ConfigDocument Draft { get; }

        private WorldConfigClientState(WorldConfigSnapshot authoritative, ConfigDocument draft)
        {
            if (authoritative == null)
                throw new ArgumentNullException(nameof(authoritative));

            if (draft == null)
                throw new ArgumentNullException(nameof(draft));

            Authoritative = authoritative;
            Draft = draft;
        }

        public static WorldConfigClientState Create(WorldConfigSnapshot authoritative)
        {
            if (authoritative == null)
                throw new ArgumentNullException(nameof(authoritative));

            return new WorldConfigClientState(authoritative, authoritative.Document); 
        }

        public WorldConfigClientState WithDraft(ConfigDocument draft)
        {
            if (draft == null)
                throw new ArgumentNullException(nameof(draft));

            return new WorldConfigClientState(Authoritative, draft);
        }

        public WorldConfigClientState ApplyAuthoritative(WorldConfigSnapshot authoritative)
        {
            if (authoritative == null)
                throw new ArgumentNullException(nameof(authoritative));

            if (!Authoritative.Identity.Equals(authoritative.Identity))
                throw new ArgumentException("Authoritative snapshot belongs to a different config.", nameof(authoritative));

            return new WorldConfigClientState(authoritative, Draft);
        }

        public WorldConfigClientState ResetDraftToAuthoritative() => new WorldConfigClientState(Authoritative, Authoritative.Document);
    }
}
