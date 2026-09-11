using System;

namespace Mz.Networking
{
    /// <summary>
    /// Tracks the latest accepted wrapping unsigned 16-bit sequence for one
    /// application stream.
    /// </summary>
    public sealed class NetworkSequenceTracker
    {
        private ushort _latestSequence;

        /// <summary>
        /// Gets whether a sequence has been accepted.
        /// </summary>
        public bool HasSequence { get; private set; }

        /// <summary>
        /// Gets the latest accepted sequence.
        /// </summary>
        public ushort LatestSequence
        {
            get
            {
                if (!HasSequence)
                    throw new InvalidOperationException("No network sequence has been accepted.");

                return _latestSequence;
            }
        }

        /// <summary>
        /// Accepts the first sequence and later sequences that are newer than
        /// the latest accepted value.
        /// </summary>
        public bool TryAccept(ushort sequence)
        {
            if (HasSequence && !NetworkSequence.IsNewer(sequence, _latestSequence))
                return false;

            _latestSequence = sequence;
            HasSequence = true;
            return true;
        }

        /// <summary>
        /// Clears the latest sequence when its application stream is recreated.
        /// </summary>
        public void Reset()
        {
            HasSequence = false;
            _latestSequence = 0;
        }
    }
}
