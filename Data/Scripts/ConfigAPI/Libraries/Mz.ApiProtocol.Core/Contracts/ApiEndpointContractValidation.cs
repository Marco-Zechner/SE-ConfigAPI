using System;
using System.Collections.Generic;
using System.Text;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Contains the result of validating an API endpoint contract.
    /// </summary>
    public sealed class ApiEndpointContractValidation
    {
        /// <summary>
        /// Gets whether every endpoint requirement was satisfied.
        /// </summary>
        public bool IsCompatible => Issues.Count == 0;

        /// <summary>
        /// Gets the endpoint contract issues.
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public IReadOnlyList<ApiEndpointContractIssue> Issues { get; }

        internal ApiEndpointContractValidation(IList<ApiEndpointContractIssue> issues)
        {
            if (issues == null)
                throw new ArgumentNullException(nameof(issues));

            var copy = new List<ApiEndpointContractIssue>();

            foreach (var issue in issues)
            {
                if (issue == null)
                    throw new ArgumentException("Validation issues cannot contain null values.", nameof(issues));

                copy.Add(issue);
            }

            Issues = new ReadOnlyListView<ApiEndpointContractIssue>(copy);
        }

        /// <summary>
        /// Returns a human-readable validation summary.
        /// </summary>
        /// <returns>
        /// A success message or the concatenated endpoint issues.
        /// </returns>
        public override string ToString()
        {
            if (IsCompatible) return "The endpoint contract is compatible.";

            var builder = new StringBuilder();

            builder.Append("The endpoint contract is incompatible.");

            foreach (var issue in Issues)
                builder.Append(" ").Append(issue);

            return builder.ToString();
        }
    }
}