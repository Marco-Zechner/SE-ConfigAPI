using System;
using System.Globalization;

namespace Mz.SemanticVersioning
{
    /// <summary>
    /// Represents a semantic version containing major, minor, and patch
    /// components.
    /// </summary>
    public sealed class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
    {
        /// <summary>
        /// Gets the major version component.
        /// </summary>
        public int Major { get; }

        /// <summary>
        /// Gets the minor version component.
        /// </summary>
        public int Minor { get; }

        /// <summary>
        /// Gets the patch version component.
        /// </summary>
        public int Patch { get; }

        /// <summary>
        /// Creates a semantic version from its numeric components.
        /// </summary>
        /// <param name="major">The non-negative major component.</param>
        /// <param name="minor">The non-negative minor component.</param>
        /// <param name="patch">The non-negative patch component.</param>
        public SemanticVersion(int major, int minor, int patch)
        {
            if (major < 0)
                throw new ArgumentException("The major component cannot be negative.", nameof(major));

            if (minor < 0)
                throw new ArgumentException("The minor component cannot be negative.", nameof(minor));

            if (patch < 0)
                throw new ArgumentException("The patch component cannot be negative.", nameof(patch));

            Major = major;
            Minor = minor;
            Patch = patch;
        }

        /// <summary>
        /// Parses an exact major.minor.patch version string.
        /// </summary>
        /// <param name="value">The version string to parse.</param>
        /// <returns>The parsed semantic version.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="value"/> is null.
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown when the value is not a valid major.minor.patch version.
        /// </exception>
        public static SemanticVersion Parse(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            SemanticVersion version;

            if (TryParse(value, out version)) 
                return version;
            throw new FormatException("Semantic version must use the format major.minor.patch with three non-negative decimal integer components.");
        }

        /// <summary>
        /// Attempts to parse an exact major.minor.patch version string.
        /// </summary>
        /// <param name="value">The version string to parse.</param>
        /// <param name="version">
        /// Receives the parsed version, or null when parsing fails.
        /// </param>
        /// <returns>True when parsing succeeds; otherwise false.</returns>
        public static bool TryParse(string value, out SemanticVersion version)
        {
            version = null;

            if (value == null)
                return false;

            var trimmedValue = value.Trim();

            if (trimmedValue.Length == 0)
                return false;

            var components = trimmedValue.Split('.');

            if (components.Length != 3)
                return false;

            int major;
            int minor;
            int patch;

            if (!TryParseComponent(components[0], out major))
                return false;

            if (!TryParseComponent(components[1], out minor))
                return false;

            if (!TryParseComponent(components[2], out patch))
                return false;

            version = new SemanticVersion(major, minor, patch);
            return true;
        }

        /// <summary>
        /// Compares this version with another semantic version.
        /// </summary>
        /// <param name="other">The version to compare with.</param>
        /// <returns>
        /// A negative value when this version is lower, zero when equal,
        /// or a positive value when this version is higher.
        /// </returns>
        public int CompareTo(SemanticVersion other)
        {
            if (ReferenceEquals(other, null))
                return 1;

            var majorComparison = Major.CompareTo(other.Major);

            if (majorComparison != 0)
                return majorComparison;

            var minorComparison = Minor.CompareTo(other.Minor);

            return minorComparison != 0 
                ? minorComparison 
                : Patch.CompareTo(other.Patch);
        }

        /// <summary>
        /// Determines whether this version has the same components as another.
        /// </summary>
        /// <param name="other">The version to compare with.</param>
        /// <returns>True when all components are equal.</returns>
        public bool Equals(SemanticVersion other)
        {
            if (ReferenceEquals(other, null))
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return Major == other.Major
                && Minor == other.Minor
                && Patch == other.Patch;
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => Equals(obj as SemanticVersion);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = 17;

                hashCode = Major + hashCode * 31;
                hashCode = Minor + hashCode * 31;
                hashCode = Patch + hashCode * 31;

                return hashCode;
            }
        }

        /// <summary>
        /// Returns the normalized major.minor.patch representation.
        /// </summary>
        /// <returns>The normalized version string.</returns>
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");

        /// <summary>
        /// Determines whether two semantic versions are equal.
        /// </summary>
        /// <param name="left">The first version to compare.</param>
        /// <param name="right">The second version to compare.</param>
        /// <returns>true if the versions are equal; otherwise, false.</returns>
        public static bool operator ==(SemanticVersion left, SemanticVersion right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (ReferenceEquals(left, null))
                return false;

            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two semantic versions are not equal.
        /// </summary>
        /// <param name="left">The first version to compare.</param>
        /// <param name="right">The second version to compare.</param>
        /// <returns>true if the versions are not equal; otherwise, false.</returns>
        public static bool operator !=(SemanticVersion left, SemanticVersion right) => !(left == right);

        /// <summary>
        /// Determines whether the first version is less than the second version.
        /// </summary>
        /// <param name="left">The first version to compare.</param>
        /// <param name="right">The second version to compare.</param>
        /// <returns>true if the first version is less than the second version; otherwise, false.</returns>
        public static bool operator <(SemanticVersion left, SemanticVersion right) => Compare(left, right) < 0;

        /// <summary>
        /// Determines whether the first version is less than or equal to the second version.
        /// </summary>
        /// <param name="left">The first version to compare.</param>
        /// <param name="right">The second version to compare.</param>
        /// <returns>true if the first version is less than or equal to the second version; otherwise, false.</returns>
        public static bool operator <=(SemanticVersion left, SemanticVersion right) => Compare(left, right) <= 0;

        /// <summary>
        /// Determines whether the first version is greater than the second version.
        /// </summary>
        /// <param name="left">The first version to compare.</param>
        /// <param name="right">The second version to compare.</param>
        /// <returns>true if the first version is greater than the second version; otherwise, false.</returns>
        public static bool operator >(SemanticVersion left, SemanticVersion right) => Compare(left, right) > 0;

        /// <summary>
        /// Determines whether the first version is greater than or equal to the second version.
        /// </summary>
        /// <param name="left">The first version to compare.</param>
        /// <param name="right">The second version to compare.</param>
        /// <returns>true if the first version is greater than or equal to the second version; otherwise, false.</returns>
        public static bool operator >=(SemanticVersion left, SemanticVersion right) => Compare(left, right) >= 0;

        private static int Compare(SemanticVersion left, SemanticVersion right)
        {
            if (ReferenceEquals(left, right))
                return 0;

            if (ReferenceEquals(left, null))
                return -1;

            if (ReferenceEquals(right, null))
                return 1;

            return left.CompareTo(right);
        }

        private static bool TryParseComponent(string value, out int component)
        {
            component = 0;

            if (string.IsNullOrEmpty(value))
                return false;

            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out component);
        }
    }
}