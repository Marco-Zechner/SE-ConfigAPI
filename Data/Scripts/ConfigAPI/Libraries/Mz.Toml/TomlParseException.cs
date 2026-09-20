using System;

namespace Mz.Toml
{
    /// <summary>
    /// Exception thrown by <see cref="Toml.Parse(string)"/> when TOML text is invalid.
    /// </summary>
    public sealed class TomlParseException : FormatException
    {
        /// <summary>
        /// Initializes an exception from a parser diagnostic.
        /// </summary>
        internal TomlParseException(TomlDiagnostic diagnostic) : base(diagnostic == null ? "TOML parsing failed." : diagnostic.ToString())
        {
            Diagnostic = diagnostic;
        }

        /// <summary>
        /// Gets the parser diagnostic that caused the exception.
        /// </summary>
        public TomlDiagnostic Diagnostic { get; }
    }
}
