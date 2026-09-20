using System;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;
using Mz.Logging;

namespace MarcoZechner.ConfigAPI.V2.Api
{
    public sealed class ConfigApiPersistenceService
    {
        private readonly IConfigClock _clock;
        private readonly Logger _logger;
        private readonly ConfigConsumerRegistrationRegistry _registry;

        public ConfigApiPersistenceService(ConfigConsumerRegistrationRegistry registry, IConfigClock clock)
            : this(registry, clock, null) { }

        public ConfigApiPersistenceService(ConfigConsumerRegistrationRegistry registry, IConfigClock clock, Logger logger)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            if (clock == null)
                throw new ArgumentNullException(nameof(clock));

            _registry = registry;
            _clock = clock;
            _logger = logger;
        }

        public object Open(string consumerId, Guid registrationId, string configKey, int location, string file, object currentDefaultsPayload)
        {
            Log(LogLevel.Debug, $"Open requested: consumer='{consumerId}', config='{configKey}', location={location}, file='{file}'.");

            try
            {
                IConfigTextStorage storage = _registry.GetStorage(consumerId, registrationId);
                ConfigLocation configLocation = ParseLocation(location);
                ConfigDocument currentDefaults = ConfigDocumentWireCodec.Decode(currentDefaultsPayload);
                var identity = new ConfigIdentity(consumerId.Trim(), configKey);

                ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(configLocation, file, identity, currentDefaults);
                bool needsPersistence = NeedsPersistence(loadResult);

                Log(LogLevel.Trace, $"Open loaded: consumer='{consumerId}', config='{configKey}', file='{file}', activeMissing={loadResult.WasActiveFileMissing}, provenanceMissing={loadResult.WasProvenanceMissing}, changes={loadResult.Changes.Count}, requiresBackup={loadResult.RequiresBackup}, needsPersistence={needsPersistence}.");

                if (needsPersistence)
                {
                    Log(LogLevel.Debug, $"Open is persisting reconciled config '{consumerId}/{configKey}' to '{file}'.");
                    ConfigPersistedWriteResult writeResult = new ConfigPersistedStateWriter(storage, _clock).Write(configLocation, loadResult, currentDefaults);
                    Log(LogLevel.Trace, $"Open persistence completed: consumer='{consumerId}', config='{configKey}', file='{file}', backup='{writeResult.BackupFile ?? "<none>"}', canonicalRegeneration={writeResult.UsedCanonicalRegeneration}.");
                }

                Log(LogLevel.Debug, $"Open completed: consumer='{consumerId}', config='{configKey}', file='{file}'.");
                return ConfigDocumentWireCodec.Encode(loadResult.State.PlayerValues);
            }
            catch (Exception exception)
            {
                Log(LogLevel.Error, $"Open failed: consumer='{consumerId}', config='{configKey}', location={location}, file='{file}'.", exception);
                throw;
            }
        }

        public object Save(string consumerId, Guid registrationId, string configKey, int location, string file, object currentDefaultsPayload, object playerValuesPayload)
        {
            Log(LogLevel.Debug, $"Save requested: consumer='{consumerId}', config='{configKey}', location={location}, file='{file}'.");

            try
            {
                IConfigTextStorage storage = _registry.GetStorage(consumerId, registrationId);
                ConfigLocation configLocation = ParseLocation(location);
                ConfigDocument currentDefaults = ConfigDocumentWireCodec.Decode(currentDefaultsPayload);
                ConfigDocument playerValues = ConfigDocumentWireCodec.Decode(playerValuesPayload);
                var identity = new ConfigIdentity(consumerId.Trim(), configKey);

                ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(configLocation, file, identity, currentDefaults);

                Log(LogLevel.Trace, $"Save loaded current state: consumer='{consumerId}', config='{configKey}', file='{file}', activeMissing={loadResult.WasActiveFileMissing}, provenanceMissing={loadResult.WasProvenanceMissing}, changes={loadResult.Changes.Count}, requiresBackup={loadResult.RequiresBackup}.");

                ConfigDefaultReconciliationResult validation = ConfigDefaultReconciler.Reconcile(loadResult.State.BaselineDefaults, playerValues, currentDefaults);

                if (!validation.PlayerValues.Equals(playerValues))
                    throw new ArgumentException("Player values do not match the current config schema.", nameof(playerValuesPayload));

                var state = new ConfigPersistedState(loadResult.State.Identity, playerValues, loadResult.State.BaselineDefaults, loadResult.State.CurrentFile);
                var saveResult = new ConfigPersistedLoadResult(state, loadResult.ActiveSource, loadResult.ProvenanceFile, loadResult.WasActiveFileMissing, loadResult.WasProvenanceMissing, loadResult.Changes, loadResult.RequiresBackup);

                ConfigPersistedWriteResult writeResult = new ConfigPersistedStateWriter(storage, _clock).Write(configLocation, saveResult, currentDefaults);

                Log(LogLevel.Trace, $"Save persistence completed: consumer='{consumerId}', config='{configKey}', file='{file}', backup='{writeResult.BackupFile ?? "<none>"}', canonicalRegeneration={writeResult.UsedCanonicalRegeneration}.");
                Log(LogLevel.Debug, $"Save completed: consumer='{consumerId}', config='{configKey}', file='{file}'.");

                return ConfigDocumentWireCodec.Encode(playerValues);
            }
            catch (Exception exception)
            {
                Log(LogLevel.Error, $"Save failed: consumer='{consumerId}', config='{configKey}', location={location}, file='{file}'.", exception);
                throw;
            }
        }

        private void Log(LogLevel level, string message, Exception exception = null)
        {
            if (_logger == null)
                return;

            try
            {
                _logger.Write(level, message, exception);
            }
            catch
            {
            }
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
                    throw new InvalidOperationException("World configs require the server-authoritative ConfigAPI path and cannot use direct persistence.");

                default:
                    throw new ArgumentException($"Unsupported ConfigAPI storage location: {location}", nameof(location));
            }
        }
    }
}