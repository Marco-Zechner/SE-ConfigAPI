using System;
using MarcoZechner.ConfigAPI.V2.Domain;

namespace MarcoZechner.ConfigAPI.V2.Persistence
{
    public sealed class ConfigProvenance
    {
        public ConfigIdentity Identity { get; }
        public ConfigDocument BaselineDefaults { get; }

        public ConfigProvenance(ConfigIdentity identity, ConfigDocument baselineDefaults)
        {
            if (identity == null)
                throw new ArgumentNullException(nameof(identity));

            if (baselineDefaults == null)
                throw new ArgumentNullException(nameof(baselineDefaults));

            Identity = identity;
            BaselineDefaults = baselineDefaults;
        }
    }
}
