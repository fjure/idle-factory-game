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
    /// <see cref="MachinePresentationModel.ApplyCompletions"/>.
    /// </para>
    /// <para>
    /// Rebuild cadence. A full snapshot (<c>RunSnapshots.Factory</c>, about 100 objects and 5 to 12 KB, 174 allocations measured) is built
    /// <b>immediately</b> only when the machines themselves change: the row count, a row's slot or machine type, or a completed-cycles sum that went
    /// down (a removed or replaced machine); and on the first frame, a layout change, an offline return and a session replace. Any other
    /// <see cref="RunSession.StructureRevision"/> change (a level, an unlock, an effect, Overdrive, and above all contract progress: the contract
    /// board bumps its version on every produced unit, so the revision moves on most frames while a production contract is active) is picked up
    /// <b>at most once per <see cref="MinStructureRebuildMilliseconds"/></b> with the latest revision. That is safe because the snapshot only feeds
    /// the loop rate and the visual state, and a pace change applies from the next frame 0 anyway (R12). So a factory rebuilds at most once per second
    /// in the steady state, however fast its contracts progress.
    /// </para>
    /// <para>
    /// Session replace (P2-F12): a new run id (prestige, a new run, a restore of another run) resets the model's baseline and the pool and raises
    /// <see cref="SessionReplaced"/>, so the owner invalidates the attention model too. <see cref="ResetModels"/> is the explicit form (an offline
    /// return passes <c>offlineReturn</c>: the first refresh after it reports no delta, so nothing spawns for the offline cycles).
    /// </para>
    /// </summary>
    public sealed class WorldEffectsFeed
    {
        /// <summary>The least time between two rebuilds that a structure revision change alone causes, in presentation milliseconds.</summary>
        public const long MinStructureRebuildMilliseconds = 1000;

        private readonly RunSession session;
        private readonly EffectPool pool;
        private readonly MachinePresentationModel model;
        private MachineStateView[] states = new MachineStateView[16];
        private RunFactorySnapshot factory;
        private int lastLocationCount = -1;
        private long lastRebuildMs = long.MinValue / 2;
        private long identitySignature;
        private long lastCompleted;
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

        /// <summary>
        /// The machine rows read this frame (<see cref="RunSession.CopyMachineStates(MachineStateView[])"/>, always current, never a snapshot): the link layer
        /// reads the live Running state from them, so a pip stops in the frame after its machine stops. Valid after <see cref="Update"/>; the array is reused.
        /// </summary>
        public MachineStateView[] LiveStates => states;

        /// <summary>The number of valid rows of <see cref="LiveStates"/> this frame.</summary>
        public int LiveStateCount { get; private set; }

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

            var count = ReadStates(out var signature, out var completed);
            LiveStateCount = count;
            var machinesChanged = signature != identitySignature || completed < lastCompleted;
            identitySignature = signature;
            lastCompleted = completed;
            var structureChanged = factory != null && session.StructureRevision != factory.StructureRevision;
            var rebuild = factory == null || suppressNext || machinesChanged || layout.LocationCount != lastLocationCount ||
                (structureChanged && presentationMilliseconds - lastRebuildMs >= MinStructureRebuildMilliseconds);
            if (rebuild)
            {
                factory = RunSnapshots.Factory(session);
                Rebuilds++;
                lastRebuildMs = presentationMilliseconds;
                lastLocationCount = layout.LocationCount;
            }

            model.Refresh(layout, factory, states, count, null, suppressNext, rebuild);
            suppressNext = false;
            model.ApplyCompletions(states, count);
            pool.ConsumeCompletions(model);
        }

        // The machine identity of the rows: how many and, per row, its slot and machine type. A level or a counter is not identity.
        private int ReadStates(out long signature, out long completed)
        {
            var count = session.CopyMachineStates(states);
            if (count > states.Length)
            {
                states = new MachineStateView[count];
                count = session.CopyMachineStates(states);
            }

            long hash = count;
            long sum = 0;
            for (var i = 0; i < count; i++)
            {
                unchecked
                {
                    hash = hash * 1_000_003L + states[i].SlotIndex * 131L + states[i].MachineTypeIndex + 1;
                }

                sum += states[i].CompletedCycles;
            }

            signature = hash;
            completed = sum;
            return count;
        }
    }
}
