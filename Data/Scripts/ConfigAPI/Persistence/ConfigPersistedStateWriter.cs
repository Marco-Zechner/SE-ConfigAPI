using System;
using MarcoZechner.ConfigAPI.Domain;
using MarcoZechner.ConfigAPI.Serialization;

namespace MarcoZechner.ConfigAPI.Persistence
{
    public sealed class ConfigPersistedWriteResult
    {
        public string ActiveSource { get; }
        public string DefaultsFile { get; }
        public string DefaultsSource { get; }
        public string BackupFile { get; }
        public bool UsedCanonicalRegeneration { get; }

        internal ConfigPersistedWriteResult(string activeSource, string defaultsFile, string defaultsSource, string backupFile, bool usedCanonicalRegeneration)
        {
            if (activeSource == null)
                throw new ArgumentNullException(nameof(activeSource));
            if (string.IsNullOrWhiteSpace(defaultsFile))
                throw new ArgumentException("Defaults file must not be empty.", nameof(defaultsFile));
            if (defaultsSource == null)
                throw new ArgumentNullException(nameof(defaultsSource));

            ActiveSource = activeSource;
            DefaultsFile = defaultsFile;
            DefaultsSource = defaultsSource;
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
            var entry = new ConfigDefaultsEntry(loadResult.State.CurrentFile, loadResult.State.Identity, loadResult.State.BaselineDefaults);
            ConfigDefaultsStore defaultsStore = loadResult.DefaultsStore.With(entry);
            string defaultsSource = ConfigDefaultsStoreCodec.Encode(defaultsStore);

            ConfigTextWriteResult activeWrite = _writeCoordinator.Write(location, loadResult.State.CurrentFile, plan.ActiveSource, plan.RequiresBackup);
            _storage.Write(location, loadResult.DefaultsFile, defaultsSource);

            return new ConfigPersistedWriteResult(plan.ActiveSource, loadResult.DefaultsFile, defaultsSource,
                                                  activeWrite.BackupFile, plan.UsedCanonicalRegeneration);
        }
    }
}