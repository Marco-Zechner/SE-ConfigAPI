using System;

namespace Mz.Logging
{
    /// <summary>
    /// Dispatches each log entry to multiple sinks in registration order.
    /// </summary>
    public sealed class CompositeLogSink : ILogSink
    {
        private readonly ILogSink[] _sinks;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeLogSink"/> class.
        /// </summary>
        /// <param name="sinks">The sinks to which log entries are dispatched.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public CompositeLogSink(params ILogSink[] sinks)
        {
            if (sinks == null)
                throw new ArgumentNullException(nameof(sinks));

            if (sinks.Length == 0)
                throw new ArgumentException("At least one log sink is required.", nameof(sinks));

            _sinks = new ILogSink[sinks.Length];

            for (var index = 0; index < sinks.Length; index++)
            {
                if (sinks[index] == null)
                    throw new ArgumentException("The sink collection cannot contain null.", nameof(sinks));

                _sinks[index] = sinks[index];
            }
        }

        /// <summary>
        /// Writes the same entry to every configured sink.
        /// </summary>
        public void Write(LogEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            foreach (var sink in _sinks)
                sink.Write(entry);
        }
    }
}