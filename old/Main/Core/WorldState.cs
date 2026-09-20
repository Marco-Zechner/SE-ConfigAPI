using System.Collections.Generic;
using MarcoZechner.ConfigAPI.Main.Domain;
using MarcoZechner.ConfigAPI.Shared.Domain;

namespace MarcoZechner.ConfigAPI.Main.Core
{
    public sealed class WorldState
    {
        public readonly string TypeKey;
        public readonly HooksDefinitionMain DefinitionMain;

        public string CurrentFile;
        public ulong ServerIteration;

        public string AuthXml;
        public object AuthObj;

        public string DraftXml;
        public object DraftObj;

        public readonly Queue<CfgUpdate> Updates = new Queue<CfgUpdate>();

        public WorldState(string typeKey, HooksDefinitionMain definitionMain)
        {
            TypeKey = typeKey;
            DefinitionMain = definitionMain;
        }

        public override string ToString()
        {
            return $"WorldState(TypeKey={TypeKey}, CurrentFile={CurrentFile}, ServerIteration={ServerIteration}, AuthXmlLen={(AuthXml != null ? AuthXml.Length : 0)}, DraftXmlLen={(DraftXml != null ? DraftXml.Length : 0)}, UpdatesCount={Updates.Count})";
        }
    }
}