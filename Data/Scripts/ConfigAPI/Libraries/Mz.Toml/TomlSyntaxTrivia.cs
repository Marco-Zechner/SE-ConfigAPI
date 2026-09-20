namespace Mz.Toml
{
    /// <summary>
    /// Represents one exact whitespace, newline, or comment range in TOML source.
    /// </summary>
    public sealed class TomlSyntaxTrivia
    {
        internal TomlSyntaxTrivia(TomlSyntaxTriviaKind kind, TomlSourceSpan span, TomlSyntaxTriviaPlacement placement)
        {
            Kind = kind;
            Span = span;
            Placement = placement;
        }

        /// <summary>
        /// Gets the trivia kind.
        /// </summary>
        public TomlSyntaxTriviaKind Kind { get; }

        /// <summary>
        /// Gets the exact source range occupied by this trivia.
        /// </summary>
        public TomlSourceSpan Span { get; }

        /// <summary>
        /// Gets the objective source-layout placement of this trivia.
        /// This does not assign comments to semantic fields or statements.
        /// </summary>
        public TomlSyntaxTriviaPlacement Placement { get; }
    }
}
