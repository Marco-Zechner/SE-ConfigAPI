using System;

namespace Mz.Networking
{
    /// <summary>
    /// Contains trusted receive metadata and mutable routing decisions for one
    /// processed network message.
    /// </summary>
    public sealed class NetworkReceiveContext
    {
        /// <summary>
        /// Gets the validated message envelope.
        /// </summary>
        public NetworkEnvelope Envelope { get; }

        /// <summary>
        /// Gets the immediate transport sender identity.
        /// </summary>
        public ulong TransportSenderId { get; }

        /// <summary>
        /// Gets whether the local endpoint is the authoritative server.
        /// </summary>
        public bool IsServer { get; }

        /// <summary>
        /// Gets whether the immediate transport sender is the authoritative
        /// server.
        /// </summary>
        public bool TransportSenderIsServer { get; }

        /// <summary>
        /// Gets whether the claimed original sender was replaced with the
        /// trusted transport sender.
        /// </summary>
        public bool OriginalSenderWasCorrected { get; }

        /// <summary>
        /// Gets whether a client-forged relay flag was removed.
        /// </summary>
        public bool RelayFlagWasCorrected { get; }

        /// <summary>
        /// Gets or sets how the server should relay the processed message.
        /// </summary>
        public NetworkRelayMode RelayMode { get; set; }

        /// <summary>
        /// Gets or sets the delivery guarantees used for a server relay.
        /// </summary>
        public NetworkDeliveryMode RelayDeliveryMode { get; set; }

        /// <summary>
        /// Gets or sets whether the envelope must be serialized again before
        /// it is sent onward.
        /// </summary>
        public bool RequiresSerialization { get; set; }

        internal NetworkReceiveContext(
            NetworkEnvelope envelope, ulong transportSenderId, bool isServer, bool transportSenderIsServer, 
            bool originalSenderWasCorrected, bool relayFlagWasCorrected)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            Envelope = envelope;
            TransportSenderId = transportSenderId;
            IsServer = isServer;
            TransportSenderIsServer = transportSenderIsServer;
            OriginalSenderWasCorrected = originalSenderWasCorrected;

            RelayFlagWasCorrected = relayFlagWasCorrected;

            RelayMode = NetworkRelayMode.None;
            RelayDeliveryMode = NetworkDeliveryMode.Reliable;
            RequiresSerialization = originalSenderWasCorrected || relayFlagWasCorrected;
        }
    }
}
