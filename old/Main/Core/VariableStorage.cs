using MarcoZechner.ConfigAPI.Main.NetworkCore;
using Sandbox.ModAPI;

namespace MarcoZechner.ConfigAPI.Main.Core
{
    public static class VariableStorage
    {
        private static string VarId(ulong consumerModId, string typeKey)
            => "ConfigAPI.World.AuthXml|" + consumerModId + "|" + typeKey;

        private static string VarIdIter(ulong consumerModId, string typeKey)
            => "ConfigAPI.World.Iter|" + consumerModId + "|" + typeKey;

        private static string VarIdFile(ulong consumerModId, string typeKey)
            => "ConfigAPI.World.File|" + consumerModId + "|" + typeKey;

        public static void Clear(ulong consumerModId, string typeKey)
        {
            MyAPIGateway.Utilities.RemoveVariable(VarId(consumerModId, typeKey));
            MyAPIGateway.Utilities.RemoveVariable(VarIdIter(consumerModId, typeKey));
            MyAPIGateway.Utilities.RemoveVariable(VarIdFile(consumerModId, typeKey));
        }

        public static void Persist(ulong consumerModId, WorldConfigPacket req)
        {
            MyAPIGateway.Utilities.SetVariable(VarId(consumerModId, req.TypeKey), req.XmlData ?? string.Empty);
            MyAPIGateway.Utilities.SetVariable(VarIdIter(consumerModId, req.TypeKey), req.ServerIteration);
            MyAPIGateway.Utilities.SetVariable(VarIdFile(consumerModId, req.TypeKey), req.FileName ?? string.Empty);
        }

        public static bool TryRead(ulong consumerModId, ref WorldState worldState)
        {
            string storedXml;
            if (!MyAPIGateway.Utilities.GetVariable(VarId(consumerModId, worldState.TypeKey), out storedXml))
                return false;

            ulong storedIter;
            MyAPIGateway.Utilities.GetVariable(VarIdIter(consumerModId, worldState.TypeKey), out storedIter);

            string storedFile;
            MyAPIGateway.Utilities.GetVariable(VarIdFile(consumerModId, worldState.TypeKey), out storedFile);

            worldState.AuthXml = storedXml;
            worldState.DraftXml = storedXml;
            worldState.ServerIteration = storedIter;
            worldState.CurrentFile = storedFile;
            return true;
        }
    }
}