using IdleFactory.Core.Numerics;
using IdleFactory.Run.Domain;

namespace IdleFactory.Run.Application
{
    /// <summary>Result of <see cref="RunCommands.ActivateOverdrive"/>.</summary>
    public readonly struct OverdriveActivationResult
    {
        public long StartUtcMs { get; }

        /// <summary>The exclusive end of the boost, <c>start + durationMs</c>.</summary>
        public long ActiveUntilUtcMs { get; }

        /// <summary>The instant Overdrive can be activated again, <c>start + durationMs + cooldownMs</c>.</summary>
        public long CooldownUntilUtcMs { get; }

        public OverdriveActivationResult(long startUtcMs, long activeUntilUtcMs, long cooldownUntilUtcMs)
        {
            StartUtcMs = startUtcMs;
            ActiveUntilUtcMs = activeUntilUtcMs;
            CooldownUntilUtcMs = cooldownUntilUtcMs;
        }
    }

    public sealed partial class RunCommands
    {
        /// <summary>
        /// Activates Overdrive. Rejected with command.invalid while it is not unlocked (the reserved
        /// <see cref="RunState.OverdriveUnlockId"/>; the contract has no code for it and the UI shows the control
        /// locked, so this mirrors the T137 convention for a locked target), overdrive.active (it cannot be
        /// re-activated while running), overdrive.cooling-down (<c>timeline &lt; cooldownUntil</c>) or value.limit.
        /// Sets <c>activeUntil = t + duration</c> and <c>cooldownUntil = activeUntil + cooldown</c>; the settle
        /// rescales every machine to the boosted cycle.
        /// </summary>
        public RunCommandResult<OverdriveActivationResult> ActivateOverdrive()
        {
            if (_session.IsLifecycleBlocked) return Reject<OverdriveActivationResult>(RunReasons.LifecycleBlocked);
            RunState state = State;
            if (!state.IsUnlocked(RunState.OverdriveUnlockId)) return Reject<OverdriveActivationResult>(RunReasons.CommandInvalid);
            long now = _session.TimelineUtcMs;
            if (state.Overdrive.IsActive(now)) return Reject<OverdriveActivationResult>(RunReasons.OverdriveActive);
            if (state.Overdrive.IsCoolingDown(now)) return Reject<OverdriveActivationResult>(RunReasons.OverdriveCoolingDown);
            if (!CheckedMath.TryAdd(now, _catalog.Overdrive.DurationMs, out long activeUntil) ||
                !CheckedMath.TryAdd(activeUntil, _catalog.Overdrive.CooldownMs, out long cooldownUntil) ||
                !ExactBound.Contains(cooldownUntil) || !ChangeFits(true))
                return Reject<OverdriveActivationResult>(RunReasons.ValueLimit);

            _session.BeginCommand();
            RunStateResult activated = state.Overdrive.TryActivate(now);
            if (activated != RunStateResult.Ok) return Reject<OverdriveActivationResult>(RunReasons.From(activated));
            return _session.EndCommand(new OverdriveActivationResult(now, activeUntil, cooldownUntil), true);
        }
    }
}
