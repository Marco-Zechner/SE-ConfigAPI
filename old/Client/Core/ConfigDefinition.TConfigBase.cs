using System;
using System.Collections.Generic;

namespace MarcoZechner.ConfigAPI.Client.Core
{
    public sealed class ConfigDefinitionClient<T> : IConfigDefinitionClient where T : ConfigBase, new()
    {
        public ConfigBase NewDefault()
        {
            var t = new T();
            t.ApplyDefaults();
            return t;
        }

        public string DefaultName()
        {
            return typeof(T).Name;
        }

        public string SerializeToInternalXml(ConfigBase instance)
        {
            var t = instance as T;
            if (t == null)
                throw new Exception("SerializeToInternalXml: instance is not of expected type " + typeof(T).FullName);

            return ConfigXmlSerializer.SerializeToXml(instance);
        }

        public ConfigBase DeserializeFromInternalXml(string internalXml)
        {
            if (string.IsNullOrEmpty(internalXml))
                throw new Exception("DeserializeFromInternalXml: xml is null/empty for " + typeof(T).FullName);

            var instance = ConfigXmlSerializer.DeserializeFromXml<T>(internalXml);
            return instance;
        }

        public IReadOnlyDictionary<string, string> GetVariableDescriptions()
            => NewDefault().VariableDescriptions ?? new Dictionary<string, string>();
    }
}