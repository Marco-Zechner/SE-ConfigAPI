using System;
using System.Collections.Generic;

namespace Mz.ConfigApi
{
    public sealed class WorldConfigResponse
    {
        private WorldConfigResponse(string configKey, ulong requestId, WorldConfigOperation operation, ulong triggeredBy, bool isApplied, bool isStale, string error, ulong? serverIteration, string currentFile, ConfigDocument document)
        {
            ConfigKey = configKey;
            RequestId = requestId;
            Operation = operation;
            TriggeredBy = triggeredBy;
            IsApplied = isApplied;
            IsStale = isStale;
            Error = error;
            ServerIteration = serverIteration;
            CurrentFile = currentFile;
            Document = document;
        }

        public string ConfigKey { get; }
        public ulong RequestId { get; }
        public WorldConfigOperation Operation { get; }
        public ulong TriggeredBy { get; }
        public bool IsApplied { get; }
        public bool IsStale { get; }
        public string Error { get; }
        public ulong? ServerIteration { get; }
        public string CurrentFile { get; }
        public ConfigDocument Document { get; }
        public bool HasSnapshot => ServerIteration.HasValue && Document != null;
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

            object iterationPayload = Value(payload, "ServerIteration");
            ulong? serverIteration = null;
            if (iterationPayload != null)
            {
                if (!(iterationPayload is ulong))
                    throw new InvalidOperationException("World config response field 'ServerIteration' has an invalid type.");

                serverIteration = (ulong)iterationPayload;
            }

            object currentFilePayload = Value(payload, "CurrentFile");
            if (currentFilePayload != null && !(currentFilePayload is string))
                throw new InvalidOperationException("World config response field 'CurrentFile' has an invalid type.");

            object documentPayload = Value(payload, "Document");
            ConfigDocument document = documentPayload == null ? null : ConfigDocumentWireCodec.Decode(documentPayload);

            return new WorldConfigResponse(
                configKey, Required<ulong>(payload, "RequestId"), operation, Required<ulong>(payload, "TriggeredBy"),
                Required<bool>(payload, "IsApplied"), Required<bool>(payload, "IsStale"), errorPayload as string,
                serverIteration, currentFilePayload as string, document);
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