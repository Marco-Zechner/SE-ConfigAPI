using System;

namespace MarcoZechner.ConfigAPI.Domain
{
    public static class WorldConfigOperations
    {
        public static WorldConfigAuthorityResult Apply(WorldConfigSnapshot current, ulong expectedRevision, ConfigDocument draft)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));
            if (draft == null)
                throw new ArgumentNullException(nameof(draft));

            return WorldConfigAuthority.Update(current, expectedRevision, current.Stored, draft, current.CurrentVariant);
        }

        public static WorldConfigAuthorityResult Save(WorldConfigSnapshot current, ulong expectedRevision)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));

            return WorldConfigAuthority.Update(current, expectedRevision, current.Applied, current.Applied, current.CurrentVariant);
        }

        public static WorldConfigAuthorityResult Reload(WorldConfigSnapshot current, ulong expectedRevision, ConfigDocument loadedDocument)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));
            if (loadedDocument == null)
                throw new ArgumentNullException(nameof(loadedDocument));

            return WorldConfigAuthority.Update(current, expectedRevision, loadedDocument, loadedDocument, current.CurrentVariant);
        }

        public static WorldConfigAuthorityResult Load(WorldConfigSnapshot current, ulong expectedRevision, ConfigDocument loadedDocument, string variant)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));
            if (loadedDocument == null)
                throw new ArgumentNullException(nameof(loadedDocument));
            if (string.IsNullOrWhiteSpace(variant))
                throw new ArgumentException("Variant must not be empty.", nameof(variant));

            return WorldConfigAuthority.Update(current, expectedRevision, loadedDocument, loadedDocument, variant);
        }

        public static WorldConfigAuthorityResult SaveAs(WorldConfigSnapshot current, ulong expectedRevision, string variant)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));
            if (string.IsNullOrWhiteSpace(variant))
                throw new ArgumentException("Variant must not be empty.", nameof(variant));

            return WorldConfigAuthority.Update(current, expectedRevision, current.Applied, current.Applied, variant);
        }
    }
}