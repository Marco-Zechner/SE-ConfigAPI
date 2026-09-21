namespace Mz.Storage
{
    /// <summary>
    /// Provides complete-text access to a physical storage namespace.
    /// </summary>
    public interface IStorageBackend
    {
        /// <summary>
        /// Returns whether the physical file exists.
        /// </summary>
        bool Exists(string fileName);

        /// <summary>
        /// Reads the complete physical file content.
        /// </summary>
        string Read(string fileName);

        /// <summary>
        /// Replaces the complete physical file content.
        /// </summary>
        void Write(string fileName, string content);
    }
}
