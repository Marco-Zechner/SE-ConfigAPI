using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Main.Domain;

namespace MarcoZechner.ConfigAPI.Main.Core.Migrator
{
    /// <summary>
    /// Normalizes a config's XML layout against the current code layout:
    /// - adds missing keys with defaults
    /// - removes keys that no longer exist (signals backup)
    /// - keeps existing values where possible
    /// - upgrades values that are still equal to an old default to the new default
    ///
    /// It operates only on XML strings produced/consumed by the XML serializer,
    /// using LayoutXml to handle full child elements (so xsi:nil, nested blocks etc. survive).
    /// </summary>
    public sealed class ConfigLayoutMigrator : IConfigLayoutMigrator
    {
        public ConfigLayoutResult Normalize(
                string typeName,
                string xmlCurrentFromFile,
                string xmlOldDefaultsFromFile,
                string xmlCurrentDefaults)
        {

            // Defensive: treat null as empty
            if (xmlCurrentFromFile == null) xmlCurrentFromFile = string.Empty;
            if (xmlOldDefaultsFromFile == null) xmlOldDefaultsFromFile = string.Empty;
            if (xmlCurrentDefaults == null) xmlCurrentDefaults = string.Empty;

            var result = new ConfigLayoutResult();

            try
            {
                // Parse three layouts into: rootName + map of childName -> full child XML
                string rootCurrent;
                var currentChildren = LayoutXml.ParseChildren(xmlCurrentFromFile, out rootCurrent);

                string rootOldDefaults;
                var oldDefaultChildren = LayoutXml.ParseChildren(xmlOldDefaultsFromFile, out rootOldDefaults);

                string rootNewDefaults;
                var newDefaultChildren = LayoutXml.ParseChildren(xmlCurrentDefaults, out rootNewDefaults);

                // Use the config's type name as canonical root name (matches serializer)
                var rootName = typeName;

                var requiresBackup = false;
                var normalizedCurrentOrdered = new List<LayoutXml.Child>();

                // Track which keys we already handled (so we can append missing defaults later)
                var seen = new HashSet<string>(StringComparer.Ordinal);

                foreach (var child in currentChildren.Ordered)
                {
                    var key = child.Name;
                    var currentElement = child.Block;

                    seen.Add(key);

                    if (!newDefaultChildren.Contains(key))
                    {
                        requiresBackup = true;
                        continue; // drop it
                    }

                    string newDefaultElement;
                    newDefaultChildren.TryGet(key, out newDefaultElement);

                    string oldDefaultElement;
                    var hasOldDefault = oldDefaultChildren.TryGet(key, out oldDefaultElement);

                    var structuralCorruption =
                        HasBrokenComplexChildren(newDefaultElement, currentElement);

                    string finalCurrentElement;

                    if (structuralCorruption)
                    {
                        finalCurrentElement = newDefaultElement;
                        requiresBackup = true;
                    }
                    else if (hasOldDefault &&
                             LayoutXml.Canonicalize(currentElement) == LayoutXml.Canonicalize(oldDefaultElement) &&
                             LayoutXml.Canonicalize(oldDefaultElement) != LayoutXml.Canonicalize(newDefaultElement))
                    {
                        // upgrade old-default -> new-default, formatting-insensitive
                        finalCurrentElement = newDefaultElement;
                    }
                    else
                    {
                        finalCurrentElement = currentElement;
                    }

                    normalizedCurrentOrdered.Add(new LayoutXml.Child { Name = key, Block = finalCurrentElement });
                }

                // Append missing keys from new defaults at the end (in the defaults' order)
                foreach (var child in newDefaultChildren.Ordered)
                {
                    var key = child.Name;
                    if (seen.Contains(key))
                        continue;

                    // missing in user's file
                    requiresBackup = true; // your old code didn't set this for missing keys; decide if you want it.
                    normalizedCurrentOrdered.Add(new LayoutXml.Child { Name = key, Block = child.Block });
                }

                // Normalized defaults should follow the *current defaults order* (code layout)

                // Build output XML (canonical header, keep child block formatting as provided)
                var normalizedCurrentXml = LayoutXml.Build(rootName, new LayoutXml.Children(normalizedCurrentOrdered));
                var normalizedDefaultsXml = LayoutXml.Build(rootName, newDefaultChildren);

                result.NormalizedXml = normalizedCurrentXml;
                result.NormalizedDefaultsXml = normalizedDefaultsXml;
                result.RequiresBackup = requiresBackup;
                return result;
            }
            catch
            {
                // On any error, fall back to original "current" XML and no backup recommendation.
                result.NormalizedXml = xmlCurrentFromFile;
                result.NormalizedDefaultsXml = xmlCurrentDefaults;
                result.RequiresBackup = false;
                return result;
            }
        }

        /// <summary>
        /// Detects structural corruption for a single element:
        /// If in the default element a direct child is a complex object (it has its own children),
        /// but in the current element that same child is missing or has no children (flattened to scalar),
        /// we treat this as a corrupted layout.
        ///
        /// This is generic and works for things like:
        /// - Settings.Display (complex in defaults) -> turned into scalar text in current
        /// - NamedValues (dictionary) -> turned into scalar text in current
        /// </summary>
        private static bool HasBrokenComplexChildren(string defaultElement, string currentElement)
        {
            if (string.IsNullOrEmpty(defaultElement) || string.IsNullOrEmpty(currentElement))
                return false;

            string defaultRootName;
            var defaultChildren = LayoutXml.ParseChildren(defaultElement, out defaultRootName);
            if (defaultChildren.Ordered.Count == 0)
                return false; // no expectations about nested structure

            string currentRootName;
            var currentChildren = LayoutXml.ParseChildren(currentElement, out currentRootName);

            foreach (var child in defaultChildren.Ordered)
            {
                var childName = child.Name;
                var defaultChildBlock = child.Block;

                // Look at the default child's children to see if it's complex
                string dummy;
                var defaultGrandChildren = LayoutXml.ParseChildren(defaultChildBlock, out dummy);

                // Only care about children that are complex in defaults
                if (defaultGrandChildren.Ordered.Count == 0)
                    continue;

                // In current: if the complex child is missing or has been flattened, that's corruption
                string currentChildBlock;
                if (!currentChildren.TryGet(childName, out currentChildBlock))
                {
                    // Complex child missing entirely
                    return true;
                }

                var currentGrandChildren = LayoutXml.ParseChildren(currentChildBlock, out dummy);
                if (currentGrandChildren.Ordered.Count == 0)
                {
                    // Child exists but has no children -> likely scalar or empty, not complex as required
                    return true;
                }
            }

            return false;
        }
    }
}