using System.Collections.Generic;
using Mz.Toml.Internal;

namespace Mz.Toml
{
    /// <summary>
    /// Represents the result of a non-throwing TOML parse operation.
    /// </summary>
    public sealed class TomlParseResult
    {
        internal TomlParseResult(TomlDocument document, IEnumerable<TomlDiagnostic> diagnostics, TomlSyntaxDocument syntax = null)
        {
            var copy = new List<TomlDiagnostic>(diagnostics);
            Document = document;
            Diagnostics = new TomlReadOnlyList<TomlDiagnostic>(copy);
            Syntax = syntax;
        }

        /// <summary>
        /// Gets a value indicating whether parsing succeeded.
        /// </summary>
        public bool IsSuccess => Document != null && Diagnostics.Count == 0;

        /// <summary>
        /// Gets the parsed document, or null when parsing failed.
        /// </summary>
        public TomlDocument Document { get; }

        /// <summary>
        /// Gets parse diagnostics. The collection is empty on success.
        /// </summary>
        public IReadOnlyList<TomlDiagnostic> Diagnostics { get; }

        /// <summary>
        /// Gets the exact source-preserving syntax document when source text was
        /// available. Failed decoded-text parses retain syntax for safely classified
        /// source and may contain an Unparsed node for the remaining source. This is
        /// null when exact source text could not be produced, such as invalid UTF-8
        /// byte input.
        /// </summary>
        public TomlSyntaxDocument Syntax { get; }
    }
}
