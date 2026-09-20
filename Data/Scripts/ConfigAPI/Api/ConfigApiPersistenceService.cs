using System;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;

namespace MarcoZechner.ConfigAPI.V2.Api
{
    public sealed class ConfigApiPersistenceService
    {
        private readonly IConfigClock _clock;
        private readonly ConfigConsumerRegistrationRegistry _registry;

        public ConfigApiPersistenceService(ConfigConsumerRegistrationRegistry registry, IConfigClock clock)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            if (clock == null)
                throw new ArgumentNullException(nameof(clock));

            _registry = registry;
            _clock = clock;
        }

        public object Open(string consumerId, Guid registrationId, string configKey, int location, string file, 
                           object currentDefaultsPayload)
        {
            IConfigTextStorage storage = _registry.GetStorage(consumerId, registrationId);

            ConfigLocation configLocation = ParseLocation(location);
            ConfigDocument currentDefaults = ConfigDocumentWireCodec.Decode(currentDefaultsPayload);

            var identity = new ConfigIdentity(consumerId.Trim(), configKey);

            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage)
                .Load(configLocation, file, identity, currentDefaults);

            if (NeedsPersistence(loadResult))
                new ConfigPersistedStateWriter(storage, _clock).Write(configLocation, loadResult, currentDefaults);

            return ConfigDocumentWireCodec.Encode(loadResult.State.PlayerValues);
        }

        public object Save(string consumerId, Guid registrationId, string configKey, int location, string file, 
                           object currentDefaultsPayload, object playerValuesPayload)
        {
            IConfigTextStorage storage = _registry.GetStorage(consumerId, registrationId);

            ConfigLocation configLocation = ParseLocation(location);
            ConfigDocument currentDefaults = ConfigDocumentWireCodec.Decode(currentDefaultsPayload);

            ConfigDocument playerValues = ConfigDocumentWireCodec.Decode(playerValuesPayload);

            var identity = new ConfigIdentity(consumerId.Trim(), configKey);

            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage)
                .Load(configLocation, file, identity, currentDefaults);

            ConfigDefaultReconciliationResult validation = ConfigDefaultReconciler
                .Reconcile(loadResult.State.BaselineDefaults, playerValues, currentDefaults);

            if (!validation.PlayerValues.Equals(playerValues))
                throw new ArgumentException("Player values do not match the current config schema.", nameof(playerValuesPayload));

            var state = new ConfigPersistedState(
                loadResult.State.Identity,
                playerValues,
                loadResult.State.BaselineDefaults,
                loadResult.State.CurrentFile
            );

            var saveResult = new ConfigPersistedLoadResult(
                state,
                loadResult.ActiveSource,
                loadResult.ProvenanceFile,
                loadResult.WasActiveFileMissing,
                loadResult.WasProvenanceMissing,
                loadResult.Changes,
                loadResult.RequiresBackup
            );

            new ConfigPersistedStateWriter(storage, _clock)
                .Write(configLocation, saveResult, currentDefaults);

            return ConfigDocumentWireCodec.Encode(playerValues);
        }

        private static bool NeedsPersistence(ConfigPersistedLoadResult loadResult) 
            => loadResult.WasActiveFileMissing || loadResult.WasProvenanceMissing || loadResult.Changes.Count > 0;

        private static ConfigLocation ParseLocation(int location)
        {
            switch (location)
            {
                case 0:
                    return ConfigLocation.Local;

                case 1:
                    return ConfigLocation.Global;

                case 2:
                    throw new InvalidOperationException(
                        "World configs require the server-authoritative ConfigAPI path and cannot use direct persistence."
                    );

                default:
                    throw new ArgumentException($"Unsupported ConfigAPI storage location: {location}", nameof(location));
            }
        }
    }
}
