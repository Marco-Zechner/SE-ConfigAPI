namespace Mz.Networking
{
    /// <summary>
    /// Selects the delivery guarantees used by a concrete network transport.
    /// </summary>
    public enum NetworkDeliveryMode
    {
        /// <summary>
        /// Requests ordered, guaranteed delivery.
        /// </summary>
        Reliable = 0,

        /// <summary>
        /// Prefers low latency and allows packets to be lost or reordered.
        /// </summary>
        Unreliable = 1
    }
}
