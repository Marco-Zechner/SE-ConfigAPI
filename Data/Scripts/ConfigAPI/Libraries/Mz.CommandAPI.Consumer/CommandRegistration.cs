using System;

namespace Mz.CommandApi
{
    /// <summary>
    /// Describes one command registered through CommandAPI.
    /// </summary>
    public sealed class CommandRegistration
    {
        private readonly string[] _aliases;

        public string Prefix { get; }

        public string CanonicalName { get; }

        public string[] Aliases =>
            (string[])_aliases.Clone();

        public string ShortDescription { get; }

        public string HelpText { get; }

        public string Usage { get; }

        public string Category { get; }

        public CommandExecutionLocation ExecutionLocation { get; }

        public int PermissionRequirement { get; }

        public CommandRegistration(
            string prefix,
            string canonicalName,
            CommandExecutionLocation executionLocation =
                CommandExecutionLocation.Server,
            string[] aliases = null,
            string shortDescription = "",
            string helpText = "",
            string usage = "",
            string category = "",
            int permissionRequirement = 0
        )
        {
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException(
                    "A command prefix is required.",
                    nameof(prefix)
                );

            string normalizedPrefix =
                prefix.Trim();

            if (
                normalizedPrefix.Length < 2
                || normalizedPrefix[0] != '/'
            )
            {
                throw new ArgumentException(
                    "A command prefix must begin with '/'.",
                    nameof(prefix)
                );
            }

            for (
                int index = 1;
                index < normalizedPrefix.Length;
                index++
            )
            {
                if (char.IsWhiteSpace(normalizedPrefix[index]))
                {
                    throw new ArgumentException(
                        "A command prefix cannot contain whitespace.",
                        nameof(prefix)
                    );
                }
            }

            if (string.IsNullOrWhiteSpace(canonicalName))
                throw new ArgumentException(
                    "A canonical command name is required.",
                    nameof(canonicalName)
                );

            if (
                !Enum.IsDefined(
                    typeof(CommandExecutionLocation),
                    executionLocation
                )
            )
            {
                throw new ArgumentException(
                    "The execution location is not supported.",
                    nameof(executionLocation)
                );
            }

            if (executionLocation == CommandExecutionLocation.Internal)
                throw new ArgumentException("The Internal execution location is reserved for CommandAPI.", nameof(executionLocation));

            if (permissionRequirement < 0)
                throw new ArgumentException(
                    "The permission requirement cannot be negative.",
                    nameof(permissionRequirement)
                );

            string[] sourceAliases =
                aliases ?? new string[0];

            _aliases =
                new string[sourceAliases.Length];

            for (
                int index = 0;
                index < sourceAliases.Length;
                index++
            )
            {
                if (string.IsNullOrWhiteSpace(sourceAliases[index]))
                {
                    throw new ArgumentException(
                        "Aliases cannot contain empty values.",
                        nameof(aliases)
                    );
                }

                _aliases[index] =
                    sourceAliases[index].Trim();
            }

            Prefix =
                normalizedPrefix.ToLowerInvariant();

            CanonicalName =
                canonicalName.Trim();

            ShortDescription =
                shortDescription ?? string.Empty;

            HelpText =
                helpText ?? string.Empty;

            Usage =
                usage ?? string.Empty;

            Category =
                category ?? string.Empty;

            ExecutionLocation =
                executionLocation;

            PermissionRequirement =
                permissionRequirement;
        }
    }
}
