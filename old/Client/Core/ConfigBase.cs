using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using MarcoZechner.ConfigAPI.Client.Api;
using MarcoZechner.ConfigAPI.Shared.Domain;

namespace MarcoZechner.ConfigAPI.Client.Core
{
    /// <summary>
    /// Variables in your config should either be ALL public properties with getters and setters, or ALL public fields.
    /// Mixing fields and properties can lead to the XML serializer reordering stuff.
    /// </summary>
    public abstract class ConfigBase
    {
        [XmlIgnore]
        public abstract string ConfigVersion { get; }

        [XmlElement("ConfigVersion")]
        public string ConfigVersionSerialized
        {
            get { return ConfigVersion; }
            set { /* Needed for XML serialization */ }
        }

        public virtual string ConfigNameOverride => GetType().Name;

        // Name -> description, per *property* name
        public virtual IReadOnlyDictionary<string, string> VariableDescriptions
            => _emptyDescriptions;

        private static readonly IReadOnlyDictionary<string, string> _emptyDescriptions
            = new Dictionary<string, string>();

        /// <summary>
        /// Called when the framework creates a default instance.
        /// Implementations should apply default values to collections etc.
        /// Simple types like int, bool, string can be initialized inline, but Collections cause issues with XML serialization.
        /// So these should be initialized here. (Simple types can also be initialized here if desired.)
        /// </summary>
        public virtual void ApplyDefaults() { }

        // ==========================================================
        // Runtime binding (UserMod-side only)
        // ==========================================================

        [XmlIgnore]
        private string _typeKey;

        [XmlIgnore]
        private LocationType _location;

        internal void __Bind(string typeKey, LocationType location)
        {
            _typeKey = typeKey;
            _location = location;
        }

        private void EnsureBound()
        {
            if (string.IsNullOrEmpty(_typeKey))
                throw new InvalidOperationException(
                    "ConfigBase: This instance is not bound. Obtain it via ConfigStorage.Get<T>(...).");
        }

        private static ConfigService Service
        {
            get
            {
                var svc = ServiceLoader.Service;
                if (svc == null)
                    throw new InvalidOperationException("ConfigBase: ConfigAPI service not available.");
                return svc;
            }
        }

        // ==========================================================
        // User-facing helpers (Local/Global only)
        // ==========================================================

        /// <summary>
        /// Save to the current file on the provider side.
        /// </summary>
        public bool Save()
        {
            EnsureBound();
            return Service.ClientConfigSave(_typeKey, _location);
        }

        public T Reload<T>() where T : ConfigBase, new()
        {
            EnsureBound();
            var obj = Service.ClientConfigReload(_typeKey, _location);
            if (obj == null) return null;

            var cfg = (T)obj;
            cfg.__Bind(_typeKey, _location);
            return cfg;
        }

        public string CurrentFile {
            get
            {
                EnsureBound();
                return Service.ClientConfigGetCurrentFileName(_typeKey, _location);
            }
        }

        /// <summary>
        /// Force reload from disk and replace the provider instance.
        /// Returns the new instance (bound), or null if load failed.
        /// </summary>
        public T LoadAndSwitch<T>(string filename) where T : ConfigBase, new()
        {
            EnsureBound();
            var obj = Service.ClientConfigLoadAndSwitch(_typeKey, _location, filename);
            if (obj == null) return null;

            var cfg = (T)obj;
            cfg.__Bind(_typeKey, _location);
            return cfg;
        }

        /// <summary>
        /// Save to filename and switch current file.
        /// Returns the new instance (bound), or null if save failed.
        /// </summary>
        public T SaveAndSwitch<T>(string filename) where T : ConfigBase, new()
        {
            EnsureBound();
            var obj = Service.ClientConfigSaveAndSwitch(_typeKey, _location, filename);
            if (obj == null) return null;

            var cfg = (T)obj;
            cfg.__Bind(_typeKey, _location);
            return cfg;
        }

        public bool Export(string filename, bool overwrite = false)
        {
            EnsureBound();
            return Service.ClientConfigExport(_typeKey, _location, filename, overwrite);
        }
    }
}