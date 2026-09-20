using System;
using System.Collections.Generic;

namespace MarcoZechner.ConfigAPI.V2.Domain
{
    public sealed class ConfigDefaultReconciliationResult
    {
        public ConfigDocument BaselineDefaults { get; }
        public ConfigDocument PlayerValues { get; }
        public IReadOnlyList<ConfigDefaultChange> Changes { get; }

        public bool RequiresBackup { get; }

        internal ConfigDefaultReconciliationResult(ConfigDocument baselineDefaults, ConfigDocument playerValues, 
                                                   IList<ConfigDefaultChange> changes, bool requiresBackup)
        {
            if (baselineDefaults == null)
                throw new ArgumentNullException(nameof(baselineDefaults));

            if (playerValues == null)
                throw new ArgumentNullException(nameof(playerValues));

            if (changes == null)
                throw new ArgumentNullException(nameof(changes));

            BaselineDefaults = baselineDefaults;
            PlayerValues = playerValues;
            RequiresBackup = requiresBackup;

            var changesCopy = new ConfigDefaultChange[changes.Count];

            for (var i = 0; i < changes.Count; i++)
                changesCopy[i] = changes[i];

            Changes = Array.AsReadOnly(changesCopy);
        }
    }
}
