using System;

namespace Mz.Storage.SpaceEngineers
{
    /// <summary>
    /// Creates indexed storage over Space Engineers local, world, and global storage.
    /// </summary>
    public static class SpaceEngineersStorage
    {
        /// <summary>
        /// Creates indexed assembly-scoped local storage.
        /// </summary>
        public static IndexedStorage CreateLocal(Type callingType)
        {
            if (callingType == null)
                throw new ArgumentNullException(nameof(callingType));

            return new IndexedStorage(new LocalStorageBackend(callingType));
        }

        /// <summary>
        /// Creates indexed assembly-scoped storage in the active world.
        /// </summary>
        public static IndexedStorage CreateWorld(Type callingType)
        {
            if (callingType == null)
                throw new ArgumentNullException(nameof(callingType));

            return new IndexedStorage(new WorldStorageBackend(callingType));
        }

        /// <summary>
        /// Creates indexed shared global storage with physical filenames namespaced by the supplied owner.
        /// </summary>
        public static IndexedStorage CreateGlobal(string ownerPrefix)
        {
            if (string.IsNullOrWhiteSpace(ownerPrefix))
                throw new ArgumentException("A stable global storage owner prefix is required.", nameof(ownerPrefix));

            var owner = ownerPrefix.Trim().TrimEnd('.');
            if (string.IsNullOrWhiteSpace(owner))
                throw new ArgumentException("A stable global storage owner prefix is required.", nameof(ownerPrefix));

            return new IndexedStorage(new GlobalStorageBackend(), owner + ".", "." + owner + IndexedStorage.IndexFileName);
        }
    }
}