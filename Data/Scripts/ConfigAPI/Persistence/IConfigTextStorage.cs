using MarcoZechner.ConfigAPI.V2.Domain;

namespace MarcoZechner.ConfigAPI.V2.Persistence
{
    public interface IConfigTextStorage
    {
        string Read(ConfigLocation location, string file);

        void Write(ConfigLocation location, string file, string content);
    }

    public interface IIndexedConfigTextStorage : IConfigTextStorage
    {
        bool Exists(ConfigLocation location, string file);

        string[] ListKnown(ConfigLocation location);
    }
}
