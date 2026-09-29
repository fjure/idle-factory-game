using System;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;

namespace IdleFactory.Run.Domain
{
    /// <summary>
    /// Overdrive timers. Both instants are absent (ready) or both are set with
    /// <c>CooldownUntilUtcMs - ActiveUntilUtcMs = CooldownMs</c>.
    /// </summary>
    public sealed class OverdriveState
    {
        private readonly OverdriveDef _def;

        public long? ActiveUntilUtcMs { get; private set; }
        public long? CooldownUntilUtcMs { get; private set; }

        public OverdriveState(OverdriveDef def, long? activeUntilUtcMs = null, long? cooldownUntilUtcMs = null)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            if (activeUntilUtcMs.HasValue != cooldownUntilUtcMs.HasValue)
                throw new ArgumentException("Both Overdrive instants or neither.", nameof(cooldownUntilUtcMs));
            if (activeUntilUtcMs.HasValue)
            {
                if (activeUntilUtcMs.Value < 0 || cooldownUntilUtcMs.Value - activeUntilUtcMs.Value != def.CooldownMs)
                    throw new ArgumentException("CooldownUntil - ActiveUntil must equal CooldownMs.", nameof(cooldownUntilUtcMs));
            }
            ActiveUntilUtcMs = activeUntilUtcMs;
            CooldownUntilUtcMs = cooldownUntilUtcMs;
        }

        public bool IsActive(long nowUtcMs) => ActiveUntilUtcMs.HasValue && nowUtcMs < ActiveUntilUtcMs.Value;

        public bool IsCoolingDown(long nowUtcMs) =>
            CooldownUntilUtcMs.HasValue && !IsActive(nowUtcMs) && nowUtcMs < CooldownUntilUtcMs.Value;

        public RunStateResult TryActivate(long nowUtcMs)
        {
            if (nowUtcMs < 0) return RunStateResult.InvalidArgument;
            if (IsActive(nowUtcMs)) return RunStateResult.OverdriveActive;
            if (IsCoolingDown(nowUtcMs)) return RunStateResult.OverdriveCoolingDown;
            if (!CheckedMath.TryAdd(nowUtcMs, _def.DurationMs, out long activeUntil)) return RunStateResult.ValueLimit;
            if (!CheckedMath.TryAdd(activeUntil, _def.CooldownMs, out long cooldownUntil)) return RunStateResult.ValueLimit;
            ActiveUntilUtcMs = activeUntil;
            CooldownUntilUtcMs = cooldownUntil;
            return RunStateResult.Ok;
        }

        /// <summary>Returns to ready once the cooldown has elapsed (<c>CooldownUntil &lt;= now</c>).</summary>
        public bool ClearElapsed(long nowUtcMs)
        {
            if (!CooldownUntilUtcMs.HasValue || nowUtcMs < CooldownUntilUtcMs.Value) return false;
            ActiveUntilUtcMs = null;
            CooldownUntilUtcMs = null;
            return true;
        }
    }
}
