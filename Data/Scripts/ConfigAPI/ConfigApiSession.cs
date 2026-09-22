using System;
using System.Collections.Generic;
using MarcoZechner.ConfigAPI.V2.Api;
using MarcoZechner.ConfigAPI.V2.Domain;
using MarcoZechner.ConfigAPI.V2.Persistence;
using Mz.ApiProtocol.SpaceEngineers;
using Mz.Networking.SpaceEngineers;
using Sandbox.ModAPI;
using Mz.CommandApi;
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
        private const ushort WorldNetworkChannelId = 12345;
        private const string WorldNetworkId = "MarcoZechner.ConfigAPI.World";
        private const string WorldSmokeConfigKey = "WorldSmoke";
        private const string WorldSmokeFile = "ConfigAPI.WorldSmoke.toml";

        private readonly Guid _worldSmokeRegistrationId = Guid.NewGuid();
        private readonly List<CommandRegistrationHandle> _commandRegistrations = new List<CommandRegistrationHandle>();
        private CommandApiClient _commandClient;
        private SpaceEngineersStorageLogger _logger;
        private ConfigApiProvider _provider;
        private ConfigConsumerRegistrationRegistry _registry;
        private SpaceEngineersNetworkSession _worldNetworkSession;
        private WorldConfigNetworkRuntime _worldNetworkRuntime;
        private Action _worldSmokeUnregister;
        private IDictionary<string, object> _worldSmokeLastResponse;
        private bool _selfConfigLoaded;

        public override void LoadData()
        {
            _logger = SpaceEngineersStorageLogger.CreateLocal("ConfigAPI.log", typeof(ConfigApiSession), "ConfigAPI", LogLevel.Trace);
            _logger.Logger.Info("ConfigAPI session loading. Bootstrap logging is Trace until ConfigAPI.toml is loaded.");

            try
            {
                var messageBus = new SpaceEngineersModMessageBus();
                _registry = new ConfigConsumerRegistrationRegistry();
                _registry.RegisterInternalStorage(ConfigApiProvider.ApiId, ConfigApiSpaceEngineersTextStorage.Create(ConfigApiProvider.ApiId, typeof(ConfigApiSession)));

                _provider = new ConfigApiProvider(messageBus, _registry, _modVersion, _logger.Logger);
                _provider.Start();
                _worldSmokeUnregister = _provider.RegisterWorldInternal(ConfigApiProvider.ApiId, _worldSmokeRegistrationId, OnWorldConfigResponseReceived);

                LoadSelfConfig();
                StartCommandApiIntegration(messageBus);
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

        public override void BeforeStart()
        {
            if (_registry == null)
                throw new InvalidOperationException("ConfigAPI provider state is unavailable before World networking startup.");
            if (MyAPIGateway.Multiplayer == null)
                throw new InvalidOperationException("Space Engineers multiplayer is unavailable before World networking startup.");

            try
            {
                _worldNetworkSession = new SpaceEngineersNetworkSession(WorldNetworkChannelId, WorldNetworkId, OnWorldNetworkReceiveFailure);
                _worldNetworkRuntime = new WorldConfigNetworkRuntime(_worldNetworkSession.Endpoint, _worldNetworkSession.Transport, _registry, new SystemConfigClock(), new SpaceEngineersWorldConfigAuthorization(), new WorldConfigBootstrapStore(new SpaceEngineersWorldConfigBootstrapVariables()));
                _provider.AttachWorldRuntime(_worldNetworkRuntime);

                _logger.Logger.Info("ConfigAPI World networking started as " + (_worldNetworkRuntime.IsServer ? "server" : "client") + " on channel " + _worldNetworkSession.ChannelId + ".");
            }
            catch (Exception exception)
            {
                DisposeWorldNetworking();
                _logger?.Logger.Critical("ConfigAPI World networking failed to start.", exception);
                throw;
            }
        }

        private void OnWorldNetworkReceiveFailure(SpaceEngineersNetworkReceiveFailure failure)
        {
            if (failure == null)
                return;

            _logger?.Logger.Warning("ConfigAPI World networking rejected a packet on channel " + failure.ChannelId + " from peer " + failure.SenderPeerId + ".", failure.Exception);
        }

        private void DisposeWorldNetworking()
        {
            if (_provider != null)
            {
                try
                {
                    _provider.DetachWorldRuntime();
                }
                catch (Exception exception)
                {
                    _logger?.Logger.Error("ConfigAPI World provider bridge failed while unloading.", exception);
                }
            }

            if (_worldNetworkRuntime != null)
            {
                try
                {
                    _worldNetworkRuntime.Dispose();
                }
                catch (Exception exception)
                {
                    _logger?.Logger.Error("ConfigAPI World network runtime failed while unloading.", exception);
                }
                finally
                {
                    _worldNetworkRuntime = null;
                }
            }

            if (_worldNetworkSession != null)
            {
                try
                {
                    _worldNetworkSession.Dispose();
                }
                catch (Exception exception)
                {
                    _logger?.Logger.Error("ConfigAPI World network session failed while unloading.", exception);
                }
                finally
                {
                    _worldNetworkSession = null;
                }
            }
        }
        private void StartCommandApiIntegration(SpaceEngineersModMessageBus messageBus)
        {
            try
            {
                _commandClient = new CommandApiClient(
                    messageBus, "ConfigAPI", "ConfigAPI", _modVersion, false, "Provides ConfigAPI administration and diagnostic commands.");

                _commandClient.Connected += OnCommandApiConnected;
                _commandClient.Disconnected += OnCommandApiDisconnected;
                _commandClient.RegistrationFailed += OnCommandRegistrationFailed;

                RegisterCommands();

                _logger.Logger.Debug("Starting optional CommandAPI consumer.");
                _commandClient.Start();

                if (!_commandClient.IsConnected)
                    _logger.Logger.Debug("CommandAPI is not currently available; ConfigAPI will continue without /cfg commands.");
            }
            catch (Exception exception)
            {
                _logger.Logger.Warning("Optional CommandAPI integration failed to start. ConfigAPI will continue without /cfg commands.", exception);

                for (int index = 0; index < _commandRegistrations.Count; index++)
                    _commandRegistrations[index].Dispose();

                _commandRegistrations.Clear();

                if (_commandClient != null)
                {
                    _commandClient.Connected -= OnCommandApiConnected;
                    _commandClient.Disconnected -= OnCommandApiDisconnected;
                    _commandClient.RegistrationFailed -= OnCommandRegistrationFailed;
                    _commandClient.Dispose();
                    _commandClient = null;
                }
            }
        }
        private void RegisterCommands()
        {
            _commandRegistrations.Add(
                _commandClient.Register(
                    new CommandRegistration(
                        "/cfg", "help", CommandExecutionLocation.Either, null,
                        "Lists ConfigAPI commands.",
                        "Lists the commands currently exposed by ConfigAPI.",
                        "help", "ConfigAPI"),
                    HandleCommandHelp));

            _commandRegistrations.Add(
                _commandClient.Register(
                    new CommandRegistration(
                        "/cfg", "status", CommandExecutionLocation.Either, null,
                        "Reports ConfigAPI runtime status.",
                        "Reports the local ConfigAPI provider, self-configuration consumer, and CommandAPI integration state.",
                        "status", "ConfigAPI"),
                    HandleCommandStatus));

            _commandRegistrations.Add(
                _commandClient.Register(
                    new CommandRegistration(
                        "/cfg", "world-open", CommandExecutionLocation.Client, null,
                        "Opens the World smoke config on this client.",
                        "Opens the shared World smoke config through the real client-to-server ConfigAPI path.",
                        "world-open", "ConfigAPI"),
                    HandleCommandWorldOpen));

            _commandRegistrations.Add(
                _commandClient.Register(
                    new CommandRegistration(
                        "/cfg", "world-open-server", CommandExecutionLocation.Server, null,
                        "Opens the World smoke config on the authoritative server.",
                        "Primes the server-local World consumer so remote applied broadcasts can be observed in the dedicated-server log.",
                        "world-open-server", "ConfigAPI"),
                    HandleCommandWorldOpen));

            _commandRegistrations.Add(
                _commandClient.Register(
                    new CommandRegistration(
                        "/cfg", "world-save", CommandExecutionLocation.Client, null,
                        "Attempts a World smoke save from this player.",
                        "Saves one string value through the client World path. Server authorization must accept admins and reject non-admins.",
                        "world-save <value>", "ConfigAPI"),
                    HandleCommandWorldSave));

            _commandRegistrations.Add(
                _commandClient.Register(
                    new CommandRegistration(
                        "/cfg", "world-status", CommandExecutionLocation.Either, null,
                        "Reports the last local World smoke response.",
                        "Reports the most recent World smoke response observed on the side where this command executes.",
                        "world-status", "ConfigAPI"),
                    HandleCommandWorldStatus));

            _commandRegistrations.Add(
                _commandClient.Register(
                    new CommandRegistration(
                        "/cfg", "world-save-stale", CommandExecutionLocation.Client, null,
                        "Submits two immediate World smoke saves from this player.",
                        "Submits two saves without waiting for the first response so both use the same base iteration and the second should be rejected as stale.",
                        "world-save-stale <first-value> <second-value>", "ConfigAPI"),
                    HandleCommandWorldSaveStale));
        }

        private CommandResponse HandleCommandHelp(CommandRequest request)
        {
            if (request.Arguments.Length != 0)
                return new CommandResponse(false, "Invalid help request", "Help does not accept arguments.", severity: CommandSeverity.Error, usageHint: "/cfg help");

            return new CommandResponse(
                true,
                "ConfigAPI commands",
                "Available commands: 7",
                new[]
                {
                    "help - Lists ConfigAPI commands.",
                    "status - Reports ConfigAPI runtime status.",
                    "world-open - Opens the shared World smoke config on this client.",
                    "world-open-server - Opens the shared World smoke config on the server.",
                    "world-save <value> - Attempts a player-authorized World save.",
                    "world-save-stale <first-value> <second-value> - Submits two immediate saves to exercise stale-write correction.",
                    "world-status - Reports the last World smoke response on this execution side."
                });
        }

        private CommandResponse HandleCommandWorldOpen(CommandRequest request)
        {
            if (request.Arguments.Length != 0)
                return new CommandResponse(false, "Invalid World open request", "World open does not accept arguments.", severity: CommandSeverity.Error, usageHint: "/cfg " + request.CommandName);
            if (_provider == null || _worldSmokeUnregister == null)
                return new CommandResponse(false, "World configs unavailable", "The internal ConfigAPI World facade is unavailable.", severity: CommandSeverity.Error);

            try
            {
                _provider.OpenWorldInternal(ConfigApiProvider.ApiId, _worldSmokeRegistrationId, WorldSmokeConfigKey, WorldSmokeFile, CreateWorldSmokeDocument("initial"));
                return new CommandResponse(true, "World smoke open requested", "The asynchronous World open request was submitted on the " + (request.IsServer ? "server." : "client."), new[] { "Run /cfg world-status on this side after the response arrives." }, CommandSeverity.Success);
            }
            catch (Exception exception)
            {
                return new CommandResponse(false, "World smoke open failed", exception.Message, severity: CommandSeverity.Error);
            }
        }
        private CommandResponse HandleCommandWorldSave(CommandRequest request)
        {
            if (request.Arguments.Length != 1 || string.IsNullOrWhiteSpace(request.Arguments[0]))
                return new CommandResponse(false, "Invalid World save request", "Expected exactly one non-empty string value.", severity: CommandSeverity.Error, usageHint: "/cfg world-save <value>");
            if (_provider == null || _worldSmokeUnregister == null)
                return new CommandResponse(false, "World configs unavailable", "The internal ConfigAPI World facade is unavailable.", severity: CommandSeverity.Error);

            try
            {
                _provider.SaveWorldInternal(ConfigApiProvider.ApiId, _worldSmokeRegistrationId, WorldSmokeConfigKey, CreateWorldSmokeDocument(request.Arguments[0]));
                return new CommandResponse(true, "World smoke save requested", "The asynchronous player-authorized World save request was submitted.", new[]
                {
                    "Requested value: " + request.Arguments[0],
                    "Requester Steam ID: " + request.RequesterSteamId,
                    "Run /cfg world-status after the response arrives."
                }, CommandSeverity.Information);
            }
            catch (Exception exception)
            {
                return new CommandResponse(false, "World smoke save failed", exception.Message, severity: CommandSeverity.Error);
            }
        }
        private CommandResponse HandleCommandWorldSaveStale(CommandRequest request)
        {
            if (request.Arguments.Length != 2 || string.IsNullOrWhiteSpace(request.Arguments[0]) || string.IsNullOrWhiteSpace(request.Arguments[1]))
                return new CommandResponse(false, "Invalid stale World save request", "Expected exactly two non-empty string values.", severity: CommandSeverity.Error, usageHint: "/cfg world-save-stale <first-value> <second-value>");
            if (_provider == null || _worldSmokeUnregister == null)
                return new CommandResponse(false, "World configs unavailable", "The internal ConfigAPI World facade is unavailable.", severity: CommandSeverity.Error);

            try
            {
                _provider.SaveWorldInternal(ConfigApiProvider.ApiId, _worldSmokeRegistrationId, WorldSmokeConfigKey, CreateWorldSmokeDocument(request.Arguments[0]));
                _provider.SaveWorldInternal(ConfigApiProvider.ApiId, _worldSmokeRegistrationId, WorldSmokeConfigKey, CreateWorldSmokeDocument(request.Arguments[1]));
                return new CommandResponse(true, "World stale smoke saves requested", "Two asynchronous saves were submitted back-to-back without waiting for the first response.", new[]
                {
                    "First value: " + request.Arguments[0],
                    "Second value: " + request.Arguments[1],
                    "Expected result: first save applies; second save returns Stale=True with the first authoritative value.",
                    "Run /cfg world-status after both responses arrive."
                }, CommandSeverity.Information);
            }
            catch (Exception exception)
            {
                return new CommandResponse(false, "World stale smoke save failed", exception.Message, severity: CommandSeverity.Error);
            }
        }
        private CommandResponse HandleCommandWorldStatus(CommandRequest request)
        {
            if (request.Arguments.Length != 0)
                return new CommandResponse(false, "Invalid World status request", "World status does not accept arguments.", severity: CommandSeverity.Error, usageHint: "/cfg world-status");

            var detailLines = new List<string>
            {
                "Execution side: " + (request.IsServer ? "server" : "client"),
                "World facade: " + (_provider != null && _worldSmokeUnregister != null ? "available" : "unavailable")
            };

            IDictionary<string, object> response = _worldSmokeLastResponse;
            if (response == null)
            {
                detailLines.Add("Last response: none");
            }
            else
            {
                detailLines.Add("Operation: " + ResponseText(response, "Operation", "(none)"));
                detailLines.Add("TriggeredBy: " + ResponseText(response, "TriggeredBy", "(none)"));
                detailLines.Add("Applied: " + ResponseText(response, "IsApplied", "(none)"));
                detailLines.Add("Stale: " + ResponseText(response, "IsStale", "(none)"));
                detailLines.Add("Server iteration: " + ResponseText(response, "ServerIteration", "(none)"));
                detailLines.Add("Current file: " + ResponseText(response, "CurrentFile", "(none)"));
                detailLines.Add("Value: " + ReadWorldSmokeValue(response));
                detailLines.Add("Error: " + ResponseText(response, "Error", "(none)"));
            }

            return new CommandResponse(true, "World smoke status", response == null ? "No World smoke response has been observed on this side." : "The last local World smoke response is shown below.", detailLines.ToArray());
        }
        private CommandResponse HandleCommandStatus(CommandRequest request)
        {
            if (request.Arguments.Length != 0)
                return new CommandResponse(false, "Invalid status request", "Status does not accept arguments.", severity: CommandSeverity.Error, usageHint: "/cfg status");

            var otherConsumers = new List<string>();
            string[] consumerIds = _registry != null ? _registry.GetConsumerIds() : new string[0];

            for (int index = 0; index < consumerIds.Length; index++)
            {
                if (!string.Equals(consumerIds[index], ConfigApiProvider.ApiId, StringComparison.Ordinal))
                    otherConsumers.Add(consumerIds[index]);
            }

            var detailLines = new List<string>
            {
                "Provider: " + (_provider != null && _provider.IsStarted ? "started" : "stopped"),
                "Self config: " + (_selfConfigLoaded ? "loaded" : "not loaded"),
                "CommandAPI: " + (_commandClient != null && _commandClient.IsConnected ? "connected" : "disconnected"),
                "Mod version: " + _modVersion,
                "Other registered mods: " + otherConsumers.Count
            };

            int listedConsumerCount = otherConsumers.Count > 3 ? 2 : otherConsumers.Count;

            for (int index = 0; index < listedConsumerCount; index++)
                detailLines.Add(" - " + otherConsumers[index]);

            if (listedConsumerCount < otherConsumers.Count)
                detailLines.Add(" - ... " + (otherConsumers.Count - listedConsumerCount) + " more");

            return new CommandResponse(true, "ConfigAPI status", "ConfigAPI is running.", detailLines.ToArray());
        }

        private void OnWorldConfigResponseReceived(IDictionary<string, object> response)
        {
            if (response == null || !string.Equals(ResponseValue(response, "ConfigKey") as string, WorldSmokeConfigKey, StringComparison.Ordinal))
                return;

            _worldSmokeLastResponse = response;
            string side = _worldNetworkRuntime != null && _worldNetworkRuntime.IsServer ? "server" : "client";
            _logger?.Logger.Info("World smoke response on " + side + ": " + DescribeWorldSmokeResponse(response));
        }

        private static ConfigDocument CreateWorldSmokeDocument(string value)
            => new ConfigDocument(new ConfigObjectNode(new ConfigObjectEntry("Value", ConfigScalarNode.String(value))));

        private static string ReadWorldSmokeValue(IDictionary<string, object> response)
        {
            object payload = ResponseValue(response, "Document");
            if (payload == null)
                return "(none)";

            ConfigDocument document = ConfigDocumentWireCodec.Decode(payload);
            ConfigNode node;
            if (!document.TryGet(new ConfigValuePath("Value"), out node))
                return "(missing)";

            var scalar = node as ConfigScalarNode;
            if (scalar == null)
                return "(" + node.GetType().Name + ")";
            if (scalar.Kind != ConfigScalarKind.String)
                return "(" + scalar.Kind + ")";

            return (string)scalar.Value;
        }

        private static string DescribeWorldSmokeResponse(IDictionary<string, object> response)
        {
            return "operation=" + ResponseText(response, "Operation", "none")
                + ", triggeredBy=" + ResponseText(response, "TriggeredBy", "none")
                + ", applied=" + ResponseText(response, "IsApplied", "none")
                + ", stale=" + ResponseText(response, "IsStale", "none")
                + ", iteration=" + ResponseText(response, "ServerIteration", "none")
                + ", file=" + ResponseText(response, "CurrentFile", "none")
                + ", value=" + ReadWorldSmokeValue(response)
                + ", error=" + ResponseText(response, "Error", "none");
        }

        private static object ResponseValue(IDictionary<string, object> response, string key)
        {
            object value;
            return response != null && response.TryGetValue(key, out value) ? value : null;
        }

        private static string ResponseText(IDictionary<string, object> response, string key, string fallback)
        {
            object value = ResponseValue(response, key);
            return value == null ? fallback : value.ToString();
        }
        private void OnCommandApiConnected()
        {
            _logger?.Logger.Info("Optional CommandAPI consumer connected; /cfg commands are available.");
        }

        private void OnCommandApiDisconnected()
        {
            _logger?.Logger.Warning("Optional CommandAPI consumer disconnected; /cfg commands are unavailable.");
        }

        private void OnCommandRegistrationFailed(CommandRegistration registration, Exception exception)
        {
            _logger?.Logger.Error("CommandAPI registration failed for " + registration.Prefix + " " + registration.CanonicalName + ".", exception);
        }
        private void LoadSelfConfig()
        {
            _logger.Logger.Debug("Loading ConfigAPI self-configuration directly through provider persistence.");

            try
            {
                ConfigDocument document = _provider.OpenInternal(ConfigApiProvider.ApiId, ConfigApiRuntimeConfigDefinition.ConfigKey, ConfigLocation.Local, ConfigApiRuntimeConfigDefinition.FileName, ConfigApiRuntimeConfigDefinition.CreateDefaults());
                ConfigApiRuntimeConfig config = ConfigApiRuntimeConfigDefinition.Deserialize(document);
                _logger.Logger.Info("ConfigAPI runtime config loaded. Applying MinimumLogLevel=" + config.MinimumLogLevel + ".");
                _logger.Logger.MinimumLevel = config.MinimumLogLevel;
                _selfConfigLoaded = true;
            }
            catch (Exception exception)
            {
                _selfConfigLoaded = false;
                _logger.Logger.MinimumLevel = LogLevel.Trace;
                _logger.Logger.Error("Failed to load ConfigAPI.toml. Trace logging remains enabled.", exception);
            }
        }
        protected override void UnloadData()
        {
            _logger?.Logger.Debug("ConfigAPI session unloading.");

            DisposeWorldNetworking();

            try
            {
                if (_commandClient != null)
                {
                    _commandClient.Connected -= OnCommandApiConnected;
                    _commandClient.Disconnected -= OnCommandApiDisconnected;
                    _commandClient.RegistrationFailed -= OnCommandRegistrationFailed;
                }

                for (int index = 0; index < _commandRegistrations.Count; index++)
                    _commandRegistrations[index].Dispose();

                _commandRegistrations.Clear();
                _commandClient?.Dispose();
            }
            catch (Exception exception)
            {
                _logger?.Logger.Error("Optional CommandAPI consumer failed while unloading.", exception);
            }
            finally
            {
                _commandClient = null;
            }

            try
            {
                _worldSmokeUnregister?.Invoke();
            }
            catch (Exception exception)
            {
                _logger?.Logger.Error("ConfigAPI internal World smoke registration failed while unloading.", exception);
            }
            finally
            {
                _worldSmokeUnregister = null;
                _worldSmokeLastResponse = null;
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

                if (_registry != null)
                    _registry.UnregisterInternalStorage(ConfigApiProvider.ApiId);

                _registry = null;
                _selfConfigLoaded = false;

                _logger?.Logger.Info("ConfigAPI session stopped.");
                _logger?.Dispose();
                _logger = null;

                base.UnloadData();
            }
        }
    }
}