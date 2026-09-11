using System;
using Mz.SemanticVersioning;

namespace Mz.Networking.SpaceEngineers
{
    /// <summary>
    /// Configures a Space Engineers networking session that can use a local
    /// NetworkManager provider while remaining immediately usable on a
    /// fallback channel.
    /// </summary>
    public sealed class SpaceEngineersManagedNetworkConfiguration
    {
        /// <summary>
        /// Creates managed networking configuration.
        /// </summary>
        public SpaceEngineersManagedNetworkConfiguration(
            string modId, string modDisplayName, SemanticVersion modVersion, string networkId, string networkName, 
            ushort preferredChannel, ushort? forcedChannel)
        {
            ModId = NormalizeRequired(modId, nameof(modId));
            ModDisplayName = NormalizeRequired(modDisplayName, nameof(modDisplayName));

            if (modVersion == null)
                throw new ArgumentNullException(nameof(modVersion));

            ModVersion = modVersion;
            NetworkId = SpaceEngineersNetworkIdentity.Normalize(networkId);
            NetworkName = NormalizeRequired(networkName, nameof(networkName));
            PreferredChannel = preferredChannel;
            ForcedChannel = forcedChannel;
        }

        /// <summary>
        /// Gets the stable case-sensitive consuming mod identifier.
        /// </summary>
        public string ModId { get; }

        /// <summary>
        /// Gets the human-readable consuming mod name.
        /// </summary>
        public string ModDisplayName { get; }

        /// <summary>
        /// Gets the consuming mod version.
        /// </summary>
        public SemanticVersion ModVersion { get; }

        /// <summary>
        /// Gets the stable Mz.Networking wire identity.
        /// </summary>
        public string NetworkId { get; }

        /// <summary>
        /// Gets the human-readable network name.
        /// </summary>
        public string NetworkName { get; }

        /// <summary>
        /// Gets the immediately active fallback and preferred channel.
        /// </summary>
        public ushort PreferredChannel { get; }

        /// <summary>
        /// Gets the channel that disables discovery and reassignment, or null
        /// when NetworkManager integration is enabled.
        /// </summary>
        public ushort? ForcedChannel { get; }

        internal ushort InitialChannel => ForcedChannel ?? PreferredChannel;

        private static string NormalizeRequired(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A non-empty value is required.", parameterName);

            return value.Trim();
        }
    }
}
