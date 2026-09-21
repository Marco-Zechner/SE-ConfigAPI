using System;
using MarcoZechner.ConfigAPI.V2.Domain;

namespace MarcoZechner.ConfigAPI.V2.Api
{
    public sealed class WorldConfigServerRequestHandler
    {
        public const string PermissionDeniedError = "Permission denied: Only admins can perform this operation.";

        private readonly WorldConfigServerService _service;
        private readonly IWorldConfigAuthorization _authorization;

        public WorldConfigServerRequestHandler(WorldConfigServerService service, IWorldConfigAuthorization authorization)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));
            if (authorization == null)
                throw new ArgumentNullException(nameof(authorization));

            _service = service;
            _authorization = authorization;
        }

        public WorldConfigNetworkResponse Handle(ulong requesterId, WorldConfigNetworkRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.Operation != WorldConfigNetworkOperation.Open && !_authorization.IsAdmin(requesterId))
                return Error(request, requesterId, PermissionDeniedError);

            switch (request.Operation)
            {
                case WorldConfigNetworkOperation.Open:
                    return HandleOpen(requesterId, request);
                case WorldConfigNetworkOperation.Reload:
                    return HandleReload(requesterId, request);
                case WorldConfigNetworkOperation.LoadAndSwitch:
                    return HandleLoadAndSwitch(requesterId, request);
                case WorldConfigNetworkOperation.Save:
                    return HandleSave(requesterId, request);
                case WorldConfigNetworkOperation.SaveAndSwitch:
                    return HandleSaveAndSwitch(requesterId, request);
                case WorldConfigNetworkOperation.Export:
                    return HandleExport(requesterId, request);
                case WorldConfigNetworkOperation.ApplyPreset:
                    return HandleApplyPreset(requesterId, request);
                case WorldConfigNetworkOperation.SavePreset:
                    return HandleSavePreset(requesterId, request);
                default:
                    throw new ArgumentException("Unsupported World config network operation: " + request.Operation, nameof(request));
            }
        }

        private WorldConfigNetworkResponse HandleOpen(ulong requesterId, WorldConfigNetworkRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.File))
                return Error(request, requesterId, "Open requires a config file.");
            if (request.Defaults == null)
                return Error(request, requesterId, "Open requires current defaults.");

            WorldConfigSnapshot snapshot = _service.Open(request.ConsumerId, request.ConfigKey, request.File, request.Defaults);
            return Snapshot(request, requesterId, false, false, snapshot);
        }

        private WorldConfigNetworkResponse HandleReload(ulong requesterId, WorldConfigNetworkRequest request)
        {
            WorldConfigAuthorityResult result = _service.Reload(request.ConsumerId, request.ConfigKey, request.BaseIteration);
            return Snapshot(request, requesterId, result.IsApplied, result.IsStale, result.Snapshot);
        }

        private WorldConfigNetworkResponse HandleLoadAndSwitch(ulong requesterId, WorldConfigNetworkRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.File))
                return Error(request, requesterId, "LoadAndSwitch requires a config file.");

            WorldConfigAuthorityResult result = _service.LoadAndSwitch(request.ConsumerId, request.ConfigKey, request.BaseIteration, request.File);
            return Snapshot(request, requesterId, result.IsApplied, result.IsStale, result.Snapshot);
        }

        private WorldConfigNetworkResponse HandleSave(ulong requesterId, WorldConfigNetworkRequest request)
        {
            if (request.Document == null)
                return Error(request, requesterId, "Save requires a config document.");

            WorldConfigAuthorityResult result = _service.Save(request.ConsumerId, request.ConfigKey, request.BaseIteration, request.Document);
            return Snapshot(request, requesterId, result.IsApplied, result.IsStale, result.Snapshot);
        }

        private WorldConfigNetworkResponse HandleSaveAndSwitch(ulong requesterId, WorldConfigNetworkRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.File))
                return Error(request, requesterId, "SaveAndSwitch requires a config file.");
            if (request.Document == null)
                return Error(request, requesterId, "SaveAndSwitch requires a config document.");

            WorldConfigAuthorityResult result = _service.SaveAndSwitch(request.ConsumerId, request.ConfigKey, request.BaseIteration, request.Document, request.File);
            return Snapshot(request, requesterId, result.IsApplied, result.IsStale, result.Snapshot);
        }

        private WorldConfigNetworkResponse HandleApplyPreset(ulong requesterId, WorldConfigNetworkRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.File))
                return Error(request, requesterId, "ApplyPreset requires a preset file.");

            WorldConfigAuthorityResult result = _service.ApplyPreset(request.ConsumerId, request.ConfigKey, request.BaseIteration, request.File);
            return Snapshot(request, requesterId, result.IsApplied, result.IsStale, result.Snapshot);
        }

        private WorldConfigNetworkResponse HandleSavePreset(ulong requesterId, WorldConfigNetworkRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.File))
                return Error(request, requesterId, "SavePreset requires a preset file.");
            if (request.Document == null)
                return Error(request, requesterId, "SavePreset requires a config document.");

            WorldConfigExport saved = _service.SavePreset(request.ConsumerId, request.ConfigKey, request.Document, request.File, request.Overwrite);
            return new WorldConfigNetworkResponse(request.RequestId, request.Operation, WorldConfigNetworkResponseKind.Exported, requesterId, false, false, saved.Authoritative, null);
        }
        private WorldConfigNetworkResponse HandleExport(ulong requesterId, WorldConfigNetworkRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.File))
                return Error(request, requesterId, "Export requires a config file.");
            if (request.Document == null)
                return Error(request, requesterId, "Export requires a config document.");

            WorldConfigExport export = _service.Export(request.ConsumerId, request.ConfigKey, request.Document, request.File, request.Overwrite);
            return new WorldConfigNetworkResponse(request.RequestId, request.Operation, WorldConfigNetworkResponseKind.Exported, requesterId, false, false, export.Authoritative, null);
        }

        private static WorldConfigNetworkResponse Snapshot(WorldConfigNetworkRequest request, ulong requesterId, bool isApplied, bool isStale, WorldConfigSnapshot snapshot)
            => new WorldConfigNetworkResponse(request.RequestId, request.Operation, WorldConfigNetworkResponseKind.Snapshot, requesterId, isApplied, isStale, snapshot, null);

        private static WorldConfigNetworkResponse Error(WorldConfigNetworkRequest request, ulong requesterId, string error)
            => new WorldConfigNetworkResponse(request.RequestId, request.Operation, WorldConfigNetworkResponseKind.Error, requesterId, false, false, null, error);
    }
}