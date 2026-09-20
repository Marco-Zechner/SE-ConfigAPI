using System;
using System.IO;

namespace Mz.Logging
{
    /// <summary>
    /// Writes formatted log entries to a text writer.
    /// </summary>
    public sealed class TextWriterLogSink : ILogSink, IDisposable
    {
        private readonly TextWriter _writer;
        private readonly ILogFormatter _formatter;
        private readonly bool _leaveOpen;
        private readonly bool _flushAfterWrite;

        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextWriterLogSink"/> class.
        /// </summary>
        /// <param name="writer">The text writer to which formatted log entries are written.</param>
        /// <param name="formatter">The formatter used to format log entries.</param>
        /// <param name="leaveOpen">Whether to leave the writer open after disposing this sink.</param>
        /// <param name="flushAfterWrite">Whether to flush the writer after each write operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="writer"/> or <paramref name="formatter"/> is null.</exception>
        public TextWriterLogSink(TextWriter writer, ILogFormatter formatter, bool leaveOpen = false, bool flushAfterWrite = true)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            if (formatter == null)
                throw new ArgumentNullException(nameof(formatter));

            _writer = writer;
            _formatter = formatter;
            _leaveOpen = leaveOpen;
            _flushAfterWrite = flushAfterWrite;
        }

        /// <summary>
        /// Formats and writes one log entry.
        /// </summary>
        public void Write(LogEntry entry)
        {
            ThrowIfDisposed();

            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var formattedEntry = _formatter.Format(entry);

            _writer.WriteLine(formattedEntry);

            if (_flushAfterWrite)
                _writer.Flush();
        }

        /// <summary>
        /// Flushes pending output.
        /// </summary>
        public void Flush()
        {
            ThrowIfDisposed();
            _writer.Flush();
        }

        /// <summary>
        /// Flushes and optionally disposes the underlying writer.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            try
            {
                _writer.Flush();
            }
            finally
            {
                if (!_leaveOpen)
                    _writer.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new InvalidOperationException("The text-writer log sink has been disposed.");
        }
    }
}
