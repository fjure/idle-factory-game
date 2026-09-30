using System;
using System.Collections.Generic;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;

namespace IdleFactory.Run.Application
{
    /// <summary>Result of <see cref="RunCommands.ActivateItem"/>: the effect that now runs and the Items left.</summary>
    public readonly struct ItemActivationResult
    {
        public string ItemId { get; }
        public string EffectKind { get; }

        /// <summary>The chosen machine type ID, or <see cref="RunCommands.FactoryTargetKey"/> for a factory Item.</summary>
        public string TargetKey { get; }
        public long StartUtcMs { get; }

        /// <summary>The exclusive end of the effect, <c>StartUtcMs + Item.DurationMs</c>.</summary>
        public long EndUtcMs { get; }

        /// <summary>The Items of this kind still owned after the activation.</summary>
        public long RemainingCount { get; }

        public ItemActivationResult(string itemId, string effectKind, string targetKey, long startUtcMs, long endUtcMs, long remainingCount)
        {
            ItemId = itemId;
            EffectKind = effectKind;
            TargetKey = targetKey;
            StartUtcMs = startUtcMs;
            EndUtcMs = endUtcMs;
            RemainingCount = remainingCount;
        }
    }

    /// <summary>
    /// The Item command of User Story 3 (contracts/run-application.md): ActivateItem. It follows the rules of
    /// <see cref="RunCommands"/>: lifecycle first, validation, a pre-check that nothing can overflow after the change,
    /// <see cref="RunSession.BeginCommand"/>, one atomic mutation and <see cref="RunSession.EndCommand{T}"/>.
    ///
    /// Interpretations where the contract is silent (each reported to the coordinator): (1) the conflict key of a
    /// <c>factory</c> Item is <c>(effect.kind, "factory")</c>, the key the simulator fixtures already use; (2) a
    /// <c>factory</c> Item given a target is <c>item.target.invalid</c> (it has no target); (3) an unknown Item ID has
    /// a count of 0 and is <c>item.none</c>; (4) an effect whose end is at or before the timeline (left over after a
    /// re-bound clock, before the next settle) is over, so it neither conflicts nor is touched by a rejected command; the
    /// applied command replaces it.
    /// </summary>
    public sealed partial class RunCommands
    {
        /// <summary>The target key of the effect of a <c>factory</c> Item (its conflict key is <c>(effectKind, "factory")</c>).</summary>
        public const string FactoryTargetKey = "factory";

        /// <summary>
        /// Activates one owned Item. <paramref name="target"/> is null for a <c>factory</c> Item and, for a
        /// <c>machineType</c> Item, a machine type ID from its <c>allowedMachineIds</c> (never an instance ID; OD-1).
        /// Rejected with item.none (no Item owned), item.target.invalid, effect.conflict (an effect with the same
        /// <c>(effectKind, targetKey)</c> is running; the Item is kept) or value.limit. Consumes exactly one Item and
        /// adds the effect over <c>[t, t + duration)</c> in real UTC; the settle rescales every machine's progress
        /// to its new cycle (R03).
        /// </summary>
        public RunCommandResult<ItemActivationResult> ActivateItem(string itemId, string target)
        {
            if (_session.IsLifecycleBlocked) return Reject<ItemActivationResult>(RunReasons.LifecycleBlocked);
            RunState state = State;
            if (Permanent == null) return Reject<ItemActivationResult>(RunReasons.CommandInvalid);
            if (itemId == null || !_catalog.Items.TryGetValue(itemId, out ItemDef item) || Permanent.ItemCount(itemId) < 1)
                return Reject<ItemActivationResult>(RunReasons.ItemNone);

            string targetKey;
            if (item.TargetKind == "factory")
            {
                if (!string.IsNullOrEmpty(target)) return Reject<ItemActivationResult>(RunReasons.ItemTargetInvalid);
                targetKey = FactoryTargetKey;
            }
            else
            {
                if (target == null || !ContainsOrdinal(item.AllowedMachineIds, target)) return Reject<ItemActivationResult>(RunReasons.ItemTargetInvalid);
                targetKey = target;
            }

            long now = _session.TimelineUtcMs;
            var key = new ConflictKey(item.Effect.Kind, targetKey);
            ActiveEffect staleEffect = null;
            IReadOnlyList<ActiveEffect> running = state.Effects.Effects;
            for (int i = 0; i < running.Count; i++)
            {
                if (!running[i].ConflictKey.Equals(key)) continue;
                if (running[i].EndUtcMs > now) return Reject<ItemActivationResult>(RunReasons.EffectConflict);
                staleEffect = running[i];
            }
            if (!CheckedMath.TryAdd(now, item.DurationMs, out long end) || !ExactBound.Contains(end))
                return Reject<ItemActivationResult>(RunReasons.ValueLimit);
            var effect = new ActiveEffect(itemId, key, now, end);

            // Trial: the composed speeds, cycles and output factors after the change must be computable and the
            // settle that follows must not overflow Cash. The trial is undone before the real transaction.
            // An over effect with the same key is swapped out for the trial and put back in the finally, so a
            // rejection leaves the effect list exactly as it was.
            bool fits;
            RunStateResult trial;
            bool added = false;
            try
            {
                if (staleEffect != null) state.Effects.TryRemove(key);
                trial = state.TryAddEffect(effect);
                added = trial == RunStateResult.Ok;
                fits = added && ChangeFits(null);
            }
            finally
            {
                if (added) state.Effects.TryRemove(key);
                if (staleEffect != null) state.Effects.TryAdd(staleEffect);
            }
            if (trial != RunStateResult.Ok) return Reject<ItemActivationResult>(RunReasons.From(trial));
            if (!fits) return Reject<ItemActivationResult>(RunReasons.ValueLimit);

            _session.BeginCommand();
            RunStateResult consumed = Permanent.TryConsumeItem(itemId);
            if (consumed != RunStateResult.Ok) return Reject<ItemActivationResult>(RunReasons.From(consumed));
            if (staleEffect != null) state.Effects.TryRemove(key); // the command applies, so the over effect is dropped now
            RunStateResult applied = state.TryAddEffect(effect);
            if (applied != RunStateResult.Ok)
            {
                Permanent.TryAddItems(itemId, 1);
                if (staleEffect != null) state.Effects.TryAdd(staleEffect);
                return Reject<ItemActivationResult>(RunReasons.From(applied));
            }
            return _session.EndCommand(new ItemActivationResult(itemId, item.Effect.Kind, targetKey, now, end, Permanent.ItemCount(itemId)), true);
        }

        private static bool ContainsOrdinal(IReadOnlyList<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++)
                if (string.Equals(ids[i], id, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// The pre-check shared by the modifier commands (Item, Research, Industry, Overdrive): with the change
        /// already applied to the state (or, for Overdrive, announced by <paramref name="overdriveAfter"/>), every
        /// machine must derive its speed, batch, cycle and output factor, its progress must rescale to the new
        /// cycle, and the settle after the command must not overflow Cash (<see cref="SettleFits"/>). A false result
        /// is <c>value.limit</c>, returned before the command mutates anything.
        /// </summary>
        private bool ChangeFits(bool? overdriveAfter)
        {
            RunState state = State;
            bool overdrive = overdriveAfter ?? OverdriveActive;
            IReadOnlyList<MachineInstance> machines = state.Machines;
            for (int i = 0; i < machines.Count; i++)
            {
                MachineInstance m = machines[i];
                if (!RunDerivation.TryDerive(_catalog, _modifiers, state, Permanent, m, overdrive, out RunDerivation.Derived d) ||
                    !SpeedComposer.TryRescaleProgress(m.ProgressTicks, m.CycleDurationTicks, d.Cycle, out _))
                    return false;
            }
            return SettleFits(state.Cash, null, null, null, null, null, 0, overdrive);
        }
    }
}
