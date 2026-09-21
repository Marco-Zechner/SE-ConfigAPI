using Mz.SemanticVersioning;

namespace Mz.Toml
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
                new ChangelogEntry("0.2.2", new[]
                {
                    "Replaced the duplicated internal read-only list wrapper with Mz.Collections 0.1.0.",
                    "Declared the exact Mz.Collections 0.1.0 package dependency."
                }),
                new ChangelogEntry(
                    "0.2.1",
                    new[]
                    {
                        "Applied internal naming and code-style cleanup.",
                        "Declared the exact Mz.SemanticVersioning 0.2.0 SELibs dependency."
                    }
                ),
                new ChangelogEntry(
                    "0.2.0",
                    new[]
                    {
                        "Added exact source-preserving syntax spans, comments, trivia, and objective trivia placement.",
                        "Added the custom '#!' disabled-assignment extension while keeping disabled values out of the semantic document.",
                        "Added source-preserving enable, disable, value-replacement, and validated source-insertion operations.",
                        "Added recoverable syntax information for invalid decoded source, including explicit unparsed ranges.",
                        "Added the composable TomlSourceEditor with refreshed syntax, stale-node rejection, and atomic validated edits.",
                        "Kept strict TOML 1.0 semantic parsing, deterministic canonical writing, and Space Engineers C# 6 source-copy compatibility."
                    }
                ),
                new ChangelogEntry(
                    "0.1.0",
                    new[]
                    {
                        "Published the initial strict TOML 1.0 source library.",
                        "Added parsing, diagnostics, a mutable document model, and deterministic canonical writing.",
                        "Added strict UTF-8 byte parsing and complete pinned TOML 1.0 valid/invalid corpus validation.",
                        "Added permanent Space Engineers C# 6 source-copy validation.",
                        "Adopted the shared Mz.SemanticVersioning package metadata model."
                    }
                )
            }
        );
    }
}
