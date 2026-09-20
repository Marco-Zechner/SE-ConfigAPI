namespace Mz.ApiProtocol
{
    /// <summary>
    /// Identifies a message in the API discovery wire protocol.
    /// </summary>
    public enum ApiWireMessageKind
    {
        /// <summary>
        /// A consumer is requesting an API provider.
        /// </summary>
        Request = 0,

        /// <summary>
        /// A provider is announcing an available API.
        /// </summary>
        Announcement = 1,

        /// <summary>
        /// A provider is withdrawing an API instance.
        /// </summary>
        Withdrawal = 2
    }
}