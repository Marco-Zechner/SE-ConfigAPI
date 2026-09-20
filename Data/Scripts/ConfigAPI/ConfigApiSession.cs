using System;
using MarcoZechner.ConfigAPI.V2.Api;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.ConfigApi;
using Mz.Logging;
using Mz.Logging.SpaceEngineers;
using Mz.SemanticVersioning;
using VRage.Game.Components;

namespace MarcoZechner.ConfigAPI.V2
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public sealed class ConfigApiSession : MySessionComponentBase
    {
        private static readonly SemanticVersion _modVersion = new SemanticVersion(0, 1, 0);

        private ConfigApiClient _configClient;
        private SpaceEngineersStorageLogger _logger;
        private ConfigApiProvider _provider;

        public override void LoadData()
        {
            _logger = SpaceEngineersStorageLogger.CreateLocal("ConfigAPI.log", typeof(ConfigApiSession), "ConfigAPI", LogLevel.Trace);
            _logger.Logger.Info("ConfigAPI session loading. Bootstrap logging is Trace until ConfigAPI.toml is loaded.");

            try
            {
                var messageBus = new SpaceEngineersModMessageBus();
                var registry = new ConfigConsumerRegistrationRegistry();

                _provider = new ConfigApiProvider(messageBus, registry, _modVersion, _logger.Logger);
                _provider.Start();

                _configClient = ConfigApiClient.CreateForSpaceEngineers(
                    messageBus, ConfigApiProvider.ApiId, "ConfigAPI", _modVersion, true, "Controls ConfigAPI runtime configuration.");

                _configClient.Connected += OnSelfConfigConnected;
                _configClient.Disconnected += OnSelfConfigDisconnected;

                _logger.Logger.Debug("Starting ConfigAPI self-configuration consumer.");
                _configClient.Start();

                if (!_configClient.IsConnected)
                {
                    _logger.Logger.MinimumLevel = LogLevel.Trace;
                    _logger.Logger.Error("ConfigAPI self-configuration consumer did not connect. Trace logging remains enabled.", _configClient.LastError);
                }
            }
            catch (Exception exception)
            {
                if (_logger != null)
                {
                    _logger.Logger.MinimumLevel = LogLevel.Trace;
                    _logger.Logger.Critical("ConfigAPI provider failed to start. Trace logging remains enabled.", exception);
                }

                throw;
            }
        }

        private void OnSelfConfigConnected()
        {
            _logger.Logger.Debug("ConfigAPI self-configuration consumer connected.");

            try
            {
                ConfigApiRuntimeConfig config = _configClient.Open(ConfigApiRuntimeConfigDefinition.Definition, ConfigLocation.Local);
                _logger.Logger.Info($"ConfigAPI runtime config loaded. Applying MinimumLogLevel={config.MinimumLogLevel}.");
                _logger.Logger.MinimumLevel = config.MinimumLogLevel;
            }
            catch (Exception exception)
            {
                _logger.Logger.MinimumLevel = LogLevel.Trace;
                _logger.Logger.Error("Failed to load ConfigAPI.toml. Trace logging remains enabled.", exception);
            }
        }

        private void OnSelfConfigDisconnected()
        {
            if (_logger == null)
                return;

            _logger.Logger.MinimumLevel = LogLevel.Trace;
            _logger.Logger.Warning("ConfigAPI self-configuration consumer disconnected. Trace logging remains enabled.");
        }

        protected override void UnloadData()
        {
            _logger?.Logger.Debug("ConfigAPI session unloading.");

            try
            {
                if (_configClient != null)
                {
                    _configClient.Connected -= OnSelfConfigConnected;
                    _configClient.Disconnected -= OnSelfConfigDisconnected;
                    _configClient.Dispose();
                }
            }
            catch (Exception exception)
            {
                _logger?.Logger.Error("ConfigAPI self-configuration consumer failed while unloading.", exception);
            }
            finally
            {
                _configClient = null;
            }

            try
            {
                _provider?.Dispose();
            }
            catch (Exception exception)
            {
                _logger?.Logger.Error("ConfigAPI provider failed while unloading.", exception);
            }
            finally
            {
                _provider = null;

                _logger?.Logger.Info("ConfigAPI session stopped.");
                _logger?.Dispose();
                _logger = null;

                base.UnloadData();
            }
        }
    }
}