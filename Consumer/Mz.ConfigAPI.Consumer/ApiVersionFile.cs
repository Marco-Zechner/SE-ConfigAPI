using Mz.SemanticVersioning;

namespace Mz.ConfigApi
{
    public static class ApiVersionFile
    {
        public const int Major = 2;
        public const int Minor = 3;
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
                    "2.3.0",
                    new[]
                    {
                        "Added optional ApplyPreset support for Local and Global configs while preserving the canonical ConfigDefinition<T>.DefaultFile as the active config file.",
                        "Added optional asynchronous ApplyPresetWorld support for server-authoritative World configs without changing the existing ConfigAPI 2.2 World file-operation capability group.",
                        "Added SupportsPresets and SupportsWorldPresets; older 2.0-2.2 providers remain compatible and simply report these capabilities as unavailable.",
                        "Added optional SavePreset support for Local and Global configs with overwrite disabled by default; preset saves do not change active config identity.",
                        "Added optional asynchronous SavePresetWorld support plus SupportsPresetSaving and SupportsWorldPresetSaving; World preset saves do not switch CurrentFile or advance ServerIteration.",
                    }
                ),
                new ChangelogEntry(
                    "2.2.0",
                    new[]
                    {
                        "Added optional asynchronous ReloadWorld, LoadAndSwitchWorld, SaveAndSwitchWorld, and ExportWorld operations for server-authoritative World configs.",
                        "Added SupportsWorldFileOperations while preserving compatibility with providers that expose only the ConfigAPI 2.1 World endpoint set.",
                        "ExportWorld writes a requested file without switching authoritative state or advancing its server iteration.",
                    }
                ),
                new ChangelogEntry(
                    "2.1.0",
                    new[]
                    {
                        "Added optional server-authoritative World config endpoints without breaking compatibility with ConfigAPI 2.0 providers.",
                        "Added asynchronous OpenWorld and SaveWorld operations plus WorldConfigResponse notifications for authoritative snapshots, stale responses, and errors.",
                    }
                ),
                new ChangelogEntry(
                    "2.0.3",
                    new[]
                    {
                        "Made ConfigHandle<T>.SwitchFile failure-atomic so a failed load preserves the previous CurrentFile and Value.",
                    }
                ),
                new ChangelogEntry(
                    "2.0.2",
                    new[]
                    {
                        "Corrected the consumer contract documentation: typed configs use caller-supplied ConfigDefinition<T> serialization delegates; automatic reflection-based CLR mapping is not part of the released facade.",
                        "Documented that CLR model shapes are consumer-defined and map through ConfigDocument semantic values.",
                    }
                ),
                new ChangelogEntry(
                    "2.0.1",
                    new[]
                    {
                        "Fixed ConfigHandle<T>.SwitchFile and Reload so the selected CurrentFile is actually loaded and remains active for subsequent reloads.",
                    }
                ),
                new ChangelogEntry(
                    "2.0.0",
                    new[]
                    {
                        "Introduced the typed ConfigAPI consumer facade.",
                        "Added consumer-owned semantic config documents and values without provider-domain dependencies.",
                        "Added provider-backed Open and Save operations with exact endpoint validation.",
                        "Added typed Open<T> and Save<T> operations using caller-supplied ConfigDefinition<T> serialization and deserialization delegates.",
                        "Added ConfigDefinition<T> for explicit config identity, default file selection, and on-demand current-default creation.",
                        "Added client-owned ConfigHandle<T> state with CurrentFile, Value, and fresh-disk Reload through the existing Open persistence path.",
                        "Added semantic config values for null, Boolean, Integer, Float, String, Object, Array, and TOML date/time scalar kinds; consumer serializers decide how CLR models map to those values.",
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
