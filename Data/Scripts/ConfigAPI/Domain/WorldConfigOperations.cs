using System;

namespace MarcoZechner.ConfigAPI.V2.Domain
{
    public static class WorldConfigOperations
    {
        public static WorldConfigAuthorityResult Apply(WorldConfigSnapshot current, ulong expectedRevision, ConfigDocument draft)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));
            if (draft == null)
                throw new ArgumentNullException(nameof(draft));

            return WorldConfigAuthority.Update(current, expectedRevision, current.Stored, draft, current.CurrentFile);
        }

        public static WorldConfigAuthorityResult Save(WorldConfigSnapshot current, ulong expectedRevision)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));

            return WorldConfigAuthority.Update(current, expectedRevision, current.Applied, current.Applied, current.CurrentFile);
        }
        public static WorldConfigAuthorityResult Reload(WorldConfigSnapshot current, ulong baseIteration, ConfigDocument loadedDocument)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));

            if (loadedDocument == null)
                throw new ArgumentNullException(nameof(loadedDocument));

            return WorldConfigAuthority.Apply(current, baseIteration, loadedDocument, current.CurrentFile);
        }

        public static WorldConfigAuthorityResult LoadAndSwitch(WorldConfigSnapshot current, ulong baseIteration, 
                                                               ConfigDocument loadedDocument, string file)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));

            if (loadedDocument == null)
                throw new ArgumentNullException(nameof(loadedDocument));

            return WorldConfigAuthority.Apply(current, baseIteration, loadedDocument, file);
        }

        public static WorldConfigAuthorityResult Save(WorldConfigSnapshot current, ulong baseIteration, ConfigDocument draft)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));

            if (draft == null)
                throw new ArgumentNullException(nameof(draft));

            return WorldConfigAuthority.Apply(current, baseIteration, draft, current.CurrentFile);
        }

        public static WorldConfigAuthorityResult SaveAndSwitch(WorldConfigSnapshot current, ulong baseIteration, 
                                                               ConfigDocument draft, string file)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));

            if (draft == null)
                throw new ArgumentNullException(nameof(draft));

            return WorldConfigAuthority.Apply(current, baseIteration, draft, file);
        }

        public static WorldConfigExport Export(WorldConfigSnapshot authoritative, ConfigDocument document, string file, bool overwrite)
        {
            if (authoritative == null)
                throw new ArgumentNullException(nameof(authoritative));

            if (document == null)
                throw new ArgumentNullException(nameof(document));

            return new WorldConfigExport(authoritative, document, file, overwrite);
        }
    }
}
