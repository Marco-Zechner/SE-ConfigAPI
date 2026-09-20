using System;
using System.Collections.Generic;
using MarcoZechner.ApiLib;
using MarcoZechner.ConfigAPI.Main.Core;
using MarcoZechner.ConfigAPI.Main.Core.Migrator;
using MarcoZechner.ConfigAPI.Main.Core.XmlConverter;
using MarcoZechner.ConfigAPI.Main.NetworkCore;
using MarcoZechner.ConfigAPI.Shared.Domain;
using MarcoZechner.ConfigAPI.Shared.Api;
using VRage;

namespace MarcoZechner.ConfigAPI.Main.Api
{
    /// <summary>
    /// Main API bound to a single consumer mod.
    /// No modId needs to be passed on calls anymore.
    /// </summary>
    public sealed class ConfigServiceImpl : IConfigService, IApiProvider
    {
        internal WorldConfigClientService WorldConfigClientService => _worldConfigClientService;
        internal InternalConfigService InternalConfigService => _internalConfigService;

        private readonly ulong _consumerModId;
        private readonly string _consumerModName;
        private readonly ConfigUserHooks _configUserHooks;
        private readonly InternalConfigService _internalConfigService;
        private readonly WorldConfigClientService _worldConfigClientService;

        public ConfigServiceImpl(ulong modId, string modName, ConfigUserHooks configUserHooks, IWorldConfigNetwork worldNet)
        {
            _consumerModId = modId;
            _consumerModName = modName;
            _configUserHooks = configUserHooks;
            _internalConfigService = new InternalConfigService(
                _configUserHooks,
                new TomlXmlConverter(),
                new ConfigLayoutMigrator()
            );

            _worldConfigClientService = new WorldConfigClientService(
                _consumerModId,
                _configUserHooks,
                new ConfigLayoutMigrator(),
                worldNet
            );
        }

        // Client-side APIs

        public object ClientConfigGet(string typeKey, LocationType locationType, string filename)
        {
            bool _;
            return _internalConfigService.ConfigGet(typeKey, locationType, filename, out _);
        }

        public object ClientConfigReload(string typeKey, LocationType locationType)
            => _internalConfigService.ConfigReload(typeKey, locationType);

        public string ClientConfigGetCurrentFileName(string typeKey, LocationType locationType)
            => _internalConfigService.ConfigGetCurrentFileName(typeKey, locationType);

        public object ClientConfigLoadAndSwitch(string typeKey, LocationType locationType, string filename)
            => _internalConfigService.ConfigLoadAndSwitch(typeKey, locationType, filename);

        public bool ClientConfigSave(string typeKey, LocationType locationType)
            => _internalConfigService.ConfigSave(typeKey, locationType);

        public object ClientConfigSaveAndSwitch(string typeKey, LocationType locationType, string filename)
            => _internalConfigService.ConfigSaveAndSwitch(typeKey, locationType, filename);

        public bool ClientConfigExport(string typeKey, LocationType locationType, string filename, bool overwrite)
            => _internalConfigService.ConfigExport(typeKey, locationType, filename, overwrite);

        // Server-side APIs

        public void ServerConfigInit(string typeKey, string defaultFile)
            => _worldConfigClientService.ServerConfigInit(typeKey, defaultFile);

        public CfgUpdate ServerConfigGetUpdate(string typeKey)
            => _worldConfigClientService.ServerConfigGetUpdate(typeKey);

        public object ServerConfigGetAuth(string typeKey)
            => _worldConfigClientService.ServerConfigGetAuth(typeKey);

        public object ServerConfigGetDraft(string typeKey)
            => _worldConfigClientService.ServerConfigGetDraft(typeKey);

        public void ServerConfigResetDraft(string typeKey)
            => _worldConfigClientService.ServerConfigResetDraft(typeKey);

        public bool ServerConfigReload(string typeKey, ulong baseIteration)
            => _worldConfigClientService.ServerConfigReload(typeKey, baseIteration);

        public bool ServerConfigLoadAndSwitch(string typeKey, string file, ulong baseIteration)
            => _worldConfigClientService.ServerConfigLoadAndSwitch(typeKey, file, baseIteration);

        public bool ServerConfigSave(string typeKey, ulong baseIteration)
            => _worldConfigClientService.ServerConfigSave(typeKey, baseIteration);

        public bool ServerConfigSaveAndSwitch(string typeKey, string file, ulong baseIteration)
            => _worldConfigClientService.ServerConfigSaveAndSwitch(typeKey, file, baseIteration);

        public bool ServerConfigExport(string typeKey, string file, bool overwrite)
            => _worldConfigClientService.ServerConfigExport(typeKey, file, overwrite);

        public Dictionary<string, Delegate> ConvertToDict()
        {
            return new Dictionary<string, Delegate>
            {
                { nameof(ClientConfigGet), new Func<string, int, string, object>(ClientConfigGetInternal) },
                { nameof(ClientConfigReload), new Func<string, int, object>(ClientConfigReloadInternal) },
                { nameof(ClientConfigGetCurrentFileName), new Func<string, int, string>(ClientConfigGetCurrentFileNameInternal) },
                { nameof(ClientConfigLoadAndSwitch), new Func<string, int, string, object>(ClientConfigLoadAndSwitchInternal) },
                { nameof(ClientConfigSave), new Func<string, int, bool>(ClientConfigSaveInternal) },
                { nameof(ClientConfigSaveAndSwitch), new Func<string, int, string, object>(ClientConfigSaveAndSwitchInternal) },
                { nameof(ClientConfigExport), new Func<string, int, string, bool, bool>(ClientConfigExportInternal) },
                { nameof(ServerConfigInit), new Action<string, string>(ServerConfigInit) },
                { nameof(ServerConfigGetUpdate), new Func<string, MyTuple<int, string, ulong, ulong, string>>(ServerConfigGetUpdateInternal) },
                { nameof(ServerConfigGetAuth), new Func<string, object>(ServerConfigGetAuth) },
                { nameof(ServerConfigGetDraft), new Func<string, object>(ServerConfigGetDraft) },
                { nameof(ServerConfigResetDraft), new Action<string>(ServerConfigResetDraft) },
                { nameof(ServerConfigReload), new Func<string, ulong, bool>(ServerConfigReload) },
                { nameof(ServerConfigLoadAndSwitch), new Func<string, string, ulong, bool>(ServerConfigLoadAndSwitch) },
                { nameof(ServerConfigSave), new Func<string, ulong, bool>(ServerConfigSave) },
                { nameof(ServerConfigSaveAndSwitch), new Func<string, string, ulong, bool>(ServerConfigSaveAndSwitch) },
                { nameof(ServerConfigExport), new Func<string, string, bool, bool>(ServerConfigExport) },
            };
        }

        // ===============================================================
        // Internal conversion methods for delegate to custom types
        // ===============================================================

        private object ClientConfigGetInternal(string typeKey, int locationTypeEnum, string filename)
            => ClientConfigGet(typeKey, (LocationType)locationTypeEnum, filename);
        private object ClientConfigReloadInternal(string typeKey, int locationTypeEnum)
            => ClientConfigReload(typeKey, (LocationType)locationTypeEnum);
        private string ClientConfigGetCurrentFileNameInternal(string typeKey, int locationTypeEnum)
            => ClientConfigGetCurrentFileName(typeKey, (LocationType)locationTypeEnum);
        private object ClientConfigLoadAndSwitchInternal(string typeKey, int locationTypeEnum, string filename)
            => ClientConfigLoadAndSwitch(typeKey, (LocationType)locationTypeEnum, filename);
        private bool ClientConfigSaveInternal(string typeKey, int locationTypeEnum)
            => ClientConfigSave(typeKey, (LocationType)locationTypeEnum);
        private object ClientConfigSaveAndSwitchInternal(string typeKey, int locationTypeEnum, string filename)
            => ClientConfigSaveAndSwitch(typeKey, (LocationType)locationTypeEnum, filename);
        private bool ClientConfigExportInternal(string typeKey, int locationTypeEnum, string filename, bool overwrite)
            => ClientConfigExport(typeKey, (LocationType)locationTypeEnum, filename, overwrite);

        private MyTuple<int, string, ulong, ulong, string> ServerConfigGetUpdateInternal(string typeKey)
        {
            var cfgUpdate = ServerConfigGetUpdate(typeKey);
            if (cfgUpdate == null)
            {
                return new MyTuple<int, string, ulong, ulong, string>();
            }

            var resultsTuple = new MyTuple<int, string, ulong, ulong, string>();

            resultsTuple.Item1 = (int)cfgUpdate.WorldOpKind;
            resultsTuple.Item2 = cfgUpdate.Error;
            resultsTuple.Item3 = cfgUpdate.TriggeredBy;
            resultsTuple.Item4 = cfgUpdate.ServerIteration;
            resultsTuple.Item5 = cfgUpdate.CurrentFile;
            return resultsTuple;
        }
    }
}