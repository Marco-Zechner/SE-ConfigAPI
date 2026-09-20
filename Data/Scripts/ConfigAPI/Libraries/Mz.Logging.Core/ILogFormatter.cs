namespace Mz.Logging
{
    /// <summary>
    /// Converts structured log entries into text.
    /// </summary>
    public interface ILogFormatter
    {
        /// <summary>
        /// Formats one log entry.
        /// </summary>
        string Format(LogEntry entry);
    }
}