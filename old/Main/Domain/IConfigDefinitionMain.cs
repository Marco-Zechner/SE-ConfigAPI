using System.Collections.Generic;

namespace MarcoZechner.ConfigAPI.Main.Domain
{
    public interface IConfigDefinitionMain
    {
        string TypeName { get; }
        string GetCurrentDefaultsInternalXml();
        IReadOnlyDictionary<string,string> GetVariableDescriptions();
    }
}