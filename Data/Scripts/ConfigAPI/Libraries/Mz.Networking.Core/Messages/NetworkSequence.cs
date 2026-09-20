namespace Mz.Networking
{
    /// <summary>
    /// Compares wrapping unsigned 16-bit application sequence numbers.
    /// </summary>
    public static class NetworkSequence
    {
        private const int HalfRange = 32768;

        /// <summary>
        /// Returns whether a candidate sequence is newer than the current
        /// sequence within the unambiguous forward half of the value range.
        /// </summary>
        public static bool IsNewer(ushort candidate, ushort current)
        {
            var delta = unchecked((ushort)(candidate - current));

            return delta != 0 && delta < HalfRange;
        }
    }
}
