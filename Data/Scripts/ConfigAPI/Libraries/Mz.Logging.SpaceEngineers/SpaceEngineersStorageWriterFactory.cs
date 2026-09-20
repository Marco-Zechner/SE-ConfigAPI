using System;
using System.IO;
using Sandbox.ModAPI;

namespace Mz.Logging.SpaceEngineers
{
    /// <summary>
    /// Opens text writers through the Space Engineers ModAPI.
    /// </summary>
    public sealed class SpaceEngineersStorageWriterFactory : IStorageWriterFactory
    {
        /// <summary>
        /// Opens a local storage writer.
        /// %APPDATA%/Roaming/SpaceEngineers/Storage/{Assembly Scope}/
        /// </summary>
        /// <param name="fileName">The name of the file to open.</param>
        /// <param name="callingType">The type of the calling class.</param>
        /// <returns>The text writer for the local storage file.</returns>
        public TextWriter OpenLocal(string fileName, Type callingType)
        {
            ValidateScopedArguments(fileName, callingType);

            EnsureUtilitiesAvailable();

            return MyAPIGateway.Utilities.WriteFileInLocalStorage(fileName.Trim(), callingType);
        }

        /// <summary>
        /// Opens a world storage writer.
        /// %APPDATA%/Roaming/SpaceEngineers/Save/{SteamId}/{WorldName}/Storage/{Assembly Scope}/
        /// </summary>
        /// <param name="fileName">The name of the file to open.</param>
        /// <param name="callingType">The type of the calling class.</param>
        /// <returns>The text writer for the world storage file.</returns>
        public TextWriter OpenWorld(string fileName, Type callingType)
        {
            ValidateScopedArguments(fileName, callingType);

            EnsureUtilitiesAvailable();

            return MyAPIGateway.Utilities.WriteFileInWorldStorage(fileName.Trim(), callingType);
        }

        /// <summary>
        /// Opens a global storage writer.
        /// %APPDATA%/Roaming/SpaceEngineers/Storage/
        /// </summary>
        /// <param name="fileName">The name of the file to open.</param>
        /// <returns>The text writer for the global storage file.</returns>
        public TextWriter OpenGlobal(string fileName)
        {
            ValidateFileName(fileName);
            EnsureUtilitiesAvailable();

            return MyAPIGateway.Utilities.WriteFileInGlobalStorage(fileName.Trim());
        }

        /// <summary>
        /// Validates the arguments for scoped storage operations.
        /// </summary>
        /// <param name="fileName">The name of the file to open.</param>
        /// <param name="callingType">The type of the calling class.</param>
        private static void ValidateScopedArguments(string fileName, Type callingType)
        {
            ValidateFileName(fileName);

            if (callingType == null)
                throw new ArgumentNullException(nameof(callingType));
        }

        /// <summary>
        /// Validates the log file name.
        /// </summary>
        /// <param name="fileName">The name of the file to open.</param>
        private static void ValidateFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("A log file name is required.", nameof(fileName));
        }

        /// <summary>
        /// Ensures that the Space Engineers utilities are available.
        /// </summary>
        private static void EnsureUtilitiesAvailable()
        {
            if (MyAPIGateway.Utilities == null)
                throw new InvalidOperationException(
                    "Space Engineers utilities are unavailable. " +
                    "Create the logger during the mod lifecycle, not from a static initializer."
                );
        }
    }
}