using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Persistence;

namespace MarcoZechner.ConfigAPI.Api
{
    public sealed class ConfigConsumerRegistrationRegistry
    {
        private readonly Dictionary<string, Registration> _registrations = new Dictionary<string, Registration>(StringComparer.Ordinal);

        public int Count => _registrations.Count;

        public string[] GetConsumerIds()
        {
            var consumerIds = new string[_registrations.Count];
            _registrations.Keys.CopyTo(consumerIds, 0);
            Array.Sort(consumerIds, StringComparer.Ordinal);
            return consumerIds;
        }

        public void Register(string consumerId, Guid registrationId, Func<int, string, bool> exists, Func<int, string, string> read, Action<int, string, string> write, Func<int, string[]> listKnown)
        {
            string normalizedConsumerId = ValidateConsumerId(consumerId);

            if (registrationId == Guid.Empty)
                throw new ArgumentException("Registration ID must not be empty.", nameof(registrationId));
            if (exists == null)
                throw new ArgumentNullException(nameof(exists));
            if (read == null)
                throw new ArgumentNullException(nameof(read));
            if (write == null)
                throw new ArgumentNullException(nameof(write));
            if (listKnown == null)
                throw new ArgumentNullException(nameof(listKnown));

            Registration existing;
            if (_registrations.TryGetValue(normalizedConsumerId, out existing) && existing.IsInternal)
                throw new InvalidOperationException("Consumer ID is reserved for ConfigAPI internal storage: " + normalizedConsumerId);

            _registrations[normalizedConsumerId] = new Registration(registrationId, new ConfigCallbackTextStorage(exists, read, write, listKnown), false);
        }

        internal void RegisterInternalStorage(string consumerId, IIndexedConfigTextStorage storage)
        {
            string normalizedConsumerId = ValidateConsumerId(consumerId);

            if (storage == null)
                throw new ArgumentNullException(nameof(storage));
            if (_registrations.ContainsKey(normalizedConsumerId))
                throw new InvalidOperationException("Consumer is already registered: " + normalizedConsumerId);

            _registrations.Add(normalizedConsumerId, new Registration(Guid.Empty, storage, true));
        }

        public IConfigTextStorage GetStorage(string consumerId, Guid registrationId)
        {
            string normalizedConsumerId = ValidateConsumerId(consumerId);

            if (registrationId == Guid.Empty)
                throw new ArgumentException("Registration ID must not be empty.", nameof(registrationId));

            Registration registration = GetRegistration(normalizedConsumerId);

            if (registration.IsInternal)
                throw new InvalidOperationException("Consumer ID is reserved for ConfigAPI internal storage: " + normalizedConsumerId);
            if (registration.RegistrationId != registrationId)
                throw new InvalidOperationException("Consumer registration token is stale: " + normalizedConsumerId);

            return registration.Storage;
        }

        public IIndexedConfigTextStorage GetIndexedStorage(string consumerId, Guid registrationId) => (IIndexedConfigTextStorage)GetStorage(consumerId, registrationId);

        internal IConfigTextStorage GetCurrentStorage(string consumerId)
        {
            string normalizedConsumerId = ValidateConsumerId(consumerId);
            return GetRegistration(normalizedConsumerId).Storage;
        }

        internal IIndexedConfigTextStorage GetCurrentIndexedStorage(string consumerId) => (IIndexedConfigTextStorage)GetCurrentStorage(consumerId);

        public bool Unregister(string consumerId, Guid registrationId)
        {
            string normalizedConsumerId = ValidateConsumerId(consumerId);

            if (registrationId == Guid.Empty)
                throw new ArgumentException("Registration ID must not be empty.", nameof(registrationId));

            Registration registration;
            if (!_registrations.TryGetValue(normalizedConsumerId, out registration) || registration.IsInternal)
                return false;
            if (registration.RegistrationId != registrationId)
                return false;

            return _registrations.Remove(normalizedConsumerId);
        }

        internal bool UnregisterInternalStorage(string consumerId)
        {
            string normalizedConsumerId = ValidateConsumerId(consumerId);
            Registration registration;

            if (!_registrations.TryGetValue(normalizedConsumerId, out registration) || !registration.IsInternal)
                return false;

            return _registrations.Remove(normalizedConsumerId);
        }

        private Registration GetRegistration(string normalizedConsumerId)
        {
            Registration registration;

            if (!_registrations.TryGetValue(normalizedConsumerId, out registration))
                throw new InvalidOperationException("Consumer is not registered: " + normalizedConsumerId);

            return registration;
        }

        private static string ValidateConsumerId(string consumerId)
        {
            if (string.IsNullOrWhiteSpace(consumerId))
                throw new ArgumentException("Consumer ID must not be empty.", nameof(consumerId));

            return consumerId.Trim();
        }

        private sealed class Registration
        {
            public Registration(Guid registrationId, IIndexedConfigTextStorage storage, bool isInternal)
            {
                RegistrationId = registrationId;
                Storage = storage;
                IsInternal = isInternal;
            }

            public Guid RegistrationId { get; }
            public IIndexedConfigTextStorage Storage { get; }
            public bool IsInternal { get; }
        }
    }
}