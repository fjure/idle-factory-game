using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using IdleFactory.Run.Content;

namespace IdleFactory.Tests.EditMode.Balance
{
    /// <summary>The outcome of the casual look-interval search (balance-policy-r2.md section 7.1).</summary>
    internal sealed class CasualCalibration
    {
        public int LookIntervalSeconds;
        public long ChainFactoryValue900;
        public long CasualFactoryValue900;
        public readonly List<string> Tried = new List<string>();
    }

    /// <summary>
    /// Builds the orientation report of T311: the strategies of <see cref="Strategies"/> on the shipped content, five offsets,
    /// runs 1 to 5, medians per run index, wall-clock per strategy and offset and the projection of the 9-strategy matrix (PF-15).
    /// It asserts nothing; the output is orientation (balance-policy-r2.md section 1, FR-023).
    /// </summary>
    internal static class StrategyBaselineReport
    {
        public const int ProjectedStrategies = 9;
        public const int ProjectedDefinitions = 12;
        public const double BudgetMinutes = 20.0;

        /// <summary>
        /// The search of section 7.1 for a given content: the smallest integer look interval, from 40 s upward in steps of 1 s, whose
        /// median Factory Value at run second 900 of run 1 over the five casual offsets is at most 0.4 of the chain median.
        /// </summary>
        public static CasualCalibration FindCasualLookInterval(RunCatalog catalog, int startSeconds = 40, int limitSeconds = 400)
        {
            List<StrategyHistory> chain = StrategyEnsemble.PlayEnsemble(catalog, Strategies.Chain, 1, 900);
            long chainFv = StrategyEnsemble.MedianFactoryValue(chain, 1, 900) ?? throw new InvalidOperationException("The chain has no Factory Value at second 900.");
            var result = new CasualCalibration { ChainFactoryValue900 = chainFv };
            for (int look = startSeconds; look <= limitSeconds; look++)
            {
                int captured = look;
                var candidate = new StrategyDefinition("casual-search", "calibration candidate",
                    () => new ReferencePolicy { Id = "casual-search", LookIntervalSeconds = captured });
                List<StrategyHistory> casual = StrategyEnsemble.PlayEnsemble(catalog, candidate, 1, 900);
                long fv = StrategyEnsemble.MedianFactoryValue(casual, 1, 900) ?? throw new InvalidOperationException("No casual Factory Value at second 900.");
                result.Tried.Add(look + " s: " + fv);
                if (fv * 5 <= chainFv * 2)
                {
                    result.LookIntervalSeconds = look;
                    result.CasualFactoryValue900 = fv;
                    return result;
                }
            }
            throw new InvalidOperationException("No look interval up to " + limitSeconds + " s satisfies the casual ratio.");
        }

        /// <summary>The matrix of the strategies: one list of five histories per strategy.</summary>
        public static Dictionary<string, List<StrategyHistory>> PlayMatrix(RunCatalog catalog)
        {
            var matrix = new Dictionary<string, List<StrategyHistory>>(StringComparer.Ordinal);
            foreach (StrategyDefinition strategy in Strategies.All)
                matrix[strategy.Id] = StrategyEnsemble.PlayEnsemble(catalog, strategy);
            return matrix;
        }

        public static string BuildCommit(string workingDirectory)
        {
            try
            {
                string head = Git(workingDirectory, "rev-parse --short=9 HEAD");
                string dirty = Git(workingDirectory, "status --porcelain -- Assets/IdleFactory");
                return head + (string.IsNullOrWhiteSpace(dirty) ? "" : " (+ uncommitted changes under Assets/IdleFactory)");
            }
            catch (Exception e)
            {
                return "unknown (" + e.GetType().Name + ")";
            }
        }

        private static string Git(string workingDirectory, string arguments)
        {
            var info = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using (Process p = Process.Start(info))
            {
                string output = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit();
                return output;
            }
        }

        private static string N(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

        private static string Time(long seconds) => seconds < 0 ? "not reached" : N(seconds) + " s";

        private static string Opt(long? value) => value.HasValue ? N(value.Value) : "-";

        private static long? MedianStat(IReadOnlyList<StrategyHistory> histories, int runIndex, Func<RunRecord, long?> selector)
        {
            var values = new List<long?>();
            foreach (StrategyHistory h in histories)
                values.Add(runIndex <= h.Runs.Count ? selector(h.Runs[runIndex - 1]) : (long?)null);
            return StrategyEnsemble.MedianOfAvailable(values);
        }

        /// <summary>
        /// Commands issued in run n. The trace counts them cumulatively over the runs of a history (TraceContext.Commands is never reset), and
        /// the golden CSV carries that cumulative value, so the report takes the difference to the previous run of the same history.
        /// </summary>
        private static long? MedianCommands(IReadOnlyList<StrategyHistory> histories, int runIndex)
        {
            var values = new List<long?>();
            foreach (StrategyHistory h in histories)
            {
                if (runIndex > h.Runs.Count) { values.Add(null); continue; }
                long now = Stat(h.Runs[runIndex - 1], "commands");
                long before = runIndex > 1 ? Stat(h.Runs[runIndex - 2], "commands") : 0;
                values.Add(now - before);
            }
            return StrategyEnsemble.MedianOfAvailable(values);
        }

        private static long Stat(RunRecord rec, string key) => rec.Stats.TryGetValue(key, out long value) ? value : 0;

        public static string Compose(RunCatalog catalog, CasualCalibration calibration, Dictionary<string, List<StrategyHistory>> matrix, string buildCommit, string contentPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Strategy baseline on content revision 2 (T311, issue #373)");
            sb.AppendLine();
            sb.AppendLine("**Orientation only. No band in this report is asserted.** The gates are the FR-024 list of `balance-policy-r2.md` section 8.2, which T341 adds. Every number below is a measurement of the engine on the content named here, by the generator `StrategyBaselineReport` (`Assets/IdleFactory/Tests/EditMode/Balance/`), run through `StrategyFrameworkTests.WriteStrategyBaselineReport` (category `BalanceStrategies`). The full matrix, the casual search and this file are produced only on opt-in: `$env:IDLEFACTORY_WRITE_STRATEGY_BASELINE=1; scripts/test-unity.ps1 -Suite EditMode -Category BalanceStrategies`. Without the variable the test plays only the fast gate (chain and ore-only, offset 0, runs 1 and 2).");
            sb.AppendLine();
            sb.AppendLine("## 1. Inputs");
            sb.AppendLine();
            sb.AppendLine("| Item | Value | Source |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine("| Build commit | `" + buildCommit + "` | `git rev-parse` in the generator |");
            sb.AppendLine("| Content | `" + contentPath + "`, `contentRevision` " + catalog.ContentRevision + " | loaded by `GameplayContentDispatcher.Load` |");
            sb.AppendLine("| Content state | **Transitional**: schema version 3 with the revision 2 values; contract Cash comes through the transitional reward adapter (`ceil(quantity x price x ratio)`), not the F003 curves | `Assets/IdleFactory/Tests/EditMode/Fixtures/Balance/trace-r2-transitional.md` (T313) |");
            sb.AppendLine("| Policy | the current reference policy (policy r1 behaviour) behind `IStrategyPolicy`; policy r2 is T341 | `ReferencePolicy.cs` |");
            sb.AppendLine("| Look grid | looks at run second `offset + k x L`; reference `L` = 20 s; away window 240 to 299 of every 300 s (idle-heavy: 120 to 299) | `balance-policy-r2.md` sections 2, 7 |");
            sb.AppendLine("| Offsets | 0, 4, 8, 12, 16 s (casual: `i x L_casual / 5`, integer division: " + string.Join(", ", StrategyEnsemble.Offsets(ReferencePolicy.CasualLookIntervalSeconds)) + " s) | `balance-policy-r2.md` section 8.1 |");
            sb.AppendLine("| Median | per run index over the 5 offsets: third smallest, \"not reached\" ordered above every number; a `-` means fewer than 3 offsets have the value (for example the run ended before that second) | `StrategyEnsemble.Median` |");
            sb.AppendLine("| Time to threshold | run second of the first look at which the prestige preview reports eligible (`EligibleAtSec`); limit 21,600 s of run time | `BalanceTraceRunner.PlayRun` |");
            sb.AppendLine("| Not simulated | the held mining click (no `Mine` command before F005): `casual` uses its look interval only, no 25 % mining | `balance-policy-r2.md` sections 6, 7 |");
            sb.AppendLine();
            sb.AppendLine("## 2. Casual calibration (section 7.1)");
            sb.AppendLine();
            sb.AppendLine("`ReferencePolicy.CasualLookIntervalSeconds` = **" + ReferencePolicy.CasualLookIntervalSeconds + " s** [provisional]. The search on this build returns " + calibration.LookIntervalSeconds + " s (" + (calibration.LookIntervalSeconds == ReferencePolicy.CasualLookIntervalSeconds ? "equal to the pin" : "different from the pin: the pin is not recalibrated") + ").");
            sb.AppendLine();
            sb.AppendLine("- Rule: the smallest integer `L`, searched from 40 s in steps of 1 s, for which the median over the five casual offsets of the Factory Value at run second 900 of run 1 is at most 0.4 (1 / 2.5, the owner playtest of 2026-10-09, `docs/agents/roles-and-reviews.md` Play-Tester) of the chain median at the same second.");
            sb.AppendLine("- Chain median Factory Value at second 900 (offsets 0, 4, 8, 12, 16): **" + N(calibration.ChainFactoryValue900) + "**.");
            sb.AppendLine("- Casual median at `L` = " + calibration.LookIntervalSeconds + " s: **" + N(calibration.CasualFactoryValue900) + "**; ratio **" + ((double)calibration.CasualFactoryValue900 / calibration.ChainFactoryValue900).ToString("0.000", CultureInfo.InvariantCulture) + "** (limit 0.400).");
            sb.AppendLine("- Candidates tried (L: casual median FV at 900 s): " + string.Join("; ", calibration.Tried) + ".");
            sb.AppendLine("- The Factory Value is read at the exact run second 900, by splitting the look step that spans it into two advances. Source: `BalanceTraceRunner.RecordCheckpoints`.");
            sb.AppendLine("- **Provisional.** Section 7.1 defines the pin for policy r2 including the 25 % held mining click. Neither exists before T341/F005, so this constant was found on the r1 reference policy without mining. T341 makes the one-time final pin on policy r2; until then 73 s is provisional and the ratio is orientation only.");
            sb.AppendLine();
            sb.AppendLine("## 3. Medians per strategy and run index");
            sb.AppendLine();
            sb.AppendLine("Columns: time to eligibility (median, min to max over the five offsets); first Gear (median); IP awarded at the prestige (median); Factory Value at run second 600, 900, 1,800, 3,600, 5,400, 7,200 (median); contracts delivered, commands issued in that run (the trace counts them cumulatively; the report shows the per-run difference) and store-blocked machine samples (medians).");
            foreach (StrategyDefinition strategy in Strategies.All)
            {
                List<StrategyHistory> hs = matrix[strategy.Id];
                sb.AppendLine();
                sb.AppendLine("### " + strategy.Id);
                sb.AppendLine();
                sb.AppendLine("Rule: " + strategy.Rule);
                sb.AppendLine();
                sb.AppendLine("| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | Contracts | Commands (this run) | Store-blocked |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
                for (int n = 1; n <= StrategyEnsemble.RunCount; n++)
                {
                    long median = StrategyEnsemble.MedianEligible(hs, n);
                    long min = long.MaxValue;
                    long max = -2;
                    bool anyNotReached = false;
                    foreach (StrategyHistory h in hs)
                    {
                        long v = h.EligibleAt(n);
                        if (v < 0) { anyNotReached = true; continue; }
                        min = Math.Min(min, v);
                        max = Math.Max(max, v);
                    }
                    string range = min == long.MaxValue ? "all not reached" : N(min) + " to " + (anyNotReached ? "not reached" : N(max));
                    sb.Append("| ").Append(n).Append(" | ").Append(Time(median)).Append(" | ").Append(range).Append(" | ");
                    sb.Append(Opt(MedianStat(hs, n, r => r.FirstGearAtSec < 0 ? (long?)null : r.FirstGearAtSec))).Append(" | ");
                    sb.Append(Opt(MedianStat(hs, n, r => r.EligibleAtSec < 0 ? (long?)null : r.IpAwarded))).Append(" | ");
                    foreach (int cp in BalanceTraceRunner.CheckpointSeconds)
                        sb.Append(Opt(StrategyEnsemble.MedianFactoryValue(hs, n, cp))).Append(" | ");
                    sb.Append(Opt(MedianStat(hs, n, r => Stat(r, "contracts_delivered")))).Append(" | ");
                    sb.Append(Opt(MedianCommands(hs, n))).Append(" | ");
                    sb.Append(Opt(MedianStat(hs, n, r => Stat(r, "store_blocked_samples")))).AppendLine(" |");
                }
            }

            AppendObservations(sb, matrix);

            sb.AppendLine();
            sb.AppendLine("## 5. Conservation");
            sb.AppendLine();
            int violations = 0;
            foreach (List<StrategyHistory> hs in matrix.Values)
                foreach (StrategyHistory h in hs) violations += h.Violations.Count;
            sb.AppendLine("Conservation violations recorded by the trace commands across all " + Strategies.All.Count * StrategyEnsemble.OffsetCount + " histories: **" + violations + "** (`TraceContext.Violations`).");

            AppendRuntime(sb, matrix);
            return sb.ToString();
        }

        private static void AppendObservations(StringBuilder sb, Dictionary<string, List<StrategyHistory>> matrix)
        {
            sb.AppendLine();
            sb.AppendLine("## 4. Observations (derived from section 3; no verdicts)");
            sb.AppendLine();
            List<StrategyHistory> chain = matrix[Strategies.Chain.Id];
            foreach (StrategyDefinition strategy in Strategies.All)
            {
                if (strategy.Id == Strategies.Chain.Id) continue;
                var parts = new List<string>();
                var notReached = new List<int>();
                for (int n = 1; n <= StrategyEnsemble.RunCount; n++)
                {
                    long m = StrategyEnsemble.MedianEligible(matrix[strategy.Id], n);
                    long c = StrategyEnsemble.MedianEligible(chain, n);
                    if (m < 0) { notReached.Add(n); continue; }
                    parts.Add("run " + n + ": " + ((double)m / c).ToString("0.00", CultureInfo.InvariantCulture) + "x");
                }
                string reached = parts.Count == 0 ? "no run reached the threshold" : "median time to eligibility relative to the chain, " + string.Join(", ", parts);
                string missing = notReached.Count == 0 ? "" : "; not reached (median) at run " + string.Join(", ", notReached);
                sb.AppendLine("- `" + strategy.Id + "`: " + reached + missing + ".");
            }
            List<StrategyHistory> contractsOnly = matrix[Strategies.ContractsOnly.Id];
            if (StrategyEnsemble.MedianEligible(contractsOnly, 1) < 0)
                sb.AppendLine("- `contracts-only` run 1 stalls: its median commands are " + Opt(MedianCommands(contractsOnly, 1)) + " and its store-blocked samples " + Opt(MedianStat(contractsOnly, 1, r => Stat(r, "store_blocked_samples"))) + " (section 3), and the Factory Value stays flat from the last checkpoints. With auto-sell off the first Miner only fills the store and no Cash arrives to buy a machine or storage; the existing contracts need Bars or Plates. This is the by-construction stall of balance-policy-r2.md section 8.3 (P4c), reported and not a defect of the harness.");
            sb.AppendLine("- Each ratio divides a strategy median by the chain median of the same run index (section 3). A ratio below 1.00x means the strategy reached the threshold earlier than the chain on this content and policy; it is reported, not judged (the single-lever gate is T341's, on policy r2).");
            if (StrategyEnsemble.MedianEligible(matrix[Strategies.Casual.Id], 1) >= 0 && StrategyEnsemble.MedianEligible(matrix[Strategies.Casual.Id], 2) < 0)
                sb.AppendLine("- `casual` run 2 is slower than run 1 within the same history (Factory Value at second 1,800 and the first Gear time in section 3), even after the IP of run 1 was spent. The cause is unexplained and probably an artifact of the r1 policy at long look intervals. T341 diagnoses it, and T357 treats it as a suspected artifact, not as a finding about the game.");
            sb.AppendLine("- A run that is not reached ends the history: the later run indices of that strategy cannot be played and show `-` in section 3.");
        }

        private static void AppendRuntime(StringBuilder sb, Dictionary<string, List<StrategyHistory>> matrix)
        {
            sb.AppendLine();
            sb.AppendLine("## 6. Runtime and the 9-strategy projection (PF-15) - r1 projection, to be re-measured on policy r2 in T341");
            sb.AppendLine();
            sb.AppendLine("Wall-clock milliseconds of one history (runs 1 to 5, prestige and IP spending included) per strategy and offset, measured with `Stopwatch` around `BalanceTraceRunner.PlayRun` in a single-threaded Unity Edit Mode run on this workstation. Load on the machine changes these numbers; the figures are an order of magnitude, not a benchmark.");
            sb.AppendLine();
            sb.AppendLine("| Strategy | Offset 1 | Offset 2 | Offset 3 | Offset 4 | Offset 5 | Strategy total | Run-plays | ms per run-play |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            long grandMs = 0;
            long grandPlays = 0;
            double worstPerPlay = 0;
            foreach (StrategyDefinition strategy in Strategies.All)
            {
                long total = 0;
                long plays = 0;
                var cells = new StringBuilder();
                foreach (StrategyHistory h in matrix[strategy.Id])
                {
                    long ms = 0;
                    foreach (long r in h.RunMilliseconds) ms += r;
                    total += ms;
                    plays += h.RunMilliseconds.Count;
                    cells.Append(N(ms)).Append(" ms (offset ").Append(h.OffsetSec).Append(" s, ").Append(h.RunMilliseconds.Count).Append(" runs) | ");
                }
                double perPlay = plays == 0 ? 0 : (double)total / plays;
                worstPerPlay = Math.Max(worstPerPlay, perPlay);
                grandMs += total;
                grandPlays += plays;
                sb.AppendLine("| " + strategy.Id + " | " + cells + N(total) + " ms | " + plays + " | " + perPlay.ToString("0", CultureInfo.InvariantCulture) + " |");
            }

            double worstMsPerSimSecond = 0;
            foreach (List<StrategyHistory> hs in matrix.Values)
            {
                foreach (StrategyHistory h in hs)
                {
                    long ms = 0;
                    long simSeconds = 0;
                    for (int i = 0; i < h.Runs.Count; i++)
                    {
                        ms += h.RunMilliseconds[i];
                        RunRecord r = h.Runs[i];
                        simSeconds += r.PrestigedAtSec >= 0 ? r.PrestigedAtSec : (r.EligibleAtSec >= 0 ? r.EligibleAtSec : BalanceTraceRunner.CapSeconds);
                    }
                    if (simSeconds > 0) worstMsPerSimSecond = Math.Max(worstMsPerSimSecond, (double)ms / simSeconds);
                }
            }

            double meanPerPlay = grandPlays == 0 ? 0 : (double)grandMs / grandPlays;
            double measuredMinutes = grandMs / 60000.0;
            int plays9 = ProjectedStrategies * StrategyEnsemble.OffsetCount * StrategyEnsemble.RunCount;
            int plays12 = ProjectedDefinitions * StrategyEnsemble.OffsetCount * StrategyEnsemble.RunCount;
            sb.AppendLine();
            sb.AppendLine("Measured: " + grandPlays + " run-plays (" + Strategies.All.Count + " strategies x 5 offsets x up to 5 runs) in " + (grandMs / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s = " + measuredMinutes.ToString("0.00", CultureInfo.InvariantCulture) + " min, mean " + meanPerPlay.ToString("0", CultureInfo.InvariantCulture) + " ms per run-play, worst strategy " + worstPerPlay.ToString("0", CultureInfo.InvariantCulture) + " ms per run-play.");
            sb.AppendLine();
            sb.AppendLine("| Projection (sequential, no memoisation, no parallelism) | Run-plays | Mean-based | Worst-strategy-based | Against the " + BudgetMinutes.ToString("0", CultureInfo.InvariantCulture) + " min budget |");
            sb.AppendLine("|---|---|---|---|---|");
            AppendProjection(sb, "9 FR-023 strategies x 5 offsets x 5 runs", plays9, meanPerPlay, worstPerPlay);
            AppendProjection(sb, "12 strategy definitions of section 8.1 x 5 offsets x 5 runs", plays12, meanPerPlay, worstPerPlay);
            double ceiling9 = plays9 * BalanceTraceRunner.CapSeconds * worstMsPerSimSecond / 60000.0;
            double ceiling12 = plays12 * BalanceTraceRunner.CapSeconds * worstMsPerSimSecond / 60000.0;
            sb.AppendLine("| Ceiling: every run-play runs to the 21,600 s limit at the highest measured cost per simulated second (" + worstMsPerSimSecond.ToString("0.000", CultureInfo.InvariantCulture) + " ms/s), 9 / 12 strategies | " + plays9 + " / " + plays12 + " | - | " + ceiling9.ToString("0.0", CultureInfo.InvariantCulture) + " / " + ceiling12.ToString("0.0", CultureInfo.InvariantCulture) + " min | " + (ceiling12 <= BudgetMinutes ? "within the budget" : "over the budget") + " |");
            sb.AppendLine();
            sb.AppendLine("Method: projected minutes = run-plays x measured ms per run-play / 60,000. The mean is over the five strategies measured here; the worst-strategy figure applies the slowest strategy's cost to every run-play. A strategy that never reaches the threshold plays to the 21,600 s limit and costs more than one that prestiges, so the projection leans on whether the unmeasured strategies (stop-at-bar, stop-at-plate, prestige-later, market-only chain, chain+mining, mining-only) look like the measured ones. Decision rule of `balance-policy-r2.md` section 8.1: if a projection exceeds the budget, memoise `T_fluid` and run histories in parallel first, then move only `mining-only` to an opt-in category; the offset count stays five.");
        }

        private static void AppendProjection(StringBuilder sb, string label, int plays, double meanMs, double worstMs)
        {
            double mean = plays * meanMs / 60000.0;
            double worst = plays * worstMs / 60000.0;
            string verdict = worst <= BudgetMinutes ? "within the budget" : mean <= BudgetMinutes ? "within the budget on the mean, over it on the worst-strategy figure" : "over the budget";
            sb.AppendLine("| " + label + " | " + plays + " | " + mean.ToString("0.0", CultureInfo.InvariantCulture) + " min | " + worst.ToString("0.0", CultureInfo.InvariantCulture) + " min | " + verdict + " |");
        }
    }
}
