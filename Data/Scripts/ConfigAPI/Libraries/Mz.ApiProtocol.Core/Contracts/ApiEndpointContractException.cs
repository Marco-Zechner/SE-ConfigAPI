using System;

namespace Mz.ApiProtocol
{
    /// <summary>
    /// Represents a required API endpoint contract that was not satisfied.
    /// </summary>
    public sealed class ApiEndpointContractException
        : InvalidOperationException
    {
        /// <summary>
        /// Gets the endpoint contract validation result.
        /// </summary>
        public ApiEndpointContractValidation Validation { get; }

        /// <summary>
        /// Creates an endpoint contract exception.
        /// </summary>
        /// <param name="validation">
        /// The incompatible validation result.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="validation"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="validation"/> is compatible.
        /// </exception>
        public ApiEndpointContractException(ApiEndpointContractValidation validation) : base(CreateMessage(validation))
        {
            Validation = validation;
        }

        private static string CreateMessage(ApiEndpointContractValidation validation)
        {
            if (validation == null)
                throw new ArgumentNullException(nameof(validation));

            if (validation.IsCompatible)
                throw new ArgumentException("A compatible validation cannot produce an endpoint contract exception.", nameof(validation));

            return validation.ToString();
        }
    }
}