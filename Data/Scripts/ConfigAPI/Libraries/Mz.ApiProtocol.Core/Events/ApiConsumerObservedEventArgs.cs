using System;
using Mz.SemanticVersioning;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Provides information about a consumer that requested an API.
    /// </summary>
    public sealed class ApiConsumerObservedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the parsed discovery request.
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public ApiDiscoveryRequest Request { get; }

        /// <summary>
        /// Gets the mod requesting the API.
        /// </summary>
        public ApiModIdentity Consumer => Request.Dependency.Consumer;

        /// <summary>
        /// Gets the consumer's API dependency declaration.
        /// </summary>
        public ApiDependencyDescriptor Dependency => Request.Dependency;

        /// <summary>
        /// Gets the compatibility result between the provider API version and
        /// the consumer's supported version range.
        /// </summary>
        public ApiCompatibilityStatus CompatibilityStatus { get; }

        /// <summary>
        /// Gets the wire-protocol version used by the consumer.
        /// </summary>
        public SemanticVersion ConsumerWireProtocolVersion => Request.WireProtocolVersion;

        /// <summary>
        /// Gets the protocol-library version embedded by the consumer.
        /// </summary>
        public SemanticVersion ConsumerLibraryVersion => Request.LibraryVersion;

        /// <summary>
        /// Gets the discovery request correlation identifier.
        /// </summary>
        public Guid CorrelationId => Request.CorrelationId;

        /// <summary>
        /// Creates consumer-observation event data.
        /// </summary>
        /// <param name="request">
        /// The parsed discovery request.
        /// </param>
        /// <param name="compatibilityStatus">
        /// The compatibility result between the provider and consumer.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="request"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="compatibilityStatus"/> is undefined.
        /// </exception>
        public ApiConsumerObservedEventArgs(ApiDiscoveryRequest request, ApiCompatibilityStatus compatibilityStatus)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (compatibilityStatus < ApiCompatibilityStatus.Compatible || compatibilityStatus > ApiCompatibilityStatus.ProviderTooNew)
                throw new ArgumentException("The value is outside the supported range.", nameof(compatibilityStatus));

            Request = request;
            CompatibilityStatus = compatibilityStatus;
        }
    }
}