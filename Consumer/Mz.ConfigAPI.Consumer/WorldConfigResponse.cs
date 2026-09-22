using System;
using System.Collections.Generic;

namespace Mz.ConfigApi
{
    public sealed class WorldConfigResponse
    {
        private WorldConfigResponse(string configKey, ulong requestId, WorldConfigOperation operation, ulong triggeredBy, bool isApplied, bool isStale, string error, ulong? revision, string currentFile, ConfigDocument stored, ConfigDocument applied)
        {
            ConfigKey = configKey;
            RequestId = requestId;
            Operation = operation;
            TriggeredBy = triggeredBy;
            IsApplied = isApplied;
            IsStale = isStale;
            Error = error;
            Revision = revision;
            CurrentFile = currentFile;
            Stored = stored;
            Applied = applied;
        }

        public string ConfigKey { get; }
        public ulong RequestId { get; }
        public WorldConfigOperation Operation { get; }
        public ulong TriggeredBy { get; }
        public bool IsApplied { get; }
        public bool IsStale { get; }
        public string Error { get; }
        public ulong? Revision { get; }
        public ulong? ServerIteration => Revision;
        public string CurrentFile { get; }
        public ConfigDocument Stored { get; }
        public ConfigDocument Applied { get; }
        public ConfigDocument Document => Applied;
        public bool HasSnapshot => Revision.HasValue && Stored != null && Applied != null;
        public bool HasUnsavedChanges => HasSnapshot && !Applied.Equals(Stored);
        public bool IsError => !string.IsNullOrEmpty(Error);

        internal static WorldConfigResponse FromPayload(IDictionary<string, object> payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            string configKey = Required<string>(payload, "ConfigKey");
            string operationText = Required<string>(payload, "Operation");
            WorldConfigOperation operation;

            if (!Enum.TryParse(operationText, false, out operation))
                throw new InvalidOperationException("Unsupported World config operation in provider response: " + operationText);

            object errorPayload = Value(payload, "Error");
            if (errorPayload != null && !(errorPayload is string))
                throw new InvalidOperationException("World config response field 'Error' has an invalid type.");

            object revisionPayload = Value(payload, "Revision");
            ulong? revision = null;
            if (revisionPayload != null)
            {
                if (!(revisionPayload is ulong))
                    throw new InvalidOperationException("World config response field 'Revision' has an invalid type.");

                revision = (ulong)revisionPayload;
            }

            object currentFilePayload = Value(payload, "CurrentFile");
            if (currentFilePayload != null && !(currentFilePayload is string))
                throw new InvalidOperationException("World config response field 'CurrentFile' has an invalid type.");

            object storedPayload = Value(payload, "Stored");
            ConfigDocument stored = storedPayload == null ? null : ConfigDocumentWireCodec.Decode(storedPayload);
            object appliedPayload = Value(payload, "Applied");
            ConfigDocument applied = appliedPayload == null ? null : ConfigDocumentWireCodec.Decode(appliedPayload);

            return new WorldConfigResponse(
                configKey, Required<ulong>(payload, "RequestId"), operation, Required<ulong>(payload, "TriggeredBy"),
                Required<bool>(payload, "IsApplied"), Required<bool>(payload, "IsStale"), errorPayload as string,
                revision, currentFilePayload as string, stored, applied);
        }

        private static object Value(IDictionary<string, object> payload, string key)
        {
            object value;
            if (!payload.TryGetValue(key, out value))
                throw new InvalidOperationException("World config response is missing field '" + key + "'.");

            return value;
        }

        private static T Required<T>(IDictionary<string, object> payload, string key)
        {
            object value = Value(payload, key);
            if (!(value is T))
                throw new InvalidOperationException("World config response field '" + key + "' has an invalid type.");

            return (T)value;
        }
    }
}