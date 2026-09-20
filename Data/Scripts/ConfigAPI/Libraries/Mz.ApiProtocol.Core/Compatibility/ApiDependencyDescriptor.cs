using System;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Describes one mod's dependency on an API.
    /// </summary>
    public sealed class ApiDependencyDescriptor
    {
        /// <summary>
        /// Gets the mod consuming the API.
        /// </summary>
        public ApiModIdentity Consumer { get; }

        /// <summary>
        /// Gets the required API identity and supported provider versions.
        /// </summary>
        public ApiRequirement Requirement { get; }

        /// <summary>
        /// Gets whether the dependency is required or optional.
        /// </summary>
        public ApiDependencyKind Kind { get; }

        /// <summary>
        /// Gets a human-readable description of the feature using the API.
        /// </summary>
        /// <remarks>
        /// An empty value means the consuming mod did not provide a
        /// feature-specific description.
        /// </remarks>
        public string FeatureDescription { get; }

        /// <summary>
        /// Creates an API dependency descriptor.
        /// </summary>
        /// <param name="consumer">
        /// The mod consuming the API.
        /// </param>
        /// <param name="requirement">
        /// The required API identity and supported provider versions.
        /// </param>
        /// <param name="kind">
        /// Whether the dependency is required or optional.
        /// </param>
        /// <param name="featureDescription">
        /// An optional human-readable description of the feature that uses
        /// the API.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="consumer"/> or
        /// <paramref name="requirement"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="kind"/> is not defined.
        /// </exception>
        public ApiDependencyDescriptor(ApiModIdentity consumer, ApiRequirement requirement, ApiDependencyKind kind, string featureDescription)
        {
            if (consumer == null)
                throw new ArgumentNullException(nameof(consumer));

            if (requirement == null)
                throw new ArgumentNullException(nameof(requirement));

            if (kind < ApiDependencyKind.Required || kind > ApiDependencyKind.Optional)
                throw new ArgumentException("The value is outside the supported range.", nameof(kind));

            Consumer = consumer;
            Requirement = requirement;
            Kind = kind;

            FeatureDescription = string.IsNullOrWhiteSpace(featureDescription)
                    ? string.Empty
                    : featureDescription.Trim();
        }

        /// <summary>
        /// Gets whether a compatible provider is mandatory.
        /// </summary>
        public bool IsRequired => Kind == ApiDependencyKind.Required;

        /// <summary>
        /// Gets whether the consuming mod may continue without the API.
        /// </summary>
        public bool IsOptional => Kind == ApiDependencyKind.Optional;
    }
}