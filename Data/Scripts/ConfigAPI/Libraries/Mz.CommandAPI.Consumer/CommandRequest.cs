using System;

namespace Mz.CommandApi
{
    /// <summary>
    /// Provides trusted request context and parsed command input.
    /// </summary>
    public sealed class CommandRequest
    {
        private readonly string[] _arguments;

        public string RequestId { get; }

        public ulong RequesterSteamId { get; }

        public long RequesterIdentityId { get; }

        public string RequesterDisplayName { get; }

        public int PermissionLevel { get; }

        public bool IsServer { get; }

        public string Prefix { get; }

        public string CommandName { get; }

        public string[] Arguments =>
            (string[])_arguments.Clone();

        internal CommandRequest(
            string requestId,
            ulong requesterSteamId,
            long requesterIdentityId,
            string requesterDisplayName,
            int permissionLevel,
            bool isServer,
            string prefix,
            string commandName,
            string[] arguments
        )
        {
            if (requestId == null)
                throw new ArgumentNullException(nameof(requestId));

            if (requesterDisplayName == null)
                throw new ArgumentNullException(
                    nameof(requesterDisplayName)
                );

            if (prefix == null)
                throw new ArgumentNullException(nameof(prefix));

            if (commandName == null)
                throw new ArgumentNullException(
                    nameof(commandName)
                );

            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

            RequestId = requestId;
            RequesterSteamId = requesterSteamId;
            RequesterIdentityId = requesterIdentityId;
            RequesterDisplayName = requesterDisplayName;
            PermissionLevel = permissionLevel;
            IsServer = isServer;
            Prefix = prefix;
            CommandName = commandName;
            _arguments = (string[])arguments.Clone();
        }
    }
}
