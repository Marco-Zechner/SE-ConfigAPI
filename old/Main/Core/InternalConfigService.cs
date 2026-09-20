using System;
using MarcoZechner.ConfigAPI.Main.Api;
using MarcoZechner.ConfigAPI.Main.Core.Migrator;
using MarcoZechner.ConfigAPI.Main.Domain;
using MarcoZechner.ConfigAPI.Scripts.ConfigAPI.Shared;
using MarcoZechner.ConfigAPI.Shared.Domain;

namespace MarcoZechner.ConfigAPI.Main.Core
{
    /// <summary>
    /// Local/Global/World config handling (no networking).
    /// Uses:
    /// - ConfigUserHooks for file IO + (de)serialize
    /// - IXmlConverter for TOML &lt;-> internal XML
    /// - IConfigLayoutMigrator for default/layout migration
    /// </summary>
    public sealed class InternalConfigService : IInternalConfigService
    {
        private readonly ConfigUserHooks _configUserHooks;
        private readonly IXmlConverter _converter;
        private readonly IConfigLayoutMigrator _migrator;

        private readonly ConfigInstanceStore _instanceStore = new ConfigInstanceStore();

        public InternalConfigService(
            ConfigUserHooks configUserHooks,
            IXmlConverter converter,
            IConfigLayoutMigrator migrator)
        {
            if (configUserHooks == null) throw new ArgumentNullException(nameof(configUserHooks));
            if (converter == null) throw new ArgumentNullException(nameof(converter));
            if (migrator == null) throw new ArgumentNullException(nameof(migrator));

            _configUserHooks = configUserHooks;
            _converter = converter;
            _migrator = migrator;
        }

        private string DefaultFile(string typeKey, string filename)
        {
            if (!string.IsNullOrEmpty(filename))
                return filename;

            return _configUserHooks.DefaultName(typeKey) + ".toml";
        }

        private static string EnsureFileExtension(string filename)
        {
            if (string.IsNullOrEmpty(filename) || filename.EndsWith(".toml", StringComparison.OrdinalIgnoreCase))
                return filename;

            return filename + ".toml";
        }

        private static string DefaultSidecar(string filename)
        {
            if (string.IsNullOrEmpty(filename) || filename.EndsWith(".default.toml", StringComparison.OrdinalIgnoreCase))
                return filename;

            if (filename.EndsWith(".toml", StringComparison.OrdinalIgnoreCase))
                return filename.Substring(0, filename.Length - 5) + ".default.toml";

            return filename + ".default.toml";
        }

        public object ConfigGet(string typeKey, LocationType locationType, string filename, out bool wasCached)
        {
            filename = EnsureFileExtension(filename);

            filename = DefaultFile(typeKey, filename);

            wasCached = true;

            object existing;
            if (_instanceStore.TryGet(typeKey, locationType, out existing))
                return existing;

            wasCached = false;

            var loaded = TryLoad(typeKey, locationType, filename);
            if (loaded != null)
                return loaded;

            var def = _configUserHooks.NewDefault(typeKey);
            if (def == null)
                throw new Exception("ClientConfigGet: NewDefault returned null for " + typeKey);

            SaveToFile(typeKey, locationType, filename, ref def);

            _instanceStore.Set(typeKey, locationType, def, filename);
            return def;
        }

        public object ConfigReload(string typeKey, LocationType locationType)
        {
            string currentFile;
            return _instanceStore.TryGetCurrentFile(typeKey, locationType, out currentFile)
                ? TryLoad(typeKey, locationType, currentFile)
                : null;
        }

        public string ConfigGetCurrentFileName(string typeKey, LocationType locationType)
        {
            string currentFile;
            return _instanceStore.TryGetCurrentFile(typeKey, locationType, out currentFile)
                ? currentFile
                : null;
        }

        public object ConfigLoadAndSwitch(string typeKey, LocationType locationType, string filename)
        {
            filename = EnsureFileExtension(filename);

            filename = DefaultFile(typeKey, filename);
            return TryLoad(typeKey, locationType, filename);
        }

        public bool ConfigSave(string typeKey, LocationType locationType, string xmlOverride = null)
        {
            object instance;
            if (!_instanceStore.TryGet(typeKey, locationType, out instance))
                return false;

            string filename;
            if (!_instanceStore.TryGetCurrentFile(typeKey, locationType, out filename))
                return false;

            SaveToFile(typeKey, locationType, filename, ref instance, xmlOverride);
            _instanceStore.Set(typeKey, locationType, instance, filename);
            return true;
        }

        public object ConfigSaveAndSwitch(string typeKey, LocationType locationType, string filename, string xmlOverride = null)
        {
            filename = EnsureFileExtension(filename);

            filename = DefaultFile(typeKey, filename);

            object instance;
            if (!_instanceStore.TryGet(typeKey, locationType, out instance))
                return null;

            SaveToFile(typeKey, locationType, filename, ref instance, xmlOverride);

            // Keep same instance reference; you can change this later if you want “reload after save”.
            _instanceStore.Set(typeKey, locationType, instance, filename);
            return instance;
        }

        public bool ConfigExport(string typeKey, LocationType locationType, string filename, bool overwrite)
        {
            filename = EnsureFileExtension(filename);

            if (string.IsNullOrEmpty(filename))
                return false;

            object instance;
            if (!_instanceStore.TryGet(typeKey, locationType, out instance))
                return false;

            string current;
            if (_instanceStore.TryGetCurrentFile(typeKey, locationType, out current))
            {
                if (string.Equals(current, filename, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // If file exists, only overwrite if allowed AND type matches.
            var existingExternal = _configUserHooks.LoadFile(locationType, filename);
            if (existingExternal != null)
            {
                if (!overwrite)
                    return false;

                if (!ExternalFileMatchesType(typeKey, existingExternal))
                    return false;
            }

            SaveToFile(typeKey, locationType, filename, ref instance);
            _instanceStore.Set(typeKey, locationType, instance, filename);
            return true;
        }

        // -------------------------
        // Helpers
        // -------------------------

        private object TryLoad(string typeKey, LocationType locationType, string filename)
        {
            filename = EnsureFileExtension(filename);

            var def = new HooksDefinitionMain(_configUserHooks, typeKey);

            // External TOML content
            var externalCurrent = _configUserHooks.LoadFile(locationType, filename);
            var externalDefaults = _configUserHooks.LoadFile(locationType, DefaultSidecar(filename));


            // If missing or unreadable: create defaults, write both files, return default instance.
            if (string.IsNullOrEmpty(externalCurrent))
            {
                if (externalCurrent != null)
                    _configUserHooks.BackupFile(locationType, filename);

                var xmlCurrentDefaults = def.GetCurrentDefaultsInternalXml();

                var external = _converter.ToExternal(def, xmlCurrentDefaults, true);
                _configUserHooks.SaveFile(locationType, filename, external);
                _configUserHooks.SaveFile(locationType, DefaultSidecar(filename), external);

                var instDefault = _configUserHooks.DeserializeFromInternalXml(typeKey, xmlCurrentDefaults);
                _instanceStore.Set(typeKey, locationType, instDefault, filename);
                return instDefault;
            }

            // Convert: external -> internal
            string xmlCurrentFromFile;
            try
            {
                xmlCurrentFromFile = _converter.ToInternal(def, externalCurrent);
            }
            catch
            {
                // invalid external format: backup + recreate from defaults
                _configUserHooks.BackupFile(locationType, filename);

                var xmlCurrentDefaults = def.GetCurrentDefaultsInternalXml();
                var external = _converter.ToExternal(def, xmlCurrentDefaults, true);

                _configUserHooks.SaveFile(locationType, filename, external);
                _configUserHooks.SaveFile(locationType, DefaultSidecar(filename), external);

                var instDefault = _configUserHooks.DeserializeFromInternalXml(typeKey, xmlCurrentDefaults);
                _instanceStore.Set(typeKey, locationType, instDefault, filename);
                return instDefault;
            }

            // Defaults from file may be missing/invalid -> fall back to current defaults.
            string xmlOldDefaultsFromFile = null;
            if (!string.IsNullOrEmpty(externalDefaults))
            {
                try { xmlOldDefaultsFromFile = _converter.ToInternal(def, externalDefaults); }
                catch { xmlOldDefaultsFromFile = null; }
            }

            var xmlCurrentDefaultsFromCode = def.GetCurrentDefaultsInternalXml();
            if (string.IsNullOrEmpty(xmlOldDefaultsFromFile))
                xmlOldDefaultsFromFile = xmlCurrentDefaultsFromCode;

            // Migrate/normalize layout
            var layout = _migrator.Normalize(
                def.TypeName,
                xmlCurrentFromFile,
                xmlOldDefaultsFromFile,
                xmlCurrentDefaultsFromCode);

            // If migration was destructive, backup original file.
            if (layout.RequiresBackup)
                _configUserHooks.BackupFile(locationType, filename);

            // If normalization changed anything, rewrite disk.
            // (We compare internal XML first, then write external TOML.)
            var changedCurrent = !string.Equals(
                LayoutXml.Canonicalize(layout.NormalizedXml),
                LayoutXml.Canonicalize(xmlCurrentFromFile),
                StringComparison.Ordinal);
            var changedDefaults = !string.Equals(
                LayoutXml.Canonicalize(layout.NormalizedDefaultsXml),
                LayoutXml.Canonicalize(xmlOldDefaultsFromFile),
                StringComparison.Ordinal);

            if (changedCurrent || changedDefaults)
            {
                CfgLog.Debug(() => $"{layout.NormalizedXml}\n\n{xmlCurrentFromFile}");
                CfgLog.Debug(() => $"{layout.NormalizedDefaultsXml}\n\n{xmlOldDefaultsFromFile}");

                var newExternalCurrent = _converter.ToExternal(def, layout.NormalizedXml, true);
                var newExternalDefaults = _converter.ToExternal(def, layout.NormalizedDefaultsXml, true);

                _configUserHooks.SaveFile(locationType, filename, newExternalCurrent);
                _configUserHooks.SaveFile(locationType, DefaultSidecar(filename), newExternalDefaults);
            }

            // Deserialize normalized config
            var inst = _configUserHooks.DeserializeFromInternalXml(typeKey, layout.NormalizedXml);
            if (inst == null)
                return null;

            _instanceStore.Set(typeKey, locationType, inst, filename);
            return inst;
        }

        private void SaveToFile(string typeKey, LocationType locationType, string filename, ref object instance, string xmlOverride = null)
        {
            filename = EnsureFileExtension(filename);

            var def = new HooksDefinitionMain(_configUserHooks, typeKey);

            var internalXml = xmlOverride;
            if (xmlOverride == null)
            {
                if (!_configUserHooks.IsInstanceOf(typeKey, instance))
                    throw new Exception("SaveClientToFile: instance/typeKey mismatch: " + typeKey);

                // instance -> internal xml
                internalXml = _configUserHooks.SerializeToInternalXml(typeKey, instance);
            }
            else
            {
                instance = _configUserHooks.DeserializeFromInternalXml(typeKey, internalXml);
            }

            // current defaults XML (code)
            var xmlCurrentDefaults = def.GetCurrentDefaultsInternalXml();

            // externalize both the config and the defaults sidecar
            var external = _converter.ToExternal(def, internalXml, true);
            var externalDefaults = _converter.ToExternal(def, xmlCurrentDefaults, true);

            var loadedFile = _configUserHooks.LoadFile(locationType, filename);
            if (loadedFile != null)
            {
                if (loadedFile.Split(new[] {'\n'}, 2)[0] != external.Split(new[] {'\n'}, 2)[0])
                {
                    _configUserHooks.BackupFile(locationType, filename);
                    CfgLogWorld.Warning($"Potentially overwritten config file from another mod: {filename}.\nA backup was created as '{filename}.bak'.");
                }
            }

            _configUserHooks.SaveFile(locationType, filename, external);
            _configUserHooks.SaveFile(locationType, DefaultSidecar(filename), externalDefaults);
        }

        private bool ExternalFileMatchesType(string typeKey, string existingExternal)
        {
            // The robust way is: ToInternal + check root name == definition.TypeName (or typeKey)
            // We keep this cheap and deterministic.
            var def = new HooksDefinitionMain(_configUserHooks, typeKey);
            try
            {
                var xml = _converter.ToInternal(def, existingExternal);
                // Ensure internal xml root matches type name (not perfect, but good enough)
                string rootName;
                LayoutXml.ParseChildren(xml, out rootName);
                return string.Equals(rootName, def.TypeName, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }
    }
}