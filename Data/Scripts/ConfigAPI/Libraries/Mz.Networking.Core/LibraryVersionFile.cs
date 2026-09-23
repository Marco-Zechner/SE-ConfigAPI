using Mz.SemanticVersioning;

namespace Mz.Networking
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
        public const int Minor = 2;

        /// <summary>
        /// Gets the patch version number.
        /// </summary>
        public const int Patch = 2;

        /// <summary>
        /// Gets the version string.
        /// </summary>
        public static string VersionString => $"{Major}.{Minor}.{Patch}";

        /// <summary>
        /// Gets the exact package dependencies required by this release.
        /// </summary>
        public static LibraryDependency[] Dependencies { get; } = {
            new LibraryDependency("Mz.ApiProtocol", "0.3.1"),
            new LibraryDependency("Mz.SemanticVersioning", "0.2.0")
        };

        /// <summary>
        /// Gets the complete changelog ordered from newest to oldest.
        /// </summary>
        public static Changelog Changelog { get; } = new Changelog(
            VersionString,
            new[]
            {
                new ChangelogEntry("0.2.2", new[]
                {
                    "Updated the exact Mz.ApiProtocol dependency to 0.3.1.",
                    "Included the transitive Mz.Collections sources in Space Engineers source-copy validation."
                }),
                new ChangelogEntry(
                    "0.2.1",
                    new[]
                    {
                        "Improved internal variable declarations and formatting.",
                        "Improved receive classification for packets belonging to another network.",
                        "Declared exact dependencies on Mz.ApiProtocol 0.3.0 and Mz.SemanticVersioning 0.2.0."
                    }
                ),
                new ChangelogEntry(
                    "0.2.0",
                    new[]
                    {
                        "Added reliable and unreliable delivery selection with Space Engineers size validation.",
                        "Added wrap-aware application sequence comparison and tracking helpers.",
                        "Added versioned network identity and bounded structured receive diagnostics.",
                        "Added optional NetworkManager channel assignment, generation-checked reassignment, and active conflict reporting.",
                        "Added fallback and forced-channel managed-session modes while preserving legacy fixed-channel compatibility.",
                        "Added the exact Mz.ApiProtocol dependency and expanded source-copy validation."
                    }
                ),
                new ChangelogEntry(
                    "0.1.3",
                    new[]
                    {
                        "Checked and enforced code style and formatting.",
                        "Improved code style in the Guide codeblocks."
                    }
                ),
                new ChangelogEntry(
                    "0.1.2",
                    new[]
                    {
                        "Added compiled server and client copy-paste examples for a multiplayer ping flow.",
                        "Included the copy-paste guide in release bundles."
                    }
                ),
                new ChangelogEntry(
                    "0.1.1",
                    new[]
                    {
                        "Organized Core and Space Engineers sources by responsibility.",
                        "Preserved nested source folders in validation and release archives.",
                        "Added a complete package usage guide and installation examples.",
                        "Added the shared Mz.SemanticVersioning dependency.",
                        "Adopted the shared SemanticVersioning changelog model."
                    }
                ),
                new ChangelogEntry(
                    "0.1.0",
                    new[]
                    {
                        "Published the initial SELibs package release."
                    }
                )
            }
        );
    }
}
