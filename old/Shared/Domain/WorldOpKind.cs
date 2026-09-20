namespace MarcoZechner.ConfigAPI.Shared.Domain
{
    public enum WorldOpKind
    {
        Error = 0,
        NoUpdate = 1,
        Get = 2,
        LoadAndSwitch = 3,
        Reload = 4,
        SaveAndSwitch = 5,
        Save = 6,
        Export = 7,
        WorldUpdate = 8,
    }
}