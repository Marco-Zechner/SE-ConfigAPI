using System;
using Mz.SemanticVersioning;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Identifies a mod participating in API discovery.
    /// </summary>
    /// <remarks>
    /// The identifier should remain stable across mod releases. It should
    /// identify the mod project rather than a particular API or provider
    /// instance.
    /// </remarks>
    public sealed class ApiModIdentity
    {
        /// <summary>
        /// Gets the stable, case-sensitive mod identifier.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the human-readable mod name.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the mod version.
        /// </summary>
        public SemanticVersion Version { get; }

        /// <summary>
        /// Creates a mod identity.
        /// </summary>
        /// <param name="id">
        /// The stable, case-sensitive mod identifier.
        /// </param>
        /// <param name="displayName">
        /// The human-readable mod name.
        /// </param>
        /// <param name="version">
        /// The mod version.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="id"/> or
        /// <paramref name="displayName"/> is empty.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="version"/> is null.
        /// </exception>
        public ApiModIdentity(string id, string displayName, SemanticVersion version)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A stable mod identifier is required.", nameof(id));

            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("A mod display name is required.", nameof(displayName));

            if (version == null)
                throw new ArgumentNullException(nameof(version));

            Id = id.Trim();
            DisplayName = displayName.Trim();
            Version = version;
        }

        /// <summary>
        /// Returns a diagnostic representation of the mod identity.
        /// </summary>
        /// <returns>
        /// The display name, stable identifier, and mod version.
        /// </returns>
        public override string ToString() => $"{DisplayName} ({Id}) {Version}";
    }
}