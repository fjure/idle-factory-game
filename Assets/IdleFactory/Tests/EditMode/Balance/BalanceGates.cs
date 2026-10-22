using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Unity.Content;

namespace IdleFactory.Tests.EditMode.Balance
{
    /// <summary>
    /// The verdict of one asserted trace gate of FR-024 (balance-policy-r2.md section 8.2). A gate that content values decide
    /// (<see cref="ContentTuned"/>) is reported, not asserted, until <c>contentRevision</c> 3 (owner decision 2026-10-22, #408; ECO-4 and
    /// balance-policy-r2.md section 9): T345 tunes revision 3, and from then on every gate asserts. A gate that fails for any other reason is a
    /// defect of the policy or the engine and always asserts.
    /// </summary>
    internal sealed class GateResult
    {
        public string Id;
        public string Title;
        public string Rule;
        public string Measured;
        public bool Passed;

        /// <summary>True for a gate whose outcome depends on content values that T345 tunes (ratio bounds, Item uptime, chain dominance, single lever).</summary>
        public bool ContentTuned;

        /// <summary>True when this gate fails the test at <paramref name="contentRevision"/>: it failed and is not a transitional content gate.</summary>
        public bool IsAsserted(int contentRevision) => !ContentTuned || contentRevision >= TraceGates.AssertsFromRevision;

        public bool Fails(int contentRevision) => !Passed && IsAsserted(contentRevision);

        public bool IsTransitionalMiss(int contentRevision) => !Passed && !IsAsserted(contentRevision);

        public override string ToString() => (Passed ? "PASS " : "FAIL ") + Id + ": " + Measured;
    }

    /// <summary>The played strategies of one matrix (a subset of <see cref="Strategies.All"/>, some offsets, some runs).</summary>
    internal sealed class BalanceMatrix
    {
        public RunCatalog Catalog;
        public int Runs;
        public int OffsetCount;
        public long WallMilliseconds;
        public readonly Dictionary<string, List<StrategyHistory>> Histories = new Dictionary<string, List<StrategyHistory>>(StringComparer.Ordinal);

        public bool Has(string id) => Histories.ContainsKey(id);

        public IEnumerable<StrategyHistory> All() => Histories.Values.SelectMany(h => h);

        public long Median(string id, int runIndex) => StrategyEnsemble.MedianEligible(Histories[id], runIndex);

        /// <summary>Plays the strategies for runs 1 to <paramref name="runs"/> over the first <paramref name="offsetCount"/> of their five look-grid offsets.</summary>
        public static BalanceMatrix Play(RunCatalog catalog, IEnumerable<StrategyDefinition> strategies, int offsetCount, int runs)
        {
            var matrix = new BalanceMatrix { Catalog = catalog, Runs = runs, OffsetCount = offsetCount };
            var clock = System.Diagnostics.Stopwatch.StartNew();
            foreach (StrategyDefinition strategy in strategies)
            {
                var histories = new List<StrategyHistory>();
                int[] offsets = StrategyEnsemble.Offsets(strategy.Create().LookIntervalSeconds);
                for (int i = 0; i < offsetCount; i++) histories.Add(StrategyEnsemble.PlayHistory(catalog, strategy, offsets[i], runs));
                matrix.Histories[strategy.Id] = histories;
            }
            clock.Stop();
            matrix.WallMilliseconds = clock.ElapsedMilliseconds;
            return matrix;
        }
    }

    /// <summary>The rules of the content validator that content revision 3 turns on, applied to the shipped values read as revision 3 (what T345 must fix).</summary>
    internal sealed class StaticRules
    {
        public readonly List<ContentDiagnostic> Diagnostics = new List<ContentDiagnostic>();

        public int Count(string code) => Diagnostics.Count(d => d.Code == code);

        public string Describe(string code) =>
            string.Join(", ", Diagnostics.Where(d => d.Code == code).Select(d => d.EntryId ?? d.Path).Distinct().OrderBy(x => x, StringComparer.Ordinal));

        /// <summary>Reads the content with <c>contentRevision</c> replaced by 3 and validates it. The values are not changed, only the revision the gated rules key on.</summary>
        public static StaticRules ReadAsRevision3(byte[] contentBytes)
        {
            string json = new UTF8Encoding(false, true).GetString(contentBytes);
            string patched = System.Text.RegularExpressions.Regex.Replace(json, "\"contentRevision\"\\s*:\\s*\\d+", "\"contentRevision\": 3");
            var rules = new StaticRules();
            RunContentReadResult read = RunContentReader.Read(new UTF8Encoding(false).GetBytes(patched));
            if (!read.IsSuccess)
            {
                rules.Diagnostics.Add(read.Diagnostic);
                return rules;
            }
            rules.Diagnostics.AddRange(RunContentValidator.Validate(read.Document));
            return rules;
        }
    }

    /// <summary>The asserted gates of FR-024 and the F5-R20 / FR-021 additions (balance-policy-r2.md section 8.2), evaluated over a matrix.</summary>
    internal static class TraceGates
    {
        /// <summary>From this <c>contentRevision</c> on every gate asserts (ECO-4, balance-policy-r2.md section 9).</summary>
        public const int AssertsFromRevision = 3;

        /// <summary>The ids of the gates whose outcome depends on content values and that stay transitional at revision 2.</summary>
        public static readonly string[] ContentTunedIds = { "ratio-bounds", "item-uptime", "chain-dominance", "single-lever" };

        private static string N(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

        private static string T(long seconds) => seconds < 0 ? "not reached" : N(seconds) + " s";

        public static List<GateResult> Evaluate(BalanceMatrix m, byte[] contentBytes)
        {
            var list = new List<GateResult>();
            StaticRules rules = StaticRules.ReadAsRevision3(contentBytes);
            list.Add(Conservation(m));
            list.Add(SaleNeutrality(m));
            list.Add(PartitionInvariance(m.Catalog));
            list.Add(RemoveRebuild(m.Catalog));
            list.Add(Eco3(m));
            list.Add(RatioBounds(m));
            list.Add(ItemUptime(m, rules));
            list.Add(StartAffordability(m));
            list.Add(ChainDominance(m.Catalog, rules));
            list.Add(SingleLever(m));
            list.Add(ContractsOnlyLonger(m));
            list.Add(ChainWithinSixHours(m));
            if (Strategies.IpRule.All(s => m.Has(s.Id))) list.Add(IpRule(m));
            if (m.Has(Strategies.ChainPlusMining.Id)) list.Add(ChainPlusMining(m));
            return list;
        }

        // ---- conservation and sale neutrality ----------------------------------------------------------------

        public static GateResult Conservation(BalanceMatrix m)
        {
            int histories = 0;
            var violations = new List<string>();
            foreach (KeyValuePair<string, List<StrategyHistory>> pair in m.Histories)
                foreach (StrategyHistory h in pair.Value)
                {
                    histories++;
                    foreach (string v in h.Violations) violations.Add(pair.Key + "@" + h.OffsetSec + ": " + v);
                }
            return new GateResult
            {
                Id = "conservation",
                Title = "Conservation and exact per-command deltas",
                Rule = "No command and no advance changes Cash, stock, RP, IP or Items other than by its exact expected delta (FR-024, SC-001).",
                Passed = violations.Count == 0,
                Measured = violations.Count == 0 ? "0 violations over " + histories + " histories" : violations.Count + " violations, first: " + violations[0],
            };
        }

        public static GateResult SaleNeutrality(BalanceMatrix m)
        {
            long checkedSells = 0;
            long lost = 0;
            long unchecked_ = 0;
            long badSells = 0;
            foreach (StrategyHistory h in m.All())
            {
                foreach (RunRecord r in h.Runs)
                {
                    checkedSells += Stat(r, "sell_fv_checked");
                    lost += Stat(r, "sell_fv_loss_1");
                    unchecked_ += Stat(r, "sell_fv_unchecked");
                }
                badSells += h.Violations.Count(v => v.StartsWith("sell ", StringComparison.Ordinal));
            }
            return new GateResult
            {
                Id = "sale-neutrality",
                Title = "Sale value neutrality (FR-002)",
                Rule = "A Sell never raises Factory Value; the single floor loses at most 1 (0 at an identity sale price). Sells whose settle can produce (a machine waits for room) are not checked.",
                Passed = badSells == 0,
                Measured = N(checkedSells) + " checked Sells (" + N(lost) + " lost 1 to the floor, " + N(badSells) + " out of rule), " + N(unchecked_) + " unchecked",
            };
        }

        // ---- partition invariance and remove/rebuild ------------------------------------------------------------

        /// <summary>SC-003: the same history with every advance cut into pieces of 7 s (and, with the held click, 3 s) ends in the identical state.</summary>
        public static GateResult PartitionInvariance(RunCatalog catalog)
        {
            var problems = new List<string>();
            long deliveries = 0;
            foreach (KeyValuePair<StrategyDefinition, int> pair in new[]
            {
                new KeyValuePair<StrategyDefinition, int>(Strategies.Chain, 7),
                new KeyValuePair<StrategyDefinition, int>(Strategies.ChainPlusMining, 3),
            })
            {
                var plain = new BalanceTraceRunner(catalog, pair.Key.Create());
                var cut = new BalanceTraceRunner(catalog, pair.Key.Create()) { AdvancePartitionSeconds = pair.Value };
                RunRecord a = plain.PlayRun("p", 1, false, -1, null, 0, 1_500);
                RunRecord b = cut.PlayRun("p", 1, false, -1, null, 0, 1_500);
                string label = pair.Key.Id + " (" + pair.Value + " s pieces)";
                if (!a.Rows.Select(r => string.Join(",", r)).SequenceEqual(b.Rows.Select(r => string.Join(",", r)))) problems.Add(label + ": rows differ");
                if (!a.Stats.SequenceEqual(b.Stats)) problems.Add(label + ": statistics differ");
                if (plain.Context.Session.State.Cash != cut.Context.Session.State.Cash) problems.Add(label + ": Cash differs");
                if (plain.Context.Preview().Value.FactoryValue.Total != cut.Context.Preview().Value.FactoryValue.Total) problems.Add(label + ": Factory Value differs");
                if (!a.Deliveries.Select(d => d.OfferId + ":" + d.Reward + ":" + d.EffectiveValue).SequenceEqual(b.Deliveries.Select(d => d.OfferId + ":" + d.Reward + ":" + d.EffectiveValue)))
                    problems.Add(label + ": deliveries differ");
                deliveries += a.Deliveries.Count;
                problems.AddRange(cut.Context.Violations);
            }
            return new GateResult
            {
                Id = "partition-invariance",
                Title = "Auto-deliver partition invariance (SC-003)",
                Rule = "Replaying 1,500 s with the advances cut into pieces gives the same rows, statistics, Cash and Factory Value.",
                Passed = problems.Count == 0,
                Measured = problems.Count == 0 ? "identical state for 2 strategies, " + deliveries + " contract completions compared" : string.Join("; ", problems),
            };
        }

        /// <summary>ECO-7 (FR-015): removing a machine and building the same type again never raises Cash plus Factory Value.</summary>
        public static GateResult RemoveRebuild(RunCatalog catalog)
        {
            var problems = new List<string>();
            int probes = 0;
            long worst = long.MinValue;
            var runner = new BalanceTraceRunner(catalog, Strategies.Chain.Create());
            runner.PlayRun("e7", 1, false, -1, null, 0, 1_800);
            foreach (MachineInstance m in new List<MachineInstance>(runner.Context.Session.State.Machines))
            {
                if (!runner.Context.RemoveAndRebuild(m.InstanceId, out long before, out long after)) { problems.Add(m.MachineId + ": not replaced"); continue; }
                probes++;
                worst = Math.Max(worst, after - before);
                if (after > before) problems.Add(m.MachineId + ": " + before + " -> " + after);
            }
            problems.AddRange(runner.Context.Violations);
            return new GateResult
            {
                Id = "remove-rebuild",
                Title = "Remove/rebuild (ECO-7)",
                Rule = "Remove then Build of the same type never raises Factory Value (which contains Cash).",
                Passed = problems.Count == 0 && probes > 0,
                Measured = problems.Count == 0 ? probes + " machines replaced, largest change " + worst : string.Join("; ", problems),
            };
        }

        // ---- ECO-3 ------------------------------------------------------------------------------------------------

        /// <summary>Restated ECO-3 (FR-021) on the chain medians, with a tolerance of one look interval.</summary>
        public static GateResult Eco3(BalanceMatrix m)
        {
            long look = ReferencePolicy.EpochSeconds;
            var medians = new long[m.Runs + 1];
            var parts = new List<string>();
            for (int n = 1; n <= m.Runs; n++)
            {
                medians[n] = m.Median(Strategies.Chain.Id, n);
                parts.Add(T(medians[n]));
            }
            var problems = new List<string>();
            for (int n = 1; n <= m.Runs; n++) if (medians[n] < 0) problems.Add("run " + n + " not reached");
            if (problems.Count == 0)
            {
                for (int n = 1; n < m.Runs; n++)
                    if (medians[n + 1] > medians[n] + look) problems.Add("T(" + (n + 1) + ") " + medians[n + 1] + " > T(" + n + ") + " + look + " = " + (medians[n] + look));
                for (int n = 2; n <= m.Runs; n++)
                    if (medians[n] * 4 < medians[1] - 4 * look) problems.Add("T(" + n + ") " + medians[n] + " < 0.25 x T(1) - " + look);
            }
            return new GateResult
            {
                Id = "eco-3",
                Title = "Restated ECO-3 (FR-021)",
                Rule = "Chain medians: T(n+1) <= T(n) + L for n = 1..4 and T(n) >= 0.25 x T(1) - L for n = 2..5, with L = " + look + " s.",
                Passed = problems.Count == 0,
                Measured = string.Join(" / ", parts) + (problems.Count == 0 ? "" : " (" + string.Join("; ", problems) + ")"),
            };
        }

        // ---- contract ratio bounds ---------------------------------------------------------------------------------

        private const long StandingMaxPpm = 1_500_000;
        private const long BonusMaxPpm = 2_000_000;

        /// <summary>
        /// The ratio bounds of FR-004 and D-D on every delivery in the trace, on base and on effective prices. Base prices: the configured ratio of
        /// the offer (the reward at an identity sale price) is at most 1.5 (standing) or between 1.0 and 2.0 (bonus). Effective prices: the Cash paid
        /// against the sale value of the delivered stock at the completion instant is between 1.0 and 1.5 (standing) or up to 2.0 (bonus), within one unit
        /// of rounding. A production objective has its own rule (<c>contract.ratio.produce</c>) and is not part of this gate.
        /// </summary>
        public static GateResult RatioBounds(BalanceMatrix m)
        {
            long deliveries = 0;
            long outBase = 0;
            long outEffective = 0;
            long minEffective = long.MaxValue;
            long maxEffective = 0;
            var offenders = new SortedSet<string>(StringComparer.Ordinal);
            foreach (StrategyHistory h in m.All())
                foreach (RunRecord r in h.Runs)
                    foreach (DeliveryRecord d in r.Deliveries)
                    {
                        if (d.Production) continue;
                        deliveries++;
                        ContractOfferDef offer = m.Catalog.ContractOffers[d.OfferId];
                        long max = d.Bonus ? BonusMaxPpm : StandingMaxPpm;
                        bool baseOk = offer.RewardRatioPpm <= max && offer.RewardRatioPpm >= 1_000_000;
                        long ceiling = (d.EffectiveValue * max + 999_999) / 1_000_000;
                        bool effectiveOk = d.Reward <= ceiling && d.Reward + 1 >= d.EffectiveValue;
                        if (!baseOk) { outBase++; offenders.Add(d.OfferId + " (ratio " + (offer.RewardRatioPpm / 1_000_000m).ToString("0.###", CultureInfo.InvariantCulture) + ")"); }
                        if (!effectiveOk) { outEffective++; offenders.Add(d.OfferId + " (effective)"); }
                        if (d.EffectiveValue > 0)
                        {
                            long ppm = d.Reward * 1_000_000L / d.EffectiveValue;
                            minEffective = Math.Min(minEffective, ppm);
                            maxEffective = Math.Max(maxEffective, ppm);
                        }
                    }
            bool passed = outBase == 0 && outEffective == 0;
            string range = deliveries == 0 ? "no deliveries" : "effective ratio " + (minEffective / 1_000_000m).ToString("0.000", CultureInfo.InvariantCulture) + " to " +
                (maxEffective / 1_000_000m).ToString("0.000", CultureInfo.InvariantCulture);
            return new GateResult
            {
                Id = "ratio-bounds",
                Title = "Contract ratio bounds on base and effective prices (FR-004, D-D)",
                Rule = "Standing delivery offers pay 1.0 to 1.5 of the market value, bonus offers 1.0 to 2.0 (the document's lower bound of more than 1.5 for a bonus offer is stricter than FR-004 and is reported, not gated).",
                ContentTuned = true,
                Passed = passed,
                Measured = N(deliveries) + " deliveries, " + range + "; " + N(outBase) + " beyond the configured bound, " + N(outEffective) + " beyond the effective bound" +
                    (offenders.Count == 0 ? "" : " (" + string.Join(", ", offenders) + ")"),
            };
        }

        // ---- Item uptime, chain dominance, start affordability --------------------------------------------------------

        public static GateResult ItemUptime(BalanceMatrix m, StaticRules rules)
        {
            long bound = Math.Min(m.Catalog.ContractItemLadder.PerRunCap, m.Catalog.ContractItemLadder.Entries.Count);
            long worst = 0;
            foreach (StrategyHistory h in m.All())
                foreach (RunRecord r in h.Runs)
                    worst = Math.Max(worst, Stat(r, "items_granted.tier1") + Stat(r, "items_granted.tier2") + Stat(r, "items_granted.tier3"));
            int staticMiss = rules.Count("ladder.uptime");
            return new GateResult
            {
                Id = "item-uptime",
                Title = "Item uptime (ECO-6, ladder.uptime)",
                Rule = "perRunCap x durationMs of every ladder Item stays within uptimeMaxPpm of minRunDurationMs, and no run is granted more than perRunCap Items.",
                ContentTuned = true,
                Passed = staticMiss == 0 && worst <= bound,
                Measured = "ladder.uptime at revision 3: " + (staticMiss == 0 ? "holds" : "fails for " + rules.Describe("ladder.uptime")) + "; most Items granted in one run " + worst + " (bound " + bound + ")",
            };
        }

        public static GateResult ChainDominance(RunCatalog catalog, StaticRules rules)
        {
            int miss = rules.Count("chain.dominance");
            return new GateResult
            {
                Id = "chain-dominance",
                Title = "Chain dominance per slot (ECO-12, chain.dominance)",
                Rule = "Output per slot-second does not fall along the chain and every terminal beats Ore-only by chainDominanceMarginPpm (" + catalog.ChainDominanceMarginPpm + ").",
                ContentTuned = true,
                Passed = miss == 0,
                Measured = miss == 0 ? "holds at revision 3" : miss + " diagnostics at revision 3 (" + rules.Describe("chain.dominance") + ")",
            };
        }

        /// <summary>FR-016, SC-011: at every run start the Cash plus what the held click mines in the start window pays the first Miner.</summary>
        public static GateResult StartAffordability(BalanceMatrix m)
        {
            ManualMiningDef mining = m.Catalog.RunStart.ManualMining;
            var problems = new List<string>();
            long minimum = long.MaxValue;
            foreach (StrategyHistory h in m.Histories[Strategies.Chain.Id])
            {
                for (int i = 0; i < h.Runs.Count; i++)
                {
                    long cash = Stat(h.Runs[i], "start_cash");
                    long price = Stat(h.Runs[i], "first_miner_price");
                    long window = 0;
                    if (mining != null)
                    {
                        long orePrice = m.Catalog.Resources[mining.ResourceId].BasePrice;
                        window = mining.StartWindowMs / 1000 * mining.MaxActivationsPerSecond * mining.BaseYieldMilli / 1000 * orePrice;
                    }
                    minimum = Math.Min(minimum, cash + window - price);
                    if (cash + window < price) problems.Add("offset " + h.OffsetSec + " run " + (i + 1) + ": " + cash + " + " + window + " < " + price);
                }
            }
            return new GateResult
            {
                Id = "start-affordability",
                Title = "Start affordability (FR-016, SC-011)",
                Rule = "Start Cash plus the Ore the held click mines in the start window buys the first Miner at every prestige index.",
                Passed = problems.Count == 0,
                Measured = problems.Count == 0 ? "reachable at every run start, smallest margin " + minimum + " Cash" : problems[0],
            };
        }

        // ---- single lever -----------------------------------------------------------------------------------------------

        private static bool Faster(long strategyMedian, long chainMedian)
        {
            if (strategyMedian < 0) return false;
            return chainMedian < 0 || strategyMedian < chainMedian;
        }

        /// <summary>No single-lever strategy (ore-only, stop-at-bar, stop-at-plate) is faster than the chain in any run index; no tolerance (ties are not faster).</summary>
        public static GateResult SingleLever(BalanceMatrix m)
        {
            var misses = new List<string>();
            var shares = new List<string>();
            foreach (StrategyDefinition s in Strategies.SingleLever)
            {
                if (s.Id == Strategies.ContractsOnly.Id || !m.Has(s.Id)) continue;
                var ratios = new List<string>();
                for (int n = 1; n <= m.Runs; n++)
                {
                    long sm = m.Median(s.Id, n);
                    long cm = m.Median(Strategies.Chain.Id, n);
                    if (Faster(sm, cm)) misses.Add(s.Id + " run " + n + ": " + N(sm) + " s vs chain " + N(cm) + " s");
                    ratios.Add(sm < 0 ? "-" : cm <= 0 ? "?" : ((decimal)sm / cm).ToString("0.00", CultureInfo.InvariantCulture));
                }
                shares.Add(s.Id + " " + string.Join("/", ratios));
            }
            return new GateResult
            {
                Id = "single-lever",
                Title = "No single-lever strategy faster than the chain (ECO-13)",
                Rule = "median(strategy, n) >= median(chain, n) for ore-only, stop-at-bar and stop-at-plate and n = 1..5, no tolerance.",
                ContentTuned = true,
                Passed = misses.Count == 0,
                Measured = misses.Count == 0 ? "none faster (" + string.Join("; ", shares) + ")" : misses.Count + " misses (" + string.Join("; ", shares) + ")",
            };
        }

        /// <summary>SC-002: contracts-only is strictly longer than the chain. By construction it stalls (balance-policy-r2.md section 8.3, P4c).</summary>
        public static GateResult ContractsOnlyLonger(BalanceMatrix m)
        {
            var problems = new List<string>();
            if (!m.Has(Strategies.ContractsOnly.Id)) return new GateResult { Id = "contracts-only-longer", Title = "Contracts-only strictly longer (SC-002)", Passed = true, Measured = "not played" };
            for (int n = 1; n <= m.Runs; n++)
            {
                long co = m.Median(Strategies.ContractsOnly.Id, n);
                long cm = m.Median(Strategies.Chain.Id, n);
                if (co >= 0 && (cm < 0 || co <= cm)) problems.Add("run " + n + ": " + co + " s vs chain " + cm + " s");
            }
            long stall = m.Median(Strategies.ContractsOnly.Id, 1);
            return new GateResult
            {
                Id = "contracts-only-longer",
                Title = "Contracts-only strictly longer than the chain (SC-002)",
                Rule = "median(contracts-only, n) > median(chain, n); not reached counts as longer.",
                Passed = problems.Count == 0,
                Measured = problems.Count == 0 ? "run 1 " + T(stall) : string.Join("; ", problems),
            };
        }

        // ---- reachability, the 4.0x rule and chain+mining --------------------------------------------------------------

        public static GateResult ChainWithinSixHours(BalanceMatrix m)
        {
            var problems = new List<string>();
            long slowest = 0;
            foreach (StrategyHistory h in m.Histories[Strategies.Chain.Id])
                for (int n = 1; n <= m.Runs; n++)
                {
                    long t = h.EligibleAt(n);
                    if (t < 0 || t > ReferencePolicy.RunLimitSeconds) problems.Add("offset " + h.OffsetSec + " run " + n + ": " + T(t));
                    else slowest = Math.Max(slowest, t);
                }
            return new GateResult
            {
                Id = "chain-within-6h",
                Title = "The chain reaches every threshold within 6 simulated hours",
                Rule = "Every offset and every run 1..5 of the chain reaches the threshold in at most " + N(ReferencePolicy.RunLimitSeconds) + " s.",
                Passed = problems.Count == 0,
                Measured = problems.Count == 0 ? "slowest " + N(slowest) + " s" : problems[0],
            };
        }

        /// <summary>The cumulative IP to bring every Industry upgrade to its practical-ceiling level (FR-021).</summary>
        public static long PracticalCeiling(RunCatalog catalog)
        {
            long total = 0;
            foreach (IndustryUpgradeDef def in catalog.IndustryUpgrades.Values)
            {
                if (def.Id == BalanceTraceReport.PriceVariantUpgradeId) continue;
                long level = def.PracticalCeilingLevel > 0 ? Math.Min(def.PracticalCeilingLevel, def.MaxLevel) : def.MaxLevel;
                for (int i = 0; i < level; i++) total += def.IpCostTable[i];
            }
            return total;
        }

        /// <summary>FR-021, ECO-9: the practical Industry ceiling is at least 4.0 times the highest IP awarded in runs 1..4 (all offsets; at-threshold and both prestige-later strategies).</summary>
        public static GateResult IpRule(BalanceMatrix m)
        {
            long highest = 0;
            string source = "";
            foreach (StrategyDefinition s in Strategies.IpRule)
                foreach (StrategyHistory h in m.Histories[s.Id])
                    for (int i = 0; i < Math.Min(4, h.Runs.Count); i++)
                    {
                        if (h.Runs[i].PrestigedAtSec < 0) continue;
                        if (h.Runs[i].IpAwarded > highest)
                        {
                            highest = h.Runs[i].IpAwarded;
                            source = s.Id + " run " + (i + 1) + " offset " + h.OffsetSec;
                        }
                    }
            long ceiling = PracticalCeiling(m.Catalog);
            return new GateResult
            {
                Id = "ip-4x",
                Title = "4.0x rule: practical Industry ceiling over the highest IP (FR-021, ECO-9)",
                Rule = "ceiling >= 4.0 x max IP over runs 1..4 across offsets and the strategies chain, prestige-later-1.5 and prestige-later-3.",
                Passed = ceiling * 10 >= highest * 40,
                Measured = "ceiling " + N(ceiling) + " IP, highest IP " + N(highest) + " (" + source + "), ratio " + (highest == 0 ? "-" : ((decimal)ceiling / highest).ToString("0.0", CultureInfo.InvariantCulture)),
            };
        }

        /// <summary>F5-R20, MIN-9: holding the click through the whole run is not faster than the chain by more than <c>miningAdvantageMaxPpm</c>.</summary>
        public static GateResult ChainPlusMining(BalanceMatrix m)
        {
            ManualMiningDef mining = m.Catalog.RunStart.ManualMining;
            long maxPpm = mining == null ? 0 : mining.MiningAdvantageMaxPpm;
            var problems = new List<string>();
            var advantage = new List<string>();
            for (int n = 1; n <= m.Runs; n++)
            {
                long chain = m.Median(Strategies.Chain.Id, n);
                long mined = m.Median(Strategies.ChainPlusMining.Id, n);
                if (chain < 0 || mined < 0) { advantage.Add("-"); continue; }
                long saved = chain - mined;
                long allowed = maxPpm * chain / 1_000_000;
                advantage.Add((saved * 1000 / Math.Max(chain, 1) / 10m).ToString("0.0", CultureInfo.InvariantCulture) + " %");
                if (saved > allowed) problems.Add("run " + n + ": " + saved + " s faster, allowed " + allowed + " s");
            }
            return new GateResult
            {
                Id = "chain-plus-mining",
                Title = "Chain+mining not faster than the chain by more than miningAdvantageMaxPpm (MIN-9)",
                Rule = "median(chain, n) - median(chain+mining, n) <= miningAdvantageMaxPpm (" + maxPpm + ") x median(chain, n) / 10^6 for n = 1..5.",
                Passed = problems.Count == 0,
                Measured = "advantage per run " + string.Join(" / ", advantage) + (problems.Count == 0 ? "" : "; " + string.Join("; ", problems)),
            };
        }

        private static long Stat(RunRecord rec, string key) => rec.Stats.TryGetValue(key, out long value) ? value : 0;
    }
}
