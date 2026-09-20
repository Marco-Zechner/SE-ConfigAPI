using System;
using System.Collections.Generic;
using Mz.SemanticVersioning;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Encodes and decodes API discovery messages using runtime types shared
    /// safely between separately compiled mods.
    /// </summary>
    public static class ApiDiscoveryWireProtocol
    {
        /// <summary>
        /// Gets the discovery-request wire marker.
        /// </summary>
        public const string RequestMarker = "Mz.ApiProtocol.Discovery.Request";

        /// <summary>
        /// Gets the provider-announcement wire marker.
        /// </summary>
        public const string AnnouncementMarker = "Mz.ApiProtocol.Discovery.Announcement";

        /// <summary>
        /// Gets the provider-withdrawal wire marker.
        /// </summary>
        public const string WithdrawalMarker = "Mz.ApiProtocol.Discovery.Withdrawal";

        /// <summary>
        /// Creates a transport-safe discovery request.
        /// </summary>
        /// <param name="dependency">
        /// The consumer and its API dependency declaration.
        /// </param>
        /// <param name="correlationId">
        /// The non-empty request correlation identifier.
        /// </param>
        /// <returns>The transport-safe request payload.</returns>
        public static object CreateRequest(ApiDependencyDescriptor dependency, Guid correlationId)
        {
            var request = new ApiDiscoveryRequest(dependency, correlationId);

            var versions = request.Dependency.Requirement.SupportedVersions;

            return new object[] {
                RequestMarker,
                request.WireProtocolVersion.ToString(),
                request.LibraryVersion.ToString(),
                request.Dependency.Consumer.Id,
                request.Dependency.Consumer.DisplayName,
                request.Dependency.Consumer.Version.ToString(),
                request.Dependency.Requirement.ApiId,
                versions.MinimumInclusive.ToString(),
                versions.MaximumExclusive == null
                    ? null
                    : versions.MaximumExclusive.ToString(),
                (int)request.Dependency.Kind,
                request.Dependency.FeatureDescription,
                request.CorrelationId
            };
        }

        /// <summary>
        /// Attempts to parse the stable diagnostic envelope shared by every
        /// discovery message.
        /// </summary>
        /// <param name="payload">The received transport payload.</param>
        /// <param name="envelope">
        /// Receives the parsed envelope when decoding succeeds.
        /// </param>
        /// <returns>
        /// True when the stable envelope is valid; otherwise false.
        /// </returns>
        public static bool TryParseEnvelope(object payload, out ApiWireEnvelope envelope)
        {
            envelope = null;

            var fields = payload as object[];

            if (fields == null || fields.Length < 7)
                return false;

            ApiWireMessageKind messageKind;

            if (!TryParseMessageKind(fields[0] as string, out messageKind))
                return false;

            SemanticVersion wireVersion;
            SemanticVersion libraryVersion;

            if (!TryParseVersions(fields[1], fields[2], out wireVersion, out libraryVersion))
                return false;

            ApiModIdentity participant;

            if (!TryParseModIdentity(fields[3], fields[4], fields[5], out participant))
                return false;

            var apiId = fields[6] as string;

            if (string.IsNullOrWhiteSpace(apiId))
                return false;

            try
            {
                envelope = new ApiWireEnvelope(messageKind, participant, wireVersion, libraryVersion, apiId);
                return true;
            }
            catch (ArgumentException)
            {
                envelope = null;
                return false;
            }
        }
        
        /// <summary>
        /// Attempts to decode a discovery request.
        /// </summary>
        /// <param name="payload">The received transport payload.</param>
        /// <param name="request">
        /// Receives the parsed request when decoding succeeds.
        /// </param>
        /// <returns>True when decoding succeeds; otherwise false.</returns>
        public static bool TryParseRequest(object payload, out ApiDiscoveryRequest request)
        {
            request = null;

            var fields = payload as object[];

            if (fields == null || fields.Length < 12)
                return false;

            if (!HasMarker(fields, RequestMarker))
                return false;

            SemanticVersion wireVersion;
            SemanticVersion libraryVersion;
            ApiModIdentity consumer;
            ApiVersionRange supportedVersions;

            if (!TryParseVersions(fields[1], fields[2], out wireVersion, out libraryVersion))
                return false;

            if (!TryParseModIdentity(fields[3], fields[4], fields[5], out consumer))
                return false;

            var apiId = fields[6] as string;

            if (string.IsNullOrWhiteSpace(apiId))
                return false;

            if (!TryParseVersionRange(fields[7], fields[8], out supportedVersions))
                return false;

            if (!(fields[9] is int))
                return false;

            var kind = (ApiDependencyKind)(int)fields[9];
            var featureDescription = fields[10] as string;

            if (featureDescription == null)
                return false;

            if (!(fields[11] is Guid))
                return false;

            var correlationId = (Guid)fields[11];

            if (correlationId == Guid.Empty)
                return false;

            try
            {
                var requirement = new ApiRequirement(apiId, supportedVersions);
                var dependency = new ApiDependencyDescriptor(consumer, requirement, kind, featureDescription);
                request = new ApiDiscoveryRequest(dependency, correlationId, wireVersion, libraryVersion);
                return true;
            }
            catch (ArgumentException)
            {
                request = null;
                return false;
            }
        }

        /// <summary>
        /// Creates a transport-safe provider announcement.
        /// </summary>
        /// <param name="provider">The mod providing the API.</param>
        /// <param name="descriptor">
        /// The provided API identity and version.
        /// </param>
        /// <param name="providerInstanceId">
        /// The non-empty provider-instance identity.
        /// </param>
        /// <param name="correlationId">
        /// The request correlation identifier, or <see cref="Guid.Empty"/>
        /// for an unsolicited announcement.
        /// </param>
        /// <param name="endpoints">
        /// The provider endpoint delegates.
        /// </param>
        /// <returns>The transport-safe announcement payload.</returns>
        public static object CreateAnnouncement(ApiModIdentity provider, ApiDescriptor descriptor, 
            Guid providerInstanceId, Guid correlationId, IDictionary<string, Delegate> endpoints
        )
        {
            var announcement = new ApiAnnouncement(provider, descriptor, providerInstanceId, correlationId, endpoints);

            return new object[] {
                AnnouncementMarker,
                announcement.WireProtocolVersion.ToString(),
                announcement.LibraryVersion.ToString(),
                announcement.Provider.Id,
                announcement.Provider.DisplayName,
                announcement.Provider.Version.ToString(),
                announcement.Descriptor.ApiId,
                announcement.Descriptor.Version.ToString(),
                announcement.ProviderInstanceId,
                announcement.CorrelationId,
                CopyEndpointPayload(announcement.Endpoints)
            };
        }

        /// <summary>
        /// Attempts to decode a provider announcement.
        /// </summary>
        /// <param name="payload">The received transport payload.</param>
        /// <param name="announcement">
        /// Receives the parsed announcement when decoding succeeds.
        /// </param>
        /// <returns>True when decoding succeeds; otherwise false.</returns>
        public static bool TryParseAnnouncement(object payload, out ApiAnnouncement announcement)
        {
            announcement = null;

            var fields = payload as object[];

            if (fields == null || fields.Length < 11)
                return false;

            if (!HasMarker(fields, AnnouncementMarker))
                return false;

            SemanticVersion wireVersion;
            SemanticVersion libraryVersion;
            ApiModIdentity provider;

            if (!TryParseVersions(fields[1], fields[2], out wireVersion, out libraryVersion))
                return false;

            if (!TryParseModIdentity(fields[3], fields[4], fields[5], out provider))
                return false;

            var apiId = fields[6] as string;
            var apiVersionText = fields[7] as string;

            if (string.IsNullOrWhiteSpace(apiId))
                return false;

            SemanticVersion apiVersion;

            if (!SemanticVersion.TryParse(apiVersionText, out apiVersion))
                return false;

            if (!(fields[8] is Guid))
                return false;

            var providerInstanceId = (Guid)fields[8];

            if (providerInstanceId == Guid.Empty)
                return false;

            if (!(fields[9] is Guid))
                return false;

            var endpoints = fields[10] as IDictionary<string, Delegate>;

            if (endpoints == null)
                return false;

            try
            {
                var descriptor = new ApiDescriptor(apiId, apiVersion);
                announcement = new ApiAnnouncement(provider, descriptor, providerInstanceId, (Guid)fields[9], wireVersion, libraryVersion, endpoints);
                return true;
            }
            catch (ArgumentException)
            {
                announcement = null;
                return false;
            }
        }

        /// <summary>
        /// Creates a transport-safe provider withdrawal.
        /// </summary>
        /// <param name="provider">The mod withdrawing the API.</param>
        /// <param name="apiId">The withdrawn API identifier.</param>
        /// <param name="providerInstanceId">
        /// The non-empty provider-instance identity.
        /// </param>
        /// <returns>The transport-safe withdrawal payload.</returns>
        public static object CreateWithdrawal(ApiModIdentity provider, string apiId, Guid providerInstanceId)
        {
            var withdrawal = new ApiProviderWithdrawal(provider, apiId, providerInstanceId);

            return new object[] {
                WithdrawalMarker,
                withdrawal.WireProtocolVersion.ToString(),
                withdrawal.LibraryVersion.ToString(),
                withdrawal.Provider.Id,
                withdrawal.Provider.DisplayName,
                withdrawal.Provider.Version.ToString(),
                withdrawal.ApiId,
                withdrawal.ProviderInstanceId
            };
        }

        /// <summary>
        /// Attempts to decode a provider withdrawal.
        /// </summary>
        /// <param name="payload">The received transport payload.</param>
        /// <param name="withdrawal">
        /// Receives the parsed withdrawal when decoding succeeds.
        /// </param>
        /// <returns>True when decoding succeeds; otherwise false.</returns>
        public static bool TryParseWithdrawal(object payload, out ApiProviderWithdrawal withdrawal)
        {
            withdrawal = null;

            var fields = payload as object[];

            if (fields == null || fields.Length < 8)
                return false;

            if (!HasMarker(fields, WithdrawalMarker))
                return false;

            SemanticVersion wireVersion;
            SemanticVersion libraryVersion;
            ApiModIdentity provider;

            if (!TryParseVersions(fields[1], fields[2], out wireVersion, out libraryVersion))
                return false;

            if (!TryParseModIdentity(fields[3], fields[4], fields[5], out provider))
                return false;

            var apiId = fields[6] as string;

            if (string.IsNullOrWhiteSpace(apiId))
                return false;

            if (!(fields[7] is Guid))
                return false;

            var providerInstanceId = (Guid)fields[7];

            if (providerInstanceId == Guid.Empty)
                return false;

            try
            {
                withdrawal = new ApiProviderWithdrawal(provider, apiId, providerInstanceId, wireVersion, libraryVersion);
                return true;
            }
            catch (ArgumentException)
            {
                withdrawal = null;
                return false;
            }
        }

        private static bool HasMarker(object[] fields, string expectedMarker)
        {
            return fields.Length > 0 
                   && string.Equals(fields[0] as string, expectedMarker, StringComparison.Ordinal);
        }
        
        private static bool TryParseMessageKind(string marker, out ApiWireMessageKind messageKind)
        {
            if (string.Equals(marker, RequestMarker, StringComparison.Ordinal))
            {
                messageKind = ApiWireMessageKind.Request;
                return true;
            }

            if (string.Equals(marker, AnnouncementMarker, StringComparison.Ordinal))
            {
                messageKind = ApiWireMessageKind.Announcement;
                return true;
            }

            if (string.Equals(marker, WithdrawalMarker, StringComparison.Ordinal))
            {
                messageKind = ApiWireMessageKind.Withdrawal;
                return true;
            }

            messageKind = default(ApiWireMessageKind);
            return false;
        }

        private static bool TryParseVersions(object wireVersionField, object libraryVersionField, 
            out SemanticVersion wireVersion, out SemanticVersion libraryVersion
        )
        {
            wireVersion = null;
            libraryVersion = null;

            return SemanticVersion.TryParse(wireVersionField as string, out wireVersion)
                && SemanticVersion.TryParse(libraryVersionField as string, out libraryVersion);
        }

        private static bool TryParseModIdentity(object idField, object displayNameField, object versionField, out ApiModIdentity identity)
        {
            identity = null;

            var id = idField as string;
            var displayName = displayNameField as string;
            var versionText = versionField as string;

            SemanticVersion version;

            if (!SemanticVersion.TryParse(versionText, out version))
                return false;

            try
            {
                identity = new ApiModIdentity(id, displayName, version);
                return true;
            }
            catch (ArgumentException)
            {
                identity = null;
                return false;
            }
        }

        private static bool TryParseVersionRange(object minimumField, object maximumField, out ApiVersionRange range)
        {
            range = null;

            SemanticVersion minimum;

            if (!SemanticVersion.TryParse(minimumField as string, out minimum))
                return false;

            SemanticVersion maximum = null;

            if (maximumField != null)
            {
                var maximumText = maximumField as string;

                if (maximumText == null)
                    return false;

                if (!SemanticVersion.TryParse(maximumText, out maximum))
                    return false;
            }

            try
            {
                range = new ApiVersionRange(minimum, maximum);
                return true;
            }
            catch (ArgumentException)
            {
                range = null;
                return false;
            }
        }

        private static Dictionary<string, Delegate> CopyEndpointPayload(IReadOnlyDictionary<string, Delegate> endpoints)
        {
            var copy = new Dictionary<string, Delegate>(StringComparer.Ordinal);

            foreach (var pair in endpoints)
                copy.Add(pair.Key, pair.Value);

            return copy;
        }
    }
}