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
    /// The full strategy matrix of T341 (issue #406, balance-policy-r2.md section 8): the twelve strategy definitions of FR-023 and F5-R20, five look-grid
    /// offsets each, runs 1 to 5, with every FR-024 gate asserted on the medians (the content-tuned ones from <c>contentRevision</c> 3, see
    /// <see cref="TraceGates"/>). It is <c>[Category("BalanceFull")]</c> and stays out of the default run: it plays only when selected
    /// (<see cref="BalanceOptIn"/>) and passes at once otherwise. Run it with <c>scripts/test-unity.ps1 -Suite EditMode -Category BalanceFull</c>.
    /// With <c>IDLEFACTORY_WRITE_STRATEGY_BASELINE=1</c> it also searches the casual look interval and writes
    /// <c>specs/005-economy-contracts/reviews/strategy-baseline-r3.md</c>. The runtime budget is 20 minutes (PF-15).
    /// </summary>
    [Category("BalanceFull")]
    public sealed class BalanceTraceFullTests
    {
        private const string ContentPath = "Assets/IdleFactory/Content/Iron/iron-factory-content.json";
        private const string ReportPath = "specs/005-economy-contracts/reviews/strategy-baseline-r3.md";
        private const string WriteVariable = "IDLEFACTORY_WRITE_STRATEGY_BASELINE";
        private const double BudgetMinutes = 20.0;

        private static RunCatalog _catalog;
        private static byte[] _contentBytes;
        private static BalanceMatrix _matrix;
        private static List<GateResult> _gates;

        private static byte[] ContentBytes() => _contentBytes ?? (_contentBytes = File.ReadAllBytes(ContentPath));

        private static RunCatalog Catalog()
        {
            if (_catalog != null) return _catalog;
            var result = GameplayContentDispatcher.Load(ContentBytes());
            Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Code + " " + d.Path)));
            return _catalog = result.RunCatalog;
        }

        private static void RequireSelection()
        {
            if (!BalanceOptIn.BalanceFullSelected())
                Assert.Pass("BalanceFull is out of the default run; use scripts/test-unity.ps1 -Suite EditMode -Category BalanceFull (or " + BalanceOptIn.RunVariable + "=1).");
        }

        private static BalanceMatrix Matrix()
        {
            if (_matrix != null) return _matrix;
            _matrix = BalanceMatrix.Play(Catalog(), Strategies.All, StrategyEnsemble.OffsetCount, StrategyEnsemble.RunCount);
            return _matrix;
        }

        private static List<GateResult> Gates() => _gates ?? (_gates = TraceGates.Evaluate(Matrix(), ContentBytes()));

        [Test]
        public void TheFullMatrixPlaysTwelveStrategiesOverFiveOffsetsAndFiveRunsWithinTheBudget()
        {
            RequireSelection();
            BalanceMatrix matrix = Matrix();
            Assert.That(matrix.Histories.Keys, Is.EquivalentTo(Strategies.All.Select(s => s.Id)));
            foreach (StrategyDefinition s in Strategies.All)
            {
                Assert.That(matrix.Histories[s.Id], Has.Count.EqualTo(StrategyEnsemble.OffsetCount), s.Id);
                Assert.That(matrix.Histories[s.Id].Select(h => h.OffsetSec), Is.EqualTo(StrategyEnsemble.Offsets(s.Create().LookIntervalSeconds)), s.Id);
            }
            double minutes = matrix.WallMilliseconds / 60000.0;
            TestContext.Progress.WriteLine("BalanceFull: " + matrix.Histories.Count + " strategies in " + minutes.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " min (budget " + BudgetMinutes + " min).");
            Assert.That(minutes, Is.LessThanOrEqualTo(BudgetMinutes), "PF-15: BalanceFull stays within its 20-minute budget");
        }

        [Test]
        public void TheFullMatrixAssertsTheFr024GatesOnTheMedians()
        {
            RequireSelection();
            List<GateResult> gates = Gates();
            Assert.That(gates.Select(g => g.Id), Is.SupersetOf(new[]
            {
                "conservation", "sale-neutrality", "partition-invariance", "remove-rebuild", "eco-3", "ratio-bounds", "item-uptime", "start-affordability",
                "chain-dominance", "single-lever", "contracts-only-longer", "chain-within-6h", "ip-4x", "chain-plus-mining",
            }));
            foreach (GateResult g in gates) TestContext.Progress.WriteLine(g.ToString());
            GateAssert.AssertAll(gates, Catalog().ContentRevision);
        }

        [Test]
        public void TheMiningStrategyAndTheOrientationWarningsAreReportedNotAsserted()
        {
            RequireSelection();
            BalanceMatrix matrix = Matrix();
            // W-mining (P4b), W-later and W-market never fail a test (balance-policy-r2.md section 8.3); the report prints them.
            foreach (StrategyHistory h in matrix.Histories[Strategies.MiningOnly.Id])
                Assert.That(h.Violations, Is.Empty, "mining-only conserves");
            long miningOnly = matrix.Median(Strategies.MiningOnly.Id, 1);
            long chain = matrix.Median(Strategies.Chain.Id, 1);
            TestContext.Progress.WriteLine("W-mining: mining-only run 1 " + (miningOnly < 0 ? "not reached" : miningOnly + " s") + ", chain " + chain + " s");
            Assert.That(matrix.Histories[Strategies.ContractsOnly.Id].All(h => h.Runs[0].EligibleAtSec < 0), Is.True, "contracts-only stalls by construction (P4c)");
        }

        [Test]
        public void TheSensitivityTolerancesAreAtLeastAsWideAsTheMeasuredSpreadOfTheOffsets()
        {
            RequireSelection();
            // FR-025: a bound is a measurement tolerance, never narrower than the spread of the five look-grid offsets of the chain (in parts per 1000).
            List<StrategyHistory> chain = Matrix().Histories[Strategies.Chain.Id];
            long worstSpread = 0;
            for (int n = 1; n <= StrategyEnsemble.RunCount; n++)
            {
                var values = chain.Select(h => h.EligibleAt(n)).ToList();
                long median = StrategyEnsemble.Median(values);
                worstSpread = Math.Max(worstSpread, (values.Max() - values.Min()) * 1000 / Math.Max(median, 1));
            }
            TestContext.Progress.WriteLine("Chain spread over the five offsets: " + worstSpread + " per mille");
            foreach (long tolerance in new[]
            {
                BalanceBands.PriceBoundPermille, BalanceBands.ContractGrowthBoundPermille, BalanceBands.OverdriveGrowthBoundPermille,
                BalanceBands.AbsenceMaxSavingPermille, 1000 - BalanceBands.ReturnsMinPermille, BalanceBands.ReturnsMaxPermille - 1000,
            })
                Assert.That(tolerance, Is.GreaterThanOrEqualTo(worstSpread));
        }

        [Test]
        public void TheCasualPinIsTheFirstIntegerThatSatisfiesTheRuleOnPolicyR2()
        {
            RequireSelection();
            // Balance-policy-r2.md section 7.1: the pin is a constant of the policy; it is checked against the rule on the content it was found on
            // (revision 2 values) and only reported on any later revision, because it is never recalibrated.
            CasualCalibration calibration = StrategyBaselineReport.FindCasualLookInterval(Catalog());
            TestContext.Progress.WriteLine("Casual search: " + calibration.LookIntervalSeconds + " s, ratio " +
                ((double)calibration.CasualFactoryValue900 / calibration.ChainFactoryValue900).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
            if (Catalog().ContentRevision == 2) Assert.That(ReferencePolicy.CasualLookIntervalSeconds, Is.EqualTo(calibration.LookIntervalSeconds));
        }

        [Test]
        public void WriteStrategyBaselineReport()
        {
            RequireSelection();
            if (Environment.GetEnvironmentVariable(WriteVariable) != "1") Assert.Pass("Set " + WriteVariable + "=1 to search the casual interval and write " + ReportPath + ".");
            CasualCalibration calibration = StrategyBaselineReport.FindCasualLookInterval(Catalog());
            string report = StrategyBaselineReport.Compose(Matrix(), Gates(), calibration, StrategyBaselineReport.BuildCommit(Directory.GetCurrentDirectory()), ContentPath);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllBytes(ReportPath, new UTF8Encoding(false).GetBytes(report.Replace("\r\n", "\n")));
            Assert.Pass("Wrote " + ReportPath + ".");
        }
    }
}
