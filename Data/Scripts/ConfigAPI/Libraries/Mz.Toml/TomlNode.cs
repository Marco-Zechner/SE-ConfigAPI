namespace Mz.Toml
{
    /// <summary>
    /// Base type for nodes in a TOML document.
    /// </summary>
    public abstract class TomlNode
    {
        internal TomlNode(TomlNodeKind kind, int line, int column)
        {
            Kind = kind;
            Line = line;
            Column = column;
        }

        /// <summary>
        /// Gets the structural node kind.
        /// </summary>
        public TomlNodeKind Kind { get; }

        /// <summary>
        /// Gets the one-based source line where the node originated.
        /// A value of zero indicates a programmatically-created node.
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// Gets the one-based source column where the node originated.
        /// A value of zero indicates a programmatically-created node.
        /// </summary>
        public int Column { get; }
    }
}
