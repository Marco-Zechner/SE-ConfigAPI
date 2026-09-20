using System;

namespace Mz.SemanticVersioning
{
    /// <summary>
    /// Describes one exact source-package dependency.
    /// </summary>
    public sealed class LibraryDependency
    {
        /// <summary>
        /// Gets the exact SELibs package identifier.
        /// </summary>
        public string PackageId { get; }

        /// <summary>
        /// Gets the exact required package version.
        /// </summary>
        public SemanticVersion Version { get; }

        /// <summary>
        /// Creates an exact package dependency declaration.
        /// </summary>
        /// <param name="packageId">The SELibs package identifier.</param>
        /// <param name="version">The exact numeric major.minor.patch version.</param>
        public LibraryDependency(string packageId, string version)
        {
            if (packageId == null)
                throw new ArgumentNullException(nameof(packageId));

            if (!IsValidPackageId(packageId))
                throw new ArgumentException("Package ID must be a valid dotted identifier.", nameof(packageId));

            if (version == null)
                throw new ArgumentNullException(nameof(version));

            if (!IsExactNumericVersion(version))
                throw new FormatException("Dependency version must use the exact major.minor.patch format.");

            PackageId = packageId;
            Version = SemanticVersion.Parse(version);
        }

        private static bool IsValidPackageId(string value)
        {
            var segments = value.Split('.');

            foreach (var segment in segments)
            {
                if (segment.Length == 0 || !IsIdentifierStart(segment[0]))
                    return false;

                for (var index = 1; index < segment.Length; index++)
                {
                    if (!IsIdentifierPart(segment[index]))
                        return false;
                }
            }

            return true;
        }

        private static bool IsIdentifierStart(char value) => value == '_' || value >= 'A' && value <= 'Z' || value >= 'a' && value <= 'z';

        private static bool IsIdentifierPart(char value) => IsIdentifierStart(value) || value >= '0' && value <= '9';

        private static bool IsExactNumericVersion(string value)
        {
            var components = value.Split('.');

            if (components.Length != 3)
                return false;

            foreach (var component in components)
            {
                if (component.Length == 0)
                    return false;

                foreach (var character in component)
                {
                    if (character < '0' || character > '9')
                        return false;
                }
            }

            return true;
        }
    }
}
