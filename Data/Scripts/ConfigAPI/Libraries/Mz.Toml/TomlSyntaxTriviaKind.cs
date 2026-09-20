namespace Mz.Toml
{
    /// <summary>
    /// Identifies source trivia that does not itself contribute a TOML semantic value.
    /// </summary>
    public enum TomlSyntaxTriviaKind
    {
        /// <summary>
        /// Spaces or tabs between TOML syntax constructs.
        /// </summary>
        Whitespace = 0,

        /// <summary>
        /// An LF or CRLF newline.
        /// </summary>
        Newline = 1,

        /// <summary>
        /// A TOML comment beginning with '#'.
        /// </summary>
        Comment = 2
    }
}
