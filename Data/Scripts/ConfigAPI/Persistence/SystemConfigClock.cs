using System;

namespace MarcoZechner.ConfigAPI.V2.Persistence
{
    public sealed class SystemConfigClock : IConfigClock
    {
        public DateTime UtcNow
        {
            get { return DateTime.UtcNow; }
        }
    }
}