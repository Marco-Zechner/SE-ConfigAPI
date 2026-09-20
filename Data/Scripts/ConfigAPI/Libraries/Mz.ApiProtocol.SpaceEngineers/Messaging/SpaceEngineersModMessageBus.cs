using System;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace Mz.ApiProtocol.SpaceEngineers
{
    /// <summary>
    /// Uses the Space Engineers ModAPI mod-message system.
    /// </summary>
    public sealed class SpaceEngineersModMessageBus :
        IModMessageBus
    {
        /// <inheritdoc />
        public void RegisterHandler(
            long channelId,
            Action<object> handler
        )
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            GetUtilities().RegisterMessageHandler(
                channelId,
                handler
            );
        }

        /// <inheritdoc />
        public void UnregisterHandler(
            long channelId,
            Action<object> handler
        )
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            GetUtilities().UnregisterMessageHandler(
                channelId,
                handler
            );
        }

        /// <inheritdoc />
        public void Send(
            long channelId,
            object payload
        )
        {
            GetUtilities().SendModMessage(
                channelId,
                payload
            );
        }

        private static IMyUtilities GetUtilities()
        {
            var utilities = MyAPIGateway.Utilities;

            if (utilities == null)
            {
                throw new InvalidOperationException(
                    "Space Engineers utilities are unavailable. "
                    + "Use the mod-message bus during the active "
                    + "session lifecycle."
                );
            }

            return utilities;
        }
    }
}