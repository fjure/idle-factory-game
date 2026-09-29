using System;

namespace IdleFactory.Run.Content
{
    public sealed class OverdriveDef
    {
        public long DurationMs { get; }
        public long CooldownMs { get; }
        public long SpeedPpm { get; }
        public UnlockCondition Unlock { get; }
        public OverdriveDef(long durationMs, long cooldownMs, long speedPpm, UnlockCondition unlock)
        {
            DurationMs = ContentGuard.PositiveQuantity(durationMs, nameof(durationMs));
            CooldownMs = ContentGuard.Quantity(cooldownMs, nameof(cooldownMs));
            if (speedPpm <= Curve.Ppm || speedPpm > IdleFactory.Core.Numerics.ExactBound.Value)
                throw new ArgumentOutOfRangeException(nameof(speedPpm));
            SpeedPpm = speedPpm; Unlock = unlock ?? throw new ArgumentNullException(nameof(unlock));
        }
    }
}
