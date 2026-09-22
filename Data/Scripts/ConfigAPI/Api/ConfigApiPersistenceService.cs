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

        public ConfigApiPersistenceService(ConfigConsumerRegistrationRegistry registry, IConfigClock clock) : this(registry, clock, null) { }

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
                ConfigDocument result = OpenCore(storage, consumerId, configKey, configLocation, file, currentDefaults);
                Log(LogLevel.Debug, $"Open completed: consumer='{consumerId}', config='{configKey}', file='{file}'.");
                return ConfigDocumentWireCodec.Encode(result);
            }
            catch (Exception exception)
            {
                Log(LogLevel.Error, $"Open failed: consumer='{consumerId}', config='{configKey}', location={location}, file='{file}'.", exception);
                throw;
            }
        }

        internal ConfigDocument OpenInternal(string consumerId, string configKey, ConfigLocation location, string file, ConfigDocument currentDefaults)
        {
            ValidateDirectLocation(location);
            IConfigTextStorage storage = _registry.GetCurrentStorage(consumerId);
            return OpenCore(storage, consumerId, configKey, location, file, currentDefaults);
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
                ConfigDocument result = SaveCore(storage, consumerId, configKey, configLocation, file, currentDefaults, playerValues);
                Log(LogLevel.Debug, $"Save completed: consumer='{consumerId}', config='{configKey}', file='{file}'.");
                return ConfigDocumentWireCodec.Encode(result);
            }
            catch (Exception exception)
            {
                Log(LogLevel.Error, $"Save failed: consumer='{consumerId}', config='{configKey}', location={location}, file='{file}'.", exception);
                throw;
            }
        }

        internal ConfigDocument SaveInternal(string consumerId, string configKey, ConfigLocation location, string file, ConfigDocument currentDefaults, ConfigDocument playerValues)
        {
            ValidateDirectLocation(location);
            IConfigTextStorage storage = _registry.GetCurrentStorage(consumerId);
            return SaveCore(storage, consumerId, configKey, location, file, currentDefaults, playerValues);
        }

        public object ApplyPreset(string consumerId, Guid registrationId, string configKey, int location, string canonicalFile, string presetFile, object currentDefaultsPayload)
        {
            Log(LogLevel.Debug, $"ApplyPreset requested: consumer='{consumerId}', config='{configKey}', location={location}, canonical='{canonicalFile}', preset='{presetFile}'.");

            try
            {
                if (string.IsNullOrWhiteSpace(presetFile))
                    throw new ArgumentException("Preset file must not be empty.", nameof(presetFile));

                IConfigTextStorage storage = _registry.GetStorage(consumerId, registrationId);
                ConfigLocation configLocation = ParseLocation(location);
                ConfigDocument currentDefaults = ConfigDocumentWireCodec.Decode(currentDefaultsPayload);
                var identity = new ConfigIdentity(consumerId.Trim(), configKey);

                if (storage.Read(configLocation, presetFile) == null)
                    throw new InvalidOperationException("Preset config file does not exist: " + presetFile);

                ConfigPersistedLoadResult preset = new ConfigPersistedStateLoader(storage).Load(configLocation, presetFile, identity, currentDefaults);
                object result = Save(consumerId, registrationId, configKey, location, canonicalFile, currentDefaultsPayload, ConfigDocumentWireCodec.Encode(preset.State.PlayerValues));

                Log(LogLevel.Debug, $"ApplyPreset completed: consumer='{consumerId}', config='{configKey}', canonical='{canonicalFile}', preset='{presetFile}'.");
                return result;
            }
            catch (Exception exception)
            {
                Log(LogLevel.Error, $"ApplyPreset failed: consumer='{consumerId}', config='{configKey}', location={location}, canonical='{canonicalFile}', preset='{presetFile}'.", exception);
                throw;
            }
        }

        public object SavePreset(string consumerId, Guid registrationId, string configKey, int location, string canonicalFile, string presetFile, object currentDefaultsPayload, object playerValuesPayload, bool overwrite)
        {
            Log(LogLevel.Debug, $"SavePreset requested: consumer='{consumerId}', config='{configKey}', location={location}, canonical='{canonicalFile}', preset='{presetFile}', overwrite={overwrite}.");

            try
            {
                if (string.IsNullOrWhiteSpace(canonicalFile))
                    throw new ArgumentException("Canonical config file must not be empty.", nameof(canonicalFile));
                if (string.IsNullOrWhiteSpace(presetFile))
                    throw new ArgumentException("Preset file must not be empty.", nameof(presetFile));
                if (string.Equals(canonicalFile, presetFile, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Preset target must not be the canonical active config file: " + presetFile);

                IConfigTextStorage storage = _registry.GetStorage(consumerId, registrationId);
                ConfigLocation configLocation = ParseLocation(location);

                if (!overwrite && (storage.Read(configLocation, presetFile) != null || storage.Read(configLocation, ConfigPersistedStateLoader.GetProvenanceFile(presetFile)) != null))
                    throw new InvalidOperationException("Config preset target already exists: " + presetFile);

                object result = Save(consumerId, registrationId, configKey, location, presetFile, currentDefaultsPayload, playerValuesPayload);
                Log(LogLevel.Debug, $"SavePreset completed: consumer='{consumerId}', config='{configKey}', preset='{presetFile}', overwrite={overwrite}.");
                return result;
            }
            catch (Exception exception)
            {
                Log(LogLevel.Error, $"SavePreset failed: consumer='{consumerId}', config='{configKey}', location={location}, canonical='{canonicalFile}', preset='{presetFile}', overwrite={overwrite}.", exception);
                throw;
            }
        }

        private ConfigDocument OpenCore(IConfigTextStorage storage, string consumerId, string configKey, ConfigLocation location, string file, ConfigDocument currentDefaults)
        {
            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));

            var identity = new ConfigIdentity(consumerId.Trim(), configKey);
            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(location, file, identity, currentDefaults);
            bool needsPersistence = NeedsPersistence(loadResult);

            Log(LogLevel.Trace, $"Open loaded: consumer='{consumerId}', config='{configKey}', file='{file}', activeMissing={loadResult.WasActiveFileMissing}, provenanceMissing={loadResult.WasProvenanceMissing}, changes={loadResult.Changes.Count}, requiresBackup={loadResult.RequiresBackup}, needsPersistence={needsPersistence}.");

            if (needsPersistence)
            {
                Log(LogLevel.Debug, $"Open is persisting reconciled config '{consumerId}/{configKey}' to '{file}'.");
                ConfigPersistedWriteResult writeResult = new ConfigPersistedStateWriter(storage, _clock).Write(location, loadResult, currentDefaults);
                Log(LogLevel.Trace, $"Open persistence completed: consumer='{consumerId}', config='{configKey}', file='{file}', backup='{writeResult.BackupFile ?? "<none>"}', canonicalRegeneration={writeResult.UsedCanonicalRegeneration}.");
            }

            return loadResult.State.PlayerValues;
        }

        private ConfigDocument SaveCore(IConfigTextStorage storage, string consumerId, string configKey, ConfigLocation location, string file, ConfigDocument currentDefaults, ConfigDocument playerValues)
        {
            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));
            if (playerValues == null)
                throw new ArgumentNullException(nameof(playerValues));

            var identity = new ConfigIdentity(consumerId.Trim(), configKey);
            ConfigPersistedLoadResult loadResult = new ConfigPersistedStateLoader(storage).Load(location, file, identity, currentDefaults);

            Log(LogLevel.Trace, $"Save loaded current state: consumer='{consumerId}', config='{configKey}', file='{file}', activeMissing={loadResult.WasActiveFileMissing}, provenanceMissing={loadResult.WasProvenanceMissing}, changes={loadResult.Changes.Count}, requiresBackup={loadResult.RequiresBackup}.");

            ConfigDefaultReconciliationResult validation = ConfigDefaultReconciler.Reconcile(loadResult.State.BaselineDefaults, playerValues, currentDefaults);

            if (!validation.PlayerValues.Equals(playerValues))
                throw new ArgumentException("Player values do not match the current config schema.", nameof(playerValues));

            var state = new ConfigPersistedState(loadResult.State.Identity, playerValues, loadResult.State.BaselineDefaults, loadResult.State.CurrentFile);
            var saveResult = new ConfigPersistedLoadResult(state, loadResult.ActiveSource, loadResult.ProvenanceFile, loadResult.WasActiveFileMissing, loadResult.WasProvenanceMissing, loadResult.Changes, loadResult.RequiresBackup);

            ConfigPersistedWriteResult writeResult = new ConfigPersistedStateWriter(storage, _clock).Write(location, saveResult, currentDefaults);
            Log(LogLevel.Trace, $"Save persistence completed: consumer='{consumerId}', config='{configKey}', file='{file}', backup='{writeResult.BackupFile ?? "<none>"}', canonicalRegeneration={writeResult.UsedCanonicalRegeneration}.");
            return playerValues;
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
                case 0: return ConfigLocation.Local;
                case 1: return ConfigLocation.Global;
                case 2: throw new InvalidOperationException("World configs require the server-authoritative ConfigAPI path and cannot use direct persistence.");
                default: throw new ArgumentException($"Unsupported ConfigAPI storage location: {location}", nameof(location));
            }
        }

        private static void ValidateDirectLocation(ConfigLocation location)
        {
            if (location == ConfigLocation.Local || location == ConfigLocation.Global)
                return;
            if (location == ConfigLocation.World)
                throw new InvalidOperationException("World configs require the server-authoritative ConfigAPI path and cannot use direct persistence.");

            throw new ArgumentException("Unsupported ConfigAPI storage location: " + location, nameof(location));
        }
    }
}