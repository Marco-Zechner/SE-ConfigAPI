using System;

namespace MarcoZechner.ConfigAPI.Persistence
{
    public sealed class SystemConfigClock : IConfigClock
    {
        public DateTime UtcNow
        {
            get { return DateTime.UtcNow; }
        }
    }
}