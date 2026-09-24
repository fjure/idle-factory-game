using System;
using IdleFactory.Core.Application;

namespace IdleFactory.Unity.Persistence
{
    /// <summary>Reads the device's local system clock as UTC; clock manipulation is outside this contract.</summary>
    public sealed class DeviceUtcClock : IUtcClock
    {
        public DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;
    }
}
