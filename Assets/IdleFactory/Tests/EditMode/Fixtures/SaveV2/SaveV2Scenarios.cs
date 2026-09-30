using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>One committed Save V2 fixture: a scenario, its bytes, and (for a corrupt fixture) the expected rejection code.</summary>
    internal sealed class SaveV2Fixture
    {
        public string FileName;
        public string Scenario;
        public RunCatalog Catalog;
        public SaveV2Meta Meta;
        public RunState Run;
        public PermanentState Permanent;
        public byte[] Bytes;

        /// <summary>Null for a valid fixture; the code the mapper must reject a corrupt fixture with.</summary>
        public string ExpectedCode;

        public bool IsCorrupt => ExpectedCode != null;
    }

    /// <summary>
    /// The deterministic in-memory sources of every Save V2 fixture (T104): a fresh run, a mid-run with every state
    /// family populated, the pending and the post-prestige states, the three W003 workloads and one corrupt file per
    /// cross-field bound. Used by <see cref="SaveV2FixtureTests"/> (byte compare against the committed files) and by
    /// <see cref="SaveV2MapperTests"/>.
    /// </summary>
    internal static class SaveV2Scenarios
    {
        public const string Boost = "item.boost";
        public const string Speed = "research.speed";
        public const string Plate = "contract.plate";
        public const string TimedBar = "contract.timed-bar";
        public const string IndustryCash = "industry.cash";
        public const string IndustryStorage = "industry.storage";
        public const string IndustrySpeed = "industry.speed";

        public static Guid Uuid(int n) => new Guid("22222222-2222-2222-2222-" + n.ToString("D12"));

        public static RunCommandFixtures.Options Options() => new RunCommandFixtures.Options
        {
            Items = new[] { new ItemDef(Boost, 1, "Boost", "F003-ITEM-BOOST", 60_000, "factory", new string[0], new EffectDef("machineSpeed", "allMachines", null, 1_500_000)) },
            Offers = new[]
            {
                new ContractOfferDef(Plate, "plate", "iron.plate", new Curve(5, 1_010_000, "ceil"), null,
                    new[] { new ContractRewardDef("cash", new Curve(100, 1_010_000, "floor")) }, new UnlockCondition("always")),
                new ContractOfferDef(TimedBar, "timedBar", "iron.bar", new Curve(10, 1_010_000, "ceil"), 60_000,
                    new[] { new ContractRewardDef("cash", new Curve(200, 1_000_000, "floor")) }, new UnlockCondition("always")),
            },
            Research = new[]
            {
                new ResearchDef(Speed, "Speed", 3, new Curve(5, 1_500_000, "ceil"), new ResearchPrerequisite[0],
                    new EffectDef("machineSpeed", "allMachines", null, 1_100_000)),
            },
            Industry = new[]
            {
                new IndustryUpgradeDef(IndustryCash, "Cash", 3, new Curve(2, 1_000_000, "ceil"), new EffectDef("startingCash", "none", null, 100)),
                new IndustryUpgradeDef(IndustryStorage, "Storage", 2, new Curve(2, 1_000_000, "ceil"), new EffectDef("startingStorageLevel", "none", null, 1)),
                new IndustryUpgradeDef(IndustrySpeed, "Speed", 3, new Curve(3, 1_500_000, "ceil"), new EffectDef("machineSpeed", "allMachines", null, 1_100_000)),
            },
        };

        public static SaveV2Meta Meta(RunCatalog catalog, int checkpoint, long generation, long departureUtcMs, Guid? reconciledFrom = null,
            SaveV2Import import = null)
        {
            return new SaveV2Meta(Uuid(checkpoint), generation, departureUtcMs, catalog.OfflinePolicy.Revision, reconciledFrom,
                catalog.ContentRevision, import);
        }

        public static RunCommands NewCommands(RunCommandFixtures.Rig rig) =>
            new RunCommands(rig.Session, () => RunSimHarness.Id(rig.NextId++), () => RunSimHarness.Id(7777));

        private static void Ok<T>(RunCommandResult<T> result)
        {
            Assert.That(result.IsSuccess, Is.True, result.ReasonCode);
        }

        /// <summary>The fresh run of the content: run 1, starting Cash, nothing built, settled once.</summary>
        public static SaveV2Fixture FreshRun()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(Options(), 25);
            rig.Advance(0);
            return Serialized("01-fresh-run.json",
                "A fresh first run: run 1, starting Cash 25, storage and factory level 1, no machine, no stock, the initial unlock set " +
                "after its first settle (T140), no contract, effect or Overdrive state, no Industry or Items.",
                rig.Catalog, Meta(rig.Catalog, 1, 0, rig.Session.TimelineUtcMs), rig.State, rig.Session.Permanent);
        }

        /// <summary>The mid-run, the pending prestige candidate and the post-prestige state of one played game.</summary>
        public static List<SaveV2Fixture> PlayedGame()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(Options(), 50_000_000);
            rig.Advance(0);
            RunCommands c = NewCommands(rig);
            RunSession session = rig.Session;
            RunCatalog catalog = rig.Catalog;
            Ok(rig.Build(0, RunCommandFixtures.Miner));
            Ok(rig.Build(1, RunCommandFixtures.Smelter));
            Ok(c.SetAutoSell("iron.ore", true));
            Ok(c.UpgradeStorage());
            Ok(c.UpgradeFactory());
            Assert.That(rig.State.TryAddRp(50), Is.EqualTo(RunStateResult.Ok));
            Ok(c.PurchaseResearch(Speed));
            Assert.That(session.Permanent.TryAddItems(Boost, 3), Is.EqualTo(RunStateResult.Ok));
            Assert.That(session.Permanent.TryAddIp(7), Is.EqualTo(RunStateResult.Ok));
            Assert.That(session.Permanent.TrySetIndustryLevel(IndustryCash, 2), Is.EqualTo(RunStateResult.Ok));
            Assert.That(session.Permanent.TrySetIndustryLevel(IndustryStorage, 1), Is.EqualTo(RunStateResult.Ok));
            Assert.That(session.Permanent.TrySetIndustryLevel(IndustrySpeed, 1), Is.EqualTo(RunStateResult.Ok));
            Ok(c.ActivateItem(Boost, null));
            Ok(c.ActivateOverdrive());
            // One delivered untimed contract (a completion count and a cash reward), then a timed one stays active.
            Assert.That(rig.State.Inventory.TryImport(new[]
            {
                new ResourceAmount("iron.plate", 5), new ResourceAmount("iron.bar", 12), new ResourceAmount("iron.ore", 30), new ResourceAmount("iron.gear", 3),
            }), Is.EqualTo(RunStateResult.Ok));
            Ok(c.AcceptContract(Plate));
            Ok(c.DeliverContract());
            Ok(c.AcceptContract(TimedBar));
            rig.Advance(30_000_000);

            Assert.That(rig.State.Machines.Any(m => m.ProgressTicks > 0 && m.ProgressTicks < m.CycleDurationTicks), Is.True, "a mid-cycle machine");
            Assert.That(rig.State.Effects.Count, Is.EqualTo(1));
            Assert.That(rig.State.Contracts.Active, Is.Not.Null);
            Assert.That(rig.State.Overdrive.ActiveUntilUtcMs.HasValue, Is.True);
            var list = new List<SaveV2Fixture>();
            list.Add(Serialized("02-mid-run-all-families.json",
                "A mid-run after a bit of play with every state family populated: two machines (a mid-cycle Miner and a Smelter), " +
                "stock, storage and factory level 2 with invested Cash, Research level 1, auto-sell on iron.ore, an active factory " +
                "Item effect, Overdrive active with its cooldown, one completed untimed contract and one active timed contract, " +
                "Industry levels, Items and IP in the permanent state, and reconciledFromCheckpointId set.",
                catalog, Meta(catalog, 2, 7, session.TimelineUtcMs, Uuid(1)), rig.State, session.Permanent));

            Ok(c.ConfirmPrestige());
            PrestigeCandidate candidate = session.PendingTransition;
            Assert.That(candidate, Is.Not.Null);
            list.Add(Serialized("03-pending-prestige.json",
                "The pending prestige candidate of the mid-run above (the RunState and PermanentState pair that is promoted): run 2 with " +
                "previousRunId set to the old run, prestigeCount 1 and the awarded IP, Items and Industry kept, everything else reset.",
                catalog, Meta(catalog, 3, 8, candidate.ConfirmedUtcMs), candidate.Run, candidate.Permanent));

            Ok(c.PromotePrestige());
            list.Add(Serialized("04-post-prestige.json",
                "The state after the prestige candidate was promoted: run 2 live, generation advanced, Industry starting effects applied.",
                catalog, Meta(catalog, 4, 9, session.TimelineUtcMs), session.State, session.Permanent));
            return list;
        }

        public static SaveV2Fixture W003(W003Variant variant, string fileName, string scenario)
        {
            W003Scenario sc = W003Workload.Build(variant);
            return Serialized(fileName, scenario, sc.Catalog, Meta(sc.Catalog, 10 + (int)variant, 0, sc.StartUtcMs), sc.State, sc.Permanent);
        }

        private static SaveV2Fixture Serialized(string fileName, string scenario, RunCatalog catalog, SaveV2Meta meta, RunState run,
            PermanentState permanent)
        {
            SaveV2WriteResult written = SaveV2Mapper.Serialize(meta, run, permanent);
            Assert.That(written.IsSuccess, Is.True, fileName + " " + written.Code);
            return new SaveV2Fixture
            {
                FileName = fileName,
                Scenario = scenario,
                Catalog = catalog,
                Meta = meta,
                Run = run,
                Permanent = permanent,
                Bytes = written.Bytes,
            };
        }

        /// <summary>Replaces the one occurrence of <paramref name="from"/> in the text of <paramref name="bytes"/>.</summary>
        public static byte[] Mutate(byte[] bytes, string from, string to)
        {
            string text = new UTF8Encoding(false, true).GetString(bytes);
            int at = text.IndexOf(from, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "mutation source not found: " + from);
            Assert.That(text.IndexOf(from, at + 1, StringComparison.Ordinal), Is.LessThan(0), "mutation source is not unique: " + from);
            return new UTF8Encoding(false).GetBytes(text.Substring(0, at) + to + text.Substring(at + from.Length));
        }

        private static SaveV2Fixture Corrupt(string fileName, string scenario, SaveV2Fixture basis, string from, string to)
        {
            return new SaveV2Fixture
            {
                FileName = fileName,
                Scenario = scenario,
                Catalog = basis.Catalog,
                Bytes = Mutate(basis.Bytes, from, to),
                ExpectedCode = SaveV2Codes.CrossField,
            };
        }

        /// <summary>Every fixture in README order: the valid ones, then one corrupt file per cross-field bound.</summary>
        public static List<SaveV2Fixture> All()
        {
            var list = new List<SaveV2Fixture>();
            list.Add(FreshRun());
            List<SaveV2Fixture> played = PlayedGame();
            list.AddRange(played);
            list.Add(W003(W003Variant.Bound, "05-w003-b-bound.json",
                "W003-B (bound): twelve machines at the event bound with balanced batches and auto-sell on Gear only; carries the event bound and " +
                "the SC-008 budgets (plan.md, Performance). One Item effect and Overdrive active for ten hours."));
            list.Add(W003(W003Variant.Stress, "06-w003-s-stress.json",
                "W003-S (auto-sell sanity): the same event-bound machines with auto-sell on every resource; exercises the auto-sell path only and " +
                "is not the event-bound record."));
            list.Add(W003(W003Variant.Reference, "07-w003-r-reference.json",
                "W003-R (reference): the provisional 120-minute reference state, auto-sell on Gear only, store at 50 percent."));

            SaveV2Fixture mid = played[0];
            SaveV2Fixture post = played[2];
            long departure = mid.Meta.DepartureUtcMs;
            ActiveContract contract = mid.Run.Contracts.Active;
            ActiveEffect effect = mid.Run.Effects.Effects[0];
            OverdriveDef overdrive = mid.Catalog.Overdrive;
            long activeUntil = mid.Run.Overdrive.ActiveUntilUtcMs.Value;
            long cooldownUntil = mid.Run.Overdrive.CooldownUntilUtcMs.Value;
            long contractDuration = contract.DeadlineUtcMs.Value - contract.AcceptedUtcMs;
            long effectDuration = effect.EndUtcMs - effect.StartUtcMs;

            list.Add(Corrupt("corrupt-01-contract-accepted-after-departure.json",
                "The mid-run with acceptedUtcMs one millisecond after the departure (the deadline moves with it): rejected, acceptedUtcMs must not be after departureUtcMs.",
                mid, "\"acceptedUtcMs\":" + contract.AcceptedUtcMs + ",\"deadlineUtcMs\":" + contract.DeadlineUtcMs.Value,
                "\"acceptedUtcMs\":" + (departure + 1) + ",\"deadlineUtcMs\":" + (departure + 1 + contractDuration)));
            list.Add(Corrupt("corrupt-02-effect-start-after-departure.json",
                "The mid-run with the effect startUtcMs one millisecond after the departure (the end moves with it): rejected, an effect must not start after departureUtcMs.",
                mid, "\"startUtcMs\":" + effect.StartUtcMs + ",\"endUtcMs\":" + effect.EndUtcMs,
                "\"startUtcMs\":" + (departure + 1) + ",\"endUtcMs\":" + (departure + 1 + effectDuration)));
            long lateActiveUntil = departure + overdrive.DurationMs + 1;
            list.Add(Corrupt("corrupt-03-overdrive-start-after-departure.json",
                "The mid-run with the Overdrive start (activeUntilUtcMs minus its duration) one millisecond after the departure (the cooldown moves with it): rejected.",
                mid, "\"activeUntilUtcMs\":" + activeUntil + ",\"cooldownUntilUtcMs\":" + cooldownUntil,
                "\"activeUntilUtcMs\":" + lateActiveUntil + ",\"cooldownUntilUtcMs\":" + (lateActiveUntil + overdrive.CooldownMs)));
            list.Add(Corrupt("corrupt-04-effect-duration-above-cap.json",
                "The mid-run with endUtcMs one millisecond beyond startUtcMs plus the seven-day cap: rejected, endUtcMs minus startUtcMs is at most MaxTimedDurationMs (owner decision 2026-10-01).",
                mid, "\"endUtcMs\":" + effect.EndUtcMs, "\"endUtcMs\":" + (effect.StartUtcMs + RunLimits.MaxTimedDurationMs + 1)));
            list.Add(Corrupt("corrupt-05-contract-duration-above-cap.json",
                "The mid-run with deadlineUtcMs one millisecond beyond acceptedUtcMs plus the seven-day cap: rejected, deadlineUtcMs minus acceptedUtcMs is at most MaxTimedDurationMs.",
                mid, "\"deadlineUtcMs\":" + contract.DeadlineUtcMs.Value, "\"deadlineUtcMs\":" + (contract.AcceptedUtcMs + RunLimits.MaxTimedDurationMs + 1)));
            list.Add(Corrupt("corrupt-06-overdrive-cooldown-above-cap.json",
                "The mid-run with cooldownUntilUtcMs one millisecond beyond activeUntilUtcMs plus the seven-day cap: rejected, cooldownUntilUtcMs minus activeUntilUtcMs is at most MaxTimedDurationMs.",
                mid, "\"cooldownUntilUtcMs\":" + cooldownUntil, "\"cooldownUntilUtcMs\":" + (activeUntil + RunLimits.MaxTimedDurationMs + 1)));
            list.Add(Corrupt("corrupt-07-run-number-mismatch.json",
                "The mid-run (prestigeCount 0) with runNumber 2: rejected, runNumber must equal prestigeCount plus 1.",
                mid, "\"runNumber\":1", "\"runNumber\":2"));
            list.Add(Corrupt("corrupt-08-previous-run-id-on-run-1.json",
                "The mid-run (run 1) with a previousRunId: rejected, previousRunId is absent exactly on run 1.",
                mid, "\"previousRunId\":null", "\"previousRunId\":\"" + Uuid(99).ToString("D") + "\""));
            list.Add(Corrupt("corrupt-09-previous-run-id-missing-after-prestige.json",
                "The post-prestige state (run 2) with previousRunId null: rejected, previousRunId must be present after run 1.",
                post, "\"previousRunId\":\"" + post.Run.PreviousRunId.Value.ToString("D") + "\"", "\"previousRunId\":null"));
            return list;
        }

        public static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
            }
        }

        /// <summary>The run and permanent state as text for round-trip comparisons (members Fingerprint and Snapshot do not print included).</summary>
        public static string Dump(RunState run, PermanentState permanent)
        {
            var b = new StringBuilder();
            b.Append(RunCommandFixtures.Snapshot(run));
            b.Append(RunSimHarness.Fingerprint(run, permanent));
            b.Append("run=").Append(run.RunId).Append('#').Append(run.RunNumber).Append(" prev=").Append(run.PreviousRunId).Append('\n');
            b.Append("invested=").Append(run.StorageInvestedCash).Append('/').Append(run.FactoryInvestedCash).Append('\n');
            b.Append("prestige=").Append(permanent.PrestigeCount).Append(" ip=").Append(permanent.Ip).Append('\n');
            b.Append("industry=").Append(string.Join(",", permanent.IndustryLevels().Select(x => x.Id + ":" + x.Count))).Append('\n');
            b.Append("items=").Append(string.Join(",", permanent.ItemStacks().Select(x => x.Id + ":" + x.Count))).Append('\n');
            if (run.Contracts.Active != null)
            {
                ActiveContract a = run.Contracts.Active;
                b.Append("active=").Append(a.OfferId).Append(' ').Append(a.AcceptedUtcMs).Append(' ').Append(a.DeadlineUtcMs).Append(' ').Append(a.RequiredUnits).Append('\n');
            }

            return b.ToString();
        }
    }
}
