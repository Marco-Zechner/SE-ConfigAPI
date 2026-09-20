using System;

namespace MarcoZechner.ConfigAPI.V2.Domain
{
    public sealed class ConfigDefaultOperationResult
    {
        public ConfigDocument BaselineDefaults { get; }
        public ConfigDocument PlayerValues { get; }

        internal ConfigDefaultOperationResult(ConfigDocument baselineDefaults, ConfigDocument playerValues)
        {
            if (baselineDefaults == null)
                throw new ArgumentNullException(nameof(baselineDefaults));

            if (playerValues == null)
                throw new ArgumentNullException(nameof(playerValues));

            BaselineDefaults = baselineDefaults;
            PlayerValues = playerValues;
        }
    }
}
