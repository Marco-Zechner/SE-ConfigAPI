using MarcoZechner.ConfigAPI.Main.Domain;
using MarcoZechner.ConfigAPI.Shared.Api;

namespace MarcoZechner.ConfigAPI.Main.NetworkCore
{
    public interface IWorldConfigNetworkCore
    {
        IWorldConfigNetwork CreateConsumerFacade(ulong consumerModId);

        void RegisterConsumer(ulong consumerModId, IWorldConfigClientSink sink, IInternalConfigService configService, IConfigUserHooks userHooks);
        void UnregisterConsumer(ulong consumerModId);
    }
}