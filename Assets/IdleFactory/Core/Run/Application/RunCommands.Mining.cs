using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;

namespace IdleFactory.Run.Application
{
    /// <summary>The outcome of <see cref="RunCommands.Mine"/> (T358, D-O, D-P, MIN-10).</summary>
    public readonly struct MineResult
    {
        /// <summary>The mined resource.</summary>
        public string ResourceId { get; }

        /// <summary>The activations that were credited (their milli-yield joined the remainder).</summary>
        public long Counted { get; }

        /// <summary>The activations over the per-second rate cap. They are dropped silently and do not make the command fail.</summary>
        public long Ignored { get; }

        /// <summary>The activations that were within the cap but stopped by a full store or the numeric bound. They did not use the cap.</summary>
        public long Blocked { get; }

        /// <summary>The whole units of Ore that moved into stock (the remainder below one Ore stays in the transient counter).</summary>
        public long Units { get; }

        /// <summary>The stock of the mined resource after the command and its settle.</summary>
        public long StockAfter { get; }

        public MineResult(string resourceId, long counted, long ignored, long blocked, long units, long stockAfter)
        {
            ResourceId = resourceId;
            Counted = counted;
            Ignored = ignored;
            Blocked = blocked;
            Units = units;
            StockAfter = stockAfter;
        }
    }

    /// <summary>
    /// Manual mining of User Story 10 (F5-R19, F5-R20 MIN-1 ... MIN-10, contracts/run-application-f005.md, T358).
    ///
    /// Order of checks: lifecycle, the rate cap, the store (<c>storage.full</c>), the numeric bound (<c>value.limit</c>), one checked add,
    /// the settle. Content without <c>runStart.manualMining</c> (legacy) has no mining: <c>command.invalid</c>.
    ///
    /// Rate cap: a transient counter on <see cref="RunState"/> (<c>MiningTimelineSecond</c>, <c>MiningCountedInSecond</c>, and the milli
    /// remainder) keyed on the run-timeline second, so development time acceleration cannot raise a real-time rate. It is never persisted
    /// and is fresh on a new or loaded state (a load grants at most one extra second, MIN-7). Activations above the cap are ignored
    /// silently and are only reported.
    ///
    /// Yield: every counted activation adds the effective milli-yield <c>max(1, floor(baseYieldMilli x composedManualYieldPpm / 10^6))</c> to
    /// the remainder, and every full 1,000 moves one Ore into stock, in one checked transaction. <c>counted = min(n, cap remaining,
    /// activations whose whole Ore fit the store)</c>; activations stopped by the store or the numeric bound do not use the cap (MIN-10),
    /// and the command is rejected only when none can be counted. A rejection changes nothing, the counter included.
    ///
    /// Mined Ore enters stock only through <see cref="RunState.TryMine"/>, never through a machine completion, so it cannot advance a
    /// production objective (MIN-5). Nothing here is persisted and the simulator is unchanged (no mining offline). The aggregated event
    /// is raised through the guarded sink after the decision and never alters the result.
    /// </summary>
    public sealed partial class RunCommands
    {
        /// <summary>
        /// Mines by hand: <paramref name="activations"/> clicks, taps or hold ticks at the command instant. Rejected with
        /// lifecycle.blocked, command.invalid (no manual mining in the content, or fewer than 1 activation), resource.locked,
        /// storage.full (not even one activation fits the store) or value.limit (the yield, the stock or the settle that
        /// follows would pass 10^18). Otherwise a success, possibly with <see cref="MineResult.Counted"/> 0 when the rate cap is used up.
        /// </summary>
        public RunCommandResult<MineResult> Mine(long activations)
        {
            if (_session.IsLifecycleBlocked) return Reject<MineResult>(RunReasons.LifecycleBlocked);
            ManualMiningDef mining = _catalog.RunStart.ManualMining;
            if (mining == null || activations < 1) return Reject<MineResult>(RunReasons.CommandInvalid);
            if (!TryUnlockedResource(mining.ResourceId, out ResourceDef resource)) return Reject<MineResult>(RunReasons.ResourceLocked);
            RunState state = State;

            // Rate cap: the counter of another second counts as 0 (a backward timeline move also starts a fresh second).
            long second = _session.TimelineUtcMs / 1000;
            long used = state.MiningTimelineSecond == second ? state.MiningCountedInSecond : 0;
            long remaining = mining.MaxActivationsPerSecond > used ? mining.MaxActivationsPerSecond - used : 0;
            long eligible = activations < remaining ? activations : remaining;
            long ignored = activations - eligible;
            if (eligible == 0)
            {
                _session.Events.OnMining(new RunMiningEvent(resource.Id, 0, ignored, 0, 0));
                return RunCommandResult.Success(new MineResult(resource.Id, 0, ignored, 0, 0, state.Inventory.Units(resource.Id)));
            }

            if (!_modifiers.TryManualYieldPpm(state, Permanent, out long composedPpm) ||
                !RunModifiers.TryEffectiveMilliYield(mining.BaseYieldMilli, composedPpm, out long milliYield))
                return RejectMine(resource.Id, 0, ignored, eligible, RunReasons.ValueLimit);

            // The largest k <= eligible whose whole Ore fits the store (storage.full) and whose settle stays within 10^18 (value.limit).
            // eligible <= 20 (the cap), so the descending search is bounded and allocation-free.
            long remainder = state.MiningRemainderMilli;
            long free = state.Inventory.Free;
            long counted = 0;
            long milliTotal = 0;
            long ore = 0;
            string firstFailure = null;
            for (long k = eligible; k >= 1; k--)
            {
                string failure = null;
                long total = 0;
                long units = 0;
                if (!CheckedMath.TryMultiply(k, milliYield, out long added) || !CheckedMath.TryAdd(remainder, added, out total))
                {
                    failure = RunReasons.ValueLimit;
                }
                else
                {
                    units = total / 1000;
                    if (units > free) failure = RunReasons.StorageFull;
                    else if (!SettleFits(state.Cash, null, null, null, null, resource.Id, -units)) failure = RunReasons.ValueLimit;
                }

                if (k == 1) firstFailure = failure;
                if (failure != null) continue;
                counted = k;
                milliTotal = total;
                ore = units;
                break;
            }

            long blocked = eligible - counted;
            if (counted == 0) return RejectMine(resource.Id, 0, ignored, blocked, firstFailure ?? RunReasons.ValueLimit);

            _session.BeginCommand();
            // Defensive guard: unreachable, because the k search and the SettleFits pre-check above guarantee that TryMine and the settle succeed.
            RunStateResult mined = state.TryMine(resource.Id, ore, second, used + counted, milliTotal % 1000);
            if (mined != RunStateResult.Ok) return RejectMine(resource.Id, 0, ignored, eligible, RunReasons.From(mined));
            RunCommandResult<Unit> settled = _session.EndCommand(default(Unit), false);
            if (!settled.IsSuccess) return Reject<MineResult>(settled.ReasonCode);
            _session.Events.OnMining(new RunMiningEvent(resource.Id, counted, ignored, blocked, ore));
            return RunCommandResult.Success(new MineResult(resource.Id, counted, ignored, blocked, ore, state.Inventory.Units(resource.Id)));
        }

        private RunCommandResult<MineResult> RejectMine(string resourceId, long counted, long ignored, long blocked, string reasonCode)
        {
            _session.Events.OnMining(new RunMiningEvent(resourceId, counted, ignored, blocked, 0));
            return Reject<MineResult>(reasonCode);
        }
    }
}
