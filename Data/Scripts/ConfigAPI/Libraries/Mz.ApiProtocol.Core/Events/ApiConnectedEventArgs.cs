using System;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Provides information about an accepted API connection.
    /// </summary>
    public sealed class ApiConnectedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the accepted API connection.
        /// </summary>
        public ApiConnection Connection { get; }

        /// <summary>
        /// Gets the correlation identifier from the accepted announcement.
        /// </summary>
        /// <remarks>
        /// <see cref="Guid.Empty"/> indicates that the provider was accepted
        /// from an unsolicited announcement.
        /// </remarks>
        public Guid CorrelationId { get; }

        /// <summary>
        /// Creates API connection event data.
        /// </summary>
        /// <param name="connection">
        /// The accepted API connection.
        /// </param>
        /// <param name="correlationId">
        /// The correlation identifier from the accepted announcement.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="connection"/> is null.
        /// </exception>
        public ApiConnectedEventArgs(ApiConnection connection, Guid correlationId)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            Connection = connection;
            CorrelationId = correlationId;
        }
    }
}