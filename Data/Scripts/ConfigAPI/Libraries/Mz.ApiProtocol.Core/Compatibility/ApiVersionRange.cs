using System;
using Mz.SemanticVersioning;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Describes the provider versions supported by an API consumer.
    /// </summary>
    public sealed class ApiVersionRange
    {
        /// <summary>
        /// Gets the lowest supported provider version.
        /// </summary>
        public SemanticVersion MinimumInclusive { get; }

        /// <summary>
        /// Gets the first unsupported provider version.
        /// Null means there is no upper bound.
        /// </summary>
        public SemanticVersion MaximumExclusive { get; }

        /// <summary>
        /// Creates a supported version range.
        /// </summary>
        public ApiVersionRange(SemanticVersion minimumInclusive, SemanticVersion maximumExclusive)
        {
            if (minimumInclusive == null)
                throw new ArgumentNullException(nameof(minimumInclusive));

            if (maximumExclusive != null && maximumExclusive <= minimumInclusive)
                throw new ArgumentException("The exclusive maximum must be greater than the inclusive minimum.", nameof(maximumExclusive));

            MinimumInclusive = minimumInclusive;
            MaximumExclusive = maximumExclusive;
        }

        /// <summary>
        /// Determines whether the supplied version is supported.
        /// </summary>
        public bool Contains(SemanticVersion version)
        {
            if (version == null)
                throw new ArgumentNullException(nameof(version));

            return Evaluate(version) == ApiCompatibilityStatus.Compatible;
        }

        /// <summary>
        /// Evaluates a provider version against this range.
        /// </summary>
        public ApiCompatibilityStatus Evaluate(SemanticVersion providerVersion)
        {
            if (providerVersion == null)
                throw new ArgumentNullException(nameof(providerVersion));

            if (providerVersion < MinimumInclusive)
                return ApiCompatibilityStatus.ProviderTooOld;

            if (MaximumExclusive != null && providerVersion >= MaximumExclusive)
                return ApiCompatibilityStatus.ProviderTooNew;

            return ApiCompatibilityStatus.Compatible;
        }

        /// <summary>
        /// Returns the range using mathematical interval notation.
        /// </summary>
        public override string ToString() =>
            MaximumExclusive == null 
                ? $"[{MinimumInclusive}, infinity)" 
                : $"[{MinimumInclusive}, {MaximumExclusive})";
    }
}