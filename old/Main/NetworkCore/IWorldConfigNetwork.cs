namespace MarcoZechner.ConfigAPI.Main.NetworkCore
{
    /// <summary>
    /// Per-consumer sending facade used by ServerConfigService.
    /// </summary>
    public interface IWorldConfigNetwork
    {
        bool SendRequest(WorldNetRequest req);
    }
}