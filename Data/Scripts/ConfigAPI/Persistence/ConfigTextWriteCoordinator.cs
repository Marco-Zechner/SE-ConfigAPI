using System;
using MarcoZechner.ConfigAPI.Domain;

namespace MarcoZechner.ConfigAPI.Persistence
{
    public sealed class ConfigTextWriteResult
    {
        public string BackupFile { get; }

        internal ConfigTextWriteResult(string backupFile)
        {
            BackupFile = backupFile;
        }
    }

    public sealed class ConfigTextWriteCoordinator
    {
        private readonly IConfigTextStorage _storage;
        private readonly IConfigClock _clock;

        public ConfigTextWriteCoordinator(IConfigTextStorage storage, IConfigClock clock)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            if (clock == null)
                throw new ArgumentNullException(nameof(clock));

            _storage = storage;
            _clock = clock;
        }

        public ConfigTextWriteResult Write(ConfigLocation location, string file, string content, bool requiresBackup)
        {
            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Config file must not be empty.", nameof(file));

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            string backupFile = null;

            if (requiresBackup)
            {
                string original = _storage.Read(location, file);

                if (original != null)
                {
                    backupFile = FindAvailableBackupFile(location, file, _clock.UtcNow);

                    _storage.Write(location, backupFile, original);
                }
            }

            _storage.Write(location, file, content);

            return new ConfigTextWriteResult(backupFile);
        }

        private string FindAvailableBackupFile(ConfigLocation location, string file, DateTime timestampUtc)
        {
            var collisionIndex = 0;

            while (true)
            {
                string candidate = ConfigBackupName.Create(file, timestampUtc, collisionIndex);

                if (_storage.Read(location, candidate) == null)
                    return candidate;

                if (collisionIndex == int.MaxValue)
                    throw new InvalidOperationException("No available ConfigAPI backup file name could be found.");

                collisionIndex++;
            }
        }
    }
}
