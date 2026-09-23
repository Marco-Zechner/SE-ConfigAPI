using Mz.SemanticVersioning;

namespace Mz.Collections
{
    /// <summary>
    /// Defines the released version and ordered changelog of this library
    /// package.
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
        public const int Patch = 0;

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
                new ChangelogEntry(
                    "0.1.0",
                    new[]
                    {
                        "Published reusable live read-only list and dictionary views.",
                        "Added the exact Mz.SemanticVersioning 0.2.0 SELibs dependency."
                    }
                )
            }
        );
    }
}