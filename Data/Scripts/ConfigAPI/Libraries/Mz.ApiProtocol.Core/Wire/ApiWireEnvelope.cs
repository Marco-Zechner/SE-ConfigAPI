using System;
using Mz.SemanticVersioning;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Contains the stable diagnostic header shared by all API discovery
    /// wire messages.
    /// </summary>
    /// <remarks>
    /// Future wire-protocol versions must preserve this envelope even when
    /// the message-specific body becomes incompatible. This allows older
    /// implementations to identify the remote mod and report the conflict.
    /// </remarks>
    public sealed class ApiWireEnvelope
    {
        /// <summary>
        /// Gets the kind of wire message.
        /// </summary>
        public ApiWireMessageKind MessageKind { get; }

        /// <summary>
        /// Gets the mod that sent the message.
        /// </summary>
        public ApiModIdentity Participant { get; }

        /// <summary>
        /// Gets the remote wire-protocol version.
        /// </summary>
        public SemanticVersion WireProtocolVersion { get; }

        /// <summary>
        /// Gets the protocol-library version embedded by the remote mod.
        /// </summary>
        public SemanticVersion LibraryVersion { get; }

        /// <summary>
        /// Gets the API identifier associated with the message.
        /// </summary>
        public string ApiId { get; }

        /// <summary>
        /// Creates a wire-message envelope.
        /// </summary>
        /// <param name="messageKind">
        /// The kind of wire message.
        /// </param>
        /// <param name="participant">
        /// The mod that sent the message.
        /// </param>
        /// <param name="wireProtocolVersion">
        /// The remote wire-protocol version.
        /// </param>
        /// <param name="libraryVersion">
        /// The protocol-library version embedded by the remote mod.
        /// </param>
        /// <param name="apiId">
        /// The API identifier associated with the message.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="messageKind"/> is undefined.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a required object is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="apiId"/> is empty.
        /// </exception>
        public ApiWireEnvelope(
            ApiWireMessageKind messageKind, ApiModIdentity participant, 
            SemanticVersion wireProtocolVersion, SemanticVersion libraryVersion, string apiId
        )
        {
            if (messageKind < ApiWireMessageKind.Request || messageKind > ApiWireMessageKind.Withdrawal)
                throw new ArgumentException("The value is outside the supported range.", nameof(messageKind));

            if (participant == null)
                throw new ArgumentNullException(nameof(participant));

            if (wireProtocolVersion == null)
                throw new ArgumentNullException(nameof(wireProtocolVersion));

            if (libraryVersion == null)
                throw new ArgumentNullException(nameof(libraryVersion));

            if (string.IsNullOrWhiteSpace(apiId))
                throw new ArgumentException("An API identifier is required.", nameof(apiId));

            MessageKind = messageKind;
            Participant = participant;
            WireProtocolVersion = wireProtocolVersion;
            LibraryVersion = libraryVersion;
            ApiId = apiId.Trim();
        }
    }
}