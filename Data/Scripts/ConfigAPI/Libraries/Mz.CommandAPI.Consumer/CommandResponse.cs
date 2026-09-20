using System;

namespace Mz.CommandApi
{
    /// <summary>
    /// Represents a structured response returned by a command handler.
    /// </summary>
    public sealed class CommandResponse
    {
        private readonly string[] _detailLines;

        public bool IsSuccess { get; }

        public string Title { get; }

        public string Summary { get; }

        public string[] DetailLines =>
            (string[])_detailLines.Clone();

        public CommandSeverity Severity { get; }

        public string UsageHint { get; }

        public CommandResponse(
            bool isSuccess,
            string title,
            string summary,
            string[] detailLines = null,
            CommandSeverity severity =
                CommandSeverity.Information,
            string usageHint = null
        )
        {
            if (title == null)
                throw new ArgumentNullException(nameof(title));

            if (summary == null)
                throw new ArgumentNullException(nameof(summary));

            if (
                !Enum.IsDefined(
                    typeof(CommandSeverity),
                    severity
                )
            )
            {
                throw new ArgumentException(
                    "The command severity is not supported.",
                    nameof(severity)
                );
            }

            string[] sourceLines =
                detailLines ?? new string[0];

            _detailLines =
                new string[sourceLines.Length];

            for (
                int index = 0;
                index < sourceLines.Length;
                index++
            )
            {
                if (sourceLines[index] == null)
                {
                    throw new ArgumentException(
                        "Detail lines cannot contain null values.",
                        nameof(detailLines)
                    );
                }

                _detailLines[index] =
                    sourceLines[index];
            }

            IsSuccess = isSuccess;
            Title = title;
            Summary = summary;
            Severity = severity;
            UsageHint = usageHint;
        }
    }
}
