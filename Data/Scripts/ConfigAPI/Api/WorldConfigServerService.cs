using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;

namespace MarcoZechner.ConfigAPI.V2.Api
{
    public sealed class WorldConfigServerService
    {
        private const string DefaultVariant = "default";

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

        public WorldConfigSnapshot Open(string consumerId, string configKey, ConfigDocument currentDefaults)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeConfigKey(configKey);
            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));

            string key = StateKey(normalizedConsumerId, normalizedConfigKey);
            ServerState existing;
            if (_states.TryGetValue(key, out existing))
                return existing.Snapshot;

            var identity = new ConfigIdentity(normalizedConsumerId, normalizedConfigKey);
            string currentVariant = DefaultVariant;
            ulong revision = 0UL;

            WorldConfigSnapshot bootstrap;
            if (_bootstrapStore.TryRead(identity, out bootstrap) && bootstrap != null && identity.Equals(bootstrap.Identity))
            {
                currentVariant = NormalizeVariant(bootstrap.CurrentVariant);
                revision = bootstrap.Revision;
            }

            IIndexedConfigTextStorage storage = _registry.GetCurrentIndexedStorage(normalizedConsumerId);
            string file = GetVariantFile(normalizedConfigKey, currentVariant);
            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(ConfigLocation.World, file, identity, currentDefaults);

            if (NeedsPersistence(loadResult))
                new ConfigPersistedStateWriter(storage, _clock).Write(ConfigLocation.World, loadResult, currentDefaults);

            var snapshot = new WorldConfigSnapshot(identity, loadResult.State.PlayerValues, loadResult.State.PlayerValues, revision, currentVariant);
            _states.Add(key, new ServerState(snapshot, currentDefaults));
            _bootstrapStore.Write(snapshot);
            return snapshot;
        }

        public WorldConfigAuthorityResult Apply(string consumerId, string configKey, ulong expectedRevision, ConfigDocument draft)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeConfigKey(configKey);
            if (draft == null)
                throw new ArgumentNullException(nameof(draft));

            string key = StateKey(normalizedConsumerId, normalizedConfigKey);
            ServerState state = GetRequiredState(key, normalizedConsumerId, normalizedConfigKey);
            WorldConfigAuthorityResult stale = RejectStale(state, expectedRevision);
            if (stale != null)
                return stale;

            IIndexedConfigTextStorage storage = _registry.GetCurrentIndexedStorage(normalizedConsumerId);
            string file = GetVariantFile(normalizedConfigKey, state.Snapshot.CurrentVariant);
            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(ConfigLocation.World, file, state.Snapshot.Identity, state.CurrentDefaults);
            ValidateDocument(loadResult, draft, state.CurrentDefaults, nameof(draft));

            WorldConfigAuthorityResult result = WorldConfigOperations.Apply(state.Snapshot, expectedRevision, draft);
            StoreState(key, state, result);
            return result;
        }

        public WorldConfigAuthorityResult Save(string consumerId, string configKey, ulong expectedRevision)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeConfigKey(configKey);
            string key = StateKey(normalizedConsumerId, normalizedConfigKey);
            ServerState state = GetRequiredState(key, normalizedConsumerId, normalizedConfigKey);
            WorldConfigAuthorityResult stale = RejectStale(state, expectedRevision);
            if (stale != null)
                return stale;

            if (!state.Snapshot.HasUnsavedChanges)
                return new WorldConfigAuthorityResult(false, false, state.Snapshot);

            IIndexedConfigTextStorage storage = _registry.GetCurrentIndexedStorage(normalizedConsumerId);
            string file = GetVariantFile(normalizedConfigKey, state.Snapshot.CurrentVariant);
            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(ConfigLocation.World, file, state.Snapshot.Identity, state.CurrentDefaults);
            ValidateDocument(loadResult, state.Snapshot.Applied, state.CurrentDefaults, nameof(state.Snapshot.Applied));
            PersistDocument(storage, loadResult, state.Snapshot.Applied, state.CurrentDefaults);

            WorldConfigAuthorityResult result = WorldConfigOperations.Save(state.Snapshot, expectedRevision);
            StoreState(key, state, result);
            return result;
        }

        public WorldConfigAuthorityResult Reload(string consumerId, string configKey, ulong expectedRevision)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeConfigKey(configKey);
            string key = StateKey(normalizedConsumerId, normalizedConfigKey);
            ServerState state = GetRequiredState(key, normalizedConsumerId, normalizedConfigKey);
            WorldConfigAuthorityResult stale = RejectStale(state, expectedRevision);
            if (stale != null)
                return stale;

            IIndexedConfigTextStorage storage = _registry.GetCurrentIndexedStorage(normalizedConsumerId);
            string file = GetVariantFile(normalizedConfigKey, state.Snapshot.CurrentVariant);
            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(ConfigLocation.World, file, state.Snapshot.Identity, state.CurrentDefaults);

            if (NeedsPersistence(loadResult))
                new ConfigPersistedStateWriter(storage, _clock).Write(ConfigLocation.World, loadResult, state.CurrentDefaults);

            WorldConfigAuthorityResult result = WorldConfigOperations.Reload(state.Snapshot, expectedRevision, loadResult.State.PlayerValues);
            StoreState(key, state, result);
            return result;
        }

        public WorldConfigAuthorityResult Load(string consumerId, string configKey, ulong expectedRevision, string variant)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeConfigKey(configKey);
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
            WorldConfigAuthorityResult result = WorldConfigOperations.Load(state.Snapshot, expectedRevision, loadResult.State.PlayerValues, normalizedVariant);

            if (NeedsPersistence(loadResult))
                new ConfigPersistedStateWriter(storage, _clock).Write(ConfigLocation.World, loadResult, state.CurrentDefaults);

            StoreState(key, state, result);
            return result;
        }

        public WorldConfigAuthorityResult SaveAs(string consumerId, string configKey, ulong expectedRevision, string variant)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeConfigKey(configKey);
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

            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(ConfigLocation.World, file, state.Snapshot.Identity, state.CurrentDefaults);
            ValidateDocument(loadResult, state.Snapshot.Applied, state.CurrentDefaults, nameof(state.Snapshot.Applied));
            PersistDocument(storage, loadResult, state.Snapshot.Applied, state.CurrentDefaults);

            WorldConfigAuthorityResult result = WorldConfigOperations.SaveAs(state.Snapshot, expectedRevision, normalizedVariant);
            StoreState(key, state, result);
            return result;
        }

        public string[] ListVariants(string consumerId, string configKey)
        {
            string normalizedConsumerId = NormalizeRequired(consumerId, nameof(consumerId));
            string normalizedConfigKey = NormalizeConfigKey(configKey);
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

        private void StoreState(string key, ServerState previous, WorldConfigAuthorityResult result)
        {
            if (!result.IsChanged)
                return;

            _states[key] = new ServerState(result.Snapshot, previous.CurrentDefaults);
            _bootstrapStore.Write(result.Snapshot);
        }

        private static string NormalizeConfigKey(string configKey)
        {
            string normalized = NormalizeRequired(configKey, nameof(configKey));
            if (normalized.IndexOf('.') >= 0)
                throw new ArgumentException("Config key must not contain '.'.", nameof(configKey));

            return normalized;
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
            ConfigDefaultReconciliationResult validation = ConfigDefaultReconciler.Reconcile(loadResult.State.BaselineDefaults, document, currentDefaults);
            if (!validation.PlayerValues.Equals(document))
                throw new ArgumentException("Player values do not match the current config schema.", parameterName);
        }

        private void PersistDocument(IConfigTextStorage storage, ConfigPersistedLoadResult loadResult, ConfigDocument document, ConfigDocument currentDefaults)
        {
            var persistedState = new ConfigPersistedState(loadResult.State.Identity, document, loadResult.State.BaselineDefaults, loadResult.State.CurrentFile);
            var saveResult = new ConfigPersistedLoadResult(
                persistedState, loadResult.ActiveSource, loadResult.ProvenanceFile,
                loadResult.WasActiveFileMissing, loadResult.WasProvenanceMissing,
                loadResult.Changes, loadResult.RequiresBackup);

            new ConfigPersistedStateWriter(storage, _clock).Write(ConfigLocation.World, saveResult, currentDefaults);
        }

        private static WorldConfigAuthorityResult RejectStale(ServerState state, ulong expectedRevision)
        {
            if (expectedRevision == state.Snapshot.Revision)
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