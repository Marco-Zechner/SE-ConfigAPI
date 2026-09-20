using System.Collections.Generic;
using System.Text;

namespace Mz.Toml.Internal
{
    internal static partial class TomlWriter
    {
        public static string Write(TomlDocument document)
        {
            ValidateAcyclicGraph(document.Root);

            var sb = new StringBuilder();
            var wroteAnything = AppendValueEntries(sb, document.Root);
            var path = new List<string>();

            AppendChildTables(sb, document.Root, path, ref wroteAnything);

            return sb.ToString();
        }
    }
}
