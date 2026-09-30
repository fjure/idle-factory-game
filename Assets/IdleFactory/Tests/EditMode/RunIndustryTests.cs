using System;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// PurchaseIndustry and the IndustrySnapshot (T101, contracts/run-application.md, FR-019, R12): IP spent once and
    /// permanently, run-scoped effects (machineSpeed, upgradeCost, salePrice) applied now, startingCash and
    /// startingStorageLevel only recorded for the next run start, level cap, IP check, atomic rejection with the
    /// structural flags.
    ///
    /// Fixture upgrades: industry.speed (machineSpeed x1.1, 3 levels, 3/5/7 IP by Curve(3, 1.5, ceil) ... rounded up),
    /// industry.cost (upgradeCost allMachines x0.5, 2 levels), industry.price (salePrice iron.ore x2, 2 levels),
    /// industry.cash (startingCash +100 per level, 3 levels), industry.storage (startingStorageLevel +1 per level,
    /// 2 levels), industry.huge (a speed factor that overflows at level 2).
    /// </summary>
    public sealed class RunIndustryTests
    {
        private const string SpeedUp = "industry.speed";
        private const string CostDown = "industry.cost";
        private const string Price = "industry.price";
        private const string Cash = "industry.cash";
        private const string Storage = "industry.storage";
        private const string Huge = "industry.huge";

        private static IndustryUpgradeDef[] Industry() => new[]
        {
            new IndustryUpgradeDef(SpeedUp, "Speed", 3, new Curve(3, 1_500_000, "ceil"), new EffectDef("machineSpeed", "allMachines", null, 1_100_000)),
            new IndustryUpgradeDef(CostDown, "Cost", 2, new Curve(2, 1_000_000, "ceil"), new EffectDef("upgradeCost", "allMachines", null, 500_000)),
            new IndustryUpgradeDef(Price, "Price", 2, new Curve(2, 1_000_000, "ceil"), new EffectDef("salePrice", "resource", "iron.ore", 2_000_000)),
            new IndustryUpgradeDef(Cash, "Cash", 3, new Curve(2, 1_000_000, "ceil"), new EffectDef("startingCash", "none", null, 100)),
            new IndustryUpgradeDef(Storage, "Storage", 2, new Curve(2, 1_000_000, "ceil"), new EffectDef("startingStorageLevel", "none", null, 1)),
            new IndustryUpgradeDef(Huge, "Huge", 3, new Curve(1, 1_000_000, "ceil"), new EffectDef("machineSpeed", "allMachines", null, 100_000_000_000_000L)),
        };

        private static RunCommandFixtures.Rig Rig(long ip = 100, bool withMiner = false)
        {
            var o = new RunCommandFixtures.Options { Industry = Industry() };
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(o, 100_000);
            rig.Session.Permanent.TryAddIp(ip);
            rig.Advance(0);
            if (withMiner) Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            return rig;
        }

        private static PermanentState Perm(RunCommandFixtures.Rig rig) => rig.Session.Permanent;

        private static IndustryPurchaseResult Buy(RunCommandFixtures.Rig rig, string id)
        {
            RunCommandResult<IndustryPurchaseResult> r = rig.Commands.PurchaseIndustry(id);
            Assert.That(r.IsSuccess, Is.True, id + ": " + r.ReasonCode);
            return r.Value;
        }

        private static string Full(RunCommandFixtures.Rig rig) =>
            RunCommandFixtures.Snapshot(rig.State) + "cash=" + rig.State.Cash + " ip=" + Perm(rig).Ip +
            " industry=" + string.Join(",", Perm(rig).IndustryLevels().Select(i => i.Id + ":" + i.Count)) + " tl=" + rig.Session.ActiveTicksSinceAnchor;

        private static void AssertRejected<T>(RunCommandFixtures.Rig rig, Func<RunCommandResult<T>> command, string code)
        {
            string before = Full(rig);
            long structure = rig.Session.StructureRevision, quantity = rig.Session.QuantityRevision;

            RunCommandResult<T> result = command();

            Assert.That(result.IsSuccess, Is.False, "expected " + code);
            Assert.That(result.ReasonCode, Is.EqualTo(code));
            Assert.That(result.Value, Is.EqualTo(default(T)));
            Assert.That(Full(rig), Is.EqualTo(before), "a rejection never changes state");
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(structure));
            Assert.That(rig.Session.QuantityRevision, Is.EqualTo(quantity));
        }

        [Test]
        public void Purchase_SpendsIpPermanently_AndRaisesTheLevel()
        {
            var rig = Rig(ip: 20);
            long structure = rig.Session.StructureRevision;

            IndustryPurchaseResult r = Buy(rig, SpeedUp);

            Assert.That(r.NewLevel, Is.EqualTo(1));
            Assert.That(r.IpPaid, Is.EqualTo(3));
            Assert.That(r.HasNext, Is.True);
            Assert.That(r.NextCost, Is.EqualTo(5), "ceil(3 x 1.5)");
            Assert.That(r.AppliesNextRun, Is.False);
            Assert.That(Perm(rig).Ip, Is.EqualTo(17));
            Assert.That(Perm(rig).IndustryLevel(SpeedUp), Is.EqualTo(1));
            Assert.That(rig.Session.StructureRevision, Is.GreaterThan(structure), "IP and levels are structural facts of the Industry surface");
        }

        [Test]
        public void Purchase_MachineSpeed_AppliesNow_WithARescale()
        {
            var rig = Rig(withMiner: true);
            rig.Advance(10_000_000);
            MachineInstance miner = rig.State.FindMachine(rig.Id(1));

            Buy(rig, SpeedUp);

            Assert.That(miner.CycleDurationTicks, Is.EqualTo(18_181_819), "ceil(20,000,000 x 10^6 / 1,100,000)");
            Assert.That(miner.ProgressTicks, Is.EqualTo(9_090_909), "floor(10,000,000 x 18,181,819 / 20,000,000)");
        }

        [Test]
        public void Purchase_UpgradeCost_LowersTheNextBuild()
        {
            var rig = Rig();
            Buy(rig, CostDown);

            RunCommandResult<BuildResult> built = rig.Build(0, RunCommandFixtures.Miner);

            Assert.That(built.Value.CostPaid, Is.EqualTo(5), "the 10 Cash miner at x0.5");
        }

        [Test]
        public void Purchase_SalePrice_RaisesTheAutoSellIncome_FromNow()
        {
            var rig = Rig();
            Buy(rig, Price);
            RunSimHarness.Stock(rig.State, "iron.ore", 10);
            long cash = rig.State.Cash;

            Assert.That(rig.Commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True, "enabling sells the stock at once");

            Assert.That(rig.State.Cash - cash, Is.EqualTo(20), "10 ore x base price 1 x 2");
        }

        [Test]
        public void Purchase_StartingEffects_OnlyChangeThePermanentLevel_AndExposeTheNextRun()
        {
            var rig = Rig();
            long cash = rig.State.Cash;
            int storage = rig.State.StorageLevel;
            IndustrySnapshot before = RunSnapshots.Industry(rig.Session);
            Assert.That(before.NextRunStartingCash, Is.EqualTo(25), "the catalog's starting Cash");
            Assert.That(before.NextRunStorageLevel, Is.EqualTo(1));

            Assert.That(Buy(rig, Cash).AppliesNextRun, Is.True);
            Buy(rig, Cash);
            Assert.That(Buy(rig, Storage).AppliesNextRun, Is.True);
            Buy(rig, Storage);

            Assert.That(rig.State.Cash, Is.EqualTo(cash), "the running run is unchanged");
            Assert.That(rig.State.StorageLevel, Is.EqualTo(storage));
            IndustrySnapshot after = RunSnapshots.Industry(rig.Session);
            Assert.That(after.NextRunStartingCash, Is.EqualTo(25 + 2 * 100));
            Assert.That(after.NextRunStorageLevel, Is.EqualTo(3), "1 + 2 levels");
            Assert.That(after.Upgrades.Single(u => u.UpgradeId == Cash).AppliesNextRun, Is.True);
            Assert.That(after.Upgrades.Single(u => u.UpgradeId == SpeedUp).AppliesNextRun, Is.False);
        }

        [Test]
        public void Purchase_AtTheMaximumLevel_IsLevelMax()
        {
            var rig = Rig();
            Buy(rig, CostDown);
            Buy(rig, CostDown);

            AssertRejected(rig, () => rig.Commands.PurchaseIndustry(CostDown), RunReasons.LevelMax);
        }

        [Test]
        public void Purchase_WithTooLittleIp_IsIpInsufficient_AtTheBoundaryOfTheCost()
        {
            var rig = Rig(ip: 2);

            AssertRejected(rig, () => rig.Commands.PurchaseIndustry(SpeedUp), RunReasons.IpInsufficient);
            Perm(rig).TryAddIp(1);
            Assert.That(rig.Commands.PurchaseIndustry(SpeedUp).IsSuccess, Is.True, "exactly the cost is enough");
            Assert.That(Perm(rig).Ip, Is.Zero);
        }

        [Test]
        public void Purchase_UnknownUpgrade_IsCommandInvalid()
        {
            var rig = Rig();

            AssertRejected(rig, () => rig.Commands.PurchaseIndustry("industry.unknown"), RunReasons.CommandInvalid);
            AssertRejected(rig, () => rig.Commands.PurchaseIndustry(null), RunReasons.CommandInvalid);
        }

        [Test]
        public void Purchase_LifecycleBlocked_IsRejectedFirst()
        {
            var rig = Rig();
            rig.Session.SetLifecycleBlocked(true);

            AssertRejected(rig, () => rig.Commands.PurchaseIndustry(SpeedUp), RunReasons.LifecycleBlocked);
            AssertRejected(rig, () => rig.Commands.PurchaseIndustry("industry.unknown"), RunReasons.LifecycleBlocked);
        }

        [Test]
        public void Purchase_WhenTheComposedSpeedOverflows_IsValueLimit_AndTheTrialIsUndone()
        {
            var rig = Rig(withMiner: true);
            Buy(rig, Huge);

            AssertRejected(rig, () => rig.Commands.PurchaseIndustry(Huge), RunReasons.ValueLimit);
            Assert.That(Perm(rig).IndustryLevel(Huge), Is.EqualTo(1));
        }

        [Test]
        public void IndustrySnapshot_ListsUpgradesWithCostsReasonsAndTheIpBalance()
        {
            var rig = Rig(ip: 3);
            Buy(rig, SpeedUp);

            IndustrySnapshot s = RunSnapshots.Industry(rig.Session);

            Assert.That(s.Ip, Is.Zero);
            Assert.That(s.Upgrades.Select(u => u.UpgradeId), Is.EqualTo(new[] { CostDown, Cash, Huge, Price, SpeedUp, Storage }.OrderBy(x => x, StringComparer.Ordinal)));
            IndustryUpgradeSnapshot speed = s.Upgrades.Single(u => u.UpgradeId == SpeedUp);
            Assert.That((speed.Level, speed.MaxLevel, speed.NextCost), Is.EqualTo((1, 3, 5L)));
            Assert.That(speed.CanPurchase, Is.False);
            Assert.That(speed.PurchaseReason, Is.EqualTo(RunReasons.IpInsufficient));
            Assert.That(speed.EffectKind, Is.EqualTo("machineSpeed"));
        }
    }
}
