using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// Semantic coverage for <see cref="RunContentValidator"/> (T096, issue #178; ported to schema v3 by T313, issue #375): one test
    /// case per rule code the validator implements (the v2 contract table minus the retired contract codes, plus the objective and class
    /// vocabularies), including the identity, structure and unlock codes (<see cref="RuleCases"/>), plus ordering, the cap of 100, echo bounds and the boundary behavior of the
    /// numeric rules. Every case starts from the guide example (<see cref="RunContentExample.Valid"/>) and
    /// applies one mutation, so a code that fires unexpectedly on the baseline fails the baseline test.
    /// </summary>
    public sealed class RunContentValidatorTests
    {
        private sealed class KeyResolver : IPresentationKeyResolver
        {
            private readonly HashSet<string> _unresolved;

            public KeyResolver(params string[] unresolved)
            {
                _unresolved = new HashSet<string>(unresolved, StringComparer.Ordinal);
            }

            public bool IsResolved(string presentationKey) => !_unresolved.Contains(presentationKey);
        }

        private static IReadOnlyList<ContentDiagnostic> Run(Action<JObject> mutate, IPresentationKeyResolver keys = null)
        {
            var document = RunContentExample.Valid();
            mutate?.Invoke(document);
            var read = RunContentReader.Read(RunContentExample.Bytes(document));
            if (!read.IsSuccess)
            {
                return new[] { read.Diagnostic };
            }

            return RunContentValidator.Validate(read.Document, keys);
        }

        private static string Describe(IEnumerable<ContentDiagnostic> diagnostics) =>
            string.Join(" | ", diagnostics.Select(d => d.Code + "@" + d.Path));

        private static TestCaseData Case(string code, string path, Action<JObject> mutate, IPresentationKeyResolver keys = null) =>
            new TestCaseData(code, path, mutate, keys).SetName(code);

        public static IEnumerable<TestCaseData> RuleCases()
        {
            yield return Case("content.version.unsupported", "/contentSchemaVersion", d => d["contentSchemaVersion"] = 2);

            yield return Case("runstart.unlock.unknown", "/runStart/initialUnlocks/0", d => d["runStart"]["initialUnlocks"] = new JArray("nothing.here"));
            yield return Case("runstart.refund.out-of-range", "/runStart/removalRefundPpm", d => d["runStart"]["removalRefundPpm"] = 1_000_000);
            yield return Case("runstart.min-cycle.invalid", "/runStart/minCycleTicks", d => d["runStart"]["minCycleTicks"] = 999_999);
            yield return Case("runstart.speed-bound.invalid", "/runStart/maxCombinedSpeedPpm", d => d["runStart"]["maxCombinedSpeedPpm"] = 999_999);
            yield return Case("runstart.output-bound.invalid", "/runStart/maxCombinedOutputFactor", d => d["runStart"]["maxCombinedOutputFactor"] = 1);
            yield return Case("runstart.reward-ratio.invalid", "/runStart/maxRewardRatioPpm", d => d["runStart"]["maxRewardRatioPpm"] = 999_999);
            yield return Case("sell.steps", "/runStart/sellSteps/1", d => d["runStart"]["sellSteps"] = new JArray(10, 10, "all"));
            yield return Case("live-gap.range", "/runStart/liveGapThresholdMs", d => d["runStart"]["liveGapThresholdMs"] = 999);

            yield return Case("resource.price.negative", "/resources/0/basePrice", d => d["resources"][0]["basePrice"] = -1);
            yield return Case("recipe.outputs.empty", "/recipes/0/outputs", d => d["recipes"][0]["outputs"] = new JArray());
            yield return Case("recipe.amount.non-positive", "/recipes/0/outputs/0/amount", d => d["recipes"][0]["outputs"][0]["amount"] = 0);
            yield return Case("recipe.duration.non-positive", "/recipes/0/durationTicks", d => d["recipes"][0]["durationTicks"] = 0);
            yield return Case("recipe.duration.out-of-range", "/recipes/0/durationTicks", d => d["recipes"][0]["durationTicks"] = 1_000_000_000_001L);
            yield return Case(
                "recipe.resource.duplicate",
                "/recipes/0/inputs/1/resourceId",
                d => ((JArray)d["recipes"][0]["inputs"]).Add(new JObject { ["resourceId"] = "iron.bar", ["amount"] = 3 }));
            yield return Case("ref.resource.unknown", "/recipes/0/inputs/0/resourceId", d => d["recipes"][0]["inputs"][0]["resourceId"] = "iron.missing");

            yield return Case("ref.recipe.unknown", "/machines/0/recipeId", d => d["machines"][0]["recipeId"] = "recipe.missing");
            yield return Case("machine.max-level.invalid", "/machines/1/maxLevel", d => d["machines"][1]["maxLevel"] = 0);
            yield return Case("machine.milestone.level-invalid", "/machines/1/milestones/1/level", d => d["machines"][1]["milestones"][1]["level"] = 26);
            yield return Case("machine.milestone.duplicate", "/machines/1/milestones/1/level", d => d["machines"][1]["milestones"][1]["level"] = 10);
            yield return Case("curve.growth.invalid", "/machines/1/cost/growthPpm", d => d["machines"][1]["cost"]["growthPpm"] = 999_999);
            yield return Case(
                "curve.overflow",
                "/machines/1/cost",
                d => d["machines"][1]["cost"] = new JObject { ["base"] = 1_000_000_000_000_000_000L, ["growthPpm"] = 2_000_000, ["rounding"] = "ceil" });
            yield return Case(
                "curve.stalled",
                "/machines/1/cost",
                d => d["machines"][1]["cost"] = new JObject { ["base"] = 1, ["growthPpm"] = 1_000_001, ["rounding"] = "floor" });
            yield return Case("speed.level.dead", "/machines/1/speedGrowthPpm", d => d["machines"][1]["speedGrowthPpm"] = 1_000_000);
            yield return Case("build.base.bound", "/machines/1/buildPrice/base", d => d["machines"][1]["buildPrice"]["base"] = 1_000_000_000_001L);
            yield return Case("cost.floor", "/machines/0", d => d["research"][2]["effect"]["valuePerLevelPpm"] = 500_000);
            yield return Case("prestige.threshold.bound", "/prestige/threshold/maxIndex", d => d["prestige"]["threshold"]["growthPpm"] = 100_000_000L);
            yield return Case("industry.ceiling.level", "/industryUpgrades/0/practicalCeilingLevel", d => d["industryUpgrades"][0]["practicalCeilingLevel"] = 5);
            yield return Case(
                "batch.overflow",
                "/machines/1",
                d => d["recipes"][1]["outputs"][0]["amount"] = 100_000_000_000_000_000L);

            yield return Case("level.sequence.invalid", "/storageLevels/1/level", d => d["storageLevels"][1]["level"] = 5);
            yield return Case("level.first-cost.non-zero", "/storageLevels/0/cost", d => d["storageLevels"][0]["cost"] = 1);
            yield return Case("storage.capacity.non-increasing", "/storageLevels/1/capacity", d => d["storageLevels"][1]["capacity"] = 1000);
            yield return Case("factory.slots.non-increasing", "/factoryLevels/1/slots", d => d["factoryLevels"][1]["slots"] = 4);
            yield return Case("factory.slots.too-many", "/factoryLevels/3/slots", d => d["factoryLevels"][3]["slots"] = 13);

            yield return Case("contract.objective.unknown", "/contracts/0/objective", d => d["contracts"][0]["objective"] = "mine");
            yield return Case("contract.class.unknown", "/contracts/0/class", d => d["contracts"][0]["class"] = "legendary");
            yield return Case("contract.duration.out-of-range", "/contracts/2/durationMs", d => d["contracts"][2]["durationMs"] = 604_800_001L);
            yield return Case("contract.reward.item.unknown", "/contractItemLadder/entries/0/itemId", d => d["contractItemLadder"]["entries"][0]["itemId"] = "item.missing");
            yield return Case("contract.reward.ratio", "/contracts/0/rewardRatioPpm", d => d["contracts"][0]["rewardRatioPpm"] = 999_999);
            yield return Case("contract.rp.growth", "/contracts/0/rpReward/growthPpm", d => d["contracts"][0]["rpReward"]["growthPpm"] = 1_020_000);
            yield return Case("contract.bonus.fields", "/contracts/3/bonus", d => ((JObject)d["contracts"][3]).Remove("bonus"));
            yield return Case("contract.ratio.standing", "/contracts/0/rewardRatioPpm", d => d["contracts"][0]["rewardRatioPpm"] = 1_500_001);
            yield return Case("contract.ratio.bonus", "/contracts/3/rewardRatioPpm", d =>
            {
                d["runStart"]["maxRewardRatioPpm"] = 3_000_000;
                d["contracts"][3]["rewardRatioPpm"] = 2_000_001;
            });
            yield return Case("contract.quantity.cap", "/contracts/0/quantity/base", d => d["contracts"][0]["quantity"]["base"] = 4_001);
            yield return Case("contract.produce.min", "/contracts/0/quantity/base", d =>
            {
                d["contracts"][0]["objective"] = "produce";
                d["contracts"][0]["quantity"]["base"] = 9;
            });

            yield return Case("item.tier.invalid", "/items/0/tier", d => d["items"][0]["tier"] = 4);
            yield return Case("item.tiers.incomplete", "/items", d => d["items"][0]["tier"] = 2);
            yield return Case("item.target.invalid", "/items/0/target/allowedMachineIds", d => d["items"][0]["target"]["allowedMachineIds"] = new JArray());
            yield return Case("item.duration.non-positive", "/items/0/durationMs", d => d["items"][0]["durationMs"] = 0);
            yield return Case("item.duration.out-of-range", "/items/0/durationMs", d => d["items"][0]["durationMs"] = 604_800_001L);

            yield return Case("research.prerequisite.unknown", "/research/2/prerequisites/0/id", d => d["research"][2]["prerequisites"][0]["id"] = "research.missing");
            yield return Case(
                "research.prerequisite.cycle",
                "/research/1/prerequisites",
                d => d["research"][1]["prerequisites"] = new JArray(new JObject { ["id"] = "research.tool-discount", ["level"] = 1 }));
            yield return Case("research.prerequisite.level-invalid", "/research/2/prerequisites/0/level", d => d["research"][2]["prerequisites"][0]["level"] = 6);
            yield return Case("upgrade.max-level.invalid", "/research/0/maxLevel", d => d["research"][0]["maxLevel"] = 0);
            yield return Case(
                "industry.first-affordable.missing",
                "/industryUpgrades",
                d =>
                {
                    d["industryUpgrades"][0]["ipCost"]["base"] = 50;
                    d["industryUpgrades"][1]["ipCost"]["base"] = 50;
                });

            yield return Case("effect.kind.unknown", "/research/0/effect/kind", d => d["research"][0]["effect"]["kind"] = "teleport");
            yield return Case("effect.target.invalid", "/research/0/effect/target/kind", d => d["research"][0]["effect"]["target"]["kind"] = "allMachines");
            yield return Case("effect.value.out-of-range", "/research/0/effect/valuePerLevelPpm", d => d["research"][0]["effect"]["valuePerLevelPpm"] = 1_000_000);

            yield return Case("overdrive.value.invalid", "/overdrive/speedPpm", d => d["overdrive"]["speedPpm"] = 1_000_000);
            yield return Case("overdrive.duration.out-of-range", "/overdrive/durationMs", d => d["overdrive"]["durationMs"] = 604_800_001L);
            yield return Case("overdrive.cooldown.out-of-range", "/overdrive/cooldownMs", d => d["overdrive"]["cooldownMs"] = 604_800_001L);
            yield return Case("prestige.threshold.invalid", "/prestige/threshold/base", d => d["prestige"]["threshold"]["base"] = 0);
            yield return Case("milestone.order", "/milestones/1/fractionPpm", d => d["milestones"][1]["fractionPpm"] = 250_000);
            yield return Case("prestige.curve.unsupported", "/prestige/ipCurve/kind", d => d["prestige"]["ipCurve"]["kind"] = "linear");
            yield return Case("policy.revision-1.required", "/offlinePolicies", d => d["offlinePolicies"][0]["efficiencyNumerator"] = 3);
            yield return Case(
                "presentation.key.unresolved",
                "/machines/1/presentationKey",
                d => { },
                new KeyResolver("F003-MACHINE-MINER"));

            // Identity, structure and unlock codes.
            yield return Case("value.null", "/runStart/startingCash", d => d["runStart"]["startingCash"] = JValue.CreateNull());
            yield return Case("id.empty", "/resources/0/id", d => d["resources"][0]["id"] = string.Empty);
            yield return Case("id.too-long", "/resources/0/id", d => d["resources"][0]["id"] = new string('a', 129));
            yield return Case("id.not-canonical", "/resources/0/id", d => d["resources"][0]["id"] = " iron.bar");
            yield return Case("id.duplicate", "/resources/1/id", d => d["resources"][1]["id"] = "iron.bar");
            yield return Case("value.out-of-range", "/resources/0/basePrice", d => d["resources"][0]["basePrice"] = 1_000_000_000_000_000_001L);
            yield return Case(
                "ref.research.unknown",
                "/resources/0/unlock/id",
                d => d["resources"][0]["unlock"] = new JObject { ["kind"] = "researchPurchased", ["id"] = "research.missing" });
            yield return Case("unlock.kind.unknown", "/resources/2/unlock/kind", d => d["resources"][2]["unlock"]["kind"] = "onFullMoon");
            yield return Case("unlock.level.invalid", "/resources/1/unlock/level", d => d["resources"][1]["unlock"]["level"] = 9);
            yield return Case("id.reserved", "/resources/0/id", d => d["resources"][0]["id"] = "overdrive");
            yield return Case(
                "unlock.unreachable",
                "/resources/0/unlock",
                d => d["resources"][0]["unlock"] = new JObject { ["kind"] = "resourceProduced", ["id"] = "iron.bar" });
        }

        [Test]
        public void ValidatedBaselineHasNoDiagnostics()
        {
            var diagnostics = Run(null);

            Assert.That(diagnostics, Is.Empty, Describe(diagnostics));
        }

        [Test]
        public void GuideExampleValidatesWithZeroDiagnostics()
        {
            var read = RunContentReader.Read(RunContentExample.Bytes(RunContentExample.GuideJson()));
            Assert.That(read.IsSuccess, Is.True, read.Diagnostic == null ? string.Empty : read.Diagnostic.Message);

            var diagnostics = RunContentValidator.Validate(read.Document);

            Assert.That(diagnostics, Is.Empty, Describe(diagnostics));
        }

        [TestCaseSource(nameof(RuleCases))]
        public void RuleCodeIsReportedForItsViolation(string code, string path, Action<JObject> mutate, IPresentationKeyResolver keys)
        {
            var diagnostics = Run(mutate, keys);

            var match = diagnostics.FirstOrDefault(d => d.Code == code);
            Assert.That(match, Is.Not.Null, "Expected " + code + " but got: " + Describe(diagnostics));
            Assert.That(match.Path, Is.EqualTo(path), Describe(diagnostics));
            foreach (var diagnostic in diagnostics)
            {
                Assert.That(diagnostic.Message, Does.Not.Contain(RunContentExample.RepositoryRoot()));
                Assert.That(diagnostic.Path, Does.Not.Contain(":\\"));
                Assert.That(diagnostic.Message, Does.Not.Contain(":\\"));
            }
        }

        [Test]
        public void EveryContractRuleCodeHasATestCase()
        {
            var covered = new HashSet<string>(RuleCases().Select(c => (string)c.Arguments[0]));

            Assert.That(RunContentValidator.RuleCodes.Where(code => !covered.Contains(code)), Is.Empty);
            Assert.That(covered.Where(code => !RunContentValidator.RuleCodes.Contains(code)), Is.Empty);
        }

        [Test]
        public void NullDocumentAndNullEntriesAreReportedNotThrown()
        {
            var nullDocument = RunContentValidator.Validate(null);
            Assert.That(nullDocument.Single().Code, Is.EqualTo("value.null"));

            var emptyDocument = new RunContentDocumentDto { ContentSchemaVersion = 3, ContentRevision = 1 };
            Assert.DoesNotThrow(() => RunContentValidator.Validate(emptyDocument));

            var read = RunContentReader.Read(RunContentExample.Bytes(RunContentExample.Valid()));
            read.Document.Resources[0] = null;
            read.Document.Machines[0] = null;
            read.Document.ContractItemLadder.Entries[0] = null;
            read.Document.Milestones[0] = null;
            read.Document.Machines[1].BuildPrice = null;
            read.Document.RunStart.ManualMining = null;
            read.Document.Prestige.Threshold = null;
            var diagnostics = RunContentValidator.Validate(read.Document);
            Assert.That(diagnostics.Any(d => d.Code == "value.null" && d.Path == "/resources/0"), Is.True, Describe(diagnostics));
            Assert.That(diagnostics.Any(d => d.Code == "value.null" && d.Path == "/machines/0"), Is.True, Describe(diagnostics));
            Assert.That(diagnostics.Any(d => d.Code == "value.null" && d.Path == "/contractItemLadder/entries/0"), Is.True, Describe(diagnostics));
            Assert.That(diagnostics.Any(d => d.Code == "value.null" && d.Path == "/milestones/0"), Is.True, Describe(diagnostics));
            Assert.That(diagnostics.Any(d => d.Code == "value.null" && d.Path == "/machines/1/buildPrice"), Is.True, Describe(diagnostics));
            Assert.That(diagnostics.Any(d => d.Code == "value.null" && d.Path == "/runStart/manualMining"), Is.True, Describe(diagnostics));
            Assert.That(diagnostics.Any(d => d.Code == "value.null" && d.Path == "/prestige/threshold"), Is.True, Describe(diagnostics));
        }

        // ---- ordering, cap, echo bounds -----------------------------------------------------------------------

        [Test]
        public void MultipleViolationsAreReportedInDocumentOrder()
        {
            var diagnostics = Run(d =>
            {
                d["contracts"][1]["objective"] = "widget";
                d["resources"][3]["basePrice"] = -1;
                d["resources"][0]["basePrice"] = -1;
                d["recipes"][2]["durationTicks"] = 0;
                d["machines"][2]["maxLevel"] = 0;
                d["storageLevels"][1]["capacity"] = 1000;
                d["items"][2]["tier"] = 4;
                d["research"][2]["maxLevel"] = 0;
                d["overdrive"]["speedPpm"] = 1_000_000;
                d["prestige"]["threshold"]["base"] = 0;
                d["offlinePolicies"][0]["efficiencyNumerator"] = 3;
                d["runStart"]["removalRefundPpm"] = -1;
            });

            var expected = new[]
            {
                "runstart.refund.out-of-range@/runStart/removalRefundPpm",
                "resource.price.negative@/resources/0/basePrice",
                "resource.price.negative@/resources/3/basePrice",
                "recipe.duration.non-positive@/recipes/2/durationTicks",
                "machine.max-level.invalid@/machines/2/maxLevel",
                "storage.capacity.non-increasing@/storageLevels/1/capacity",
                "contract.objective.unknown@/contracts/1/objective",
                "item.tier.invalid@/items/2/tier",
                "item.tiers.incomplete@/items",
                "upgrade.max-level.invalid@/research/2/maxLevel",
                "overdrive.value.invalid@/overdrive/speedPpm",
                "prestige.threshold.invalid@/prestige/threshold/base",
                "policy.revision-1.required@/offlinePolicies",
            };
            var actual = diagnostics.Select(x => x.Code + "@" + x.Path).ToList();
            Assert.That(actual, Is.EqualTo(expected), Describe(diagnostics));
        }

        [Test]
        public void ReportedViolationsAreCappedAtOneHundredWithATruncationNote()
        {
            var diagnostics = Run(d =>
            {
                var resources = (JArray)d["resources"];
                for (var i = 0; i < 150; i++)
                {
                    resources.Add(new JObject
                    {
                        ["id"] = "extra." + i.ToString("D3"),
                        ["label"] = "x",
                        ["presentationKey"] = "k",
                        ["basePrice"] = -1,
                        ["unlock"] = new JObject { ["kind"] = "always" },
                    });
                }
            });

            Assert.That(diagnostics.Count, Is.EqualTo(GameplayContentLimits.MaximumReportedDiagnostics));
            Assert.That(diagnostics.All(x => x.Code == "resource.price.negative"), Is.True, Describe(diagnostics));
            Assert.That(diagnostics[0].Path, Is.EqualTo("/resources/4/basePrice"));
            Assert.That(diagnostics[98].Message, Does.Not.Contain("omitted"));
            Assert.That(diagnostics[99].Message, Does.Contain("omitted"));
            Assert.That(diagnostics[99].Path, Is.EqualTo("/resources/103/basePrice"));
        }

        [Test]
        public void EchoedIdentifiersAreTruncatedToOneHundredTwentyEightCodeUnits()
        {
            var longId = new string('a', 300);
            var diagnostics = Run(d =>
            {
                d["machines"][0]["id"] = longId;
                d["machines"][0]["recipeId"] = "recipe.missing";
            });

            var tooLong = diagnostics.First(x => x.Code == "id.too-long");
            Assert.That(tooLong.EntryId.Length, Is.LessThanOrEqualTo(128));
            var unknown = diagnostics.First(x => x.Code == "ref.recipe.unknown");
            Assert.That(unknown.EntryId.Length, Is.LessThanOrEqualTo(128));
            foreach (var diagnostic in diagnostics)
            {
                Assert.That(diagnostic.Message, Does.Not.Contain(new string('a', 129)));
                Assert.That(diagnostic.Message.IndexOf('\n'), Is.LessThan(0));
            }

            var echoed = Run(d =>
            {
                d["recipes"][0]["inputs"][0]["resourceId"] = new string('b', 128);
                d["research"][2]["prerequisites"][0]["id"] = new string('c', 128);
            });
            Assert.That(echoed.Any(x => x.Code == "ref.resource.unknown"), Is.True, Describe(echoed));
            Assert.That(echoed.All(x => x.Message.Length < 400), Is.True);
        }

        [Test]
        public void ValidationIsDeterministic()
        {
            Action<JObject> mutate = d =>
            {
                d["resources"][0]["basePrice"] = -1;
                d["machines"][1]["cost"]["growthPpm"] = 1;
            };

            Assert.That(Describe(Run(mutate)), Is.EqualTo(Describe(Run(mutate))));
        }

        // ---- numeric rules and boundaries --------------------------------------------------------------------

        [Test]
        public void RefundBoundsAreZeroToNineHundredNinetyNineThousandNineHundredNinetyNine()
        {
            Assert.That(Run(d => d["runStart"]["removalRefundPpm"] = 0), Is.Empty);
            Assert.That(Run(d => d["runStart"]["removalRefundPpm"] = 999_999), Is.Empty);
            Assert.That(Run(d => d["runStart"]["removalRefundPpm"] = 1_000_000).Any(x => x.Code == "runstart.refund.out-of-range"), Is.True);
            Assert.That(Run(d => d["runStart"]["removalRefundPpm"] = -1).Any(x => x.Code == "runstart.refund.out-of-range"), Is.True);
        }

        [Test]
        public void MinimumCycleMayNotBeBelowTheEngineFloor()
        {
            Assert.That(Run(d => d["runStart"]["minCycleTicks"] = 1_000_000), Is.Empty);
            Assert.That(Run(d => d["runStart"]["minCycleTicks"] = 999_999).Any(x => x.Code == "runstart.min-cycle.invalid"), Is.True);
            Assert.That(Run(d => d["runStart"]["minCycleTicks"] = 1_000_000_000_001L).Any(x => x.Code == "runstart.min-cycle.invalid"), Is.True);
        }

        [Test]
        public void FlatCurveIsValidButAGrowingCurveThatStallsIsNot()
        {
            Assert.That(Run(d => d["machines"][1]["cost"] = new JObject { ["base"] = 10, ["growthPpm"] = 1_000_000, ["rounding"] = "floor" }), Is.Empty);
            Assert.That(Run(d => d["machines"][1]["cost"] = new JObject { ["base"] = 0, ["growthPpm"] = 1_000_000, ["rounding"] = "ceil" }), Is.Empty);
            var stalled = Run(d => d["machines"][1]["cost"] = new JObject { ["base"] = 0, ["growthPpm"] = 1_500_000, ["rounding"] = "ceil" });
            Assert.That(stalled.Any(x => x.Code == "curve.stalled"), Is.True, Describe(stalled));
        }

        [Test]
        public void CurveOverflowIsReportedAtTheFirstValueAboveTenToTheEighteenth()
        {
            // 10^17 x 10 = 10^18 is the last exact value; the next step exceeds it.
            var fits = Run(d =>
            {
                d["machines"][1]["cost"] = new JObject { ["base"] = 100_000_000_000_000_000L, ["growthPpm"] = 10_000_000, ["rounding"] = "ceil" };
                d["machines"][1]["maxLevel"] = 2;
                d["machines"][1]["milestones"] = new JArray();
            });
            Assert.That(fits, Is.Empty, Describe(fits));
            var overflows = Run(d =>
            {
                d["machines"][1]["cost"] = new JObject { ["base"] = 100_000_000_000_000_000L, ["growthPpm"] = 10_000_000, ["rounding"] = "ceil" };
                d["machines"][1]["maxLevel"] = 3;
                d["machines"][1]["milestones"] = new JArray();
            });
            Assert.That(overflows.Any(x => x.Code == "curve.overflow"), Is.True, Describe(overflows));
        }

        [Test]
        public void ContractCurvesAreCheckedOverAllOneThousandTwentyFourCompletions()
        {
            // 1.01^1023 stays far below 10^18 for a base of 100, but a base near 10^17 overflows well before index 1,023.
            var diagnostics = Run(d => d["contracts"][2]["quantity"]["base"] = 100_000_000_000_000_000L);
            Assert.That(diagnostics.Any(x => x.Code == "curve.overflow" && x.Path == "/contracts/2/quantity"), Is.True, Describe(diagnostics));
        }

        [Test]
        public void SpeedTableMustStrictlyIncreaseAndEveryMilestoneMustBeLive()
        {
            Assert.That(Run(d => d["machines"][1]["speedGrowthPpm"] = 1_000_001), Is.Empty);
            Assert.That(Run(d => d["machines"][1]["speedGrowthPpm"] = 1_000_000).Any(x => x.Code == "speed.level.dead"), Is.True);
            var deadMilestone = Run(d => d["machines"][1]["milestones"][0]["speedPpm"] = 1_000_000);
            Assert.That(deadMilestone.Any(x => x.Code == "speed.level.dead" && x.Path == "/machines/1/milestones/0/speedPpm"), Is.True, Describe(deadMilestone));
        }

        [Test]
        public void WorstCaseComposedSpeedMustNotExceedTheCombinedSpeedBound()
        {
            // The Miner is the fastest machine: level 25, the factory-wide Item and Overdrive (no Research or Industry
            // speed source targets it). Its worst case is computed with the domain composer.
            var miner = new MachineDef(
                "machine.miner",
                "Miner",
                "key",
                "recipe.mine-iron-ore",
                25,
                new Curve(10, 1_150_000, "ceil"),
                1_080_000,
                new[] { new SpeedMilestone(10, 2_000_000), new SpeedMilestone(25, 2_000_000) },
                new UnlockCondition("always"));
            Assert.That(
                SpeedComposer.TryCompose(miner.SpeedTable[25], null, null, new long[] { 1_200_000 }, 2_000_000, ExactBound.Value, out var worst),
                Is.True);
            Assert.That(worst, Is.GreaterThan(60_000_000).And.LessThan(100_000_000));

            Assert.That(Run(d => d["runStart"]["maxCombinedSpeedPpm"] = worst), Is.Empty, "a bound equal to the worst case is valid");

            var below = Run(d => d["runStart"]["maxCombinedSpeedPpm"] = worst - 1);
            Assert.That(below.Any(x => x.Code == "speed.level.dead" && x.Path == "/machines/1"), Is.True, Describe(below));
        }

        [Test]
        public void EverySpeedSourceCountsTowardsTheWorstCase()
        {
            // Without the Item and Overdrive the Miner's worst case is the table value alone (about x25.4).
            var withoutOverdrive = Run(d =>
            {
                d["runStart"]["maxCombinedSpeedPpm"] = 30_000_000;
                d["overdrive"]["speedPpm"] = 1_000_001;
                ((JArray)d["items"]).RemoveAt(1);
                ((JArray)d["contractItemLadder"]["entries"]).RemoveAt(2);
            });
            Assert.That(withoutOverdrive.Any(x => x.Code == "speed.level.dead" && x.Path.StartsWith("/machines/")), Is.False, Describe(withoutOverdrive));

            var stacked = Run(d =>
            {
                d["runStart"]["maxCombinedSpeedPpm"] = 30_000_000;
            });
            Assert.That(stacked.Any(x => x.Code == "speed.level.dead" && x.Path == "/machines/1"), Is.True, Describe(stacked));

            // A Research speed source stacks for the machine it targets: 1.1^5 more for the Smelter.
            var researchDominates = Run(d =>
            {
                d["runStart"]["maxCombinedSpeedPpm"] = 59_000_000;
                d["research"][1]["effect"]["valuePerLevelPpm"] = 1_500_000;
            });
            Assert.That(researchDominates.Any(x => x.Code == "speed.level.dead" && x.Path == "/machines/3"), Is.True, Describe(researchDominates));
        }

        [Test]
        public void ItemSpeedSourcesStackAcrossConflictKeysButNotWithinOne()
        {
            // Two factory-wide Items share the conflict key (machineSpeed, factory): only the better one counts, so the
            // Miner's worst case (about x60.9) stays under a bound that stacking (x1.2 x 1.1) would break (about x67).
            var sameKey = Run(d =>
            {
                var extra = (JObject)d["items"][1].DeepClone();
                extra["id"] = "item.golden-blueprint-2";
                extra["presentationKey"] = "F003-ITEM-GOLDEN-BLUEPRINT-2";
                extra["effect"]["speedPpm"] = 1_100_000;
                ((JArray)d["items"]).Add(extra);
                d["runStart"]["maxCombinedSpeedPpm"] = 62_000_000;
            });
            Assert.That(sameKey.Where(x => x.Code == "speed.level.dead"), Is.Empty, Describe(sameKey));
        }

        [Test]
        public void OversizedBatchWithinTheLimitIsValidContent()
        {
            // OD-9: a batch larger than the store completes partially, so it is not rejected. The Miner's worst-case
            // batch is 4 recipe units; 10^16 output units x 4 x maxCombinedOutputFactor 4 = 1.6 x 10^17 fits.
            var diagnostics = Run(d => d["recipes"][1]["outputs"][0]["amount"] = 10_000_000_000_000_000L);

            Assert.That(diagnostics, Is.Empty, Describe(diagnostics));
        }

        [Test]
        public void BatchOverflowChecksInputsAndOutputsAtTheWorstCaseSpeed()
        {
            var outputs = Run(d => d["recipes"][1]["outputs"][0]["amount"] = 100_000_000_000_000_000L);
            Assert.That(outputs.Any(x => x.Code == "batch.overflow" && x.Path == "/machines/1"), Is.True, Describe(outputs));

            var inputs = Run(d => d["recipes"][3]["inputs"][0]["amount"] = 200_000_000_000_000_000L);
            Assert.That(inputs.Any(x => x.Code == "batch.overflow" && x.Path == "/machines/3"), Is.True, Describe(inputs));
        }

        [Test]
        public void DeliveryRewardRatioMustStayBetweenMarketValueAndTheRatioBound()
        {
            // The transitional adapter pays ceil(quantity x price x ratio / 10^6); a delivery offer must pay 1.0 to maxRewardRatioPpm (2.0).
            Assert.That(Run(d => d["contracts"][2]["rewardRatioPpm"] = 1_000_000), Is.Empty, "the market value itself is allowed");
            // Revision 2: the v3 ceilings (contract.ratio.standing, 1.5) apply from revision 3 only; the v2 rule is ungated.
            Assert.That(
                Run(d =>
                {
                    d["contentRevision"] = 2;
                    d["contracts"][2]["rewardRatioPpm"] = 2_000_000;
                }),
                Is.Empty,
                "the ratio bound itself is allowed");

            var below = Run(d => d["contracts"][2]["rewardRatioPpm"] = 999_999);
            Assert.That(below.Any(x => x.Code == "contract.reward.ratio" && x.Path == "/contracts/2/rewardRatioPpm" && x.Message.Contains("below")), Is.True, Describe(below));

            var above = Run(d => d["contracts"][2]["rewardRatioPpm"] = 2_000_001);
            Assert.That(above.Any(x => x.Code == "contract.reward.ratio" && x.Message.Contains("exceeds")), Is.True, Describe(above));

            var tighter = Run(d => d["runStart"]["maxRewardRatioPpm"] = 1_100_000);
            Assert.That(tighter.Count(x => x.Code == "contract.reward.ratio"), Is.GreaterThanOrEqualTo(3), Describe(tighter));

            Assert.That(Run(d => d["contracts"][2]["rewardRatioPpm"] = 1_000_000_000_000_000_001L).Any(x => x.Code == "value.out-of-range" && x.Path == "/contracts/2/rewardRatioPpm"), Is.True);
        }

        [Test]
        public void ARewardTableAbove10ToThe18IsAContentErrorNotAnException()
        {
            // 10^13 x 1.01^1023 stays below 10^18, but x price 3 x ratio 1.5 does not at the last completion index.
            var diagnostics = Run(d => d["contracts"][2]["quantity"]["base"] = 10_000_000_000_000L);

            Assert.That(diagnostics.Any(x => x.Code == "contract.reward.ratio" && x.Path == "/contracts/2/rewardRatioPpm" && x.Message.Contains("10^18")), Is.True, Describe(diagnostics));
            Assert.That(diagnostics.Any(x => x.Code == "curve.overflow"), Is.False, Describe(diagnostics));
        }

        [Test]
        public void ProductionOffersMayPayBelowTheMarketValue()
        {
            // contract.ratio.produce (0 < R, a stacked bound) is a later rule; the legacy market floor only applies to delivery.
            Assert.That(Run(d => { d["contracts"][3]["objective"] = "produce"; d["contracts"][3]["rewardRatioPpm"] = 500_000; }), Is.Empty);
        }

        [Test]
        public void RpCurveMayNotGrowFasterThanItsQuantityCurve()
        {
            Action<long, JObject> rp = (growth, d) =>
            {
                d["contracts"][0]["rpReward"]["base"] = 1000;
                d["contracts"][0]["rpReward"]["growthPpm"] = growth;
            };
            Assert.That(Run(d => rp(1_010_000, d)), Is.Empty, "equal growth is allowed");
            var faster = Run(d => rp(1_010_001, d));
            Assert.That(faster.Any(x => x.Code == "contract.rp.growth"), Is.True, Describe(faster));
        }

        [Test]
        public void ObjectiveAndClassAreClosedVocabulariesThatAreCheckedBeforeAnyCatalogObjectIsBuilt()
        {
            foreach (var objective in new[] { "deliver", "produce" })
            {
                Assert.That(Run(d => d["contracts"][3]["objective"] = objective).Any(x => x.Code == "contract.objective.unknown"), Is.False, objective);
            }

            foreach (var offerClass in new[] { "standing", "bonus" })
            {
                Assert.That(Run(d => d["contracts"][0]["class"] = offerClass).Any(x => x.Code == "contract.class.unknown"), Is.False, offerClass);
            }

            foreach (var wrong in new[] { "Deliver", " deliver", "", "delivery" })
            {
                Assert.That(Run(d => d["contracts"][0]["objective"] = wrong).Any(x => x.Code == "contract.objective.unknown"), Is.True, "'" + wrong + "'");
                Assert.That(Run(d => d["contracts"][0]["class"] = wrong).Any(x => x.Code == "contract.class.unknown"), Is.True, "'" + wrong + "'");
            }

            // The builder rejects with the rule codes; the ContractOfferDef guards (content.internal) are never reached.
            var document = RunContentExample.Valid();
            document["contracts"][0]["objective"] = "mine";
            document["contracts"][1]["class"] = "legendary";
            var read = RunContentReader.Read(RunContentExample.Bytes(document));
            Assert.That(read.IsSuccess, Is.True);
            var built = RunCatalogBuilder.Build(read.Document);
            Assert.That(built.IsSuccess, Is.False);
            Assert.That(built.Diagnostics.Select(d => d.Code), Is.EqualTo(new[] { "contract.objective.unknown", "contract.class.unknown" }), Describe(built.Diagnostics));
        }

        [Test]
        public void DurationIsOptionalAndBoundedWhenPresent()
        {
            Assert.That(Run(d => ((JObject)d["contracts"][3]).Remove("durationMs")), Is.Empty, "an untimed bonus offer is valid");
            Assert.That(Run(d => d["contracts"][0]["durationMs"] = 1000), Is.Empty, "any offer may be timed");
            Assert.That(Run(d => d["contracts"][2]["durationMs"] = 604_800_000L), Is.Empty);
            Assert.That(Run(d => d["contracts"][2]["durationMs"] = 0).Any(x => x.Code == "contract.duration.out-of-range"), Is.True);
            Assert.That(Run(d => d["contracts"][2]["durationMs"] = -5).Any(x => x.Code == "contract.duration.out-of-range"), Is.True);
        }

        [Test]
        public void BonusTriggerAndWindowAreValidatedWhenPresent()
        {
            var unknownKind = Run(d => d["contracts"][3]["bonus"]["trigger"]["kind"] = "onFullMoon");
            Assert.That(unknownKind.Any(x => x.Code == "unlock.kind.unknown" && x.Path == "/contracts/3/bonus/trigger/kind"), Is.True, Describe(unknownKind));

            var badLevel = Run(d => d["contracts"][3]["bonus"]["trigger"]["level"] = 9);
            Assert.That(badLevel.Any(x => x.Code == "unlock.level.invalid" && x.Path == "/contracts/3/bonus/trigger/level"), Is.True, Describe(badLevel));

            var unknownResearch = Run(d => d["contracts"][3]["bonus"]["trigger"] = new JObject { ["kind"] = "researchPurchased", ["id"] = "research.missing" });
            Assert.That(unknownResearch.Any(x => x.Code == "ref.research.unknown" && x.Path == "/contracts/3/bonus/trigger/id"), Is.True, Describe(unknownResearch));

            var zeroWindow = Run(d => d["contracts"][3]["bonus"]["windowMs"] = 0);
            Assert.That(zeroWindow.Any(x => x.Code == "value.out-of-range" && x.Path == "/contracts/3/bonus/windowMs"), Is.True, Describe(zeroWindow));

            Assert.That(Run(d => d["contracts"][3]["cooldownMs"] = -1).Any(x => x.Code == "value.out-of-range" && x.Path == "/contracts/3/cooldownMs"), Is.True);
            Assert.That(Run(d => ((JObject)d["contracts"][0]).Remove("bonus")), Is.Empty);
        }

        [Test]
        public void ContractItemLadderEntriesNeedKnownItemsAndSafeIndices()
        {
            var tooHigh = Run(d => d["contractItemLadder"]["entries"][0]["index"] = 1024);
            Assert.That(tooHigh.Any(x => x.Code == "value.out-of-range" && x.Path == "/contractItemLadder/entries/0/index"), Is.True, Describe(tooHigh));
            Assert.That(Run(d => d["contractItemLadder"]["entries"][0]["index"] = 1023), Is.Empty);
            Assert.That(Run(d => d["contractItemLadder"]["entries"][0]["index"] = -1).Any(x => x.Code == "value.out-of-range"), Is.True);

            var duplicate = Run(d => ((JArray)d["contractItemLadder"]["entries"]).Add(d["contractItemLadder"]["entries"][0].DeepClone()));
            Assert.That(duplicate.Any(x => x.Code == "value.out-of-range" && x.Path == "/contractItemLadder/entries/3/index"), Is.True, Describe(duplicate));

            Assert.That(Run(d => d["contractItemLadder"]["entries"] = new JArray()).Any(x => x.Code == "item.tiers.incomplete"), Is.True, "an empty ladder leaves every tier unobtainable");
            Assert.That(Run(d => d["contractItemLadder"]["perRunCap"] = -1).Any(x => x.Code == "value.out-of-range" && x.Path == "/contractItemLadder/perRunCap"), Is.True);

            var sameItemTwice = Run(d =>
            {
                var entries = (JArray)d["contractItemLadder"]["entries"];
                for (var i = 1; i <= 256; i++)
                {
                    entries.Add(new JObject { ["index"] = i, ["itemId"] = "item.diamond-drill-head" });
                }
            });
            Assert.That(sameItemTwice.Any(x => x.Code == "value.out-of-range" && x.Message.Contains("256")), Is.True, Describe(sameItemTwice));
        }

        [Test]
        public void SchemaV3NumbersMustFitTheCoreDefinitions()
        {
            Assert.That(Run(d => d["runStart"]["sellSteps"] = new JArray(1, 1_000_000_000_000_000_001L)).Any(x => x.Code == "sell.steps" && x.Path == "/runStart/sellSteps/1"), Is.True);
            Assert.That(Run(d => d["runStart"]["liveGapThresholdMs"] = 1_000_000_000_000_000_001L).Any(x => x.Code == "live-gap.range" && x.Path == "/runStart/liveGapThresholdMs"), Is.True);
            Assert.That(Run(d => d["runStart"]["minRunDurationMs"] = -1).Any(x => x.Code == "value.out-of-range" && x.Path == "/runStart/minRunDurationMs"), Is.True);
            Assert.That(Run(d => d["runStart"]["uptimeMaxPpm"] = -1).Any(x => x.Code == "value.out-of-range" && x.Path == "/runStart/uptimeMaxPpm"), Is.True);

            Func<string, long, IReadOnlyList<ContentDiagnostic>> mining = (member, value) => Run(d => d["runStart"]["manualMining"][member] = value);
            Assert.That(mining("baseYieldMilli", 0).Any(x => x.Code == "value.out-of-range" && x.Path == "/runStart/manualMining/baseYieldMilli"), Is.True);
            Assert.That(mining("baseYieldMilli", 1), Is.Empty);
            Assert.That(mining("maxActivationsPerSecond", 0).Any(x => x.Code == "value.out-of-range"), Is.True);
            Assert.That(mining("maxActivationsPerSecond", 20), Is.Empty);
            Assert.That(mining("maxActivationsPerSecond", 21).Any(x => x.Code == "value.out-of-range"), Is.True);
            Assert.That(mining("referenceMinerLevel", 0).Any(x => x.Code == "value.out-of-range"), Is.True);
            Assert.That(mining("maxShareOfMinerPpm", 3_000_000), Is.Empty);
            Assert.That(mining("maxShareOfMinerPpm", 3_000_001).Any(x => x.Code == "value.out-of-range"), Is.True);
            Assert.That(mining("startWindowMs", -1).Any(x => x.Code == "value.out-of-range"), Is.True);
            Assert.That(mining("miningAdvantageMaxPpm", -1).Any(x => x.Code == "value.out-of-range"), Is.True);
            Assert.That(
                Run(d => d["runStart"]["manualMining"]["resourceId"] = "iron.missing").Any(x => x.Code == "ref.resource.unknown" && x.Path == "/runStart/manualMining/resourceId"),
                Is.True);

            Assert.That(Run(d => d["machines"][1]["buildPrice"]["ownedGrowthPpm"] = 999_999).Any(x => x.Code == "value.out-of-range" && x.Path == "/machines/1/buildPrice/ownedGrowthPpm"), Is.True);
            Assert.That(Run(d => d["machines"][1]["buildPrice"]["ownedGrowthPpm"] = 1_000_000), Is.Empty);
            Assert.That(Run(d => d["machines"][1]["buildPrice"]["prestigeGrowthPpm"] = 999_999).Any(x => x.Code == "value.out-of-range" && x.Path == "/machines/1/buildPrice/prestigeGrowthPpm"), Is.True);
            Assert.That(Run(d => d["machines"][1]["buildPrice"]["base"] = -1).Any(x => x.Code == "value.out-of-range"), Is.True);
            Assert.That(Run(d => d["machines"][1]["buildPrice"]["cap"] = 1_000_000_000_000_000_001L).Any(x => x.Code == "build.base.bound" && x.Path == "/machines/1/buildPrice/cap"), Is.True);

            Assert.That(Run(d => d["prestige"]["threshold"]["growthPpm"] = 999_999).Any(x => x.Code == "value.out-of-range" && x.Path == "/prestige/threshold/growthPpm"), Is.True);
            Assert.That(Run(d => d["prestige"]["threshold"]["maxIndex"] = 0).Any(x => x.Code == "value.out-of-range" && x.Path == "/prestige/threshold/maxIndex"), Is.True);
            Assert.That(Run(d => d["prestige"]["threshold"]["maxIndex"] = 1), Is.Empty);

            Assert.That(Run(d => d["milestones"][0]["fractionPpm"] = -1).Any(x => x.Code == "milestone.order" && x.Path == "/milestones/0/fractionPpm"), Is.True);
            Assert.That(Run(d => d["industryUpgrades"][0]["decayPpm"] = 1_000_001).Any(x => x.Code == "value.out-of-range" && x.Path == "/industryUpgrades/0/decayPpm"), Is.True);
            Assert.That(Run(d => d["industryUpgrades"][0]["decayPpm"] = 1_000_000), Is.Empty);
            Assert.That(Run(d => d["industryUpgrades"][0]["practicalCeilingLevel"] = -1).Any(x => x.Code == "value.out-of-range"), Is.True);
            Assert.That(Run(d => d["industryUpgrades"][0]["minGainPpm"] = -1).Any(x => x.Code == "value.out-of-range"), Is.True);
            Assert.That(Run(d => d["chainDominanceMarginPpm"] = -1).Any(x => x.Code == "value.out-of-range" && x.Path == "/chainDominanceMarginPpm"), Is.True);
        }

        [Test]
        public void EveryDocumentThatPassesTheValidatorBuildsACatalogWithoutContentInternal()
        {
            // The validator mirrors every Core constructor rule: boundary values the validator accepts must construct.
            Action<JObject>[] boundaries =
            {
                d => d["runStart"]["sellSteps"] = new JArray(Enumerable.Range(1, 8).Cast<object>().Concat(new object[] { "all" }).ToArray()),
                d => d["runStart"]["manualMining"]["maxShareOfMinerPpm"] = 3_000_000,
                d => d["machines"][1]["buildPrice"]["ownedGrowthPpm"] = 1_000_000,
                d => { d["prestige"]["threshold"]["base"] = 1_000_000_000_000_000_000L; d["prestige"]["threshold"]["growthPpm"] = 1_000_000; },
                d => d["contractItemLadder"]["entries"][0]["index"] = 1023,
                d => d["milestones"] = new JArray(Enumerable.Range(1, 64).Select(i => (object)new JObject { ["fractionPpm"] = i * 10_000 }).ToArray()),
                d => d["industryUpgrades"][0]["decayPpm"] = 1_000_000,
                d => d["contracts"][3]["objective"] = "produce",
            };
            foreach (var mutate in boundaries)
            {
                var document = RunContentExample.Valid();
                mutate(document);
                var read = RunContentReader.Read(RunContentExample.Bytes(document));
                Assert.That(read.IsSuccess, Is.True);
                var built = RunCatalogBuilder.Build(read.Document);
                Assert.That(built.IsSuccess, Is.True, Describe(built.Diagnostics));
            }
        }

        [Test]
        public void EveryItemTierNeedsAnObtainableAndUsableDefinition()
        {
            var unobtainable = Run(d => ((JArray)d["contractItemLadder"]["entries"]).RemoveAt(0));
            var missingTier = unobtainable.Single(x => x.Code == "item.tiers.incomplete");
            Assert.That(missingTier.Message, Does.Contain("missing tier(s): 1."));

            var unusable = Run(d => d["items"][1]["effect"]["speedPpm"] = 1_000_000);
            Assert.That(unusable.Any(x => x.Code == "effect.value.out-of-range" && x.Path == "/items/1/effect/speedPpm"), Is.True, Describe(unusable));
            Assert.That(unusable.Single(x => x.Code == "item.tiers.incomplete").Message, Does.Contain("missing tier(s): 3."));

            var redirectedReward = Run(d => d["contractItemLadder"]["entries"][2]["itemId"] = "item.diamond-drill-head");
            Assert.That(redirectedReward.Any(x => x.Code == "item.tiers.incomplete"), Is.True, Describe(redirectedReward));
        }

        [Test]
        public void ResearchCyclesAreReportedOnlyForMembersOfTheCycle()
        {
            var diagnostics = Run(d =>
            {
                d["research"][1]["prerequisites"] = new JArray(new JObject { ["id"] = "research.tool-discount", ["level"] = 1 });
                d["research"][0]["prerequisites"] = new JArray(new JObject { ["id"] = "research.tool-discount", ["level"] = 1 });
            });

            var cycles = diagnostics.Where(x => x.Code == "research.prerequisite.cycle").Select(x => x.Path).ToList();
            Assert.That(cycles, Is.EqualTo(new[] { "/research/1/prerequisites", "/research/2/prerequisites" }), Describe(diagnostics));
        }

        [Test]
        public void SelfPrerequisiteIsACycle()
        {
            var diagnostics = Run(d =>
                d["research"][0]["prerequisites"] = new JArray(new JObject { ["id"] = "research.market-insight", ["level"] = 1 }));

            Assert.That(diagnostics.Any(x => x.Code == "research.prerequisite.cycle" && x.Path == "/research/0/prerequisites"), Is.True, Describe(diagnostics));
        }

        [Test]
        public void EffectKindsAreClosedPerSource()
        {
            var itemSalePrice = Run(d => d["items"][0]["effect"] = new JObject { ["kind"] = "salePrice", ["valuePerLevelPpm"] = 1_100_000 });
            Assert.That(itemSalePrice.Any(x => x.Code == "effect.kind.unknown" && x.Path == "/items/0/effect/kind"), Is.True, Describe(itemSalePrice));

            var industryOutput = Run(d =>
                d["industryUpgrades"][0]["effect"] = new JObject { ["kind"] = "machineOutput", ["factor"] = 2 });
            Assert.That(industryOutput.Any(x => x.Code == "effect.kind.unknown" && x.Path == "/industryUpgrades/0/effect/kind"), Is.True, Describe(industryOutput));

            var researchStartingCash = Run(d => d["research"][0]["effect"] = new JObject { ["kind"] = "startingCash", ["valuePerLevel"] = 5 });
            Assert.That(researchStartingCash.Any(x => x.Code == "effect.kind.unknown" && x.Path == "/research/0/effect/kind"), Is.True, Describe(researchStartingCash));
        }

        [Test]
        public void EffectTargetsAndValuesFollowTheEffectTable()
        {
            var unknownMachine = Run(d => d["research"][1]["effect"]["target"]["id"] = "machine.missing");
            Assert.That(unknownMachine.Any(x => x.Code == "effect.target.invalid" && x.Path == "/research/1/effect/target/id"), Is.True, Describe(unknownMachine));

            var outputOnAllMachines = Run(d =>
                d["research"][0]["effect"] = new JObject
                {
                    ["kind"] = "machineOutput",
                    ["target"] = new JObject { ["kind"] = "allMachines" },
                    ["factor"] = 2,
                });
            Assert.That(outputOnAllMachines.Any(x => x.Code == "effect.target.invalid"), Is.True, Describe(outputOnAllMachines));

            var expansion = Run(d => d["research"][2]["effect"]["target"] = new JObject { ["kind"] = "expansion" });
            Assert.That(expansion, Is.Empty, Describe(expansion));
            var expansionWithId = Run(d => d["research"][2]["effect"]["target"] = new JObject { ["kind"] = "expansion", ["id"] = "storage" });
            Assert.That(expansionWithId.Single().Code, Is.EqualTo("member.unknown"), "an expansion target has no id");

            var industryExpansion = Run(d =>
                d["industryUpgrades"][0]["effect"] = new JObject
                {
                    ["kind"] = "upgradeCost",
                    ["target"] = new JObject { ["kind"] = "expansion" },
                    ["valuePerLevelPpm"] = 970_000,
                });
            Assert.That(industryExpansion, Is.Empty, Describe(industryExpansion));

            Assert.That(
                Run(d => d["research"][2]["effect"]["valuePerLevelPpm"] = 100_000).Select(x => x.Code).Distinct(),
                Is.EqualTo(new[] { "cost.floor" }),
                "10^5 is the smallest upgradeCost multiplier (the effect range allows it; the combined 0.5x floor rejects it)");
            Assert.That(Run(d => d["research"][2]["effect"]["valuePerLevelPpm"] = 99_999).Any(x => x.Code == "effect.value.out-of-range"), Is.True);
            Assert.That(Run(d => d["research"][2]["effect"]["valuePerLevelPpm"] = 1_000_000).Any(x => x.Code == "effect.value.out-of-range"), Is.True);

            Assert.That(Run(d => d["items"][0]["effect"]["factor"] = 4), Is.Empty, "the factor may equal maxCombinedOutputFactor");
            Assert.That(Run(d => d["items"][0]["effect"]["factor"] = 5).Any(x => x.Code == "effect.value.out-of-range"), Is.True);
            Assert.That(Run(d => d["items"][0]["effect"]["factor"] = 1).Any(x => x.Code == "effect.value.out-of-range"), Is.True);

            var factoryOutput = Run(d =>
            {
                d["items"][0]["target"] = new JObject { ["kind"] = "factory" };
            });
            Assert.That(factoryOutput.Any(x => x.Code == "effect.target.invalid" && x.Path == "/items/0/target"), Is.True, Describe(factoryOutput));
        }

        [Test]
        public void ItemTargetsMustNameKnownDistinctMachines()
        {
            var unknown = Run(d => d["items"][2]["target"]["allowedMachineIds"] = new JArray("machine.press", "machine.missing"));
            Assert.That(unknown.Any(x => x.Code == "item.target.invalid" && x.Path == "/items/2/target/allowedMachineIds/1"), Is.True, Describe(unknown));

            var duplicate = Run(d => d["items"][2]["target"]["allowedMachineIds"] = new JArray("machine.press", "machine.press"));
            Assert.That(duplicate.Any(x => x.Code == "item.target.invalid" && x.Path == "/items/2/target/allowedMachineIds/1"), Is.True, Describe(duplicate));
        }

        [Test]
        public void FirstIndustryCostMustBeAffordableAtTheThresholdIp()
        {
            // IP = floor(10 x sqrt(FV / 10^7)) = 10 at the threshold, so a level-1 cost of 10 is the last affordable one.
            Func<int, Action<JObject>> baseCost = cost => d =>
            {
                d["industryUpgrades"][0]["ipCost"]["base"] = cost;
                d["industryUpgrades"][1]["ipCost"]["base"] = cost;
            };
            Assert.That(Run(baseCost(10)), Is.Empty);
            Assert.That(Run(baseCost(11)).Any(x => x.Code == "industry.first-affordable.missing"), Is.True);

            Assert.That(Run(d => d["industryUpgrades"] = new JArray()), Is.Empty, "no Industry upgrade means nothing to afford");
            Assert.That(Run(d => d["industryUpgrades"][1]["ipCost"]["base"] = 5000), Is.Empty, "one affordable upgrade is enough");
        }

        [Test]
        public void ExpansionTablesFollowTheirSequenceAndMonotonicityRules()
        {
            Assert.That(Run(d => d["storageLevels"][0]["level"] = 2).Any(x => x.Code == "level.sequence.invalid"), Is.True);
            Assert.That(Run(d => ((JArray)d["factoryLevels"]).RemoveAt(1)).Any(x => x.Code == "level.sequence.invalid"), Is.True);
            Assert.That(Run(d => d["factoryLevels"][3]["slots"] = 12), Is.Empty);
            Assert.That(Run(d => d["factoryLevels"][2]["slots"] = 6).Any(x => x.Code == "factory.slots.non-increasing"), Is.True);
            Assert.That(Run(d => d["storageLevels"][2]["capacity"] = 2000).Any(x => x.Code == "storage.capacity.non-increasing"), Is.True);
        }

        [Test]
        public void PrestigeCurveMustBeSqrtWithPositiveParameters()
        {
            Assert.That(Run(d => d["prestige"]["ipCurve"]["divisor"] = 0).Any(x => x.Code == "value.out-of-range" && x.Path == "/prestige/ipCurve/divisor"), Is.True);
            Assert.That(Run(d => d["prestige"]["ipCurve"]["coefficient"] = 0).Any(x => x.Code == "value.out-of-range" && x.Path == "/prestige/ipCurve/coefficient"), Is.True);
        }

        [Test]
        public void OfflinePolicyMustBeExactlyRevisionOne()
        {
            Assert.That(Run(d => d["offlinePolicies"][0]["revision"] = 2).Any(x => x.Code == "policy.revision-1.required"), Is.True);
            Assert.That(Run(d => d["offlinePolicies"][0]["maxCreditedTicks"] = 1).Any(x => x.Code == "policy.revision-1.required"), Is.True);
            Assert.That(
                Run(d => ((JArray)d["offlinePolicies"]).Add(d["offlinePolicies"][0].DeepClone())).Any(x => x.Code == "policy.revision-1.required"),
                Is.True);
        }

        // ---- presentation hook -----------------------------------------------------------------------------------

        [Test]
        public void PresentationKeysAreCheckedOnResourcesMachinesAndItemsOnly()
        {
            var resolver = new KeyResolver("F003-RES-IRON-ORE", "F003-MACHINE-SMELTER", "F003-ITEM-TUNED-GEARBOX");

            var diagnostics = Run(null, resolver);

            Assert.That(
                diagnostics.Where(x => x.Code == "presentation.key.unresolved").Select(x => x.Code + "@" + x.Path).ToList(),
                Is.EqualTo(new[]
                {
                    "presentation.key.unresolved@/resources/2/presentationKey",
                    "presentation.key.unresolved@/machines/3/presentationKey",
                    "presentation.key.unresolved@/items/2/presentationKey",
                }),
                Describe(diagnostics));
            Assert.That(diagnostics[0].EntryId, Is.EqualTo("iron.ore"));
        }

        // ---- reserved IDs and unlock reachability (issue #179, owner scope decision 2026-10-06) --------------------

        [Test]
        public void ReservedOverdriveIdIsRejectedInEveryUnlockableFamily()
        {
            Action<JObject>[] mutations =
            {
                d => d["resources"][2]["id"] = "overdrive",
                d => d["machines"][1]["id"] = "overdrive",
                d => d["contracts"][1]["id"] = "overdrive",
                d => d["items"][1]["id"] = "overdrive",
                d => d["research"][1]["id"] = "overdrive",
            };
            var paths = new[] { "/resources/2/id", "/machines/1/id", "/contracts/1/id", "/items/1/id", "/research/1/id" };
            for (var i = 0; i < mutations.Length; i++)
            {
                var diagnostics = Run(mutations[i]);
                var reserved = diagnostics.Where(d => d.Code == "id.reserved").ToList();
                Assert.That(reserved.Select(d => d.Path), Is.EqualTo(new[] { paths[i] }), Describe(diagnostics));
                Assert.That(reserved[0].EntryId, Is.EqualTo(RunState.OverdriveUnlockId));
            }
        }

        [Test]
        public void ReservedIdCheckIsExactOrdinalAndLimitedToTheUnlockableFamilies()
        {
            Assert.That(Run(d => d["resources"][0]["id"] = "Overdrive").Any(x => x.Code == "id.reserved"), Is.False, "IDs are case-sensitive");
            Assert.That(Run(d => d["recipes"][0]["id"] = "overdrive").Any(x => x.Code == "id.reserved"), Is.False, "recipes are not unlockable");
            Assert.That(
                Run(d => d["industryUpgrades"][0]["id"] = "overdrive").Any(x => x.Code == "id.reserved"),
                Is.False,
                "Industry upgrades are not unlockable");
        }

        [Test]
        public void SelfReferencingProductionUnlockIsUnreachableAndCascadesInDocumentOrder()
        {
            // The Smelter waits for Bars, which only the Smelter makes: Bar, Plate and Gear are never produced.
            var diagnostics = Run(d => d["machines"][3]["unlock"] = new JObject { ["kind"] = "resourceProduced", ["id"] = "iron.bar" });

            Assert.That(
                diagnostics.Where(x => x.Code == "unlock.unreachable").Select(x => x.Path).ToList(),
                Is.EqualTo(new[]
                {
                    "/machines/3/unlock",
                    "/contracts/0/unlock",
                    "/contracts/1/unlock",
                    "/contracts/2/unlock",
                    "/contracts/3/unlock",
                }),
                Describe(diagnostics));
            Assert.That(diagnostics.First(x => x.Code == "unlock.unreachable").EntryId, Is.EqualTo("machine.smelter"));
        }

        [Test]
        public void ResearchPrerequisiteCycleMakesAResearchUnlockUnreachable()
        {
            var diagnostics = Run(d =>
            {
                d["research"][1]["prerequisites"] = new JArray(new JObject { ["id"] = "research.tool-discount", ["level"] = 1 });
                d["overdrive"]["unlock"] = new JObject { ["kind"] = "researchPurchased", ["id"] = "research.tool-discount" };
            });

            Assert.That(diagnostics.Any(x => x.Code == "research.prerequisite.cycle"), Is.True, Describe(diagnostics));
            var unreachable = diagnostics.Where(x => x.Code == "unlock.unreachable").ToList();
            Assert.That(unreachable.Select(x => x.Path), Is.EqualTo(new[] { "/overdrive/unlock" }), Describe(diagnostics));
            Assert.That(unreachable[0].EntryId, Is.Null);
        }

        [Test]
        public void ResearchNeedsSomeContractThatPaysRpOtherwiseItsUnlockIsUnreachable()
        {
            Action<JObject> gateOverdriveOnResearch = d =>
                d["overdrive"]["unlock"] = new JObject { ["kind"] = "researchPurchased", ["id"] = "research.smelter-speed" };

            Assert.That(Run(gateOverdriveOnResearch), Is.Empty, "RP is paid by the guide's plate and gear contracts");

            var diagnostics = Run(d =>
            {
                gateOverdriveOnResearch(d);
                foreach (var contract in d["contracts"])
                {
                    contract["rpReward"]["base"] = 0;
                }
            });

            Assert.That(
                diagnostics.Where(x => x.Code == "unlock.unreachable").Select(x => x.Path),
                Is.EqualTo(new[] { "/overdrive/unlock" }),
                Describe(diagnostics));
        }

        [Test]
        public void ReachableUnlockChainsAndInitialUnlocksAreNotReported()
        {
            Assert.That(
                Run(d => d["overdrive"]["unlock"] = new JObject { ["kind"] = "researchPurchased", ["id"] = "research.tool-discount" }),
                Is.Empty,
                "a research chain with a prerequisite is reachable");
            Assert.That(
                Run(d =>
                {
                    d["machines"][3]["unlock"] = new JObject { ["kind"] = "resourceProduced", ["id"] = "iron.bar" };
                    d["runStart"]["initialUnlocks"] = new JArray("iron.ore", "machine.miner", "machine.smelter");
                }),
                Is.Empty,
                "an initial unlock breaks the cycle");
        }

        [Test]
        public void UnknownUnlockReferencesKeepTheirOwnCodeAndAddNoUnreachableNoise()
        {
            var diagnostics = Run(d =>
            {
                d["resources"][0]["unlock"] = new JObject { ["kind"] = "researchPurchased", ["id"] = "research.missing" };
                d["resources"][2]["unlock"] = new JObject { ["kind"] = "resourceProduced", ["id"] = "iron.missing" };
            });

            Assert.That(diagnostics.Select(x => x.Code), Is.EqualTo(new[] { "ref.research.unknown", "ref.resource.unknown" }), Describe(diagnostics));
        }

        [Test]
        public void UnreachableAndReservedDiagnosticsAreDeterministic()
        {
            Action<JObject> mutate = d =>
            {
                d["machines"][3]["unlock"] = new JObject { ["kind"] = "resourceProduced", ["id"] = "iron.bar" };
                d["items"][0]["id"] = "overdrive";
            };

            Assert.That(Describe(Run(mutate)), Is.EqualTo(Describe(Run(mutate))));
        }

        [Test]
        public void NoResolverMeansNoPresentationCheckAndResolvingEverythingIsClean()
        {
            Assert.That(Run(null, null), Is.Empty);
            Assert.That(Run(null, new KeyResolver()), Is.Empty);
        }
    }
}
