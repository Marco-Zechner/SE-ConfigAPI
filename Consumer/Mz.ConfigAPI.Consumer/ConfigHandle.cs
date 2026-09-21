using System;

namespace Mz.ConfigApi
{
    public sealed class ConfigHandle<T> where T : class
    {
        private readonly ConfigApiClient _client;
        private readonly ConfigDefinition<T> _definition;

        internal ConfigHandle(ConfigApiClient client, ConfigDefinition<T> definition, ConfigLocation location, string currentVariant, T value)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            _client = client;
            _definition = definition;
            Location = location;
            CurrentVariant = definition.NormalizeVariant(currentVariant);
            Value = value;
        }

        public ConfigLocation Location { get; }
        public string CurrentVariant { get; private set; }
        public T Value { get; private set; }

        public T Load(string variant)
        {
            string normalizedVariant = _definition.NormalizeVariant(variant);
            string file = _definition.GetVariantFile(normalizedVariant);
            T value = _definition.Deserialize(_client.Open(_definition.ConfigKey, Location, file, _definition.Serialize(_definition.CreateDefaults())));

            CurrentVariant = normalizedVariant;
            Value = value;
            return value;
        }

        public T SavePreset(string presetFile, bool overwrite = false)
        {
            if (string.IsNullOrWhiteSpace(presetFile))
                throw new ArgumentException("Preset file must not be empty.", nameof(presetFile));

            string defaultFile = _definition.GetVariantFile(ConfigDefinition<T>.DefaultVariant);
            if (string.Equals(defaultFile, presetFile, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Preset target must not be the canonical active config file: " + presetFile);

            return _definition.Deserialize(_client.SavePreset(_definition.ConfigKey, Location, defaultFile, presetFile, _definition.Serialize(_definition.CreateDefaults()), _definition.Serialize(Value), overwrite));
        }

        public T ApplyPreset(string presetFile)
        {
            if (string.IsNullOrWhiteSpace(presetFile))
                throw new ArgumentException("Preset file must not be empty.", nameof(presetFile));

            string defaultFile = _definition.GetVariantFile(ConfigDefinition<T>.DefaultVariant);
            T value = _definition.Deserialize(_client.ApplyPreset(_definition.ConfigKey, Location, defaultFile, presetFile, _definition.Serialize(_definition.CreateDefaults())));

            CurrentVariant = ConfigDefinition<T>.DefaultVariant;
            Value = value;
            return value;
        }

        public T Reload()
        {
            string file = _definition.GetVariantFile(CurrentVariant);
            T value = _definition.Deserialize(_client.Open(_definition.ConfigKey, Location, file, _definition.Serialize(_definition.CreateDefaults())));

            Value = value;
            return value;
        }
    }
}
