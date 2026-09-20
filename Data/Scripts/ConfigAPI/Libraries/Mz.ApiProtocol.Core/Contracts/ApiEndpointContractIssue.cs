using System;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Describes one endpoint contract validation failure.
    /// </summary>
    public sealed class ApiEndpointContractIssue
    {
        /// <summary>
        /// Gets the unsatisfied endpoint requirement.
        /// </summary>
        public ApiEndpointRequirement Requirement { get; }

        /// <summary>
        /// Gets the kind of validation failure.
        /// </summary>
        public ApiEndpointContractIssueKind Kind { get; }

        /// <summary>
        /// Gets the actual delegate type, or null when the endpoint is
        /// missing.
        /// </summary>
        public Type ActualDelegateType { get; }

        /// <summary>
        /// Creates an endpoint contract issue.
        /// </summary>
        /// <param name="requirement">
        /// The unsatisfied endpoint requirement.
        /// </param>
        /// <param name="kind">
        /// The kind of validation failure.
        /// </param>
        /// <param name="actualDelegateType">
        /// The actual delegate type, or null for a missing endpoint.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="requirement"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="kind"/> is undefined.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the actual type is inconsistent with the issue kind.
        /// </exception>
        public ApiEndpointContractIssue(ApiEndpointRequirement requirement, ApiEndpointContractIssueKind kind, Type actualDelegateType)
        {
            if (requirement == null)
                throw new ArgumentNullException(nameof(requirement));

            if (kind < ApiEndpointContractIssueKind.MissingEndpoint || kind > ApiEndpointContractIssueKind.WrongDelegateType) 
                throw new ArgumentException("The value is outside the supported range.", nameof(kind));

            if (kind == ApiEndpointContractIssueKind.MissingEndpoint && actualDelegateType != null)
                throw new ArgumentException("A missing endpoint cannot have an actual delegate type.", nameof(actualDelegateType));

            if (kind == ApiEndpointContractIssueKind.WrongDelegateType && actualDelegateType == null)
                throw new ArgumentException("A wrong-type issue requires the actual delegate type.", nameof(actualDelegateType));

            Requirement = requirement;
            Kind = kind;
            ActualDelegateType = actualDelegateType;
        }

        /// <summary>
        /// Returns a human-readable description of the contract issue.
        /// </summary>
        /// <returns>A diagnostic issue description.</returns>
        public override string ToString()
        {
            if (Kind == ApiEndpointContractIssueKind.MissingEndpoint)
                return $"Missing endpoint '{Requirement.Name}' with required type {Requirement.DelegateType.FullName}.";

            return $"Endpoint '{Requirement.Name}' has delegate type {ActualDelegateType.FullName} but requires {Requirement.DelegateType.FullName}.";
        }
    }
}