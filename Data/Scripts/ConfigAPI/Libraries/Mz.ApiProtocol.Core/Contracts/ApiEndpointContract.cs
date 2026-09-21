using System;
using System.Collections.Generic;
using Mz.Collections;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Describes the named delegate endpoints required from an API provider.
    /// </summary>
    public sealed class ApiEndpointContract
    {
        /// <summary>
        /// Gets the required endpoint definitions.
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public IReadOnlyList<ApiEndpointRequirement> Requirements { get; }

        /// <summary>
        /// Creates an endpoint contract.
        /// </summary>
        /// <param name="requirements">
        /// The endpoint requirements. An empty collection represents an API
        /// with no required endpoints.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="requirements"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a requirement is null or endpoint names are duplicated.
        /// </exception>
        public ApiEndpointContract(params ApiEndpointRequirement[] requirements) : this((IEnumerable<ApiEndpointRequirement>)requirements) { }
        
        /// <summary>
        /// Creates an endpoint contract.
        /// </summary>
        /// <param name="requirements">
        /// The endpoint requirements. An empty collection represents an API
        /// with no required endpoints.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="requirements"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a requirement is null or endpoint names are duplicated.
        /// </exception>
        public ApiEndpointContract(IEnumerable<ApiEndpointRequirement> requirements)
        {
            if (requirements == null)
                throw new ArgumentNullException(nameof(requirements));

            var copy = new List<ApiEndpointRequirement>();

            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (var requirement in requirements)
            {
                if (requirement == null)
                    throw new ArgumentException("Endpoint requirements cannot contain null values.", nameof(requirements));

                if (!names.Add(requirement.Name))
                    throw new ArgumentException("Endpoint requirement names must be unique.", nameof(requirements));

                copy.Add(requirement);
            }

            Requirements = new ReadOnlyListView<ApiEndpointRequirement>(copy);
        }

        /// <summary>
        /// Validates an accepted API connection.
        /// </summary>
        /// <param name="connection">
        /// The connection whose endpoints will be validated.
        /// </param>
        /// <returns>The endpoint validation result.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="connection"/> is null.
        /// </exception>
        // ReSharper disable once MemberCanBePrivate.Global
        public ApiEndpointContractValidation Validate(ApiConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));
            
            return Validate(connection.Endpoints);
        }

        /// <summary>
        /// Validates an endpoint collection.
        /// </summary>
        /// <param name="endpoints">
        /// The endpoint collection to validate.
        /// </param>
        /// <returns>The endpoint validation result.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="endpoints"/> is null.
        /// </exception>
        // ReSharper disable once MemberCanBePrivate.Global
        public ApiEndpointContractValidation Validate(IReadOnlyDictionary<string, Delegate> endpoints)
        {
            if (endpoints == null)
                throw new ArgumentNullException(nameof(endpoints));

            var issues = new List<ApiEndpointContractIssue>();

            foreach (var requirement in Requirements)
            {
                Delegate endpoint;

                if (!endpoints.TryGetValue(requirement.Name, out endpoint))
                {
                    issues.Add(new ApiEndpointContractIssue(requirement, ApiEndpointContractIssueKind.MissingEndpoint, null));
                    continue;
                }

                var actualType = endpoint.GetType();

                if (actualType != requirement.DelegateType)
                    issues.Add(new ApiEndpointContractIssue(requirement, ApiEndpointContractIssueKind.WrongDelegateType, actualType));
            }

            return new ApiEndpointContractValidation(issues);
        }

        /// <summary>
        /// Ensures that an accepted API connection satisfies the contract.
        /// </summary>
        /// <param name="connection">
        /// The connection whose endpoints will be validated.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="connection"/> is null.
        /// </exception>
        /// <exception cref="ApiEndpointContractException">
        /// Thrown when one or more endpoint requirements are not satisfied.
        /// </exception>
        public void EnsureCompatible(ApiConnection connection)
        {
            var validation = Validate(connection);

            if (!validation.IsCompatible)
                throw new ApiEndpointContractException(validation);
        }
    }
}