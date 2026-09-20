namespace Mz.Networking.SpaceEngineers
{
    /// <summary>
    /// Identifies the stage and conflict meaning of a failed received packet.
    /// </summary>
    public enum SpaceEngineersNetworkReceiveFailureKind
    {
        /// <summary>
        /// The packet does not use the Mz.Networking wire format.
        /// </summary>
        ForeignPacket = 0,

        /// <summary>
        /// The packet is valid Mz.Networking traffic for another network ID.
        /// </summary>
        NetworkMismatch = 1,

        /// <summary>
        /// The packet uses an unsupported Mz.Networking wire version.
        /// </summary>
        UnsupportedWireVersion = 2,

        /// <summary>
        /// The Mz.Networking wire header or framing is malformed.
        /// </summary>
        MalformedWirePacket = 3,

        /// <summary>
        /// The packet matched this network but its serialized envelope was malformed.
        /// </summary>
        MalformedOwnPacket = 4,

        /// <summary>
        /// Trusted receive validation, dispatch, or relay processing failed.
        /// </summary>
        ProcessingFailure = 5,

        /// <summary>
        /// The registered application message handler threw an exception.
        /// </summary>
        HandlerFailure = 6
    }
}
