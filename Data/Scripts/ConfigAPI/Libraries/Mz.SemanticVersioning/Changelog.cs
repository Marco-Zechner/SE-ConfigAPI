using System;

namespace Mz.SemanticVersioning
{
    /// <summary>
    /// Represents a complete immutable package changelog ordered from newest
    /// to oldest.
    /// </summary>
    public sealed class Changelog
    {
        private readonly ChangelogEntry[] _entries;

        /// <summary>
        /// Gets the current package version represented by this changelog.
        /// </summary>
        public SemanticVersion CurrentVersion { get; }

        /// <summary>
        /// Gets the newest changelog entry.
        /// </summary>
        public ChangelogEntry Current => _entries[0];

        /// <summary>
        /// Gets a copy of all entries ordered from newest to oldest.
        /// </summary>
        public ChangelogEntry[] Entries => (ChangelogEntry[])_entries.Clone();

        /// <summary>
        /// Creates and validates a complete package changelog.
        /// </summary>
        /// <param name="currentVersion">
        /// The current exact numeric major.minor.patch package version.
        /// </param>
        /// <param name="entries">
        /// All changelog entries ordered from newest to oldest.
        /// </param>
        public Changelog(string currentVersion, ChangelogEntry[] entries)
        {
            if (currentVersion == null)
                throw new ArgumentNullException(nameof(currentVersion));

            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            if (entries.Length == 0)
                throw new ArgumentException("At least one changelog entry is required.", nameof(entries));

            CurrentVersion = SemanticVersion.Parse(currentVersion);
            _entries = (ChangelogEntry[])entries.Clone();

            SemanticVersion previousVersion = null;

            foreach (var entry in _entries)
            {
                if (entry == null)
                    throw new ArgumentException("Changelog entries cannot contain null.", nameof(entries));

                if (previousVersion != null && entry.Version >= previousVersion)
                    throw new ArgumentException("Changelog entries must be ordered from newest to oldest with unique versions.", nameof(entries));

                previousVersion = entry.Version;
            }

            if (_entries[0].Version != CurrentVersion)
                throw new ArgumentException("The first changelog entry must match the current package version.", nameof(entries));
        }
    }
}
