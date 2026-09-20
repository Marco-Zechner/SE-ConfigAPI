using System;

namespace Mz.Networking
{
    /// <summary>
    /// Contains a transport-independent network message and its routing
    /// metadata.
    /// </summary>
    public sealed class NetworkEnvelope
    {
        private readonly byte[] _payload;

        /// <summary>
        /// Gets the application-defined message type.
        /// </summary>
        public string MessageType { get; }

        /// <summary>
        /// Gets the identity of the client that originally sent the message.
        /// </summary>
        public ulong OriginalSenderId { get; }

        /// <summary>
        /// Gets whether the message has already been relayed by a server.
        /// </summary>
        public bool IsRelay { get; }

        /// <summary>
        /// Gets a copy of the application-defined message payload.
        /// </summary>
        public byte[] Payload => CopyPayload(_payload);

        /// <summary>
        /// Creates a network-message envelope.
        /// </summary>
        /// <param name="messageType">
        /// The application-defined message type.
        /// </param>
        /// <param name="originalSenderId">
        /// The identity claimed by the original sender.
        /// </param>
        /// <param name="isRelay">
        /// Whether the message has already been relayed by a server.
        /// </param>
        /// <param name="payload">
        /// The application-defined payload.
        /// </param>
        public NetworkEnvelope(string messageType, ulong originalSenderId, bool isRelay, byte[] payload)
        {
            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentException("A message type is required.", nameof(messageType));

            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            MessageType = messageType.Trim();
            OriginalSenderId = originalSenderId;
            IsRelay = isRelay;
            _payload = CopyPayload(payload);
        }

        internal NetworkEnvelope WithOriginalSenderId(ulong originalSenderId) 
            => new NetworkEnvelope(MessageType, originalSenderId, IsRelay, _payload);

        internal NetworkEnvelope WithRelay(bool isRelay) 
            => new NetworkEnvelope(MessageType, OriginalSenderId, isRelay, _payload);

        private static byte[] CopyPayload(byte[] payload)
        {
            var copy = new byte[payload.Length];
            Array.Copy(payload, copy, payload.Length);
            return copy;
        }
    }
}
