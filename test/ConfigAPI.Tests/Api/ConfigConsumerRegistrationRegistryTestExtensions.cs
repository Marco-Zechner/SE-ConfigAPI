using System;
using MarcoZechner.ConfigAPI.Api;

namespace MarcoZechner.ConfigAPI.Tests.V2.Api
{
    internal static class ConfigConsumerRegistrationRegistryTestExtensions
    {
        public static void RegisterReadWriteStorage(this ConfigConsumerRegistrationRegistry registry, string consumerId, Guid registrationId, Func<int, string, string> read, Action<int, string, string> write)
        {
            registry.Register(consumerId, registrationId, (location, file) => read(location, file) != null, read, write, location => new string[0]);
        }
    }
}
