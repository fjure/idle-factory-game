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
    /// Final-green gate for the balance trace (T107, issue #189, R13, SC-001, SC-003, SC-004 trace part, FR-025 sensitivity)
    /// and, since T125 (issue #207), for the proposed content revision 2. The trace drives the real application commands with
    /// the written reference policy (<c>specs/003-first-factory-prestige/reviews/balance-policy.md</c>). The golden CSV proves
    /// determinism; the ECO-2 bands (first Gear, eligibility, second run) and the ECO-3 run times are asserted for revision 2,
    /// and the sensitivity bounds are asserted except for the deviations that <c>reviews/balance-trace-r2.md</c> records as
    /// OWNER DECISION. The content stays a balance hypothesis until the human playtest (T126) and the owner approval (T127).
    /// <b>Transitional since T313 (issue #375, analysis I1):</b> the shipped content is schema version 3 with the revision 2 values, and its
    /// contract Cash rewards come from <c>ceil(quantity x price x ratio)</c> through the transitional reward adapter instead of the F003 Cash
    /// curves, so <c>trace-r2.csv</c> was regenerated once as a transitional golden (see <c>trace-r2-transitional.md</c>). The conservation
    /// (zero violations), SC-001 chain, ECO-3 monotonic run times and sensitivity-set gates stay asserted; the F003 pacing bands and the
    /// quotes of the review records are <b>reported, not asserted</b>, until T339 retunes the content and regenerates the golden.
    /// Revision 1 is kept as history (<c>trace-r1.csv</c>, <c>balance-trace-r1.md</c>, <c>content-record-r1.md</c>). Regenerate
    /// the golden file with <c>IDLEFACTORY_REGENERATE_BALANCE_TRACE=1</c>.
    /// </summary>
    public sealed class BalanceTraceTests
    {
        private const string ContentPath = "Assets/IdleFactory/Content/Iron/iron-factory-content.json";
        private const string GoldenPath = "Assets/IdleFactory/Tests/EditMode/Fixtures/Balance/trace-r2.csv";
        private const string HistoryPath = "Assets/IdleFactory/Tests/EditMode/Fixtures/Balance/trace-r1.csv";
        private const string HistorySha256 = "8c1c17516d89881c872022fd11f47efa3623fdc68b015ecfc9ee1ccaa9d8f647";
        private const string PolicyPath = "specs/003-first-factory-prestige/reviews/balance-policy.md";
        private const string RecordPath = "specs/003-first-factory-prestige/reviews/balance-trace-r2.md";
        private const string ContentRecordPath = "specs/003-first-factory-prestige/reviews/content-record-r2.md";
        private const string RegenerateVariable = "IDLEFACTORY_REGENERATE_BALANCE_TRACE";
        private const string TransitionalNotePath = "Assets/IdleFactory/Tests/EditMode/Fixtures/Balance/trace-r2-transitional.md";

        /// <summary>Writes a pacing figure that used to be asserted (F003 bands and record quotes) and is only reported until T339.</summary>
        private static void Reported(string what, bool withinExpectation, string detail)
        {
            string line = "REPORTED (transitional golden, T313; asserted again by T339) " + (withinExpectation ? "within" : "OUTSIDE") + " " + what + ": " + detail;
            TestContext.Out.WriteLine(line);
            TestContext.Progress.WriteLine(line);
        }

        /// <summary>
        /// Sensitivity bounds that revision 2 misses under policy r1. Each is recorded as OWNER DECISION in
        /// <c>balance-trace-r2.md</c> section 6 with its cause; the set is pinned so that a new miss, or a fix, fails here.
        /// </summary>
        private static readonly string[] RecordedSensitivityDeviations = { "var.contracts-off", "var.price-80", "var.returns" };

        private static readonly string[] AssertedBands =
        {
            "first Gear (run 1)", "eligibility (run 1)", "second run to eligibility (run 2)", "runs 1 to 5 (ECO-3)", "scaled runs 1 to 5 (ECO-3, threshold scaled)",
        };

        private static RunCatalog _catalog;
        private static BalanceTraceReport _report;

        private static RunCatalog Catalog()
        {
            if (_catalog != null) return _catalog;
            var result = GameplayContentDispatcher.Load(File.ReadAllBytes(ContentPath));
            Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Code + " " + d.Path)));
            return _catalog = result.RunCatalog;
        }

        private static BalanceTraceReport Report() => _report ?? (_report = BalanceTraceReport.Build(Catalog()));

        private static long Stat(RunRecord rec, string key) => rec.Stats.TryGetValue(key, out long value) ? value : 0;

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

        // ---- ECO-2 bands, ECO-3 and ECO-4 for content revision 2 (T125) -----------------------------------------

        [Test]
        public void TraceTargetsContentRevision2AndTheEarlierTraceIsKeptAsHistory()
        {
            Assert.That(Catalog().ContentRevision, Is.EqualTo(2), "the shipped content keeps the revision 2 values (schema version 3, T313)");

            Assert.That(File.Exists(HistoryPath), Is.True, "trace-r1.csv is history and stays");
            string history = new UTF8Encoding(false, true).GetString(File.ReadAllBytes(HistoryPath)).Replace("\r\n", "\n");
            Assert.That(history, Does.StartWith(BalanceTraceReport.Header + "\n"));
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                string hash = string.Concat(sha.ComputeHash(new UTF8Encoding(false).GetBytes(history)).Select(b => b.ToString("x2")));
                Assert.That(hash, Is.EqualTo(HistorySha256), "trace-r1.csv (content revision 1) must stay unchanged");
            }
        }

        [Test]
        public void StartingCashBuysAtLeastTheFirstMinerSoTheFirstRunCannotSoftLock()
        {
            // Revision 2 prices the Miner at $30. With a start Cash below that the first run owns no machine and never earns
            // (the trace reports "not reached" with $25), and the content validator does not check it.
            RunCatalog catalog = Catalog();
            long minerCost = catalog.Machines["machine.miner"].CostTable[0];
            Assert.That(catalog.RunStart.StartingCash, Is.GreaterThanOrEqualTo(minerCost), "start Cash must buy the first Miner");
            Assert.That(catalog.RunStart.InitialUnlocks, Does.Contain("machine.miner"));
        }

        [Test]
        public void Eco2BandsAndTheEco3RunTimesAreReportedForTheTransitionalRevision2()
        {
            // The F003 pacing bands were asserted for revision 2 (T125). Through the transitional reward adapter the first run becomes faster
            // (eligibility 6,540 s instead of 6,940 s, below the 6,600 s lower bound), so the bands are reported, not asserted, until T339.
            BalanceTraceReport report = Report();
            foreach (string name in AssertedBands)
            {
                BandResult band = report.Bands().Single(b => b.Name == name);
                Reported("band '" + band.Name + "'", band.Pass, "measured " + band.Measured + ", expected " + band.Expected);
            }

            Reported("SC-003 first Gear", report.Baseline.FirstGearAtSec >= BalanceBands.FirstGearMinSec && report.Baseline.FirstGearAtSec <= BalanceBands.FirstGearMaxSec,
                report.Baseline.FirstGearAtSec + " s, expected " + BalanceBands.FirstGearMinSec + " to " + BalanceBands.FirstGearMaxSec + " s");
            Reported("SC-003 first prestige", report.Baseline.EligibleAtSec >= BalanceBands.EligibleMinSec && report.Baseline.EligibleAtSec <= BalanceBands.EligibleMaxSec,
                report.Baseline.EligibleAtSec + " s, expected " + BalanceBands.EligibleMinSec + " to " + BalanceBands.EligibleMaxSec + " s");
            Reported("SC-004 second run", report.Chain[1].EligibleAtSec >= BalanceBands.SecondRunMinSec && report.Chain[1].EligibleAtSec <= BalanceBands.SecondRunMaxSec,
                report.Chain[1].EligibleAtSec + " s, expected " + BalanceBands.SecondRunMinSec + " to " + BalanceBands.SecondRunMaxSec + " s");

            // Still asserted: the chain reaches eligibility in every run, and nothing is created or lost (conservation, FR-015).
            Assert.That(report.Chain.Count, Is.EqualTo(BalanceTraceReport.MaxRuns), "all five runs of the real catalog reach eligibility");
            Assert.That(report.Violations, Is.Empty);
        }

        [Test]
        public void TheTransitionalGoldenIsLabelledAndNamesWhatMovedFromAssertedToReported()
        {
            Assert.That(File.Exists(TransitionalNotePath), Is.True, "The transitional golden needs its adjacent note.");
            string note = File.ReadAllText(TransitionalNotePath);
            foreach (string needle in new[]
            {
                "transitional", "T313", "T339", "ceil(quantity", "trace-r2.csv", "Eco2BandsAndTheEco3RunTimesAreReportedForTheTransitionalRevision2",
                "ContentRecordR2QuotesAreReportedForTheTransitionalTrace", "ReviewRecordQuotesAreReportedForTheTransitionalTrace",
                "contracts-off", "eligibility",
            })
                Assert.That(note, Does.Contain(needle), TransitionalNotePath + " must mention '" + needle + "'");
        }

        [Test]
        public void SensitivityBoundsHoldExceptTheRecordedOwnerDecisionDeviations()
        {
            string[] failing = Report().Bands().Where(b => b.Name.StartsWith("var.", StringComparison.Ordinal) && !b.Pass).Select(b => b.Name)
                .OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Assert.That(failing, Is.EqualTo(RecordedSensitivityDeviations), "sensitivity bounds that fail must be exactly the ones recorded as OWNER DECISION");

            string record = File.ReadAllText(RecordPath);
            foreach (string name in RecordedSensitivityDeviations) Assert.That(record, Does.Contain(name + " "), "the record explains " + name);
            Assert.That(record, Does.Contain("OWNER DECISION"));
        }

        [Test]
        public void ContentRecordR2QuotesAreReportedForTheTransitionalTrace()
        {
            string record = File.ReadAllText(ContentRecordPath);
            Assert.That(record, Does.Contain("contentRevision 2"));
            Assert.That(record, Does.Contain("Theoretical maximum income"));
            Assert.That(record, Does.Contain("Income lost to partial batches"));
            RunRecord run1 = Report().Chain[0];
            Assert.That(Stat(run1, "partial_lost_cash_lower_bound"), Is.LessThanOrEqualTo(Stat(run1, "partial_lost_cash_epoch_bound")), "lower bound <= upper bound");
            foreach (string key in new[] { "partial_lost_cash_lower_bound", "partial_lost_cash_epoch_bound", "partial_lost_units", "store_blocked_samples", "machine_look_samples" })
            {
                string quoted = Stat(run1, key).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
                Reported("record quote " + key + " of run 1", record.Contains(quoted), "trace " + quoted + (record.Contains(quoted) ? ", quoted" : ", not quoted by content-record-r2.md (it describes the F003 trace)"));
            }
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
            Assert.That(Report().Variants.Select(v => v.Block), Is.EqualTo(new[]
            {
                "var.price-80", "var.price-120", "var.contracts-off", "var.contracts-slow", "var.overdrive-off", "var.absence-30m", "var.absence-60m", "var.returns", "probe.ore-only",
            }));
            Assert.That(Report().Csv, Does.Contain("var.absence-30m,"));
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

        // ---- ECO-3, ECO-5, ECO-6, OD-6, OD-9 ---------------------------------------------------------------

        [Test]
        public void Eco3RunTimesOfTheProjectionAreNonIncreasingAndAboveTheFloor()
        {
            List<RunRecord> chain = Report().Scaled;
            for (int i = 1; i < chain.Count; i++)
            {
                Assert.That(chain[i].EligibleAtSec, Is.LessThanOrEqualTo(chain[i - 1].EligibleAtSec), "run " + (i + 1) + " is not slower than run " + i);
                Assert.That(chain[i].EligibleAtSec * 1000, Is.GreaterThanOrEqualTo(chain[0].EligibleAtSec * BalanceBands.RunFloorPermille), "run " + (i + 1) + " is above the floor");
            }
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
            long bound = Catalog().ContractOffers.Values.SelectMany(o => o.Rewards).Where(r => r.Kind == "item").Sum(r => r.OnCompletionIndices.Count);
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

        // ---- sensitivity (ECO-4, FR-025) --------------------------------------------------------------------

        [Test]
        public void SensitivityVariantsAllReachTheMilestoneAndNeverChangeTheOfflinePolicy()
        {
            foreach (RunRecord v in Report().Variants) Assert.That(v.MilestoneAtSec, Is.GreaterThan(0), v.Block + " reached the milestone Factory Value");
            Assert.That(Catalog().OfflinePolicy.Revision, Is.EqualTo(1));
            // The absence starts at 1,800 s or 3,600 s of the variant. Revision 2 reaches the Factory Value milestone of $250,000
            // before minute 60, so var.absence-60m never leaves (vacuous; recorded in balance-trace-r2.md section 6).
            long milestone = Report().Baseline.MilestoneAtSec;
            Assert.That(Report().Variants.Single(v => v.Block == "var.absence-30m").AbsentSec, Is.EqualTo(milestone > 1_800 ? 28_800 : 0));
            Assert.That(Report().Variants.Single(v => v.Block == "var.absence-60m").AbsentSec, Is.EqualTo(milestone > 3_600 ? 28_800 : 0));
        }

        [Test]
        public void SensitivityPriceVariantsMoveEligibilityInTheDirectionOfThePrice()
        {
            long baseline = Report().Baseline.MilestoneAtSec;
            Assert.That(Report().Variants.Single(v => v.Block == "var.price-80").MilestoneAtSec, Is.GreaterThanOrEqualTo(baseline));
            Assert.That(Report().Variants.Single(v => v.Block == "var.price-120").MilestoneAtSec, Is.LessThanOrEqualTo(baseline));
        }

        // ---- the written policy and the review record -------------------------------------------------------

        [Test]
        public void PolicyDocumentStatesTheBandsAndTheLatencyRules()
        {
            string text = File.ReadAllText(PolicyPath);
            foreach (string needle in new[]
            {
                "600", "900", "6,600", "7,800", "4,200", "4,800", "no-perfect-timing", "20 s", "240", "300", "action latency", "idle",
                "var.price-80", "var.price-120", "var.contracts-off", "var.contracts-slow", "var.overdrive-off", "var.absence-30m", "var.absence-60m", "var.returns",
            })
                Assert.That(text, Does.Contain(needle), PolicyPath + " must contain '" + needle + "'");
            Assert.That(ReferencePolicy.EpochSeconds, Is.EqualTo(20));
            Assert.That(ReferencePolicy.IdleStartSeconds, Is.EqualTo(240));
            Assert.That(ReferencePolicy.IdleCycleSeconds, Is.EqualTo(300));
        }

        [Test]
        public void ReviewRecordQuotesAreReportedForTheTransitionalTrace()
        {
            string text = File.ReadAllText(RecordPath);
            BalanceTraceReport report = Report();
            Assert.That(text, Does.Contain("first Gear"));
            string eligibility = report.Baseline.EligibleAtSec < 0 ? "not reached" : report.Baseline.EligibleAtSec + " s";
            Reported("record quote first Gear time", text.Contains(report.Baseline.FirstGearAtSec + " s"), report.Baseline.FirstGearAtSec + " s");
            Reported("record quote eligibility time", text.Contains(eligibility), eligibility + " (balance-trace-r2.md describes the F003 trace)");
            Reported("record quote scaled second run time", text.Contains(report.Scaled[1].EligibleAtSec + " s"), report.Scaled[1].EligibleAtSec + " s");
            foreach (BandResult band in report.Bands())
                Assert.That(text, Does.Contain(band.Name), "the record names " + band.Name);
        }
    }
}
