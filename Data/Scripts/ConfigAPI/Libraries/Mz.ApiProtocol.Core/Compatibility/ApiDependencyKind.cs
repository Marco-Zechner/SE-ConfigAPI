namespace Mz.ApiProtocol
{
    /// <summary>
    /// Describes how strongly a consuming mod depends on an API.
    /// </summary>
    public enum ApiDependencyKind
    {
        /// <summary>
        /// The consuming mod cannot provide its intended functionality
        /// without a compatible provider.
        /// </summary>
        Required = 0,

        /// <summary>
        /// The consuming mod remains functional without a compatible
        /// provider, but an integration or feature is unavailable.
        /// </summary>
        Optional = 1
    }
}