using System;
using System.Collections.Generic;

namespace MarcoZechner.ConfigAPI.Domain
{
    public sealed class ConfigDefaultStatus
    {
        public ConfigValuePath Path { get; }
        public ConfigNode BaselineDefault { get; }
        public ConfigNode PlayerValue { get; }
        public ConfigNode CurrentDefault { get; }

        public bool IsOverride => !PlayerValue.Equals(CurrentDefault);

        public bool HasPendingDefaultChange =>
            !CurrentDefault.Equals(BaselineDefault) &&
            !PlayerValue.Equals(BaselineDefault);

        private ConfigDefaultStatus(ConfigValuePath path, 
                                    ConfigNode baselineDefault, ConfigNode playerValue, ConfigNode currentDefault)
        {
            Path = path;
            BaselineDefault = baselineDefault;
            PlayerValue = playerValue;
            CurrentDefault = currentDefault;
        }

        public static ConfigDefaultStatus Get(ConfigDocument baselineDefaults, ConfigDocument playerValues, ConfigDocument currentDefaults,
                                              ConfigValuePath path)
        {
            if (baselineDefaults == null)
                throw new ArgumentNullException(nameof(baselineDefaults));

            if (playerValues == null)
                throw new ArgumentNullException(nameof(playerValues));

            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));

            if (path == null)
                throw new ArgumentNullException(nameof(path));

            ConfigNode baselineDefault;
            if (!baselineDefaults.TryGet(path, out baselineDefault))
                throw new KeyNotFoundException("Config value path does not exist in baseline defaults.");

            ConfigNode playerValue;
            if (!playerValues.TryGet(path, out playerValue))
                throw new KeyNotFoundException("Config value path does not exist in player values.");

            ConfigNode currentDefault;
            if (!currentDefaults.TryGet(path, out currentDefault))
                throw new KeyNotFoundException("Config value path does not exist in current defaults.");

            return new ConfigDefaultStatus(path, baselineDefault, playerValue, currentDefault);
        }
    }
}
