using System;
using System.Collections.Generic;
using System.Linq;
using MarcoZechner.ConfigAPI.Domain;
using Mz.Toml;

namespace MarcoZechner.ConfigAPI.Serialization
{
    public static class ConfigTomlSourceDecoder
    {
        private const string ValueWrapperKey = "__configapi_disabled_value__";

        public static ConfigDocument Decode(string source, ConfigDocument currentDefaults) 
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (currentDefaults == null)
                throw new ArgumentNullException(nameof(currentDefaults));

            TomlParseResult parsed = Toml.TryParse(source);

            if (!parsed.IsSuccess || parsed.Syntax == null)
            {
                var message = "Source must be valid TOML.";

                if (parsed.Diagnostics.Count > 0)
                    message += " " + parsed.Diagnostics[0];

                throw new ArgumentException(message, nameof(source));
            }

            var index = ConfigTomlSyntaxIndex.Create(parsed);

            RejectUnaddressableDisabledAssignments(index);

            ConfigDocument decoded = ConfigTomlDocumentCodec.FromTomlDocument(parsed.Document);
            
            foreach (ConfigTomlSyntaxAssignment assignment in index.Assignments)
            {
                if (!assignment.IsDisabled)
                    continue;

                ConfigNode currentDefault;
                if (!currentDefaults.TryGet(assignment.Path, out currentDefault))
                    continue;

                EnsureUniqueKnownAssignment(index, assignment.Path);

                ConfigNode preservedValue = DecodePreservedValue(parsed.Syntax, assignment.Node);

                if (!IsSchemaCompatible(currentDefault, preservedValue))
                    throw new NotSupportedException("Disabled TOML assignment value is incompatible with the current ConfigAPI field schema.");

                decoded = new ConfigDocument(SetOrAddValue(decoded.Root, assignment.Path, 0, ConfigNullNode.Instance));
            }

            return decoded;
        }

        private static void RejectUnaddressableDisabledAssignments(ConfigTomlSyntaxIndex index)
        {
            if (index.UnaddressableAssignments.Any(t => t.Kind == TomlSyntaxNodeKind.DisabledAssignment))
                throw new NotSupportedException("Disabled TOML assignments inside array-of-tables contexts cannot be mapped to ConfigValuePath.");
        }

        private static void EnsureUniqueKnownAssignment(ConfigTomlSyntaxIndex index, ConfigValuePath path)
        {
            var count = 0;

            foreach (ConfigTomlSyntaxAssignment assignment in index.Assignments)
            {
                if (!assignment.Path.Equals(path))
                    continue;

                count++;

                if (count > 1)
                    throw new InvalidOperationException("Multiple TOML assignments map to the same known ConfigAPI value path.");
            }
        }

        private static ConfigNode DecodePreservedValue(TomlSyntaxDocument syntax, TomlSyntaxNode node)
        {
            if (!node.ValueSpan.HasValue)
                throw new InvalidOperationException("Disabled TOML assignment has no value span.");

            TomlSourceSpan span = node.ValueSpan.Value;
            string valueSource = syntax.Source.Substring(span.Start, span.Length);

            var wrapperSource = $"{ValueWrapperKey} = {valueSource}\n";

            TomlParseResult parsed = Toml.TryParse(wrapperSource);

            if (!parsed.IsSuccess)
                throw new InvalidOperationException("Preserved disabled TOML value did not parse independently.");

            ConfigDocument document = ConfigTomlDocumentCodec.FromTomlDocument(parsed.Document);

            ConfigNode value;
            if (!document.TryGet(new ConfigValuePath(ValueWrapperKey), out value))
                throw new InvalidOperationException("Preserved disabled TOML value wrapper did not contain its value.");

            return value;
        }

        private static bool IsSchemaCompatible(ConfigNode currentDefault, ConfigNode preservedValue)
        {
            if (currentDefault is ConfigNullNode)
                return true;

            if (currentDefault is ConfigObjectNode)
                return preservedValue is ConfigObjectNode;

            if (currentDefault is ConfigArrayNode)
                return preservedValue is ConfigArrayNode;

            var currentScalar = currentDefault as ConfigScalarNode;
            var preservedScalar = preservedValue as ConfigScalarNode;

            return currentScalar != null && preservedScalar != null && currentScalar.Kind == preservedScalar.Kind;
        }

        private static ConfigObjectNode SetOrAddValue(ConfigObjectNode current, ConfigValuePath path, int segmentIndex, ConfigNode value)
        {
            if (segmentIndex >= path.Segments.Count)
                throw new ArgumentException("Value path must contain at least one segment.", nameof(path));

            string segment = path.Segments[segmentIndex];

            if (segmentIndex == path.Segments.Count - 1)
                return ReplaceOrAppend(current, segment, value);

            ConfigNode existing;
            ConfigObjectNode child;

            if (current.TryGet(segment, out existing))
            {
                child = existing as ConfigObjectNode;

                if (child == null)
                    throw new NotSupportedException("Disabled TOML assignment path traverses an active non-object value.");
            }
            else
                child = new ConfigObjectNode();

            ConfigObjectNode replacement = SetOrAddValue(child, path, segmentIndex + 1, value);

            return ReplaceOrAppend(current, segment, replacement);
        }

        private static ConfigObjectNode ReplaceOrAppend(ConfigObjectNode current, string name, ConfigNode value)
        {
            var entries = new List<ConfigObjectEntry>(current.Entries.Count + 1);

            var replaced = false;

            foreach (ConfigObjectEntry entry in current.Entries)
            {
                if (string.Equals(entry.Name, name, StringComparison.Ordinal))
                {
                    entries.Add(new ConfigObjectEntry(entry.Name, value));
                    replaced = true;
                }
                else
                    entries.Add(entry);
            }

            if (!replaced)
                entries.Add(new ConfigObjectEntry(name, value));

            return new ConfigObjectNode(entries.ToArray());
        }
    }
}