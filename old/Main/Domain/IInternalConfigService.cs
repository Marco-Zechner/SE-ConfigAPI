using MarcoZechner.ConfigAPI.Shared.Domain;

namespace MarcoZechner.ConfigAPI.Main.Domain
{
    public interface IInternalConfigService
    {
        object ConfigGet(string typeKey, LocationType locationType, string filename, out bool wasCached);
        object ConfigReload(string typeKey, LocationType locationType);
        string ConfigGetCurrentFileName(string typeKey, LocationType locationType);
        object ConfigLoadAndSwitch(string typeKey, LocationType locationType, string filename);
        bool ConfigSave(string typeKey, LocationType locationType, string xmlOverride = null);
        object ConfigSaveAndSwitch(string typeKey, LocationType locationType, string filename, string xmlOverride = null);
        bool ConfigExport(string typeKey, LocationType locationType, string filename, bool overwrite);
    }
}