using System;
using MarcoZechner.ConfigAPI.V2.Domain;
using Mz.Storage;
using Mz.Storage.SpaceEngineers;

namespace MarcoZechner.ConfigAPI.V2.Persistence
{
    internal sealed class ConfigApiSpaceEngineersTextStorage : IIndexedConfigTextStorage
    {
        private readonly IndexedStorage _local;
        private readonly IndexedStorage _global;
        private readonly IndexedStorage _world;

        private ConfigApiSpaceEngineersTextStorage(IndexedStorage local, IndexedStorage global, IndexedStorage world)
        {
            if (local == null)
                throw new ArgumentNullException(nameof(local));
            if (global == null)
                throw new ArgumentNullException(nameof(global));
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            _local = local;
            _global = global;
            _world = world;
        }

        public static ConfigApiSpaceEngineersTextStorage Create(string ownerPrefix, Type callingType)
        {
            if (string.IsNullOrWhiteSpace(ownerPrefix))
                throw new ArgumentException("A stable storage owner prefix is required.", nameof(ownerPrefix));
            if (callingType == null)
                throw new ArgumentNullException(nameof(callingType));

            return new ConfigApiSpaceEngineersTextStorage(
                SpaceEngineersStorage.CreateLocal(callingType),
                SpaceEngineersStorage.CreateGlobal(ownerPrefix),
                SpaceEngineersStorage.CreateWorld(callingType));
        }

        public bool Exists(ConfigLocation location, string file) => GetStorage(location).Exists(file);

        public string Read(ConfigLocation location, string file)
        {
            IndexedStorage storage = GetStorage(location);
            return storage.Exists(file) ? storage.Load(file) : null;
        }

        public void Write(ConfigLocation location, string file, string content) => GetStorage(location).Save(file, content);

        public string[] ListKnown(ConfigLocation location) => GetStorage(location).ListKnown();

        private IndexedStorage GetStorage(ConfigLocation location)
        {
            switch (location)
            {
                case ConfigLocation.Local: return _local;
                case ConfigLocation.Global: return _global;
                case ConfigLocation.World: return _world;
                default: throw new ArgumentException("Unsupported ConfigAPI storage location: " + location, nameof(location));
            }
        }
    }
}