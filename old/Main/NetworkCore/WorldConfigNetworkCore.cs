using System;
using System.Collections.Generic;
using System.Linq;
using Digi.NetworkLib;
using MarcoZechner.ConfigAPI.Main.Core;
using MarcoZechner.ConfigAPI.Main.Domain;
using MarcoZechner.ConfigAPI.Scripts.ConfigAPI.Shared;
using MarcoZechner.ConfigAPI.Shared.Api;
using Sandbox.ModAPI;
using MarcoZechner.ConfigAPI.Shared.Domain;
using VRage.Game.ModAPI;

namespace MarcoZechner.ConfigAPI.Main.NetworkCore
{
    public sealed class WorldConfigNetworkCore : IWorldConfigNetworkCore
    {
        public List<ulong> RegisteredConsumerModIds => _sinks.Keys.ToList();

        public static WorldConfigNetworkCore Instance { get; private set; }

        private readonly Network _net;

        private readonly Dictionary<ulong, IWorldConfigClientSink> _sinks
            = new Dictionary<ulong, IWorldConfigClientSink>();
        private readonly Dictionary<ulong, IInternalConfigService> _configServices
            = new Dictionary<ulong, IInternalConfigService>();
        private readonly Dictionary<ulong, IConfigUserHooks> _userHooks
            = new Dictionary<ulong, IConfigUserHooks>();
        private readonly Dictionary<ulong, Dictionary<string, ulong>> _currentIterationsPerMod
            = new Dictionary<ulong, Dictionary<string, ulong>>();

        public WorldConfigNetworkCore(Network net)
        {
            if (net == null) throw new ArgumentNullException(nameof(net));

            _net = net;
            Instance = this;
        }

        public void Unload()
        {
            if (ReferenceEquals(Instance, this))
                Instance = null;

            _sinks.Clear();
            _configServices.Clear();
            _userHooks.Clear();
            _currentIterationsPerMod.Clear();
        }

        public void RegisterConsumer(ulong consumerModId, IWorldConfigClientSink sink, IInternalConfigService configService, IConfigUserHooks userHooks)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (configService == null) throw new ArgumentNullException(nameof(configService));
            if (userHooks == null) throw new ArgumentNullException(nameof(userHooks));
            _sinks[consumerModId] = sink;
            _configServices[consumerModId] = configService;
            _userHooks[consumerModId] = userHooks;
            _currentIterationsPerMod[consumerModId] = new Dictionary<string, ulong>();
        }

        public void UnregisterConsumer(ulong consumerModId)
        {
            _sinks.Remove(consumerModId);
            _configServices.Remove(consumerModId);
            _userHooks.Remove(consumerModId);
            _currentIterationsPerMod.Remove(consumerModId);
        }

        public IWorldConfigNetwork CreateConsumerFacade(ulong consumerModId)
        {
            return new ConsumerFacade(this, consumerModId);
        }

        // Called by WorldConfigPacket.Received(...)
        public void OnPacketReceived(WorldConfigPacket p, ref PacketInfo packetInfo, ulong senderSteamId)
        {
            if (p == null)
                return;

            if (MyAPIGateway.Multiplayer == null)
                return;

            if (MyAPIGateway.Multiplayer.IsServer && !p.IsRelayPacket)
                HandleOnServer(p, ref packetInfo, senderSteamId);
            else
                HandleOnClient(p);
        }

        private static IMyPlayer GetPlayerBySteamId(ulong steamId)
        {
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            return players.FirstOrDefault(p => p.SteamUserId == steamId);
        }

        private void HandleOnServer(WorldConfigPacket req, ref PacketInfo packetInfo, ulong senderSteamId)
        {
            // check if sender is admin via IMyPlayer.PromoteLevel
            var player = GetPlayerBySteamId(senderSteamId);
            if (player == null || player.PromoteLevel < MyPromoteLevel.Admin)
            {
                if (req.Op != WorldOpKind.Get)
                {
                    CfgLogWorld.Warning($"Player {player?.DisplayName ?? "Unknown"} ({senderSteamId}) attempted a restricted WorldConfig operation: {req.Op}");
                    req.Op = WorldOpKind.Error;
                    req.Error = "Permission denied: Only admins can perform this operation.";
                    req.TriggeredBy = senderSteamId;

                    packetInfo.Relay = RelayMode.ReturnToSender;
                    packetInfo.Reserialize = true;
                    return;
                }
            }

            var consumerModId = req.ConsumerModId;
            IInternalConfigService cfgService;
            if (!_configServices.TryGetValue(consumerModId, out cfgService))
            {
                req.Op = WorldOpKind.Error;
                req.Error = $"No config service registered for ConsumerModId {consumerModId}";
                CfgLogWorld.Error(req.Error);
                req.TriggeredBy = senderSteamId;

                packetInfo.Relay = RelayMode.ReturnToSender;
                packetInfo.Reserialize = true;
                return;
            }

            // Turn request object into response/broadcast.
            req.TriggeredBy = senderSteamId;

            // Broadcast authoritative snapshot to everyone (including sender)
            packetInfo.Reserialize = true;
            var typeKey = req.TypeKey;
            var fileName = req.FileName;
            const LocationType location = LocationType.World;
            object newInstance;

            Dictionary<string, ulong> currentIterations;
            if (!_currentIterationsPerMod.TryGetValue(consumerModId, out currentIterations))
            {
                currentIterations = new Dictionary<string, ulong>();
                _currentIterationsPerMod[consumerModId] = currentIterations;
            }

            if (!currentIterations.ContainsKey(typeKey))
                currentIterations[typeKey] = 0;

            // Check for stale requests
            var currentIteration = currentIterations[typeKey];
            if (req.BaseIteration != currentIteration)
            {
                if (req.Op != WorldOpKind.Get)
                {
                    req.Op = WorldOpKind.Error;
                    req.Error = $"Stale request detected for typeKey {typeKey}. CurrentIteration={currentIteration}, RequestBaseIteration={req.BaseIteration}";
                    packetInfo.Relay = RelayMode.ReturnToSender;
                    packetInfo.Reserialize = true;

                    bool wasCached;
                    newInstance = cfgService.ConfigGet(typeKey, location, fileName, out wasCached);
                    if (newInstance != null)
                    {
                        req.XmlData = _userHooks[consumerModId].SerializeToInternalXml(req.TypeKey, newInstance);
                        req.Op = WorldOpKind.WorldUpdate;
                        if (wasCached)
                        {
                            req.ServerIteration = currentIterations[typeKey];
                            packetInfo.Relay = RelayMode.ReturnToSender;
                            return;
                        }
                        req.ServerIteration = ++currentIterations[typeKey];
                        packetInfo.Relay = RelayMode.ToEveryone;
                        VariableStorage.Persist(consumerModId, req);
                        return;
                    }
                    req.Error += $"\nConfigGet returned null for typeKey {typeKey} at location {location} with fileName {fileName}";
                    CfgLogWorld.Warning(req.Error);
                    return;
                }
            }

            switch (req.Op)
            {
                case WorldOpKind.Get:
                    bool wasCached;
                    newInstance = cfgService.ConfigGet(typeKey, location, fileName, out wasCached);
                    if (newInstance != null)
                    {
                        req.XmlData = _userHooks[consumerModId].SerializeToInternalXml(req.TypeKey, newInstance);
                        if (wasCached)
                        {
                            req.ServerIteration = currentIterations[typeKey];
                            req.Op = WorldOpKind.WorldUpdate;
                            packetInfo.Relay = RelayMode.ReturnToSender;
                            return;
                        }
                        req.ServerIteration = ++currentIterations[typeKey];
                        req.Op = WorldOpKind.WorldUpdate;
                        packetInfo.Relay = RelayMode.ToEveryone;
                        VariableStorage.Persist(consumerModId, req);
                        return;
                    }
                    req.Error = $"ConfigGet returned null for typeKey {typeKey} at location {location} with fileName {fileName}";
                    break;
                case WorldOpKind.LoadAndSwitch:
                    newInstance = cfgService.ConfigLoadAndSwitch(typeKey, location, fileName);
                    if (newInstance != null)
                    {
                        req.XmlData = _userHooks[consumerModId].SerializeToInternalXml(req.TypeKey, newInstance);
                        req.ServerIteration = ++currentIterations[typeKey];
                        req.Op = WorldOpKind.WorldUpdate;
                        packetInfo.Relay = RelayMode.ToEveryone;
                        VariableStorage.Persist(consumerModId, req);
                        return;
                    }
                    req.Error = $"ConfigLoadAndSwitch returned null for typeKey {typeKey} at location {location} with fileName {fileName}";
                    break;
                case WorldOpKind.Reload:
                    newInstance = cfgService.ConfigReload(typeKey, location);
                    if (newInstance != null)
                    {
                        req.XmlData = _userHooks[consumerModId].SerializeToInternalXml(req.TypeKey, newInstance);
                        req.ServerIteration = ++currentIterations[typeKey];
                        req.Op = WorldOpKind.WorldUpdate;
                        packetInfo.Relay = RelayMode.ToEveryone;
                        VariableStorage.Persist(consumerModId, req);
                        return;
                    }
                    req.Error = $"ConfigReload returned null for typeKey {typeKey} at location {location}";
                    break;
                case WorldOpKind.SaveAndSwitch:
                    newInstance = cfgService.ConfigSaveAndSwitch(typeKey, location, fileName, req.XmlData);
                    if (newInstance != null)
                    {
                        req.XmlData = _userHooks[consumerModId].SerializeToInternalXml(req.TypeKey, newInstance);
                        req.ServerIteration = ++currentIterations[typeKey];
                        req.Op = WorldOpKind.WorldUpdate;
                        packetInfo.Relay = RelayMode.ToEveryone;
                        VariableStorage.Persist(consumerModId, req);
                        return;
                    }
                    req.Error = $"ConfigSaveAndSwitch returned null for typeKey {typeKey} at location {location} with fileName {fileName}";
                    break;
                case WorldOpKind.Save:
                    if (cfgService.ConfigSave(typeKey, location, req.XmlData))
                    {
                        req.FileName = cfgService.ConfigGetCurrentFileName(typeKey, location);
                        bool _;
                        var inst = cfgService.ConfigGet(typeKey, location, req.FileName, out _);
                        if (inst != null)
                            req.XmlData = _userHooks[consumerModId].SerializeToInternalXml(typeKey, inst);

                        req.ServerIteration = ++currentIterations[typeKey];
                        req.Op = WorldOpKind.WorldUpdate;
                        packetInfo.Relay = RelayMode.ToEveryone;
                        VariableStorage.Persist(consumerModId, req);
                        return;
                    }
                    req.Error = $"ConfigSave failed for typeKey {typeKey} at location {location}";
                    break;
                case WorldOpKind.Export:
                    if (cfgService.ConfigExport(typeKey, location, fileName, req.Overwrite))
                    {
                        req.ServerIteration = currentIterations[typeKey];
                        packetInfo.Relay = RelayMode.ReturnToSender;
                        return;
                    }
                    req.Error = $"ConfigExport failed for typeKey {typeKey} at location {location} with fileName {fileName}";
                    break;
                case WorldOpKind.NoUpdate:
                case WorldOpKind.Error:
                case WorldOpKind.WorldUpdate:
                default:
                    throw new Exception($"Invalid WorldOpKind: {req.Op}");
            }

            req.Op = WorldOpKind.Error;
            CfgLogWorld.Warning(req.Error);
            packetInfo.Relay = RelayMode.ReturnToSender;
        }

        private void HandleOnClient(WorldConfigPacket msg)
        {
            IWorldConfigClientSink sink;
            if (!_sinks.TryGetValue(msg.ConsumerModId, out sink))
                return;

            sink.OnServerWorldUpdate(msg);
        }

        // ===============================================================
        // Per-consumer facade (stamps ConsumerModId)
        // ===============================================================

        private sealed class ConsumerFacade : IWorldConfigNetwork
        {
            private readonly WorldConfigNetworkCore _core;
            private readonly ulong _consumerModId;

            public ConsumerFacade(WorldConfigNetworkCore core, ulong consumerModId)
            {
                if (core == null) throw new ArgumentNullException(nameof(core));
                _core = core;
                _consumerModId = consumerModId;
            }

            public bool SendRequest(WorldNetRequest req)
            {
                var p = new WorldConfigPacket
                {
                    ConsumerModId = _consumerModId,
                    TypeKey = req.TypeKey,
                    Op = req.Op,
                    BaseIteration = req.BaseIteration,
                    Overwrite = req.Overwrite,
                    XmlData = req.XmlData,
                    FileName = req.FileName
                };

                _core._net.SendToServer(p);
                return true;
            }
        }
    }
}
