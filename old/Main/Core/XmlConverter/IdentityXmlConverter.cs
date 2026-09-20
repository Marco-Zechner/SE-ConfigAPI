using MarcoZechner.ConfigAPI.Main.Domain;

namespace MarcoZechner.ConfigAPI.Main.Core.XmlConverter
{
    /// <summary>
    /// Trivial converter: external format == internal XML.
    /// Useful for XML-only configs or tests.
    /// </summary>
    public sealed class IdentityXmlConverter : IXmlConverter
    {
        public string GetExtension => ".xml";

        public string ToExternal(IConfigDefinitionMain definitionMain, string xmlContent, bool includeDescriptions)
        {
            return xmlContent ?? string.Empty;
        }

        public string ToInternal(IConfigDefinitionMain definitionMain, string externalContent)
        {
            return externalContent ?? string.Empty;
        }
    }
}