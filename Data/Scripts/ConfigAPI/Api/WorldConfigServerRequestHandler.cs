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

                case WorldConfigNetworkOperation.Save:
                    return HandleSave(requesterId, request);

                case WorldConfigNetworkOperation.Reload:
                case WorldConfigNetworkOperation.LoadAndSwitch:
                case WorldConfigNetworkOperation.SaveAndSwitch:
                case WorldConfigNetworkOperation.Export:
                    return Error(request, requesterId, "World config operation is not implemented yet: " + request.Operation);

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

            return new WorldConfigNetworkResponse(
                request.RequestId, request.Operation, WorldConfigNetworkResponseKind.Snapshot,
                requesterId, false, false, snapshot, null);
        }

        private WorldConfigNetworkResponse HandleSave(ulong requesterId, WorldConfigNetworkRequest request)
        {
            if (request.Document == null)
                return Error(request, requesterId, "Save requires a config document.");

            WorldConfigAuthorityResult result = _service.Save(
                request.ConsumerId, request.ConfigKey, request.BaseIteration, request.Document);

            return new WorldConfigNetworkResponse(
                request.RequestId, request.Operation, WorldConfigNetworkResponseKind.Snapshot,
                requesterId, result.IsApplied, result.IsStale, result.Snapshot, null);
        }

        private static WorldConfigNetworkResponse Error(WorldConfigNetworkRequest request, ulong requesterId, string error)
            => new WorldConfigNetworkResponse(
                request.RequestId, request.Operation, WorldConfigNetworkResponseKind.Error,
                requesterId, false, false, null, error);
    }
}