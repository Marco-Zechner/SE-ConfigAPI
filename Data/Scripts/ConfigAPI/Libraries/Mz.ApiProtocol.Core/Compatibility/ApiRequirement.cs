using System;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Describes the API identity and provider versions accepted by a consumer.
    /// </summary>
    public sealed class ApiRequirement
    {
        /// <summary>
        /// Gets the required, case-sensitive API identifier.
        /// </summary>
        public string ApiId { get; }

        /// <summary>
        /// Gets the supported provider version range.
        /// </summary>
        public ApiVersionRange SupportedVersions { get; }

        /// <summary>
        /// Creates an API requirement.
        /// </summary>
        public ApiRequirement(string apiId, ApiVersionRange supportedVersions)
        {
            if (string.IsNullOrWhiteSpace(apiId))
                throw new ArgumentException("An API identifier is required.", nameof(apiId));

            if (supportedVersions == null)
                throw new ArgumentNullException(nameof(supportedVersions));

            ApiId = apiId.Trim();
            SupportedVersions = supportedVersions;
        }

        /// <summary>
        /// Evaluates an API provider against this requirement.
        /// </summary>
        public ApiCompatibilityStatus Evaluate(ApiDescriptor provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            return string.Equals(ApiId, provider.ApiId, StringComparison.Ordinal) 
                ? SupportedVersions.Evaluate(provider.Version) 
                : ApiCompatibilityStatus.DifferentApi;
        }

        /// <summary>
        /// Determines whether an API provider satisfies this requirement.
        /// </summary>
        public bool IsSatisfiedBy(ApiDescriptor provider) => Evaluate(provider) == ApiCompatibilityStatus.Compatible;
    }
}