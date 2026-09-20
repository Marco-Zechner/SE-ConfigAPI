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
        private readonly Dictionary<string, ServerState> _states = new Dictionary<string, ServerState>(StringComparer.Ordinal);

        public WorldConfigServerService(ConfigConsumerRegistrationRegistry registry, IConfigClock clock)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            if (clock == null)
                throw new ArgumentNullException(nameof(clock));

            _registry = registry;
            _clock = clock;
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
            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(ConfigLocation.World, file, identity, currentDefaults);

            if (NeedsPersistence(loadResult))
                new ConfigPersistedStateWriter(storage, _clock).Write(ConfigLocation.World, loadResult, currentDefaults);

            var snapshot = new WorldConfigSnapshot(identity, loadResult.State.PlayerValues, 0UL, loadResult.State.CurrentFile);
            _states.Add(key, new ServerState(snapshot, currentDefaults));
            return snapshot;
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
            return authority;
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