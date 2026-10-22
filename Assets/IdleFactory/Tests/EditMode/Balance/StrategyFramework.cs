using System;
using System.Collections.Generic;
using System.Diagnostics;
using IdleFactory.Run.Content;

namespace IdleFactory.Tests.EditMode.Balance
{
    /// <summary>
    /// What the balance trace needs from a strategy (T311, <c>specs/005-economy-contracts/reviews/balance-policy-r2.md</c> section 7).
    /// A strategy decides the commands of one look and the Industry spending after a prestige, and fixes the look grid and the
    /// away window. Every implementation is deterministic: it reads only the run state, never the wall clock or a random source.
    /// <see cref="ReferencePolicy"/> is the first implementation; the other strategies are configurations of it.
    /// </summary>
    internal interface IStrategyPolicy
    {
        /// <summary>The strategy id of balance-policy-r2.md section 7 (<c>chain</c>, <c>ore-only</c>, ...).</summary>
        string Id { get; }

        /// <summary>Seconds between two looks. The ensemble offsets scale with it (<see cref="StrategyEnsemble.Offsets"/>).</summary>
        int LookIntervalSeconds { get; }

        /// <summary>The away window is the run seconds from this value up to the end of every <see cref="IdleWindowCycleSeconds"/>.</summary>
        int IdleWindowStartSeconds { get; }

        int IdleWindowCycleSeconds { get; }

        int ContractCooldownSeconds { get; }

        /// <summary>The prestige happens at Factory Value of this multiple of the threshold in ppm (1,000,000 = at the threshold; prestige later = 1.5x or 3x).</summary>
        long PrestigeFactorPpm { get; }

        /// <summary>Machines the contract lever holds switched off right now (the held-off counter of section 4.3).</summary>
        int HeldOffMachineCount { get; }

        /// <summary>Called at the start of every run, before the first look; <paramref name="offsetSeconds"/> is the ensemble offset of the look grid.</summary>
        void BeginRun(TraceContext context, int offsetSeconds);

        /// <summary>One look: issues the commands and returns how many were issued.</summary>
        int Act(TraceContext context);

        /// <summary>Spends the IP after a prestige and returns the number of purchases.</summary>
        int SpendIndustry(TraceContext context);

        /// <summary>The activations of the held mining click at a whole run second (section 6); 0 when the strategy does not click then.</summary>
        long HeldClickActivations(TraceContext context, long runSecond);

        /// <summary>The next run second, after <paramref name="runSecond"/>, at which the held click may be wanted again (long.MaxValue: never in this run).</summary>
        long HeldClickResumeSecond(long runSecond);
    }

    internal static class StrategyPolicyExtensions
    {
        /// <summary>The away window is a function of the run second, not of the look index (ECO-14).</summary>
        public static bool IsIdleAt(this IStrategyPolicy policy, long runSecond) => runSecond % policy.IdleWindowCycleSeconds >= policy.IdleWindowStartSeconds;
    }

    /// <summary>One strategy of the ensemble: an id, a one-line rule and a factory for a fresh policy (a policy keeps per-history state).</summary>
    internal sealed class StrategyDefinition
    {
        public StrategyDefinition(string id, string rule, Func<ReferencePolicy> create)
        {
            Id = id;
            Rule = rule;
            Create = create;
        }

        public string Id { get; }
        public string Rule { get; }
        public Func<ReferencePolicy> Create { get; }
    }

    /// <summary>
    /// The strategy definitions of balance-policy-r2.md section 7: twelve definitions (the nine FR-023 strategies with prestige-later at its two
    /// factors, plus chain+mining and mining-only of F5-R20). Each is a configuration of <see cref="ReferencePolicy"/>.
    /// </summary>
    internal static class Strategies
    {
        public static readonly StrategyDefinition Chain = new StrategyDefinition("chain",
            "Reference policy r2: full chain, contracts, held mining click until the first Miner, prestige at the first non-idle look at or after eligibility.",
            () => new ReferencePolicy { Id = "chain" });

        public static readonly StrategyDefinition OreOnly = new StrategyDefinition("ore-only",
            "Only Miners are built; no contracts; all Ore sold by auto-sell; prestige at the threshold.",
            () => new ReferencePolicy { Id = "ore-only", OreOnly = true, ContractsEnabled = false });

        public static readonly StrategyDefinition ContractsOnly = new StrategyDefinition("contracts-only",
            "Chain machine mix; auto-sell off for every resource, no Sell; the only Cash from the factory is contract rewards.",
            () => new ReferencePolicy { Id = "contracts-only", ContractsOnly = true });

        public static readonly StrategyDefinition IdleHeavy = new StrategyDefinition("idle-heavy",
            "Chain, but the player is away from second 120 to 299 of every 300 s (60 % idle).",
            () => new ReferencePolicy { Id = "idle-heavy", IdleWindowStartSeconds = 120 });

        public static readonly StrategyDefinition Casual = new StrategyDefinition("casual",
            "Chain with a look every " + ReferencePolicy.CasualLookIntervalSeconds + " s (pinned L_casual) and the held mining click at 25 % of the cap.",
            () => new ReferencePolicy
            {
                Id = "casual",
                LookIntervalSeconds = ReferencePolicy.CasualLookIntervalSeconds,
                MiningAfterFirstMinerPpm = ReferencePolicy.CasualMiningFractionPpm,
            });

        public static readonly StrategyDefinition PrestigeLaterLow = new StrategyDefinition("prestige-later-1.5",
            "Chain, but the prestige waits until Factory Value is 1.5 times the threshold (or the run limit).",
            () => new ReferencePolicy { Id = "prestige-later-1.5", PrestigeFactorPpm = ReferencePolicy.LaterFactorLowPpm });

        public static readonly StrategyDefinition PrestigeLaterHigh = new StrategyDefinition("prestige-later-3",
            "Chain, but the prestige waits until Factory Value is 3 times the threshold (or the run limit).",
            () => new ReferencePolicy { Id = "prestige-later-3", PrestigeFactorPpm = ReferencePolicy.LaterFactorHighPpm });

        public static readonly StrategyDefinition StopAtBar = new StrategyDefinition("stop-at-bar",
            "Miner and Smelter only; the Iron Bar is the terminal and is sold; feasible contracts only.",
            () => new ReferencePolicy { Id = "stop-at-bar", Scope = ReferencePolicy.StopAtBarScope });

        public static readonly StrategyDefinition StopAtPlate = new StrategyDefinition("stop-at-plate",
            "Miner, Smelter and Press only; the Iron Plate is the terminal and is sold; feasible contracts only.",
            () => new ReferencePolicy { Id = "stop-at-plate", Scope = ReferencePolicy.StopAtPlateScope });

        public static readonly StrategyDefinition MarketOnlyChain = new StrategyDefinition("market-only-chain",
            "Chain with contracts off: no AcceptContract, no SetAutoDeliver, no Items from the ladder.",
            () => new ReferencePolicy { Id = "market-only-chain", ContractsEnabled = false });

        public static readonly StrategyDefinition ChainPlusMining = new StrategyDefinition("chain+mining",
            "Chain, but the held click mines the whole per-second cap in every active second of every run.",
            () => new ReferencePolicy { Id = "chain+mining", MiningAfterFirstMinerPpm = ReferencePolicy.ChainMiningFractionPpm });

        public static readonly StrategyDefinition MiningOnly = new StrategyDefinition("mining-only",
            "Builds nothing; mines the whole per-second cap in every active second; sells by auto-sell; buys Industry.",
            () => new ReferencePolicy
            {
                Id = "mining-only",
                BuildsNothing = true,
                ContractsEnabled = false,
                OverdriveEnabled = false,
                MiningAfterFirstMinerPpm = ReferencePolicy.ChainMiningFractionPpm,
            });

        public static readonly IReadOnlyList<StrategyDefinition> All = new[]
        {
            Chain, OreOnly, ContractsOnly, IdleHeavy, Casual, PrestigeLaterLow, PrestigeLaterHigh, StopAtBar, StopAtPlate, MarketOnlyChain, ChainPlusMining, MiningOnly,
        };

        /// <summary>The single-lever set of ECO-13 (FR-024's "no single-lever strategy faster than the chain" and SC-002).</summary>
        public static readonly IReadOnlyList<StrategyDefinition> SingleLever = new[] { OreOnly, ContractsOnly, StopAtBar, StopAtPlate };

        /// <summary>The strategies of the 4.0x rule: the at-threshold chain and both prestige-later factors.</summary>
        public static readonly IReadOnlyList<StrategyDefinition> IpRule = new[] { Chain, PrestigeLaterLow, PrestigeLaterHigh };

        public static StrategyDefinition ById(string id)
        {
            foreach (StrategyDefinition s in All)
                if (s.Id == id) return s;
            throw new ArgumentException("unknown strategy " + id, nameof(id));
        }
    }

    /// <summary>The runs 1 to 5 of one strategy at one look-grid offset, with the wall-clock measurement of each run.</summary>
    internal sealed class StrategyHistory
    {
        public string StrategyId;
        public int OffsetSec;
        public readonly List<RunRecord> Runs = new List<RunRecord>();

        /// <summary>Wall-clock milliseconds per played run. The only place where wall-clock time is read; it never feeds a decision.</summary>
        public readonly List<long> RunMilliseconds = new List<long>();

        public readonly List<string> Violations = new List<string>();

        /// <summary>Time to eligibility at run index n (1-based) in run seconds, or -1 when the run was not played or did not reach the threshold.</summary>
        public long EligibleAt(int runIndex) => runIndex >= 1 && runIndex <= Runs.Count ? Runs[runIndex - 1].EligibleAtSec : -1;

        /// <summary>
        /// The cumulative elapsed play time C(n) of balance-policy-r2.md section 8.3: the run seconds from the start of run 1 to the prestige that
        /// completes run n, every earlier run and the wait after eligibility included; -1 when the history did not prestige run n.
        /// </summary>
        public long CumulativeAt(int runIndex)
        {
            if (runIndex < 1 || runIndex > Runs.Count) return -1;
            long total = 0;
            for (int i = 0; i < runIndex; i++)
            {
                if (Runs[i].PrestigedAtSec < 0) return -1;
                total += Runs[i].PrestigedAtSec;
            }
            return total;
        }
    }

    /// <summary>The runner extension: plays strategies for runs 1 to 5 over the five look-grid offsets and takes medians per run index.</summary>
    internal static class StrategyEnsemble
    {
        public const int RunCount = 5;
        public const int OffsetCount = 5;

        /// <summary>
        /// The five look-grid offsets of a look interval: <c>i * L / 5</c> with integer division, so the reference grid (L = 20) gives
        /// 0, 4, 8, 12, 16 and a casual grid covers the same fractions of its look interval (balance-policy-r2.md section 7, 8.1).
        /// </summary>
        public static int[] Offsets(int lookIntervalSeconds)
        {
            if (lookIntervalSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(lookIntervalSeconds));
            var offsets = new int[OffsetCount];
            for (int i = 0; i < OffsetCount; i++) offsets[i] = i * lookIntervalSeconds / OffsetCount;
            return offsets;
        }

        /// <summary>
        /// The median of the ensemble values for one run index (balance-policy-r2.md section 8.1). A negative value means "not reached" and
        /// is ordered above every number; the median of five is the third smallest, and it is "not reached" (-1) when three or more are.
        /// For an even count the lower of the two middle values is taken.
        /// </summary>
        public static long Median(IReadOnlyList<long> values)
        {
            if (values == null || values.Count == 0) throw new ArgumentException("no values", nameof(values));
            var sorted = new long[values.Count];
            for (int i = 0; i < sorted.Length; i++) sorted[i] = values[i] < 0 ? long.MaxValue : values[i];
            Array.Sort(sorted);
            long median = sorted[(sorted.Length - 1) / 2];
            return median == long.MaxValue ? -1 : median;
        }

        /// <summary>Median of the available values (a missing value is skipped); null when fewer than three of them exist.</summary>
        public static long? MedianOfAvailable(IReadOnlyList<long?> values)
        {
            var present = new List<long>();
            foreach (long? v in values)
                if (v.HasValue) present.Add(v.Value);
            if (present.Count < 3) return null;
            present.Sort();
            return present[(present.Count - 1) / 2];
        }

        /// <summary>Plays runs 1 to <paramref name="runs"/> of one strategy at one offset: prestige at eligibility, then all IP spent.</summary>
        public static StrategyHistory PlayHistory(RunCatalog catalog, StrategyDefinition strategy, int offsetSec, int runs = RunCount, long stopAfterSec = -1)
        {
            ReferencePolicy policy = strategy.Create();
            var runner = new BalanceTraceRunner(catalog, policy) { RecordCheckpoints = true };
            var history = new StrategyHistory { StrategyId = strategy.Id, OffsetSec = offsetSec };
            for (int n = 1; n <= runs; n++)
            {
                var clock = Stopwatch.StartNew();
                RunRecord rec = runner.PlayRun(strategy.Id + ".run" + n, n, true, 0, null, offsetSec, stopAfterSec);
                if (rec.PrestigedAtSec >= 0) runner.SpendIndustry();
                clock.Stop();
                history.Runs.Add(rec);
                history.RunMilliseconds.Add(clock.ElapsedMilliseconds);
                if (rec.PrestigedAtSec < 0) break;
            }
            history.Violations.AddRange(runner.Context.Violations);
            return history;
        }

        /// <summary>Plays one strategy over its five offsets.</summary>
        public static List<StrategyHistory> PlayEnsemble(RunCatalog catalog, StrategyDefinition strategy, int runs = RunCount, long stopAfterSec = -1)
        {
            var histories = new List<StrategyHistory>();
            foreach (int offset in Offsets(strategy.Create().LookIntervalSeconds))
                histories.Add(PlayHistory(catalog, strategy, offset, runs, stopAfterSec));
            return histories;
        }

        /// <summary>Median over the offsets of the time to eligibility at run index n (1-based); -1 is "not reached".</summary>
        public static long MedianEligible(IReadOnlyList<StrategyHistory> histories, int runIndex)
        {
            var values = new List<long>();
            foreach (StrategyHistory h in histories) values.Add(h.EligibleAt(runIndex));
            return Median(values);
        }

        /// <summary>Median over the offsets of the Factory Value at an exact run second of run n (1-based); null when fewer than three offsets have it.</summary>
        public static long? MedianFactoryValue(IReadOnlyList<StrategyHistory> histories, int runIndex, int checkpointSec)
        {
            var values = new List<long?>();
            foreach (StrategyHistory h in histories)
            {
                long? v = null;
                if (runIndex <= h.Runs.Count && h.Runs[runIndex - 1].CheckpointFactoryValue.TryGetValue(checkpointSec, out long fv)) v = fv;
                values.Add(v);
            }
            return MedianOfAvailable(values);
        }
    }
}
