using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Shared.Domain;

namespace MarcoZechner.ConfigAPI.Shared.Api
{
    /// <summary>
    /// Callback API implemented by each UserMod (consumer),
    /// called by ConfigAPIMod to do mod-specific work:
    /// - file IO in the mod's folder
    /// - create default instances
    /// - serialize/deserialize real config types
    ///
    /// IMPORTANT: use object at the boundary to avoid cross-assembly type issues.
    /// Inside UserMod, cast object to the real config type.
    /// </summary>
    public interface IConfigUserHooks
    {
        // -------------------------
        // Type system
        // -------------------------

        /// <summary>
        /// Create a new default instance for the config identified by typeKey.
        /// Must call ApplyDefaults() internally for collection defaults.
        /// </summary>
        object NewDefault(string typeKey);
        /// <summary>
        /// Return the default name for the config identified by typeKey.
        /// </summary>
        string DefaultName(string typeKey);

        /// <summary>
        /// Check if the given instance matches the typeKey.
        /// </summary>
        bool IsInstanceOf(string typeKey, object instance);

        /// <summary>
        /// Serialize an instance to INTERNAL CANONICAL XML (not TOML).
        /// If you keep comments as a post-process in ConfigAPIMod, you can ignore this flag.
        /// </summary>
        string SerializeToInternalXml(string typeKey, object instance);

        /// <summary>
        /// Deserialize from INTERNAL CANONICAL XML to a new instance.
        /// </summary>
        object DeserializeFromInternalXml(string typeKey, string internalXml);

        /// <summary>
        /// Get per-variable description map used for TOML comments on disk.
        /// Return empty dict if not provided.
        /// Key = property/field name.
        /// Value = multi-line comment text.
        /// </summary>
        IReadOnlyDictionary<string, string> GetVariableDescriptions(string typeKey);

        // -------------------------
        // File IO (mod-specific folders)
        // -------------------------

        /// <summary>
        /// Load file content from the mod's config folder.
        /// Return null if missing.
        /// location will be World/Local/Global; you can ignore non-World if you want.
        /// </summary>
        string LoadFile(LocationType locationType, string filename);

        /// <summary>
        /// Save file content into the mod's config folder.
        /// </summary>
        void SaveFile(LocationType locationType, string filename, string content);

        /// <summary>
        /// Backup file (e.g. to *.bak). No-op if missing.
        /// </summary>
        void BackupFile(LocationType locationType, string filename);
    }
}