using System;
using System.Collections.Generic;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;

namespace IdleFactory.Run.Application
{
    // -------------------------------------------------------------------------------------------------------
    // GoalsSnapshot (T332, FR-018, F5-R07, PF-8)
    // -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The goal line of the run: the next Factory Value goal and how far away it is. The goals are the content milestones
    /// (<c>floor(threshold x fractionPpm / 10^6)</c>, ascending, positive and below the threshold; any other value is skipped) followed
    /// by the prestige threshold itself. Content without milestones has the threshold as the only goal. Display only: nothing here grants anything.
    /// Immutable. <see cref="PercentFloored"/> is the value at the time the snapshot was built; between two goal crossings
    /// <see cref="PercentAt"/> gives the live percentage for the current Factory Value (hot read, no allocation).
    /// </summary>
    public sealed class GoalsSnapshot
    {
        private readonly long _lower;
        private readonly long _upper;

        /// <summary>False when the threshold cannot be derived (it would exceed 10^18); every other member is then 0 or false.</summary>
        public bool Available { get; }

        /// <summary>The next goal: the index of the next milestone, or <see cref="MilestoneCount"/> when the next goal is the threshold.</summary>
        public int Next { get; }

        /// <summary>True when the next goal is the prestige threshold (the last milestone is passed, or content defines none).</summary>
        public bool NextIsThreshold { get; }

        /// <summary>The Factory Value of the next goal.</summary>
        public long Target { get; }

        /// <summary>The next goal is reached: only possible for the threshold, which then stays as the goal.</summary>
        public bool Reached { get; }

        /// <summary>The Factory Value this snapshot was built from.</summary>
        public long FactoryValue { get; }

        /// <summary>floor(100 x min(Factory Value, Target) / Target), 0 to 100, as of <see cref="FactoryValue"/>.</summary>
        public int PercentFloored { get; }

        /// <summary>Factory Value is at or above the prestige threshold ("Factory Sale available").</summary>
        public bool FactorySaleAvailable { get; }

        /// <summary>Every content milestone is passed (vacuously true for content without milestones; see <see cref="MilestoneCount"/>).</summary>
        public bool AllReached { get; }

        /// <summary>The number of valid content milestones of the run (0 is the threshold-only goal line).</summary>
        public int MilestoneCount { get; }

        /// <summary>The number of milestones at or below the Factory Value.</summary>
        public int ReachedMilestones { get; }

        /// <summary>The prestige threshold of the run (the final goal).</summary>
        public long Threshold { get; }

        internal GoalsSnapshot(bool available, int next, bool nextIsThreshold, long target, bool reached, long factoryValue, int percent,
            bool factorySale, bool allReached, int milestoneCount, int reachedMilestones, long threshold, long lower, long upper)
        {
            Available = available;
            Next = next;
            NextIsThreshold = nextIsThreshold;
            Target = target;
            Reached = reached;
            FactoryValue = factoryValue;
            PercentFloored = percent;
            FactorySaleAvailable = factorySale;
            AllReached = allReached;
            MilestoneCount = milestoneCount;
            ReachedMilestones = reachedMilestones;
            Threshold = threshold;
            _lower = lower;
            _upper = upper;
        }

        /// <summary>floor(100 x min(factoryValue, Target) / Target) for the current goal; 0 when unavailable.</summary>
        public int PercentAt(long factoryValue) => Available ? GoalsMath.Percent(factoryValue, Target) : 0;

        /// <summary>Whether this snapshot still describes <paramref name="factoryValue"/>: no goal was crossed up or down since it was built.</summary>
        internal bool Covers(long factoryValue) => factoryValue >= _lower && factoryValue < _upper;
    }

    internal static class GoalsMath
    {
        public static int Percent(long factoryValue, long target)
        {
            if (target < 1 || factoryValue <= 0) return 0;
            if (factoryValue >= target) return 100;
            return MulDiv.MulDivFloor(factoryValue, 100, target, out long p) ? (int)p : 0;
        }

        /// <summary>The milestone value <c>floor(threshold x fractionPpm / 10^6)</c>, or 0 when it is not a valid goal after <paramref name="previous"/>.</summary>
        public static long Milestone(long threshold, long fractionPpm, long previous)
        {
            if (!MulDiv.MulDivFloor(threshold, fractionPpm, 1_000_000, out long value)) return 0;
            return value > previous && value < threshold ? value : 0;
        }

        /// <summary>Builds the snapshot of a Factory Value against a threshold and the content milestones. Pure.</summary>
        public static GoalsSnapshot Build(PrestigeDef prestige, IReadOnlyList<MilestoneDef> milestones, long prestigeCount, long factoryValue)
        {
            if (factoryValue < 0) factoryValue = 0;
            if (!PrestigeThreshold.TryForCount(prestige, prestigeCount, out long threshold))
                return new GoalsSnapshot(false, 0, false, 0, false, factoryValue, 0, false, false, 0, 0, 0, 0, long.MaxValue);

            // One pass: count the valid milestones, how many are at or below the value, and the first one above it.
            int count = 0, reachedCount = 0, nextIndex = -1;
            long previous = 0, lower = 0, next = 0;
            int n = milestones == null ? 0 : milestones.Count;
            for (int i = 0; i < n; i++)
            {
                long value = Milestone(threshold, milestones[i].FractionPpm, previous);
                if (value == 0) continue;
                if (factoryValue >= value)
                {
                    reachedCount++;
                    lower = value;
                }
                else if (nextIndex < 0)
                {
                    nextIndex = count;
                    next = value;
                }

                previous = value;
                count++;
            }

            bool sale = factoryValue >= threshold;
            if (nextIndex >= 0)
            {
                return new GoalsSnapshot(true, nextIndex, false, next, false, factoryValue, Percent(factoryValue, next), false, false, count,
                    reachedCount, threshold, lower, next);
            }

            // Every milestone is passed (or there are none): the threshold is the goal.
            return new GoalsSnapshot(true, count, true, threshold, sale, factoryValue, Percent(factoryValue, threshold), sale, true, count,
                reachedCount, threshold, sale ? threshold : lower, sale ? long.MaxValue : threshold);
        }
    }

    public static partial class RunSnapshots
    {
        /// <summary>The goal line for the session's current Factory Value. Read-only; a fresh value on every call (see <see cref="GoalsTracker"/> for the cached form).</summary>
        public static GoalsSnapshot Goals(RunSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            return GoalsMath.Build(session.Catalog.Prestige, session.Catalog.Milestones, session.Permanent == null ? 0 : session.Permanent.PrestigeCount,
                FactoryValueOf(session));
        }

        // An overflowing value is above every goal: it counts as the exact bound.
        internal static long FactoryValueOf(RunSession session) =>
            FactoryValuation.TryCompute(session.Catalog, session.State, session.Permanent, out FactoryValueBreakdown value) ? value.Total : ExactBound.Value;
    }

    /// <summary>
    /// The cached goal line of one session (PF-8): the snapshot is rebuilt only when a goal is crossed (the Factory Value leaves the range
    /// between the previous and the next goal), or when the run, the prestige count or the catalog changes, never on every Factory Value
    /// change. Reads with unchanged session revisions return the same instance and compute nothing. Not thread-safe; owned by the presenter.
    /// A freshly created tracker (a load, a new run) seeds from the current value and raises nothing, so already-passed goals never re-notify (F5-R07).
    /// Crossing the threshold in the first run (prestige count 0) raises one <see cref="RunMilestoneKind.Run2Eligible"/> event through
    /// <see cref="RunSession.Events"/> (the guarded session-log port); the event cannot alter simulation, save or timing.
    /// </summary>
    public sealed class GoalsTracker
    {
        private GoalsSnapshot _cached;
        private RunCatalog _catalog;
        private Guid _runId;
        private long _prestigeCount = -1;
        private long _structureRevision = -1;
        private long _quantityRevision = -1;
        private bool _eventRaised;

        /// <summary>The number of times the snapshot was rebuilt (a crossing, a new run or catalog); for tests and diagnostics.</summary>
        public int Rebuilds { get; private set; }

        /// <summary>The cached snapshot, rebuilt only on a goal crossing. <paramref name="playSeconds"/> is the run's play time for the milestone event.</summary>
        public GoalsSnapshot Current(RunSession session, long playSeconds)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            long prestigeCount = session.Permanent == null ? 0 : session.Permanent.PrestigeCount;
            bool sameRun = _cached != null && ReferenceEquals(_catalog, session.Catalog) && _runId == session.State.RunId && _prestigeCount == prestigeCount;
            if (sameRun && _structureRevision == session.StructureRevision && _quantityRevision == session.QuantityRevision) return _cached;

            long factoryValue = RunSnapshots.FactoryValueOf(session);
            _structureRevision = session.StructureRevision;
            _quantityRevision = session.QuantityRevision;
            if (sameRun && _cached.Covers(factoryValue)) return _cached;

            bool wasAvailable = sameRun && _cached.FactorySaleAvailable;
            GoalsSnapshot rebuilt = GoalsMath.Build(session.Catalog.Prestige, session.Catalog.Milestones, prestigeCount, factoryValue);
            Rebuilds++;
            if (!sameRun)
            {
                _catalog = session.Catalog;
                _runId = session.State.RunId;
                _prestigeCount = prestigeCount;
                _eventRaised = rebuilt.FactorySaleAvailable; // a seed never notifies
            }
            else if (rebuilt.FactorySaleAvailable && !wasAvailable && !_eventRaised && prestigeCount == 0)
            {
                _eventRaised = true;
                session.Events.OnMilestone(new RunMilestoneEvent(RunMilestoneKind.Run2Eligible, playSeconds));
            }

            _cached = rebuilt;
            return _cached;
        }
    }
}
