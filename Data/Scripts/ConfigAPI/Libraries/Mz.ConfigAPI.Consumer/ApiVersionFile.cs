using Mz.SemanticVersioning;

namespace Mz.ConfigApi
{
    public static class ApiVersionFile
    {
        public const int Major = 2;
        public const int Minor = 0;
        public const int Patch = 0;

        public static SemanticVersion MinimumProviderApiVersion { get; } = new SemanticVersion(2, 0, 0);

        public static string VersionString => $"{Major}.{Minor}.{Patch}";

        /// <summary>
        /// Gets the exact SELibs package dependencies required by this consumer release.
        /// </summary>
        public static LibraryDependency[] Dependencies { get; } = {
            new LibraryDependency("Mz.ApiProtocol", "0.3.0"),
            new LibraryDependency("Mz.SemanticVersioning", "0.2.0")
        };
        public static Changelog Changelog { get; } = new Changelog(
            VersionString,
            new[]
            {
                new ChangelogEntry(
                    "2.0.0",
                    new[]
                    {
                        "Introduced the typed ConfigAPI consumer facade.",
                        "Added consumer-owned semantic config documents and values without provider-domain dependencies.",
                        "Added provider-backed Open and Save operations with exact endpoint validation.",
                        "Added reflection-based CLR config mapping and typed Open<T> and Save<T> operations for public field or public read/write property models.",
                        "Added ConfigDefinition<T> for explicit config identity, default file selection, and on-demand current-default creation.",
                        "Added client-owned ConfigHandle<T> state with CurrentFile, Value, and fresh-disk Reload through the existing Open persistence path.",
                        "Supported enums, nullable values, nested objects, one-dimensional arrays, List<T>, and Dictionary<string, T> in typed configs.",
                        "Reserved World configs for the server-authoritative path; direct Open and Save operations now accept only Local and Global.",
                        "Added automatic provider discovery and consumer-owned storage callback registration with reconnect-safe registration identifiers.",
                        "Accepted newer provider API versions without a hardcoded upper version ceiling.",
                        "Declared exact SELibs dependencies on Mz.ApiProtocol 0.3.0 and Mz.SemanticVersioning 0.2.0.",
                    }
                ),
            }
        );
    }
}
