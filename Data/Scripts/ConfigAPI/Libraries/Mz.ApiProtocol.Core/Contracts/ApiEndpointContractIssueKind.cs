namespace Mz.ApiProtocol
{
    /// <summary>
    /// Identifies why an endpoint does not satisfy a contract.
    /// </summary>
    public enum ApiEndpointContractIssueKind
    {
        /// <summary>
        /// The required endpoint was not exposed.
        /// </summary>
        MissingEndpoint = 0,

        /// <summary>
        /// The endpoint exists but uses the wrong delegate type.
        /// </summary>
        WrongDelegateType = 1
    }
}