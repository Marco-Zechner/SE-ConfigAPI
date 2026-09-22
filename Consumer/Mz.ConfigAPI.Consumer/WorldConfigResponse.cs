using System;
using System.Collections.Generic;

namespace Mz.ConfigApi
{
    public sealed class WorldConfigResponse
    {
        private WorldConfigResponse(string configKey, ulong requestId, WorldConfigOperation operation, ulong triggeredBy, bool isChanged, bool isStale, string error, ulong? revision, string currentVariant, ConfigDocument stored, ConfigDocument applied, string[] variants)
        {
            ConfigKey = configKey;
            RequestId = requestId;
            Operation = operation;
            TriggeredBy = triggeredBy;
            IsChanged = isChanged;
            IsStale = isStale;
            Error = error;
            Revision = revision;
            CurrentVariant = currentVariant;
            Stored = stored;
            Applied = applied;
            Variants = variants;
        }

        public string ConfigKey { get; }
        public ulong RequestId { get; }
        public WorldConfigOperation Operation { get; }
        public ulong TriggeredBy { get; }
        public bool IsChanged { get; }
        public bool IsStale { get; }
        public string Error { get; }
        public ulong? Revision { get; }
        public string CurrentVariant { get; }
        public ConfigDocument Stored { get; }
        public ConfigDocument Applied { get; }
        public string[] Variants { get; }
        public bool HasSnapshot => Revision.HasValue && !string.IsNullOrEmpty(CurrentVariant) && Stored != null && Applied != null;
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

            object currentVariantPayload = Value(payload, "CurrentVariant");
            if (currentVariantPayload != null && !(currentVariantPayload is string))
                throw new InvalidOperationException("World config response field 'CurrentVariant' has an invalid type.");

            object storedPayload = Value(payload, "Stored");
            ConfigDocument stored = storedPayload == null ? null : ConfigDocumentWireCodec.Decode(storedPayload);
            object appliedPayload = Value(payload, "Applied");
            ConfigDocument applied = appliedPayload == null ? null : ConfigDocumentWireCodec.Decode(appliedPayload);

            object variantsPayload = Value(payload, "Variants");
            string[] variants = null;
            if (variantsPayload != null)
            {
                var source = variantsPayload as string[];
                if (source == null)
                    throw new InvalidOperationException("World config response field 'Variants' has an invalid type.");

                variants = (string[])source.Clone();
            }

            return new WorldConfigResponse(configKey, Required<ulong>(payload, "RequestId"), operation, Required<ulong>(payload, "TriggeredBy"), Required<bool>(payload, "IsChanged"), Required<bool>(payload, "IsStale"), errorPayload as string, revision, currentVariantPayload as string, stored, applied, variants);
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