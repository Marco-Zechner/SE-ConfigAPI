using System;
using Mz.SemanticVersioning;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Provides information about a remote mod using an incompatible API
    /// discovery wire protocol.
    /// </summary>
    public sealed class ApiWireIncompatibilityEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the remote mod that sent the incompatible message.
        /// </summary>
        public ApiModIdentity RemoteMod => Envelope.Participant;

        /// <summary>
        /// Gets the API identifier associated with the message.
        /// </summary>
        public string ApiId => Envelope.ApiId;

        /// <summary>
        /// Gets the kind of incompatible message that was received.
        /// </summary>
        public ApiWireMessageKind MessageKind => Envelope.MessageKind;

        /// <summary>
        /// Gets the remote wire-protocol version.
        /// </summary>
        public SemanticVersion RemoteWireProtocolVersion => Envelope.WireProtocolVersion;

        /// <summary>
        /// Gets the protocol-library version embedded by the remote mod.
        /// </summary>
        public SemanticVersion RemoteLibraryVersion => Envelope.LibraryVersion;

        /// <summary>
        /// Gets the parsed stable message envelope.
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public ApiWireEnvelope Envelope { get; }

        /// <summary>
        /// Gets whether the remote wire protocol is too old or too new.
        /// </summary>
        public ApiWireCompatibilityStatus CompatibilityStatus { get; }

        /// <summary>
        /// Creates wire-incompatibility event data.
        /// </summary>
        /// <param name="envelope">
        /// The stable envelope parsed from the incompatible message.
        /// </param>
        /// <param name="compatibilityStatus">
        /// The wire compatibility result.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="envelope"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="compatibilityStatus"/> is compatible.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="compatibilityStatus"/> is undefined.
        /// </exception>
        public ApiWireIncompatibilityEventArgs(ApiWireEnvelope envelope, ApiWireCompatibilityStatus compatibilityStatus)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            if (compatibilityStatus < ApiWireCompatibilityStatus.Compatible || compatibilityStatus > ApiWireCompatibilityStatus.RemoteTooNew)
                throw new ArgumentException("The value is outside the supported range.", nameof(compatibilityStatus));

            if (compatibilityStatus == ApiWireCompatibilityStatus.Compatible)
                throw new ArgumentException("A wire-incompatibility event requires an incompatible status.", nameof(compatibilityStatus));

            Envelope = envelope;
            CompatibilityStatus = compatibilityStatus;
        }
    }
}