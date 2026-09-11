using System;
using Mz.ApiProtocol;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.SemanticVersioning;

namespace Mz.Networking.SpaceEngineers
{
    internal static class NetworkManagerApiContract
    {
        internal const string ApiId = "Mz.NetworkManager";
        internal const string RegisterNetworkEndpointName = "RegisterNetwork";

        internal const string RegisterNetworkWithConflictReportingEndpointName = "RegisterNetworkWithConflictReporting";

        private static readonly ApiEndpointContract _endpointContract = new ApiEndpointContract(
            new ApiEndpointRequirement(
                RegisterNetworkEndpointName, 
                typeof(Func<string, string, Version, string, string, ushort, Action<ushort, ulong>, Action>)
            )
        );

        internal static ApiDiscoveryConsumer CreateConsumer(IModMessageBus messageBus, SpaceEngineersManagedNetworkConfiguration configuration)
        {
            var consumerIdentity = new ApiModIdentity(configuration.ModId, configuration.ModDisplayName, configuration.ModVersion);

            var requirement = new ApiRequirement(
                ApiId, 
                new ApiVersionRange(
                    new SemanticVersion(1, 0, 0),
                    new SemanticVersion(2, 0, 0)
                )
            );

            var dependency = new ApiDependencyDescriptor(
                consumerIdentity, requirement, 
                ApiDependencyKind.Optional, "Managed secure-message channel assignment"
            );

            return new ApiDiscoveryConsumer(messageBus, dependency);
        }

        internal static Func<string, string, Version, string, string, ushort, Action<ushort, ulong>, Action> 
            GetRegisterNetworkEndpoint(ApiConnection connection)
        {
            _endpointContract.EnsureCompatible(connection);

            Func<string, string, Version, string, string, ushort, Action<ushort, ulong>, Action> endpoint;

            if (!connection.TryGetEndpoint(RegisterNetworkEndpointName, out endpoint))
                throw new InvalidOperationException("The validated NetworkManager registration endpoint could not be loaded.");

            return endpoint;
        }

        internal static bool TryGetRegisterNetworkWithConflictReportingEndpoint(
            ApiConnection connection,
            out Func<string, string, Version, string, string, ushort, Action<ushort, ulong, Action<ushort, ulong>>, Action> endpoint)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            return connection.TryGetEndpoint(RegisterNetworkWithConflictReportingEndpointName, out endpoint);
        }
    }
}
