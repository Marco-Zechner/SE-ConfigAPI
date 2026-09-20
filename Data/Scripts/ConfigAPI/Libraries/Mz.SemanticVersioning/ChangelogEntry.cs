using System;
using System.Linq;

namespace Mz.SemanticVersioning
{
    /// <summary>
    /// Describes the changes published in one semantic package version.
    /// </summary>
    public sealed class ChangelogEntry
    {
        private readonly string[] _changes;

        /// <summary>
        /// Gets the package version described by this entry.
        /// </summary>
        public SemanticVersion Version { get; }

        /// <summary>
        /// Gets a copy of the ordered changes for this version.
        /// </summary>
        public string[] Changes => (string[])_changes.Clone();

        /// <summary>
        /// Creates one immutable changelog entry.
        /// </summary>
        /// <param name="version">
        /// The exact numeric major.minor.patch version.
        /// </param>
        /// <param name="changes">
        /// One or more non-empty descriptions of published changes.
        /// </param>
        public ChangelogEntry(string version, string[] changes)
        {
            if (version == null)
                throw new ArgumentNullException(nameof(version));

            if (changes == null)
                throw new ArgumentNullException(nameof(changes));

            if (changes.Length == 0)
                throw new ArgumentException("At least one changelog change is required.", nameof(changes));

            var copiedChanges = (string[])changes.Clone();

            if (copiedChanges.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Changelog changes cannot be empty.", nameof(changes));

            Version = SemanticVersion.Parse(version);
            _changes = copiedChanges;
        }
    }
}
