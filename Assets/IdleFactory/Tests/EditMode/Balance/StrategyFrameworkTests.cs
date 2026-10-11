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
    /// The strategy framework of T311 (issue #373, balance-policy-r2.md sections 7 and 8.1). The default tests are structural
    /// only: determinism, medians on a known input, the look-grid offsets and the exact checkpoints. They assert no balance band.
    /// The full strategy x offset x run matrix is <see cref="WriteStrategyBaselineReport"/> (about 40 s): category
    /// <c>BalanceStrategies</c>, run with <c>scripts/test-unity.ps1 -Suite EditMode -Category BalanceStrategies</c>. It is not marked
    /// <c>[Explicit]</c>, because the test runner script counts a skipped test as a failure of the whole suite; it therefore also runs in
    /// the default suite, asserts only conservation, and rewrites the reviewed report only with <c>IDLEFACTORY_WRITE_STRATEGY_BASELINE=1</c>.
    /// </summary>
    [Category("BalanceStrategies")]
    public sealed class StrategyFrameworkTests
    {
        private const string ContentPath = "Assets/IdleFactory/Content/Iron/iron-factory-content.json";
        private const string ReportPath = "specs/005-economy-contracts/reviews/strategy-baseline-r2.md";
        private const string WriteVariable = "IDLEFACTORY_WRITE_STRATEGY_BASELINE";

        private static RunCatalog _catalog;

        private static RunCatalog Catalog()
        {
            if (_catalog != null) return _catalog;
            var result = GameplayContentDispatcher.Load(File.ReadAllBytes(ContentPath));
            Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Code + " " + d.Path)));
            return _catalog = result.RunCatalog;
        }

        // ---- medians and offsets -----------------------------------------------------------------------------

        [Test]
        public void MedianOfFiveIsTheThirdSmallestAndNotReachedIsOrderedAboveEveryNumber()
        {
            Assert.That(StrategyEnsemble.Median(new long[] { 50, 10, 40, 20, 30 }), Is.EqualTo(30));
            Assert.That(StrategyEnsemble.Median(new long[] { 10, -1, -1, 20, 30 }), Is.EqualTo(30), "two not reached: the third smallest is still a number");
            Assert.That(StrategyEnsemble.Median(new long[] { 10, -1, -1, -1, 30 }), Is.EqualTo(-1), "three not reached: the median is not reached");
            Assert.That(StrategyEnsemble.Median(new long[] { -1, -1, -1, -1, -1 }), Is.EqualTo(-1));
            Assert.That(StrategyEnsemble.Median(new long[] { 7 }), Is.EqualTo(7));
            Assert.That(StrategyEnsemble.Median(new long[] { 4, 2, 8, 6 }), Is.EqualTo(4), "even count: the lower middle value");
        }

        [Test]
        public void MedianOfAvailableSkipsMissingValuesAndNeedsThreeOfThem()
        {
            Assert.That(StrategyEnsemble.MedianOfAvailable(new long?[] { 5, null, 1, 3, null }), Is.EqualTo(3));
            Assert.That(StrategyEnsemble.MedianOfAvailable(new long?[] { 5, null, 1, null, null }), Is.Null);
        }

        [Test]
        public void OffsetsScaleProportionallyWithTheLookInterval()
        {
            Assert.That(StrategyEnsemble.Offsets(20), Is.EqualTo(new[] { 0, 4, 8, 12, 16 }));
            Assert.That(StrategyEnsemble.Offsets(37), Is.EqualTo(new[] { 0, 7, 14, 22, 29 }), "integer division of i x L / 5");
            Assert.That(StrategyEnsemble.Offsets(ReferencePolicy.CasualLookIntervalSeconds)[4], Is.LessThan(ReferencePolicy.CasualLookIntervalSeconds));
        }

        [Test]
        public void TheAwayWindowIsAFunctionOfTheRunSecondAndStrategyConfigurable()
        {
            IStrategyPolicy chain = Strategies.Chain.Create();
            Assert.That(chain.IsIdleAt(239), Is.False);
            Assert.That(chain.IsIdleAt(240), Is.True);
            Assert.That(chain.IsIdleAt(299), Is.True);
            Assert.That(chain.IsIdleAt(300), Is.False);
            IStrategyPolicy heavy = Strategies.IdleHeavy.Create();
            Assert.That(heavy.IsIdleAt(119), Is.False);
            Assert.That(heavy.IsIdleAt(120), Is.True);
            Assert.That(Strategies.Casual.Create().LookIntervalSeconds, Is.EqualTo(ReferencePolicy.CasualLookIntervalSeconds));
            Assert.That(Strategies.Chain.Create().LookIntervalSeconds, Is.EqualTo(ReferencePolicy.EpochSeconds));
        }

        // ---- the runner extension ----------------------------------------------------------------------------

        [Test]
        public void TwoHistoriesOfTheSameStrategyAndOffsetAreIdentical()
        {
            StrategyHistory a = StrategyEnsemble.PlayHistory(Catalog(), Strategies.Chain, 0, 2);
            StrategyHistory b = StrategyEnsemble.PlayHistory(Catalog(), Strategies.Chain, 0, 2);

            Assert.That(a.Runs, Has.Count.EqualTo(2));
            Assert.That(b.Runs.Count, Is.EqualTo(a.Runs.Count));
            for (int i = 0; i < a.Runs.Count; i++)
            {
                Assert.That(b.Runs[i].EligibleAtSec, Is.EqualTo(a.Runs[i].EligibleAtSec), "run " + (i + 1));
                Assert.That(b.Runs[i].IpAwarded, Is.EqualTo(a.Runs[i].IpAwarded));
                Assert.That(b.Runs[i].Stats, Is.EqualTo(a.Runs[i].Stats));
                Assert.That(b.Runs[i].CheckpointFactoryValue, Is.EqualTo(a.Runs[i].CheckpointFactoryValue));
                Assert.That(b.Runs[i].Rows.Select(r => string.Join(",", r)), Is.EqualTo(a.Runs[i].Rows.Select(r => string.Join(",", r))));
            }
            Assert.That(a.Violations, Is.Empty);
        }

        [Test]
        public void OffsetZeroOfTheChainIsTheReferencePolicyTrace()
        {
            // The strategy runner at offset 0 plays the same trace as the plain reference policy: no behaviour change.
            RunRecord plain = new BalanceTraceRunner(Catalog(), new ReferencePolicy()).PlayRun("run1", 1, false, -1, null, 0, 1_200);
            RunRecord viaEnsemble = StrategyEnsemble.PlayHistory(Catalog(), Strategies.Chain, 0, 1, 1_200).Runs[0];

            Assert.That(viaEnsemble.Rows.Select(r => string.Join(",", r.Skip(1))), Is.EqualTo(plain.Rows.Select(r => string.Join(",", r.Skip(1)))));
            Assert.That(viaEnsemble.Stats, Is.EqualTo(plain.Stats));
        }

        [Test]
        public void TheOffsetMovesTheFirstLookAndTheCheckpointsAreExactOnAnyGrid()
        {
            // Offset 0: the first look is at second 0, so the first Miner exists at second 8. Offset 8: nothing is built before second 8.
            var early = new BalanceTraceRunner(Catalog(), Strategies.Chain.Create());
            early.PlayRun("early", 1, false, -1, null, 0, 8);
            Assert.That(early.Context.Session.State.Machines.Count, Is.GreaterThan(0), "offset 0 looks at second 0");

            var late = new BalanceTraceRunner(Catalog(), Strategies.Chain.Create());
            late.PlayRun("late", 1, false, -1, null, 8, 8);
            Assert.That(late.Context.Session.State.Machines.Count, Is.EqualTo(0), "offset 8 has no look before second 8");

            // A 37 s grid does not contain second 600 or 900; the Factory Value is still read at exactly those seconds.
            var grid = new StrategyDefinition("grid", "test", () => new ReferencePolicy { Id = "grid", LookIntervalSeconds = 37 });
            RunRecord rec = StrategyEnsemble.PlayHistory(Catalog(), grid, 11, 1, 900).Runs[0];
            Assert.That(rec.CheckpointFactoryValue.Keys, Is.EqualTo(new[] { 600, 900 }));
            Assert.That(rec.CheckpointFactoryValue[900], Is.GreaterThanOrEqualTo(rec.CheckpointFactoryValue[600]));
        }

        [Test]
        public void EveryStrategyPlaysFiveMinutesWithoutAConservationViolation()
        {
            foreach (StrategyDefinition strategy in Strategies.All)
            {
                StrategyHistory h = StrategyEnsemble.PlayHistory(Catalog(), strategy, StrategyEnsemble.Offsets(strategy.Create().LookIntervalSeconds)[1], 1, 300);
                Assert.That(h.Violations, Is.Empty, strategy.Id);
                Assert.That(h.Runs[0].Stats.ContainsKey("commands"), Is.True, strategy.Id);
            }
            // Contracts-only never sells: no auto-sell is on, so its Cash only comes from the start and from deliveries.
            Assert.That(Strategies.ContractsOnly.Create().ContractsOnly, Is.True);
            Assert.That(Strategies.OreOnly.Create().OreOnly, Is.True);
        }

        // ---- the matrix (explicit) ---------------------------------------------------------------------------

        /// <summary>
        /// Plays the whole matrix (5 strategies x 5 offsets x runs 1 to 5), searches the casual look interval, measures the runtime
        /// and composes the report. The report file is written only when <c>IDLEFACTORY_WRITE_STRATEGY_BASELINE=1</c>; otherwise the test
        /// only asserts the conservation of all 25 histories and passes.
        /// </summary>
        [Test]
        public void WriteStrategyBaselineReport()
        {
            RunCatalog catalog = Catalog();
            CasualCalibration calibration = StrategyBaselineReport.FindCasualLookInterval(catalog);
            Dictionary<string, List<StrategyHistory>> matrix = StrategyBaselineReport.PlayMatrix(catalog);
            foreach (List<StrategyHistory> histories in matrix.Values)
                foreach (StrategyHistory h in histories) Assert.That(h.Violations, Is.Empty, h.StrategyId + " offset " + h.OffsetSec);

            string repoRoot = Directory.GetCurrentDirectory();
            string report = StrategyBaselineReport.Compose(catalog, calibration, matrix, StrategyBaselineReport.BuildCommit(repoRoot), ContentPath);
            TestContext.Out.WriteLine(report);
            TestContext.Progress.WriteLine("Casual look interval found by the search: " + calibration.LookIntervalSeconds + " s (pinned: " + ReferencePolicy.CasualLookIntervalSeconds + " s)");
            if (Environment.GetEnvironmentVariable(WriteVariable) != "1") Assert.Pass("The report was composed but not written; set " + WriteVariable + "=1 to write " + ReportPath + ".");

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllBytes(ReportPath, new UTF8Encoding(false).GetBytes(report.Replace("\r\n", "\n")));
            Assert.Pass("Wrote " + ReportPath + ".");
        }
    }
}
