using System;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Describes one endpoint required by an API contract.
    /// </summary>
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class ApiEndpointRequirement
    {
        /// <summary>
        /// Gets the case-sensitive endpoint name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the exact delegate type required for the endpoint.
        /// </summary>
        public Type DelegateType { get; }

        /// <summary>
        /// Creates an endpoint requirement.
        /// </summary>
        /// <param name="name">
        /// The case-sensitive endpoint name.
        /// </param>
        /// <param name="delegateType">
        /// The exact delegate type required for the endpoint.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="name"/> is empty.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="delegateType"/> is null.
        /// </exception>
        public ApiEndpointRequirement(string name, Type delegateType)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("An endpoint name is required.", nameof(name));

            if (delegateType == null)
                throw new ArgumentNullException(nameof(delegateType));

            Name = name.Trim();
            DelegateType = delegateType;
        }

        /// <summary>
        /// Returns a diagnostic representation of the endpoint requirement.
        /// </summary>
        /// <returns>
        /// The endpoint name and required delegate type.
        /// </returns>
        public override string ToString() => $"{Name}: {DelegateType.FullName}";
    }
}