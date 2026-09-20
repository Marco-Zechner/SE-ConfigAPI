using System;

namespace Mz.Logging
{
    /// <summary>
    /// Creates log entries and dispatches enabled entries to a sink.
    /// </summary>
    public sealed class Logger
    {
        private readonly ILogSink _sink;
        private readonly Func<DateTime> _utcNow;
        private LogLevel _minimumLevel;

        /// <summary>
        /// Gets the source attached to entries produced by this logger.
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public string Source { get; }

        /// <summary>
        /// Gets or sets the minimum severity dispatched to the sink.
        /// </summary>
        public LogLevel MinimumLevel
        {
            get
            {
                return _minimumLevel;
            }
            set
            {
                ValidateLevel(value, nameof(value));
                _minimumLevel = value;
            }
        }

        
        /// <summary>
        /// Initializes a new instance of the <see cref="Logger"/> class.
        /// </summary>
        /// <param name="source">The source attached to entries produced by this logger.</param>
        /// <param name="sink">The sink to which enabled entries are dispatched.</param>
        /// <param name="minimumLevel">The minimum severity dispatched to the sink.</param>
        public Logger(string source, ILogSink sink, LogLevel minimumLevel) 
            : this(source, sink, minimumLevel, GetUtcNow) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Logger"/> class.
        /// </summary>
        /// <param name="source">The source attached to entries produced by this logger.</param>
        /// <param name="sink">The sink to which enabled entries are dispatched.</param>
        /// <param name="minimumLevel">The minimum severity dispatched to the sink.</param>
        /// <param name="utcNow">A function that returns the current UTC time.</param>
        // ReSharper disable once MemberCanBePrivate.Global
        public Logger(string source, ILogSink sink, LogLevel minimumLevel, Func<DateTime> utcNow)
        {
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("A logger source is required.", nameof(source));

            if (sink == null)
                throw new ArgumentNullException(nameof(sink));

            if (utcNow == null)
                throw new ArgumentNullException(nameof(utcNow));

            ValidateLevel(minimumLevel, nameof(minimumLevel));

            Source = source.Trim();
            _sink = sink;
            _utcNow = utcNow;
            _minimumLevel = minimumLevel;
        }

        /// <summary>
        /// Determines whether an entry at the given level would be written.
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public bool IsEnabled(LogLevel level)
        {
            ValidateLevel(level, nameof(level));
            return level >= MinimumLevel;
        }

        /// <summary>
        /// Writes a trace-level message.
        /// </summary>
        public void Trace(string message, Exception exception = null) 
            => Write(LogLevel.Trace, message, exception);

        /// <summary>
        /// Writes a debug-level message.
        /// </summary>
        public void Debug(string message, Exception exception = null) 
            => Write(LogLevel.Debug, message, exception);

        /// <summary>
        /// Writes an information-level message.
        /// </summary>
        public void Info(string message, Exception exception = null) 
            => Write(LogLevel.Information, message, exception);

        /// <summary>
        /// Writes a warning-level message.
        /// </summary>
        public void Warning(string message, Exception exception = null) 
            => Write(LogLevel.Warning, message, exception);

        /// <summary>
        /// Writes an error-level message.
        /// </summary>
        public void Error(string message, Exception exception = null) 
            => Write(LogLevel.Error, message, exception);

        /// <summary>
        /// Writes a critical-level message.
        /// </summary>
        public void Critical(string message, Exception exception = null) 
            => Write(LogLevel.Critical, message, exception);

        /// <summary>
        /// Creates and dispatches a log entry when its level is enabled.
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public void Write(LogLevel level, string message, Exception exception = null)
        {
            ValidateLevel(level, nameof(level));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (!IsEnabled(level))
                return;

            _sink.Write(new LogEntry(_utcNow(), level, Source, message, exception));
        }

        private static DateTime GetUtcNow() => DateTime.UtcNow;

        private static void ValidateLevel(LogLevel level, string parameterName)
        {
            if (level < LogLevel.Trace || level > LogLevel.Critical)
                throw new ArgumentException("The log level is outside the supported range.", parameterName);
        }
    }
}
