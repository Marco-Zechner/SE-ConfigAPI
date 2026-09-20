using System;

namespace Mz.ApiProtocol.SpaceEngineers
{
    /// <summary>
    /// Provides access to the Space Engineers mod-message system.
    /// </summary>
    public interface IModMessageBus
    {
        /// <summary>
        /// Registers a handler for a message channel.
        /// </summary>
        void RegisterHandler(
            long channelId,
            Action<object> handler
        );

        /// <summary>
        /// Removes a previously registered handler.
        /// </summary>
        void UnregisterHandler(
            long channelId,
            Action<object> handler
        );

        /// <summary>
        /// Sends a payload to handlers registered on a channel.
        /// </summary>
        void Send(
            long channelId,
            object payload
        );
    }
}