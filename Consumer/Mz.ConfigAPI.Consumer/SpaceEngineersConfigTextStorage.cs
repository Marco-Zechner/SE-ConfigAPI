using System;
using Mz.Storage;
using Mz.Storage.SpaceEngineers;

namespace Mz.ConfigApi
{
    public sealed class SpaceEngineersConfigTextStorage
    {
        private readonly IndexedStorage _local;
        private readonly IndexedStorage _global;
        private readonly IndexedStorage _world;

        public SpaceEngineersConfigTextStorage(IndexedStorage local, IndexedStorage global, IndexedStorage world)
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

        public static SpaceEngineersConfigTextStorage Create(string ownerPrefix, Type callingType)
        {
            if (string.IsNullOrWhiteSpace(ownerPrefix))
                throw new ArgumentException("A stable storage owner prefix is required.", nameof(ownerPrefix));

            if (callingType == null)
                throw new ArgumentNullException(nameof(callingType));

            return new SpaceEngineersConfigTextStorage(
                SpaceEngineersStorage.CreateLocal(callingType),
                SpaceEngineersStorage.CreateGlobal(ownerPrefix),
                SpaceEngineersStorage.CreateWorld(callingType));
        }

        public bool Exists(int location, string file) => GetStorage(location).Exists(file);

        public string Read(int location, string file)
        {
            IndexedStorage storage = GetStorage(location);
            return storage.Exists(file) ? storage.Load(file) : null;
        }

        public void Write(int location, string file, string content) => GetStorage(location).Save(file, content);

        public string[] ListKnown(int location) => GetStorage(location).ListKnown();

        private IndexedStorage GetStorage(int location)
        {
            switch (location)
            {
                case 0: return _local;
                case 1: return _global;
                case 2: return _world;
                default: throw new ArgumentException($"Unsupported ConfigAPI storage location: {location}", nameof(location));
            }
        }
    }
}
