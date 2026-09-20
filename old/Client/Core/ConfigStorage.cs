using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Client.Api;
using MarcoZechner.ConfigAPI.Scripts.ConfigAPI.Shared;
using MarcoZechner.ConfigAPI.Shared.Domain;
using VRage.Game.ModAPI;

namespace MarcoZechner.ConfigAPI.Client.Core
{
    public static class ConfigStorage
    {
        // Keep ONLY world handles cached (they hold state like iteration/currentFile/draft)
        private static readonly Dictionary<string, object> _worldCache
            = new Dictionary<string, object>(StringComparer.Ordinal);

        private static string _modName = "UnknownMod";

        private static string TypeKey<T>() where T : ConfigBase => $"{_modName}:{typeof(T).FullName}";

        private static void EnsureApiLoaded()
        {
            if (ServiceLoader.ApiLoaded) return;

            CfgLogWorld.Error("ConfigStorage: ConfigAPI is not loaded. Call ConfigStorage.Init(modContext) first.");
            throw new InvalidOperationException(
                "ConfigStorage: ConfigAPI is not loaded. Call ConfigStorage.Init(modContext) first.");
        }

        private static void EnsureRegistered<T>(string typeKey)
            where T : ConfigBase, new()
        {
            // Location independent; only once per T per session.
            if (ConfigUserHooksImpl.IsRegistered(typeKey))
                return;

            ConfigUserHooksImpl.Register<T>();
        }

        public static void Init(IMyModContext modContext)
        {
            _modName = modContext.ModName;
            ServiceLoader.Init(modContext.ModItem.PublishedFileId, modContext.ModName);
        }

        public static void Unload()
        {
            _worldCache.Clear();
            ServiceLoader.Unload();
        }

        /// <summary>
        /// Local/Global: always delegate to provider (provider owns caching and can swap instances).
        /// </summary>
        public static T Get<T>(LocationType location, string name = null) //TODO: maybe move into ConfigBase? to hide __Bind?
            where T : ConfigBase, new()
        {
            EnsureApiLoaded();

            var typeKey = TypeKey<T>();

            EnsureRegistered<T>(typeKey);

            var obj = ServiceLoader.Service.ClientConfigGet(typeKey, location, name);
            if (obj == null)
                throw new Exception("ConfigStorage.Get: ClientConfigGet returned null for " + typeKey);

            var cfg = (T)obj;

            // Bind runtime so cfg.Save()/LoadAndSwitch()/Export() works.
            cfg.__Bind(typeKey, location);

            return cfg;
        }

        public static CfgSync<T> World<T>(string defaultFile = null)
            where T : ConfigBase, new()
        {
            var typeKey = TypeKey<T>();

            EnsureRegistered<T>(typeKey);

            object existing;
            if (_worldCache.TryGetValue(typeKey, out existing))
                return (CfgSync<T>)existing;

            var sync = new CfgSync<T>(defaultFile);
            _worldCache[typeKey] = sync;
            return sync;
        }
    }
}