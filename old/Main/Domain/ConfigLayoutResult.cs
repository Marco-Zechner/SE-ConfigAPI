namespace MarcoZechner.ConfigAPI.Main.Domain
{
    /// <summary>
    /// Result of layout migration for a given config type.
    /// </summary>
    public struct ConfigLayoutResult
    {
        /// <summary>
        /// Normalized XML for the current config instance.
        /// This is what will be deserialized into the ConfigBase object.
        /// </summary>
        public string NormalizedXml;

        /// <summary>
        /// Normalized XML representing the "defaults for this version" that
        /// should be written into the per-type defaults file.
        /// </summary>
        public string NormalizedDefaultsXml;

        /// <summary>
        /// True if extra/unknown keys or other destructive changes were made
        /// and the original external content (before migration) should be
        /// backed up.
        /// </summary>
        public bool RequiresBackup;
    }
}