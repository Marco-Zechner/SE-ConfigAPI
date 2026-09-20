using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Shared.Domain;

namespace MarcoZechner.ConfigAPI.Main.Core
{
    internal sealed class ConfigInstanceStore
    {
        private readonly Dictionary<string, object> _instances = new Dictionary<string, object>();
        private readonly Dictionary<string, string> _currentFiles = new Dictionary<string, string>();

        private static string Key(string typeKey, LocationType loc) => typeKey + "|" + (int)loc;

        public bool TryGet(string typeKey, LocationType loc, out object instance)
        {
            var res = _instances.TryGetValue(Key(typeKey, loc), out instance);
            return res;
        }

        public void Set(string typeKey, LocationType loc, object instance, string currentFile)
        {
            _instances[Key(typeKey, loc)] = instance;
            _currentFiles[Key(typeKey, loc)] = currentFile;
        }

        public bool TryGetCurrentFile(string typeKey, LocationType loc, out string file)
        {
            var res = _currentFiles.TryGetValue(Key(typeKey, loc), out file);
            return res;
        }
    }
}