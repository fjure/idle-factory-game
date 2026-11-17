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
    /// The per-frame path allocates nothing: it reads the machine rows (<see cref="RunSession.CopyMachineStates(MachineStateView[])"/>), refreshes the
    /// model from a cached factory snapshot, and takes the new completions straight from the rows with
    /// <see cref="MachinePresentationModel.ApplyCompletions"/>. A new snapshot, which allocates (about 100 objects, 5 to 12 KB), is built only on a
    /// structure revision change (machines, levels, unlocks, effects, Overdrive: everything that can change a cycle time), a layout change, an offline
    /// return or a session replace. There is no periodic rebuild: the loop rate applies from the next frame 0 anyway (R12), and every change of a
    /// cycle time is a structure revision change.
    /// </para>
    /// <para>
    /// Session replace (P2-F12): a new run id (prestige, a new run, a restore of another run) resets the model's baseline and the pool and raises
    /// <see cref="SessionReplaced"/>, so the owner invalidates the attention model too. <see cref="ResetModels"/> is the explicit form (an offline
    /// return passes <c>offlineReturn</c>: the first refresh after it reports no delta, so nothing spawns for the offline cycles).
    /// </para>
    /// </summary>
    public sealed class WorldEffectsFeed
    {
        private readonly RunSession session;
        private readonly EffectPool pool;
        private readonly MachinePresentationModel model;
        private MachineStateView[] states = new MachineStateView[16];
        private RunFactorySnapshot factory;
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

        /// <summary>The number of full snapshot rebuilds so far (the allocating path), for tests and the profile.</summary>
        public int Rebuilds { get; private set; }

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

            // An offline return reaches ResetModels(true) and a new run id reaches it too; both are cheap and idempotent, so the overlap is harmless.
            var current = session.State.RunId;
            if (hasRun && current != runId)
            {
                runId = current;
                ResetModels(false);
                SessionReplaced?.Invoke();
            }

            runId = current;
            hasRun = true;

            var count = ReadStates();
            var rebuild = factory == null || suppressNext || session.StructureRevision != factory.StructureRevision || layout.LocationCount != lastLocationCount;
            if (rebuild)
            {
                factory = RunSnapshots.Factory(session);
                Rebuilds++;
                lastLocationCount = layout.LocationCount;
            }

            model.Refresh(layout, factory, states, count, null, suppressNext, rebuild);
            suppressNext = false;
            model.ApplyCompletions(states, count);
            pool.ConsumeCompletions(model);
        }

        private int ReadStates()
        {
            var count = session.CopyMachineStates(states);
            if (count > states.Length)
            {
                states = new MachineStateView[count];
                count = session.CopyMachineStates(states);
            }

            return count;
        }
    }
}
