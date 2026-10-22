using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Run.Content;
using IdleFactory.Tests.EditMode.Balance;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// The gate for the balance trace: T107 (issue #189, R13, SC-001, SC-003, SC-004 trace part, FR-025 sensitivity), T125 (content revision 2), and
    /// since T341 (issue #406) policy r2 and the FR-024 gates of <c>specs/005-economy-contracts/reviews/balance-policy-r2.md</c> section 8.2. The
    /// default run holds the golden trace (<c>trace-r3.csv</c>, proves determinism), the structural checks and the <b>fast gate</b>: the chain and
    /// Ore-only at one look offset for runs 1 and 2, with every FR-024 gate that this small matrix can decide. The whole matrix (twelve
    /// strategy definitions, five offsets, runs 1 to 5) is <see cref="BalanceTraceFullTests"/>, category <c>BalanceFull</c>.
    /// The minute bands of the F003 policy (first Gear, eligibility, second run) are orientation since T341 (balance-policy-r2.md section 0) and are
    /// reported, never asserted. Regenerate the golden file with <c>IDLEFACTORY_REGENERATE_BALANCE_TRACE=1</c>.
    /// </summary>
    public sealed class BalanceTraceTests
    {
        private const string ContentPath = "Assets/IdleFactory/Content/Iron/iron-factory-content.json";
        private const string GoldenPath = "Assets/IdleFactory/Tests/EditMode/Fixtures/Balance/trace-r3.csv";
        private const string HistoryPathR1 = "Assets/IdleFactory/Tests/EditMode/Fixtures/Balance/trace-r1.csv";
        private const string HistorySha256R1 = "8c1c17516d89881c872022fd11f47efa3623fdc68b015ecfc9ee1ccaa9d8f647";
        private const string HistoryPathR2 = "Assets/IdleFactory/Tests/EditMode/Fixtures/Balance/trace-r2.csv";
        private const string HistorySha256R2 = "6e1df3d490e8cced8d5d4bf17a74d15bacb6ea862137f7ecabb28ffc61791791";
        private const string RemovedTransitionalNotePath = "Assets/IdleFactory/Tests/EditMode/Fixtures/Balance/trace-r2-transitional.md";
        private const string PolicyPathR1 = "specs/003-first-factory-prestige/reviews/balance-policy.md";
        private const string PolicyPathR2 = "specs/005-economy-contracts/reviews/balance-policy-r2.md";
        private const string RecordPath = "specs/005-economy-contracts/reviews/policy-r2-record.md";
        private const string TuningRecordPath = "specs/005-economy-contracts/reviews/balance-trace-r3.md";
        private const string RegenerateVariable = "IDLEFACTORY_REGENERATE_BALANCE_TRACE";

        private static RunCatalog _catalog;
        private static byte[] _contentBytes;
        private static BalanceTraceReport _report;
        private static BalanceMatrix _fast;
        private static List<GateResult> _fastGates;

        private static byte[] ContentBytes() => _contentBytes ?? (_contentBytes = File.ReadAllBytes(ContentPath));

        private static RunCatalog Catalog()
        {
            if (_catalog != null) return _catalog;
            var result = GameplayContentDispatcher.Load(ContentBytes());
            Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Code + " " + d.Path)));
            return _catalog = result.RunCatalog;
        }

        private static BalanceTraceReport Report() => _report ?? (_report = BalanceTraceReport.Build(Catalog()));

        /// <summary>The fast gate matrix: chain and Ore-only, the first look offset (0), runs 1 and 2.</summary>
        private static BalanceMatrix Fast() => _fast ?? (_fast = BalanceMatrix.Play(Catalog(), new[] { Strategies.Chain, Strategies.OreOnly }, 1, 2));

        private static List<GateResult> FastGates() => _fastGates ?? (_fastGates = TraceGates.Evaluate(Fast(), ContentBytes()));

        private static long Stat(RunRecord rec, string key) => rec.Stats.TryGetValue(key, out long value) ? value : 0;

        /// <summary>Writes a minute-band figure that is orientation only since T341 (balance-policy-r2.md section 0): it is reported, it never fails.</summary>
        private static void Orientation(string what, bool withinF003Band, string detail)
        {
            string line = "ORIENTATION (minute bands never fail, balance-policy-r2.md section 0) " + (withinF003Band ? "within" : "outside") + " " + what + ": " + detail;
            TestContext.Out.WriteLine(line);
            TestContext.Progress.WriteLine(line);
        }

        // ---- golden file and determinism ------------------------------------------------------------------

        [Test]
        public void TraceRegeneratedInMemoryEqualsTheGoldenFileByteForByte()
        {
            byte[] actual = new UTF8Encoding(false).GetBytes(Report().Csv);
            if (Environment.GetEnvironmentVariable(RegenerateVariable) == "1")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(GoldenPath));
                File.WriteAllBytes(GoldenPath, actual);
                Assert.Pass("Regenerated " + GoldenPath + " (" + actual.Length + " bytes).");
            }

            Assert.That(File.Exists(GoldenPath), Is.True, "The golden trace is missing; regenerate it with " + RegenerateVariable + "=1.");
            byte[] golden = File.ReadAllBytes(GoldenPath);
            Assert.That(golden.Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }), "UTF-8 BOM is forbidden.");
            // The repository stores the file as text (`* text=auto`), so a Windows checkout may turn LF into CRLF; compare with LF.
            string goldenText = new UTF8Encoding(false, true).GetString(golden).Replace("\r\n", "\n");
            Assert.That(new UTF8Encoding(false).GetString(actual), Is.EqualTo(goldenText),
                "The trace changed. If the change is intended, regenerate with " + RegenerateVariable + "=1 and review the diff.");
        }

        [Test]
        public void TwoIndependentBaselineRunsProduceIdenticalRows()
        {
            var runner = new BalanceTraceRunner(Catalog(), new ReferencePolicy());
            RunRecord again = runner.PlayRun("run1", 1, false, 7_200);

            Assert.That(again.Rows.Select(r => string.Join(",", r)), Is.EqualTo(Report().Baseline.Rows.Select(r => string.Join(",", r))));
            Assert.That(again.Stats, Is.EqualTo(Report().Baseline.Stats));
            Assert.That(runner.Context.Violations, Is.Empty);
        }

        [Test]
        public void EarlierTracesAreKeptAsHistoryAndTheTransitionalNoteIsGone()
        {
            foreach (KeyValuePair<string, string> pair in new Dictionary<string, string> { [HistoryPathR1] = HistorySha256R1, [HistoryPathR2] = HistorySha256R2 })
            {
                Assert.That(File.Exists(pair.Key), Is.True, pair.Key + " is history and stays");
                string history = new UTF8Encoding(false, true).GetString(File.ReadAllBytes(pair.Key)).Replace("\r\n", "\n");
                Assert.That(history, Does.StartWith(BalanceTraceReport.Header + "\n"));
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    string hash = string.Concat(sha.ComputeHash(new UTF8Encoding(false).GetBytes(history)).Select(b => b.ToString("x2")));
                    Assert.That(hash, Is.EqualTo(pair.Value), pair.Key + " must stay unchanged");
                }
            }

            // T341 restored the gates in place of the paused figures, so the note that explained the pause (T313, T320) is deleted.
            Assert.That(File.Exists(RemovedTransitionalNotePath), Is.False, "T341 deletes trace-r2-transitional.md");
            Assert.That(File.Exists(GoldenPath), Is.True, "trace-r3.csv is the golden of policy r2");
        }

        [Test]
        public void StartingCashBuysAtLeastTheFirstMinerSoTheFirstRunCannotSoftLock()
        {
            // The Miner is $30. With a start Cash below that the first run owns no machine and never earns (the trace reports "not reached"),
            // unless the held click pays the difference in the start window (FR-016); the start-affordability gate checks that case.
            RunCatalog catalog = Catalog();
            long minerCost = catalog.Machines["machine.miner"].CostTable[0];
            Assert.That(catalog.RunStart.StartingCash, Is.GreaterThanOrEqualTo(minerCost), "start Cash must buy the first Miner");
            Assert.That(catalog.RunStart.InitialUnlocks, Does.Contain("machine.miner"));
        }

        // ---- the FR-024 gates -----------------------------------------------------------------------------------

        [Test]
        public void TheFastGateAssertsTheFr024GatesOnTheChainAndOreOnlyAtOffsetZeroForRunsOneAndTwo()
        {
            BalanceMatrix matrix = Fast();
            Assert.That(matrix.Histories.Keys, Is.EquivalentTo(new[] { "chain", "ore-only" }));
            Assert.That(matrix.OffsetCount, Is.EqualTo(1));
            Assert.That(matrix.Histories["chain"][0].Runs, Has.Count.EqualTo(2));

            List<GateResult> gates = FastGates();
            Assert.That(gates.Select(g => g.Id), Is.SupersetOf(new[]
            {
                "conservation", "sale-neutrality", "partition-invariance", "remove-rebuild", "eco-3", "ratio-bounds", "ratio-content", "item-uptime", "start-affordability",
                "chain-dominance", "single-lever", "chain-within-6h",
            }));
            GateAssert.AssertAll(gates, Catalog().ContentRevision);
        }

        [Test]
        public void ContentTunedGatesAreTransitionalBelowRevision3AndAssertFromRevision3()
        {
            Assert.That(TraceGates.AssertsFromRevision, Is.EqualTo(3));
            Assert.That(TraceGates.ContentTunedIds, Is.EquivalentTo(new[] { "ratio-content", "item-uptime", "chain-dominance", "single-lever" }));

            var tuned = new GateResult { Id = "single-lever", ContentTuned = true, Passed = false };
            Assert.That(tuned.Fails(2), Is.False, "a failing content-tuned gate is only reported at revision 2");
            Assert.That(tuned.IsTransitionalMiss(2), Is.True);
            Assert.That(tuned.Fails(3), Is.True, "and it asserts at revision 3");
            Assert.That(tuned.IsTransitionalMiss(3), Is.False);

            var hard = new GateResult { Id = "y", ContentTuned = false, Passed = false };
            Assert.That(hard.Fails(2), Is.True, "a gate that content values do not decide always asserts");
            Assert.That(new GateResult { Id = "z", ContentTuned = true, Passed = true }.Fails(3), Is.False);

            Assert.Throws<AssertionException>(() => GateAssert.AssertAll(new[] { tuned }, 3));
            Assert.Throws<AssertionException>(() => GateAssert.AssertAll(new[] { hard }, 2));
            Assert.DoesNotThrow(() => GateAssert.AssertAll(new[] { tuned }, 2));
        }

        [Test]
        public void TheTransitionalMissesOfRevision2AreTheContentTunedGatesAndNothingElse()
        {
            List<GateResult> gates = FastGates();
            int revision = Catalog().ContentRevision;
            Assert.That(gates.Where(g => !g.Passed && !g.ContentTuned).Select(g => g.Id), Is.Empty, "a gate that content does not decide must pass");
            if (revision >= TraceGates.AssertsFromRevision)
                Assert.That(gates.Where(g => !g.Passed).Select(g => g.Id), Is.Empty, "from revision 3 every gate passes");
            else
                foreach (GateResult g in gates.Where(g => g.IsTransitionalMiss(revision)))
                    Assert.That(TraceGates.ContentTunedIds, Does.Contain(g.Id));
        }

        [Test]
        public void MinuteBandsAreOrientationAndTheChainStillReachesEveryThreshold()
        {
            // The F003 minute bands (first Gear 600 to 900 s, eligibility 6,600 to 7,800 s, second run 4,200 to 4,800 s) are orientation since T341:
            // they are written down and never fail. What stays asserted is that nothing is created or lost and that the chain reaches all five runs.
            BalanceTraceReport report = Report();
            foreach (BandResult band in report.Bands().Where(b => !b.Name.StartsWith("var.", StringComparison.Ordinal) && !b.Name.StartsWith("probe.", StringComparison.Ordinal)))
                Orientation("band '" + band.Name + "'", band.Pass, "measured " + band.Measured + ", expected " + band.Expected);
            Orientation("SC-003 first Gear", report.Baseline.FirstGearAtSec >= BalanceBands.FirstGearMinSec && report.Baseline.FirstGearAtSec <= BalanceBands.FirstGearMaxSec,
                report.Baseline.FirstGearAtSec + " s");
            Orientation("SC-003 first prestige", report.Baseline.EligibleAtSec >= BalanceBands.EligibleMinSec && report.Baseline.EligibleAtSec <= BalanceBands.EligibleMaxSec,
                report.Baseline.EligibleAtSec + " s");
            Orientation("SC-004 second run", report.Chain[1].EligibleAtSec >= BalanceBands.SecondRunMinSec && report.Chain[1].EligibleAtSec <= BalanceBands.SecondRunMaxSec,
                report.Chain[1].EligibleAtSec + " s");

            Assert.That(report.Chain.Count, Is.EqualTo(BalanceTraceReport.MaxRuns), "all five runs of the real catalog reach eligibility");
            Assert.That(report.Chain.All(r => r.EligibleAtSec > 0 && r.EligibleAtSec <= ReferencePolicy.RunLimitSeconds), Is.True);
            Assert.That(report.Violations, Is.Empty);
        }

        // ---- the sensitivity variants (FR-025) ---------------------------------------------------------------------

        [Test]
        public void SensitivityBoundsAreAssertedUnderThePolicyR2Tolerances()
        {
            // FR-025: the bounds are measurement tolerances re-derived under policy r2 (BalanceBands, balance-policy-r2.md, policy-r2-record.md section 5).
            // The two F003 deviations that were recorded as OWNER DECISION under policy r1 (contracts-off, returns) are inside the re-derived bounds, and
            // var.contracts-slow, reported only since T320, asserts again.
            BandResult[] failing = Report().Bands().Where(b => b.Name.StartsWith("var.", StringComparison.Ordinal) && !b.Pass).ToArray();
            Assert.That(failing.Select(b => b.Name + ": " + b.Measured + " (" + b.Expected + ")"), Is.Empty);
        }

        [Test]
        public void ThePriceVariantIsANonRoundingModifierAndTheRoundingPathIsGone()
        {
            // QA-14: the old var.price-80 (and var.price-120) scaled every base price and rounded it to a whole number; both are gone.
            Assert.That(Report().Variants.Select(v => v.Block), Is.EqualTo(new[]
            {
                "var.price-mod-125", "var.price-mod-120", "var.contracts-off", "var.contracts-slow", "var.overdrive-off", "var.absence-30m", "var.absence-60m", "var.returns", "probe.ore-only",
            }));
            Assert.That(Report().Variants.Select(v => v.Block), Has.None.EqualTo("var.price-80"));
            Assert.That(Report().Variants.Select(v => v.Block), Has.None.EqualTo("var.price-120"));
            Assert.That(Report().Csv, Does.Not.Contain("var.price-80,"));
            Assert.That(Report().Csv, Does.Not.Contain("var.price-120,"));
            Assert.That(typeof(BalanceTraceReport).GetMethod("ScalePrices", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance), Is.Null, "the rounding price scaler is removed");

            // The variants do not touch a base price: the modifier is one Industry level of an upgrade no run can buy.
            RunCatalog catalog = Catalog();
            foreach (ResourceDef resource in catalog.Resources.Values)
                Assert.That(resource.BasePrice, Is.GreaterThan(0));
            Assert.That(catalog.IndustryUpgrades.ContainsKey(BalanceTraceReport.PriceVariantUpgradeId), Is.False, "the shipped content has no variant upgrade");
        }

        [Test]
        public void SensitivityPriceVariantsMoveTheMilestoneInTheDirectionOfThePrice()
        {
            long baseline = Report().Baseline.MilestoneAtSec;
            long mod125 = Report().Variants.Single(v => v.Block == "var.price-mod-125").MilestoneAtSec;
            long mod120 = Report().Variants.Single(v => v.Block == "var.price-mod-120").MilestoneAtSec;
            Assert.That(mod125, Is.LessThan(baseline), "a higher sale price reaches the milestone sooner");
            Assert.That(mod120, Is.LessThan(baseline));
            // The order of two variants 4 % apart is not guaranteed: the milestone is a discrete event of a greedy policy, and on revision 3 the 1.25 variant
            // buys its storage level at 1,700 s while the 1.20 variant does not need one before the milestone, which costs it 100 s (4 % of the baseline) at every
            // one of the five look-grid offsets. Both variants are inside the 25 % price bound (the band test) and within a tenth of the baseline of each other.
            Assert.That(Math.Abs(mod125 - mod120), Is.LessThanOrEqualTo(baseline / 10), "and the two variants are close to each other");
        }

        [Test]
        public void SensitivityVariantsAllReachTheMilestoneAndNeverChangeTheOfflinePolicy()
        {
            foreach (RunRecord v in Report().Variants) Assert.That(v.MilestoneAtSec, Is.GreaterThan(0), v.Block + " reached the milestone Factory Value");
            Assert.That(Catalog().OfflinePolicy.Revision, Is.EqualTo(1));
            Assert.That(Catalog().OfflinePolicy.EfficiencyNumerator * 5, Is.EqualTo(Catalog().OfflinePolicy.EfficiencyDenominator * 4), "80 % offline effectiveness is unchanged (D-A)");
            // The absence starts at 1,800 s or 3,600 s of the variant. If the Factory Value milestone of $250,000 comes before that, the absence never starts.
            long milestone = Report().Baseline.MilestoneAtSec;
            Assert.That(Report().Variants.Single(v => v.Block == "var.absence-30m").AbsentSec, Is.EqualTo(milestone > 1_800 ? 28_800 : 0));
            Assert.That(Report().Variants.Single(v => v.Block == "var.absence-60m").AbsentSec, Is.EqualTo(milestone > 3_600 ? 28_800 : 0));
        }

        // ---- contract columns and checkpoints --------------------------------------------------------------

        [Test]
        public void BaselineRowsCarryTheContractColumnsAtTheSixCheckpoints()
        {
            string[] header = BalanceTraceReport.Header.Split(',');
            Assert.That(header, Is.EqualTo(new[]
            {
                "block", "minute", "cash", "fv", "ip_preview", "stock", "machines", "storageLevel", "factoryLevel", "contractsCompleted",
                "itemsHeld", "researchLevels", "firstGearAtSec", "eligibleAtSec",
            }));

            var checkpoints = Report().Baseline.Rows.Where(r => !r[1].EndsWith("E")).ToList();
            Assert.That(checkpoints.Select(r => r[1]), Is.EqualTo(new[] { "10", "15", "30", "60", "90", "120" }));
            foreach (string[] row in Report().Baseline.Rows)
            {
                Assert.That(row.Length, Is.EqualTo(header.Length));
                Assert.That(row[5].Split('/').Length, Is.EqualTo(4), "stock ore/bar/plate/gear");
                Assert.That(row[10].Split('/').Length, Is.EqualTo(3), "itemsHeld t1/t2/t3");
            }
            Assert.That(checkpoints.All(r => r[12] == Report().Baseline.FirstGearAtSec.ToString()), Is.True);
            Assert.That(checkpoints.All(r => r[13] == Report().Baseline.EligibleAtSec.ToString()), Is.True);
        }

        [Test]
        public void TraceHasASecondRunBlockAFiveRunProjectionAndTheSensitivityVariants()
        {
            Assert.That(Report().Scaled.Count, Is.EqualTo(BalanceTraceReport.MaxRuns));
            Assert.That(Report().Scaled.All(r => r.PrestigedAtSec > 0), Is.True, "every run of the scaled projection reached eligibility and prestiged");
            Assert.That(Report().Scaled[1].Rows, Is.Not.Empty, "second-run block");
            Assert.That(Report().Csv, Does.Contain("scaled.run2,"));
            Assert.That(Report().Csv, Does.Contain("var.absence-30m,"));
            Assert.That(Report().Csv, Does.Contain("var.price-mod-125,"));
        }

        [Test]
        public void ChainRunOneEqualsTheBaselineUpToEligibility()
        {
            Assert.That(Report().Chain[0].MilestoneAtSec, Is.EqualTo(Report().Baseline.MilestoneAtSec));
            Assert.That(Report().Chain[0].EligibleAtSec, Is.EqualTo(Report().Baseline.EligibleAtSec));
            Assert.That(Report().Chain[0].FirstGearAtSec, Is.EqualTo(Report().Baseline.FirstGearAtSec));
        }

        // ---- SC-001 -----------------------------------------------------------------------------------------

        [Test]
        public void Sc001ChainOreBarPlateGearContractItemResearchPrestigeIndustryFasterNextRunWithNoUnintendedChange()
        {
            BalanceTraceReport report = Report();
            SortedDictionary<string, long> events = report.ScaledEvents;
            string[] required = { "gear", "gearContract", "item", "research", "prestige", "industry" };
            foreach (string name in required) Assert.That(events.ContainsKey(name), Is.True, "chain step missing: " + name + " (" + string.Join(",", events.Keys) + ")");

            // Ore -> Bar -> Plate -> Gear: the Gear exists only because the earlier machines produced their goods.
            Assert.That(events["gear"], Is.GreaterThan(0));
            Assert.That(events["gearContract"], Is.GreaterThanOrEqualTo(events["gear"]), "a Gear contract needs Gears");
            Assert.That(events["item"], Is.LessThan(events["prestige"]), "an Item was granted before the prestige");
            Assert.That(events["research"], Is.LessThan(events["prestige"]), "Research was purchased before the prestige");
            Assert.That(events["industry"], Is.GreaterThanOrEqualTo(events["prestige"]), "the Industry upgrade follows the prestige");

            RunRecord first = report.Scaled[0];
            RunRecord second = report.Scaled[1];
            Assert.That(first.IpAwarded, Is.GreaterThan(0));
            Assert.That(Stat(first, "industry_purchases"), Is.GreaterThan(0), "IP was spent on an Industry upgrade");
            Assert.That(second.EligibleAtSec, Is.GreaterThan(0).And.LessThan(first.EligibleAtSec), "the next run is faster");

            Assert.That(report.Violations, Is.Empty, "zero unintended resource, Cash, RP, IP or Item changes");
        }

        // ---- ECO-5, ECO-6, OD-6, OD-9 ---------------------------------------------------------------------------

        [Test]
        public void ScaledProjectionRunTimesAreReportedAndTheRestatedEco3IsGatedOnTheRealCatalog()
        {
            // The scaled catalog (threshold divided by 100) shapes the trace for SC-001 and the five-run projection. Its run times are orientation; the
            // restated ECO-3 gate (a look interval of tolerance, FR-021) is asserted on the real catalog by the fast gate and the full matrix.
            List<RunRecord> chain = Report().Scaled;
            Orientation("scaled runs 1 to 5", chain.Zip(chain.Skip(1), (a, b) => b.EligibleAtSec <= a.EligibleAtSec + ReferencePolicy.EpochSeconds).All(x => x),
                string.Join(" / ", chain.Select(r => r.EligibleAtSec)) + " s");
            Assert.That(chain.All(r => r.EligibleAtSec > 0), Is.True);
        }

        [Test]
        public void Eco5RewardToMarketRatioAndRpPerCompletionAreRecordedAndBounded()
        {
            long delivered = 0;
            foreach (RunRecord rec in Report().Scaled)
            {
                long n = Stat(rec, "contracts_delivered");
                delivered += n;
                if (n == 0) continue;
                long ratio = Stat(rec, "contract_ratio_ppm_sum") / n;
                Assert.That(ratio, Is.InRange(1_000_000, Catalog().RunStart.MaxRewardRatioPpm), rec.Block + " mean reward to market ratio");
            }
            Assert.That(delivered, Is.GreaterThan(0));
        }

        [Test]
        public void Eco6ItemAccrualPerRunIsRecordedAndBoundedByTheGrantLists()
        {
            // T320: the ladder grants at most its entries, and at most PerRunCap of them, per run.
            ContractItemLadder ladder = Catalog().ContractItemLadder;
            long bound = Math.Min(ladder.PerRunCap, ladder.Entries.Count);
            long total = 0;
            foreach (RunRecord rec in Report().Scaled)
            {
                long granted = Stat(rec, "items_granted.tier1") + Stat(rec, "items_granted.tier2") + Stat(rec, "items_granted.tier3");
                Assert.That(granted, Is.LessThanOrEqualTo(bound), rec.Block + " Item accrual");
                total += granted;
            }
            Assert.That(total, Is.GreaterThan(0));
        }

        [Test]
        public void PartialBatchStatisticsAreRecordedPerRun()
        {
            foreach (RunRecord rec in Report().Scaled)
            {
                Assert.That(Stat(rec, "completion_samples"), Is.GreaterThan(0), rec.Block + " samples");
                Assert.That(Stat(rec, "partial_samples"), Is.InRange(0, Stat(rec, "completion_samples")));
            }
        }

        // ---- the written policy and the T341 record ---------------------------------------------------------

        [Test]
        public void PolicyDocumentsStateTheBandsTheLatencyRulesAndTheStrategies()
        {
            string r1 = File.ReadAllText(PolicyPathR1);
            foreach (string needle in new[]
            {
                "600", "900", "6,600", "7,800", "4,200", "4,800", "no-perfect-timing", "20 s", "240", "300", "action latency", "idle",
                "var.price-80", "var.price-120", "var.contracts-off", "var.contracts-slow", "var.overdrive-off", "var.absence-30m", "var.absence-60m", "var.returns",
            })
                Assert.That(r1, Does.Contain(needle), PolicyPathR1 + " must contain '" + needle + "'");
            string r2 = File.ReadAllText(PolicyPathR2);
            foreach (StrategyDefinition s in Strategies.All)
                Assert.That(r2, Does.Contain(s.Id.StartsWith("prestige-later", StringComparison.Ordinal) ? "prestige-later" : s.Id), PolicyPathR2 + " must name " + s.Id);
            Assert.That(ReferencePolicy.EpochSeconds, Is.EqualTo(20));
            Assert.That(ReferencePolicy.IdleStartSeconds, Is.EqualTo(240));
            Assert.That(ReferencePolicy.IdleCycleSeconds, Is.EqualTo(300));
        }

        [Test]
        public void TheT341RecordNamesEveryGateEveryVariantAndEveryBand()
        {
            // The T341 record names the gates T341 built; the T345 tuning record names `ratio-content` (the content-tuned half of the split ratio gate)
            // and re-states the bands and variants on revision 3. Together they name every gate, variant and band.
            string text = File.ReadAllText(RecordPath) + "\n" + File.ReadAllText(TuningRecordPath);
            foreach (GateResult g in FastGates())
                Assert.That(text, Does.Contain("`" + g.Id + "`"), "the T341 record names gate " + g.Id);
            foreach (string id in new[] { "ip-4x", "chain-plus-mining" })
                Assert.That(text, Does.Contain("`" + id + "`"), "the T341 record names gate " + id);
            foreach (BandResult band in Report().Bands())
                Assert.That(text, Does.Contain(band.Name), "the T341 record names " + band.Name);
            foreach (string needle in new[] { "transitional", "contentRevision", "CasualLookIntervalSeconds", "PF-15", "BalanceFull", "OWNER" })
                Assert.That(text, Does.Contain(needle));
        }
    }
}
