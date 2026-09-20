namespace Mz.Toml
{
    /// <summary>
    /// Identifies where trivia occurs relative to top-level TOML statements.
    /// This describes source layout only and does not imply comment ownership.
    /// </summary>
    public enum TomlSyntaxTriviaPlacement
    {
        /// <summary>
        /// Trivia between top-level statements.
        /// </summary>
        TopLevel = 0,

        /// <summary>
        /// Trivia lexically contained inside a top-level statement or its value.
        /// </summary>
        WithinStatement = 1,

        /// <summary>
        /// Same-line whitespace or comment trivia following a completed top-level statement.
        /// </summary>
        Trailing = 2
    }
}
