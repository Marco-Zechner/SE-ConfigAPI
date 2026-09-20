namespace Mz.Toml
{
    /// <summary>
    /// Identifies a TOML parser diagnostic.
    /// </summary>
    public enum TomlDiagnosticCode
    {
        /// <summary>
        /// A key/value assignment is missing the equals sign.
        /// </summary>
        MissingEquals,

        /// <summary>
        /// A key is invalid for the syntax currently being parsed.
        /// </summary>
        InvalidKey,

        /// <summary>
        /// A key/value assignment has no value.
        /// </summary>
        MissingValue,

        /// <summary>
        /// A carriage return was not followed by a line feed.
        /// </summary>
        InvalidNewline,

        /// <summary>
        /// A TOML comment contains a prohibited control character.
        /// </summary>
        InvalidComment,

        /// <summary>
        /// The syntax is valid TOML territory but is not implemented by the
        /// current parser slice.
        /// </summary>
        UnsupportedSyntax,

        /// <summary>
        /// A string is malformed.
        /// </summary>
        InvalidString,

        /// <summary>
        /// A string escape sequence is malformed.
        /// </summary>
        InvalidEscape,

        /// <summary>
        /// A numeric literal is malformed.
        /// </summary>
        InvalidNumber,

        /// <summary>
        /// A TOML date or time literal is malformed.
        /// </summary>
        InvalidDateTime,

        /// <summary>
        /// A value token is not recognized.
        /// </summary>
        InvalidValue,

        /// <summary>
        /// A key is defined more than once.
        /// </summary>
        DuplicateKey,

        /// <summary>
        /// A table header explicitly defines a table more than once.
        /// </summary>
        DuplicateTable,

        /// <summary>
        /// A table or dotted-key path conflicts with an existing definition.
        /// </summary>
        TableConflict,

        /// <summary>
        /// A table header is malformed.
        /// </summary>
        InvalidTable,

        /// <summary>
        /// Unexpected non-comment characters follow a parsed value.
        /// </summary>
        TrailingCharacters,

        /// <summary>
        /// The input byte sequence is not valid UTF-8 TOML text.
        /// </summary>
        InvalidEncoding,

        /// <summary>
        /// A custom '#!' disabled assignment is malformed.
        /// </summary>
        InvalidDisabledAssignment
    }
}
