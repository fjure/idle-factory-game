using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;

namespace IdleFactory.Run.Application
{
    /// <summary>
    /// The next run, fully built and settled, while the transition is pending (R09, QA-4).
    ///
    /// In-memory representation of the pending transition (T102; T105 makes it durable): the candidate is a
    /// self-contained pair of NEW objects, <see cref="Run"/> (a fresh content run with the Industry starting
    /// effects) and <see cref="Permanent"/> (a copy of the permanent state with <c>ip + award</c>, the prestige
    /// counter plus one, the Industry levels and the unused Item stacks). The live <see cref="RunState"/> and
    /// <see cref="PermanentState"/> are not touched while it is pending, so abandoning a candidate changes nothing
    /// and the durable layer can serialize the candidate (its <see cref="Run"/> is already settled, so the bytes
    /// equal what is published) before <see cref="RunCommands.PromotePrestige"/> publishes it. It is held by the
    /// <see cref="RunSession"/> (<see cref="RunSession.PendingTransition"/>), which rejects commands and advancing
    /// meanwhile, so the candidate cannot go stale.
    ///
    /// Idempotency key for T105: <c>Run.PreviousRunId</c> equals the confirmed run's ID (contracts/save-v2-and-v1-import.md).
    /// </summary>
    public sealed class PrestigeCandidate
    {
        public RunState Run { get; }
        public PermanentState Permanent { get; }

        /// <summary>The Factory Value the award was computed from, at the command instant.</summary>
        public FactoryValueBreakdown FactoryValue { get; }
        public long IpAwarded { get; }

        /// <summary>The timeline instant of the confirmation: the departure of the old run and the anchor of the new one.</summary>
        public long ConfirmedUtcMs { get; }

        /// <summary>The offer of an untimed or timed contract that was active and is dropped by the reset, or null.</summary>
        public string AbandonedContractOfferId { get; }

        /// <summary>The number of running Item effects the reset drops (their Items were spent when activated).</summary>
        public int DroppedEffectCount { get; }

        internal PrestigeCandidate(RunState run, PermanentState permanent, FactoryValueBreakdown factoryValue, long ipAwarded,
            long confirmedUtcMs, string abandonedContractOfferId, int droppedEffectCount)
        {
            Run = run;
            Permanent = permanent;
            FactoryValue = factoryValue;
            IpAwarded = ipAwarded;
            ConfirmedUtcMs = confirmedUtcMs;
            AbandonedContractOfferId = abandonedContractOfferId;
            DroppedEffectCount = droppedEffectCount;
        }
    }

    /// <summary>The result of <see cref="RunCommands.ConfirmPrestige"/>: what was awarded and the pending candidate.</summary>
    public sealed class PrestigeConfirmation
    {
        public Guid OldRunId { get; }
        public Guid NewRunId { get; }
        public int NewRunNumber { get; }
        public long IpAwarded { get; }

        /// <summary>The IP balance after the award (the candidate's).</summary>
        public long IpTotal { get; }
        public FactoryValueBreakdown FactoryValue { get; }
        public long NextRunStartingCash { get; }
        public int NextRunStorageLevel { get; }
        public string AbandonedContractOfferId { get; }
        public int DroppedEffectCount { get; }

        /// <summary>The pending candidate (also <see cref="RunSession.PendingTransition"/>).</summary>
        public PrestigeCandidate Candidate { get; }

        internal PrestigeConfirmation(Guid oldRunId, PrestigeCandidate candidate)
        {
            OldRunId = oldRunId;
            Candidate = candidate;
            NewRunId = candidate.Run.RunId;
            NewRunNumber = candidate.Run.RunNumber;
            IpAwarded = candidate.IpAwarded;
            IpTotal = candidate.Permanent.Ip;
            FactoryValue = candidate.FactoryValue;
            NextRunStartingCash = candidate.Run.Cash;
            NextRunStorageLevel = candidate.Run.StorageLevel;
            AbandonedContractOfferId = candidate.AbandonedContractOfferId;
            DroppedEffectCount = candidate.DroppedEffectCount;
        }
    }

    /// <summary>The prestige surface read model (FR-022): FV breakdown, IP award, eligibility and the reset and preserve lists.</summary>
    public sealed class PrestigePreview
    {
        public FactoryValueBreakdown FactoryValue { get; }
        public long ThresholdFactoryValue { get; }

        /// <summary>The IP the current Factory Value would award (shown below the threshold as well).</summary>
        public long IpAward { get; }
        public long CurrentIp { get; }
        public long IpAfter { get; }

        /// <summary>True when <c>FV &gt;= threshold</c> and no transition is pending.</summary>
        public bool IsEligible { get; }

        /// <summary><c>threshold - FV</c> below the threshold, otherwise 0.</summary>
        public long MissingFactoryValue { get; }

        /// <summary><c>prestige.below-threshold</c>, <c>transition.pending</c> or null when eligible.</summary>
        public string Reason { get; }
        public int NextRunNumber { get; }
        public long NextRunStartingCash { get; }
        public int NextRunStorageLevel { get; }

        /// <summary>Owned Items in total; they all survive.</summary>
        public long PreservedItemCount { get; }

        /// <summary>Running Item effects that the reset drops.</summary>
        public int ActiveEffectCount { get; }

        /// <summary>The active contract the reset drops (no reward, no penalty), or null.</summary>
        public string ActiveContractOfferId { get; }

        /// <summary>What resets (<see cref="PrestigeTransition.ResetList"/>).</summary>
        public IReadOnlyList<string> Reset { get; }

        /// <summary>What is preserved (<see cref="PrestigeTransition.PreserveList"/>).</summary>
        public IReadOnlyList<string> Preserve { get; }

        internal PrestigePreview(FactoryValueBreakdown factoryValue, long threshold, long ipAward, long currentIp, long ipAfter,
            bool eligible, string reason, int nextRunNumber, NextRunStart start, long preservedItems, int activeEffects,
            string activeContract)
        {
            FactoryValue = factoryValue;
            ThresholdFactoryValue = threshold;
            IpAward = ipAward;
            CurrentIp = currentIp;
            IpAfter = ipAfter;
            IsEligible = eligible;
            MissingFactoryValue = factoryValue.Total >= threshold ? 0 : threshold - factoryValue.Total;
            Reason = reason;
            NextRunNumber = nextRunNumber;
            NextRunStartingCash = start.StartingCash;
            NextRunStorageLevel = start.StorageLevel;
            PreservedItemCount = preservedItems;
            ActiveEffectCount = activeEffects;
            ActiveContractOfferId = activeContract;
            Reset = PrestigeTransition.ResetList;
            Preserve = PrestigeTransition.PreserveList;
        }
    }

    /// <summary>
    /// The in-memory part of the prestige transition (FR-020 - FR-022, FR-024): the reset and preserve lists and the
    /// pure function that builds the next-run candidate. The durable part (write, verify, publish; R09) is T105.
    ///
    /// Interpretations where the contract is silent: (1) Industry starting effects are free, so
    /// <c>StorageInvestedCash</c> of the new run is 0 and only the starting Cash counts in its Factory Value;
    /// (2) the new run starts at factory level 1, with no machines, priorities, auto-sell settings, RP, Research,
    /// effects, Overdrive timers or contracts (completion counts and <c>LastExpired</c> included), and its unlocks are
    /// <c>RunStart.InitialUnlocks</c> plus what one settle unlocks (T140); (3) the prestige counter of the permanent
    /// state is incremented, which keeps <c>RunNumber = PrestigeCount + 1</c> (data-model.md); (4) an active contract
    /// is dropped without reward or penalty and reported in the result.
    /// </summary>
    public static class PrestigeTransition
    {
        // Stable keys (UI copy keys off them; they never change). Order is the display order.
        public const string ResetCash = "cash";
        public const string ResetStock = "stock";
        public const string ResetMachines = "machines";
        public const string ResetPriorities = "priorities";
        public const string ResetAutoSell = "autoSell";
        public const string ResetExpansion = "expansion";
        public const string ResetContracts = "contracts";
        public const string ResetResearch = "research";
        public const string ResetResearchPoints = "researchPoints";
        public const string ResetEffects = "activeEffects";
        public const string ResetOverdrive = "overdrive";
        public const string ResetUnlocks = "unlocks";

        public const string PreserveIp = "influencePoints";
        public const string PreserveIndustry = "industryUpgrades";
        public const string PreserveItems = "items";

        private static readonly string[] Reset =
        {
            ResetCash, ResetStock, ResetMachines, ResetPriorities, ResetAutoSell, ResetExpansion, ResetContracts, ResetResearch,
            ResetResearchPoints, ResetEffects, ResetOverdrive, ResetUnlocks,
        };

        private static readonly string[] Preserve = { PreserveIp, PreserveIndustry, PreserveItems };

        public static IReadOnlyList<string> ResetList { get; } = new ReadOnlyCollection<string>(Reset);

        public static IReadOnlyList<string> PreserveList { get; } = new ReadOnlyCollection<string>(Preserve);

        /// <summary>
        /// Builds the complete next-run candidate for a run whose Factory Value and award are known: a copy of the
        /// permanent state with <c>ip + award</c>, and a fresh run (runId', previousRunId = old runId, runNumber + 1)
        /// with the Industry starting effects (<see cref="NextRunStart"/>), settled once at
        /// <paramref name="timelineUtcMs"/> so the unlocks of T140 apply. Nothing live is touched. Returns a
        /// rejection code (<c>value.limit</c>) when a checked step overflows.
        /// </summary>
        public static string TryBuild(RunCatalog catalog, RunState old, PermanentState permanent, FactoryValueBreakdown factoryValue,
            long ipAward, Guid newRunId, long timelineUtcMs, out PrestigeCandidate candidate)
        {
            candidate = null;
            if (catalog == null || old == null || permanent == null) throw new ArgumentNullException(catalog == null ? nameof(catalog) : old == null ? nameof(old) : nameof(permanent));
            if (newRunId == Guid.Empty || newRunId == old.RunId || old.RunNumber == int.MaxValue) return RunReasons.CommandInvalid;

            var next = new PermanentState(catalog, permanent.Ip, permanent.PrestigeCount);
            foreach (IdCount level in permanent.IndustryLevels())
                if (next.TrySetIndustryLevel(level.Id, level.Count) != RunStateResult.Ok) return RunReasons.CommandInvalid;
            foreach (IdCount stack in permanent.ItemStacks())
                if (next.TryAddItems(stack.Id, stack.Count) != RunStateResult.Ok) return RunReasons.ValueLimit;
            RunStateResult awarded = next.TryAddIp(ipAward);
            if (awarded != RunStateResult.Ok) return RunReasons.From(awarded);
            RunStateResult counted = next.TryIncrementPrestige();
            if (counted != RunStateResult.Ok) return RunReasons.From(counted);

            NextRunStart start = NextRunStart.Compute(catalog, next);
            RunState run;
            try
            {
                run = new RunState(catalog, newRunId, old.RunNumber + 1, old.RunId, start.StartingCash, 0, start.StorageLevel, 1,
                    catalog.RunStart.InitialUnlocks);
            }
            catch (ArgumentException)
            {
                return RunReasons.CommandInvalid;
            }

            // The creation ends with one settle (T101 handoff): the permanent state is bound, the unlock pass
            // reaches its fixed point, and the candidate equals what will be published.
            RunAdvanceResult settled = new RunSimulator(catalog).Advance(run, next, 0, timelineUtcMs, null);
            if (settled != RunAdvanceResult.Ok) return RunReasons.From(settled) ?? RunReasons.CommandInvalid;

            candidate = new PrestigeCandidate(run, next, factoryValue, ipAward, timelineUtcMs,
                old.Contracts.Active?.OfferId, old.Effects.Count);
            return null;
        }
    }
}
