using System;

namespace Mz.Toml
{
    /// <summary>
    /// Describes a TOML parse error at a source location.
    /// </summary>
    public sealed class TomlDiagnostic
    {
        /// <summary>
        /// Initializes a TOML diagnostic.
        /// </summary>
        internal TomlDiagnostic(string message, int line, int column, TomlDiagnosticCode code)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Diagnostic message cannot be null or empty.", nameof(message));
            if (line < 1)
                throw new ArgumentException("Diagnostic line must be at least 1.", nameof(line));
            if (column < 1)
                throw new ArgumentException("Diagnostic column must be at least 1.", nameof(column));

            Code = code;
            Message = message;
            Line = line;
            Column = column;
        }

        /// <summary>
        /// Gets the stable diagnostic code.
        /// </summary>
        public TomlDiagnosticCode Code { get; }

        /// <summary>
        /// Gets the human-readable diagnostic message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the one-based source line.
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// Gets the one-based source column.
        /// </summary>
        public int Column { get; }

        /// <summary>
        /// Formats the diagnostic for logs and exceptions.
        /// </summary>
        public override string ToString() => $"TOML {Code} at {Line}:{Column}: {Message}";
    }
}
