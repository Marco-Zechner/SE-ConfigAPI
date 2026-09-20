using Mz.SemanticVersioning;

namespace Mz.CommandApi
{
    /// <summary>
    /// Defines the released CommandAPI consumer facade version and changelog.
    /// </summary>
    public static class ApiVersionFile
    {
        /// <summary>
        /// Gets the major facade version number.
        /// </summary>
        public const int Major = 1;

        /// <summary>
        /// Gets the minor facade version number.
        /// </summary>
        public const int Minor = 2;

        /// <summary>
        /// Gets the patch facade version number.
        /// </summary>
        public const int Patch = 0;

        /// <summary>
        /// Gets the minimum provider API version required by this facade.
        /// </summary>
        public static SemanticVersion MinimumProviderApiVersion { get; } =
            new SemanticVersion(
                1,
                1,
                0
            );

        /// <summary>
        /// Gets the exact SELibs package dependencies required by this consumer release.
        /// </summary>
        public static LibraryDependency[] Dependencies { get; } = {
            new LibraryDependency("Mz.ApiProtocol", "0.3.0"),
            new LibraryDependency("Mz.SemanticVersioning", "0.2.0")
        };

        /// <summary>
        /// Gets the normalized facade version string.
        /// </summary>
        public static string VersionString =>
            $"{Major}.{Minor}.{Patch}";

        /// <summary>
        /// Gets the complete facade changelog ordered from newest to oldest.
        /// </summary>
        public static Changelog Changelog { get; } =
            new Changelog(
                VersionString,
                new[]
                {
                    new ChangelogEntry(
                        "1.2.0",
                        new[]
                        {
                            "Rejected the reserved Internal execution location before public registrations reach the provider.",
                            "Added a packaged integration guide and expanded standalone consumer documentation.",
                            "Declared exact SELibs dependencies on Mz.ApiProtocol 0.3.0 and Mz.SemanticVersioning 0.2.0.",
                            "Retained compatibility with CommandAPI provider API 1.1.0 and newer compatible versions."
                        }
                    ),
                    new ChangelogEntry(
                        "1.1.0",
                        new[]
                        {
                            "Added the initial typed CommandAPI consumer facade.",
                            "Added typed command registration, request, response, execution-location, and severity models.",
                            "Added automatic discovery, exact RegisterCommand endpoint validation, rediscovery, and registration cleanup.",
                            "Accepted newer provider API versions without a hardcoded upper ceiling while retaining the minimum 1.1.0 contract floor."
                        }
                    )
                }
            );
    }
}
