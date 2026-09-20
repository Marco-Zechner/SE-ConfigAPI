using System;
using System.Collections.Generic;

namespace Mz.Toml.Internal
{
    internal static partial class TomlWriter
    {
        private static void ValidateAcyclicGraph(TomlNode root)
        {
            var activePath = new List<TomlNode>();
            ValidateAcyclicNode(root, activePath);
        }

        private static void ValidateAcyclicNode(TomlNode node, List<TomlNode> activePath)
        {
            foreach (var t in activePath)
                if (ReferenceEquals(t, node))
                    throw new InvalidOperationException("Cannot write a TOML document containing a cyclic node graph.");

            activePath.Add(node);

            switch (node.Kind)
            {
                case TomlNodeKind.Value:
                    break;

                case TomlNodeKind.Array:
                {
                    var array = (TomlArray)node;

                    foreach (var arrayItem in array)
                        ValidateAcyclicNode(arrayItem, activePath);

                    break;
                }

                case TomlNodeKind.Table:
                {
                    var table = (TomlTable)node;

                    foreach (var pair in table) 
                        ValidateAcyclicNode(pair.Value, activePath);

                    break;
                }

                default:
                    throw new InvalidOperationException(
                        "Unsupported TOML node kind: " +
                        node.Kind);
            }

            activePath.RemoveAt(activePath.Count - 1);
        }
    }
}
