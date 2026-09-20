using System;

namespace Mz.Networking.SpaceEngineers
{
    /// <summary>
    /// Describes a secure network message that could not be decoded or processed.
    /// </summary>
    public sealed class SpaceEngineersNetworkReceiveFailure
    {
        private readonly byte[] _serializedMessage;
        private readonly string[] _discoveredText;

        /// <summary>
        /// Gets the secure-message channel that received the packet.
        /// </summary>
        public ushort ChannelId { get; }

        /// <summary>
        /// Gets the immediate transport sender identity.
        /// </summary>
        public ulong SenderPeerId { get; }

        /// <summary>
        /// Gets whether the transport identified the sender as the server.
        /// </summary>
        public bool SenderIsServer { get; }

        /// <summary>
        /// Gets the failure classification.
        /// </summary>
        public SpaceEngineersNetworkReceiveFailureKind Kind { get; }

        /// <summary>
        /// Gets whether the packet is evidence that another protocol or network
        /// identity is using the configured channel.
        /// </summary>
        public bool IsChannelConflict 
            => Kind == SpaceEngineersNetworkReceiveFailureKind.ForeignPacket
            || Kind == SpaceEngineersNetworkReceiveFailureKind.NetworkMismatch;

        /// <summary>
        /// Gets the network identity expected by the receiving session.
        /// </summary>
        public string ExpectedNetworkId { get; }

        /// <summary>
        /// Gets the network identity observed in the packet when available.
        /// </summary>
        public string ObservedNetworkId { get; }

        /// <summary>
        /// Gets the recommended diagnostic severity. Its names and values map
        /// directly to Mz.Logging.LogLevel without creating a package dependency.
        /// </summary>
        public SpaceEngineersNetworkDiagnosticSeverity Severity { get; }

        /// <summary>
        /// Gets the stable machine-readable diagnostic code.
        /// </summary>
        public string DiagnosticCode { get; }

        /// <summary>
        /// Gets a deterministic bounded message suitable for a text logger.
        /// </summary>
        public string DiagnosticMessage { get; }

        /// <summary>
        /// Gets the complete packet length in bytes.
        /// </summary>
        public int PacketLength => _serializedMessage.Length;

        /// <summary>
        /// Gets a bounded hexadecimal prefix of the received packet.
        /// </summary>
        public string PacketPreview { get; }

        /// <summary>
        /// Gets copies of bounded, sanitized text candidates discovered in a
        /// conflicting packet.
        /// </summary>
        public string[] DiscoveredText => Copy(_discoveredText);

        /// <summary>
        /// Gets the exception raised while processing the packet.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Gets a copy of the complete received serialized packet.
        /// </summary>
        public byte[] SerializedMessage => Copy(_serializedMessage);

        internal SpaceEngineersNetworkReceiveFailure(
            ushort channelId, byte[] serializedMessage, ulong senderPeerId, bool senderIsServer, SpaceEngineersNetworkReceiveFailureKind kind,
            string expectedNetworkId, string observedNetworkId, Exception exception, SpaceEngineersNetworkDiagnosticData diagnostic)
        {
            if (serializedMessage == null)
                throw new ArgumentNullException(nameof(serializedMessage));

            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            if (diagnostic == null)
                throw new ArgumentNullException(nameof(diagnostic));

            ChannelId = channelId;
            SenderPeerId = senderPeerId;
            SenderIsServer = senderIsServer;
            Kind = kind;
            ExpectedNetworkId = expectedNetworkId;
            ObservedNetworkId = observedNetworkId;
            Exception = exception;
            Severity = diagnostic.Severity;
            DiagnosticCode = diagnostic.Code;
            DiagnosticMessage = diagnostic.Message;
            PacketPreview = diagnostic.PacketPreview;
            _serializedMessage = Copy(serializedMessage);
            _discoveredText = Copy(diagnostic.DiscoveredText);
        }

        private static byte[] Copy(byte[] source)
        {
            var copy = new byte[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static string[] Copy(string[] source)
        {
            var copy = new string[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }
}