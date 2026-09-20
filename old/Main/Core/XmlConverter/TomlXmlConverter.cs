using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MarcoZechner.ConfigAPI.Main.Domain;

namespace MarcoZechner.ConfigAPI.Main.Core.XmlConverter
{
    /// <summary>
    /// Pure XML &lt;-&gt; TOML converter for a single config instance.
    /// No defaults, no migration, no version logic.
    ///
    /// Rules:
    /// - Scalars: &lt;Key&gt;value&lt;/Key&gt;                  &lt;=&gt;  Key = value
    /// - Arrays:  &lt;IntList&gt;&lt;int&gt;1&lt;/int&gt;...&lt;/IntList&gt; &lt;=&gt; IntList = [1, 2, 3]
    /// - Nested:  &lt;Nested&gt;&lt;Threshold&gt;...&lt;/Threshold&gt;  &lt;=&gt; Nested.Threshold = ...
    /// - Dictionary&lt;string,int&gt; (SerializableDictionary):
    ///       &lt;NamedValues&gt;&lt;dictionary&gt;&lt;item&gt;...&lt;/item&gt;...&lt;/dictionary&gt;&lt;/NamedValues&gt;
    ///       &lt;=&gt;
    ///       NamedValues.start = 1
    ///       NamedValues.end   = 99
    ///
    /// Nullable values:
    ///   &lt;OptionalInt xsi:nil="true" /&gt;  &lt;=&gt; OptionalInt = null
    /// </summary>
    public sealed class TomlXmlConverter : IXmlConverter
    {
        private const string NULL_SENTINEL = "null";

        public string GetExtension => ".toml";

        // ====================================================================
        // Public API
        // ====================================================================

        public string ToExternal(IConfigDefinitionMain definitionMain, string xmlContent, bool includeDescriptions)
        {
            if (definitionMain == null)
                throw new ArgumentNullException(nameof(definitionMain));
            if (xmlContent == null)
                xmlContent = string.Empty;

            string rootName;
            var rootChildren = ParseRootChildren(xmlContent, out rootName);

            // Default instance for descriptions (comments)
            var descriptions = includeDescriptions ? definitionMain.GetVariableDescriptions() : null;


            // Main scalars/lists BEFORE the first nested parent
            var flatPre = new Dictionary<string, List<string>>();
            // Main scalars/lists AFTER the first nested parent
            var flatPost = new Dictionary<string, List<string>>();

            // Dictionary sections: parentName -> (dictKey -> value)
            var dictSections = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

            // Nested sections: parentPath -> (childName -> list of values)
            var nestedSections = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);

            var seenNested = false;

            // Preserve root property order
            var rootList = new List<KeyValuePair<string, string>>(rootChildren);

            foreach (var kv in rootList)
            {
                var propName = kv.Key;
                var innerXml = kv.Value;
                var trimmed = innerXml.Trim();

                // Decide which flat map this property belongs to (pre or post nested)
                var targetFlat = seenNested ? flatPost : flatPre;

                if (trimmed.Length == 0 || trimmed.IndexOf('<') < 0)
                {
                    // Simple scalar text (including NullSentinel from self-closing elements)
                    AddFlatValue(targetFlat, propName, trimmed);
                    continue;
                }

                // Try to parse first-level children inside this snippet.
                var children = ParseSnippetChildren(trimmed);

                if (children.Count == 0)
                {
                    // Fallback: treat as scalar blob
                    AddFlatValue(targetFlat, propName, trimmed);
                    continue;
                }

                // Special-case SerializableDictionary pattern:
                //   <PropName>
                //     <dictionary>
                //       <item><Key>k</Key><Value>v</Value></item>...
                //     </dictionary>
                //   </PropName>
                if (children.Count == 1 &&
                    string.Equals(children[0].Name, "dictionary", StringComparison.OrdinalIgnoreCase))
                {
                    Dictionary<string, string> dict;
                    if (!dictSections.TryGetValue(propName, out dict))
                    {
                        dict = new Dictionary<string, string>(StringComparer.Ordinal);
                        dictSections[propName] = dict;
                    }

                    var dictItems = ParseSnippetChildren(children[0].Value);
                    foreach (var item in dictItems)
                    {
                        if (!string.Equals(item.Name, "item", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var kvChildren = ParseSnippetChildren(item.Value);
                        string keyText = null;
                        var valueText = string.Empty;

                        foreach (var child in kvChildren)
                        {
                            if (string.Equals(child.Name, "Key", StringComparison.OrdinalIgnoreCase))
                            {
                                keyText = child.Value.Trim();
                            }
                            else if (string.Equals(child.Name, "Value", StringComparison.OrdinalIgnoreCase))
                            {
                                valueText = child.Value.Trim();
                            }
                        }

                        if (!string.IsNullOrEmpty(keyText))
                        {
                            dict[keyText] = valueText;
                        }
                    }

                    // Dictionary handled
                    continue;
                }

                // Array-of-primitive or nested object at root level
                var sameName = true;
                var firstName = children[0].Name;
                for (var i = 1; i < children.Count; i++)
                {
                    if (children[i].Name != firstName)
                    {
                        sameName = false;
                        break;
                    }
                }

                if (sameName)
                {
                    // Root array-of-primitive: IntList.int, StringList.string, etc.
                    var elementTag = firstName;                  // "int", "string", ...
                    var flatKey = propName + "." + elementTag;   // e.g. "IntList.int"

                    for (var i = 0; i < children.Count; i++)
                    {
                        AddFlatValue(targetFlat, flatKey, children[i].Value.Trim());
                    }
                }
                else
                {
                    // Nested object at root: e.g. Nested.{Threshold,Flag} or Settings.{Display,Network}
                    seenNested = true;

                    foreach (var child in children)
                    {
                        var val = child.Value.Trim();

                        // If this child is null or pure text (no inner tags), treat as a nested scalar:
                        // e.g. Settings.Network = null
                        if (val.IndexOf('<') < 0 || string.Equals(val, NULL_SENTINEL, StringComparison.Ordinal))
                        {
                            AddNestedValue(nestedSections, propName, child.Name, val);
                        }
                        else
                        {
                            // This child itself has nested content: recurse.
                            // e.g. parentPath = "Settings.Display" -> Width, Height, Theme, Dpi
                            var childPath = propName + "." + child.Name;
                            FlattenNestedXml(nestedSections, childPath, val);
                        }
                    }
                }
            }

            var sb = new StringBuilder();

            // Main section header: [TypeName]
            sb.Append('[')
              .Append(definitionMain.TypeName)
              .Append(']')
              .AppendLine();

            // Main fields before nested
            WriteFlatSection(sb, flatPre, descriptions);

            // Dictionary sections:
            // [TypeName.NamedValues-dictionary]
            // "start" = 1
            // "end"   = 99
            foreach (var section in dictSections)
            {
                var parentName = section.Key;
                var dict = section.Value;

                if (dict == null || dict.Count == 0)
                    continue;

                sb.AppendLine();
                sb.Append('[')
                  .Append(definitionMain.TypeName)
                  .Append('.')
                  .Append(parentName)
                  .Append("-dictionary]")
                  .AppendLine();

                foreach (var entry in dict)
                {
                    var k = entry.Key;
                    var v = entry.Value;

                    sb.Append('"')
                      .Append(k)
                      .Append('"')
                      .Append(" = ")
                      .Append(ToTomlLiteral(v))
                      .AppendLine();
                }
            }

            // Nested sections:
            // [TypeName.Nested]
            // Threshold = 0.9
            // Flag      = true
            //
            // [TypeName.Settings.Display]
            // Width  = 1920
            // Height = 1080
            // Theme  = "Dark"
            // Dpi    = null
            //
            // [TypeName.Settings]
            // Network = null
            foreach (var nested in nestedSections)
            {
                var parentPath = nested.Key;
                var childMap = nested.Value;
                if (childMap == null || childMap.Count == 0)
                    continue;

                sb.AppendLine();
                sb.Append('[')
                  .Append(definitionMain.TypeName)
                  .Append('.')
                  .Append(parentPath)
                  .Append(']')
                  .AppendLine();

                foreach (var kvChild in childMap)
                {
                    var key = kvChild.Key;
                    var list = kvChild.Value;
                    if (list == null || list.Count == 0)
                        continue;

                    if (list.Count == 1)
                    {
                        var literal = ToTomlLiteral(list[0]);
                        sb.Append(key)
                          .Append(" = ")
                          .Append(literal)
                          .AppendLine();
                    }
                    else
                    {
                        sb.Append(key)
                          .Append(" = [");

                        for (var i = 0; i < list.Count; i++)
                        {
                            if (i > 0)
                                sb.Append(", ");

                            var literal = ToTomlLiteral(list[i]);
                            sb.Append(literal);
                        }

                        sb.Append(']')
                          .AppendLine();
                    }
                }
            }

            // Trailing scalars/lists after nested -> reopen [TypeName]
            if (flatPost.Count > 0)
            {
                sb.AppendLine();
                sb.Append('[')
                  .Append(definitionMain.TypeName)
                  .Append(']')
                  .AppendLine();

                WriteFlatSection(sb, flatPost, descriptions);
            }

            return sb.ToString();
        }

        private static void FlattenNestedXml(
            Dictionary<string, Dictionary<string, List<string>>> nestedSections,
            string parentPath,
            string snippet)
        {
            if (string.IsNullOrEmpty(snippet))
                return;

            var children = ParseSnippetChildren(snippet.Trim());
            if (children.Count == 0)
            {
                return;
            }

            var sameName = true;
            var firstName = children[0].Name;
            for (var i = 1; i < children.Count; i++)
            {
                if (children[i].Name != firstName)
                {
                    sameName = false;
                    break;
                }
            }

            if (sameName)
            {
                // Array-of-primitive under a nested object:
                // we store it as key = firstName with multiple values, so it becomes:
                // [TypeName.ParentPath]
                // int = [1, 2, 3]
                for (var i = 0; i < children.Count; i++)
                {
                    AddNestedValue(nestedSections, parentPath, firstName, children[i].Value.Trim());
                }
            }
            else
            {
                // Normal nested object: multiple different children
                foreach (var child in children)
                {
                    var val = child.Value.Trim();

                    if (val.IndexOf('<') < 0 || string.Equals(val, NULL_SENTINEL, StringComparison.Ordinal))
                    {
                        // Scalar leaf under parentPath
                        AddNestedValue(nestedSections, parentPath, child.Name, val);
                    }
                    else
                    {
                        // Deeper nesting: recurse
                        var childPath = parentPath + "." + child.Name;
                        FlattenNestedXml(nestedSections, childPath, val);
                    }
                }
            }
        }

        private static void WriteFlatSection(
            StringBuilder sb,
            Dictionary<string, List<string>> flat,
            IReadOnlyDictionary<string, string> descriptions)
        {
            foreach (var kv in flat)
            {
                var key = kv.Key;
                var list = kv.Value;
                if (list == null || list.Count == 0)
                    continue;

                // Description comments: match by top-level property name
                if (descriptions != null)
                {
                    var lookupKey = key;
                    var dotIndex = key.IndexOf('.');
                    if (dotIndex > 0)
                        lookupKey = key.Substring(0, dotIndex);

                    string desc;
                    if (descriptions.TryGetValue(lookupKey, out desc) && !string.IsNullOrEmpty(desc))
                    {
                        var split = desc.Replace("\r\n", "\n").Split('\n');
                        foreach (var line in split)
                        {
                            if (!string.IsNullOrEmpty(line))
                            {
                                sb.Append("# ").Append(line).AppendLine();
                            }
                            else
                            {
                                sb.Append("#").AppendLine();
                            }
                        }
                    }
                }

                if (list.Count == 1)
                {
                    var literal = ToTomlLiteral(list[0]);
                    sb.Append(key)
                      .Append(" = ")
                      .Append(literal)
                      .AppendLine();
                }
                else
                {
                    sb.Append(key)
                      .Append(" = [");

                    for (var i = 0; i < list.Count; i++)
                    {
                        if (i > 0)
                            sb.Append(", ");

                        var literal = ToTomlLiteral(list[i]);
                        sb.Append(literal);
                    }

                    sb.Append(']')
                      .AppendLine();
                }
            }
        }

        private static void AddNestedValue(
            Dictionary<string, Dictionary<string, List<string>>> nestedSections,
            string parentName,
            string childName,
            string value)
        {
            Dictionary<string, List<string>> childMap;
            if (!nestedSections.TryGetValue(parentName, out childMap))
            {
                childMap = new Dictionary<string, List<string>>();
                nestedSections[parentName] = childMap;
            }

            List<string> list;
            if (!childMap.TryGetValue(childName, out list))
            {
                list = new List<string>();
                childMap[childName] = list;
            }

            list.Add(value ?? string.Empty);
        }


        // helper near the top of the class (private static)
        private static bool IsPrimitiveElementTag(string tag)
        {
            if (tag == null)
                return false;

            tag = tag.ToLowerInvariant();
            return tag == "int" || tag == "string" || tag == "float" || tag == "double" || tag == "bool";
        }

        public string ToInternal(IConfigDefinitionMain definitionMain, string externalContent)
        {
            if (definitionMain == null)
                throw new ArgumentNullException(nameof(definitionMain));

            // Empty file => default instance XML
            if (string.IsNullOrEmpty(externalContent))
                return definitionMain.GetCurrentDefaultsInternalXml();

            // Dictionary detection is now based purely on TOML sections (*-dictionary),
            // NOT on VariableDescriptions.
            var dictionaryParents = new HashSet<string>(StringComparer.Ordinal);

            // Parse TOML into key -> list of raw values,
            // and fill dictionaryParents from [TypeName.*-dictionary] sections.
            var flat = ParseTomlToFlatMap(externalContent, definitionMain.TypeName, dictionaryParents);

            // Split into:
            // - root scalars / arrays: key
            // - nested objects: Parent.Child, Parent.Child.GrandChild
            // - dictionary entries: DictParent.DictKey
            var rootMap = new Dictionary<string, List<string>>();
            var nested = new Dictionary<string, Dictionary<string, string>>();
            var dictionaries = new Dictionary<string, Dictionary<string, string>>();

            foreach (var kv in flat)
            {
                var key = kv.Key;
                var list = kv.Value ?? new List<string>();

                var segments = key.Split('.');
                if (segments.Length == 2 &&
                    !dictionaryParents.Contains(segments[0]) &&
                    IsPrimitiveElementTag(segments[1]))
                {
                    // Case: IntArray.int, Names.string, etc.
                    // Treat as an array for the parent property (IntArray / Names).
                    var parent = segments[0];

                    List<string> arr;
                    if (!rootMap.TryGetValue(parent, out arr))
                    {
                        arr = new List<string>();
                        rootMap[parent] = arr;
                    }

                    // Append all values from TOML array to this parent list
                    arr.AddRange(list);
                    continue;
                }

                if (segments.Length == 1)
                {
                    // Root scalar or array (simple key)
                    rootMap[key] = list;
                    continue;
                }

                // Everything with at least 2 segments from here on is nested or dictionary.
                var top = segments[0];
                var val = list.Count > 0 ? list[0] : string.Empty;

                if (dictionaryParents.Contains(top))
                {
                    // Dictionary<string, int>: NamedValues.start, NamedValues.end, ...
                    var dictKey = segments.Length >= 2
                        ? segments[1]
                        : string.Empty;

                    Dictionary<string, string> dict;
                    if (!dictionaries.TryGetValue(top, out dict))
                    {
                        dict = new Dictionary<string, string>();
                        dictionaries[top] = dict;
                    }

                    if (!string.IsNullOrEmpty(dictKey))
                    {
                        dict[dictKey] = val;
                    }

                    continue;
                }

                // Normal nested object.
                // We distinguish between:
                //  - Parent.Child        (e.g. Nested.Threshold)
                //  - Parent.Child.Field  (e.g. Settings.Display.Width or Keybinds.Select.Modifier)
                var parentName = top;
                string nestedKey;

                if (segments.Length == 2)
                {
                    // Example: Nested.Threshold -> parent = "Nested", nestedKey = "Threshold"
                    nestedKey = segments[1];
                }
                else
                {
                    // Example: Settings.Display.Width -> parent = "Settings", nestedKey = "Display.Width"
                    //          Keybinds.Select.Modifier -> parent = "Keybinds", nestedKey = "Select.Modifier"
                    nestedKey = segments[1] + "." + segments[2];
                }

                Dictionary<string, string> childMap;
                if (!nested.TryGetValue(parentName, out childMap))
                {
                    childMap = new Dictionary<string, string>();
                    nested[parentName] = childMap;
                }

                childMap[nestedKey] = val;
            }

            var sb = new StringBuilder();

            // XML declaration + namespaces so xsi:nil works correctly
            sb.Append("<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n");
            sb.Append('<').Append(definitionMain.TypeName)
              .Append(" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"")
              .Append(" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"")
              .Append('>');

            // Root scalars and arrays
            foreach (var kv in rootMap)
            {
                var name = kv.Key;
                var list = kv.Value;

                if (list == null || list.Count == 0)
                {
                    // Empty scalar
                    sb.Append('<').Append(name).Append("></").Append(name).Append('>');
                    continue;
                }

                if (list.Count == 1)
                {
                    var val = list[0];

                    // Null sentinel -> xsi:nil="true"
                    if (string.Equals(val, NULL_SENTINEL, StringComparison.Ordinal))
                    {
                        sb.Append('<').Append(name).Append(" xsi:nil=\"true\" />");
                    }
                    else
                    {
                        // Scalar
                        sb.Append('<').Append(name).Append('>');
                        sb.Append(XmlEscape(val));
                        sb.Append("</").Append(name).Append('>');
                    }
                }
                else
                {
                    // Array of primitive -> wrap in parent + element tags
                    sb.Append('<').Append(name).Append('>');

                    var elementTag = InferArrayElementTag(list);

                    foreach (var t in list)
                    {
                        sb.Append('<').Append(elementTag).Append('>');
                        sb.Append(XmlEscape(t));
                        sb.Append("</").Append(elementTag).Append('>');
                    }

                    sb.Append("</").Append(name).Append('>');
                }
            }

            // Dictionary parents -> SerializableDictionary XML shape
            foreach (var kd in dictionaries)
            {
                var parentName = kd.Key;
                var dict = kd.Value;

                sb.Append('<').Append(parentName).Append('>');
                sb.Append("<dictionary>");

                foreach (var entry in dict)
                {
                    sb.Append("<item>");
                    sb.Append("<Key>").Append(XmlEscape(entry.Key)).Append("</Key>");

                    // Dictionary values are not nullable in our current use-case,
                    // treat NullSentinel as normal text if ever present.
                    sb.Append("<Value>").Append(XmlEscape(entry.Value)).Append("</Value>");
                    sb.Append("</item>");
                }

                sb.Append("</dictionary>");
                sb.Append("</").Append(parentName).Append('>');
            }

            // Nested objects (supports one and two levels: Parent.Child and Parent.Child.Field)
            foreach (var kn in nested)
            {
                var parentName = kn.Key;
                var childMap = kn.Value;

                sb.Append('<').Append(parentName).Append('>');

                // Split child keys into:
                //  - direct children: "Threshold", "OpenMenu"
                //  - nested children: "Select.Modifier", "Select.Action", "Display.Width", ...
                var directChildren = new Dictionary<string, string>();
                var nestedChildren = new Dictionary<string, Dictionary<string, string>>();

                foreach (var kvChild in childMap)
                {
                    var rawName = kvChild.Key;
                    var val = kvChild.Value;

                    var parts = rawName.Split('.');
                    if (parts.Length == 1)
                    {
                        // Direct child under <parentName>
                        directChildren[rawName] = val;
                    }
                    else
                    {
                        // Two-level: mid.Leaf under <parentName>
                        // e.g. "Select.Modifier", "Display.Width"
                        var mid = parts[0];
                        var leaf = parts[1];

                        Dictionary<string, string> leafMap;
                        if (!nestedChildren.TryGetValue(mid, out leafMap))
                        {
                            leafMap = new Dictionary<string, string>();
                            nestedChildren[mid] = leafMap;
                        }

                        leafMap[leaf] = val;
                    }
                }

                // Emit direct children: <Threshold>, <OpenMenu>, etc.
                foreach (var kvChild in directChildren)
                {
                    var name = kvChild.Key;
                    var val = kvChild.Value;

                    if (string.Equals(val, NULL_SENTINEL, StringComparison.Ordinal))
                    {
                        sb.Append('<').Append(name).Append(" xsi:nil=\"true\" />");
                    }
                    else
                    {
                        sb.Append('<').Append(name).Append('>');
                        sb.Append(XmlEscape(val));
                        sb.Append("</").Append(name).Append('>');
                    }
                }

                // Emit nested children:
                // <Select><Modifier>..</Modifier><Action>..</Action>...</Select>
                // <Display><Width>..</Width>...</Display>
                foreach (var nestedEntry in nestedChildren)
                {
                    var midName = nestedEntry.Key;
                    var leaves = nestedEntry.Value;

                    sb.Append('<').Append(midName).Append('>');

                    foreach (var leafKv in leaves)
                    {
                        var leafName = leafKv.Key;
                        var val = leafKv.Value;

                        if (string.Equals(val, NULL_SENTINEL, StringComparison.Ordinal))
                        {
                            sb.Append('<').Append(leafName).Append(" xsi:nil=\"true\" />");
                        }
                        else
                        {
                            sb.Append('<').Append(leafName).Append('>');
                            sb.Append(XmlEscape(val));
                            sb.Append("</").Append(leafName).Append('>');
                        }
                    }

                    sb.Append("</").Append(midName).Append('>');
                }

                sb.Append("</").Append(parentName).Append('>');
            }

            sb.Append("</").Append(definitionMain.TypeName).Append('>');
            return sb.ToString();
        }

        // ====================================================================
        // TOML scalar/array helpers
        // ====================================================================

        private static string ToTomlLiteral(string raw)
        {
            if (raw == null)
                raw = string.Empty;

            // Null sentinel -> TOML null (bare token)
            if (string.Equals(raw, NULL_SENTINEL, StringComparison.Ordinal))
                return "null";

            bool b;
            int i;
            double d;

            // bool
            if (bool.TryParse(raw, out b))
                return b ? "true" : "false";

            // int
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out i))
                return i.ToString(CultureInfo.InvariantCulture);

            // double
            if (double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands,
                               CultureInfo.InvariantCulture, out d))
                return d.ToString("G", CultureInfo.InvariantCulture);

            // fallback: string, escaped
            var escaped = raw
                .Replace("\\", @"\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");

            return "\"" + escaped + "\"";
        }

        private static string FromTomlLiteral(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var s = value.Trim();

            // TOML null -> our NullSentinel
            if (string.Equals(s, "null", StringComparison.OrdinalIgnoreCase))
                return NULL_SENTINEL;

            // quoted -> unescape
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
            {
                s = s.Substring(1, s.Length - 2);

                s = s.Replace("\\\"", "\"");
                s = s.Replace(@"\\", "\\");
                s = s.Replace("\\r", "\r");
                s = s.Replace("\\n", "\n");

                return s;
            }

            // bare token
            return s;
        }

        private static void AddFlatValue(Dictionary<string, List<string>> map, string key, string value)
        {
            List<string> list;
            if (!map.TryGetValue(key, out list))
            {
                list = new List<string>();
                map[key] = list;
            }
            list.Add(value ?? string.Empty);
        }

        private static string InferArrayElementTag(List<string> values)
        {
            if (values == null || values.Count == 0)
                return "string";

            // Check if all look like ints
            var allInts = true;
            foreach (var t in values)
            {
                int tmp;
                if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out tmp)) continue;
                allInts = false;
                break;
            }

            // Could add more heuristics (double, bool) here if needed.
            return allInts ? "int" : "string";
        }

        // ====================================================================
        // Minimal XML parsing for root children and one-level snippets
        // ====================================================================

        private static List<KeyValuePair<string, string>> ParseRootChildren(string xml, out string rootName)
        {
            var result = new List<KeyValuePair<string, string>>();
            rootName = string.Empty;

            if (string.IsNullOrEmpty(xml))
                return result;

            var len = xml.Length;
            var pos = 0;

            // Find root
            string foundRoot = null;
            var rootStartClose = -1;
            var endRootIndex = -1;

            while (pos < len && foundRoot == null)
            {
                var lt = xml.IndexOf('<', pos);
                if (lt < 0)
                    break;

                var gt = xml.IndexOf('>', lt + 1);
                if (gt < 0)
                    break;

                var tagContent = xml.Substring(lt + 1, gt - lt - 1).Trim();
                if (tagContent.Length == 0)
                {
                    pos = gt + 1;
                    continue;
                }

                var first = tagContent[0];
                if (first == '?' || first == '!' || first == '/')
                {
                    pos = gt + 1;
                    continue;
                }

                var spaceIndex = tagContent.IndexOf(' ');
                foundRoot = spaceIndex >= 0
                    ? tagContent.Substring(0, spaceIndex)
                    : tagContent;

                rootStartClose = gt;

                var endRootTag = "</" + foundRoot + ">";
                endRootIndex = xml.LastIndexOf(endRootTag, StringComparison.Ordinal);
                if (endRootIndex < 0)
                    return result;
            }

            if (foundRoot == null || rootStartClose < 0 || endRootIndex <= rootStartClose)
                return result;

            rootName = foundRoot;

            var inner = xml.Substring(rootStartClose + 1, endRootIndex - (rootStartClose + 1));
            var innerLen = inner.Length;
            var innerPos = 0;

            while (true)
            {
                var startTagOpen = inner.IndexOf('<', innerPos);
                if (startTagOpen < 0 || startTagOpen >= innerLen)
                    break;

                var startTagClose = inner.IndexOf('>', startTagOpen + 1);
                if (startTagClose < 0)
                    break;

                var startTagContent = inner.Substring(startTagOpen + 1, startTagClose - startTagOpen - 1).Trim();
                if (startTagContent.Length == 0)
                {
                    innerPos = startTagClose + 1;
                    continue;
                }

                var first = startTagContent[0];
                if (first == '?' || first == '!' || first == '/')
                {
                    innerPos = startTagClose + 1;
                    continue;
                }

                var spaceIndex = startTagContent.IndexOf(' ');
                var selfClosing = startTagContent.EndsWith("/", StringComparison.Ordinal);
                var tagName = spaceIndex >= 0
                    ? startTagContent.Substring(0, spaceIndex)
                    : (selfClosing
                        ? startTagContent.TrimEnd('/')
                        : startTagContent);

                if (selfClosing)
                {
                    var hasNil = startTagContent.IndexOf("xsi:nil=\"true\"", StringComparison.Ordinal) >= 0;
                    result.Add(new KeyValuePair<string, string>(tagName, hasNil ? NULL_SENTINEL : string.Empty));
                    innerPos = startTagClose + 1;
                    continue;
                }

                var endTag = "</" + tagName + ">";
                var endTagIndex = inner.IndexOf(endTag, startTagClose + 1, StringComparison.Ordinal);
                if (endTagIndex < 0)
                {
                    innerPos = startTagClose + 1;
                    continue;
                }

                var valueStart = startTagClose + 1;
                var innerValue = inner.Substring(valueStart, endTagIndex - valueStart);

                result.Add(new KeyValuePair<string, string>(tagName, innerValue));

                innerPos = endTagIndex + endTag.Length;
            }

            return result;
        }

        private struct SnippetChild
        {
            public string Name;
            public string Value;
        }

        private static List<SnippetChild> ParseSnippetChildren(string snippet)
        {
            var result = new List<SnippetChild>();

            if (string.IsNullOrEmpty(snippet))
                return result;

            var len = snippet.Length;
            var pos = 0;

            while (true)
            {
                var lt = snippet.IndexOf('<', pos);
                if (lt < 0 || lt >= len)
                    break;

                var gt = snippet.IndexOf('>', lt + 1);
                if (gt < 0)
                    break;

                var tagContent = snippet.Substring(lt + 1, gt - lt - 1).Trim();
                if (tagContent.Length == 0)
                {
                    pos = gt + 1;
                    continue;
                }

                var first = tagContent[0];
                if (first == '?' || first == '!' || first == '/')
                {
                    pos = gt + 1;
                    continue;
                }

                var spaceIndex = tagContent.IndexOf(' ');
                var selfClosing = tagContent.EndsWith("/", StringComparison.Ordinal);
                var tagName = spaceIndex >= 0
                    ? tagContent.Substring(0, spaceIndex)
                    : (selfClosing
                        ? tagContent.TrimEnd('/')
                        : tagContent);

                if (selfClosing)
                {
                    var hasNil = tagContent.IndexOf("xsi:nil=\"true\"", StringComparison.Ordinal) >= 0;
                    result.Add(new SnippetChild
                    {
                        Name = tagName,
                        Value = hasNil ? NULL_SENTINEL : string.Empty
                    });
                    pos = gt + 1;
                    continue;
                }

                var endTag = "</" + tagName + ">";
                var endTagIndex = snippet.IndexOf(endTag, gt + 1, StringComparison.Ordinal);
                if (endTagIndex < 0)
                {
                    pos = gt + 1;
                    continue;
                }

                var valueStart = gt + 1;
                var innerValue = snippet.Substring(valueStart, endTagIndex - valueStart);

                result.Add(new SnippetChild
                {
                    Name = tagName,
                    Value = innerValue
                });

                pos = endTagIndex + endTag.Length;
            }

            return result;
        }

        private static string XmlEscape(string s)
        {
            if (s == null)
                return string.Empty;

            return s
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        // ====================================================================
        // TOML parsing
        // ====================================================================

        private static Dictionary<string, List<string>> ParseTomlToFlatMap(
            string toml,
            string typeName,
            HashSet<string> dictionaryParents)
        {
            var result = new Dictionary<string, List<string>>();

            if (dictionaryParents == null)
                dictionaryParents = new HashSet<string>(StringComparer.Ordinal);

            if (string.IsNullOrEmpty(toml))
                return result;

            string currentSection = null;

            var lines = toml.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var raw in lines)
            {
                if (string.IsNullOrEmpty(raw))
                    continue;

                var line = raw.Trim();
                if (line.Length == 0)
                    continue;

                // full-line comments
                if (line[0] == '#')
                    continue;

                // section header
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    var sectionName = line.Substring(1, line.Length - 2).Trim();
                    currentSection = sectionName;

                    // Detect [TypeName.X-dictionary] sections and remember X as a dictionary parent
                    if (sectionName.StartsWith(typeName + ".", StringComparison.Ordinal))
                    {
                        var suffix = sectionName.Substring(typeName.Length + 1); // e.g. "NamedValues-dictionary"
                        if (suffix.EndsWith("-dictionary", StringComparison.Ordinal))
                        {
                            var parentName = suffix.Substring(0, suffix.Length - "-dictionary".Length);
                            dictionaryParents.Add(parentName);
                        }
                    }

                    continue;
                }

                var eqIndex = line.IndexOf('=');
                if (eqIndex <= 0)
                    continue;

                var key = line.Substring(0, eqIndex).Trim();
                var valuePart = line.Substring(eqIndex + 1).Trim();

                // strip end-of-line comment
                valuePart = StripInlineComment(valuePart);

                if (string.IsNullOrEmpty(valuePart))
                {
                    var finalKeyEmpty = QualifyTomlKey(key, currentSection, typeName);
                    AddFlatValue(result, finalKeyEmpty, string.Empty);
                    continue;
                }

                var finalKey = QualifyTomlKey(key, currentSection, typeName);

                // Array: key = [ ... ]
                if (valuePart.Length >= 2 &&
                    valuePart[0] == '[' &&
                    valuePart[valuePart.Length - 1] == ']')
                {
                    var inner = valuePart.Substring(1, valuePart.Length - 2);

                    var items = SplitTomlArray(inner);
                    foreach (var t in items)
                    {
                        var itemText = t.Trim();
                        if (itemText.Length == 0)
                            continue;

                        var rawValue = FromTomlLiteral(itemText);
                        AddFlatValue(result, finalKey, rawValue);
                    }
                }
                else
                {
                    var rawValue = FromTomlLiteral(valuePart);
                    AddFlatValue(result, finalKey, rawValue);
                }
            }

            return result;
        }

        private static string QualifyTomlKey(string key, string currentSection, string typeName)
        {
            // Default: no section or irrelevant section -> keep key as-is
            if (string.IsNullOrEmpty(currentSection))
                return key;

            // Main type section: [TypeName]
            if (string.Equals(currentSection, typeName, StringComparison.Ordinal))
                return key;

            // Subsections of the type: [TypeName.Something]
            if (currentSection.StartsWith(typeName + ".", StringComparison.Ordinal))
            {
                var suffix = currentSection.Substring(typeName.Length + 1); // e.g. "NamedValues-dictionary"

                // Dictionary section: [TypeName.NamedValues-dictionary]
                if (suffix.EndsWith("-dictionary", StringComparison.Ordinal))
                {
                    var propName = suffix.Substring(0, suffix.Length - "-dictionary".Length);

                    // remove quotes from TOML key: "start" -> start
                    var dictKey = key;
                    if (dictKey.Length >= 2 && dictKey[0] == '"' && dictKey[dictKey.Length - 1] == '"')
                    {
                        dictKey = dictKey.Substring(1, dictKey.Length - 2);
                    }

                    return propName + "." + dictKey; // NamedValues.start
                }

                // Generic subsection (not used yet, but keep sane behavior):
                // e.g. [TypeName.Nested] Threshold = 0.9  ->  Nested.Threshold
                return suffix + "." + key;
            }

            // Unknown section; leave as-is
            return key;
        }

        private static string StripInlineComment(string s)
        {
            var inString = false;
            var escape = false;
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (escape) { escape = false; continue; }
                switch (c)
                {
                    case '\\':
                        escape = true;
                        continue;
                    case '"':
                        inString = !inString;
                        continue;
                }

                if (c == '#' && !inString)
                    return s.Substring(0, i).TrimEnd();
            }
            return s;
        }

        private static List<string> SplitTomlArray(string inner)
        {
            var result = new List<string>();
            if (inner == null)
                return result;

            var sb = new StringBuilder();
            var inString = false;
            var escape = false;

            foreach (var c in inner)
            {
                if (escape)
                {
                    sb.Append(c);
                    escape = false;
                    continue;
                }

                switch (c)
                {
                    case '\\':
                        sb.Append(c);
                        escape = true;
                        continue;
                    case '"':
                        sb.Append(c);
                        inString = !inString;
                        continue;
                }

                if (c == ',' && !inString)
                {
                    result.Add(sb.ToString());
                    sb.Length = 0;
                    continue;
                }

                sb.Append(c);
            }

            if (sb.Length > 0)
                result.Add(sb.ToString());

            return result;
        }
    }
}