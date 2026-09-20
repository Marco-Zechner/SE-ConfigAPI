namespace Mz.Logging
{
    /// <summary>
    /// Receives log entries from a logger.
    /// </summary>
    public interface ILogSink
    {
        /// <summary>
        /// Writes one log entry.
        /// </summary>
        /// <param name="entry">The entry to write.</param>
        void Write(LogEntry entry);
    }
}