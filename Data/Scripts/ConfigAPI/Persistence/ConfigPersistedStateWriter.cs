using System;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Serialization;

namespace MarcoZechner.ConfigAPI.V2.Persistence
{
    public sealed class ConfigPersistedWriteResult
    {
        public string ActiveSource { get; }
        public string ProvenanceFile { get; }
        public string ProvenanceSource { get; }
        public string BackupFile { get; }
        public bool UsedCanonicalRegeneration { get; }

        internal ConfigPersistedWriteResult(string activeSource, string provenanceFile, string provenanceSource,
                                            string backupFile, bool usedCanonicalRegeneration)
        {
            if (activeSource == null)
                throw new ArgumentNullException(nameof(activeSource));

            if (string.IsNullOrWhiteSpace(provenanceFile))
                throw new ArgumentException("Provenance file must not be empty.", nameof(provenanceFile));

            if (provenanceSource == null)
                throw new ArgumentNullException(nameof(provenanceSource));

            ActiveSource = activeSource;
            ProvenanceFile = provenanceFile;
            ProvenanceSource = provenanceSource;
            BackupFile = backupFile;
            UsedCanonicalRegeneration = usedCanonicalRegeneration;
        }
    }

    public sealed class ConfigPersistedStateWriter
    {
        private readonly IConfigTextStorage _storage;
        private readonly ConfigTextWriteCoordinator _writeCoordinator;

        public ConfigPersistedStateWriter(IConfigTextStorage storage, IConfigClock clock)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            if (clock == null)
                throw new ArgumentNullException(nameof(clock));

            _storage = storage;
            _writeCoordinator = new ConfigTextWriteCoordinator(storage, clock);
        }

        public ConfigPersistedWriteResult Write(ConfigLocation location, ConfigPersistedLoadResult loadResult, ConfigDocument currentDefaults)
        {
            if (loadResult == null)
                throw new ArgumentNullException(nameof(loadResult));

            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));

            ConfigPersistedSourcePlan plan = ConfigPersistedSourcePlanner.Plan(loadResult, currentDefaults);

            string provenanceSource = ConfigProvenanceCodec.Encode(
                new ConfigProvenance(loadResult.State.Identity, loadResult.State.BaselineDefaults));

            ConfigTextWriteResult activeWrite = _writeCoordinator.Write(location, loadResult.State.CurrentFile, 
                                                                        plan.ActiveSource, plan.RequiresBackup);

            _storage.Write(location, loadResult.ProvenanceFile, provenanceSource);

            return new ConfigPersistedWriteResult(plan.ActiveSource, loadResult.ProvenanceFile, provenanceSource,
                                                  activeWrite.BackupFile, plan.UsedCanonicalRegeneration);
        }
    }
}
