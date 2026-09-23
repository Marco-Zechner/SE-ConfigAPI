using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Domain;
using MarcoZechner.ConfigAPI.Serialization;

namespace MarcoZechner.ConfigAPI.Persistence
{
    public sealed class ConfigPersistedLoadResult
    {
        public ConfigPersistedState State { get; }
        public string ActiveSource { get; }
        public string DefaultsFile { get; }
        public bool WasActiveFileMissing { get; }
        public bool WasDefaultsEntryMissing { get; }
        public IReadOnlyList<ConfigDefaultChange> Changes { get; }
        public bool RequiresBackup { get; }

        internal ConfigDefaultsStore DefaultsStore { get; }

        internal ConfigPersistedLoadResult(ConfigPersistedState state, string activeSource, string defaultsFile, ConfigDefaultsStore defaultsStore,
                                           bool wasActiveFileMissing, bool wasDefaultsEntryMissing,
                                           IReadOnlyList<ConfigDefaultChange> changes, bool requiresBackup)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (string.IsNullOrWhiteSpace(defaultsFile))
                throw new ArgumentException("Defaults file must not be empty.", nameof(defaultsFile));
            if (defaultsStore == null)
                throw new ArgumentNullException(nameof(defaultsStore));
            if (changes == null)
                throw new ArgumentNullException(nameof(changes));

            State = state;
            ActiveSource = activeSource;
            DefaultsFile = defaultsFile;
            DefaultsStore = defaultsStore;
            WasActiveFileMissing = wasActiveFileMissing;
            WasDefaultsEntryMissing = wasDefaultsEntryMissing;
            Changes = changes;
            RequiresBackup = requiresBackup;
        }
    }

    public sealed class ConfigPersistedStateLoader
    {
        public const string DefaultsFileName = ".defaults";

        private readonly IConfigTextStorage _storage;

        public ConfigPersistedStateLoader(IConfigTextStorage storage)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            _storage = storage;
        }

        public ConfigPersistedLoadResult Load(ConfigLocation location, string activeFile, ConfigIdentity identity, ConfigDocument currentDefaults)
        {
            if (string.IsNullOrWhiteSpace(activeFile))
                throw new ArgumentException("Config file must not be empty.", nameof(activeFile));
            if (identity == null)
                throw new ArgumentNullException(nameof(identity));
            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));

            string activeSource = _storage.Read(location, activeFile);
            string defaultsSource = _storage.Read(location, DefaultsFileName);
            ConfigDefaultsStore defaultsStore = defaultsSource == null ? new ConfigDefaultsStore() : ConfigDefaultsStoreCodec.Decode(defaultsSource);

            bool wasActiveFileMissing = activeSource == null;
            ConfigDefaultsEntry defaultsEntry;
            bool hasDefaultsEntry = defaultsStore.TryGet(activeFile, out defaultsEntry);

            if (wasActiveFileMissing && hasDefaultsEntry)
                throw new InvalidOperationException("Config defaults entry exists without its config file.");

            bool wasDefaultsEntryMissing = !hasDefaultsEntry;

            ConfigDocument playerValues;
            ConfigDocument baselineDefaults;

            if (wasActiveFileMissing)
            {
                playerValues = currentDefaults;
                baselineDefaults = currentDefaults;
            }
            else
            {
                playerValues = ConfigTomlSourceDecoder.Decode(activeSource, currentDefaults);

                if (!hasDefaultsEntry)
                {
                    playerValues = FillMissingKnownValues(playerValues, currentDefaults);
                    baselineDefaults = currentDefaults;
                }
                else
                {
                    if (!defaultsEntry.Identity.Equals(identity))
                        throw new InvalidOperationException("Config defaults identity does not match the requested config identity.");

                    baselineDefaults = defaultsEntry.BaselineDefaults;
                }
            }

            var state = new ConfigPersistedState(identity, playerValues, baselineDefaults, activeFile);
            ConfigPersistedStateReconciliationResult reconciliationResult = ConfigPersistedStateReconciler.Reconcile(state, currentDefaults);

            return new ConfigPersistedLoadResult(reconciliationResult.State, activeSource, DefaultsFileName, defaultsStore,
                                                 wasActiveFileMissing, wasDefaultsEntryMissing,
                                                 reconciliationResult.Changes, reconciliationResult.RequiresBackup);
        }

        private static ConfigDocument FillMissingKnownValues(ConfigDocument playerValues, ConfigDocument currentDefaults)
            => new ConfigDocument(FillMissingKnownValues(playerValues.Root, currentDefaults.Root));

        private static ConfigObjectNode FillMissingKnownValues(ConfigObjectNode player, ConfigObjectNode currentDefaults)
        {
            var entries = new List<ConfigObjectEntry>(player.Entries.Count + currentDefaults.Entries.Count);

            foreach (ConfigObjectEntry playerEntry in player.Entries)
            {
                ConfigNode currentDefault;

                if (currentDefaults.TryGet(playerEntry.Name, out currentDefault))
                {
                    var playerObject = playerEntry.Value as ConfigObjectNode;
                    var defaultObject = currentDefault as ConfigObjectNode;

                    if (playerObject != null && defaultObject != null)
                    {
                        entries.Add(new ConfigObjectEntry(playerEntry.Name, FillMissingKnownValues(playerObject, defaultObject)));
                        continue;
                    }
                }

                entries.Add(playerEntry);
            }

            foreach (ConfigObjectEntry defaultEntry in currentDefaults.Entries)
            {
                ConfigNode ignored;
                if (!player.TryGet(defaultEntry.Name, out ignored))
                    entries.Add(defaultEntry);
            }

            return new ConfigObjectNode(entries.ToArray());
        }
    }
}