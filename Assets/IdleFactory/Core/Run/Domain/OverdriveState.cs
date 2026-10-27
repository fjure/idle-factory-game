using System;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;

namespace IdleFactory.Run.Domain
{
    /// <summary>
    /// Overdrive timers. Both instants are absent (ready) or both are set with
    /// <c>0 &lt;= CooldownUntilUtcMs - ActiveUntilUtcMs &lt;= MaxTimedDurationMs</c>. A fresh activation sets exactly
    /// <c>CooldownMs</c>; a restored state keeps its persisted instants when content was retuned (owner decision 2026-10-01).
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
                if (!ContractBoard.IsTimedSpan(activeUntilUtcMs.Value, cooldownUntilUtcMs.Value, true))
                    throw new ArgumentException("CooldownUntil - ActiveUntil must be 0 to MaxTimedDurationMs.", nameof(cooldownUntilUtcMs));
            }
            ActiveUntilUtcMs = activeUntilUtcMs;
            CooldownUntilUtcMs = cooldownUntilUtcMs;
        }

        /// <summary>True when the timers satisfy the invariant (both absent, or both set and CooldownMs apart).</summary>
        public bool IsConsistent =>
            ActiveUntilUtcMs.HasValue == CooldownUntilUtcMs.HasValue &&
            (!ActiveUntilUtcMs.HasValue || ContractBoard.IsTimedSpan(ActiveUntilUtcMs.Value, CooldownUntilUtcMs.Value, true));

        /// <summary>Restores persisted timers (load path); rejected unless <c>0 &lt;= cooldown - active &lt;= MaxTimedDurationMs</c>.</summary>
        public RunStateResult TryRestore(long? activeUntilUtcMs, long? cooldownUntilUtcMs)
        {
            if (activeUntilUtcMs.HasValue != cooldownUntilUtcMs.HasValue) return RunStateResult.InvalidArgument;
            if (activeUntilUtcMs.HasValue)
            {
                if (!ContractBoard.IsTimedSpan(activeUntilUtcMs.Value, cooldownUntilUtcMs.Value, true))
                    return RunStateResult.InvalidArgument;
            }
            ActiveUntilUtcMs = activeUntilUtcMs;
            CooldownUntilUtcMs = cooldownUntilUtcMs;
            return RunStateResult.Ok;
        }

        public bool IsActive(long nowUtcMs) => ActiveUntilUtcMs.HasValue && nowUtcMs < ActiveUntilUtcMs.Value;

        public bool IsCoolingDown(long nowUtcMs) =>
            CooldownUntilUtcMs.HasValue && !IsActive(nowUtcMs) && nowUtcMs < CooldownUntilUtcMs.Value;

        internal RunStateResult TryActivate(long nowUtcMs)
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
        internal bool ClearElapsed(long nowUtcMs)
        {
            if (!CooldownUntilUtcMs.HasValue || nowUtcMs < CooldownUntilUtcMs.Value) return false;
            ActiveUntilUtcMs = null;
            CooldownUntilUtcMs = null;
            return true;
        }
    }
}
