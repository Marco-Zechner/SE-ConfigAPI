using System;
using MarcoZechner.ConfigAPI.V2.Domain;

namespace MarcoZechner.ConfigAPI.V2.Persistence
{
    public sealed class ConfigCallbackTextStorage : IConfigTextStorage
    {
        private readonly Func<int, string, string> _read;
        private readonly Action<int, string, string> _write;

        public ConfigCallbackTextStorage(Func<int, string, string> read, Action<int, string, string> write)
        {
            if (read == null)
                throw new ArgumentNullException(nameof(read));

            if (write == null)
                throw new ArgumentNullException(nameof(write));

            _read = read;
            _write = write;
        }

        public string Read(ConfigLocation location, string file) => _read((int)location, file);

        public void Write(ConfigLocation location, string file, string content) => _write((int)location, file, content);
    }
}
