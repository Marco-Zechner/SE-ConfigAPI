using Mz.SemanticVersioning;

namespace Mz.ApiProtocol
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
        public const int Minor = 3;

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
            new LibraryDependency("Mz.Collections", "0.1.0"),
            new LibraryDependency("Mz.SemanticVersioning", "0.2.0")
        };

        /// <summary>
        /// Gets the complete changelog ordered from newest to oldest.
        /// </summary>
        public static Changelog Changelog { get; } = new Changelog(
            VersionString,
            new[]
            {
                new ChangelogEntry("0.3.1", new[]
                {
                    "Replaced the duplicated internal read-only collection wrappers with Mz.Collections 0.1.0.",
                    "Declared the exact Mz.Collections 0.1.0 package dependency."
                }),
                new ChangelogEntry(
                    "0.3.0",
                    new[]
                    {
                        "Added a params ApiEndpointContract constructor for concise endpoint declarations.",
                        "Declared the exact Mz.SemanticVersioning 0.2.0 SELibs dependency."
                    }
                ),
                new ChangelogEntry(
                    "0.2.5",
                    new[]
                    {
                        "Published the guide example style improvements after correcting release validation."
                    }
                ),
                new ChangelogEntry(
                    "0.2.3",
                    new[]
                    {
                        "Improved code style in the guide examples."
                    }
                ),
                new ChangelogEntry(
                    "0.2.2",
                    new[]
                    {
                        "Added compiled provider, consumer-facade, and downstream usage examples.",
                        "Included the copy-paste guide in release bundles."
                    }
                ),
                new ChangelogEntry(
                    "0.2.1",
                    new[]
                    {
                        "Added a complete package usage guide and installation examples.",
                        "Adopted the shared SemanticVersioning changelog model."
                    }
                ),
                new ChangelogEntry(
                    "0.2.0",
                    new[]
                    {
                        "Published the initial SELibs package release."
                    }
                )
            }
        );
    }
}
