using System;

namespace MarcoZechner.ConfigAPI.V2.Persistence
{
    public interface IConfigClock
    {
        DateTime UtcNow { get; }
    }
}
