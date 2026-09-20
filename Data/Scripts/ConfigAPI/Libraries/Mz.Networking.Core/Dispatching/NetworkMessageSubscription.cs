using System;

namespace Mz.Networking
{
    /// <summary>
    /// Owns one network-message handler registration.
    /// </summary>
    public sealed class NetworkMessageSubscription : IDisposable
    {
        private readonly NetworkMessageDispatcher _dispatcher;
        private readonly string _messageType;
        private readonly Action<NetworkReceiveContext> _handler;

        private bool _isDisposed;

        internal NetworkMessageSubscription(NetworkMessageDispatcher dispatcher, string messageType, Action<NetworkReceiveContext> handler)
        {
            if (dispatcher == null)
                throw new ArgumentNullException(nameof(dispatcher));

            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _dispatcher = dispatcher;
            _messageType = messageType;
            _handler = handler;
        }

        /// <summary>
        /// Removes the owned handler registration.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;
            
            _dispatcher.UnregisterHandler(_messageType, _handler);

            _isDisposed = true;
        }
    }
}