using System;
using System.IO;

namespace Mz.Logging.SpaceEngineers
{
    /// <summary>
    /// Owns a logger and its Space Engineers storage writer.
    /// </summary>
    public sealed class SpaceEngineersStorageLogger : IDisposable
    {
        private readonly TextWriterLogSink _sink;
        private bool _isDisposed;

        /// <summary>
        /// Gets the logger used to write entries.
        /// </summary>
        public Logger Logger { get; }

        /// <summary>
        /// Creates a logger in assembly-scoped local storage.
        /// %APPDATA%/Roaming/SpaceEngineers/Storage/{Assembly Scope}/
        /// </summary>
        public static SpaceEngineersStorageLogger CreateLocal(string fileName, Type callingType, string source, LogLevel minimumLevel) =>
            CreateLocal(new SpaceEngineersStorageWriterFactory(), new PlainTextLogFormatter(), fileName, callingType, source, minimumLevel);

        /// <summary>
        /// Creates a logger in assembly-scoped world storage.
        /// %APPDATA%/Roaming/SpaceEngineers/Save/{SteamId}/{WorldName}/Storage/{Assembly Scope}/
        /// </summary>
        public static SpaceEngineersStorageLogger CreateWorld(string fileName, Type callingType, string source, LogLevel minimumLevel) =>
            CreateWorld(new SpaceEngineersStorageWriterFactory(), new PlainTextLogFormatter(), fileName, callingType, source, minimumLevel);
        
        /// <summary>
        /// Creates a logger in shared global storage.
        /// %APPDATA%/Roaming/SpaceEngineers/Storage/
        /// </summary>
        /// <remarks>
        /// Global storage is not assembly-scoped. Use a uniquely prefixed
        /// filename to avoid collisions with other mods.
        /// </remarks>
        public static SpaceEngineersStorageLogger CreateGlobalUnsafe(string fileName, string source, LogLevel minimumLevel) =>
            CreateGlobalUnsafe(new SpaceEngineersStorageWriterFactory(), new PlainTextLogFormatter(), fileName, source, minimumLevel);

        /// <summary>
        /// Creates a local-storage logger using supplied services.
        /// %APPDATA%/Roaming/SpaceEngineers/Storage/{Assembly Scope}/
        /// </summary>
        public static SpaceEngineersStorageLogger CreateLocal(
            IStorageWriterFactory writerFactory, ILogFormatter formatter, string fileName,
            Type callingType, string source, LogLevel minimumLevel
        )
        {
            ValidateCommonArguments(writerFactory, formatter, fileName);

            if (callingType == null)
                throw new ArgumentNullException(nameof(callingType));

            var writer = writerFactory.OpenLocal(fileName.Trim(), callingType);
            return CreateOwnedLogger(writer, formatter, source, minimumLevel);
        }

        /// <summary>
        /// Creates a world-storage logger using supplied services.
        /// %APPDATA%/Roaming/SpaceEngineers/Save/{SteamId}/{WorldName}/Storage/{Assembly Scope}/
        /// </summary>
        public static SpaceEngineersStorageLogger CreateWorld(
            IStorageWriterFactory writerFactory, ILogFormatter formatter, string fileName,
            Type callingType, string source, LogLevel minimumLevel
        )
        {
            ValidateCommonArguments(writerFactory, formatter, fileName);

            if (callingType == null)
                throw new ArgumentNullException(nameof(callingType));

            var writer = writerFactory.OpenWorld(fileName.Trim(), callingType);
            return CreateOwnedLogger(writer, formatter, source, minimumLevel);
        }

        /// <summary>
        /// Creates a global-storage logger using supplied services.
        /// %APPDATA%/Roaming/SpaceEngineers/Storage/
        /// </summary>
        public static SpaceEngineersStorageLogger CreateGlobalUnsafe(
            IStorageWriterFactory writerFactory, ILogFormatter formatter, string fileName,
            string source, LogLevel minimumLevel
        )
        {
            ValidateCommonArguments(writerFactory, formatter, fileName);

            var writer = writerFactory.OpenGlobal(fileName.Trim());
            return CreateOwnedLogger(writer, formatter, source, minimumLevel);
        }

        private SpaceEngineersStorageLogger(TextWriter writer, ILogFormatter formatter, string source, LogLevel minimumLevel)
        {
            _sink = new TextWriterLogSink(writer, formatter);

            try
            {
                Logger = new Logger(source, _sink, minimumLevel);
            }
            catch
            {
                _sink.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Flushes pending output to the owned writer.
        /// </summary>
        public void Flush()
        {
            ThrowIfDisposed();
            _sink.Flush();
        }

        /// <summary>
        /// Flushes and disposes the owned writer.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _sink.Dispose();
        }

        private static SpaceEngineersStorageLogger CreateOwnedLogger(TextWriter writer, ILogFormatter formatter, string source, LogLevel minimumLevel)
        {
            if (writer == null)
                throw new InvalidOperationException("The storage writer factory returned null.");

            return new SpaceEngineersStorageLogger(writer, formatter, source, minimumLevel);
        }

        private static void ValidateCommonArguments(IStorageWriterFactory writerFactory, ILogFormatter formatter, string fileName)
        {
            if (writerFactory == null)
                throw new ArgumentNullException(nameof(writerFactory));

            if (formatter == null)
                throw new ArgumentNullException(nameof(formatter));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("A log file name is required.", nameof(fileName));
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new InvalidOperationException("The Space Engineers storage logger has been disposed.");
        }
    }
}
