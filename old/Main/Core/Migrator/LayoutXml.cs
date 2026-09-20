using System;
using System.Collections.Generic;
using System.Text;

namespace MarcoZechner.ConfigAPI.Main.Core.Migrator
{
    public static class LayoutXml
    {
        private const string XML_DECL = "<?xml version=\"1.0\" encoding=\"utf-16\"?>";
        private const string XSD_NS = "http://www.w3.org/2001/XMLSchema";
        private const string XSI_NS = "http://www.w3.org/2001/XMLSchema-instance";
        private const string ROOT_INDENT = "  ";

        public struct Child
        {
            public string Name;
            public string Block; // raw xml fragment: <Name>...</Name> or <Name .../>
        }

        public sealed class Children
        {
            public readonly List<Child> Ordered;
            private readonly Dictionary<string, int> _index; // name -> first occurrence index

            // For easier testing
            public Children(Dictionary<string, string> children)
            {
                Ordered = new List<Child>();
                _index = new Dictionary<string, int>(StringComparer.Ordinal);
                if (children == null) return;

                var i = 0;
                foreach (var kvp in children)
                {
                    Ordered.Add(new Child { Name = kvp.Key, Block = kvp.Value });
                    _index.Add(kvp.Key, i);
                    i++;
                }
            }

            public Children(List<Child> ordered)
            {
                Ordered = ordered ?? new List<Child>();
                _index = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var i = 0; i < Ordered.Count; i++)
                {
                    // If duplicates exist (shouldn't for serializer), keep the first for lookup.
                    if (!_index.ContainsKey(Ordered[i].Name))
                        _index.Add(Ordered[i].Name, i);
                }
            }

            public bool TryGet(string name, out string block)
            {
                int idx;
                if (_index.TryGetValue(name, out idx))
                {
                    block = Ordered[idx].Block;
                    return true;
                }
                block = null;
                return false;
            }

            public bool Contains(string name) => _index.ContainsKey(name);
        }

        /// <summary>
        /// Ordered parse: returns direct root children in the exact order found in the XML.
        /// </summary>
        public static Children ParseChildren(string xml, out string rootName)
        {
            var resultList = new List<Child>();
            rootName = string.Empty;

            if (string.IsNullOrEmpty(xml)) return new Children(resultList);

            var len = xml.Length;
            var pos = 0;

            // 1) Find root element (skip xml decl, comments, etc.)
            while (pos < len)
            {
                var lt = xml.IndexOf('<', pos);
                if (lt < 0) return new Children(resultList);

                var gt = xml.IndexOf('>', lt + 1);
                if (gt < 0) return new Children(resultList);

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

                var spaceIdx = tagContent.IndexOf(' ');
                rootName = spaceIdx >= 0 ? tagContent.Substring(0, spaceIdx) : tagContent;

                pos = gt + 1;
                break;
            }

            if (string.IsNullOrEmpty(rootName)) return new Children(resultList);

            var endRootTag = "</" + rootName + ">";
            var endIndex = xml.LastIndexOf(endRootTag, StringComparison.Ordinal);
            if (endIndex < 0 || endIndex <= pos) return new Children(resultList);

            var inner = xml.Substring(pos, endIndex - pos);
            var innerLen = inner.Length;
            var i = 0;

            // 2) Scan inner for direct children in-order
            while (i < innerLen)
            {
                while (i < innerLen && char.IsWhiteSpace(inner[i])) i++;
                if (i >= innerLen) break;

                if (inner[i] != '<')
                {
                    i++;
                    continue;
                }

                var startTagOpen = i;
                var startTagClose = inner.IndexOf('>', startTagOpen + 1);
                if (startTagClose < 0) break;

                var startContent = inner.Substring(startTagOpen + 1, startTagClose - startTagOpen - 1).Trim();
                if (startContent.Length == 0)
                {
                    i = startTagClose + 1;
                    continue;
                }

                var c0 = startContent[0];
                if (c0 == '?' || c0 == '!' || c0 == '/')
                {
                    i = startTagClose + 1;
                    continue;
                }

                var spaceIndex = startContent.IndexOf(' ');
                var selfClosing = startContent.Length > 0 && startContent[startContent.Length - 1] == '/';

                var childName = spaceIndex >= 0 ? startContent.Substring(0, spaceIndex) : startContent;
                if (selfClosing && childName.EndsWith("/", StringComparison.Ordinal))
                    childName = childName.TrimEnd('/');

                if (selfClosing)
                {
                    var childEnd = startTagClose + 1;
                    var block = inner.Substring(startTagOpen, childEnd - startTagOpen);

                    resultList.Add(new Child { Name = childName, Block = block });
                    i = childEnd;
                    continue;
                }

                // Non self-closing: find matching </childName> with depth tracking
                var depth = 1;
                var searchPos = startTagClose + 1;

                while (searchPos < innerLen && depth > 0)
                {
                    var nextLt = inner.IndexOf('<', searchPos);
                    if (nextLt < 0) break;

                    var nextGt = inner.IndexOf('>', nextLt + 1);
                    if (nextGt < 0) break;

                    var t = inner.Substring(nextLt + 1, nextGt - nextLt - 1).Trim();
                    if (t.Length == 0)
                    {
                        searchPos = nextGt + 1;
                        continue;
                    }

                    var t0 = t[0];
                    if (t0 == '!' || t0 == '?')
                    {
                        searchPos = nextGt + 1;
                        continue;
                    }

                    var closing = t0 == '/';
                    string name;

                    if (closing)
                    {
                        name = t.Substring(1);
                        var sp = name.IndexOf(' ');
                        if (sp >= 0) name = name.Substring(0, sp);
                    }
                    else
                    {
                        var sp = t.IndexOf(' ');
                        name = sp >= 0 ? t.Substring(0, sp) : t;

                        // self closing tag does not change depth
                        var isSelf = t.Length > 0 && t[t.Length - 1] == '/';
                        if (isSelf)
                        {
                            searchPos = nextGt + 1;
                            continue;
                        }
                    }

                    if (!closing && name == childName)
                        depth++;
                    else if (closing && name == childName) depth--;

                    searchPos = nextGt + 1;

                    if (depth != 0) continue;

                    var childEnd = nextGt + 1;
                    var block = inner.Substring(startTagOpen, childEnd - startTagOpen);

                    resultList.Add(new Child { Name = childName, Block = block });
                    i = childEnd;
                    break;
                }

                if (depth > 0) break;
            }

            return new Children(resultList);
        }

        /// <summary>
        /// Build canonical doc while preserving the exact child order given by the list.
        /// </summary>
        public static string Build(string rootName, Children children)
        {
            var sb = new StringBuilder();

            sb.Append(XML_DECL).Append("\r\n");
            sb.Append('<').Append(rootName)
                .Append(" xmlns:xsd=\"").Append(XSD_NS).Append('"')
                .Append(" xmlns:xsi=\"").Append(XSI_NS).Append('"')
                .Append('>')
                .Append("\r\n");

            if (children != null)
            {
                for (int k = 0; k < children.Ordered.Count; k++)
                {
                    var block = children.Ordered[k].Block ?? string.Empty;

                    block = block.Replace("\r\n", "\n");
                    var lines = block.Split(new[] { '\n' }, StringSplitOptions.None);
                    var firstLineEmitted = false;

                    for (var i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        var trimmedEnd = line.TrimEnd('\r', ' ', '\t');

                        if (!firstLineEmitted)
                        {
                            sb.Append(ROOT_INDENT).Append(trimmedEnd).Append("\r\n");
                            firstLineEmitted = true;
                        }
                        else
                        {
                            sb.Append(trimmedEnd).Append("\r\n");
                        }
                    }
                }
            }

            sb.Append("</").Append(rootName).Append('>');
            return sb.ToString();
        }

        /// <summary>
        /// Canonicalize without reordering: Parse -> Build with the same ordering.
        /// Use this for "format-insensitive" comparisons.
        /// </summary>
        public static string Canonicalize(string xml)
        {
            string root;
            var list = ParseChildren(xml, out root);
            return string.IsNullOrEmpty(root)
                ? (xml ?? string.Empty).Trim()
                : Build(root, list);
        }
    }
}