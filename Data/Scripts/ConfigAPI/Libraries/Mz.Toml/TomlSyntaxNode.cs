namespace Mz.Toml
{
    /// <summary>
    /// Represents one ordered source-preserving TOML syntax range.
    /// </summary>
    public sealed class TomlSyntaxNode
    {
        internal TomlSyntaxNode(TomlSyntaxNodeKind kind, TomlSourceSpan span, TomlSourceSpan? valueSpan = null)
        {
            Kind = kind;
            Span = span;
            ValueSpan = valueSpan;
        }

        /// <summary>
        /// Gets the syntax node kind.
        /// </summary>
        public TomlSyntaxNodeKind Kind { get; }

        /// <summary>
        /// Gets the exact source range occupied by this node.
        /// </summary>
        public TomlSourceSpan Span { get; }

        /// <summary>
        /// Gets the exact value source range for an active or disabled assignment,
        /// or null for syntax nodes that do not contain an assignment value.
        /// </summary>
        public TomlSourceSpan? ValueSpan { get; }
    }
}
