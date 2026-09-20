using System;
using Mz.SemanticVersioning;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Describes the embedded API protocol library and its wire format.
    /// </summary>
    public static class ApiProtocolInfo
    {
        /// <summary>
        /// Gets the version of the embedded protocol-library implementation.
        /// </summary>
        /// <remarks>
        /// Different library versions may communicate when their wire
        /// protocol versions are compatible.
        /// </remarks>
        public static SemanticVersion LibraryVersion { get; } =
            new SemanticVersion(
                LibraryVersionFile.Major,
                LibraryVersionFile.Minor,
                LibraryVersionFile.Patch);

        /// <summary>
        /// Gets the wire-protocol version emitted by this implementation.
        /// </summary>
        public static SemanticVersion WireProtocolVersion { get; } = new SemanticVersion(1, 0, 0);

        /// <summary>
        /// Evaluates a remote wire-protocol version.
        /// </summary>
        /// <param name="remoteVersion">
        /// The wire-protocol version reported by a remote embedded library.
        /// </param>
        /// <returns>
        /// The compatibility result for the remote wire-protocol version.
        /// </returns>
        /// <remarks>
        /// Versions with the same major component are compatible. Minor and
        /// patch changes must remain backward compatible and may only append
        /// optional wire fields.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="remoteVersion"/> is null.
        /// </exception>
        public static ApiWireCompatibilityStatus EvaluateWireProtocol(SemanticVersion remoteVersion)
        {
            if (remoteVersion == null)
                throw new ArgumentNullException(nameof(remoteVersion));

            if (remoteVersion.Major < WireProtocolVersion.Major)
                return ApiWireCompatibilityStatus.RemoteTooOld;

            if (remoteVersion.Major > WireProtocolVersion.Major)
                return ApiWireCompatibilityStatus.RemoteTooNew;

            return ApiWireCompatibilityStatus.Compatible;
        }

        /// <summary>
        /// Determines whether a remote wire-protocol version is compatible.
        /// </summary>
        /// <param name="remoteVersion">
        /// The wire-protocol version reported by a remote embedded library.
        /// </param>
        /// <returns>
        /// True when the versions can communicate; otherwise false.
        /// </returns>
        public static bool IsWireProtocolCompatible(SemanticVersion remoteVersion) 
            => EvaluateWireProtocol(remoteVersion) == ApiWireCompatibilityStatus.Compatible;
    }
}