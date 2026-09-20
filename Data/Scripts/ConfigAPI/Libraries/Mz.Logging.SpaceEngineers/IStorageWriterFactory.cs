using System;
using System.IO;

namespace Mz.Logging.SpaceEngineers
{
    /// <summary>
    /// Opens writers in Space Engineers storage locations.
    /// </summary>
    public interface IStorageWriterFactory
    {
        /// <summary>
        /// Opens the file writer for %APPDATA%/Roaming/SpaceEngineers/Storage/{Assembly Scope}/
        /// </summary>
        /// <param name="fileName">The name of the file to open.</param>
        /// <param name="callingType">The type of the calling class.</param>
        /// <returns>The text writer for the specified file.</returns>
        TextWriter OpenLocal(string fileName, Type callingType);

        /// <summary>
        /// Opens the file writer for %APPDATA%/Roaming/SpaceEngineers/Save/{SteamId}/{WorldName}/Storage/{Assembly Scope}/
        /// </summary>
        /// <param name="fileName">The name of the file to open.</param>
        /// <param name="callingType">The type of the calling class.</param>
        /// <returns>The text writer for the specified file.</returns>
        TextWriter OpenWorld(string fileName, Type callingType);

        /// <summary>
        /// Opens the global writer for %APPDATA%/Roaming/SpaceEngineers/Storage/
        /// </summary>
        /// <param name="fileName">The name of the file to open.</param>
        /// <returns>The text writer for the specified file.</returns>
        TextWriter OpenGlobal(string fileName);
    }
}