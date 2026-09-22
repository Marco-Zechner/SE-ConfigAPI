using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Domain;
using Mz.Toml;

namespace MarcoZechner.ConfigAPI.Serialization
{
    public static class ConfigTomlSourceUpdater
    {
        private const string ValueWrapperKey = "__configapi_value__";
        private const string HeaderProbeKey = "__configapi_header_probe__";

        public static string SetValue(string source, ConfigValuePath path, ConfigNode value)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            TomlParseResult parsed = ParseSource(source);
            ConfigTomlSyntaxAssignment assignment = FindUniqueAssignment(
                ConfigTomlSyntaxIndex.Create(parsed),
                path);

            TomlSourceEditor editor = parsed.Syntax.CreateEditor();

            if (value is ConfigNullNode)
            {
                if (assignment.IsDisabled)
                    return source;

                editor.DisableAssignment(assignment.Node);
                return editor.Source;
            }

            string valueSource = RenderValueSource(value);

            if (assignment.IsDisabled)
            {
                editor.EnableAssignment(assignment.Node);

                TomlParseResult enabledParse = ParseSource(editor.Source);
                assignment = FindUniqueAssignment(ConfigTomlSyntaxIndex.Create(enabledParse), path);

                editor = enabledParse.Syntax.CreateEditor();
            }

            editor.ReplaceAssignmentValue(assignment.Node, valueSource);

            return editor.Source;
        }

        public static string SetOrInsertValue(string source, ConfigValuePath path, ConfigNode value)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (path.Segments.Count == 0)
                throw new ArgumentException("Value path must contain at least one segment.", nameof(path));

            TomlParseResult parsed = ParseSource(source);
            var index = ConfigTomlSyntaxIndex.Create(parsed);
            ConfigTomlSyntaxAssignment existing = TryFindUniqueAssignment(index, path);

            if (existing != null)
                return SetValue(source, path, value);

            if (value is ConfigNullNode)
                throw new NotSupportedException("A new null TOML field cannot be generated without a retained concrete value.");

            string lineEnding = GetLineEnding(source);
            string assignmentSource = RenderAssignmentSource(path.Segments[path.Segments.Count - 1], value, lineEnding);

            if (path.Segments.Count == 1)
                return InsertRootAssignment(source, parsed.Syntax, assignmentSource, lineEnding);

            ConfigValuePath parentPath = CreateParentPath(path);
            ConfigTomlSyntaxTable table = FindTable(index, parentPath);

            if (table == null)
                return AppendNewTable(source, parentPath, assignmentSource, lineEnding);
            
            if (!table.IsAddressable)
                throw new NotSupportedException("Cannot insert a ConfigAPI field inside an array-of-tables context.");

            return InsertIntoTable(source, parsed.Syntax, table, assignmentSource, lineEnding);

        }

        public static string RemoveValue(string source, ConfigValuePath path)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (path.Segments.Count == 0)
                throw new ArgumentException("Value path must contain at least one segment.", nameof(path));

            TomlParseResult parsed = ParseSource(source);
            ConfigTomlSyntaxAssignment assignment = FindUniqueAssignment(ConfigTomlSyntaxIndex.Create(parsed), path);

            int start = GetCompleteLineStart(source, assignment.Node);
            int end = GetCompleteLineEnd(source, assignment.Node);
            string edited = source.Remove(start, end - start);

            ParseSource(edited);
            return edited;
        }

        public static string SetOrInsertNullValue(string source, ConfigValuePath path, ConfigNode retainedConcreteValue)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (retainedConcreteValue == null)
                throw new ArgumentNullException(nameof(retainedConcreteValue));

            if (path.Segments.Count == 0)
                throw new ArgumentException("Value path must contain at least one segment.", nameof(path));

            if (retainedConcreteValue is ConfigNullNode)
                throw new ArgumentException("Retained null source value must be concrete.", nameof(retainedConcreteValue));

            TomlParseResult parsed = ParseSource(source);
            ConfigTomlSyntaxAssignment existing = TryFindUniqueAssignment(ConfigTomlSyntaxIndex.Create(parsed), path);

            if (existing != null)
                return SetValue(source, path, ConfigNullNode.Instance);

            string inserted = SetOrInsertValue(source, path, retainedConcreteValue);

            return SetValue(inserted, path, ConfigNullNode.Instance);
        }

        private static TomlParseResult ParseSource(string source)
        {
            TomlParseResult parsed = Toml.TryParse(source);

            if (parsed.IsSuccess && parsed.Syntax != null)
                return parsed;
            
            var message = "Source must be valid TOML.";

            if (parsed.Diagnostics.Count > 0)
                message += " " + parsed.Diagnostics[0];

            throw new ArgumentException(message, nameof(source));

        }

        private static ConfigTomlSyntaxAssignment FindUniqueAssignment(ConfigTomlSyntaxIndex index, ConfigValuePath path)
        {
            ConfigTomlSyntaxAssignment result = TryFindUniqueAssignment(index, path);

            if (result == null)
                throw new KeyNotFoundException("The ConfigAPI value path has no addressable TOML assignment.");

            return result;
        }

        private static ConfigTomlSyntaxAssignment TryFindUniqueAssignment(ConfigTomlSyntaxIndex index, ConfigValuePath path)
        {
            ConfigTomlSyntaxAssignment result = null;

            foreach (ConfigTomlSyntaxAssignment candidate in index.Assignments)
            {
                if (!candidate.Path.Equals(path))
                    continue;

                if (result != null)
                    throw new InvalidOperationException("Multiple TOML assignments map to the same ConfigAPI value path.");

                result = candidate;
            }

            return result;
        }

        private static ConfigTomlSyntaxTable FindTable(ConfigTomlSyntaxIndex index, ConfigValuePath path)
        {
            ConfigTomlSyntaxTable result = null;

            foreach (ConfigTomlSyntaxTable candidate in index.Tables)
            {
                if (!PathEquals(candidate.Path, path))
                    continue;

                if (!candidate.IsAddressable)
                    return candidate;

                if (result != null)
                    throw new InvalidOperationException("Multiple addressable TOML table headers map to the same ConfigAPI value path.");

                result = candidate;
            }

            return result;
        }

        private static bool PathEquals(IReadOnlyList<string> segments, ConfigValuePath path)
        {
            if (segments.Count != path.Segments.Count)
                return false;

            for (var i = 0; i < segments.Count; i++)
                if (!string.Equals(segments[i], path.Segments[i], StringComparison.Ordinal))
                    return false;

            return true;
        }

        private static ConfigValuePath CreateParentPath(ConfigValuePath path)
        {
            var segments =
                new string[path.Segments.Count - 1];

            for (var i = 0; i < segments.Length; i++)
                segments[i] = path.Segments[i];

            return new ConfigValuePath(segments);
        }

        private static string InsertRootAssignment(string source, TomlSyntaxDocument syntax, string assignmentSource, string lineEnding)
        {
            TomlSyntaxNode firstHeader = null;
            TomlSyntaxNode lastRootAssignment = null;

            foreach (TomlSyntaxNode node in syntax.Nodes)
            {
                if (node.Kind == TomlSyntaxNodeKind.TableHeader || node.Kind == TomlSyntaxNodeKind.ArrayTableHeader)
                {
                    firstHeader = node;
                    break;
                }

                if (node.Kind == TomlSyntaxNodeKind.Assignment || node.Kind == TomlSyntaxNodeKind.DisabledAssignment)
                    lastRootAssignment = node;
            }

            if (lastRootAssignment != null)
            {
                int offset = GetCompleteLineEnd(source, lastRootAssignment);
                string fragment = PrefixIfNeeded(source, offset, assignmentSource, lineEnding);
                return InsertValidated(source, offset, fragment);
            }

            if (firstHeader != null)
                return InsertValidated(source, 0, assignmentSource);

            string endFragment = PrefixIfNeeded(source, source.Length, assignmentSource, lineEnding);
            return InsertValidated(source, source.Length, endFragment);
        }

        private static string InsertIntoTable(string source, TomlSyntaxDocument syntax, ConfigTomlSyntaxTable table,
                                              string assignmentSource, string lineEnding)
        {
            int headerIndex = FindOwnedNodeIndex(syntax, table.Node);

            TomlSyntaxNode anchor = table.Node;

            for (int i = headerIndex + 1; i < syntax.Nodes.Count; i++)
            {
                TomlSyntaxNode node = syntax.Nodes[i];

                if (node.Kind == TomlSyntaxNodeKind.TableHeader || node.Kind == TomlSyntaxNodeKind.ArrayTableHeader)
                    break;

                if (node.Kind == TomlSyntaxNodeKind.Assignment || node.Kind == TomlSyntaxNodeKind.DisabledAssignment)
                    anchor = node;
            }

            int offset = GetCompleteLineEnd(source, anchor);
            string fragment = PrefixIfNeeded(source, offset, assignmentSource, lineEnding);

            return InsertValidated(source, offset, fragment);
        }

        private static int FindOwnedNodeIndex(TomlSyntaxDocument syntax, TomlSyntaxNode node)
        {
            for (var i = 0; i < syntax.Nodes.Count; i++)
                if (ReferenceEquals(syntax.Nodes[i], node))
                    return i;

            throw new InvalidOperationException("TOML syntax table node is not owned by the current syntax document.");
        }

        private static int GetCompleteLineStart(string source, TomlSyntaxNode node)
        {
            int offset = node.Span.Start;

            while (offset > 0 && source[offset - 1] != '\r' && source[offset - 1] != '\n')
                offset--;

            return offset;
        }

        private static int GetCompleteLineEnd(string source, TomlSyntaxNode node)
        {
            int offset = node.Span.End;

            while (offset < source.Length && source[offset] != '\r' && source[offset] != '\n')
                offset++;

            if (offset >= source.Length)
                return offset;

            if (source[offset] != '\r')
                return offset + 1;
            
            if (offset + 1 >= source.Length || source[offset + 1] != '\n')
                throw new InvalidOperationException("Valid TOML source unexpectedly contains a lone CR newline.");

            return offset + 2;
        }

        private static string PrefixIfNeeded(string source, int offset, string fragment, string lineEnding)
        {
            if (offset == 0)
                return fragment;

            if (source[offset - 1] == '\n')
                return fragment;

            return lineEnding + fragment;
        }

        private static string AppendNewTable(string source, ConfigValuePath parentPath, string assignmentSource, string lineEnding)
        {
            string headerSource = RenderTableHeaderSource(parentPath);

            string section = headerSource + lineEnding + assignmentSource;

            string fragment;

            if (source.Length == 0)
                fragment = section;
            else if (source[source.Length - 1] == '\n')
                fragment = lineEnding + section;
            else
                fragment = lineEnding + lineEnding + section;

            return InsertValidated(source, source.Length, fragment);
        }

        private static string InsertValidated(string source, int offset, string fragment)
        {
            string edited = source.Insert(offset, fragment);

            TomlParseResult parsed = Toml.TryParse(edited);

            if (parsed.IsSuccess)
                return edited;
            
            var message = "The source-preserving TOML insertion would make the document invalid.";

            if (parsed.Diagnostics.Count > 0)
                message += " " + parsed.Diagnostics[0];

            throw new NotSupportedException(message);
        }

        private static string GetLineEnding(string source)
        {
            int newline = source.IndexOf('\n');

            if (newline > 0 && source[newline - 1] == '\r')
                return "\r\n";

            return "\n";
        }

        private static string RenderAssignmentSource(string key, ConfigNode value, string lineEnding)
        {
            var wrapper = new ConfigDocument(new ConfigObjectNode(new ConfigObjectEntry(key, ConfigScalarNode.Integer(0))));

            string source = Toml.Write(ConfigTomlDocumentCodec.ToTomlDocument(wrapper));

            TomlParseResult parsed = Toml.TryParse(source);

            if (!parsed.IsSuccess || parsed.Syntax == null)
                throw new InvalidOperationException("Generated TOML key wrapper did not parse successfully.");

            TomlSyntaxNode assignment = null;

            foreach (TomlSyntaxNode t in parsed.Syntax.Nodes)
            {
                if (t.Kind != TomlSyntaxNodeKind.Assignment)
                    continue;

                assignment = t;
                break;
            }

            if (assignment?.ValueSpan == null)
                throw new InvalidOperationException("Generated TOML key wrapper has no assignment value span.");

            TomlSourceSpan valueSpan = assignment.ValueSpan.Value;
            int prefixLength = valueSpan.Start - assignment.Span.Start;

            string prefix = source.Substring(assignment.Span.Start, prefixLength);

            return prefix + RenderValueSource(value) + lineEnding; 
        }

        private static string RenderTableHeaderSource(ConfigValuePath path)
        {
            ConfigNode current = new ConfigObjectNode(new ConfigObjectEntry(HeaderProbeKey, ConfigScalarNode.Integer(0)));

            for (int i = path.Segments.Count - 1; i >= 0; i--)
                current = new ConfigObjectNode(new ConfigObjectEntry(path.Segments[i], current));

            var document = new ConfigDocument((ConfigObjectNode)current);

            string source = Toml.Write(ConfigTomlDocumentCodec.ToTomlDocument(document));

            TomlParseResult parsed = Toml.TryParse(source);

            if (!parsed.IsSuccess || parsed.Syntax == null)
                throw new InvalidOperationException("Generated TOML table-header wrapper did not parse successfully.");

            TomlSyntaxNode header = null;

            foreach (TomlSyntaxNode node in parsed.Syntax.Nodes)
                if (node.Kind == TomlSyntaxNodeKind.TableHeader)
                    header = node;

            if (header == null)
                throw new InvalidOperationException("Generated TOML table-header wrapper has no table header.");

            return source.Substring(header.Span.Start, header.Span.Length);
        }

        private static string RenderValueSource(ConfigNode value)
        {
            var wrapper = new ConfigDocument(new ConfigObjectNode(new ConfigObjectEntry(ValueWrapperKey, new ConfigArrayNode(value))));

            string source = Toml.Write(ConfigTomlDocumentCodec.ToTomlDocument(wrapper));

            TomlParseResult parsed = Toml.TryParse(source);

            if (!parsed.IsSuccess || parsed.Syntax == null)
                throw new InvalidOperationException("Generated TOML value wrapper did not parse successfully.");

            TomlSyntaxNode assignment = null;

            foreach (TomlSyntaxNode node in parsed.Syntax.Nodes)
            {
                if (node.Kind != TomlSyntaxNodeKind.Assignment)
                    continue;

                assignment = node;
                break;
            }

            if (assignment?.ValueSpan == null)
                throw new InvalidOperationException("Generated TOML value wrapper has no assignment value span.");

            TomlSourceSpan span = assignment.ValueSpan.Value;
            string arraySource = source.Substring(span.Start, span.Length);

            if (arraySource.Length < 2 || arraySource[0] != '[' || arraySource[arraySource.Length - 1] != ']')
                throw new InvalidOperationException("Generated TOML value wrapper is not an array.");

            return arraySource.Substring(1, arraySource.Length - 2);
        }
    }
}