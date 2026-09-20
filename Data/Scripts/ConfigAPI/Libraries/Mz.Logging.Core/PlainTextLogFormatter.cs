using System;
using System.Globalization;

namespace Mz.Logging
{
    /// <summary>
    /// Formats log entries as deterministic human-readable text.
    /// </summary>
    public sealed class PlainTextLogFormatter : ILogFormatter
    {
        /// <summary>
        /// Formats one log entry.
        /// </summary>
        public string Format(LogEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var formatted = string.Format(
                CultureInfo.InvariantCulture,
                "{0:yyyy-MM-ddTHH:mm:ss.fff'Z'} [{1}] [{2}] {3}",
                entry.TimestampUtc,
                GetLevelName(entry.Level),
                entry.Source,
                entry.Message
            );

            if (entry.Exception != null)
                formatted += Environment.NewLine + entry.Exception;
            
            return formatted;
        }

        private static string GetLevelName(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Trace:
                    return "TRACE";

                case LogLevel.Debug:
                    return "DEBUG";

                case LogLevel.Information:
                    return "INFO";

                case LogLevel.Warning:
                    return "WARN";

                case LogLevel.Error:
                    return "ERROR";

                case LogLevel.Critical:
                    return "CRITICAL";

                default:
                    throw new ArgumentException("The log level is outside the supported range.", nameof(level));
            }
        }
    }
}
