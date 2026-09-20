namespace Mz.Networking.SpaceEngineers
{
    /// <summary>
    /// Provides optional best-effort decoding used only for bounded receive
    /// diagnostics.
    /// </summary>
    public interface ISpaceEngineersNetworkDiagnosticGateway
    {
        /// <summary>
        /// Attempts to decode a packet as a Space Engineers serialized string.
        /// Implementations must not throw for malformed input.
        /// </summary>
        bool TryDeserializeString(byte[] serialized, out string value);
    }
}