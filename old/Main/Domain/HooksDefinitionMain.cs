using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Main.Api;

namespace MarcoZechner.ConfigAPI.Main.Domain
{
    public sealed class HooksDefinitionMain : IConfigDefinitionMain
    {
        private readonly ConfigUserHooks _hooks;
        private readonly string _typeKey;

        public HooksDefinitionMain(ConfigUserHooks hooks, string typeKey)
        {
            _hooks = hooks;
            _typeKey = typeKey;
            TypeName = ExtractTypeName(typeKey);
        }

        public string TypeName { get; }


        public string GetCurrentDefaultsInternalXml()
        {
            var def = _hooks.NewDefault(_typeKey);
            if (def == null) throw new Exception("HooksDefinition: NewDefault returned null for " + _typeKey);
            return _hooks.SerializeToInternalXml(_typeKey, def);
        }

        public IReadOnlyDictionary<string, string> GetVariableDescriptions()
            => _hooks.GetVariableDescriptions(_typeKey);

        private static string ExtractTypeName(string typeKey)
        {
            // Your XML serializer root is typically the class name (not full name).
            // TomlXmlConverter expects TypeName to match the XML root element.
            // If your serializer root is actually full name, change this.
            if (string.IsNullOrEmpty(typeKey))
                return "Config";

            var lastDot = typeKey.LastIndexOf('.');
            return lastDot >= 0 ? typeKey.Substring(lastDot + 1) : typeKey;
        }
    }
}