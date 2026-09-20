namespace Mz.Toml
{
    /// <summary>
    /// Identifies a source-preserving top-level TOML syntax node.
    /// </summary>
    public enum TomlSyntaxNodeKind
    {
        /// <summary>
        /// Spaces or tabs between top-level TOML constructs.
        /// </summary>
        Whitespace = 0,

        /// <summary>
        /// An LF or CRLF newline.
        /// </summary>
        Newline = 1,

        /// <summary>
        /// A TOML comment beginning with '#'.
        /// </summary>
        Comment = 2,

        /// <summary>
        /// A complete active key/value assignment.
        /// </summary>
        Assignment = 3,

        /// <summary>
        /// A standard table header.
        /// </summary>
        TableHeader = 4,

        /// <summary>
        /// An array-of-tables header.
        /// </summary>
        ArrayTableHeader = 5,

        /// <summary>
        /// A source-preserved assignment disabled with the custom '#!' marker.
        /// </summary>
        DisabledAssignment = 6,

        /// <summary>
        /// A source range that could not be safely classified after parsing failed.
        /// </summary>
        Unparsed = 7
    }
}
