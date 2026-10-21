using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Fixtures.Run
{
    /// <summary>Records the contract events of a session (T327); every other event only counts.</summary>
    internal sealed class ContractEventRecorder : IRunEventSink
    {
        /// <summary>The simulator-driven records (AutoDeliver, ProduceComplete, Expire, Blocked).</summary>
        public readonly List<RunContractEvent> Contracts = new List<RunContractEvent>();

        /// <summary>The command records (Accept, Deliver, Abandon; T364).</summary>
        public readonly List<RunContractEvent> Commands = new List<RunContractEvent>();
        public int Others;
        public void OnHeader(in RunHeaderEvent e) => Others++;
        public void OnSample(in RunSampleEvent e) => Others++;
        public void OnMilestone(in RunMilestoneEvent e) => Others++;
        public void OnSale(in RunSaleEvent e) => Others++;
        public void OnMining(in RunMiningEvent e) => Others++;
        public void OnContract(in RunContractEvent e)
        {
            if (e.Kind == RunContractEventKind.Accept || e.Kind == RunContractEventKind.Deliver || e.Kind == RunContractEventKind.Abandon) Commands.Add(e);
            else Contracts.Add(e);
        }
        public void OnPrestige(in RunPrestigeEvent e) => Others++;
        public void OnOfflineReturn(in RunOfflineReturnEvent e) => Others++;
        public void OnBottleneck(in RunBottleneckEvent e) => Others++;
        public void OnPurchase(in RunPurchaseEvent e) => Others++;
        public void OnClock(in RunClockEvent e) => Others++;
        public void OnLifecycle(in RunLifecycleEvent e) => Others++;

        public int Count(RunContractEventKind kind) => Contracts.Count(c => c.Kind == kind);
    }

    /// <summary>
    /// The shared fixture of the T327 suites (issue #392): a Miner that makes 2 Iron Ore every 2 s (completions at 2 s, 4 s, 6 s, ...), contract
    /// offers over Iron Ore (base price 1) and a ladder that grants one Boost at run index 0. A delivery of 5 units at ratio 1.5 pays 7 Cash
    /// (floor of 5 x 1.5), a production objective of 5 units at ratio 1.2 pays 6; both pay 2 RP.
    /// </summary>
    internal static class ContractFixtures
    {
        public const string Ore = "iron.ore";
        public const string Bar = "iron.bar";
        public const string Boost = "item.boost";
        public const string Deliver = "contract.deliver";
        public const string Produce = "contract.produce";
        public const string ProduceShort = "contract.produce-short";
        public const string ProduceTight = "contract.produce-tight";
        public const string BonusAlways = "contract.bonus-always";
        public const string BonusOre = "contract.bonus-ore";
        public const string BonusLevel = "contract.bonus-level";
        public const string BonusNever = "contract.bonus-never";
        public const long CooldownMs = 30_000;
        public const long ShortCooldownMs = 20_000;
        public const long BonusWindowMs = 90_000;
        public const long Second = RunSimHarness.TicksPerSecond;
        public const long StartUtcMs = RunSimHarness.StartUtcMs;

        public static ContractOfferDef[] Offers()
        {
            var always = new UnlockCondition("always");
            ContractRewardDef[] rp = { new ContractRewardDef("rp", new Curve(2, 1_000_000, "floor")) };
            return new[]
            {
                new ContractOfferDef(Deliver, "plate", Ore, new Curve(5, 1_010_000, "ceil"), null, rp, always, rewardRatioPpm: 1_500_000, cooldownMs: CooldownMs),
                new ContractOfferDef(Produce, "plate", Ore, new Curve(5, 1_000_000, "ceil"), null, rp, always, objective: "produce",
                    rewardRatioPpm: 1_200_000, cooldownMs: CooldownMs),
                new ContractOfferDef(ProduceShort, "timedBar", Ore, new Curve(5, 1_000_000, "ceil"), 6_000, rp, always, objective: "produce",
                    rewardRatioPpm: 1_200_000, cooldownMs: ShortCooldownMs),
                new ContractOfferDef(ProduceTight, "timedBar", Ore, new Curve(5, 1_000_000, "ceil"), 5_999, rp, always, objective: "produce",
                    rewardRatioPpm: 1_200_000, cooldownMs: ShortCooldownMs),
                // Bonus offers over Iron Ore deliveries: the trigger is what differs.
                Bonus(BonusAlways, new UnlockCondition("always")),
                Bonus(BonusOre, new UnlockCondition("resourceProduced", Ore)),
                Bonus(BonusLevel, new UnlockCondition("factoryLevelAtLeast", null, 2)),
                Bonus(BonusNever, new UnlockCondition("researchPurchased", "research.never")),
            };
        }

        private static ContractOfferDef Bonus(string id, UnlockCondition trigger)
        {
            ContractRewardDef[] rp = { new ContractRewardDef("rp", new Curve(1, 1_000_000, "floor")) };
            return new ContractOfferDef(id, "plate", Ore, new Curve(3, 1_000_000, "ceil"), null, rp, new UnlockCondition("always"),
                offerClass: "bonus", rewardRatioPpm: 1_800_000, cooldownMs: 10_000, bonus: new BonusDef(trigger, BonusWindowMs));
        }

        public static RunCommandFixtures.Options Options(ContractOfferDef[] offers = null) => new RunCommandFixtures.Options
        {
            Offers = offers ?? Offers(),
            Ladder = new ContractItemLadder(new[] { new ContractItemLadderEntry(0, Boost) }, 10),
            Items = new[]
            {
                new ItemDef(Boost, 1, "Boost", "F003-ITEM-BOOST", 60_000, "factory", new string[0],
                    new EffectDef("machineSpeed", "allMachines", null, 1_500_000)),
            },
        };

        /// <summary>A settled rig (the first settle unlocks the offers and fires the triggers that hold) that raises its events into <paramref name="sink"/>.</summary>
        public static RunCommandFixtures.Rig Rig(ContractEventRecorder sink = null, long cash = 1_000, RunCommandFixtures.Options options = null, bool settle = true)
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(options ?? Options(), cash);
            if (sink != null)
            {
                rig.Session = new RunSession(rig.Catalog, rig.State, new PermanentState(rig.Catalog), StartUtcMs, sink);
                rig.Commands = new RunCommands(rig.Session, () => RunSimHarness.Id(rig.NextId++));
            }
            if (settle) rig.Advance(0);
            return rig;
        }

        /// <summary>A rig with one Miner (completions every 2 s) and the offer accepted.</summary>
        public static RunCommandFixtures.Rig MiningRig(string offer, ContractEventRecorder sink = null, int miners = 1, long cash = 1_000)
        {
            RunCommandFixtures.Rig rig = Rig(sink, cash);
            for (int i = 0; i < miners; i++) RunSimHarness.Add(rig.State, RunCommandFixtures.Miner, i, 100 + i);
            if (offer != null) Accept(rig, offer);
            return rig;
        }

        public static void Accept(RunCommandFixtures.Rig rig, string offerId)
        {
            RunCommandResult<ContractAcceptResult> r = rig.Commands.AcceptContract(offerId);
            Assert.That(r.IsSuccess, Is.True, offerId + ": " + r.ReasonCode);
        }

        public static ContractBoard Board(RunCommandFixtures.Rig rig) => rig.State.Contracts;

        public static long Stock(RunCommandFixtures.Rig rig, string resourceId) => rig.State.Inventory.Units(resourceId);

        public static long Items(RunCommandFixtures.Rig rig) => rig.Session.Permanent.ItemCount(Boost);

        /// <summary>Advances <paramref name="total"/> ticks in one call or cut at the given offsets.</summary>
        public static void AdvanceCut(RunCommandFixtures.Rig rig, long total, params long[] cuts)
        {
            long at = 0;
            foreach (long cut in cuts.Where(c => c > 0 && c < total).Distinct().OrderBy(c => c).Concat(new[] { total }))
            {
                rig.Advance(cut - at);
                at = cut;
            }
        }

        public static readonly long[] BoundaryCuts =
        {
            1, 10_000, 19_999_999, 20_000_000, 33_333_333, 59_999_999, 60_000_000, 60_000_001, 60_009_999, 60_010_000, 60_010_001, 65_000_001,
        };

        /// <summary>Everything an advance can change about the run and its contracts, bonus state and last outcome, as text.</summary>
        public static string Digest(RunCommandFixtures.Rig rig)
        {
            RunState s = rig.State;
            ContractBoard b = s.Contracts;
            var sb = new StringBuilder(RunSimHarness.Fingerprint(s, rig.Session.Permanent));
            sb.Append("items=").Append(Items(rig)).Append(" total=").Append(b.RunCompletionTotal).Append('\n');
            foreach (string id in rig.Catalog.ContractOffers.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                BonusOfferState bonus = b.BonusState(id);
                sb.Append(id).Append(" n=").Append(b.CompletionCount(id)).Append(" ready=").Append(b.CooldownReadyAtUtcMs(id))
                    .Append(" bonus=").Append(bonus.Triggered).Append('/').Append(bonus.WindowEndUtcMs).Append('/').Append(bonus.Consumed).Append('\n');
            }
            sb.Append("active=").Append(b.Active == null ? "-" : b.Active.OfferId + "/auto=" + b.Active.AutoDeliver + "/blocked=" + b.Active.AutoDeliverBlocked +
                "/produced=" + b.Active.Produced).Append('\n');
            ContractOutcome o = b.LastOutcome;
            sb.Append("outcome=").Append(o.Kind).Append('/').Append(o.OfferId).Append('/').Append(o.CompletionIndex).Append('/').Append(o.EndedUtcMs)
                .Append('/').Append(o.CashReward).Append('/').Append(o.RpReward).Append('/').Append(o.ItemCount).Append(" serial=").Append(b.OutcomeSerial).Append('\n');
            sb.Append("expired=").Append(b.LastExpired.HasValue ? b.LastExpired.Value.OfferId + "@" + b.LastExpired.Value.DeadlineUtcMs : "-").Append('\n');
            sb.Append("now=").Append(rig.Session.TimelineUtcMs).Append(" ticks=").Append(rig.Session.ActiveTicksSinceAnchor);
            return sb.ToString();
        }
    }
}
