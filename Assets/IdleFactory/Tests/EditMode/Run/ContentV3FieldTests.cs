using System;
using IdleFactory.Run.Content;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>T312: schema v3 definition fields are carried by the definitions and the builder, with legacy defaults.</summary>
    public sealed class ContentV3FieldTests
    {
        [Test]
        public void LegacyDefaultsReproduceTodaysShape()
        {
            var catalog = RunTestCatalogs.IronLoop();
            Assert.That(catalog.RunStart.SellSteps, Is.Empty);
            Assert.That(catalog.RunStart.SellAll, Is.False);
            Assert.That(catalog.RunStart.LiveGapThresholdMs, Is.Null);
            Assert.That(catalog.RunStart.ManualMining, Is.Null);
            Assert.That(catalog.RunStart.MinRunDurationMs, Is.EqualTo(0));
            Assert.That(catalog.RunStart.UptimeMaxPpm, Is.EqualTo(0));
            foreach (var machine in catalog.Machines.Values) Assert.That(machine.BuildPrice, Is.Null);
            Assert.That(catalog.Prestige.Threshold, Is.Null);
            Assert.That(catalog.Milestones, Is.Empty);
            Assert.That(catalog.ContractItemLadder, Is.Null);
            Assert.That(catalog.ChainDominanceMarginPpm, Is.EqualTo(0));
        }

        [Test]
        public void OfferAndIndustryLegacyDefaults()
        {
            var offer = new ContractOfferDef("contract.a", "plate", "iron.plate", new Curve(5, 1_000_000, "ceil"), null,
                new[] { new ContractRewardDef("cash", new Curve(10, 1_000_000, "ceil")) }, new UnlockCondition("always"));
            Assert.That(offer.Objective, Is.EqualTo("deliver"));
            Assert.That(offer.Class, Is.EqualTo("standing"));
            Assert.That(offer.RewardRatioPpm, Is.EqualTo(0));
            Assert.That(offer.CooldownMs, Is.EqualTo(0));
            Assert.That(offer.Bonus, Is.Null);
            var upgrade = new IndustryUpgradeDef("industry.a", "A", 3, new Curve(1, 1_100_000, "ceil"),
                new EffectDef("machineSpeed", "allMachines", null, 1_100_000));
            Assert.That(upgrade.DecayPpm, Is.EqualTo(0));
            Assert.That(upgrade.PracticalCeilingLevel, Is.EqualTo(0));
            Assert.That(upgrade.MinGainPpm, Is.EqualTo(0));
        }

        [Test]
        public void BuilderCarriesV3FieldsThroughFrom()
        {
            var baseCatalog = RunTestCatalogs.IronLoop();
            var run = baseCatalog.RunStart;
            var runStart = new RunStartDef(run.StartingCash, run.InitialUnlocks, run.RemovalRefundPpm, run.MinCycleTicks,
                run.MaxCombinedSpeedPpm, run.MaxCombinedOutputFactor, run.MaxRewardRatioPpm,
                new long[] { 1, 10, 100 }, true, 60_000, new ManualMiningDef("iron.ore", 1000, 5, 30_000, 3, 1_000_000, 500_000), 600_000, 800_000);
            var ladder = new ContractItemLadder(new[] { new ContractItemLadderEntry(0, "item.drill"), new ContractItemLadderEntry(3, "item.drill") }, 2);
            var catalog = TestCatalogBuilder.From(baseCatalog).WithRunStart(runStart)
                .WithPrestige(new PrestigeDef(10_000_000, "sqrt", 10, 10_000_000, new ThresholdDef(1_000, 1_500_000, 20)))
                .WithMilestones(new[] { new MilestoneDef(250_000), new MilestoneDef(500_000) })
                .WithContractItemLadder(ladder).WithChainDominanceMarginPpm(100_000).Build();
            Assert.That(catalog.RunStart.SellSteps, Is.EqualTo(new long[] { 1, 10, 100 }));
            Assert.That(catalog.RunStart.SellAll, Is.True);
            Assert.That(catalog.RunStart.ManualMining.ResourceId, Is.EqualTo("iron.ore"));
            Assert.That(catalog.RunStart.UptimeMaxPpm, Is.EqualTo(800_000));
            Assert.That(catalog.Prestige.Threshold.MaxIndex, Is.EqualTo(20));
            Assert.That(catalog.Milestones, Has.Count.EqualTo(2));
            Assert.That(catalog.ContractItemLadder.PerRunCap, Is.EqualTo(2));
            Assert.That(catalog.ContractItemLadder.Entries[1].Index, Is.EqualTo(3));
            Assert.That(catalog.RunStart.LiveGapThresholdMs, Is.EqualTo(60_000));
            Assert.That(catalog.ChainDominanceMarginPpm, Is.EqualTo(100_000));
            Assert.That(catalog.Machines.Count, Is.EqualTo(baseCatalog.Machines.Count));
        }

        [Test]
        public void NestedRecordsRejectStructuralErrors()
        {
            Assert.Throws<ArgumentNullException>(() => new ContractItemLadder(null, 1));
            Assert.Throws<ArgumentNullException>(() => new BonusDef(null, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BuildPriceDef(-1, 1_000_000, 1_000_000, 5));
            Assert.Throws<ArgumentException>(() => new ContractOfferDef("contract.a", "plate", "iron.plate", new Curve(5, 1_000_000, "ceil"), null,
                new[] { new ContractRewardDef("cash", new Curve(10, 1_000_000, "ceil")) }, new UnlockCondition("always"), objective: "mine"));
        }
    }
}
