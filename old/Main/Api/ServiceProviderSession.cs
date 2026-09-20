using System;
using Digi.NetworkLib;
using MarcoZechner.ApiLib;
using MarcoZechner.ConfigAPI.Main.NetworkCore;
using MarcoZechner.ConfigAPI.Scripts.ConfigAPI.Shared;
using MarcoZechner.ConfigAPI.Shared.Api;
using VRage.Game.Components;

namespace MarcoZechner.ConfigAPI.Main.Api
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public sealed class ServiceProviderSession : MySessionComponentBase
    {
        private ApiProviderHost _host;
        private Network _net;
        private WorldConfigNetworkCore _worldNet;

        public override void LoadData()
        {
            _net = new Network(12345, "ConfigAPI");
            _worldNet  = new WorldConfigNetworkCore(_net);

            _host = new ApiProviderHost(new ConfigApiBootstrap(), Connect, Disconnect);
            _host.Load();
            CfgLog.Info("ConfigAPI loaded");
        }

        protected override void UnloadData()
        {
            _host?.Unload();
            _host = null;

            if (_worldNet != null)
            {
                CfgLog.Warning($"Mods still connected: {_worldNet.RegisteredConsumerModIds.Count} - unloading anyway.");
                _worldNet.Unload();
                _worldNet = null;
            }

            _net?.Dispose();
            _net = null;
        }

        // Called by ApiLib when a consumer connects
        private IApiProvider Connect(
            ulong consumerModId,
            string consumerModName,
            IApiProvider configUserHooks
        )
        {
            CfgLog.Info($"{consumerModId}:{consumerModName} connected to ConfigAPI");

            if (_worldNet.RegisteredConsumerModIds.Contains(consumerModId))
            {
                var ex = new Exception($"ConfigAPI: Mod {consumerModId} tried to connect twice as consumer");
                CfgLog.Error(ex.Message, ex);
            }

            // store callbacks for provider -> consumer calls
            var hooks = new ConfigUserHooks(configUserHooks);

            var worldFacade  = _worldNet.CreateConsumerFacade(consumerModId);

            var api = new ConfigServiceImpl(consumerModId, consumerModName, hooks, worldFacade);

            _worldNet.RegisterConsumer(consumerModId, api.WorldConfigClientService, api.InternalConfigService, hooks);

            return api;
        }

        // Called by ApiLib when a consumer disconnects, which means another mod on the same machine is probably unloading.
        // That normally only happens when the world unloads so we don't really need to do anything special here.
        // but it's here if you want to do some cleanup per mod.
        private void Disconnect(ulong consumerModId)
        {
            CfgLog.Info($"{consumerModId} disconnected from ConfigAPI");
            _worldNet.UnregisterConsumer(consumerModId);
        }
    }
}