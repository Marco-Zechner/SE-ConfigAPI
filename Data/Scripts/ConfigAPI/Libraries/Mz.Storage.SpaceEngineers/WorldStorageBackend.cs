using System;
using Sandbox.ModAPI;

namespace Mz.Storage.SpaceEngineers
{
    internal sealed class WorldStorageBackend : IStorageBackend
    {
        private readonly Type _callingType;

        public WorldStorageBackend(Type callingType)
        {
            _callingType = callingType;
        }

        public bool Exists(string fileName)
        {
            EnsureUtilitiesAvailable();
            return MyAPIGateway.Utilities.FileExistsInWorldStorage(fileName, _callingType);
        }

        public string Read(string fileName)
        {
            EnsureUtilitiesAvailable();
            using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(fileName, _callingType))
                return reader.ReadToEnd();
        }

        public void Write(string fileName, string content)
        {
            EnsureUtilitiesAvailable();
            using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(fileName, _callingType))
                writer.Write(content);
        }

        private static void EnsureUtilitiesAvailable()
        {
            if (MyAPIGateway.Utilities == null)
                throw new InvalidOperationException("Space Engineers utilities are unavailable. Create storage during the mod lifecycle.");
        }
    }
}
