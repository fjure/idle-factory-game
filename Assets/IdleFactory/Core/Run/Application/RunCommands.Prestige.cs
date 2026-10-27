using System;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;

namespace IdleFactory.Run.Application
{
    /// <summary>
    /// The prestige commands of User Story 4 (contracts/run-application.md): the preview, the confirmation that
    /// builds and holds the next-run candidate, its promotion and its abandonment. The promotion is durable whenever a
    /// <see cref="RunPersistence"/> is attached to the session (T105): <see cref="PromotePrestige"/> and
    /// <see cref="CancelPendingTransition"/> then run through its protocol (write the candidate checkpoint, read it back,
    /// verify, and only then publish), so a prestige cannot be published without its write. A session without
    /// persistence promotes in memory (domain tests).
    ///
    /// Flow: <see cref="GetPrestigePreview"/> (read-only; declining it needs no call) - <see cref="ConfirmPrestige"/>
    /// (re-checks eligibility at the command instant, builds the candidate and leaves the transition pending, the old
    /// run untouched) - <see cref="PromotePrestige"/> (publishes the candidate; T105 calls it after the durable
    /// promotion is verified) or <see cref="CancelPendingTransition"/> (drops the candidate, old run kept).
    /// While pending, every command and <see cref="RunSession.Advance"/> is rejected with <c>lifecycle.blocked</c>
    /// and a second confirmation with <c>transition.pending</c> (QA-4): the check of the pending transition comes
    /// first, so the second confirmation reports the more specific code.
    /// </summary>
    public sealed partial class RunCommands
    {
        /// <summary>
        /// The prestige surface: Factory Value breakdown, the IP award of the current value, eligibility with the
        /// missing value (against the rising threshold <c>PrestigeThreshold.For(min(PrestigeCount, MaxIndex))</c>, T331), the next threshold,
        /// and the reset and preserve lists. Read-only and available while the lifecycle is blocked.
        /// Rejected with command.invalid (no permanent state) or value.limit.
        /// </summary>
        public RunCommandResult<PrestigePreview> GetPrestigePreview()
        {
            RunState state = State;
            if (Permanent == null) return Reject<PrestigePreview>(RunReasons.CommandInvalid);
            PrestigeDef prestige = _catalog.Prestige;
            if (!FactoryValuation.TryCompute(_catalog, state, Permanent, out FactoryValueBreakdown value) ||
                !FactoryValuation.TryComputeIp(prestige, value.Total, out long ip) ||
                !CheckedMath.TryAdd(Permanent.Ip, ip, out long ipAfter) ||
                !PrestigeThreshold.TryForCount(prestige, Permanent.PrestigeCount, out long threshold) ||
                !PrestigeThreshold.TryNextForCount(prestige, Permanent.PrestigeCount, out long nextThreshold))
                return Reject<PrestigePreview>(RunReasons.ValueLimit);

            string reason = _session.PendingTransition != null ? RunReasons.TransitionPending
                : value.Total < threshold ? RunReasons.PrestigeBelowThreshold : null;
            long items = 0;
            foreach (IdCount stack in Permanent.ItemStacks()) items += stack.Count;
            return RunCommandResult.Success(new PrestigePreview(value, threshold, ip, Permanent.Ip, ipAfter,
                reason == null, reason, state.RunNumber + 1, NextRunStart.Compute(_catalog, Permanent), items, state.Effects.Count,
                state.Contracts.Active?.OfferId, nextThreshold, PrestigeThreshold.Index(prestige, Permanent.PrestigeCount)));
        }

        /// <summary>
        /// Confirms the prestige. Rejected with transition.pending (a candidate is already pending), lifecycle.blocked,
        /// command.invalid (no permanent state), prestige.below-threshold (<c>FV &lt; threshold</c> at this instant)
        /// or value.limit. On success the next-run candidate is built (see <see cref="PrestigeTransition"/>) and held
        /// as <see cref="RunSession.PendingTransition"/>; neither the run nor the permanent state changed yet.
        /// </summary>
        public RunCommandResult<PrestigeConfirmation> ConfirmPrestige()
        {
            if (_session.PendingTransition != null) return Reject<PrestigeConfirmation>(RunReasons.TransitionPending);
            if (_session.IsLifecycleBlocked) return Reject<PrestigeConfirmation>(RunReasons.LifecycleBlocked);
            RunState state = State;
            if (Permanent == null) return Reject<PrestigeConfirmation>(RunReasons.CommandInvalid);
            PrestigeDef prestige = _catalog.Prestige;
            if (!FactoryValuation.TryCompute(_catalog, state, Permanent, out FactoryValueBreakdown value))
                return Reject<PrestigeConfirmation>(RunReasons.ValueLimit);
            if (!PrestigeThreshold.TryForCount(prestige, Permanent.PrestigeCount, out long threshold))
                return Reject<PrestigeConfirmation>(RunReasons.ValueLimit);
            if (value.Total < threshold) return Reject<PrestigeConfirmation>(RunReasons.PrestigeBelowThreshold);
            if (!FactoryValuation.TryComputeIp(prestige, value.Total, out long ip)) return Reject<PrestigeConfirmation>(RunReasons.ValueLimit);
            _session.NoteFactorySale(true); // T364: eligible at the latest now, for a session whose goal line never saw it

            string rejected = PrestigeTransition.TryBuild(_catalog, state, Permanent, value, ip, _newRunId(), _session.TimelineUtcMs,
                out PrestigeCandidate candidate);
            if (rejected != null) return Reject<PrestigeConfirmation>(rejected);

            _session.SetPendingTransition(candidate);
            return RunCommandResult.Success(new PrestigeConfirmation(state.RunId, candidate));
        }

        /// <summary>
        /// Publishes the pending candidate as the live run. With a <see cref="RunPersistence"/> attached this is the
        /// durable promotion (R09): the candidate checkpoint is written, read back and verified first, a failure keeps
        /// the old run with the transition pending and retryable (<c>persistence.failed</c>, <c>return.blocked</c>), and a
        /// transition that is already durable is published without a second award. The value is the new run ID.
        /// Rejected with command.invalid when nothing is pending.
        /// </summary>
        public RunCommandResult<Guid> PromotePrestige()
        {
            PrestigeCandidate candidate = _session.PendingTransition;
            if (candidate == null) return Reject<Guid>(RunReasons.CommandInvalid);
            IPendingTransitionCoordinator coordinator = _session.TransitionCoordinator;
            if (coordinator != null) return coordinator.Promote();
            _session.PublishPendingTransition();
            return RunCommandResult.Success(candidate.Run.RunId);
        }

        /// <summary>
        /// Drops the pending candidate: the old run and the permanent state are exactly as they were (nothing was
        /// changed at confirmation) and commands resume. The value is whether a transition was pending. With a
        /// <see cref="RunPersistence"/> attached, a transition whose promotion is already durable cannot be cancelled
        /// (<c>transition.committed</c>): retry it instead.
        /// </summary>
        public RunCommandResult<bool> CancelPendingTransition()
        {
            IPendingTransitionCoordinator coordinator = _session.TransitionCoordinator;
            if (coordinator != null) return coordinator.Cancel();
            bool pending = _session.PendingTransition != null;
            _session.SetPendingTransition(null);
            return RunCommandResult.Success(pending);
        }
    }
}
