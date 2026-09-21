using System;
using MarcoZechner.ConfigAPI.V2.Domain;

namespace MarcoZechner.ConfigAPI.V2.Persistence
{
    public sealed class ConfigCallbackTextStorage : IIndexedConfigTextStorage
    {
        private readonly Func<int, string, bool> _exists;
        private readonly Func<int, string, string> _read;
        private readonly Action<int, string, string> _write;
        private readonly Func<int, string[]> _listKnown;

        public ConfigCallbackTextStorage(Func<int, string, bool> exists, Func<int, string, string> read, Action<int, string, string> write, Func<int, string[]> listKnown)
        {
            if (exists == null)
                throw new ArgumentNullException(nameof(exists));

            if (read == null)
                throw new ArgumentNullException(nameof(read));

            if (write == null)
                throw new ArgumentNullException(nameof(write));

            if (listKnown == null)
                throw new ArgumentNullException(nameof(listKnown));

            _exists = exists;
            _read = read;
            _write = write;
            _listKnown = listKnown;
        }

        public bool Exists(ConfigLocation location, string file) => _exists((int)location, file);

        public string Read(ConfigLocation location, string file) => _read((int)location, file);

        public void Write(ConfigLocation location, string file, string content) => _write((int)location, file, content);

        public string[] ListKnown(ConfigLocation location)
        {
            string[] names = _listKnown((int)location);
            return names ?? new string[0];
        }
    }
}
