using System;

namespace MarcoZechner.ConfigAPI.Persistence
{
    public interface IConfigClock
    {
        DateTime UtcNow { get; }
    }
}
