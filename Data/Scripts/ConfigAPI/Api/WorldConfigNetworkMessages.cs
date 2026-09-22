using System;
using MarcoZechner.ConfigAPI.V2.Domain;

namespace MarcoZechner.ConfigAPI.V2.Api
{
    public enum WorldConfigNetworkOperation
    {
        Open = 0,
        Apply = 1,
        Save = 2,
        Reload = 3,
        Load = 4,
        SaveAs = 5,
        ListVariants = 6
    }

    public enum WorldConfigNetworkResponseKind
    {
        Snapshot = 0,
        Variants = 1,
        Error = 2
    }

    public sealed class WorldConfigNetworkRequest
    {
        public WorldConfigNetworkRequest(ulong requestId, string consumerId, string configKey, WorldConfigNetworkOperation operation, ulong expectedRevision, string variant, ConfigDocument defaults, ConfigDocument document)
        {
            if (string.IsNullOrWhiteSpace(consumerId))
                throw new ArgumentException("Consumer ID must not be empty.", nameof(consumerId));
            if (string.IsNullOrWhiteSpace(configKey))
                throw new ArgumentException("Config key must not be empty.", nameof(configKey));

            EnsureOperation(operation);

            RequestId = requestId;
            ConsumerId = consumerId.Trim();
            ConfigKey = configKey.Trim();
            Operation = operation;
            ExpectedRevision = expectedRevision;
            Variant = variant;
            Defaults = defaults;
            Document = document;
        }

        public ulong RequestId { get; }
        public string ConsumerId { get; }
        public string ConfigKey { get; }
        public WorldConfigNetworkOperation Operation { get; }
        public ulong ExpectedRevision { get; }
        public string Variant { get; }
        public ConfigDocument Defaults { get; }
        public ConfigDocument Document { get; }

        internal static void EnsureOperation(WorldConfigNetworkOperation operation)
        {
            if (operation < WorldConfigNetworkOperation.Open || operation > WorldConfigNetworkOperation.ListVariants)
                throw new ArgumentException("Unsupported World config network operation: " + operation, nameof(operation));
        }
    }

    public sealed class WorldConfigNetworkResponse
    {
        public WorldConfigNetworkResponse(ulong requestId, WorldConfigNetworkOperation operation, WorldConfigNetworkResponseKind kind, ulong triggeredBy, bool isChanged, bool isStale, WorldConfigSnapshot snapshot, string[] variants, string error)
        {
            WorldConfigNetworkRequest.EnsureOperation(operation);

            if (kind < WorldConfigNetworkResponseKind.Snapshot || kind > WorldConfigNetworkResponseKind.Error)
                throw new ArgumentException("Unsupported World config network response kind: " + kind, nameof(kind));
            if (isChanged && isStale)
                throw new ArgumentException("A World config response cannot be both changed and stale.");

            if (kind == WorldConfigNetworkResponseKind.Snapshot)
            {
                if (snapshot == null)
                    throw new ArgumentNullException(nameof(snapshot));
                if (variants != null)
                    throw new ArgumentException("A snapshot response cannot contain variants.", nameof(variants));
                if (error != null)
                    throw new ArgumentException("A snapshot response cannot contain an error.", nameof(error));
            }
            else if (kind == WorldConfigNetworkResponseKind.Variants)
            {
                if (snapshot != null)
                    throw new ArgumentException("A variants response cannot contain a snapshot.", nameof(snapshot));
                if (variants == null)
                    throw new ArgumentNullException(nameof(variants));
                if (error != null)
                    throw new ArgumentException("A variants response cannot contain an error.", nameof(error));
                if (isChanged || isStale)
                    throw new ArgumentException("A variants response cannot be changed or stale.");
            }
            else
            {
                if (snapshot != null)
                    throw new ArgumentException("An error response cannot contain a snapshot.", nameof(snapshot));
                if (variants != null)
                    throw new ArgumentException("An error response cannot contain variants.", nameof(variants));
                if (string.IsNullOrWhiteSpace(error))
                    throw new ArgumentException("An error response requires an error message.", nameof(error));
                if (isChanged || isStale)
                    throw new ArgumentException("An error response cannot be changed or stale.");
            }

            RequestId = requestId;
            Operation = operation;
            Kind = kind;
            TriggeredBy = triggeredBy;
            IsChanged = isChanged;
            IsStale = isStale;
            Snapshot = snapshot;
            Variants = variants == null ? null : (string[])variants.Clone();
            Error = error;
        }

        public ulong RequestId { get; }
        public WorldConfigNetworkOperation Operation { get; }
        public WorldConfigNetworkResponseKind Kind { get; }
        public ulong TriggeredBy { get; }
        public bool IsChanged { get; }
        public bool IsStale { get; }
        public WorldConfigSnapshot Snapshot { get; }
        public string[] Variants { get; }
        public string Error { get; }
    }
}