using System;
using Mz.SemanticVersioning;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Identifies an API exposed by a provider.
    /// </summary>
    public sealed class ApiDescriptor
    {
        /// <summary>
        /// Gets the stable, case-sensitive API identifier.
        /// </summary>
        public string ApiId { get; }

        /// <summary>
        /// Gets the version exposed by the provider.
        /// </summary>
        public SemanticVersion Version { get; }

        /// <summary>
        /// Creates an API descriptor.
        /// </summary>
        public ApiDescriptor(string apiId, SemanticVersion version)
        {
            if (string.IsNullOrWhiteSpace(apiId))
                throw new ArgumentException("An API identifier is required.", nameof(apiId));

            if (version == null)
                throw new ArgumentNullException(nameof(version));

            ApiId = apiId.Trim();
            Version = version;
        }

        /// <summary>
        /// Returns the API identifier and provider version.
        /// </summary>
        public override string ToString() => $"{ApiId} {Version}";
    }
}