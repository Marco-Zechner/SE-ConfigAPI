using System;
using System.Text;

namespace Mz.Networking.SpaceEngineers
{
    internal static class SpaceEngineersNetworkIdentity
    {
        internal const int MaximumNetworkIdBytes = 256;

        internal static string Normalize(string networkId)
        {
            if (string.IsNullOrWhiteSpace(networkId))
                throw new ArgumentException("A stable network ID is required.", nameof(networkId));

            string normalized = networkId.Trim();

            if (Encoding.UTF8.GetByteCount(normalized) > MaximumNetworkIdBytes)
                throw new ArgumentException("A network ID cannot exceed 256 UTF-8 bytes.", nameof(networkId));

            return normalized;
        }
    }
}
