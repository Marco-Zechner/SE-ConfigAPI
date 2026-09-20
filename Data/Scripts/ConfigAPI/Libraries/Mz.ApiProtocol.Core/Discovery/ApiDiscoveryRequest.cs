using System;
using Mz.SemanticVersioning;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Represents a parsed request for an API provider announcement.
    /// </summary>
    public sealed class ApiDiscoveryRequest
    {
        /// <summary>
        /// Gets the consuming mod and its API dependency declaration.
        /// </summary>
        public ApiDependencyDescriptor Dependency { get; }

        /// <summary>
        /// Gets the requested API identifier.
        /// </summary>
        public string ApiId => Dependency.Requirement.ApiId;

        /// <summary>
        /// Gets the request correlation identifier.
        /// </summary>
        public Guid CorrelationId { get; }

        /// <summary>
        /// Gets the wire-protocol version used by the consumer.
        /// </summary>
        public SemanticVersion WireProtocolVersion { get; }

        /// <summary>
        /// Gets the protocol-library version embedded by the consumer.
        /// </summary>
        public SemanticVersion LibraryVersion { get; }

        /// <summary>
        /// Creates a request using the current library and wire versions.
        /// </summary>
        /// <param name="dependency">
        /// The consuming mod and its API dependency declaration.
        /// </param>
        /// <param name="correlationId">
        /// The non-empty request correlation identifier.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="dependency"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="correlationId"/> is empty.
        /// </exception>
        public ApiDiscoveryRequest(ApiDependencyDescriptor dependency, Guid correlationId) 
            : this(dependency, correlationId, ApiProtocolInfo.WireProtocolVersion, ApiProtocolInfo.LibraryVersion) { }

        internal ApiDiscoveryRequest(ApiDependencyDescriptor dependency, Guid correlationId, 
            SemanticVersion wireProtocolVersion, SemanticVersion libraryVersion
        )
        {
            if (dependency == null)
                throw new ArgumentNullException(nameof(dependency));

            if (correlationId == Guid.Empty)
                throw new ArgumentException("A discovery request requires a non-empty correlation identifier.", nameof(correlationId));

            if (wireProtocolVersion == null)
                throw new ArgumentNullException(nameof(wireProtocolVersion));

            if (libraryVersion == null)
                throw new ArgumentNullException(nameof(libraryVersion));

            Dependency = dependency;
            CorrelationId = correlationId;
            WireProtocolVersion = wireProtocolVersion;
            LibraryVersion = libraryVersion;
        }
    }
}