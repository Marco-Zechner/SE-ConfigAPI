namespace Mz.ApiProtocol
{
    /// <summary>
    /// Describes whether an API provider satisfies a consumer requirement.
    /// </summary>
    public enum ApiCompatibilityStatus
    {
        /// <summary>
        /// The provider matches the API identity and supported version range.
        /// </summary>
        Compatible = 0,

        /// <summary>
        /// The provider exposes a different API identity.
        /// </summary>
        DifferentApi = 1,

        /// <summary>
        /// The provider version is lower than the minimum supported version.
        /// </summary>
        ProviderTooOld = 2,

        /// <summary>
        /// The provider version is at or above the exclusive upper bound.
        /// </summary>
        ProviderTooNew = 3
    }
}