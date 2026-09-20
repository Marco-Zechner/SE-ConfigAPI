namespace Mz.ApiProtocol
{
    /// <summary>
    /// Describes whether a remote wire-protocol version can communicate with
    /// the current protocol implementation.
    /// </summary>
    public enum ApiWireCompatibilityStatus
    {
        /// <summary>
        /// The remote wire-protocol version is compatible.
        /// </summary>
        Compatible = 0,

        /// <summary>
        /// The remote wire-protocol major version is older than supported.
        /// </summary>
        RemoteTooOld = 1,

        /// <summary>
        /// The remote wire-protocol major version is newer than supported.
        /// </summary>
        RemoteTooNew = 2
    }
}