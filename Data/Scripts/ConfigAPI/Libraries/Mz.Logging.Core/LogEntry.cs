using System;

namespace Mz.Logging
{
    /// <summary>
    /// Represents one immutable log event.
    /// </summary>
    public sealed class LogEntry
    {
        /// <summary>
        /// Gets the UTC timestamp at which the entry was created.
        /// </summary>
        public DateTime TimestampUtc { get; }

        /// <summary>
        /// Gets the severity of the entry.
        /// </summary>
        public LogLevel Level { get; }

        /// <summary>
        /// Gets the component or mod that produced the entry.
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Gets the log message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the associated exception, when one was supplied.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogEntry"/> class.
        /// </summary>
        /// <param name="timestamp">The UTC timestamp at which the entry was created.</param>
        /// <param name="level">The severity of the entry.</param>
        /// <param name="source">The component or mod that produced the entry.</param>
        /// <param name="message">The log message.</param>
        /// <param name="exception">The associated exception, when one was supplied.</param>
        /// <exception cref="ArgumentException">Thrown when the log level is not defined.</exception>
        /// <exception cref="ArgumentException">Thrown when the log source is invalid.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the log message is null.</exception>
        public LogEntry(DateTime timestamp, LogLevel level, string source, string message, Exception exception)
        {
            if (!IsDefinedLevel(level))
                throw new ArgumentException("The log level is outside the supported range.", nameof(level));

            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("A log source is required.", nameof(source));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            TimestampUtc = timestamp.Kind == DateTimeKind.Utc
                    ? timestamp
                    : timestamp.ToUniversalTime();
            Level = level;
            Source = source.Trim();
            Message = message;
            Exception = exception;
        }

        private static bool IsDefinedLevel(LogLevel level) 
            => level >= LogLevel.Trace && level <= LogLevel.Critical;
    }
}
