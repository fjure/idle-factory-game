using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// Feeds the world effect pool from the run (T529, FR-029, research R08 and R12): it owns the world's <see cref="MachinePresentationModel"/>,
    /// refreshes it in every frame (P2-F10), and hands its completion deltas to the <see cref="EffectPool"/>. It reads the session and changes
    /// nothing in it.
    /// <para>
    /// The per-frame path reads the allocation-free machine rows (<see cref="RunSession.CopyMachineStates(MachineStateView[])"/>) and refreshes the
    /// model from a cached factory snapshot. A new snapshot, which allocates, is built only when the structure revision, the layout or the sum of the
    /// completed-cycle counters moved, and at most every <see cref="MinRebuildMilliseconds"/> for the counters; deltas add up in the model's baseline,
    /// so a throttled frame loses no completion (the display value is capped anyway).
    /// </para>
    /// <para>
    /// Session replace (P2-F12): a new run id (prestige, a new run, a restore of another run) resets the model's baseline and the pool and raises
    /// <see cref="SessionReplaced"/>, so the owner invalidates the attention model too. <see cref="ResetModels"/> is the explicit form (an offline
    /// return passes <c>offlineReturn</c>: the first refresh after it reports no delta, so nothing spawns for the offline cycles).
    /// </para>
    /// </summary>
    public sealed class WorldEffectsFeed
    {
        /// <summary>The most often a snapshot is rebuilt for a moved completed-cycles counter, in presentation milliseconds.</summary>
        public const long MinRebuildMilliseconds = 100;

        private readonly RunSession session;
        private readonly EffectPool pool;
        private readonly MachinePresentationModel model;
        private MachineStateView[] states = new MachineStateView[16];
        private RunFactorySnapshot factory;
        private long lastBuildMs = long.MinValue / 2;
        private long lastCompleted = -1;
        private int lastLocationCount = -1;
        private Guid runId;
        private bool hasRun;
        private bool suppressNext;

        public WorldEffectsFeed(RunSession session, EffectPool pool, IReadOnlyDictionary<string, int> familyFpsCaps = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.pool = pool ?? throw new ArgumentNullException(nameof(pool));
            model = new MachinePresentationModel(familyFpsCaps);
        }

        /// <summary>The world's model (the card owns another one for its own location).</summary>
        public MachinePresentationModel Model => model;

        public EffectPool Pool => pool;

        /// <summary>Raised once when the run id changed (prestige, a new run, a restore): owners of other derived models invalidate them.</summary>
        public event Action SessionReplaced;

        /// <summary>
        /// A session was replaced or the player returned from offline: the baselines are forgotten and the pool is emptied. With
        /// <paramref name="offlineReturn"/> the next refresh also takes the new baseline without a delta.
        /// </summary>
        public void ResetModels(bool offlineReturn = false)
        {
            model.ResetBaseline();
            pool.Reset();
            factory = offlineReturn ? factory : null;
            lastCompleted = -1;
            suppressNext = offlineReturn;
        }

        /// <summary>One frame: advances the pool, refreshes the model and spawns the pops of the new completions. <paramref name="presentationMilliseconds"/> is the shared clock.</summary>
        public void Update(FactoryWorldLayout layout, long presentationMilliseconds, bool reducedMotion)
        {
            if (layout == null)
            {
                return;
            }

            pool.SetReducedMotion(reducedMotion);
            pool.EnsureLocations(layout.LocationCount);
            pool.Advance(presentationMilliseconds);

            var current = session.State.RunId;
            if (hasRun && current != runId)
            {
                runId = current;
                ResetModels(false);
                SessionReplaced?.Invoke();
            }

            runId = current;
            hasRun = true;

            var completed = ReadStates(out var count);
            var rebuild = factory == null || suppressNext || session.StructureRevision != factory.StructureRevision || layout.LocationCount != lastLocationCount;
            if (!rebuild && completed != lastCompleted && presentationMilliseconds - lastBuildMs >= MinRebuildMilliseconds)
            {
                rebuild = true;
            }

            if (rebuild)
            {
                factory = RunSnapshots.Factory(session);
                count = session.CopyMachineStates(states);
                if (count > states.Length)
                {
                    states = new MachineStateView[count];
                    count = session.CopyMachineStates(states);
                }

                lastBuildMs = presentationMilliseconds;
                lastCompleted = completed;
                lastLocationCount = layout.LocationCount;
            }

            model.Refresh(layout, factory, states, count, null, suppressNext, rebuild);
            suppressNext = false;
            pool.ConsumeCompletions(model);
        }

        private long ReadStates(out int count)
        {
            count = session.CopyMachineStates(states);
            if (count > states.Length)
            {
                states = new MachineStateView[count];
                count = session.CopyMachineStates(states);
            }

            long total = 0;
            for (var i = 0; i < count; i++)
            {
                total += states[i].CompletedCycles;
            }

            return total;
        }
    }
}
