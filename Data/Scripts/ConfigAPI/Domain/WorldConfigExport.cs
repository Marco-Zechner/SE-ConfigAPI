using System;

namespace MarcoZechner.ConfigAPI.Domain
{
    public sealed class WorldConfigExport
    {
        public WorldConfigSnapshot Authoritative { get; }
        public ConfigDocument Document { get; }
        public string File { get; }
        public bool Overwrite { get; }

        internal WorldConfigExport(WorldConfigSnapshot authoritative, ConfigDocument document, string file, bool overwrite)
        {
            if (authoritative == null)
                throw new ArgumentNullException(nameof(authoritative));

            if (document == null)
                throw new ArgumentNullException(nameof(document));

            Authoritative = authoritative;
            Document = document;
            File = file;
            Overwrite = overwrite;
        }
    }
}
