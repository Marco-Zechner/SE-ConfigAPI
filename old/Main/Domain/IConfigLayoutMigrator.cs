namespace MarcoZechner.ConfigAPI.Main.Domain
{
    /// <summary>
    /// Responsible for normalizing the layout of a config based on XML.
    ///
    /// It sees three perspectives:
    /// - xmlCurrentFromFile: the user's config as saved in the file.
    /// - xmlOldDefaultsFromFile: the defaults recorded for this type when the
    ///   file was last written (from the per-type defaults file).
    /// - xmlCurrentDefaults: the defaults from the current code
    ///   (CreateDefaultInstance + SerializeToXml).
    ///
    /// It returns normalized XML for the config and for the defaults, and can
    /// signal that the original external file should be backed up.
    /// </summary>
    public interface IConfigLayoutMigrator
    {
        ConfigLayoutResult Normalize(
            string typeName,
            string xmlCurrentFromFile,
            string xmlOldDefaultsFromFile,
            string xmlCurrentDefaults);
    }
}