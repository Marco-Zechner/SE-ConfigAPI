using System;

namespace Mz.ConfigApi
{
    public sealed class ConfigDefinition<T> where T : class
    {
        private readonly Func<T> _createDefaults;
        private readonly Func<T, ConfigDocument> _serialize;
        private readonly Func<ConfigDocument, T> _deserialize;

        public string ConfigKey { get; private set; }
        public string DefaultFile { get; private set; }

        public ConfigDefinition(string configKey, string defaultFile, Func<T> createDefaults, Func<T, ConfigDocument> serialize, Func<ConfigDocument, T> deserialize)
        {
            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));

            if (string.IsNullOrWhiteSpace(defaultFile))
                throw new ArgumentException("Default config file must not be empty.", nameof(defaultFile));

            if (createDefaults == null)
                throw new ArgumentNullException(nameof(createDefaults));

            if (serialize == null)
                throw new ArgumentNullException(nameof(serialize));

            if (deserialize == null)
                throw new ArgumentNullException(nameof(deserialize));

            ConfigKey = configKey.Trim();
            DefaultFile = defaultFile;
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
    }
}
