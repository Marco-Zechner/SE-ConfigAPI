using System;
using System.Collections.Generic;
using MarcoZechner.ApiLib;
using MarcoZechner.ConfigAPI.Shared.Domain;
using MarcoZechner.ConfigAPI.Shared.Api;

namespace MarcoZechner.ConfigAPI.Main.Api
{
    public class ConfigUserHooks : IConfigUserHooks
    {
        private Func<string, object> _newDefault;
        private Func<string, string> _defaultName;
        private Func<string, object, bool> _isInstanceOf;
        private Func<string, object, string> _serializeToInternalXml;
        private Func<string, string, object> _deserializeFromInternalXml;
        private Func<string, IReadOnlyDictionary<string, string>> _getVariableDescriptions;
        private Func<int, string, string> _loadFile;
        private Action<int, string, string> _saveFile;
        private Action<int, string> _backupFile;

        public ConfigUserHooks(IApiProvider callbackApi)
        {
            var source = callbackApi.ConvertToDict();
            if (source == null)
                throw new Exception("ConfigCallbackApi: ConfigUserHooks provider returned null dictionary");

            var assignments = new Dictionary<string, Action<Delegate>>
            {
                [nameof(NewDefault)] = d => _newDefault = (Func<string, object>)d,
                [nameof(DefaultName)] = d => _defaultName = (Func<string, string>)d,
                [nameof(IsInstanceOf)] = d => _isInstanceOf = (Func<string, object, bool>)d,
                [nameof(SerializeToInternalXml)] = d => _serializeToInternalXml = (Func<string, object, string>)d,
                [nameof(DeserializeFromInternalXml)] = d => _deserializeFromInternalXml = (Func<string, string, object>)d,
                [nameof(GetVariableDescriptions)] = d => _getVariableDescriptions = (Func<string, IReadOnlyDictionary<string, string>>)d,
                [nameof(LoadFile)] = d => _loadFile = (Func<int, string, string>)d,
                [nameof(SaveFile)] = d => _saveFile = (Action<int, string, string>)d,
                [nameof(BackupFile)] = d => _backupFile = (Action<int, string>)d,
            };

            foreach (var assignment in assignments)
            {
                var endpointName = assignment.Key;
                var endpointFunc = assignment.Value;
                Delegate del;
                if (!source.TryGetValue(endpointName, out del))
                    throw new Exception($"ConfigCallbackApi: Missing callback key for '{endpointName}'");
                if (del == null)
                    throw new Exception($"ConfigCallbackApi: Missing callback implementation for '{endpointName}'");

                endpointFunc(del);
            }
        }

        public object NewDefault(string typeKey)
            => _newDefault?.Invoke(typeKey);

        public string DefaultName(string typeKey)
            => _defaultName?.Invoke(typeKey);

        public bool IsInstanceOf(string typeKey, object instance)
            => _isInstanceOf?.Invoke(typeKey, instance) ?? false;

        public string SerializeToInternalXml(string typeKey, object instance)
            => _serializeToInternalXml?.Invoke(typeKey, instance);

        public object DeserializeFromInternalXml(string typeKey, string internalXml)
            => _deserializeFromInternalXml?.Invoke(typeKey, internalXml);

        public IReadOnlyDictionary<string, string> GetVariableDescriptions(string typeKey)
            => _getVariableDescriptions?.Invoke(typeKey);

        public string LoadFile(LocationType locationType, string filename)
            => _loadFile?.Invoke((int)locationType, filename);

        public void SaveFile(LocationType locationType, string filename, string content)
            => _saveFile?.Invoke((int)locationType, filename, content);

        public void BackupFile(LocationType locationType, string filename)
            => _backupFile?.Invoke((int)locationType, filename);
    }
}