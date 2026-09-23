using System;
using System.Collections.Generic;
using System.IO;

namespace Mz.Storage
{
    /// <summary>
    /// Provides indexed logical-name access over a storage backend.
    /// </summary>
    public sealed class IndexedStorage
    {
        /// <summary>
        /// Gets the reserved logical and default physical filename used for the persisted logical-name index.
        /// </summary>
        public const string IndexFileName = ".index";

        private readonly IStorageBackend _backend;
        private readonly string _physicalPrefix;
        private readonly string _physicalLeadingDotPrefix;
        private readonly string _physicalIndexName;

        /// <summary>
        /// Creates indexed storage without a physical filename prefix.
        /// </summary>
        public IndexedStorage(IStorageBackend backend) : this(backend, string.Empty, string.Empty, IndexFileName)
        {
        }

        /// <summary>
        /// Creates indexed storage with a prefix applied to every physical filename.
        /// </summary>
        public IndexedStorage(IStorageBackend backend, string physicalPrefix) : this(backend, physicalPrefix, physicalPrefix, physicalPrefix + IndexFileName)
        {
        }

        /// <summary>
        /// Creates indexed storage with independent physical data-prefix and index-filename mappings.
        /// </summary>
        public IndexedStorage(IStorageBackend backend, string physicalPrefix, string physicalIndexName) : this(backend, physicalPrefix, physicalPrefix, physicalIndexName)
        {
        }

        /// <summary>
        /// Creates indexed storage with separate physical prefixes for regular and leading-dot logical names.
        /// </summary>
        public IndexedStorage(IStorageBackend backend, string physicalPrefix, string physicalLeadingDotPrefix, string physicalIndexName)
        {
            if (backend == null)
                throw new ArgumentNullException(nameof(backend));

            if (physicalPrefix == null)
                throw new ArgumentNullException(nameof(physicalPrefix));

            if (physicalLeadingDotPrefix == null)
                throw new ArgumentNullException(nameof(physicalLeadingDotPrefix));

            if (string.IsNullOrWhiteSpace(physicalIndexName))
                throw new ArgumentException("A physical index filename is required.", nameof(physicalIndexName));

            _backend = backend;
            _physicalPrefix = physicalPrefix;
            _physicalLeadingDotPrefix = physicalLeadingDotPrefix;
            _physicalIndexName = physicalIndexName;
        }

        /// <summary>
        /// Returns whether the logical file currently exists.
        /// </summary>
        public bool Exists(string name)
        {
            ValidateName(name);
            return _backend.Exists(GetPhysicalName(name));
        }

        /// <summary>
        /// Loads the complete logical file content and indexes a previously unknown existing file.
        /// </summary>
        public string Load(string name)
        {
            ValidateName(name);

            var physicalName = GetPhysicalName(name);
            if (!_backend.Exists(physicalName))
                throw new FileNotFoundException("The storage file does not exist.", name);

            var content = _backend.Read(physicalName);
            AddKnown(name);
            return content;
        }

        /// <summary>
        /// Saves complete logical file content and adds the logical name to the persisted index.
        /// </summary>
        public void Save(string name, string content)
        {
            ValidateName(name);

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            _backend.Write(GetPhysicalName(name), content);
            AddKnown(name);
        }

        /// <summary>
        /// Returns known logical names after pruning invalid or missing persisted entries.
        /// </summary>
        public string[] ListKnown()
        {
            bool repairNeeded;
            var names = ReadIndex(out repairNeeded);
            var staleNames = new List<string>();

            foreach (var name in names)
            {
                if (!_backend.Exists(GetPhysicalName(name)))
                    staleNames.Add(name);
            }

            foreach (var name in staleNames)
                names.Remove(name);

            if (repairNeeded || staleNames.Count > 0)
                WriteIndex(names);

            return ToSortedArray(names);
        }

        private void AddKnown(string name)
        {
            bool repairNeeded;
            var names = ReadIndex(out repairNeeded);
            var added = names.Add(name);

            if (added || repairNeeded)
                WriteIndex(names);
        }

        private HashSet<string> ReadIndex(out bool repairNeeded)
        {
            repairNeeded = false;
            var names = new HashSet<string>(StringComparer.Ordinal);

            if (!_backend.Exists(_physicalIndexName))
                return names;

            var content = _backend.Read(_physicalIndexName);
            if (string.IsNullOrEmpty(content))
                return names;

            var entries = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];

                if (entry.Length == 0)
                {
                    if (index < entries.Length - 1)
                        repairNeeded = true;

                    continue;
                }

                if (!IsValidName(entry) || !names.Add(entry))
                    repairNeeded = true;
            }

            return names;
        }

        private void WriteIndex(HashSet<string> names)
        {
            var sorted = ToSortedArray(names);
            var content = sorted.Length == 0 ? string.Empty : string.Join("\n", sorted) + "\n";
            _backend.Write(_physicalIndexName, content);
        }

        private static string[] ToSortedArray(HashSet<string> names)
        {
            var result = new string[names.Count];
            names.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private string GetPhysicalName(string name) => name[0] == '.' ? _physicalLeadingDotPrefix + name.Substring(1) : _physicalPrefix + name;

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A logical storage name is required.", nameof(name));

            if (!IsValidName(name))
                throw new ArgumentException("The logical storage name is reserved or contains a line break.", nameof(name));
        }

        private static bool IsValidName(string name) =>
            !string.IsNullOrWhiteSpace(name) && name != IndexFileName && name.IndexOf('\r') < 0 && name.IndexOf('\n') < 0;
    }
}
