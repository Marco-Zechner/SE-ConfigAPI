using System;
using Mz.SemanticVersioning;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Represents a provider instance withdrawing an exposed API.
    /// </summary>
    public sealed class ApiProviderWithdrawal
    {
        /// <summary>
        /// Gets the mod withdrawing the API.
        /// </summary>
        public ApiModIdentity Provider { get; }

        /// <summary>
        /// Gets the withdrawn API identifier.
        /// </summary>
        public string ApiId { get; }

        /// <summary>
        /// Gets the identity of the provider instance that stopped.
        /// </summary>
        public Guid ProviderInstanceId { get; }

        /// <summary>
        /// Gets the provider's wire-protocol version.
        /// </summary>
        public SemanticVersion WireProtocolVersion { get; }

        /// <summary>
        /// Gets the protocol-library version embedded by the provider.
        /// </summary>
        public SemanticVersion LibraryVersion { get; }

        /// <summary>
        /// Creates a withdrawal using the current library and wire versions.
        /// </summary>
        /// <param name="provider">The mod withdrawing the API.</param>
        /// <param name="apiId">
        /// The case-sensitive API identifier.
        /// </param>
        /// <param name="providerInstanceId">
        /// The non-empty provider-instance identity.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="provider"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the API or provider-instance identity is empty.
        /// </exception>
        public ApiProviderWithdrawal(ApiModIdentity provider, string apiId, Guid providerInstanceId)
            : this(provider, apiId, providerInstanceId, ApiProtocolInfo.WireProtocolVersion, ApiProtocolInfo.LibraryVersion) { }

        internal ApiProviderWithdrawal(ApiModIdentity provider, string apiId, Guid providerInstanceId,
            SemanticVersion wireProtocolVersion, SemanticVersion libraryVersion
        )
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            if (string.IsNullOrWhiteSpace(apiId))
                throw new ArgumentException("An API identifier is required.", nameof(apiId));

            if (providerInstanceId == Guid.Empty)
                throw new ArgumentException("A provider withdrawal requires a non-empty provider instance identifier.", nameof(providerInstanceId));

            if (wireProtocolVersion == null)
                throw new ArgumentNullException(nameof(wireProtocolVersion));

            if (libraryVersion == null)
                throw new ArgumentNullException(nameof(libraryVersion));

            Provider = provider;
            ApiId = apiId.Trim();
            ProviderInstanceId = providerInstanceId;
            WireProtocolVersion = wireProtocolVersion;
            LibraryVersion = libraryVersion;
        }
    }
}