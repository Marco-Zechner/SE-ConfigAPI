using System;

namespace Mz.Networking.SpaceEngineers
{
    /// <summary>
    /// Provides deferred execution on the Space Engineers game thread.
    /// </summary>
    public interface ISpaceEngineersNetworkSchedulingGateway
    {
        /// <summary>
        /// Schedules an action for a later game-thread invocation.
        /// </summary>
        void InvokeOnGameThread(Action action);
    }
}