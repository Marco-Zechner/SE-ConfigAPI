using System;
using System.Collections.Generic;
using Mz.Toml.Internal;

namespace Mz.Toml
{
    /// <summary>
    /// Preserves the exact TOML source string and its ordered top-level syntax ranges.
    /// </summary>
    public sealed class TomlSyntaxDocument
    {
        internal TomlSyntaxDocument(string source, IEnumerable<TomlSyntaxNode> nodes, IEnumerable<TomlSyntaxTrivia> trivia)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));
            if (trivia == null)
                throw new ArgumentNullException(nameof(trivia));

            Source = source;
            Nodes = new TomlReadOnlyList<TomlSyntaxNode>(new List<TomlSyntaxNode>(nodes));
            Trivia = new TomlReadOnlyList<TomlSyntaxTrivia>(new List<TomlSyntaxTrivia>(trivia));
        }

        /// <summary>
        /// Gets the exact source string supplied to the parser.
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Gets source-preserving syntax nodes in source order. For decoded text,
        /// these ranges preserve the complete source layout. A failed parse may end
        /// with an Unparsed node covering source that could not be safely classified.
        /// </summary>
        public IReadOnlyList<TomlSyntaxNode> Nodes { get; }

        /// <summary>
        /// Gets whitespace, newline, and comment ranges in exact source order.
        /// Trivia may lie inside a larger syntax node such as a multiline array assignment.
        /// </summary>
        public IReadOnlyList<TomlSyntaxTrivia> Trivia { get; }

        /// <summary>
        /// Creates a stateful source-preserving editor for composing validated edits.
        /// The source must currently parse successfully; syntax retained from a failed
        /// parse cannot be edited through this API.
        /// </summary>
        public TomlSourceEditor CreateEditor()
        {
            var parsed = Toml.TryParse(Source);

            if (!parsed.IsSuccess)
                throw new InvalidOperationException("A source editor can only be created for currently valid TOML source.");

            return new TomlSourceEditor(this);
        }

        /// <summary>
        /// Returns source with the specified active assignment disabled by inserting
        /// the custom '#!' marker at the assignment start.
        /// </summary>
        public string DisableAssignment(TomlSyntaxNode node)
        {
            ValidateOwnedNode(node);

            if (node.Kind != TomlSyntaxNodeKind.Assignment)
                throw new ArgumentException("The syntax node is not an active assignment.", nameof(node));

            return Source.Insert(node.Span.Start, "#!");
        }

        /// <summary>
        /// Returns source with the specified disabled assignment enabled by removing
        /// only its custom '#!' marker.
        /// </summary>
        public string EnableAssignment(TomlSyntaxNode node)
        {
            ValidateOwnedNode(node);

            if (node.Kind != TomlSyntaxNodeKind.DisabledAssignment)
                throw new ArgumentException("The syntax node is not a disabled assignment.", nameof(node));

            var potentialMarker = Source.Remove(0, Math.Min(node.Span.Start, Source.Length));
            
            if (!potentialMarker.StartsWith("#!"))
                throw new InvalidOperationException("The disabled assignment does not contain the expected '#!' marker.");
            
            return Source.Remove(node.Span.Start, 2);
        }

        /// <summary>
        /// Returns source with <paramref name="sourceFragment"/> inserted immediately
        /// before the specified owned top-level syntax node.
        /// </summary>
        public string InsertSourceBefore(TomlSyntaxNode node, string sourceFragment)
        {
            ValidateOwnedNode(node);
            return InsertValidatedSource(node.Span.Start, sourceFragment);
        }

        /// <summary>
        /// Returns source with <paramref name="sourceFragment"/> inserted immediately
        /// after the specified owned top-level syntax node.
        /// </summary>
        public string InsertSourceAfter(TomlSyntaxNode node, string sourceFragment)
        {
            ValidateOwnedNode(node);
            return InsertValidatedSource(node.Span.End, sourceFragment);
        }

        /// <summary>
        /// Returns source with <paramref name="sourceFragment"/> inserted at the
        /// beginning of the document.
        /// </summary>
        public string InsertSourceAtStart(string sourceFragment) 
            => InsertValidatedSource(0, sourceFragment);

        /// <summary>
        /// Returns source with <paramref name="sourceFragment"/> inserted at the
        /// end of the document.
        /// </summary>
        public string InsertSourceAtEnd(string sourceFragment) 
            => InsertValidatedSource(Source.Length, sourceFragment);

        /// <summary>
        /// Returns source with only the exact value range of the specified active
        /// or disabled assignment replaced by <paramref name="valueSource"/>.
        /// </summary>
        public string ReplaceAssignmentValue(TomlSyntaxNode node, string valueSource)
        {
            ValidateOwnedNode(node);

            if (node.Kind != TomlSyntaxNodeKind.Assignment && node.Kind != TomlSyntaxNodeKind.DisabledAssignment)
                throw new ArgumentException("The syntax node is not an active or disabled assignment.", nameof(node));

            if (valueSource == null)
                throw new ArgumentNullException(nameof(valueSource));

            ValidateValueSource(valueSource);

            if (!node.ValueSpan.HasValue)
                throw new InvalidOperationException("The assignment syntax node does not contain a value span.");

            var valueSpan = node.ValueSpan.Value;

            return Source.Remove(valueSpan.Start, valueSpan.Length).Insert(valueSpan.Start, valueSource);
        }

        private string InsertValidatedSource(int offset, string sourceFragment)
        {
            if (sourceFragment == null)
                throw new ArgumentNullException(nameof(sourceFragment));

            if (sourceFragment.Length == 0 || !Toml.TryParse(sourceFragment).IsSuccess)
                throw new ArgumentException("The inserted source fragment must be non-empty valid TOML source.", nameof(sourceFragment));

            var edited = Source.Insert(offset, sourceFragment);

            if (!Toml.TryParse(edited).IsSuccess)
                throw new ArgumentException("The inserted source fragment would make the TOML document invalid.", nameof(sourceFragment));

            return edited;
        }

        private static void ValidateValueSource(string valueSource)
        {
            const string prefix = "value = ";
            var parsed = Toml.TryParse(prefix + valueSource);

            const string errorMessage = "Replacement text must be exactly one valid TOML value.";
            if (!parsed.IsSuccess || parsed.Syntax == null)
                throw new ArgumentException(errorMessage + " Parsing failed.", nameof(valueSource));
            
            if (parsed.Syntax.Nodes.Count == 0 || parsed.Syntax.Nodes[0].Kind != TomlSyntaxNodeKind.Assignment)
                throw new ArgumentException(errorMessage + " Parsed syntax has no valid assignment.", nameof(valueSource));
            
            if (!parsed.Syntax.Nodes[0].ValueSpan.HasValue)
                throw new ArgumentException(errorMessage + " Parsed syntax has no valid value span.", nameof(valueSource));

            var span = parsed.Syntax.Nodes[0].ValueSpan.Value;

            if (span.Start != prefix.Length || span.Length != valueSource.Length)
                throw new ArgumentException(errorMessage + " Parsed value does not match the expected format.", nameof(valueSource));
        }

        private void ValidateOwnedNode(TomlSyntaxNode node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            foreach (var t in Nodes)
                if (ReferenceEquals(t, node))
                    return;

            throw new ArgumentException("The syntax node does not belong to this syntax document.", nameof(node));
        }
    }
}
