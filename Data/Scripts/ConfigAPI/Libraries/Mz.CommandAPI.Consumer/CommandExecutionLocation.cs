namespace Mz.CommandApi
{
    /// <summary>
    /// Selects where CommandAPI executes a registered command.
    /// </summary>
    public enum CommandExecutionLocation
    {
        Client,
        Server,
        Either,

        /// <summary>
        /// Reserved for CommandAPI implementation use and unavailable to public registrations.
        /// </summary>
        Internal
    }
}
