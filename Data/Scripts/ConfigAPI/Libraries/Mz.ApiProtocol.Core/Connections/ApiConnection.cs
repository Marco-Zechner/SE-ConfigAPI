using System;
using System.Collections.Generic;
using Mz.SemanticVersioning;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Represents an accepted connection to a compatible API provider.
    /// </summary>
    public sealed class ApiConnection
    {
        /// <summary>
        /// Gets the mod providing the API.
        /// </summary>
        public ApiModIdentity Provider { get; }

        /// <summary>
        /// Gets the identity and API version exposed by the provider.
        /// </summary>
        public ApiDescriptor Descriptor { get; }

        /// <summary>
        /// Gets the identity of the connected provider instance.
        /// </summary>
        public Guid ProviderInstanceId { get; }

        /// <summary>
        /// Gets the provider's wire-protocol version.
        /// </summary>
        public SemanticVersion ProviderWireProtocolVersion { get; }

        /// <summary>
        /// Gets the protocol-library version embedded by the provider.
        /// </summary>
        public SemanticVersion ProviderLibraryVersion { get; }

        /// <summary>
        /// Gets the endpoint delegates exposed by the provider.
        /// </summary>
        public IReadOnlyDictionary<string, Delegate> Endpoints { get; }

        /// <summary>
        /// Creates a connection from a validated provider announcement.
        /// </summary>
        /// <param name="announcement">
        /// The accepted provider announcement.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="announcement"/> is null.
        /// </exception>
        public ApiConnection(ApiAnnouncement announcement)
        {
            if (announcement == null)
                throw new ArgumentNullException(nameof(announcement));

            Provider = announcement.Provider;
            Descriptor = announcement.Descriptor;
            ProviderInstanceId = announcement.ProviderInstanceId;
            ProviderWireProtocolVersion = announcement.WireProtocolVersion;
            ProviderLibraryVersion = announcement.LibraryVersion;

            var copy = new Dictionary<string, Delegate>(StringComparer.Ordinal);

            foreach (var pair in announcement.Endpoints)
                copy.Add(pair.Key, pair.Value);

            Endpoints = new ReadOnlyDictionaryView<string, Delegate>(copy);
        }

        /// <summary>
        /// Attempts to retrieve an endpoint using its expected delegate type.
        /// </summary>
        /// <typeparam name="TDelegate">
        /// The expected delegate type.
        /// </typeparam>
        /// <param name="endpointName">
        /// The case-sensitive endpoint name.
        /// </param>
        /// <param name="endpoint">
        /// Receives the endpoint when it exists with the expected type.
        /// </param>
        /// <returns>
        /// True when the endpoint exists with the expected type; otherwise
        /// false.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the endpoint name is empty.
        /// </exception>
        public bool TryGetEndpoint<TDelegate>(string endpointName, out TDelegate endpoint) where TDelegate : class
        {
            if (string.IsNullOrWhiteSpace(endpointName))
                throw new ArgumentException("An endpoint name is required.", nameof(endpointName));

            Delegate value;
            if (!Endpoints.TryGetValue(endpointName.Trim(), out value))
            {
                endpoint = null;
                return false;
            }

            endpoint = value as TDelegate;
            return endpoint != null;
        }
    }
}