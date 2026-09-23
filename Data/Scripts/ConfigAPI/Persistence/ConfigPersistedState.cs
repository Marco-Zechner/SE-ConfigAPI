using System;
using MarcoZechner.ConfigAPI.Domain;

namespace MarcoZechner.ConfigAPI.Persistence
{
    public sealed class ConfigPersistedState
    {
        public ConfigIdentity Identity { get; }
        public ConfigDocument PlayerValues { get; }
        public ConfigDocument BaselineDefaults { get; }
        public string CurrentFile { get; }

        public ConfigPersistedState(ConfigIdentity identity, ConfigDocument playerValues, ConfigDocument baselineDefaults, string currentFile)
        {
            if (identity == null)
                throw new ArgumentNullException(nameof(identity));

            if (playerValues == null)
                throw new ArgumentNullException(nameof(playerValues));

            if (baselineDefaults == null)
                throw new ArgumentNullException(nameof(baselineDefaults));

            Identity = identity;
            PlayerValues = playerValues;
            BaselineDefaults = baselineDefaults;
            CurrentFile = currentFile;
        }
    }
}
