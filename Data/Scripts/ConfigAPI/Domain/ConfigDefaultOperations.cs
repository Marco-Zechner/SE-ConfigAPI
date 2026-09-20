using System;
using System.Collections.Generic;

namespace MarcoZechner.ConfigAPI.V2.Domain
{
    public static class ConfigDefaultOperations
    {
        public static ConfigDefaultOperationResult RevertToDefault(
            ConfigDocument baselineDefaults, ConfigDocument playerValues, ConfigDocument currentDefaults, ConfigValuePath path)
        {
            if (baselineDefaults == null)
                throw new ArgumentNullException(nameof(baselineDefaults));

            if (playerValues == null)
                throw new ArgumentNullException(nameof(playerValues));

            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));

            if (path == null)
                throw new ArgumentNullException(nameof(path));

            ConfigNode baselineValue;
            if (!baselineDefaults.TryGet(path, out baselineValue))
                throw new KeyNotFoundException("Config value path does not exist in baseline defaults.");

            ConfigNode playerValue;
            if (!playerValues.TryGet(path, out playerValue))
                throw new KeyNotFoundException("Config value path does not exist in player values.");

            ConfigNode currentDefault;
            if (!currentDefaults.TryGet(path, out currentDefault))
                throw new KeyNotFoundException("Config value path does not exist in current defaults.");

            return new ConfigDefaultOperationResult(
                baselineDefaults.WithValue(path, currentDefault),
                playerValues.WithValue(path, currentDefault));
        }
    }
}
