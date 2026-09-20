using System;
using Mz.SemanticVersioning;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Provides information about an API provider observed during discovery.
    /// </summary>
    public sealed class ApiProviderObservedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the mod providing the observed API.
        /// </summary>
        public ApiModIdentity Provider { get; }

        /// <summary>
        /// Gets the API identity and version announced by the provider.
        /// </summary>
        public ApiDescriptor Descriptor { get; }

        /// <summary>
        /// Gets the compatibility result for the observed API version.
        /// </summary>
        public ApiCompatibilityStatus CompatibilityStatus { get; }

        /// <summary>
        /// Gets the provider's wire-protocol version.
        /// </summary>
        public SemanticVersion ProviderWireProtocolVersion { get; }

        /// <summary>
        /// Gets the provider's embedded protocol-library version.
        /// </summary>
        public SemanticVersion ProviderLibraryVersion { get; }

        /// <summary>
        /// Gets the correlation identifier from the announcement.
        /// </summary>
        public Guid CorrelationId { get; }

        /// <summary>
        /// Creates provider-observation event data.
        /// </summary>
        /// <param name="announcement">
        /// The observed provider announcement.
        /// </param>
        /// <param name="compatibilityStatus">
        /// The API compatibility result.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="announcement"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the compatibility status is undefined.
        /// </exception>
        public ApiProviderObservedEventArgs(ApiAnnouncement announcement, ApiCompatibilityStatus compatibilityStatus)
        {
            if (announcement == null)
                throw new ArgumentNullException(nameof(announcement));

            if (compatibilityStatus < ApiCompatibilityStatus.Compatible || compatibilityStatus > ApiCompatibilityStatus.ProviderTooNew)
                throw new ArgumentException("The value is outside the supported range.", nameof(compatibilityStatus));

            Provider = announcement.Provider;
            Descriptor = announcement.Descriptor;
            CompatibilityStatus = compatibilityStatus;
            ProviderWireProtocolVersion = announcement.WireProtocolVersion;
            ProviderLibraryVersion = announcement.LibraryVersion;
            CorrelationId = announcement.CorrelationId;
        }
    }
}