namespace Mz.Logging
{
    /// <summary>
    /// Describes the severity of a log entry.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// The least severe level, used for detailed tracing of program execution.
        /// </summary>
        Trace = 0,
        /// <summary>
        /// A diagnostic message, used for debugging purposes.
        /// </summary>
        Debug = 1,
        /// <summary>
        /// An informational message, used to provide general feedback about the program's operation.
        /// </summary>
        Information = 2,
        /// <summary>
        /// A warning message, used to indicate that an issue has occurred but the program can still continue.
        /// </summary>
        Warning = 3,
        /// <summary>
        /// An error message, used to indicate that an issue has occurred that affects the program's ability to continue.
        /// </summary>
        Error = 4,
        /// <summary>
        /// A critical message, used to indicate that an issue has occurred that requires immediate attention.
        /// </summary>
        Critical = 5
    }
}