namespace Mz.Networking
{
    /// <summary>
    /// Describes how a server should relay a received message.
    /// </summary>
    public enum NetworkRelayMode
    {
        /// <summary>
        /// Does not relay the message.
        /// </summary>
        None = 0,

        /// <summary>
        /// Relays the message to all clients except its original sender.
        /// </summary>
        ToOthers = 1,

        /// <summary>
        /// Relays the message to all clients, including its original sender.
        /// </summary>
        ToEveryone = 2,

        /// <summary>
        /// Relays the message only to its original sender.
        /// </summary>
        ReturnToSender = 3
    }
}