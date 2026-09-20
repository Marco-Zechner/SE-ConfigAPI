using System;

namespace Mz.Toml
{
    /// <summary>
    /// Identifies a half-open range of characters in TOML source text.
    /// </summary>
    public struct TomlSourceSpan : IEquatable<TomlSourceSpan>
    {
        /// <summary>
        /// Creates a source span beginning at <paramref name="start"/> and containing <paramref name="length"/> characters.
        /// </summary>
        public TomlSourceSpan(int start, int length)
        {
            if (start < 0)
                throw new ArgumentException("Source span start cannot be negative.", nameof(start));
            if (length < 0)
                throw new ArgumentException("Source span length cannot be negative.", nameof(length));
            if (length > int.MaxValue - start)
                throw new ArgumentException("Source span end cannot exceed Int32.MaxValue.", nameof(length));

            Start = start;
            Length = length;
        }

        /// <summary>
        /// Gets the zero-based source offset at which the span begins.
        /// </summary>
        public int Start { get; }

        /// <summary>
        /// Gets the number of characters contained by the span.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets the exclusive zero-based source offset at which the span ends.
        /// </summary>
        public int End => Start + Length;

        /// <summary>
        /// Returns whether the supplied source offset lies inside this span.
        /// </summary>
        public bool Contains(int offset) => offset >= Start && offset < End;

        /// <summary>
        /// Returns whether another source span has the same start and length.
        /// </summary>
        public bool Equals(TomlSourceSpan other) => Start == other.Start && Length == other.Length;

        /// <summary>
        /// Returns whether another object is an equal source span.
        /// </summary>
        public override bool Equals(object obj) => obj is TomlSourceSpan && Equals((TomlSourceSpan)obj);

        /// <summary>
        /// Returns a hash code derived from the span start and length.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Start * 397) ^ Length;
            }
        }

        /// <summary>
        /// Returns the span using half-open range notation.
        /// </summary>
        public override string ToString() => $"[{Start}..{End})";

        /// <summary>
        /// Returns whether two source spans are equal.
        /// </summary>
        public static bool operator ==(TomlSourceSpan left, TomlSourceSpan right) => left.Equals(right);

        /// <summary>
        /// Returns whether two source spans are different.
        /// </summary>
        public static bool operator !=(TomlSourceSpan left, TomlSourceSpan right) => !left.Equals(right);
    }
}
