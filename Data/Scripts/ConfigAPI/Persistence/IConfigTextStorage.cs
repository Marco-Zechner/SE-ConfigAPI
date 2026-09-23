using MarcoZechner.ConfigAPI.Domain;

namespace MarcoZechner.ConfigAPI.Persistence
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
