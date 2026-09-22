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
                case WorldConfigNetworkOperation.Apply:
                    return HandleApply(requesterId, request);
                case WorldConfigNetworkOperation.Save:
                    return HandleSave(requesterId, request);
                case WorldConfigNetworkOperation.Reload:
                    return HandleReload(requesterId, request);
                case WorldConfigNetworkOperation.Load:
                    return HandleLoad(requesterId, request);
                case WorldConfigNetworkOperation.SaveAs:
                    return HandleSaveAs(requesterId, request);
                case WorldConfigNetworkOperation.ListVariants:
                    return HandleListVariants(requesterId, request);
                default:
                    throw new ArgumentException("Unsupported World config network operation: " + request.Operation, nameof(request));
            }
        }

        private WorldConfigNetworkResponse HandleOpen(ulong requesterId, WorldConfigNetworkRequest request)
        {
            if (request.Defaults == null)
                return Error(request, requesterId, "Open requires current defaults.");

            WorldConfigSnapshot snapshot = _service.Open(request.ConsumerId, request.ConfigKey, request.Defaults);
            return Snapshot(request, requesterId, false, false, snapshot);
        }

        private WorldConfigNetworkResponse HandleApply(ulong requesterId, WorldConfigNetworkRequest request)
        {
            if (request.Document == null)
                return Error(request, requesterId, "Apply requires a config document.");

            WorldConfigAuthorityResult result = _service.Apply(request.ConsumerId, request.ConfigKey, request.ExpectedRevision, request.Document);
            return Snapshot(request, requesterId, result.IsChanged, result.IsStale, result.Snapshot);
        }

        private WorldConfigNetworkResponse HandleSave(ulong requesterId, WorldConfigNetworkRequest request)
        {
            WorldConfigAuthorityResult result = _service.Save(request.ConsumerId, request.ConfigKey, request.ExpectedRevision);
            return Snapshot(request, requesterId, result.IsChanged, result.IsStale, result.Snapshot);
        }

        private WorldConfigNetworkResponse HandleReload(ulong requesterId, WorldConfigNetworkRequest request)
        {
            WorldConfigAuthorityResult result = _service.Reload(request.ConsumerId, request.ConfigKey, request.ExpectedRevision);
            return Snapshot(request, requesterId, result.IsChanged, result.IsStale, result.Snapshot);
        }

        private WorldConfigNetworkResponse HandleLoad(ulong requesterId, WorldConfigNetworkRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Variant))
                return Error(request, requesterId, "Load requires a variant.");

            WorldConfigAuthorityResult result = _service.Load(request.ConsumerId, request.ConfigKey, request.ExpectedRevision, request.Variant);
            return Snapshot(request, requesterId, result.IsChanged, result.IsStale, result.Snapshot);
        }

        private WorldConfigNetworkResponse HandleSaveAs(ulong requesterId, WorldConfigNetworkRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Variant))
                return Error(request, requesterId, "SaveAs requires a variant.");

            WorldConfigAuthorityResult result = _service.SaveAs(request.ConsumerId, request.ConfigKey, request.ExpectedRevision, request.Variant);
            return Snapshot(request, requesterId, result.IsChanged, result.IsStale, result.Snapshot);
        }

        private WorldConfigNetworkResponse HandleListVariants(ulong requesterId, WorldConfigNetworkRequest request)
        {
            string[] variants = _service.ListVariants(request.ConsumerId, request.ConfigKey);
            return new WorldConfigNetworkResponse(request.RequestId, request.Operation, WorldConfigNetworkResponseKind.Variants, requesterId, false, false, null, variants, null);
        }

        private static WorldConfigNetworkResponse Snapshot(WorldConfigNetworkRequest request, ulong requesterId, bool isChanged, bool isStale, WorldConfigSnapshot snapshot)
            => new WorldConfigNetworkResponse(request.RequestId, request.Operation, WorldConfigNetworkResponseKind.Snapshot, requesterId, isChanged, isStale, snapshot, null, null);

        private static WorldConfigNetworkResponse Error(WorldConfigNetworkRequest request, ulong requesterId, string error)
            => new WorldConfigNetworkResponse(request.RequestId, request.Operation, WorldConfigNetworkResponseKind.Error, requesterId, false, false, null, null, error);
    }
}