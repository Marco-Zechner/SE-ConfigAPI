using System;

namespace Mz.Networking.SpaceEngineers
{
    /// <summary>
    /// Describes one accepted NetworkManager channel assignment.
    /// </summary>
    public sealed class SpaceEngineersNetworkChannelAssignmentEventArgs :
        EventArgs
    {
        /// <summary>
        /// Creates channel-assignment event data.
        /// </summary>
        public SpaceEngineersNetworkChannelAssignmentEventArgs(ushort previousChannel, ushort channelId, ulong generation)
        {
            PreviousChannel = previousChannel;
            ChannelId = channelId;
            Generation = generation;
        }

        /// <summary>
        /// Gets the active channel before the assignment was applied.
        /// </summary>
        public ushort PreviousChannel { get; }

        /// <summary>
        /// Gets the assigned active channel.
        /// </summary>
        public ushort ChannelId { get; }

        /// <summary>
        /// Gets the provider-scoped assignment generation.
        /// </summary>
        public ulong Generation { get; }

        /// <summary>
        /// Gets whether applying the assignment changed secure-message
        /// registration.
        /// </summary>
        public bool ChannelChanged => PreviousChannel != ChannelId;
    }
}
