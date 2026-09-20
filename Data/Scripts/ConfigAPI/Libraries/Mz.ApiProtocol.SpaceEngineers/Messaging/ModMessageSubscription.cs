using System;

namespace Mz.ApiProtocol.SpaceEngineers
{
    /// <summary>
    /// Owns one mod-message handler registration.
    /// </summary>
    public sealed class ModMessageSubscription : IDisposable
    {
        private readonly IModMessageBus _messageBus;
        private readonly Action<object> _handler;

        private bool _isDisposed;

        /// <summary>
        /// Gets the channel on which the handler is registered.
        /// </summary>
        public long ChannelId { get; }

        /// <summary>
        /// Registers a handler and owns its lifecycle.
        /// </summary>
        public ModMessageSubscription(
            IModMessageBus messageBus,
            long channelId,
            Action<object> handler
        )
        {
            if (messageBus == null)
            {
                throw new ArgumentNullException(
                    nameof(messageBus)
                );
            }

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _messageBus = messageBus;
            _handler = handler;
            ChannelId = channelId;

            _messageBus.RegisterHandler(
                ChannelId,
                _handler
            );
        }

        /// <summary>
        /// Removes the owned handler registration.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _messageBus.UnregisterHandler(
                ChannelId,
                _handler
            );

            _isDisposed = true;
        }
    }
}