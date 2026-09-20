using System;
using System.Collections.Generic;
using MarcoZechner.ApiLib;
using MarcoZechner.ConfigAPI.Scripts.ConfigAPI.Shared;
using MarcoZechner.ConfigAPI.Shared.Domain;
using MarcoZechner.ConfigAPI.Shared.Api;
using VRage;

namespace MarcoZechner.ConfigAPI.Client.Api
{
    public sealed class ConfigService : IConfigService
    {
        private Func<string, int, string, object> _clientConfigGet;
        private Func<string, int, object> _clientConfigReload;
        private Func<string, int, string> _clientConfigGetCurrentFileName;
        private Func<string, int, string, object> _clientConfigLoadAndSwitch;
        private Func<string, int, bool> _clientConfigSave;
        private Func<string, int, string, object> _clientConfigSaveAndSwitch;
        private Func<string, int, string, bool, bool> _clientConfigExport;
        private Action<string, string> _serverConfigInit;
        private Func<string, MyTuple<int, string, ulong, ulong, string>> _serverConfigGetUpdate;
        private Func<string, object> _worldGetAuth;
        private Func<string, object> _worldGetDraft;
        private Action<string> _worldResetDraft;
        private Func<string, ulong, bool> _worldReload;
        private Func<string, string, ulong, bool> _worldLoadAndSwitch;
        private Func<string, ulong, bool> _worldSave;
        private Func<string, string, ulong, bool> _worldSaveAndSwitch;
        private Func<string, string, bool, bool> _worldExport;


        public ConfigService(IApiProvider mainApi)
        {
            var source = mainApi.ConvertToDict();
            if (source == null)
                throw new Exception("ConfigApi: ConfigService provider returned null dictionary");

            var assignments = new Dictionary<string, Action<Delegate>>
            {
                [nameof(ClientConfigGet)] = d => _clientConfigGet = (Func<string, int, string, object>)d,
                [nameof(ClientConfigReload)] = d => _clientConfigReload = (Func<string, int, object>)d,
                [nameof(ClientConfigGetCurrentFileName)] = d => _clientConfigGetCurrentFileName = (Func<string, int, string>)d,
                [nameof(ClientConfigLoadAndSwitch)] = d => _clientConfigLoadAndSwitch = (Func<string, int, string, object>)d,
                [nameof(ClientConfigSave)] = d => _clientConfigSave = (Func<string, int, bool>)d,
                [nameof(ClientConfigSaveAndSwitch)] = d => _clientConfigSaveAndSwitch = (Func<string, int, string, object>)d,
                [nameof(ClientConfigExport)] = d => _clientConfigExport = (Func<string, int, string, bool, bool>)d,
                [nameof(ServerConfigInit)] = d => _serverConfigInit = (Action<string, string>)d,
                [nameof(ServerConfigGetUpdate)] = d => _serverConfigGetUpdate = (Func<string, MyTuple<int, string, ulong, ulong, string>>)d,
                [nameof(ServerConfigGetAuth)] = d => _worldGetAuth = (Func<string, object>)d,
                [nameof(ServerConfigGetDraft)] = d => _worldGetDraft = (Func<string, object>)d,
                [nameof(ServerConfigResetDraft)] = d => _worldResetDraft = (Action<string>)d,
                [nameof(ServerConfigReload)] = d => _worldReload = (Func<string, ulong, bool>)d,
                [nameof(ServerConfigLoadAndSwitch)] = d => _worldLoadAndSwitch = (Func<string, string, ulong, bool>)d,
                [nameof(ServerConfigSave)] = d => _worldSave = (Func<string, ulong, bool>)d,
                [nameof(ServerConfigSaveAndSwitch)] = d => _worldSaveAndSwitch = (Func<string, string, ulong, bool>)d,
                [nameof(ServerConfigExport)] = d => _worldExport = (Func<string, string, bool, bool>)d,
            };



            foreach (var assignment in assignments)
            {
                var endpointName = assignment.Key;
                var endpointFunc = assignment.Value;
                Delegate del;
                if (!source.TryGetValue(endpointName, out del))
                    throw new Exception($"ConfigApi: Missing key for '{endpointName}'");
                if (del == null)
                    throw new Exception($"ConfigApi: Missing implementation for '{endpointName}'");

                endpointFunc(del);
            }
        }

        // -------------------------
        // Client config: local only

        public object ClientConfigGet(string typeKey, LocationType locationType, string filename)
            => _clientConfigGet?.Invoke(typeKey, (int)locationType, filename);

        public object ClientConfigReload(string typeKey, LocationType locationType)
            => _clientConfigReload?.Invoke(typeKey, (int)locationType);

        public string ClientConfigGetCurrentFileName(string typeKey, LocationType locationType)
            => _clientConfigGetCurrentFileName?.Invoke(typeKey, (int)locationType);

        public object ClientConfigLoadAndSwitch(string typeKey, LocationType locationType, string filename)
            => _clientConfigLoadAndSwitch?.Invoke(typeKey, (int)locationType, filename);

        public bool ClientConfigSave(string typeKey, LocationType locationType)
            => _clientConfigSave?.Invoke(typeKey, (int)locationType) ?? false;

        public object ClientConfigSaveAndSwitch(string typeKey, LocationType locationType, string filename)
            => _clientConfigSaveAndSwitch?.Invoke(typeKey, (int)locationType, filename);

        public bool ClientConfigExport(string typeKey, LocationType locationType, string filename, bool overwrite)
            => _clientConfigExport?.Invoke(typeKey, (int)locationType, filename, overwrite) ?? false;

        // -------------------------
        // World config: client-side sync surface

        public void ServerConfigInit(string typeKey, string defaultFile)
            => _serverConfigInit?.Invoke(typeKey, defaultFile);

        public CfgUpdate ServerConfigGetUpdate(string typeKey)
        {
            var result = _serverConfigGetUpdate?.Invoke(typeKey);
            if (result == null)
            {
                CfgLog.Error($"{nameof(ServerConfigGetUpdate)} returned null for typeKey '{typeKey}'");
                return null;
            }

            var cfgUpdateTuple = (MyTuple<int, string, ulong, ulong, string>)result;

            return new CfgUpdate
            {
                WorldOpKind = (WorldOpKind)cfgUpdateTuple.Item1,
                Error = cfgUpdateTuple.Item2,
                TriggeredBy = cfgUpdateTuple.Item3,
                ServerIteration = cfgUpdateTuple.Item4,
                CurrentFile = cfgUpdateTuple.Item5
            };
        }

        public object ServerConfigGetAuth(string typeKey)
            => _worldGetAuth?.Invoke(typeKey);

        public object ServerConfigGetDraft(string typeKey)
            => _worldGetDraft?.Invoke(typeKey);

        public void ServerConfigResetDraft(string typeKey)
            => _worldResetDraft?.Invoke(typeKey);

        public bool ServerConfigReload(string typeKey, ulong baseIteration)
            => _worldReload?.Invoke(typeKey, baseIteration) ?? false;

        public bool ServerConfigLoadAndSwitch(string typeKey, string file, ulong baseIteration)
            => _worldLoadAndSwitch?.Invoke(typeKey, file, baseIteration) ?? false;

        public bool ServerConfigSave(string typeKey, ulong baseIteration)
            => _worldSave?.Invoke(typeKey, baseIteration) ?? false;

        public bool ServerConfigSaveAndSwitch(string typeKey, string file, ulong baseIteration)
            => _worldSaveAndSwitch?.Invoke(typeKey, file, baseIteration) ?? false;

        public bool ServerConfigExport(string typeKey, string file, bool overwrite)
            => _worldExport?.Invoke(typeKey, file, overwrite) ?? false;
    }
}