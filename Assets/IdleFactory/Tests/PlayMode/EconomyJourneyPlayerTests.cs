using System.Linq;
using IdleFactory.Run.Application;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T348 (issue #413, plan step 17b): the F005 economy journey on real components in the built Windows player, over the packaged
    /// shipped content (revision 3) and the production <see cref="RunSession"/>, <see cref="RunCommands"/>, <see cref="RunPersistence"/> and the
    /// real <c>JsonRunCheckpointStore</c> file protocol: mine by hand, the first Miner, sell, a delivery contract with auto-deliver, a prestige at
    /// the rising threshold, the next run's higher build price and the first Miner again by mining. The assertions are shape only (orderings,
    /// signs and state transitions), never an exact balance number, so a content retune does not break them.
    /// The prestige threshold is the scaled one of the balance trace (ECO-3), as in <see cref="RunJourneyPlayerTests"/>, because the real
    /// threshold is not reached within a unit-test time budget; the commands, content and persistence are the shipped ones. The F004 chain
    /// journey stays in <see cref="RunJourneyPlayerTests"/>; this class adds the F005 economy path and does not repeat its Industry and restart steps.
    /// </summary>
    public sealed class EconomyJourneyPlayerTests
    {
        private const long Second = 10_000_000L;
        private const string BarContract = "contract.timed-bar";

        private string root;

        [SetUp]
        public void CreateRoot() => root = RunPlayerFixtures.NewRoot();

        [TearDown]
        public void DeleteRoot() => RunPlayerFixtures.DeleteRoot(root);

        private static void Advance(RunRig rig, long seconds)
        {
            var advanced = rig.Session.Advance(seconds * Second);
            Assert.That(advanced.IsSuccess, Is.True, "advance " + advanced.ReasonCode);
        }

        /// <summary>Mines at the rate cap for <paramref name="seconds"/> run seconds and returns the Ore the hand moved into stock.</summary>
        private static long MineBySeconds(RunRig rig, int seconds)
        {
            long ore = 0;
            for (int i = 0; i < seconds; i++)
            {
                var mined = rig.Commands.Mine(rig.Catalog.RunStart.ManualMining.MaxActivationsPerSecond);
                Assert.That(mined.IsSuccess, Is.True, "mine " + mined.ReasonCode);
                ore += mined.Value.Units;
                Advance(rig, 1);
            }

            return ore;
        }

        [Category("StandalonePlayer")]
        [Test]
        public void TheEconomyJourneyFromHandMiningThroughContractsAndPrestigeToTheSecondRunsFirstMiner()
        {
            var catalog = RunPlayerFixtures.ScaledCatalog();
            Assert.That(catalog.RunStart.ManualMining, Is.Not.Null, "the shipped content has manual mining (revision 3)");
            var rig = RunRig.Create(root, catalog, RunPlayerFixtures.StartUtcMs);
            rig.ColdStartOk(null);
            var commands = rig.Commands;
            var run1 = rig.Session.State;
            Assert.That(run1.Machines, Is.Empty, "a fresh run owns no machine");

            // ---- 1. Mine by hand, then sell the Ore ------------------------------------------------------------
            long ore = MineBySeconds(rig, 10);
            Assert.That(ore, Is.GreaterThan(0), "the hand moved Ore into stock");
            Assert.That(run1.Inventory.Units("iron.ore"), Is.EqualTo(ore));
            long cashBeforeSale = run1.Cash;
            var sold = commands.SellAll("iron.ore");
            Assert.That(sold.IsSuccess, Is.True, sold.ReasonCode);
            Assert.That(sold.Value.CashEarned, Is.GreaterThan(0), "selling the mined Ore earns Cash");
            Assert.That(run1.Cash, Is.EqualTo(cashBeforeSale + sold.Value.CashEarned));
            Assert.That(run1.Inventory.Units("iron.ore"), Is.Zero);

            // ---- 2. The first Miner ------------------------------------------------------------------------------
            long cashBeforeBuild = run1.Cash;
            var miner = commands.Build(0, "machine.miner");
            Assert.That(miner.IsSuccess, Is.True, miner.ReasonCode);
            long minerPriceRun1 = miner.Value.CostPaid;
            Assert.That(minerPriceRun1, Is.GreaterThan(0));
            Assert.That(run1.Cash, Is.EqualTo(cashBeforeBuild - minerPriceRun1));
            Assert.That(run1.Machines.Count, Is.EqualTo(1));

            // The Smelter unlocks when a machine has produced Ore.
            Advance(rig, 5);
            Assert.That(run1.IsUnlocked("machine.smelter"), Is.True, "the Miner's first Ore unlocks the Smelter");
            if (run1.Cash < 9) MineBySeconds(rig, 5);
            var smelter = commands.Build(1, "machine.smelter");
            Assert.That(smelter.IsSuccess, Is.True, smelter.ReasonCode);

            // ---- 3. Bars pile up unsold, so the delivery contract can be auto-delivered at once ---------------
            var offer = catalog.ContractOffers[BarContract];
            long required = offer.QuantityTable[0];
            for (int i = 0; i < 120 && run1.Inventory.Units("iron.bar") < required; i++) Advance(rig, 10);
            Assert.That(run1.Inventory.Units("iron.bar"), Is.GreaterThanOrEqualTo(required), "the chain stocked the contract quantity");
            Assert.That(run1.IsUnlocked(BarContract), Is.True, "the first Bar unlocks the Bar contract");

            long cashBeforeContract = run1.Cash;
            var accepted = commands.AcceptContract(BarContract);
            Assert.That(accepted.IsSuccess, Is.True, accepted.ReasonCode);
            var board = run1.Contracts;
            // An accepted delivery contract settles nothing by itself; auto-deliver delivers it at the same instant from the stock.
            var autoDeliver = commands.SetAutoDeliver(true);
            Assert.That(autoDeliver.IsSuccess, Is.True, autoDeliver.ReasonCode);
            Assert.That(board.Active, Is.Null, "auto-deliver completed the contract from the stock");
            Assert.That(board.CompletionCount(BarContract), Is.EqualTo(1));
            Assert.That(run1.Cash, Is.GreaterThan(cashBeforeContract), "the delivery paid its reward");

            // ---- 4. Play to the rising prestige threshold --------------------------------------------------------
            var policy = new RunJourneyPolicy(rig);
            long firstRunSeconds = policy.PlayUntilEligible();
            rig.SyncClock();
            var preview = commands.GetPrestigePreview();
            Assert.That(preview.IsSuccess && preview.Value.IsEligible, Is.True);
            long thresholdBefore = preview.Value.Threshold;
            long award = preview.Value.IpAward;
            Assert.That(award, Is.GreaterThan(0));
            var confirmed = commands.ConfirmPrestige();
            Assert.That(confirmed.IsSuccess, Is.True, confirmed.ReasonCode);
            var promoted = commands.PromotePrestige();
            Assert.That(promoted.IsSuccess, Is.True, promoted.ReasonCode);

            var run2 = rig.Session.State;
            Assert.That(run2.RunNumber, Is.EqualTo(2));
            Assert.That(rig.Session.Permanent.PrestigeCount, Is.EqualTo(1));
            Assert.That(rig.Session.Permanent.Ip, Is.EqualTo(award), "the award was paid once");
            Assert.That(run2.Machines, Is.Empty, "the new run starts without machines");
            long thresholdAfter = commands.GetPrestigePreview().Value.Threshold;
            Assert.That(thresholdAfter, Is.GreaterThan(thresholdBefore), "the threshold rises with every prestige");
            var durable = rig.Store.ReadActive();
            Assert.That(durable.Succeeded, Is.True, durable.Message);
            Assert.That(durable.Checkpoint.Run.RunId, Is.EqualTo(run2.RunId), "the new run is durable before it is live");

            // ---- 5. Run 2: the first Miner again, by mining, at a higher price ---------------------------------
            long oreRun2 = MineBySeconds(rig, 10);
            Assert.That(oreRun2, Is.GreaterThan(0), "mining works again in the new run");
            var soldRun2 = commands.SellAll("iron.ore");
            Assert.That(soldRun2.IsSuccess && soldRun2.Value.CashEarned > 0, Is.True, "the mined Ore sells in run 2");
            long cashBeforeBuildRun2 = run2.Cash;
            var minerRun2 = commands.Build(0, "machine.miner");
            Assert.That(minerRun2.IsSuccess, Is.True, minerRun2.ReasonCode);
            Assert.That(minerRun2.Value.CostPaid, Is.GreaterThan(minerPriceRun1), "a prestige raises the build price of the first Miner");
            Assert.That(run2.Cash, Is.EqualTo(cashBeforeBuildRun2 - minerRun2.Value.CostPaid));
            Assert.That(run2.Machines.Select(m => m.MachineId), Is.EqualTo(new[] { "machine.miner" }));

            Debug.Log("T348 economy journey: run 1 eligible after " + firstRunSeconds + " s, threshold " + thresholdBefore + " -> " + thresholdAfter +
                ", IP " + award + ", Miner price " + minerPriceRun1 + " -> " + minerRun2.Value.CostPaid + ".");
        }
    }
}
