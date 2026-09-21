using System;
using Sandbox.ModAPI;

namespace Mz.Storage.SpaceEngineers
{
    internal sealed class GlobalStorageBackend : IStorageBackend
    {
        public bool Exists(string fileName)
        {
            EnsureUtilitiesAvailable();
            return MyAPIGateway.Utilities.FileExistsInGlobalStorage(fileName);
        }

        public string Read(string fileName)
        {
            EnsureUtilitiesAvailable();
            using (var reader = MyAPIGateway.Utilities.ReadFileInGlobalStorage(fileName))
                return reader.ReadToEnd();
        }

        public void Write(string fileName, string content)
        {
            EnsureUtilitiesAvailable();
            using (var writer = MyAPIGateway.Utilities.WriteFileInGlobalStorage(fileName))
                writer.Write(content);
        }

        private static void EnsureUtilitiesAvailable()
        {
            if (MyAPIGateway.Utilities == null)
                throw new InvalidOperationException("Space Engineers utilities are unavailable. Create storage during the mod lifecycle.");
        }
    }
}
