namespace Mz.Networking.SpaceEngineers
{
    /// <summary>
    /// Describes the recommended severity of a networking diagnostic.
    ///
    /// The names and numeric values intentionally match Mz.Logging.LogLevel so
    /// consumers can map them without Mz.Networking depending on Mz.Logging.
    /// </summary>
    public enum SpaceEngineersNetworkDiagnosticSeverity
    {
        /// <summary>
        /// Detailed execution tracing.
        /// </summary>
        Trace = 0,

        /// <summary>
        /// Debugging information.
        /// </summary>
        Debug = 1,

        /// <summary>
        /// Normal operational information.
        /// </summary>
        Information = 2,

        /// <summary>
        /// A recoverable networking problem.
        /// </summary>
        Warning = 3,

        /// <summary>
        /// A networking or application operation failed.
        /// </summary>
        Error = 4,

        /// <summary>
        /// Immediate attention is required.
        /// </summary>
        Critical = 5
    }
}