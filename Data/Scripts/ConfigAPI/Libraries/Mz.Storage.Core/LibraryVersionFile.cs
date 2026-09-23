using Mz.SemanticVersioning;

namespace Mz.Storage
{
    /// <summary>
    /// Defines the released version and ordered changelog of this library package.
    /// </summary>
    public static class LibraryVersionFile
    {
        /// <summary>
        /// Gets the major version number.
        /// </summary>
        public const int Major = 0;

        /// <summary>
        /// Gets the minor version number.
        /// </summary>
        public const int Minor = 1;

        /// <summary>
        /// Gets the patch version number.
        /// </summary>
        public const int Patch = 1;

        /// <summary>
        /// Gets the version string.
        /// </summary>
        public static string VersionString => $"{Major}.{Minor}.{Patch}";

        /// <summary>
        /// Gets the exact package dependencies required by this release.
        /// </summary>
        public static LibraryDependency[] Dependencies { get; } = {
            new LibraryDependency("Mz.SemanticVersioning", "0.2.0")
        };

        /// <summary>
        /// Gets the complete changelog ordered from newest to oldest.
        /// </summary>
        public static Changelog Changelog { get; } = new Changelog(
            VersionString,
            new[]
            {
                new ChangelogEntry("0.1.1", new[]
                {
                    "Global storage maps leading-dot logical metadata names after the owner prefix, avoiding duplicated dots such as MyMod..defaults."
                }),
                new ChangelogEntry("0.1.0", new[]
                {
                    "Published indexed logical-name storage with persisted discovery and stale-entry repair.",
                    "Added local and world Space Engineers storage adapters using assembly-scoped ModAPI storage.",
                    "Added safely namespaced global storage through a caller-owned physical filename prefix.",
                    "Uses .index for Local and World indexes and .<owner>.index for Global indexes.",
                    "Added the exact Mz.SemanticVersioning 0.2.0 SELibs dependency."
                })
            }
        );
    }
}
