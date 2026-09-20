using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Domain;
using Mz.Toml;

namespace MarcoZechner.ConfigAPI.V2.Serialization
{
    public sealed class ConfigTomlSyntaxAssignment
    {
        public ConfigValuePath Path { get; }
        public TomlSyntaxNode Node { get; }

        public bool IsDisabled => Node.Kind == TomlSyntaxNodeKind.DisabledAssignment;

        internal ConfigTomlSyntaxAssignment(ConfigValuePath path, TomlSyntaxNode node)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (node == null)
                throw new ArgumentNullException(nameof(node));

            Path = path;
            Node = node;
        }
    }

    internal sealed class ConfigTomlSyntaxTable
    {
        public IReadOnlyList<string> Path { get; }

        public TomlSyntaxNode Node { get; }
        public bool IsAddressable { get; }

        public ConfigTomlSyntaxTable(string[] path, TomlSyntaxNode node, bool isAddressable)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (node == null)
                throw new ArgumentNullException(nameof(node));

            var pathCopy = new string[path.Length];
            Array.Copy(path, pathCopy, path.Length);
            Path = Array.AsReadOnly(pathCopy);

            Node = node;
            IsAddressable = isAddressable;
        }
    }

    public sealed class ConfigTomlSyntaxIndex
    {
        private const string ProbeKey = "__configapi_path_probe__";
        private const long ProbeValue = 584321;

        public IReadOnlyList<ConfigTomlSyntaxAssignment> Assignments { get; }

        public IReadOnlyList<TomlSyntaxNode> UnaddressableAssignments { get; }

        internal IReadOnlyList<ConfigTomlSyntaxTable> Tables { get; }

        private ConfigTomlSyntaxIndex(IList<ConfigTomlSyntaxAssignment> assignments,
                                      IList<TomlSyntaxNode> unaddressableAssignments,
                                      IList<ConfigTomlSyntaxTable> tables)
        {
            var assignmentsCopy = new ConfigTomlSyntaxAssignment[assignments.Count];
            for (var i = 0; i < assignments.Count; i++)
                assignmentsCopy[i] = assignments[i];

            Assignments = Array.AsReadOnly(assignmentsCopy);

            var unaddressableAssignmentsCopy = new TomlSyntaxNode[unaddressableAssignments.Count];
            for (var i = 0; i < unaddressableAssignments.Count; i++)
                unaddressableAssignmentsCopy[i] = unaddressableAssignments[i];

            UnaddressableAssignments = Array.AsReadOnly(unaddressableAssignmentsCopy);

            var tablesCopy = new ConfigTomlSyntaxTable[tables.Count];
            for (var i = 0; i < tables.Count; i++)
                tablesCopy[i] = tables[i];

            Tables = Array.AsReadOnly(tablesCopy);
        }

        public static ConfigTomlSyntaxIndex Create(TomlParseResult parseResult)
        {
            if (parseResult == null)
                throw new ArgumentNullException(nameof(parseResult));

            if (!parseResult.IsSuccess || parseResult.Syntax == null)
                throw new ArgumentException("A successful TOML parse with source syntax is required.", nameof(parseResult));

            var assignments = new List<ConfigTomlSyntaxAssignment>();
            var unaddressableAssignments = new List<TomlSyntaxNode>();
            var tables = new List<ConfigTomlSyntaxTable>();
            var arrayTablePaths = new List<string[]>();

            string[] currentTablePath = Array.Empty<string>();
            var currentTableTraversesArray = false;
            TomlSyntaxDocument syntax = parseResult.Syntax;

            foreach (TomlSyntaxNode node in syntax.Nodes)
            {
                switch (node.Kind)
                {
                    case TomlSyntaxNodeKind.TableHeader:
                        currentTablePath = ReadHeaderPath(syntax, node);
                        currentTableTraversesArray = TraversesArrayTable(currentTablePath, arrayTablePaths);

                        tables.Add(new ConfigTomlSyntaxTable(currentTablePath, node, !currentTableTraversesArray));

                        break;

                    case TomlSyntaxNodeKind.ArrayTableHeader:
                        currentTablePath = ReadHeaderPath(syntax, node);
                        arrayTablePaths.Add(Copy(currentTablePath));
                        currentTableTraversesArray = true;

                        tables.Add(new ConfigTomlSyntaxTable(currentTablePath, node, false));

                        break;

                    case TomlSyntaxNodeKind.Assignment:
                    case TomlSyntaxNodeKind.DisabledAssignment:
                        if (currentTableTraversesArray)
                        {
                            unaddressableAssignments.Add(node);
                            break;
                        }

                        string[] localPath = ReadAssignmentPath(syntax, node);
                        string[] completePath = Combine(currentTablePath, localPath);

                        assignments.Add(new ConfigTomlSyntaxAssignment(CreateConfigValuePath(completePath), node));
                        break;
                }
            }

            return new ConfigTomlSyntaxIndex(assignments, unaddressableAssignments, tables);
        }

        private static ConfigValuePath CreateConfigValuePath(string[] segments)
        {
            try
            {
                return new ConfigValuePath(segments);
            }
            catch (ArgumentException exception)
            {
                throw new NotSupportedException("The TOML assignment path contains a segment that ConfigValuePath cannot represent.", exception);
            }
        }

        private static string[] ReadAssignmentPath(TomlSyntaxDocument syntax, TomlSyntaxNode node)
        {
            if (!node.ValueSpan.HasValue)
                throw new InvalidOperationException("Assignment syntax node has no value span.");

            string statement = syntax.Source.Substring(node.Span.Start, node.Span.Length);
            TomlSourceSpan valueSpan = node.ValueSpan.Value;
            int relativeValueStart = valueSpan.Start - node.Span.Start;

            statement = statement.Remove(relativeValueStart, valueSpan.Length).Insert(relativeValueStart, "0");

            if (node.Kind != TomlSyntaxNodeKind.DisabledAssignment)
                return ReadSingleAssignmentPath(Toml.Parse(statement).Root);
            
            if (!statement.StartsWith("#!", StringComparison.Ordinal))
                throw new InvalidOperationException("Disabled assignment is missing the expected '#!' marker.");

            statement = statement.Remove(0, 2);

            return ReadSingleAssignmentPath(Toml.Parse(statement).Root);
        }

        private static string[] ReadSingleAssignmentPath(TomlTable root)
        {
            var path = new List<string>();
            TomlTable table = root;

            while (true)
            {
                if (table.Count != 1)
                    throw new InvalidOperationException("Synthetic TOML assignment did not produce exactly one path.");

                string key = table.Keys[0];
                TomlNode node = table[key];

                path.Add(key);

                if (node.Kind == TomlNodeKind.Value)
                    return path.ToArray();

                if (node.Kind != TomlNodeKind.Table)
                    throw new InvalidOperationException("Synthetic TOML assignment produced an unexpected node kind.");

                table = (TomlTable)node;
            }
        }

        private static string[] ReadHeaderPath(TomlSyntaxDocument syntax, TomlSyntaxNode node)
        {
            string statement = syntax.Source.Substring(node.Span.Start, node.Span.Length);
            string synthetic = statement + "\n" + ProbeKey + " = " + ProbeValue + "\n";
            TomlDocument parsed = Toml.Parse(synthetic);

            string[] path;
            if (!TryFindProbePath(parsed.Root, new List<string>(), out path))
                throw new InvalidOperationException("Synthetic TOML header did not resolve the probe path.");

            return path;
        }

        private static bool TryFindProbePath(TomlTable table, List<string> path, out string[] result)
        {
            foreach (var pair in table)
            {
                var value = pair.Value as TomlValue;

                if (string.Equals(pair.Key, ProbeKey, StringComparison.Ordinal) && value != null && 
                    value.ValueKind == TomlValueKind.Integer && value.AsInteger() == ProbeValue)
                {
                    result = path.ToArray();
                    return true;
                }

                if (pair.Value.Kind == TomlNodeKind.Table)
                {
                    path.Add(pair.Key);

                    if (TryFindProbePath((TomlTable)pair.Value, path, out result))
                        return true;

                    path.RemoveAt(path.Count - 1);
                    continue;
                }

                if (pair.Value.Kind != TomlNodeKind.Array)
                    continue;

                path.Add(pair.Key);

                var array = (TomlArray)pair.Value;
                foreach (TomlNode item in array)
                {
                    var element = item as TomlTable;
                    if (element != null && TryFindProbePath(element, path, out result))
                        return true;
                }

                path.RemoveAt(path.Count - 1);
            }

            result = null;
            return false;
        }

        private static bool TraversesArrayTable(string[] path, IList<string[]> arrayTablePaths)
        {
            foreach (string[] tablePath in arrayTablePaths)
                if (IsPrefix(tablePath, path))
                    return true;

            return false;
        }

        private static bool IsPrefix(string[] prefix, string[] value)
        {
            if (prefix.Length > value.Length)
                return false;

            for (var i = 0; i < prefix.Length; i++)
                if (!string.Equals(prefix[i], value[i], StringComparison.Ordinal))
                    return false;

            return true;
        }

        private static string[] Combine(string[] first, string[] second)
        {
            var result = new string[first.Length + second.Length];

            Array.Copy(first, 0, result, 0, first.Length);
            Array.Copy(second, 0, result, first.Length, second.Length);

            return result;
        }

        private static string[] Copy(string[] source)
        {
            var result = new string[source.Length];
            Array.Copy(source, result, source.Length);
            return result;
        }
    }
}
