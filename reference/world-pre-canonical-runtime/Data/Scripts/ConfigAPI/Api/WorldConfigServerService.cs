using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;

namespace MarcoZechner.ConfigAPI.V2.Api
{
    public sealed class WorldConfigServerService
    {
        private readonly ConfigConsumerRegistrationRegistry _registry;
        private readonly IConfigClock _clock;
        private readonly IWorldConfigBootstrapStore _bootstrapStore;
        private readonly Dictionary<string, ServerState> _states = new Dictionary<string, ServerState>(StringComparer.Ordinal);

        public WorldConfigServerService(ConfigConsumerRegistrationRegistry registry, IConfigClock clock)
            : this(registry, clock, NullWorldConfigBootstrapStore.Instance) { }

        public WorldConfigServerService(ConfigConsumerRegistrationRegistry registry, IConfigClock clock, IWorldConfigBootstrapStore bootstrapStore)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            if (clock == null)
                throw new ArgumentNullException(nameof(clock));
            if (bootstrapStore == null)
                throw new ArgumentNullException(nameof(bootstrapStore));

            _registry = registry;
            _clock = clock;
            _bootstrapStore = bootstrapStore;
        }

        public WorldConfigSnapshot Open(string consumerId, string configKey, string file, ConfigDocument currentDefaults)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeRequired(configKey, nameof(configKey));

            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));

            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));

            string key = StateKey(normalizedConsumerId, normalizedConfigKey);
            ServerState existing;

            if (_states.TryGetValue(key, out existing))
                return existing.Snapshot;

            IConfigTextStorage storage = _registry.GetCurrentStorage(normalizedConsumerId);
            var identity = new ConfigIdentity(normalizedConsumerId, normalizedConfigKey);
            string authoritativeFile = file;
            ulong authoritativeIteration = 0UL;

            WorldConfigSnapshot bootstrap;
            if (_bootstrapStore.TryRead(identity, out bootstrap) && bootstrap != null && identity.Equals(bootstrap.Identity))
            {
                if (!string.IsNullOrWhiteSpace(bootstrap.CurrentFile))
                    authoritativeFile = bootstrap.CurrentFile;

                authoritativeIteration = bootstrap.ServerIteration;
            }

            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(ConfigLocation.World, authoritativeFile, identity, currentDefaults);

            if (NeedsPersistence(loadResult))
                new ConfigPersistedStateWriter(storage, _clock).Write(ConfigLocation.World, loadResult, currentDefaults);

            var snapshot = new WorldConfigSnapshot(identity, loadResult.State.PlayerValues, authoritativeIteration, loadResult.State.CurrentFile);
            _states.Add(key, new ServerState(snapshot, currentDefaults));
            _bootstrapStore.Write(snapshot);
            return snapshot;
        }

        public WorldConfigAuthorityResult Apply(string consumerId, string configKey, ulong expectedRevision, ConfigDocument draft)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeRequired(configKey, nameof(configKey));
            if (draft == null)
                throw new ArgumentNullException(nameof(draft));

            string key = StateKey(normalizedConsumerId, normalizedConfigKey);
            ServerState state = GetRequiredState(key, normalizedConsumerId, normalizedConfigKey);
            WorldConfigAuthorityResult stale = RejectStale(state, expectedRevision);
            if (stale != null)
                return stale;

            IConfigTextStorage storage = _registry.GetCurrentStorage(normalizedConsumerId);
            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(ConfigLocation.World, state.Snapshot.CurrentFile, state.Snapshot.Identity, state.CurrentDefaults);
            ValidateDocument(loadResult, draft, state.CurrentDefaults, nameof(draft));

            WorldConfigAuthorityResult authority = WorldConfigOperations.Apply(state.Snapshot, expectedRevision, draft);
            _states[key] = new ServerState(authority.Snapshot, state.CurrentDefaults);
            _bootstrapStore.Write(authority.Snapshot);
            return authority;
        }

        public WorldConfigAuthorityResult Save(string consumerId, string configKey, ulong expectedRevision)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeRequired(configKey, nameof(configKey));
            string key = StateKey(normalizedConsumerId, normalizedConfigKey);
            ServerState state = GetRequiredState(key, normalizedConsumerId, normalizedConfigKey);
            WorldConfigAuthorityResult stale = RejectStale(state, expectedRevision);
            if (stale != null)
                return stale;
            if (!state.Snapshot.HasUnsavedChanges)
                return new WorldConfigAuthorityResult(false, false, state.Snapshot);

            IConfigTextStorage storage = _registry.GetCurrentStorage(normalizedConsumerId);
            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(ConfigLocation.World, state.Snapshot.CurrentFile, state.Snapshot.Identity, state.CurrentDefaults);
            ValidateDocument(loadResult, state.Snapshot.Applied, state.CurrentDefaults, nameof(state.Snapshot.Applied));
            PersistDocument(storage, loadResult, state.Snapshot.Applied, state.CurrentDefaults);

            WorldConfigAuthorityResult authority = WorldConfigOperations.Save(state.Snapshot, expectedRevision);
            _states[key] = new ServerState(authority.Snapshot, state.CurrentDefaults);
            _bootstrapStore.Write(authority.Snapshot);
            return authority;
        }

        public WorldConfigAuthorityResult Load(string consumerId, string configKey, ulong expectedRevision, string variant)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeRequired(configKey, nameof(configKey));
            string normalizedVariant = NormalizeVariant(variant);
            string key = StateKey(normalizedConsumerId, normalizedConfigKey);
            ServerState state = GetRequiredState(key, normalizedConsumerId, normalizedConfigKey);
            WorldConfigAuthorityResult stale = RejectStale(state, expectedRevision);
            if (stale != null)
                return stale;

            string file = GetVariantFile(normalizedConfigKey, normalizedVariant);
            IIndexedConfigTextStorage storage = _registry.GetCurrentIndexedStorage(normalizedConsumerId);
            if (!storage.Exists(ConfigLocation.World, file))
                throw new InvalidOperationException("World config variant does not exist: " + normalizedVariant);

            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(ConfigLocation.World, file, state.Snapshot.Identity, state.CurrentDefaults);
            WorldConfigAuthorityResult authority = WorldConfigOperations.Load(state.Snapshot, expectedRevision, loadResult.State.PlayerValues, file);

            if (NeedsPersistence(loadResult))
                new ConfigPersistedStateWriter(storage, _clock).Write(ConfigLocation.World, loadResult, state.CurrentDefaults);

            _states[key] = new ServerState(authority.Snapshot, state.CurrentDefaults);
            _bootstrapStore.Write(authority.Snapshot);
            return authority;
        }

        public WorldConfigAuthorityResult SaveAs(string consumerId, string configKey, ulong expectedRevision, string variant)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeRequired(configKey, nameof(configKey));
            string normalizedVariant = NormalizeVariant(variant);
            string key = StateKey(normalizedConsumerId, normalizedConfigKey);
            ServerState state = GetRequiredState(key, normalizedConsumerId, normalizedConfigKey);
            WorldConfigAuthorityResult stale = RejectStale(state, expectedRevision);
            if (stale != null)
                return stale;

            string file = GetVariantFile(normalizedConfigKey, normalizedVariant);
            IIndexedConfigTextStorage storage = _registry.GetCurrentIndexedStorage(normalizedConsumerId);
            if (storage.Exists(ConfigLocation.World, file))
                throw new InvalidOperationException("World config variant already exists: " + normalizedVariant);

            WorldConfigAuthorityResult authority = WorldConfigOperations.SaveAs(state.Snapshot, expectedRevision, file);
            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(ConfigLocation.World, file, state.Snapshot.Identity, state.CurrentDefaults);
            ValidateDocument(loadResult, state.Snapshot.Applied, state.CurrentDefaults, nameof(state.Snapshot.Applied));
            PersistDocument(storage, loadResult, state.Snapshot.Applied, state.CurrentDefaults);

            _states[key] = new ServerState(authority.Snapshot, state.CurrentDefaults);
            _bootstrapStore.Write(authority.Snapshot);
            return authority;
        }

        public string[] ListVariants(string consumerId, string configKey)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeRequired(configKey, nameof(configKey));
            GetRequiredState(StateKey(normalizedConsumerId, normalizedConfigKey), normalizedConsumerId, normalizedConfigKey);

            string prefix = normalizedConfigKey + ".";
            const string suffix = ".toml";
            string[] files = _registry.GetCurrentIndexedStorage(normalizedConsumerId).ListKnown(ConfigLocation.World);
            var variants = new HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < files.Length; index++)
            {
                string file = files[index];
                if (string.IsNullOrEmpty(file) || !file.StartsWith(prefix, StringComparison.Ordinal) || !file.EndsWith(suffix, StringComparison.Ordinal))
                    continue;

                int variantLength = file.Length - prefix.Length - suffix.Length;
                if (variantLength <= 0)
                    continue;

                string candidate = file.Substring(prefix.Length, variantLength);
                string normalizedVariant;
                try
                {
                    normalizedVariant = NormalizeVariant(candidate);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (!string.Equals(file, GetVariantFile(normalizedConfigKey, normalizedVariant), StringComparison.Ordinal))
                    continue;

                variants.Add(normalizedVariant);
            }

            var result = new List<string>(variants);
            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }
        public WorldConfigAuthorityResult Save(string consumerId, string configKey, ulong baseIteration, ConfigDocument draft)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeRequired(configKey, nameof(configKey));

            if (draft == null)
                throw new ArgumentNullException(nameof(draft));

            string key = StateKey(normalizedConsumerId, normalizedConfigKey);
            ServerState state;

            if (!_states.TryGetValue(key, out state))
                throw new InvalidOperationException("World config is not open on the authoritative server: " + normalizedConsumerId + "/" + normalizedConfigKey);

            WorldConfigAuthorityResult authority = WorldConfigOperations.Save(state.Snapshot, baseIteration, draft);

            if (!authority.IsApplied)
                return authority;

            IConfigTextStorage storage = _registry.GetCurrentStorage(normalizedConsumerId);
            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(
                ConfigLocation.World, state.Snapshot.CurrentFile, state.Snapshot.Identity, state.CurrentDefaults);

            ConfigDefaultReconciliationResult validation = ConfigDefaultReconciler.Reconcile(
                loadResult.State.BaselineDefaults, draft, state.CurrentDefaults);

            if (!validation.PlayerValues.Equals(draft))
                throw new ArgumentException("Player values do not match the current config schema.", nameof(draft));

            var persistedState = new ConfigPersistedState(
                loadResult.State.Identity, draft, loadResult.State.BaselineDefaults, loadResult.State.CurrentFile);

            var saveResult = new ConfigPersistedLoadResult(
                persistedState, loadResult.ActiveSource, loadResult.ProvenanceFile,
                loadResult.WasActiveFileMissing, loadResult.WasProvenanceMissing,
                loadResult.Changes, loadResult.RequiresBackup);

            new ConfigPersistedStateWriter(storage, _clock).Write(ConfigLocation.World, saveResult, state.CurrentDefaults);

            _states[key] = new ServerState(authority.Snapshot, state.CurrentDefaults);
            _bootstrapStore.Write(authority.Snapshot);
            return authority;
        }

        public WorldConfigAuthorityResult Reload(string consumerId, string configKey, ulong baseIteration)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeRequired(configKey, nameof(configKey));
            string key = StateKey(normalizedConsumerId, normalizedConfigKey);
            ServerState state = GetRequiredState(key, normalizedConsumerId, normalizedConfigKey);

            WorldConfigAuthorityResult stale = RejectStale(state, baseIteration);
            if (stale != null)
                return stale;

            IConfigTextStorage storage = _registry.GetCurrentStorage(normalizedConsumerId);
            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(
                ConfigLocation.World, state.Snapshot.CurrentFile, state.Snapshot.Identity, state.CurrentDefaults);

            if (NeedsPersistence(loadResult))
                new ConfigPersistedStateWriter(storage, _clock).Write(ConfigLocation.World, loadResult, state.CurrentDefaults);

            WorldConfigAuthorityResult authority = WorldConfigOperations.Reload(state.Snapshot, baseIteration, loadResult.State.PlayerValues);
            _states[key] = new ServerState(authority.Snapshot, state.CurrentDefaults);
            _bootstrapStore.Write(authority.Snapshot);
            return authority;
        }

        public WorldConfigAuthorityResult LoadAndSwitch(string consumerId, string configKey, ulong baseIteration, string file)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeRequired(configKey, nameof(configKey));
            RequireFile(file);
            string key = StateKey(normalizedConsumerId, normalizedConfigKey);
            ServerState state = GetRequiredState(key, normalizedConsumerId, normalizedConfigKey);

            WorldConfigAuthorityResult stale = RejectStale(state, baseIteration);
            if (stale != null)
                return stale;

            IConfigTextStorage storage = _registry.GetCurrentStorage(normalizedConsumerId);
            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(
                ConfigLocation.World, file, state.Snapshot.Identity, state.CurrentDefaults);

            if (NeedsPersistence(loadResult))
                new ConfigPersistedStateWriter(storage, _clock).Write(ConfigLocation.World, loadResult, state.CurrentDefaults);

            WorldConfigAuthorityResult authority = WorldConfigOperations.LoadAndSwitch(
                state.Snapshot, baseIteration, loadResult.State.PlayerValues, file);

            _states[key] = new ServerState(authority.Snapshot, state.CurrentDefaults);
            _bootstrapStore.Write(authority.Snapshot);
            return authority;
        }

        public WorldConfigAuthorityResult SaveAndSwitch(string consumerId, string configKey, ulong baseIteration, ConfigDocument draft, string file)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeRequired(configKey, nameof(configKey));

            if (draft == null)
                throw new ArgumentNullException(nameof(draft));

            RequireFile(file);
            string key = StateKey(normalizedConsumerId, normalizedConfigKey);
            ServerState state = GetRequiredState(key, normalizedConsumerId, normalizedConfigKey);

            WorldConfigAuthorityResult stale = RejectStale(state, baseIteration);
            if (stale != null)
                return stale;

            IConfigTextStorage storage = _registry.GetCurrentStorage(normalizedConsumerId);
            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(
                ConfigLocation.World, file, state.Snapshot.Identity, state.CurrentDefaults);

            ValidateDocument(loadResult, draft, state.CurrentDefaults, nameof(draft));
            PersistDocument(storage, loadResult, draft, state.CurrentDefaults);

            WorldConfigAuthorityResult authority = WorldConfigOperations.SaveAndSwitch(state.Snapshot, baseIteration, draft, file);
            _states[key] = new ServerState(authority.Snapshot, state.CurrentDefaults);
            _bootstrapStore.Write(authority.Snapshot);
            return authority;
        }

        public WorldConfigAuthorityResult ApplyPreset(string consumerId, string configKey, ulong baseIteration, string presetFile)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeRequired(configKey, nameof(configKey));
            RequireFile(presetFile);

            string key = StateKey(normalizedConsumerId, normalizedConfigKey);
            ServerState state = GetRequiredState(key, normalizedConsumerId, normalizedConfigKey);
            WorldConfigAuthorityResult stale = RejectStale(state, baseIteration);
            if (stale != null)
                return stale;

            IConfigTextStorage storage = _registry.GetCurrentStorage(normalizedConsumerId);
            if (storage.Read(ConfigLocation.World, presetFile) == null)
                throw new InvalidOperationException("World config preset does not exist: " + presetFile);

            ConfigPersistedLoadResult preset = new ConfigPersistedStateLoader(storage).Load(
                ConfigLocation.World, presetFile, state.Snapshot.Identity, state.CurrentDefaults);

            return Save(normalizedConsumerId, normalizedConfigKey, baseIteration, preset.State.PlayerValues);
        }
        public WorldConfigExport SavePreset(string consumerId, string configKey, ConfigDocument document, string presetFile, bool overwrite)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeRequired(configKey, nameof(configKey));

            if (document == null)
                throw new ArgumentNullException(nameof(document));

            RequireFile(presetFile);
            ServerState state = GetRequiredState(StateKey(normalizedConsumerId, normalizedConfigKey), normalizedConsumerId, normalizedConfigKey);

            if (string.Equals(state.Snapshot.CurrentFile, presetFile, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Preset target must not be the current authoritative config file: " + presetFile);

            return Export(normalizedConsumerId, normalizedConfigKey, document, presetFile, overwrite);
        }
        public WorldConfigExport Export(string consumerId, string configKey, ConfigDocument document, string file, bool overwrite)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeRequired(configKey, nameof(configKey));

            if (document == null)
                throw new ArgumentNullException(nameof(document));

            RequireFile(file);
            string key = StateKey(normalizedConsumerId, normalizedConfigKey);
            ServerState state = GetRequiredState(key, normalizedConsumerId, normalizedConfigKey);
            IConfigTextStorage storage = _registry.GetCurrentStorage(normalizedConsumerId);

            if (!overwrite && (storage.Read(ConfigLocation.World, file) != null ||
                               storage.Read(ConfigLocation.World, ConfigPersistedStateLoader.GetProvenanceFile(file)) != null))
            {
                throw new InvalidOperationException("World config export target already exists: " + file);
            }

            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(
                ConfigLocation.World, file, state.Snapshot.Identity, state.CurrentDefaults);

            ValidateDocument(loadResult, document, state.CurrentDefaults, nameof(document));
            PersistDocument(storage, loadResult, document, state.CurrentDefaults);
            return WorldConfigOperations.Export(state.Snapshot, document, file, overwrite);
        }

        private static string NormalizeVariant(string variant)
        {
            if (string.IsNullOrWhiteSpace(variant))
                throw new ArgumentException("Variant must not be empty.", nameof(variant));

            string normalized = variant.Trim();
            if (normalized.IndexOf('.') >= 0)
                throw new ArgumentException("Variant must not contain '.'.", nameof(variant));

            return normalized;
        }

        private static string GetVariantFile(string configKey, string variant) => configKey + "." + NormalizeVariant(variant) + ".toml";
        private static void ValidateDocument(ConfigPersistedLoadResult loadResult, ConfigDocument document, ConfigDocument currentDefaults, string parameterName)
        {
            ConfigDefaultReconciliationResult validation = ConfigDefaultReconciler.Reconcile(
                loadResult.State.BaselineDefaults, document, currentDefaults);

            if (!validation.PlayerValues.Equals(document))
                throw new ArgumentException("Player values do not match the current config schema.", parameterName);
        }

        private void PersistDocument(IConfigTextStorage storage, ConfigPersistedLoadResult loadResult, ConfigDocument document, ConfigDocument currentDefaults)
        {
            var persistedState = new ConfigPersistedState(
                loadResult.State.Identity, document, loadResult.State.BaselineDefaults, loadResult.State.CurrentFile);

            var saveResult = new ConfigPersistedLoadResult(
                persistedState, loadResult.ActiveSource, loadResult.ProvenanceFile,
                loadResult.WasActiveFileMissing, loadResult.WasProvenanceMissing,
                loadResult.Changes, loadResult.RequiresBackup);

            new ConfigPersistedStateWriter(storage, _clock).Write(ConfigLocation.World, saveResult, currentDefaults);
        }

        private static WorldConfigAuthorityResult RejectStale(ServerState state, ulong baseIteration)
        {
            if (baseIteration == state.Snapshot.ServerIteration)
                return null;

            return new WorldConfigAuthorityResult(false, true, state.Snapshot);
        }

        private ServerState GetRequiredState(string key, string consumerId, string configKey)
        {
            ServerState state;
            if (!_states.TryGetValue(key, out state))
                throw new InvalidOperationException("World config is not open on the authoritative server: " + consumerId + "/" + configKey);

            return state;
        }

        private static void RequireFile(string file)
        {
            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));
        }
        private static bool NeedsPersistence(ConfigPersistedLoadResult loadResult)
            => loadResult.WasActiveFileMissing || loadResult.WasProvenanceMissing || loadResult.Changes.Count > 0;

        private static string StateKey(string consumerId, string configKey) => consumerId + "\n" + configKey;

        private static string NormalizeRequired(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A non-empty value is required.", parameterName);

            return value.Trim();
        }

        private sealed class ServerState
        {
            public ServerState(WorldConfigSnapshot snapshot, ConfigDocument currentDefaults)
            {
                Snapshot = snapshot;
                CurrentDefaults = currentDefaults;
            }

            public WorldConfigSnapshot Snapshot { get; }
            public ConfigDocument CurrentDefaults { get; }
        }
    }
}