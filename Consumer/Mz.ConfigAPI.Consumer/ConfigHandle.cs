using System;

namespace Mz.ConfigApi
{
    public sealed class ConfigHandle<T> where T : class
    {
        private readonly ConfigApiClient _client;
        private readonly ConfigDefinition<T> _definition;
        private readonly ConfigDocument _defaultsDocument;
        private ConfigDocument _storedDocument;
        private ConfigDocument _appliedDocument;
        private T _draft;

        internal ConfigHandle(ConfigApiClient client, ConfigDefinition<T> definition, ConfigLocation location, string currentVariant, ConfigDocument defaultsDocument, ConfigDocument storedDocument)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (defaultsDocument == null)
                throw new ArgumentNullException(nameof(defaultsDocument));
            if (storedDocument == null)
                throw new ArgumentNullException(nameof(storedDocument));

            _client = client;
            _definition = definition;
            _defaultsDocument = defaultsDocument;
            _storedDocument = storedDocument;
            _appliedDocument = storedDocument;
            _draft = definition.Deserialize(storedDocument);
            Location = location;
            CurrentVariant = definition.NormalizeVariant(currentVariant);
        }

        public ConfigLocation Location { get; }
        public string CurrentVariant { get; private set; }

        public T Defaults => DeserializeCopy(_defaultsDocument);
        public T Stored => DeserializeCopy(_storedDocument);
        public T Applied => DeserializeCopy(_appliedDocument);
        public T Draft => _draft;
        public T Value => Applied;

        public bool HasDraftChanges => !_definition.Serialize(_draft).Equals(_appliedDocument);
        public bool HasUnsavedChanges => !_appliedDocument.Equals(_storedDocument);

        public void Apply()
        {
            ConfigDocument document = _definition.Serialize(_draft);
            T draft = DeserializeCopy(document);

            _appliedDocument = document;
            _draft = draft;
        }

        public void DiscardDraft() => _draft = DeserializeCopy(_appliedDocument);

        public void ResetDraftToDefaults() => _draft = DeserializeCopy(_defaultsDocument);

        public T Save()
        {
            string file = _definition.GetVariantFile(CurrentVariant);
            ConfigDocument saved = _client.Save(_definition.ConfigKey, Location, file, _defaultsDocument, _appliedDocument);
            T stored = DeserializeCopy(saved);

            _storedDocument = saved;
            return stored;
        }

        public T Load(string variant)
        {
            string normalizedVariant = _definition.NormalizeVariant(variant);
            string file = _definition.GetVariantFile(normalizedVariant);
            ConfigDocument loaded = _client.Open(_definition.ConfigKey, Location, file, _defaultsDocument);
            T draft = DeserializeCopy(loaded);

            CurrentVariant = normalizedVariant;
            _storedDocument = loaded;
            _appliedDocument = loaded;
            _draft = draft;
            return Applied;
        }

        public T Reload()
        {
            string file = _definition.GetVariantFile(CurrentVariant);
            ConfigDocument loaded = _client.Open(_definition.ConfigKey, Location, file, _defaultsDocument);
            T draft = DeserializeCopy(loaded);

            _storedDocument = loaded;
            _appliedDocument = loaded;
            _draft = draft;
            return Applied;
        }

        public string[] ListVariants()
        {
            string prefix = _definition.ConfigKey + ".";
            const string suffix = ".toml";
            string[] files = _client.ListKnownFiles(Location);
            var variants = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < files.Length; index++)
            {
                string file = files[index];
                if (string.IsNullOrEmpty(file) || !file.StartsWith(prefix, StringComparison.Ordinal) || !file.EndsWith(suffix, StringComparison.Ordinal))
                    continue;

                int variantLength = file.Length - prefix.Length - suffix.Length;
                if (variantLength <= 0)
                    continue;

                string variant = file.Substring(prefix.Length, variantLength);
                if (variant.IndexOf('.') >= 0 || !string.Equals(variant, variant.Trim(), StringComparison.Ordinal))
                    continue;

                variants.Add(variant);
            }

            var result = new System.Collections.Generic.List<string>(variants);
            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }

        public T SaveAs(string variant)
        {
            string normalizedVariant = _definition.NormalizeVariant(variant);
            string file = _definition.GetVariantFile(normalizedVariant);

            if (_client.StorageExists(Location, file))
                throw new InvalidOperationException("Config variant already exists: " + normalizedVariant);

            ConfigDocument saved = _client.Save(_definition.ConfigKey, Location, file, _defaultsDocument, _appliedDocument);
            T stored = DeserializeCopy(saved);

            CurrentVariant = normalizedVariant;
            _storedDocument = saved;
            return stored;
        }

        public T SavePreset(string presetFile, bool overwrite = false)
        {
            if (string.IsNullOrWhiteSpace(presetFile))
                throw new ArgumentException("Preset file must not be empty.", nameof(presetFile));

            string defaultFile = _definition.GetVariantFile(ConfigDefinition<T>.DefaultVariant);
            if (string.Equals(defaultFile, presetFile, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Preset target must not be the canonical active config file: " + presetFile);

            return _definition.Deserialize(_client.SavePreset(_definition.ConfigKey, Location, defaultFile, presetFile, _defaultsDocument, _appliedDocument, overwrite));
        }

        public T ApplyPreset(string presetFile)
        {
            if (string.IsNullOrWhiteSpace(presetFile))
                throw new ArgumentException("Preset file must not be empty.", nameof(presetFile));

            string defaultFile = _definition.GetVariantFile(ConfigDefinition<T>.DefaultVariant);
            ConfigDocument loaded = _client.ApplyPreset(_definition.ConfigKey, Location, defaultFile, presetFile, _defaultsDocument);
            T draft = DeserializeCopy(loaded);

            CurrentVariant = ConfigDefinition<T>.DefaultVariant;
            _storedDocument = loaded;
            _appliedDocument = loaded;
            _draft = draft;
            return Applied;
        }

        private T DeserializeCopy(ConfigDocument document) => _definition.Deserialize(document);
    }
}
