using System;
using System.Collections.Generic;
using IdleFactory.Core.Application;
using IdleFactory.Core.Numerics;
using IdleFactory.Domain;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Simulation;

namespace IdleFactory.Run.Application
{
    /// <summary>
    /// The stable, static failure codes of the V1 import (contracts/save-v2-and-v1-import.md, V1 import procedure; R10).
    /// UI copy keys off these strings, so they never change, and none of them carries text taken from a save file (SEC-5).
    /// </summary>
    public static class V1ImportCodes
    {
        /// <summary>The V1 active checkpoint is absent-but-damaged, unreadable, unsupported or fails the unchanged Save V1 mapper.</summary>
        public const string Invalid = "import.v1.invalid";

        /// <summary>The return instant is missing or unrepresentable (retry allowed).</summary>
        public const string Clock = "import.v1.clock";

        /// <summary>An ID that the import does not map (a building, recipe or resource other than the development ones).</summary>
        public const string IdUnsupported = "import.id.unsupported";

        /// <summary>A checked sum or product failed, for example the stock sum or the progress rescale.</summary>
        public const string Arithmetic = "import.arithmetic";

        /// <summary>More machines than the level-1 slot count.</summary>
        public const string SlotsExceeded = "import.slots.exceeded";

        /// <summary>Total stock above the level-1 storage capacity.</summary>
        public const string CapacityExceeded = "import.capacity.exceeded";

        /// <summary>The candidate could not be promoted (written, read back and verified).</summary>
        public const string Persistence = "import.persistence";

        private static readonly string[] AllCodes =
        {
            Invalid, Clock, IdUnsupported, Arithmetic, SlotsExceeded, CapacityExceeded, Persistence,
        };

        /// <summary>The seven import codes of the contract.</summary>
        public static IReadOnlyList<string> All => AllCodes;
    }

    /// <summary>What the cold start found when it asked the legacy source for the V1 active checkpoint.</summary>
    internal enum V1SourceState
    {
        /// <summary>There is no V1 save: a fresh run starts.</summary>
        Absent = 0,

        /// <summary>A valid V1 active checkpoint is available.</summary>
        Available = 1,

        /// <summary>A V1 save exists but cannot be used (damaged, unsupported version or unreadable): the import is blocked.</summary>
        Unusable = 2,
    }

    /// <summary>The outcome of reading the V1 active checkpoint through <see cref="ILegacyCheckpointSource"/>.</summary>
    internal sealed class V1SourceRead
    {
        private V1SourceRead(V1SourceState state, Checkpoint checkpoint)
        {
            State = state;
            Checkpoint = checkpoint;
        }

        public V1SourceState State { get; }

        /// <summary>The V1 checkpoint; non-null only when <see cref="State"/> is <see cref="V1SourceState.Available"/>.</summary>
        public Checkpoint Checkpoint { get; }

        internal static V1SourceRead Absent() => new V1SourceRead(V1SourceState.Absent, null);
        internal static V1SourceRead Unusable() => new V1SourceRead(V1SourceState.Unusable, null);
        internal static V1SourceRead Available(Checkpoint checkpoint) => new V1SourceRead(V1SourceState.Available, checkpoint);
    }

    /// <summary>A built import candidate, or the static code of the first rule it failed.</summary>
    internal sealed class V1ImportResult
    {
        private V1ImportResult(RunCheckpoint candidate, string code)
        {
            Candidate = candidate;
            Code = code;
        }

        public bool IsSuccess => Candidate != null;

        /// <summary>The complete, settled Save V2 candidate; null on failure.</summary>
        public RunCheckpoint Candidate { get; }

        /// <summary>One of <see cref="V1ImportCodes"/>; null on success.</summary>
        public string Code { get; }

        internal static V1ImportResult Success(RunCheckpoint candidate) => new V1ImportResult(candidate, null);
        internal static V1ImportResult Fail(string code) => new V1ImportResult(null, code);
    }

    /// <summary>
    /// The V1 import (FR-028, SC-005, R10): turns the Save V1 active checkpoint into a complete Save V2 candidate. It is a
    /// pure function of the V1 checkpoint, the captured return instant <c>r</c> and the ids it is given, and it never
    /// touches a file: V1 is read only through <see cref="ILegacyCheckpointSource"/>, which holds no write-capable
    /// dependency, and the promotion is <see cref="RunPersistence"/>'s (the same SafePromote path as every other durable
    /// transition, so there is no second promotion path).
    ///
    /// Order of the checks, where the contract table leaves room: (1) the machine count against the level-1 slot count and
    /// (2) every ID are validated on the V1 checkpoint BEFORE any simulation, so an oversized or unsupported save is
    /// rejected without running the legacy simulator over up to 100,000 buildings (<c>06-w001-scale</c> therefore fails
    /// with <c>import.slots.exceeded</c> and not with an ID code, QA-9); (3) the legacy reconcile runs at <c>r</c>, in
    /// memory; (4) the stock sums are checked; (5) the total is checked against the level-1 capacity. Then the run is
    /// mapped (Ore = sum of <c>dev.input</c>, Bar = sum of <c>dev.output</c>; each <c>dev.machine.basic</c> a level-1
    /// Smelter with <c>investedCash</c> 0 in slots and priority ordered by (anchorY, anchorX, canonical lowercase
    /// instance-ID text); progress <c>MulDivFloor(old, smelterCycle, v1RecipeDuration)</c>), every other field takes its
    /// fresh-run default (OD-5: Cash <see cref="RunStartDef.StartingCash"/>, factory and storage level 1, the initial
    /// unlocks, then Ore, Bar and the Smelter), and the candidate carries the <c>import</c> block with the V1 checkpoint ID
    /// as canonical lowercase UUID text, schema version 1 and the V1 generation.
    ///
    /// Settle owner: the import settles ONCE itself, before publication. <see cref="Build"/> advances the freshly built
    /// state by zero ticks at <c>r</c> (the unlock pass, auto-sales and completions at the first instant), so the
    /// promoted bytes equal what is published (data-model: first settle). The settle that
    /// <see cref="RunPersistence"/> performs after it installs a fresh read of the promoted generation is then a no-op on
    /// a fixed point, not a second effect.
    ///
    /// The departure is <c>r</c>, captured once per attempt by the caller. Whoever retries in the same session
    /// re-promotes the SAME candidate (built once); a restart that finds the promoted V2 selects it and never imports
    /// again; a restart that finds only an unpromoted pending file builds a new candidate with a newly captured <c>r</c>.
    /// </summary>
    internal sealed class V1Import
    {
        public const string V1InputResourceId = "dev.input";
        public const string V1OutputResourceId = "dev.output";
        public const string V1MachineId = "dev.machine.basic";
        public const string V1RecipeId = "dev.recipe.basic";
        public const string OreResourceId = "iron.ore";
        public const string BarResourceId = "iron.bar";
        public const string SmelterMachineId = "machine.smelter";

        private readonly RunCatalog catalog;
        private readonly ILegacyCheckpointSource source;

        public V1Import(RunCatalog catalog, ILegacyCheckpointSource source)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>
        /// Reads the V1 active checkpoint through the legacy source. An absent V1 save means a fresh run; a save that is
        /// damaged, of an unsupported version or unreadable blocks the import (never a silent fresh run, so an
        /// inaccessible save cannot be skipped).
        /// </summary>
        public V1SourceRead ReadSource()
        {
            bool read;
            Checkpoint checkpoint;
            CommandOutcome outcome;
            try
            {
                read = source.TryReadActive(out checkpoint, out outcome);
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                return V1SourceRead.Unusable();
            }

            if (read && checkpoint != null) return V1SourceRead.Available(checkpoint);
            return !read && outcome == CommandOutcome.NoCheckpoint ? V1SourceRead.Absent() : V1SourceRead.Unusable();
        }

        /// <summary>
        /// Builds the settled Save V2 candidate. <paramref name="returnUtcMs"/> is the return instant captured once by the
        /// caller, or null when the clock failed (<c>import.v1.clock</c>). <paramref name="candidateId"/> must differ from
        /// the V1 checkpoint ID, <paramref name="generation"/> is the V2 generation of the candidate, and
        /// <paramref name="runId"/> is the identity of the imported run.
        /// </summary>
        public V1ImportResult Build(Checkpoint v1, long? returnUtcMs, CheckpointId candidateId, long generation, Guid runId)
        {
            if (v1 == null) throw new ArgumentNullException(nameof(v1));
            try
            {
                return BuildCore(v1, returnUtcMs, candidateId, generation, runId);
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                // A defensive net: every expected rule failure returns its own code above. Nothing here carries file text.
                return V1ImportResult.Fail(V1ImportCodes.Invalid);
            }
        }

        private V1ImportResult BuildCore(Checkpoint v1, long? returnUtcMs, CheckpointId candidateId, long generation, Guid runId)
        {
            FactoryState factory = v1.CreateFactoryCopy();

            // 1 and 2: validate on the source, before any simulation.
            if (factory.Buildings.Count > catalog.FactoryLevels[0].SlotCount) return V1ImportResult.Fail(V1ImportCodes.SlotsExceeded);
            if (!catalog.Machines.TryGetValue(SmelterMachineId, out MachineDef smelter) ||
                !catalog.Resources.ContainsKey(OreResourceId) || !catalog.Resources.ContainsKey(BarResourceId) ||
                !catalog.Recipes.TryGetValue(smelter.RecipeId, out MachineRecipe smelterRecipe))
                return V1ImportResult.Fail(V1ImportCodes.IdUnsupported);
            foreach (BuildingInstanceState building in factory.Buildings.Values)
            {
                if (!IsSupported(building)) return V1ImportResult.Fail(V1ImportCodes.IdUnsupported);
            }

            // 3: the legacy reconcile at the captured return instant, in memory only.
            if (!returnUtcMs.HasValue || !ExactBound.Contains(returnUtcMs.Value)) return V1ImportResult.Fail(V1ImportCodes.Clock);
            long r = returnUtcMs.Value;
            if (candidateId == v1.Id) return V1ImportResult.Fail(V1ImportCodes.Persistence);
            OfflineReconciliationResult reconciled = new OfflineReconciler()
                .Reconcile(v1, DateTimeOffset.FromUnixTimeMilliseconds(r), candidateId);
            switch (reconciled.Status)
            {
                case OfflineReconciliationStatus.CandidateReady:
                    break;
                case OfflineReconciliationStatus.MissingReturnTime:
                case OfflineReconciliationStatus.InvalidReturnTime:
                    return V1ImportResult.Fail(V1ImportCodes.Clock);
                case OfflineReconciliationStatus.SimulationFailed
                    when reconciled.SimulationFailureStatus == ProductionAdvanceStatus.ArithmeticOverflow:
                    return V1ImportResult.Fail(V1ImportCodes.Arithmetic);
                default:
                    return V1ImportResult.Fail(V1ImportCodes.Invalid);
            }

            FactoryState offline = reconciled.CandidateCheckpoint.CreateFactoryCopy();
            if (offline.Buildings.Count != factory.Buildings.Count) return V1ImportResult.Fail(V1ImportCodes.Invalid);

            // Defensive: the legacy reconciler never changes a building's IDs, so this cannot fail today.
            foreach (BuildingInstanceState building in offline.Buildings.Values)
            {
                if (!IsSupported(building)) return V1ImportResult.Fail(V1ImportCodes.IdUnsupported);
            }

            // 4: the stock sums over every input and output buffer, per mapped resource.
            long ore = 0;
            long bar = 0;
            foreach (BuildingInstanceState building in offline.Buildings.Values)
            {
                if (!TryAddBuffer(building.InputBuffer, ref ore, ref bar) || !TryAddBuffer(building.OutputBuffer, ref ore, ref bar))
                    return V1ImportResult.Fail(V1ImportCodes.Arithmetic);
            }

            // Fresh-run defaults (OD-5).
            var permanent = new PermanentState(catalog);
            RunState run = NextRunStart.CreateRun(catalog, permanent, runId, 1, null,
                new[] { OreResourceId, BarResourceId, SmelterMachineId });

            // 5: total stock against the level-1 capacity.
            if (!CheckedMath.TryAdd(ore, bar, out long total)) return V1ImportResult.Fail(V1ImportCodes.Arithmetic);
            if (total > run.Inventory.Capacity) return V1ImportResult.Fail(V1ImportCodes.CapacityExceeded);
            var amounts = new List<ResourceAmount>();
            if (ore > 0) amounts.Add(new ResourceAmount(OreResourceId, ore));
            if (bar > 0) amounts.Add(new ResourceAmount(BarResourceId, bar));
            RunStateResult imported = run.Inventory.TryImport(amounts);
            if (imported == RunStateResult.ValueLimit) return V1ImportResult.Fail(V1ImportCodes.Arithmetic);
            if (imported == RunStateResult.CapacityExceeded) return V1ImportResult.Fail(V1ImportCodes.CapacityExceeded);
            if (imported != RunStateResult.Ok) return V1ImportResult.Fail(V1ImportCodes.Invalid);

            // Machines: stable slots and priority by (anchorY, anchorX, canonical instance-ID text).
            var ordered = new List<BuildingInstanceState>(offline.Buildings.Values);
            ordered.Sort(CompareBuildings);
            long cycle = smelterRecipe.DurationTicks;
            for (int slot = 0; slot < ordered.Count; slot++)
            {
                BuildingInstanceState building = ordered[slot];
                long v1Duration = building.Production.Definition.DurationTicks;
                if (!MulDiv.MulDivFloor(building.Production.ProgressTicks, cycle, v1Duration, out long progress))
                    return V1ImportResult.Fail(V1ImportCodes.Arithmetic);
                var machine = new MachineInstance(building.Id.Uuid, smelter, slot, 1, building.IsActive, progress, cycle, 0);
                RunStateResult added = run.TryAddMachine(machine);
                if (added == RunStateResult.SlotUnavailable) return V1ImportResult.Fail(V1ImportCodes.SlotsExceeded);
                if (added != RunStateResult.Ok) return V1ImportResult.Fail(V1ImportCodes.Invalid);
            }

            // The import's own settle (see the class remarks): once, at r, before the candidate exists.
            RunAdvanceResult settled = new RunSimulator(catalog).Advance(run, permanent, 0, r, null);
            if (settled == RunAdvanceResult.ArithmeticOverflow) return V1ImportResult.Fail(V1ImportCodes.Arithmetic);
            if (settled != RunAdvanceResult.Ok || run.Validate() != RunStateResult.Ok) return V1ImportResult.Fail(V1ImportCodes.Invalid);

            var candidate = new RunCheckpoint(candidateId, generation, r, catalog.OfflinePolicy.Revision, null, catalog.ContentRevision,
                new RunCheckpointImport(1, v1.Id.Value, v1.Generation), run, permanent);
            return V1ImportResult.Success(candidate);
        }

        private static bool IsSupported(BuildingInstanceState building)
        {
            if (building.DefinitionId.Value != V1MachineId || building.Production.RecipeId.Value != V1RecipeId) return false;
            return BufferIsSupported(building.InputBuffer) && BufferIsSupported(building.OutputBuffer);
        }

        private static bool BufferIsSupported(ResourceBufferState buffer)
        {
            foreach (KeyValuePair<ResourceId, long> entry in buffer.Quantities)
            {
                string id = entry.Key.Value;
                if (id != V1InputResourceId && id != V1OutputResourceId) return false;
            }

            return true;
        }

        private static bool TryAddBuffer(ResourceBufferState buffer, ref long ore, ref long bar)
        {
            foreach (KeyValuePair<ResourceId, long> entry in buffer.Quantities)
            {
                if (entry.Key.Value == V1InputResourceId)
                {
                    if (!CheckedMath.TryAdd(ore, entry.Value, out ore)) return false;
                }
                else if (!CheckedMath.TryAdd(bar, entry.Value, out bar))
                {
                    return false;
                }
            }

            return true;
        }

        private static int CompareBuildings(BuildingInstanceState left, BuildingInstanceState right)
        {
            int byY = left.Anchor.Y.CompareTo(right.Anchor.Y);
            if (byY != 0) return byY;
            int byX = left.Anchor.X.CompareTo(right.Anchor.X);
            return byX != 0 ? byX : string.CompareOrdinal(left.Id.Value, right.Id.Value);
        }

        private static bool IsExpectedFailure(Exception exception) =>
            exception is ArgumentException || exception is InvalidOperationException || exception is OverflowException ||
            exception is KeyNotFoundException || exception is NotSupportedException || exception is System.IO.IOException ||
            exception is UnauthorizedAccessException || exception is System.Security.SecurityException;
    }

    /// <summary>
    /// The fresh run of the content (R10 Start new run, OD-3, OD-5): the same defaults the import uses for the fields V1
    /// does not have, settled once at the given instant like the first settle of any new run.
    /// </summary>
    internal static class FreshRunStart
    {
        /// <summary>Builds a fresh, settled run 1 with an empty permanent state; null on success, otherwise the rejection code.</summary>
        public static string TryBuild(RunCatalog catalog, Guid runId, long timelineUtcMs, out RunState run, out PermanentState permanent)
        {
            run = null;
            permanent = null;
            if (runId == Guid.Empty || !ExactBound.Contains(timelineUtcMs)) return RunReasons.CommandInvalid;
            var fresh = new PermanentState(catalog);
            RunState state;
            try
            {
                state = NextRunStart.CreateRun(catalog, fresh, runId, 1, null);
            }
            catch (ArgumentException)
            {
                return RunReasons.CommandInvalid;
            }

            RunAdvanceResult settled = new RunSimulator(catalog).Advance(state, fresh, 0, timelineUtcMs, null);
            if (settled != RunAdvanceResult.Ok) return RunReasons.From(settled) ?? RunReasons.CommandInvalid;
            run = state;
            permanent = fresh;
            return null;
        }
    }
}
