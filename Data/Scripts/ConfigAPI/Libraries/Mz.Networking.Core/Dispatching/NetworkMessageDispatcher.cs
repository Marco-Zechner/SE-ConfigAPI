using System;
using System.Collections.Generic;

namespace Mz.Networking
{
    /// <summary>
    /// Registers application message handlers and dispatches validated network
    /// envelopes by message type.
    /// </summary>
    public sealed class NetworkMessageDispatcher
    {
        private readonly Dictionary<string, Action<NetworkReceiveContext>> _handlers;

        /// <summary>
        /// Creates an empty message dispatcher.
        /// </summary>
        public NetworkMessageDispatcher()
        {
            _handlers = new Dictionary<string, Action<NetworkReceiveContext>>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Registers one handler for an application-defined message type.
        /// </summary>
        public NetworkMessageSubscription RegisterHandler(string messageType, Action<NetworkReceiveContext> handler)
        {
            string normalizedMessageType = NormalizeMessageType(messageType);

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (_handlers.ContainsKey(normalizedMessageType))
                throw new InvalidOperationException($"A handler is already registered for message type '{normalizedMessageType}'.");

            _handlers.Add(normalizedMessageType, handler);

            return new NetworkMessageSubscription(this, normalizedMessageType, handler);
        }

        /// <summary>
        /// Attempts to dispatch a received envelope to its registered handler.
        /// </summary>
        public bool TryDispatch(NetworkEnvelope envelope, ulong transportSenderId, bool isServer, bool transportSenderIsServer, 
                                out NetworkReceiveContext context)
            => TryDispatch(envelope, transportSenderId, isServer, transportSenderIsServer, null, out context);

        /// <summary>
        /// Attempts to dispatch a received envelope to its registered handler.
        /// </summary>
        internal bool TryDispatch(NetworkEnvelope envelope, ulong transportSenderId, bool isServer, bool transportSenderIsServer,
                                  Action<Exception> handlerFailureObserver, out NetworkReceiveContext context)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            Action<NetworkReceiveContext> handler;

            if (!_handlers.TryGetValue(envelope.MessageType, out handler))
            {
                context = null;
                return false;
            }

            context = NetworkMessageProcessor.Process(envelope, transportSenderId, isServer, transportSenderIsServer, handler, handlerFailureObserver);

            return true;
        }

        internal void UnregisterHandler(string messageType, Action<NetworkReceiveContext> handler)
        {
            Action<NetworkReceiveContext> registeredHandler;

            if (!_handlers.TryGetValue(messageType, out registeredHandler))
                return;

            if (!ReferenceEquals(registeredHandler, handler))
                return;

            _handlers.Remove(messageType);
        }

        private static string NormalizeMessageType(string messageType)
        {
            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentException("A message type is required.", nameof(messageType));

            return messageType.Trim();
        }
    }
}
