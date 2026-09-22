using System;
using MarcoZechner.ConfigAPI.Domain;
using Sandbox.ModAPI;

namespace MarcoZechner.ConfigAPI.Api
{
    public interface IWorldConfigBootstrapVariables
    {
        bool TryRead(string name, out string value);
        void Write(string name, string value);
    }

    public sealed class WorldConfigBootstrapStore : IWorldConfigBootstrapStore
    {
        private const string VariablePrefix = "ConfigAPI.World.Bootstrap|";
        private const int MaximumEncodedLength = 25165824;
        private readonly IWorldConfigBootstrapVariables _variables;

        public WorldConfigBootstrapStore(IWorldConfigBootstrapVariables variables)
        {
            if (variables == null)
                throw new ArgumentNullException(nameof(variables));

            _variables = variables;
        }

        public bool TryRead(ConfigIdentity identity, out WorldConfigSnapshot snapshot)
        {
            if (identity == null)
                throw new ArgumentNullException(nameof(identity));

            snapshot = null;

            try
            {
                string encoded;
                if (!_variables.TryRead(VariableName(identity), out encoded) || string.IsNullOrEmpty(encoded) || encoded.Length > MaximumEncodedLength)
                    return false;

                byte[] payload = Convert.FromBase64String(encoded);
                WorldConfigNetworkResponse response = WorldConfigNetworkCodec.DecodeResponse(payload);

                if (response.Operation != WorldConfigNetworkOperation.Open ||
                    response.Kind != WorldConfigNetworkResponseKind.Snapshot ||
                    response.Snapshot == null ||
                    !identity.Equals(response.Snapshot.Identity))
                {
                    return false;
                }

                snapshot = response.Snapshot;
                return true;
            }
            catch
            {
                snapshot = null;
                return false;
            }
        }

        public void Write(WorldConfigSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            try
            {
                var response = new WorldConfigNetworkResponse(
                    0UL, WorldConfigNetworkOperation.Open, WorldConfigNetworkResponseKind.Snapshot,
                    0UL, false, false, snapshot, null, null);

                string encoded = Convert.ToBase64String(WorldConfigNetworkCodec.EncodeResponse(response));
                _variables.Write(VariableName(snapshot.Identity), encoded);
            }
            catch
            {
            }
        }

        private static string VariableName(ConfigIdentity identity)
            => VariablePrefix + identity.OwnerId.Length + ":" + identity.OwnerId + identity.ConfigKey.Length + ":" + identity.ConfigKey;
    }

    internal sealed class SpaceEngineersWorldConfigBootstrapVariables : IWorldConfigBootstrapVariables
    {
        public bool TryRead(string name, out string value)
        {
            value = null;
            var utilities = MyAPIGateway.Utilities;
            return utilities != null && utilities.GetVariable(name, out value);
        }

        public void Write(string name, string value)
        {
            var utilities = MyAPIGateway.Utilities;
            if (utilities != null)
                utilities.SetVariable(name, value);
        }
    }
}