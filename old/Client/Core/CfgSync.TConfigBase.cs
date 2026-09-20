using MarcoZechner.ConfigAPI.Client.Api;
using MarcoZechner.ConfigAPI.Scripts.ConfigAPI.Shared;
using MarcoZechner.ConfigAPI.Shared.Domain;

namespace MarcoZechner.ConfigAPI.Client.Core
{
    /// <summary>
    /// User-facing world config handle.
    /// - Auth: authoritative snapshot (updated by ConfigAPIMod)
    /// - Draft: editable snapshot (never auto-updated)
    ///
    /// IMPORTANT:
    /// Call GetUpdate() once per update loop to stay in sync.
    /// Handling the returned CfgUpdate is optional.
    /// </summary>
    public sealed class CfgSync<T> where T : ConfigBase, new()
    {
        public readonly string TypeKey;
        private readonly string _defaultFile;

        private bool _initQueued;
        private ulong _serverIteration;
        private ulong? _draftIteration;
        private string _currentFile;

        private T _auth;
        private T _draft;

        internal CfgSync(string defaultFile)
        {
            TypeKey = typeof(T).FullName;
            _defaultFile = string.IsNullOrEmpty(defaultFile) ? (TypeKey + ".toml") : defaultFile;

            // local fallback defaults until API becomes available
            _auth = new T();
            _auth.ApplyDefaults();
            _auth.__Bind(TypeKey, LocationType.World);

            _draft = new T();
            _draft.ApplyDefaults();
            _draft.__Bind(TypeKey, LocationType.World);

            _serverIteration = 0;
            _draftIteration = null;
            _currentFile = _defaultFile;
        }

        /// <summary>
        /// Authoritative snapshot. Read-only by convention.
        /// </summary>
        public T Auth => _auth;

        /// <summary>
        /// Editable working copy.
        /// </summary>
        public T Draft => _draft;

        /// <summary>
        /// Last known server iteration (used as baseIteration for requests).
        /// </summary>
        public ulong ServerIteration => _serverIteration;
        /// <summary>
        /// Last known draft iteration (from last sync).
        /// </summary>
        public ulong DraftIteration => _draftIteration ?? 0;

        /// <summary>
        /// Last known current file on the server (from updates).
        /// </summary>
        public string CurrentFile => _currentFile;

        /// <summary>
        /// Pump once per update loop.
        /// Applies the latest pending server update internally (inside ConfigAPIMod).
        /// This method then refreshes local Auth/Draft references.
        /// Returns null if no update happened.
        /// </summary>
        public CfgUpdate GetUpdate()
        {
            var service = ServiceLoader.Service;
            if (service == null)
            {
                // API not ready yet; keep returning defaults.
                // We intentionally do not throw here.
                return new CfgUpdate()
                {
                    WorldOpKind = WorldOpKind.NoUpdate
                };
            }

            // Ensure init once API is ready.
            EnsureInit(service);

            // Pump the provider once. Provider applies update to its internal Auth.
            var update = service.ServerConfigGetUpdate(TypeKey);

            if (update == null)
            {
                return new CfgUpdate()
                {
                    WorldOpKind = WorldOpKind.NoUpdate
                };
            }

            if (update.WorldOpKind == WorldOpKind.NoUpdate) return update;

            // Track metadata so requests can use correct baseIteration.
            _serverIteration = update.ServerIteration;
            if (!string.IsNullOrEmpty(update.CurrentFile))
                _currentFile = update.CurrentFile;

            var initDraftBase = _draftIteration == null;
            RefreshAuthDraft(service, refreshDraft: initDraftBase);

            return update;
        }

        /// <summary>
        /// Draft &lt;- deep copy(Auth) (done by provider via serialize/deserialize callbacks).
        /// </summary>
        public void ResetDraft(bool onlyUpdateIteration = false)
        {
            var service = ServiceLoader.Service;
            if (service == null)
                return;

            EnsureInit(service);

            service.ServerConfigResetDraft(TypeKey);
            // Pull new draft reference
            RefreshAuthDraft(service, refreshDraft: !onlyUpdateIteration);
            if (onlyUpdateIteration)
                _draftIteration = _serverIteration;
        }

        // -------------------------
        // World operations
        // -------------------------

        public bool Reload()
        {
            var service = ServiceLoader.Service;
            if (service == null)
                return false;

            EnsureInit(service);

            return service.ServerConfigReload(TypeKey, ServerIteration);
        }

        public bool LoadAndSwitch(string file)
        {
            var service = ServiceLoader.Service;
            if (service == null)
                return false;

            EnsureInit(service);

            if (string.IsNullOrEmpty(file))
                return false;

            return service.ServerConfigLoadAndSwitch(TypeKey, file, ServerIteration);
        }

        public bool Save()
        {
            var service = ServiceLoader.Service;
            if (service == null)
                return false;

            EnsureInit(service);

            if (_draftIteration == null)
                return false; // cannot save before initial sync

            if (_draftIteration != _serverIteration)
            {
                CfgLogWorld.Warning($"Draft outdated. {TypeKey}");
                return false;
            }

            return service.ServerConfigSave(TypeKey, DraftIteration);
        }

        public bool SaveAndSwitch(string file)
        {
            var service = ServiceLoader.Service;
            if (service == null)
                return false;

            EnsureInit(service);

            if (string.IsNullOrEmpty(file))
                return false;

            if (_draftIteration == null)
                return false; // cannot save before initial sync

            if (_draftIteration != _serverIteration)
            {
                CfgLogWorld.Warning($"Draft outdated. {TypeKey}");
                return false;
            }

            return service.ServerConfigSaveAndSwitch(TypeKey, file, DraftIteration);
        }

        public bool Export(string file, bool overwrite = false)
        {
            var service = ServiceLoader.Service;
            if (service == null)
                return false;

            EnsureInit(service);

            if (string.IsNullOrEmpty(file))
                return false;

            return service.ServerConfigExport(TypeKey, file, overwrite);
        }

        // ===============================================================
        // Internals
        // ===============================================================

        private void EnsureInit(ConfigService service)
        {
            if (_initQueued)
                return;

            // Provider contract: returns current internal Auth (or default created there)
            // and triggers server open/request to send authoritative snapshot.
            service.ServerConfigInit(TypeKey, _defaultFile);

            _initQueued = true;

            // pull draft immediately if available.
            RefreshAuthDraft(service, refreshDraft: true);
        }

        private void RefreshAuthDraft(ConfigService service, bool refreshDraft)
        {
            var xmlOldAuth = ConfigXmlSerializer.SerializeToXml(_auth);

            // Auth
            var authObj = service.ServerConfigGetAuth(TypeKey);
            var authCast = authObj as T;
            if (authCast != null)
            {
                _auth = authCast;
                _auth.__Bind(TypeKey, LocationType.World);
            }
            var xmlNewAuth = ConfigXmlSerializer.SerializeToXml(_auth);
            var xmlDraft = ConfigXmlSerializer.SerializeToXml(_draft);


            // either this client made the same changes, or he was the one that updated the auth
            if (xmlDraft == xmlNewAuth)
            {
                _draftIteration = _serverIteration;
                return;
            }

            // local draft has unsynced changes and refresh not requested -> keep local draft
            if (!refreshDraft && xmlDraft != xmlOldAuth) return;

            // Draft (optional)
            var draftObj = service.ServerConfigGetDraft(TypeKey);
            var draftCast = draftObj as T;
            if (draftCast == null) return;

            _draft = draftCast;
            _draft.__Bind(TypeKey, LocationType.World);
            _draftIteration = _serverIteration;
        }
    }
}