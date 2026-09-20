using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Persistence;

namespace MarcoZechner.ConfigAPI.V2.Api
{
    public sealed class ConfigConsumerRegistrationRegistry
    {
        private readonly Dictionary<string, Registration> _registrations = new Dictionary<string, Registration>(StringComparer.Ordinal);

        public void Register(string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write)
        {
            string normalizedConsumerId = ValidateConsumerId(consumerId);

            if (registrationId == Guid.Empty)
                throw new ArgumentException("Registration ID must not be empty.", nameof(registrationId));

            if (read == null)
                throw new ArgumentNullException(nameof(read));

            if (write == null)
                throw new ArgumentNullException(nameof(write));

            _registrations[normalizedConsumerId] = new Registration(registrationId, new ConfigCallbackTextStorage(read, write));
        }

        public IConfigTextStorage GetStorage(string consumerId, Guid registrationId)
        {
            string normalizedConsumerId = ValidateConsumerId(consumerId);

            if (registrationId == Guid.Empty)
                throw new ArgumentException("Registration ID must not be empty.", nameof(registrationId));

            Registration registration;

            if (!_registrations.TryGetValue(normalizedConsumerId, out registration))
                throw new InvalidOperationException("Consumer is not registered: " + normalizedConsumerId);

            if (registration.RegistrationId != registrationId)
                throw new InvalidOperationException("Consumer registration token is stale: " + normalizedConsumerId);

            return registration.Storage;
        }

        public bool Unregister(string consumerId, Guid registrationId)
        {
            string normalizedConsumerId = ValidateConsumerId(consumerId);

            if (registrationId == Guid.Empty)
                throw new ArgumentException("Registration ID must not be empty.", nameof(registrationId));

            Registration registration;

            if (!_registrations.TryGetValue(normalizedConsumerId, out registration))
                return false;

            if (registration.RegistrationId != registrationId)
                return false;

            return _registrations.Remove(normalizedConsumerId);
        }

        private static string ValidateConsumerId(string consumerId)
        {
            if (string.IsNullOrWhiteSpace(consumerId))
                throw new ArgumentException("Consumer ID must not be empty.", nameof(consumerId));

            return consumerId.Trim();
        }

        private sealed class Registration
        {
            public Registration(Guid registrationId, IConfigTextStorage storage)
            {
                RegistrationId = registrationId;
                Storage = storage;
            }

            public Guid RegistrationId { get; }
            public IConfigTextStorage Storage { get; }
        }
    }
}
