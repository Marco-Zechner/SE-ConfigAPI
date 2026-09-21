using System;

namespace Mz.ConfigApi
{
    public sealed class ConfigDefinition<T> where T : class
    {
        public const string DefaultVariant = "default";

        private readonly Func<T> _createDefaults;
        private readonly Func<T, ConfigDocument> _serialize;
        private readonly Func<ConfigDocument, T> _deserialize;

        public string ConfigKey { get; private set; }

        public ConfigDefinition(string configKey, Func<T> createDefaults, Func<T, ConfigDocument> serialize, Func<ConfigDocument, T> deserialize)
        {
            ConfigKey = NormalizeName(configKey, nameof(configKey), "Config key");

            if (createDefaults == null)
                throw new ArgumentNullException(nameof(createDefaults));
            if (serialize == null)
                throw new ArgumentNullException(nameof(serialize));
            if (deserialize == null)
                throw new ArgumentNullException(nameof(deserialize));

            _createDefaults = createDefaults;
            _serialize = serialize;
            _deserialize = deserialize;
        }

        public T CreateDefaults()
        {
            T defaults = _createDefaults();

            if (defaults == null)
                throw new InvalidOperationException($"The config default factory returned null for {typeof(T).FullName}.");

            return defaults;
        }

        public ConfigDocument Serialize(T value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            ConfigDocument document = _serialize(value);

            if (document == null)
                throw new InvalidOperationException($"The config serializer returned null for {typeof(T).FullName}.");

            return document;
        }

        public T Deserialize(ConfigDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            T value = _deserialize(document);

            if (value == null)
                throw new InvalidOperationException($"The config deserializer returned null for {typeof(T).FullName}.");

            return value;
        }

        internal string NormalizeVariant(string variant) => NormalizeName(variant, nameof(variant), "Variant");

        internal string GetVariantFile(string variant) => ConfigKey + "." + NormalizeVariant(variant) + ".toml";

        private static string NormalizeName(string value, string parameterName, string displayName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(displayName + " must not be empty.", parameterName);

            string normalized = value.Trim();

            if (normalized.IndexOf('.') >= 0)
                throw new ArgumentException(displayName + " must not contain '.'.", parameterName);

            return normalized;
        }
    }
}
