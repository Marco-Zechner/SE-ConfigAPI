namespace MarcoZechner.ConfigAPI.Shared.Domain
{
    public enum LocationType
    {
        /// <summary>
        /// Saved in 'SEAppData/Storage/ModFolder'
        /// </summary>
        Local = 0,

        /// <summary>
        /// Saved in 'SEAppData/Storage'
        /// </summary>
        Global = 1,
        /// <summary>
        /// Saved in 'SEAppData/Saves/[steamID]/[WorldName]/Storage/ModFolder'
        /// To use world-files you must use the CfgSync API to ensure multiplayer compatibility.
        /// </summary>
        World = 2
    }
}