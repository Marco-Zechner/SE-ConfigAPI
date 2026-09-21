using System;
using MarcoZechner.ConfigAPI.V2.Domain;

namespace MarcoZechner.ConfigAPI.V2.Api
{
    public enum WorldConfigNetworkOperation
    {
        Open = 0,
        Reload = 1,
        LoadAndSwitch = 2,
        Save = 3,
        SaveAndSwitch = 4,
        Export = 5,
        ApplyPreset = 6,
        SavePreset = 7
    }

    public enum WorldConfigNetworkResponseKind
    {
        Snapshot = 0,
        Exported = 1,
        Error = 2
    }

    public sealed class WorldConfigNetworkRequest
    {
        public WorldConfigNetworkRequest(ulong requestId, string consumerId, string configKey, WorldConfigNetworkOperation operation,
                                         ulong baseIteration, string file, bool overwrite, ConfigDocument defaults, ConfigDocument document)
        {
            if (string.IsNullOrWhiteSpace(consumerId))
                throw new ArgumentException("Consumer ID must not be empty.", nameof(consumerId));
            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));

            EnsureOperation(operation);

            RequestId = requestId;
            ConsumerId = consumerId;
            ConfigKey = configKey;
            Operation = operation;
            BaseIteration = baseIteration;
            File = file;
            Overwrite = overwrite;
            Defaults = defaults;
            Document = document;
        }

        public ulong RequestId { get; }
        public string ConsumerId { get; }
        public string ConfigKey { get; }
        public WorldConfigNetworkOperation Operation { get; }
        public ulong BaseIteration { get; }
        public string File { get; }
        public bool Overwrite { get; }
        public ConfigDocument Defaults { get; }
        public ConfigDocument Document { get; }

        internal static void EnsureOperation(WorldConfigNetworkOperation operation)
        {
            if (operation < WorldConfigNetworkOperation.Open || operation > WorldConfigNetworkOperation.SavePreset)
                throw new ArgumentException("Unsupported World config network operation: " + operation, nameof(operation));
        }
    }

    public sealed class WorldConfigNetworkResponse
    {
        public WorldConfigNetworkResponse(ulong requestId, WorldConfigNetworkOperation operation, WorldConfigNetworkResponseKind kind,
                                          ulong triggeredBy, bool isApplied, bool isStale, WorldConfigSnapshot snapshot, string error)
        {
            WorldConfigNetworkRequest.EnsureOperation(operation);

            if (kind < WorldConfigNetworkResponseKind.Snapshot || kind > WorldConfigNetworkResponseKind.Error)
                throw new ArgumentException("Unsupported World config network response kind: " + kind, nameof(kind));
            if (isApplied && isStale)
                throw new ArgumentException("A World config response cannot be both applied and stale.");

            if (kind == WorldConfigNetworkResponseKind.Error)
            {
                if (snapshot != null)
                    throw new ArgumentException("An error response cannot contain an authoritative snapshot.", nameof(snapshot));
                if (string.IsNullOrWhiteSpace(error))
                    throw new ArgumentException("An error response requires an error message.", nameof(error));
                if (isApplied || isStale)
                    throw new ArgumentException("An error response cannot be marked applied or stale.");
            }
            else
            {
                if (snapshot == null)
                    throw new ArgumentNullException(nameof(snapshot));
                if (error != null)
                    throw new ArgumentException("A successful World config response cannot contain an error.", nameof(error));
            }

            RequestId = requestId;
            Operation = operation;
            Kind = kind;
            TriggeredBy = triggeredBy;
            IsApplied = isApplied;
            IsStale = isStale;
            Snapshot = snapshot;
            Error = error;
        }

        public ulong RequestId { get; }
        public WorldConfigNetworkOperation Operation { get; }
        public WorldConfigNetworkResponseKind Kind { get; }
        public ulong TriggeredBy { get; }
        public bool IsApplied { get; }
        public bool IsStale { get; }
        public WorldConfigSnapshot Snapshot { get; }
        public string Error { get; }
    }
}