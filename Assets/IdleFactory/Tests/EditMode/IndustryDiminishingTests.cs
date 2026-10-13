using System;
using System.Linq;
using System.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// The per-kind diminishing Industry tables (T331, F5-R08, ECO-7): the multiplicative increment, the discount decrement and the additive
    /// amount decay with iterated floors; the cumulative fold keeps the per-level floor (speed, sale price) or ceiling (cost); a decay of 0 or
    /// 10^6 keeps the legacy fold exactly; the practical ceiling is the first level below <c>minGainPpm</c>.
    /// </summary>
    public sealed class IndustryDiminishingTests
    {
        private const string Speed = "industry.speed";
        private const string Cost = "industry.cost";
        private const string Price = "industry.price";
        private const string Cash = "industry.cash";
        private const string Storage = "industry.storage";
        private const string Legacy = "industry.legacy";

        private static IndustryUpgradeDef Def(string id, int maxLevel, EffectDef effect, long decayPpm) =>
            new IndustryUpgradeDef(id, id, maxLevel, new Curve(1, 1_000_000, "ceil"), effect, decayPpm);

        private static IndustryUpgradeDef[] Upgrades() => new[]
        {
            Def(Speed, 4, new EffectDef("machineSpeed", "allMachines", null, 1_250_000), 800_000),
            Def(Cost, 3, new EffectDef("upgradeCost", "allMachines", null, 800_000), 500_000),
            Def(Price, 3, new EffectDef("salePrice", "allResources", null, 1_100_000), 900_000),
            Def(Cash, 4, new EffectDef("startingCash", "none", null, 300), 950_000),
            Def(Storage, 3, new EffectDef("startingStorageLevel", "none", null, 2), 500_000),
            Def(Legacy, 3, new EffectDef("machineSpeed", "allMachines", null, 1_100_000), 0),
        };

        private static RunCommandFixtures.Rig Rig(params (string id, int level)[] levels)
        {
            var o = new RunCommandFixtures.Options { Industry = Upgrades() };
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(o, 100_000);
            rig.Advance(0);
            foreach (var (id, level) in levels)
                Assert.That(rig.Session.Permanent.TrySetIndustryLevel(id, level), Is.EqualTo(RunStateResult.Ok));
            return rig;
        }

        private static long[] OracleFactors(long value, long decayPpm, int levels, bool additive)
        {
            var result = new long[levels];
            BigInteger delta = additive ? value : BigInteger.Abs(value - 1_000_000);
            for (int i = 0; i < levels; i++)
            {
                result[i] = (long)(additive ? delta : value >= 1_000_000 ? 1_000_000 + delta : 1_000_000 - delta);
                delta = delta * decayPpm / 1_000_000;
            }

            return result;
        }

        [TestCase("machineSpeed", 1_250_000L, 800_000L, false)]
        [TestCase("salePrice", 1_190_000L, 950_000L, false)]
        [TestCase("upgradeCost", 639_000L, 700_000L, false)]
        [TestCase("startingCash", 300L, 950_000L, true)]
        [TestCase("startingStorageLevel", 1L, 999_999L, true)]
        [TestCase("machineSpeed", 1_000_001L, 123_456L, false)]
        public void TheValueTableFollowsTheIteratedFloorOracle(string kind, long value, long decayPpm, bool additive)
        {
            Assert.That(IndustryValueTable.TryBuild(kind, value, decayPpm, 40, out long[] table), Is.True);
            Assert.That(table, Is.EqualTo(OracleFactors(value, decayPpm, 40, additive)));
            Assert.That(additive ? table[0] : Math.Abs(table[0] - 1_000_000), Is.EqualTo(additive ? value : Math.Abs(value - 1_000_000)), "level 1 is undecayed");
        }

        [Test]
        public void PinnedTablesOfTheThreeKinds()
        {
            Assert.That(IndustryValueTable.TryBuild("machineSpeed", 1_250_000, 800_000, 4, out long[] mult), Is.True);
            Assert.That(mult, Is.EqualTo(new long[] { 1_250_000, 1_200_000, 1_160_000, 1_128_000 }), "increment 250,000 decays by 0.8");
            Assert.That(IndustryValueTable.TryBuild("upgradeCost", 800_000, 500_000, 3, out long[] discount), Is.True);
            Assert.That(discount, Is.EqualTo(new long[] { 800_000, 900_000, 950_000 }), "decrement 200,000 decays by 0.5");
            Assert.That(IndustryValueTable.TryBuild("startingCash", 300, 950_000, 4, out long[] additive), Is.True);
            Assert.That(additive, Is.EqualTo(new long[] { 300, 285, 270, 256 }), "iterated floors: 285 x 0.95 = 270.75, 270 x 0.95 = 256.5");
            Assert.That(IndustryValueTable.TryBuild("startingStorageLevel", 2, 500_000, 3, out long[] storage), Is.True);
            Assert.That(storage, Is.EqualTo(new long[] { 2, 1, 0 }));
        }

        [TestCase(0L)]
        [TestCase(1_000_000L)]
        public void NoDecayKeepsTheLegacyUniformTable(long decayPpm)
        {
            IndustryUpgradeDef def = Def("industry.x", 3, new EffectDef("machineSpeed", "allMachines", null, 1_100_000), decayPpm);
            Assert.That(def.IsDecayed, Is.False);
            Assert.That(def.ValueTable, Is.EqualTo(new long[] { 1_100_000, 1_100_000, 1_100_000 }));
            IndustryUpgradeDef cash = Def("industry.c", 3, new EffectDef("startingCash", "none", null, 100), decayPpm);
            Assert.That(cash.CumulativeAmount(0), Is.EqualTo(0));
            Assert.That(cash.CumulativeAmount(3), Is.EqualTo(300), "value x level, as before T331");
        }

        [Test]
        public void ADecayAboveOneMillionIsRejected()
        {
            Assert.That(() => Def("industry.x", 2, new EffectDef("startingCash", "none", null, 10), 1_000_001), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TheCumulativeAmountSumsTheDecayedLevels()
        {
            IndustryUpgradeDef cash = Upgrades().Single(u => u.Id == Cash);
            Assert.That(cash.IsDecayed, Is.True);
            Assert.That(new[] { 0, 1, 2, 3, 4, 9 }.Select(l => cash.CumulativeAmount(l)), Is.EqualTo(new long[] { 0, 300, 585, 855, 1111, 1111 }));
        }

        // ---- the folds -----------------------------------------------------------------------------------------

        [Test]
        public void TheSalePriceFoldMultipliesTheDecayedFactorsWithAFloorPerLevel()
        {
            var rig = Rig((Price, 3));
            var modifiers = new RunModifiers(rig.Catalog);
            Assert.That(modifiers.TrySalePricePpm(rig.State, rig.Session.Permanent, "iron.ore", out long ppm), Is.True);
            // 1,000,000 x 1.100 = 1,100,000; x 1.090 = 1,199,000; x 1.081 = 1,296,119 (floors are exact here).
            Assert.That(ppm, Is.EqualTo(1_296_119));
            rig.Session.Permanent.TrySetIndustryLevel(Price, 1);
            Assert.That(modifiers.TrySalePricePpm(rig.State, rig.Session.Permanent, "iron.ore", out ppm), Is.True);
            Assert.That(ppm, Is.EqualTo(1_100_000));
        }

        [Test]
        public void TheUpgradeCostFoldRoundsUpPerLevelWithTheDecayedDiscount()
        {
            var rig = Rig((Cost, 3));
            var modifiers = new RunModifiers(rig.Catalog);
            Assert.That(modifiers.TryUpgradeCost(rig.State, rig.Session.Permanent, 1_000, RunCommandFixtures.Miner, out long cost), Is.True);
            Assert.That(cost, Is.EqualTo(684), "1,000 x 0.80 = 800, x 0.90 = 720, x 0.95 = 684");
            rig.Session.Permanent.TrySetIndustryLevel(Cost, 2);
            Assert.That(modifiers.TryUpgradeCost(rig.State, rig.Session.Permanent, 1_000, RunCommandFixtures.Miner, out cost), Is.True);
            Assert.That(cost, Is.EqualTo(720));
            Assert.That(modifiers.TryUpgradeCost(rig.State, rig.Session.Permanent, 1, RunCommandFixtures.Miner, out cost), Is.True);
            Assert.That(cost, Is.EqualTo(1), "a positive cost never rounds down to 0");
        }

        [Test]
        public void TheMachineSpeedFoldMultipliesTheDecayedFactorsInLevelOrder()
        {
            var rig = Rig((Speed, 4));
            Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            MachineInstance miner = rig.State.FindMachine(rig.Id(1));
            var modifiers = new RunModifiers(rig.Catalog);
            Assert.That(modifiers.TryMachineSpeed(rig.State, rig.Session.Permanent, miner, false, null, out long speed), Is.True);
            // 1,000,000 x 1.25 = 1,250,000; x 1.20 = 1,500,000; x 1.16 = 1,740,000; x 1.128 = 1,962,720.
            Assert.That(speed, Is.EqualTo(1_962_720));
            rig.Session.Permanent.TrySetIndustryLevel(Speed, 3);
            Assert.That(modifiers.TryMachineSpeed(rig.State, rig.Session.Permanent, miner, false, null, out speed), Is.True);
            Assert.That(speed, Is.EqualTo(1_740_000));
        }

        [Test]
        public void AnUndecayedIndustrySpeedKeepsTheLegacyFold()
        {
            var rig = Rig((Legacy, 3));
            Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            MachineInstance miner = rig.State.FindMachine(rig.Id(1));
            Assert.That(new RunModifiers(rig.Catalog).TryMachineSpeed(rig.State, rig.Session.Permanent, miner, false, null, out long speed), Is.True);
            Assert.That(speed, Is.EqualTo(1_331_000), "1.1 three times with a floor at each step");
        }

        [Test]
        public void TheNextRunStartSumsTheDecayedStartingCashAndStorageLevels()
        {
            var rig = Rig((Cash, 3), (Storage, 3));
            NextRunStart start = NextRunStart.Compute(rig.Catalog, rig.Session.Permanent);
            Assert.That(start.StartingCash, Is.EqualTo(25 + 855), "the run start cash plus 300 + 285 + 270");
            Assert.That(start.StorageLevel, Is.EqualTo(1 + 2 + 1 + 0), "2 + 1 + 0 levels on top of level 1");
            rig.Session.Permanent.TrySetIndustryLevel(Cash, 0);
            Assert.That(NextRunStart.Compute(rig.Catalog, rig.Session.Permanent).StartingCash, Is.EqualTo(25));
        }

        // ---- the practical ceiling -----------------------------------------------------------------------------

        [TestCase(0L, 4)]
        [TestCase(100_000L, 4)]
        [TestCase(128_000L, 4)]
        [TestCase(128_001L, 4)]
        [TestCase(160_001L, 3)]
        [TestCase(210_000L, 2)]
        [TestCase(250_001L, 1)]
        public void TheCeilingOfAMultiplicativeKindIsTheFirstLevelBelowTheMinimumGain(long minGainPpm, int ceiling)
        {
            Assert.That(IndustryValueTable.TryBuild("machineSpeed", 1_250_000, 800_000, 4, out long[] table), Is.True);
            Assert.That(IndustryValueTable.PracticalCeiling("machineSpeed", table, minGainPpm), Is.EqualTo(ceiling));
        }

        [Test]
        public void TheCeilingOfAnAdditiveKindUsesTheAmountRelativeToLevelOne()
        {
            Assert.That(IndustryValueTable.TryBuild("startingCash", 300, 950_000, 4, out long[] table), Is.True);
            Assert.That(Enumerable.Range(1, 4).Select(l => IndustryValueTable.GainPpm("startingCash", table, l)),
                Is.EqualTo(new long[] { 1_000_000, 950_000, 900_000, 853_333 }));
            Assert.That(IndustryValueTable.PracticalCeiling("startingCash", table, 900_000), Is.EqualTo(4));
            Assert.That(IndustryValueTable.PracticalCeiling("startingCash", table, 5_000), Is.EqualTo(4), "no level below: the last level");
            Assert.That(IndustryValueTable.PracticalCeiling("startingCash", table, 960_000), Is.EqualTo(2));
        }

        [Test]
        public void ALevelThatRoundsToZeroHasNoGainAndEndsTheCeiling()
        {
            Assert.That(IndustryValueTable.TryBuild("startingStorageLevel", 1, 950_000, 3, out long[] table), Is.True);
            Assert.That(table, Is.EqualTo(new long[] { 1, 0, 0 }));
            Assert.That(IndustryValueTable.GainPpm("startingStorageLevel", table, 2), Is.EqualTo(0));
            Assert.That(IndustryValueTable.PracticalCeiling("startingStorageLevel", table, 5_000), Is.EqualTo(2));
        }
    }
}
