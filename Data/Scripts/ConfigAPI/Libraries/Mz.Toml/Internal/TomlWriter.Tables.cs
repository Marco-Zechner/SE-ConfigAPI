using System;
using System.Collections.Generic;
using System.Text;

namespace Mz.Toml.Internal
{
    internal static partial class TomlWriter
    {
        private static bool AppendValueEntries(StringBuilder sb, TomlTable table)
        {
            var wroteAny = false;

            foreach (var pair in table)
            {
                switch (pair.Value.Kind)
                {
                    case TomlNodeKind.Table:
                    {
                        var child = (TomlTable)pair.Value;

                        if (child.DefinitionKind != TomlTableDefinitionKind.Inline)
                            continue;
                        break;
                    }
                    case TomlNodeKind.Array:
                    {
                        var array = (TomlArray)pair.Value;

                        if (array.DefinitionKind == TomlArrayDefinitionKind.ArrayOfTables)
                            continue;
                        break;
                    }
                }

                AppendKey(sb, pair.Key);
                sb.Append(" = ");
                AppendNode(sb, pair.Value);
                sb.Append('\n');

                wroteAny = true;
            }

            return wroteAny;
        }

        private static void AppendChildTables(StringBuilder sb, TomlTable table, List<string> path, ref bool wroteAnything)
        {
            foreach (var pair in table)
            {
                if (pair.Value.Kind == TomlNodeKind.Table)
                {
                    var child = (TomlTable)pair.Value;

                    if (child.DefinitionKind == TomlTableDefinitionKind.Inline)
                        continue;

                    path.Add(pair.Key);

                    if (wroteAnything)
                        sb.Append('\n');

                    AppendHeader(sb, path);
                    sb.Append('\n');
                    AppendValueEntries(sb, child);

                    wroteAnything = true;

                    AppendChildTables(sb, child, path, ref wroteAnything);
                    path.RemoveAt(path.Count - 1);

                    continue;
                }

                if (pair.Value.Kind != TomlNodeKind.Array)
                    continue;

                var array = (TomlArray)pair.Value;

                if (array.DefinitionKind != TomlArrayDefinitionKind.ArrayOfTables)
                    continue;

                path.Add(pair.Key);

                foreach (var node in array)
                {
                    if (node.Kind != TomlNodeKind.Table)
                        throw new InvalidOperationException("An array-of-tables node contains a non-table element.");

                    var element = (TomlTable)node;

                    if (wroteAnything)
                        sb.Append('\n');

                    AppendArrayTableHeader(sb, path);
                    sb.Append('\n');
                    AppendValueEntries(sb, element);

                    wroteAnything = true;

                    AppendChildTables(sb, element, path, ref wroteAnything);
                }

                path.RemoveAt(path.Count - 1);
            }
        }

        private static void AppendArrayTableHeader(StringBuilder sb, IList<string> path)
        {
            sb.Append("[[");

            for (var i = 0; i < path.Count; i++)
            {
                if (i > 0)
                    sb.Append('.');

                AppendKey(sb, path[i]);
            }

            sb.Append("]]");
        }

        private static void AppendHeader(StringBuilder sb, IList<string> path)
        {
            sb.Append('[');

            for (var i = 0; i < path.Count; i++)
            {
                if (i > 0)
                    sb.Append('.');

                AppendKey(sb, path[i]);
            }

            sb.Append(']');
        }

        private static void AppendKey(StringBuilder sb, string key)
        {
            if (IsBareKey(key))
            {
                sb.Append(key);
                return;
            }

            AppendBasicString(sb, key);
        }
    }
}
