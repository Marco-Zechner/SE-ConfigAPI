using MarcoZechner.ConfigAPI.Shared.Domain;

namespace MarcoZechner.ConfigAPI.Main.NetworkCore
{
    public struct WorldNetRequest
    {
        public ulong ConsumerModId;

        public string TypeKey;
        public WorldOpKind Op;
        public ulong BaseIteration;

        public string FileName;   // request target file (or hint)
        public bool Overwrite;

        public string XmlData;    // draft xml for Save/Export etc
    }
}