using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Serialization;

namespace MarcoZechner.ConfigAPI.V2.Persistence
{
    public sealed class ConfigPersistedLoadResult
    {
        public ConfigPersistedState State { get; }
        public string ActiveSource { get; }
        public string ProvenanceFile { get; }
        public bool WasActiveFileMissing { get; }
        public bool WasProvenanceMissing { get; }
        public IReadOnlyList<ConfigDefaultChange> Changes { get; }
        public bool RequiresBackup { get; }

        internal ConfigPersistedLoadResult(ConfigPersistedState state,
                                           string activeSource, string provenanceFile,
                                           bool wasActiveFileMissing, bool wasProvenanceMissing,
                                           IReadOnlyList<ConfigDefaultChange> changes, bool requiresBackup)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (string.IsNullOrWhiteSpace(provenanceFile))
                throw new ArgumentException("Provenance file must not be empty.", nameof(provenanceFile));

            if (changes == null)
                throw new ArgumentNullException(nameof(changes));

            State = state;
            ActiveSource = activeSource;
            ProvenanceFile = provenanceFile;
            WasActiveFileMissing = wasActiveFileMissing;
            WasProvenanceMissing = wasProvenanceMissing;
            Changes = changes;
            RequiresBackup = requiresBackup;
        }
    }

    public sealed class ConfigPersistedStateLoader
    {
        private const string ProvenanceSuffix = ".configapi.provenance";

        private readonly IConfigTextStorage _storage;

        public ConfigPersistedStateLoader(IConfigTextStorage storage)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            _storage = storage;
        }

        public static string GetProvenanceFile(string activeFile)
        {
            if (string.IsNullOrWhiteSpace(activeFile))
                throw new ArgumentException("Config file must not be empty.", nameof(activeFile));

            return activeFile + ProvenanceSuffix;
        }

        public ConfigPersistedLoadResult Load(ConfigLocation location, string activeFile, ConfigIdentity identity, ConfigDocument currentDefaults)
        {
            if (string.IsNullOrWhiteSpace(activeFile))
                throw new ArgumentException("Config file must not be empty.", nameof(activeFile));

            if (identity == null)
                throw new ArgumentNullException(nameof(identity));

            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));

            string provenanceFile = GetProvenanceFile(activeFile);

            string activeSource = _storage.Read(location, activeFile);

            string provenanceSource = _storage.Read(location, provenanceFile);

            bool wasActiveFileMissing = activeSource == null;

            bool wasProvenanceMissing = provenanceSource == null;

            if (wasActiveFileMissing && !wasProvenanceMissing)
                throw new InvalidOperationException("Config provenance exists but the active TOML file is missing.");

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

                if (wasProvenanceMissing)
                {
                    playerValues = FillMissingKnownValues(playerValues, currentDefaults);
                    baselineDefaults = currentDefaults;
                }
                else
                {
                    ConfigProvenance provenance = ConfigProvenanceCodec.Decode(provenanceSource);

                    if (!provenance.Identity.Equals(identity))
                        throw new InvalidOperationException("Config provenance identity does not match the requested config identity.");

                    baselineDefaults = provenance.BaselineDefaults;
                }
            }

            var state = new ConfigPersistedState(identity, playerValues, baselineDefaults, activeFile);

            var reconciliationResult = ConfigPersistedStateReconciler.Reconcile(state, currentDefaults);
            
            return new ConfigPersistedLoadResult(
                reconciliationResult.State,
                activeSource, provenanceFile,
                wasActiveFileMissing, wasProvenanceMissing,
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
                        ConfigObjectNode missingKnownValues = FillMissingKnownValues(playerObject, defaultObject);
                        entries.Add(new ConfigObjectEntry(playerEntry.Name, missingKnownValues));
                        continue;
                    }
                }

                entries.Add(playerEntry);
            }

            foreach (ConfigObjectEntry defaultEntry in currentDefaults.Entries)
            {
                ConfigNode ignored;

                if (player.TryGet(defaultEntry.Name, out ignored))
                    continue;

                entries.Add(defaultEntry);
            }

            return new ConfigObjectNode(entries.ToArray());
        }
    }
}
