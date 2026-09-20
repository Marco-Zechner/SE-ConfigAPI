using System;
using System.IO;
using Sandbox.ModAPI;

namespace Mz.ConfigApi
{
    public interface IConfigApiStorageUtilities
    {
        bool FileExistsInLocalStorage(string file, Type scope);
        string ReadFileInLocalStorage(string file, Type scope);
        void WriteFileInLocalStorage(string file, string content, Type scope);

        bool FileExistsInGlobalStorage(string file);
        string ReadFileInGlobalStorage(string file);
        void WriteFileInGlobalStorage(string file, string content);

        bool FileExistsInWorldStorage(string file, Type scope);
        string ReadFileInWorldStorage(string file, Type scope);
        void WriteFileInWorldStorage(string file, string content, Type scope);
    }

    public sealed class SpaceEngineersConfigTextStorage
    {
        private readonly Type _scope;
        private readonly IConfigApiStorageUtilities _utilities;

        public SpaceEngineersConfigTextStorage(IConfigApiStorageUtilities utilities, Type scope)
        {
            if (utilities == null)
                throw new ArgumentNullException(nameof(utilities));

            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            _utilities = utilities;
            _scope = scope;
        }

        public string Read(int location, string file)
        {
            switch (location)
            {
                case 0: return _utilities.FileExistsInLocalStorage(file, _scope) 
                                   ? _utilities.ReadFileInLocalStorage(file, _scope) : null;

                case 1: return _utilities.FileExistsInGlobalStorage(file) 
                                   ? _utilities.ReadFileInGlobalStorage(file) : null;

                case 2: return _utilities.FileExistsInWorldStorage(file, _scope) 
                                   ? _utilities.ReadFileInWorldStorage(file, _scope) : null;

                default: throw UnsupportedLocation(location);
            }
        }

        public void Write(int location, string file, string content)
        {
            switch (location)
            {
                case 0:
                    _utilities.WriteFileInLocalStorage(file, content, _scope);
                    return;

                case 1:
                    _utilities.WriteFileInGlobalStorage(file, content);
                    return;

                case 2:
                    _utilities.WriteFileInWorldStorage(file, content, _scope);
                    return;

                default:
                    throw UnsupportedLocation(location);
            }
        }

        private static ArgumentException UnsupportedLocation(int location) 
            => new ArgumentException($"Unsupported ConfigAPI storage location: {location}", nameof(location));
    }

    internal sealed class SpaceEngineersConfigApiStorageUtilities : IConfigApiStorageUtilities
    {
        public bool FileExistsInLocalStorage(string file, Type scope) => MyAPIGateway.Utilities.FileExistsInLocalStorage(file, scope);

        public string ReadFileInLocalStorage(string file, Type scope)
        {
            using (TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(file, scope))
            {
                return reader.ReadToEnd();
            }
        }

        public void WriteFileInLocalStorage(string file, string content, Type scope)
        {
            using (TextWriter writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(file, scope))
            {
                writer.Write(content);
            }
        }

        public bool FileExistsInGlobalStorage(string file) => MyAPIGateway.Utilities.FileExistsInGlobalStorage(file);

        public string ReadFileInGlobalStorage(string file)
        {
            using (TextReader reader = MyAPIGateway.Utilities.ReadFileInGlobalStorage(file))
            {
                return reader.ReadToEnd();
            }
        }

        public void WriteFileInGlobalStorage(string file, string content)
        {
            using (TextWriter writer = MyAPIGateway.Utilities.WriteFileInGlobalStorage(file))
            {
                writer.Write(content);
            }
        }

        public bool FileExistsInWorldStorage(string file, Type scope) => MyAPIGateway.Utilities.FileExistsInWorldStorage(file, scope);

        public string ReadFileInWorldStorage(string file, Type scope)
        {
            using (TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(file, scope))
            {
                return reader.ReadToEnd();
            }
        }

        public void WriteFileInWorldStorage(string file, string content, Type scope)
        {
            using (TextWriter writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(file, scope))
            {
                writer.Write(content);
            }
        }
    }
}
