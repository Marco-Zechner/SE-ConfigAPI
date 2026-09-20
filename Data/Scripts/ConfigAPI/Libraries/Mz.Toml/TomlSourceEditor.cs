using System;

namespace Mz.Toml
{
    /// <summary>
    /// Composes source-preserving TOML edits while refreshing syntax after every
    /// successful change. Each edit is validated by reparsing the complete resulting
    /// source. A failed edit leaves the current source and syntax unchanged.
    /// </summary>
    public sealed class TomlSourceEditor
    {
        internal TomlSourceEditor(TomlSyntaxDocument syntax)
        {
            if (syntax == null)
                throw new ArgumentNullException(nameof(syntax));

            Syntax = syntax;
        }

        /// <summary>
        /// Gets the exact current TOML source.
        /// </summary>
        public string Source => Syntax.Source;

        /// <summary>
        /// Gets the current source-preserving syntax document.
        /// A successful edit replaces this syntax document, so callers must reacquire
        /// node references before making another node-based edit.
        /// </summary>
        public TomlSyntaxDocument Syntax { get; private set; }

        /// <summary>
        /// Disables the specified active assignment and refreshes the current syntax.
        /// </summary>
        public void DisableAssignment(TomlSyntaxNode node) 
            => Apply(Syntax.DisableAssignment(node));

        /// <summary>
        /// Enables the specified disabled assignment and refreshes the current syntax.
        /// </summary>
        public void EnableAssignment(TomlSyntaxNode node) 
            => Apply(Syntax.EnableAssignment(node));

        /// <summary>
        /// Replaces only the exact value source of the specified assignment and
        /// refreshes the current syntax.
        /// </summary>
        public void ReplaceAssignmentValue(TomlSyntaxNode node, string valueSource) 
            => Apply(Syntax.ReplaceAssignmentValue(node, valueSource));

        /// <summary>
        /// Inserts validated TOML source immediately before the specified node and
        /// refreshes the current syntax.
        /// </summary>
        public void InsertSourceBefore(TomlSyntaxNode node, string sourceFragment) 
            => Apply(Syntax.InsertSourceBefore(node, sourceFragment));

        /// <summary>
        /// Inserts validated TOML source immediately after the specified node and
        /// refreshes the current syntax.
        /// </summary>
        public void InsertSourceAfter(TomlSyntaxNode node, string sourceFragment) 
            => Apply(Syntax.InsertSourceAfter(node, sourceFragment));

        /// <summary>
        /// Inserts validated TOML source at the beginning of the document and
        /// refreshes the current syntax.
        /// </summary>
        public void InsertSourceAtStart(string sourceFragment) 
            => Apply(Syntax.InsertSourceAtStart(sourceFragment));

        /// <summary>
        /// Inserts validated TOML source at the end of the document and refreshes
        /// the current syntax.
        /// </summary>
        public void InsertSourceAtEnd(string sourceFragment) 
            => Apply(Syntax.InsertSourceAtEnd(sourceFragment));

        /// <summary>
        /// Applies the specified edited source to the current syntax.
        /// </summary>
        /// <param name="editedSource">The edited source to apply.</param>
        private void Apply(string editedSource)
        {
            var parsed = Toml.TryParse(editedSource);

            if (!parsed.IsSuccess || parsed.Syntax == null)
            {
                var message = "The source edit would make the TOML document invalid.";

                if (parsed.Diagnostics.Count > 0)
                    message += " " + parsed.Diagnostics[0];

                throw new InvalidOperationException(message);
            }

            Syntax = parsed.Syntax;
        }
    }
}
