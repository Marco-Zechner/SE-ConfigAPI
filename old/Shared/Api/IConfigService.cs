using MarcoZechner.ConfigAPI.Shared.Domain;

namespace MarcoZechner.ConfigAPI.Shared.Api
{
    /// <summary>
    /// Main API exposed by ConfigAPIMod. UserMods call this.
    /// Keep this interface stable; add methods carefully.
    /// </summary>
    public interface IConfigService
    {
        // -------------------------
        // Local/Global config: (no networking, just local file storage)
        // Routed through ConfigAPI on the same machine as migrator&converter only exist on ConfigAPI side.
        // -------------------------

        /// <summary>
        /// Get (create/load/migrate) a Local/Global config instance.
        /// If filename is null/empty, use a default derived from typeKey (e.g. typeKey + ".toml").
        /// Returns an opaque object; UserMod casts to its config type.
        /// </summary>
        object ClientConfigGet(string typeKey, LocationType locationType, string filename);

        /// <summary>
        /// Reload the config from disk and replace the in-memory instance.
        /// Returns null if load failed; otherwise returns the new instance.
        /// </summary>
        object ClientConfigReload(string typeKey, LocationType locationType);

        /// <summary>
        /// Get the filename that the current in-memory instance was loaded from.
        /// Returns null if instance doesn't exist yet.
        /// </summary>
        string ClientConfigGetCurrentFileName(string typeKey, LocationType locationType);

        /// <summary>
        /// Force load config from disk and replace the in-memory instance.
        /// Returns null if load failed; otherwise returns the new instance.
        /// </summary>
        object ClientConfigLoadAndSwitch(string typeKey, LocationType locationType, string filename);

        /// <summary>
        /// Save the current in-memory instance to the file it was loaded from.
        /// Returns false if instance doesn't exist yet or save failed.
        /// </summary>
        bool ClientConfigSave(string typeKey, LocationType locationType);
        /// <summary>
        /// Save the current in-memory instance to disk and replace it with the newly saved instance.
        /// Returns null if save or switch failed; otherwise returns the new instance.
        /// </summary>
        object ClientConfigSaveAndSwitch(string typeKey, LocationType locationType, string filename);
        /// <summary>
        /// Export the current in-memory instance to the given filename.
        /// Even with overwrite=true, it will only overwrite a file that holds the matching config type.
        /// It will not overwrite the file that the current instance was loaded from.
        /// Returns false if instance doesn't exist yet or export failed.
        /// </summary>
        bool ClientConfigExport(string typeKey, LocationType locationType, string filename, bool overwrite);

        // -------------------------
        // World config: client-side sync surface
        //
        // Key: (modId, typeKey)
        // typeKey = typeof(T).FullName from UserMod
        // -------------------------

        /// <summary>
        /// If not yet called for this typeKey, then it returns the default instance for the given typeKey,
        /// saves it into the internal authoritative instance and the draft instance,
        /// and requests the server to open (if not already opened) and send the authoritative snapshot.
        /// If it has already been called, it returns the internal authoritative instance (same as ServerConfigGetAuth).
        /// </summary>
        void ServerConfigInit(string typeKey, string defaultFile);

        /// <summary>
        /// Pump the world sync once (should be called once per update loop).
        /// Applies the latest pending server update to Auth internally.
        /// Returns:
        /// - null if no update occurred since last poll
        /// - a CfgUpdate object if something changed or an error happened
        /// </summary>
        CfgUpdate ServerConfigGetUpdate(string typeKey);

        /// <summary>
        /// Get current authoritative snapshot (opaque object).
        /// UserMod casts this to its config type.
        /// </summary>
        object ServerConfigGetAuth(string typeKey);

        /// <summary>
        /// Get current draft snapshot (opaque object).
        /// UserMod casts this to its config type and edits it locally.
        /// </summary>
        object ServerConfigGetDraft(string typeKey);

        /// <summary>
        /// Replace Draft with a deep copy of Auth (provider uses callback serialize/deserialize).
        /// </summary>
        void ServerConfigResetDraft(string typeKey);

        // -------------------------
        // World operations (requests)
        // Return false if a request is already in flight for this (typeKey).
        // BaseIteration should be whatever the UserMod last observed from WorldGetUpdate/Auth.
        // -------------------------

        /// <summary>
        /// Reload the config from disk and replace the in-memory instance.
        /// </summary>
        bool ServerConfigReload(string typeKey, ulong baseIteration);

        /// <summary>
        /// Force load config from specific file on disk and replace the in-memory instance.
        /// </summary>
        bool ServerConfigLoadAndSwitch(string typeKey, string file, ulong baseIteration);

        /// <summary>
        /// Sends a request to the server to save the current Draft as the new Authoritative config,
        /// and in the file the current Authoritative config was loaded from.
        /// </summary>
        bool ServerConfigSave(string typeKey, ulong baseIteration);

        /// <summary>
        /// Sends a request to the server to save the current Draft as the new Authoritative config,
        /// and in the given file.
        /// </summary>
        bool ServerConfigSaveAndSwitch(string typeKey, string file, ulong baseIteration);

        /// <summary>
        /// Sends a request to the server to export the current Draft to the given file.
        /// If overwrite is true, it will overwrite a file that holds the matching config type.
        /// It will not overwrite the file that the current Authoritative config was loaded from.
        /// It doesn't need baseIteration because it doesn't change any state.
        /// </summary>
        bool ServerConfigExport(string typeKey, string file, bool overwrite);
    }
}