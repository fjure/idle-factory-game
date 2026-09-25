using System;
using IdleFactory.Core.Application;

namespace IdleFactory.Unity.Persistence
{
    /// <summary>
    /// Reads the device's local system clock as UTC; clock manipulation is outside this contract.
    /// Readings are floored to whole milliseconds because Save V1 stores departure instants as Unix
    /// milliseconds and rejects any value it cannot represent losslessly. Platform clocks (for example
    /// Windows and Android players) report 100 ns ticks, which would otherwise make every save fail.
    /// </summary>
    public sealed class DeviceUtcClock : IUtcClock
    {
        public DateTimeOffset GetUtcNow() => FloorToMilliseconds(DateTimeOffset.UtcNow);

        public static DateTimeOffset FloorToMilliseconds(DateTimeOffset value)
        {
            var ticks = value.UtcTicks;
            return new DateTimeOffset(ticks - (ticks % TimeSpan.TicksPerMillisecond), TimeSpan.Zero);
        }
    }
}
