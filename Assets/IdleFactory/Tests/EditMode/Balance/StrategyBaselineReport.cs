using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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
    /// Builds the orientation report of T341: the twelve strategy definitions of balance-policy-r2.md section 7 on the shipped content, five offsets,
    /// runs 1 to 5, medians per run index, the gate verdicts and the runtime measurement (PF-15). The report is written only on opt-in; no number
    /// in it is a band (the asserted gates are listed in section 4 with their verdicts).
    /// </summary>
    internal static class StrategyBaselineReport
    {
        public const double BudgetMinutes = 20.0;

        /// <summary>The casual ratio limit in parts per 1000 (0.4 = 1 / 2.5, the owner playtest of 2026-10-09).</summary>
        public const long CasualRatioMaxPermille = 400;

        /// <summary>
        /// The search of section 7.1 for a given content: the smallest integer look interval, from 40 s upward in steps of 1 s, whose median Factory
        /// Value at run second 900 of run 1 over the five casual offsets is at most 0.4 of the chain median. The casual candidate is the casual
        /// strategy of policy r2 (the held click at 25 % of the cap) with the candidate look interval.
        /// </summary>
        public static CasualCalibration FindCasualLookInterval(RunCatalog catalog, int startSeconds = 40, int limitSeconds = 400)
        {
            long chainFv = ChainFactoryValue900(catalog);
            var result = new CasualCalibration { ChainFactoryValue900 = chainFv };
            for (int look = startSeconds; look <= limitSeconds; look++)
            {
                long fv = CasualFactoryValue900(catalog, look);
                result.Tried.Add(look + " s: " + fv);
                if (fv * 1000 <= chainFv * CasualRatioMaxPermille)
                {
                    result.LookIntervalSeconds = look;
                    result.CasualFactoryValue900 = fv;
                    return result;
                }
            }
            throw new InvalidOperationException("No look interval up to " + limitSeconds + " s satisfies the casual ratio.");
        }

        public static long ChainFactoryValue900(RunCatalog catalog)
        {
            List<StrategyHistory> chain = StrategyEnsemble.PlayEnsemble(catalog, Strategies.Chain, 1, 900);
            return StrategyEnsemble.MedianFactoryValue(chain, 1, 900) ?? throw new InvalidOperationException("The chain has no Factory Value at second 900.");
        }

        /// <summary>The median Factory Value at run second 900 of run 1 of the casual strategy with look interval <paramref name="lookSeconds"/> (five proportional offsets).</summary>
        public static long CasualFactoryValue900(RunCatalog catalog, int lookSeconds)
        {
            int captured = lookSeconds;
            var candidate = new StrategyDefinition("casual-search", "calibration candidate",
                () => new ReferencePolicy
                {
                    Id = "casual-search",
                    LookIntervalSeconds = captured,
                    MiningAfterFirstMinerPpm = ReferencePolicy.CasualMiningFractionPpm,
                });
            List<StrategyHistory> casual = StrategyEnsemble.PlayEnsemble(catalog, candidate, 1, 900);
            return StrategyEnsemble.MedianFactoryValue(casual, 1, 900) ?? throw new InvalidOperationException("No casual Factory Value at second 900.");
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

        private static long Stat(RunRecord rec, string key) => rec.Stats.TryGetValue(key, out long value) ? value : 0;

        private static long? MedianStat(IReadOnlyList<StrategyHistory> histories, int runIndex, Func<RunRecord, long?> selector)
        {
            var values = new List<long?>();
            foreach (StrategyHistory h in histories)
                values.Add(runIndex <= h.Runs.Count ? selector(h.Runs[runIndex - 1]) : (long?)null);
            return StrategyEnsemble.MedianOfAvailable(values);
        }

        /// <summary>Median over the offsets of the cumulative time C(n) of section 8.3; null when fewer than three offsets prestiged run n.</summary>
        public static long? MedianCumulative(IReadOnlyList<StrategyHistory> histories, int runIndex)
        {
            var values = new List<long?>();
            foreach (StrategyHistory h in histories)
            {
                long c = h.CumulativeAt(runIndex);
                values.Add(c < 0 ? (long?)null : c);
            }
            return StrategyEnsemble.MedianOfAvailable(values);
        }

        /// <summary>Commands issued in run n (the trace counts them cumulatively over the runs of a history, so the report takes the difference).</summary>
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

        private static string Range(IReadOnlyList<StrategyHistory> hs, int n)
        {
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
            return min == long.MaxValue ? "all not reached" : N(min) + " to " + (anyNotReached ? "not reached" : N(max));
        }

        public static string Compose(BalanceMatrix matrix, IReadOnlyList<GateResult> gates, CasualCalibration calibration, string buildCommit, string contentPath)
        {
            RunCatalog catalog = matrix.Catalog;
            var sb = new StringBuilder();
            string nl = Environment.NewLine;
            void Line(string text = "") => sb.Append(text).Append(nl);
            Line("# Strategy baseline on content revision " + catalog.ContentRevision + ", policy r2 (T341, issue #406)");
            Line();
            Line("**Orientation only. No number in sections 3, 5 and 6 is a band.** The asserted gates are the FR-024 list of `balance-policy-r2.md` section 8.2; section 4 lists their verdicts on this content, with the content-tuned gates that stay transitional until `contentRevision` 3 (T345, owner decision 2026-10-22, #408). Every number is a measurement of the engine on the content named here, by `StrategyBaselineReport` (`Assets/IdleFactory/Tests/EditMode/Balance/`), played by `BalanceTraceFullTests` (category `BalanceFull`). The matrix, the casual search and this file are produced only on opt-in: `$env:IDLEFACTORY_WRITE_STRATEGY_BASELINE=1; scripts/test-unity.ps1 -Suite EditMode -Category BalanceFull`. This file replaces `strategy-baseline-r2.md` (policy r1, kept as history).");
            Line();
            Line("## 1. Inputs");
            Line();
            Line("| Item | Value | Source |");
            Line("|---|---|---|");
            Line("| Build commit | `" + buildCommit + "` | `git rev-parse` in the generator |");
            Line("| Content | `" + contentPath + "`, `contentRevision` " + catalog.ContentRevision + " (schema version 3, transitional revision 2 values) | loaded by `GameplayContentDispatcher.Load` |");
            Line("| Policy | reference policy r2 (`ReferencePolicy`, `PolicyMix`, `IndustryModel`): balanced mix, net-worth income, storage on a `StorageFull` blocker read on arrival, `Sell` surplus dump, auto-deliver, held mining click, bonus offers, cooldowns and production objectives, IP by fluid-model gain per IP | `balance-policy-r2.md` |");
            Line("| Strategies | " + matrix.Histories.Count + " definitions: " + string.Join(", ", Strategies.All.Where(s => matrix.Has(s.Id)).Select(s => "`" + s.Id + "`")) + " | `balance-policy-r2.md` section 7 |");
            Line("| Look grid | looks at run second `offset + k x L`; reference `L` = 20 s; away window 240 to 299 of every 300 s (idle-heavy: 120 to 299); casual `L` = " + ReferencePolicy.CasualLookIntervalSeconds + " s | sections 2, 7 |");
            Line("| Offsets | " + matrix.OffsetCount + " per strategy: 0, 4, 8, 12, 16 s (casual: `i x L_casual / 5`: " + string.Join(", ", StrategyEnsemble.Offsets(ReferencePolicy.CasualLookIntervalSeconds)) + " s) | section 8.1 |");
            Line("| Median | per run index over the offsets: third smallest, \"not reached\" ordered above every number; a `-` means fewer than 3 offsets have the value | `StrategyEnsemble.Median` |");
            Line("| Time to threshold | run second of the first grid tick (idle ticks included) at which the prestige preview reports eligible; limit 21,600 s of run time | `BalanceTraceRunner.PlayRun` |");
            Line("| Held mining click | `Mine` at every whole active run second after the first look: 100 % of the cap until the first Miner exists, afterwards 0 % (chain and most strategies), 25 % (casual), 100 % (chain+mining, mining-only) | section 6 |");
            Line();

            Line("## 2. Casual calibration (section 7.1)");
            Line();
            if (calibration == null)
            {
                Line("Not searched in this run.");
            }
            else
            {
                Line("`ReferencePolicy.CasualLookIntervalSeconds` = **" + ReferencePolicy.CasualLookIntervalSeconds + " s** [pinned, final]. The search on this build returns " + calibration.LookIntervalSeconds + " s (" +
                    (calibration.LookIntervalSeconds == ReferencePolicy.CasualLookIntervalSeconds ? "equal to the pin" : "different from the pin: the pin is not recalibrated") + ").");
                Line();
                Line("- Rule: the smallest integer `L`, searched from 40 s in steps of 1 s, for which the median over the five casual offsets of the Factory Value at run second 900 of run 1 is at most 0.4 (1 / 2.5, the owner playtest of 2026-10-09) of the chain median at the same second. Policy r2, the held click at 25 % of the cap, content revision 2 values.");
                Line("- Chain median Factory Value at second 900 (offsets 0, 4, 8, 12, 16): **" + N(calibration.ChainFactoryValue900) + "**.");
                Line("- Casual median at `L` = " + calibration.LookIntervalSeconds + " s: **" + N(calibration.CasualFactoryValue900) + "**; ratio **" + ((double)calibration.CasualFactoryValue900 / calibration.ChainFactoryValue900).ToString("0.000", CultureInfo.InvariantCulture) + "** (limit 0.400).");
                Line("- Candidates tried (L: casual median FV at 900 s): " + string.Join("; ", calibration.Tried) + ".");
                Line("- The search is not monotone (the ratio is a step function of the look phase), which is why the pin is the first integer that satisfies the rule and is never moved afterwards.");
            }
            Line();

            Line("## 3. Medians per strategy and run index");
            Line();
            Line("Columns (all medians over the offsets): time to eligibility (min to max beside it); first Gear; IP awarded at the prestige; Factory Value at run second 600, 900, 1,800, 3,600, 5,400 and 7,200 (a `-` where fewer than three offsets reached that second); net-worth growth per minute between second 900 and 1,800; cumulative play time C(n) to the prestige that completes run n (section 8.3); contracts delivered; reward-to-effective-value ratio per delivery (mean over the run's deliveries); share of income from contracts (contract Cash over contract Cash plus sales Cash); Ore mined by the held click and its share of income (mined Ore valued at its base price); machine-seconds held off by a contract (4.3); IP left unspent after the spending loop (5); commands issued in the run.");
            long orePrice = catalog.RunStart.ManualMining == null ? 0 : catalog.Resources[catalog.RunStart.ManualMining.ResourceId].BasePrice;
            foreach (StrategyDefinition strategy in Strategies.All)
            {
                if (!matrix.Has(strategy.Id)) continue;
                List<StrategyHistory> hs = matrix.Histories[strategy.Id];
                Line();
                Line("### " + strategy.Id);
                Line();
                Line("Rule: " + strategy.Rule);
                Line();
                Line("| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |");
                Line("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
                for (int n = 1; n <= matrix.Runs; n++)
                {
                    long median = StrategyEnsemble.MedianEligible(hs, n);
                    var row = new StringBuilder();
                    row.Append("| ").Append(n).Append(" | ").Append(Time(median)).Append(" | ").Append(Range(hs, n)).Append(" | ");
                    row.Append(Opt(MedianStat(hs, n, r => r.FirstGearAtSec < 0 ? (long?)null : r.FirstGearAtSec))).Append(" | ");
                    row.Append(Opt(MedianStat(hs, n, r => r.PrestigedAtSec < 0 ? (long?)null : r.IpAwarded))).Append(" | ");
                    foreach (int cp in new[] { 600, 900, 1_800, 3_600, 5_400, 7_200 }) row.Append(Opt(StrategyEnsemble.MedianFactoryValue(hs, n, cp))).Append(" | ");
                    row.Append(Opt(MedianStat(hs, n, r => r.CheckpointFactoryValue.TryGetValue(900, out long a) && r.CheckpointFactoryValue.TryGetValue(1_800, out long b) ? (b - a) / 15 : (long?)null))).Append(" | ");
                    row.Append(Opt(MedianCumulative(hs, n))).Append(" | ");
                    row.Append(Opt(MedianStat(hs, n, r => Stat(r, "contracts_delivered")))).Append(" | ");
                    long? ratio = MedianStat(hs, n, r =>
                    {
                        var deliveries = r.Deliveries.Where(d => !d.Production && d.EffectiveValue > 0).ToList();
                        return deliveries.Count == 0 ? (long?)null : (long)deliveries.Average(d => d.Reward * 1_000_000.0 / d.EffectiveValue);
                    });
                    row.Append(ratio.HasValue ? (ratio.Value / 1_000_000m).ToString("0.000", CultureInfo.InvariantCulture) : "-").Append(" | ");
                    row.Append(Share(hs, n, r => Stat(r, "contract_cash_reward"), r => Stat(r, "contract_cash_reward") + Stat(r, "sales_cash"))).Append(" | ");
                    row.Append(Opt(MedianStat(hs, n, r => Stat(r, "mine_ore_units")))).Append(" | ");
                    row.Append(Share(hs, n, r => Stat(r, "mine_ore_units") * orePrice, r => Stat(r, "contract_cash_reward") + Stat(r, "sales_cash"))).Append(" | ");
                    row.Append(Opt(MedianStat(hs, n, r => Stat(r, "held_off_machine_seconds")))).Append(" | ");
                    row.Append(Opt(MedianStat(hs, n, r => r.PrestigedAtSec < 0 ? (long?)null : Stat(r, "unspent_ip")))).Append(" | ");
                    row.Append(Opt(MedianCommands(hs, n))).Append(" |");
                    Line(row.ToString());
                }
            }
            Line();

            Line("## 4. Gates (balance-policy-r2.md section 8.2)");
            Line();
            Line("A gate marked **transitional** is content-tuned: it is implemented and measured, fails on the revision 2 values, and asserts from `contentRevision` 3 (`TraceGates.AssertsFromRevision`). Every other gate asserts now.");
            Line();
            Line("| Gate | Verdict | Status at revision " + catalog.ContentRevision + " | Measured |");
            Line("|---|---|---|---|");
            foreach (GateResult g in gates)
            {
                string status = g.Passed ? "asserted (passes)" : g.IsTransitionalMiss(catalog.ContentRevision) ? "**transitional** (reported, asserts at revision 3)" : "**asserted and failing**";
                Line("| `" + g.Id + "` " + g.Title + " | " + (g.Passed ? "pass" : "FAIL") + " | " + status + " | " + g.Measured.Replace("|", "/") + " |");
            }
            Line();
            AppendObservations(sb, matrix);
            AppendRuntime(sb, matrix);
            return sb.ToString();
        }

        private static string Share(IReadOnlyList<StrategyHistory> hs, int n, Func<RunRecord, long> numerator, Func<RunRecord, long> denominator)
        {
            var values = new List<long?>();
            foreach (StrategyHistory h in hs)
            {
                if (n > h.Runs.Count) { values.Add(null); continue; }
                long den = denominator(h.Runs[n - 1]);
                values.Add(den <= 0 ? (long?)null : numerator(h.Runs[n - 1]) * 10_000 / den);
            }
            long? median = StrategyEnsemble.MedianOfAvailable(values);
            return median.HasValue ? (median.Value / 100m).ToString("0.0", CultureInfo.InvariantCulture) + " %" : "-";
        }

        private static void AppendObservations(StringBuilder sb, BalanceMatrix matrix)
        {
            string nl = Environment.NewLine;
            void Line(string text = "") => sb.Append(text).Append(nl);
            Line("## 5. Orientation: ratios, warnings and reports (never gates)");
            Line();
            List<StrategyHistory> chain = matrix.Histories[Strategies.Chain.Id];
            Line("Median time to eligibility relative to the chain (strategy median over chain median of the same run index; below 1.00x means faster than the chain):");
            Line();
            Line("| Strategy | Run 1 | Run 2 | Run 3 | Run 4 | Run 5 |");
            Line("|---|---|---|---|---|---|");
            foreach (StrategyDefinition s in Strategies.All)
            {
                if (s.Id == Strategies.Chain.Id || !matrix.Has(s.Id)) continue;
                var cells = new List<string>();
                for (int n = 1; n <= matrix.Runs; n++)
                {
                    long m = StrategyEnsemble.MedianEligible(matrix.Histories[s.Id], n);
                    long c = StrategyEnsemble.MedianEligible(chain, n);
                    cells.Add(m < 0 ? "not reached" : c <= 0 ? "-" : ((decimal)m / c).ToString("0.00", CultureInfo.InvariantCulture) + "x");
                }
                Line("| `" + s.Id + "` | " + string.Join(" | ", cells) + " |");
            }
            Line();

            if (matrix.Has(Strategies.PrestigeLaterLow.Id) && matrix.Has(Strategies.PrestigeLaterHigh.Id))
            {
                Line("Cumulative play time C(n) to the prestige that completes run n (median over offsets) and the warning **W-later** (section 8.3):");
                Line();
                Line("| Strategy | C(1) | C(2) | C(3) | C(4) | C(5) |");
                Line("|---|---|---|---|---|---|");
                foreach (StrategyDefinition s in Strategies.IpRule)
                    Line("| `" + s.Id + "` | " + string.Join(" | ", Enumerable.Range(1, matrix.Runs).Select(n => Opt(MedianCumulative(matrix.Histories[s.Id], n)))) + " |");
                var later = new List<string>();
                for (int n = 1; n <= matrix.Runs; n++)
                {
                    long? c = MedianCumulative(chain, n);
                    foreach (StrategyDefinition s in new[] { Strategies.PrestigeLaterLow, Strategies.PrestigeLaterHigh })
                    {
                        long? l = MedianCumulative(matrix.Histories[s.Id], n);
                        if (c.HasValue && l.HasValue && l.Value < c.Value) later.Add("`" + s.Id + "` at n = " + n + " (" + N(l.Value) + " s against " + N(c.Value) + " s)");
                    }
                }
                Line();
                Line(later.Count == 0 ? "- W-later: not raised (waiting never beats prestiging at the threshold in cumulative time)." :
                    "- **W-later raised**: " + string.Join("; ", later) + ". Waiting beats prestiging at the threshold, which weakens the meaning of the threshold; the owner reads this at the FR-027 approval.");
            }

            var marketWarn = new List<string>();
            if (matrix.Has(Strategies.MarketOnlyChain.Id))
                for (int n = 1; n <= matrix.Runs; n++)
                {
                    long mo = StrategyEnsemble.MedianEligible(matrix.Histories[Strategies.MarketOnlyChain.Id], n);
                    long c = StrategyEnsemble.MedianEligible(chain, n);
                    if (mo >= 0 && c >= 0 && mo < c) marketWarn.Add("run " + n + " (" + N(mo) + " s against " + N(c) + " s)");
                }
            Line(marketWarn.Count == 0 ? "- W-market (P4a): not raised (contracts never cost the chain time against the market-only chain)." :
                "- **W-market raised**: `market-only-chain` is faster than the chain at " + string.Join(", ", marketWarn) + ": contracts cost time.");

            if (matrix.Has(Strategies.MiningOnly.Id))
            {
                var mining = new List<string>();
                for (int n = 1; n <= matrix.Runs; n++)
                {
                    long mo = StrategyEnsemble.MedianEligible(matrix.Histories[Strategies.MiningOnly.Id], n);
                    long c = StrategyEnsemble.MedianEligible(chain, n);
                    if (mo >= 0) mining.Add("run " + n + (c < 0 || mo < c ? " is faster than the chain" : " reaches the threshold"));
                }
                List<StrategyHistory> mo1 = matrix.Histories[Strategies.MiningOnly.Id];
                long ore = mo1.Sum(h => Stat(h.Runs[0], "mine_ore_units")) / Math.Max(1, mo1.Count);
                long best = mo1.Max(h => h.Runs[0].CheckpointFactoryValue.Values.DefaultIfEmpty(0).Max());
                Line(mining.Count == 0 ? "- W-mining (P4b): not raised. `mining-only` never reaches a threshold within 21,600 s (highest Factory Value read at a checkpoint " + N(best) + "); it mined " + N(ore) + " Ore per offset in run 1." :
                    "- **W-mining raised (D-P dominance concern)**: `mining-only` " + string.Join(", ", mining) + ".");
            }

            if (matrix.Has(Strategies.ContractsOnly.Id))
            {
                List<StrategyHistory> co = matrix.Histories[Strategies.ContractsOnly.Id];
                long reached = StrategyEnsemble.MedianEligible(co, 1);
                long fv = StrategyEnsemble.MedianFactoryValue(co, 1, 1_800) ?? -1;
                long stallSec = co.Count == 0 ? -1 : co.Min(h => LastChange(h.Runs[0]));
                Line("- P4c, contracts-only: run 1 " + Time(reached) + " (Factory Value at 1,800 s: " + (fv < 0 ? "-" : N(fv)) + "). It stalls by construction in run 1; the last checkpoint at which its Factory Value still changed is run second " + (stallSec < 0 ? "-" : N(stallSec)) +
                    ": auto-sell is off, the starting Cash buys one Miner, and every contract needs Bars, Plates or Gears. Its income is 100 % contracts by definition (0 Cash per minute). No bootstrap exception is applied: none was decided (open question for the owner).");
            }

            AppendCompletions(sb, matrix);
            var zeroGain = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<StrategyHistory>> pair in matrix.Histories)
                foreach (StrategyHistory h in pair.Value)
                    foreach (RunRecord r in h.Runs)
                        foreach (string key in r.Stats.Keys)
                            if (key.StartsWith("industry_zero_gain.", StringComparison.Ordinal))
                            {
                                string upgrade = key.Substring("industry_zero_gain.".Length);
                                if (!zeroGain.TryGetValue(upgrade, out SortedSet<string> set)) zeroGain[upgrade] = set = new SortedSet<string>(StringComparer.Ordinal);
                                set.Add(pair.Key);
                            }
            Line("- Unspent IP and zero-gain levels (section 5): an affordable level whose fluid-model gain is not positive is never bought and is recorded (`industry_zero_gain.<id>`). " +
                (zeroGain.Count == 0 ? "None was recorded in this matrix." : "Recorded in this matrix: " + string.Join("; ", zeroGain.Select(p => "`" + p.Key + "` (" + string.Join(", ", p.Value) + ")")) + ".") +
                " The model never values `industry.warehouse` (capacity has no effect in a model without a storage limit), which is the stated design. Medians of the unspent IP are in section 3.");
            Line("- Mining income share, MIN-9 (sections 3 and 4): `chain+mining` holds the click at the whole cap in every active second; its advantage over the chain per run index is in the `chain-plus-mining` gate.");
            Line("- Reward-to-effective-value ratio per delivery: see the `ratio-bounds` gate (observed range over every delivery in the matrix).");
            Line();
        }

        /// <summary>
        /// The completions the reference policy reaches per run: the measurement that #387 asks for to settle <c>perRunCap</c> (total or per Item) and the
        /// reachable bound of <c>contract.quantity.capacity</c>. A completion index is the number of completions of one offer in one run.
        /// </summary>
        private static void AppendCompletions(StringBuilder sb, BalanceMatrix matrix)
        {
            string nl = Environment.NewLine;
            void Line(string text = "") => sb.Append(text).Append(nl);
            RunCatalog catalog = matrix.Catalog;
            List<StrategyHistory> chain = matrix.Histories[Strategies.Chain.Id];
            long topCapacity = catalog.StorageLevels[catalog.StorageLevels.Count - 1].Capacity;
            Line();
            Line("Measured completions per run (the basis for `perRunCap` and the reachable bound of `contract.quantity.capacity`, #387 (a) and (d)); median of the chain over the offsets, and the highest count of any strategy, offset and run. The required units at the highest count are `quantity[count]` of the offer; the top storage capacity is " + N(topCapacity) + ".");
            Line();
            Line("| Offer | Chain run 1 | Run 2 | Run 3 | Run 4 | Run 5 | Highest in the matrix | Required units there |");
            Line("|---|---|---|---|---|---|---|---|");
            var ids = new List<string>(catalog.ContractOffers.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                ContractOfferDef offer = catalog.ContractOffers[id];
                string key = (offer.IsProduction ? "produced." : "delivered.") + id;
                long highest = 0;
                foreach (StrategyHistory h in matrix.All())
                    foreach (RunRecord r in h.Runs) highest = Math.Max(highest, Stat(r, key));
                var cells = new List<string>();
                for (int n = 1; n <= matrix.Runs; n++) cells.Add(Opt(MedianStat(chain, n, r => Stat(r, key))));
                string required = highest < offer.QuantityTable.Count ? N(offer.QuantityTable[(int)highest]) : "-";
                Line("| `" + id + "` | " + string.Join(" | ", cells) + " | " + N(highest) + " | " + required + " |");
            }
            Line();
            Line("Items granted per run (one Item per tier in the shipped content): chain median tier 1 / tier 2 / tier 3, and the highest of any run: " +
                string.Join("; ", Enumerable.Range(1, matrix.Runs).Select(n => "run " + n + " " + Opt(MedianStat(chain, n, r => Stat(r, "items_granted.tier1"))) + "/" +
                    Opt(MedianStat(chain, n, r => Stat(r, "items_granted.tier2"))) + "/" + Opt(MedianStat(chain, n, r => Stat(r, "items_granted.tier3"))))) +
                "; highest " + string.Join("/", new[] { 1, 2, 3 }.Select(t => N(matrix.All().SelectMany(h => h.Runs).Max(r => Stat(r, "items_granted.tier" + t))))) +
                ". The ladder has " + catalog.ContractItemLadder.Entries.Count + " entries and `perRunCap` " + catalog.ContractItemLadder.PerRunCap + ".");
        }

        /// <summary>The run second of the last checkpoint at which the run's Factory Value still changed (the stall point of a run that never reaches its threshold).</summary>
        private static long LastChange(RunRecord run)
        {
            long previous = -1;
            long stall = -1;
            foreach (KeyValuePair<int, long> pair in run.CheckpointFactoryValue)
            {
                if (pair.Value != previous) stall = pair.Key;
                previous = pair.Value;
            }
            return stall;
        }

        private static void AppendRuntime(StringBuilder sb, BalanceMatrix matrix)
        {
            string nl = Environment.NewLine;
            void Line(string text = "") => sb.Append(text).Append(nl);
            Line("## 6. Runtime and the projection of the full matrix (PF-15), measured on policy r2");
            Line();
            Line("Wall-clock milliseconds of one history (runs 1 to 5, prestige and IP spending included) per strategy and offset, measured with `Stopwatch` around `BalanceTraceRunner.PlayRun` in a single-threaded Unity Edit Mode run on this workstation (`IndustryModel` memoised on its key; histories not parallelised). Load on the machine changes these numbers; the figures are an order of magnitude, not a benchmark.");
            Line();
            Line("| Strategy | Offset 1 | Offset 2 | Offset 3 | Offset 4 | Offset 5 | Strategy total | Run-plays | ms per run-play |");
            Line("|---|---|---|---|---|---|---|---|---|");
            long grandMs = 0;
            long grandPlays = 0;
            double worstPerPlay = 0;
            string worstStrategy = "";
            foreach (StrategyDefinition strategy in Strategies.All)
            {
                if (!matrix.Has(strategy.Id)) continue;
                long total = 0;
                long plays = 0;
                var cells = new StringBuilder();
                foreach (StrategyHistory h in matrix.Histories[strategy.Id])
                {
                    long ms = 0;
                    foreach (long r in h.RunMilliseconds) ms += r;
                    total += ms;
                    plays += h.RunMilliseconds.Count;
                    cells.Append(N(ms)).Append(" ms (offset ").Append(h.OffsetSec).Append(" s, ").Append(h.RunMilliseconds.Count).Append(" runs) | ");
                }
                double perPlay = plays == 0 ? 0 : (double)total / plays;
                if (perPlay > worstPerPlay) { worstPerPlay = perPlay; worstStrategy = strategy.Id; }
                grandMs += total;
                grandPlays += plays;
                Line("| " + strategy.Id + " | " + cells + N(total) + " ms | " + plays + " | " + perPlay.ToString("0", CultureInfo.InvariantCulture) + " |");
            }

            double worstMsPerSimSecond = 0;
            foreach (List<StrategyHistory> hs in matrix.Histories.Values)
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
            int definitions = Strategies.All.Count;
            int playsFull = definitions * StrategyEnsemble.OffsetCount * StrategyEnsemble.RunCount;
            Line();
            Line("Measured: " + grandPlays + " run-plays (" + matrix.Histories.Count + " strategies x " + matrix.OffsetCount + " offsets x up to " + matrix.Runs + " runs) in " + (grandMs / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s of history time = " + (grandMs / 60000.0).ToString("0.00", CultureInfo.InvariantCulture) +
                " min, mean " + meanPerPlay.ToString("0", CultureInfo.InvariantCulture) + " ms per run-play, worst strategy `" + worstStrategy + "` " + worstPerPlay.ToString("0", CultureInfo.InvariantCulture) + " ms per run-play. Matrix wall time: " +
                (matrix.WallMilliseconds / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s. `IndustryModel`: " + N(IndustryModel.Evaluations) + " evaluations computed, " + N(IndustryModel.MemoHits) + " served from the memo.");
            Line();
            Line("| Projection of the full matrix (" + definitions + " definitions x 5 offsets x 5 runs = " + playsFull + " run-plays; sequential) | Mean-based | Worst-strategy-based | Against the " + BudgetMinutes.ToString("0", CultureInfo.InvariantCulture) + " min budget |");
            Line("|---|---|---|---|");
            double mean = playsFull * meanPerPlay / 60000.0;
            double worst = playsFull * worstPerPlay / 60000.0;
            double ceiling = playsFull * BalanceTraceRunner.CapSeconds * worstMsPerSimSecond / 60000.0;
            Line("| Projected from this run | " + mean.ToString("0.0", CultureInfo.InvariantCulture) + " min | " + worst.ToString("0.0", CultureInfo.InvariantCulture) + " min | " + (worst <= BudgetMinutes ? "within the budget" : "over the budget") + " |");
            Line("| Ceiling: every run-play runs to the 21,600 s limit at the highest measured cost per simulated second (" + worstMsPerSimSecond.ToString("0.000", CultureInfo.InvariantCulture) + " ms/s) | - | " + ceiling.ToString("0.0", CultureInfo.InvariantCulture) + " min | " + (ceiling <= BudgetMinutes ? "within the budget" : "over the budget") + " |");
            Line();
            Line("Decision rule of `balance-policy-r2.md` section 8.1: if a projection exceeds the budget, memoise `T_fluid` and run histories in parallel first, then move only `mining-only` to an opt-in category; the offset count stays five and no FR-023 strategy is moved. The r1 projection of T311 (`strategy-baseline-r2.md` section 6) was 2.2 min mean and 3.5 min worst for the same 300 run-plays on five measured strategies.");
        }
    }
}
