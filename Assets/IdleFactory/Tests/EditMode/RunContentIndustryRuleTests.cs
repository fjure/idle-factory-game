using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// The content rules of T331 (issue #396): <c>prestige.threshold.bound</c>, <c>industry.ceiling.level</c>, and the two follow-ups of the T314
    /// review (#376, F2 and F6): <c>cost.floor</c> folds the decayed Industry values, and the <c>startingStorageLevel</c> extension of
    /// <c>speed.level.dead</c> accounts for decay and sums several upgrades. Every case starts from the guide example (revision 3).
    /// </summary>
    public sealed class RunContentIndustryRuleTests
    {
        private static IReadOnlyList<ContentDiagnostic> Run(Action<JObject> mutate)
        {
            var document = RunContentExample.Valid();
            mutate?.Invoke(document);
            var read = RunContentReader.Read(RunContentExample.Bytes(document));
            if (!read.IsSuccess)
            {
                return new[] { read.Diagnostic };
            }

            return RunContentValidator.Validate(read.Document);
        }

        private static string Describe(IEnumerable<ContentDiagnostic> diagnostics) =>
            string.Join(" | ", diagnostics.Select(d => d.Code + "@" + d.Path));

        private static void AssertClean(IReadOnlyList<ContentDiagnostic> diagnostics) =>
            Assert.That(diagnostics, Is.Empty, Describe(diagnostics));

        private static void AssertOnly(IReadOnlyList<ContentDiagnostic> diagnostics, string code, string path)
        {
            Assert.That(diagnostics.Count, Is.EqualTo(1), Describe(diagnostics));
            Assert.That(diagnostics[0].Code, Is.EqualTo(code), Describe(diagnostics));
            Assert.That(diagnostics[0].Path, Is.EqualTo(path), Describe(diagnostics));
        }

        private static JObject Upgrade(string id, int maxLevel, long decayPpm, long ceiling, long minGain, JObject effect) => new JObject
        {
            ["id"] = id,
            ["label"] = id,
            ["maxLevel"] = maxLevel,
            ["ipCost"] = new JObject { ["base"] = 5, ["growthPpm"] = 1_500_000, ["rounding"] = "ceil" },
            ["decayPpm"] = decayPpm,
            ["practicalCeilingLevel"] = ceiling,
            ["minGainPpm"] = minGain,
            ["effect"] = effect,
        };

        private static JObject CostEffect(long ppm) => new JObject
        {
            ["kind"] = "upgradeCost",
            ["target"] = new JObject { ["kind"] = "allMachines" },
            ["valuePerLevelPpm"] = ppm,
        };

        private static JObject StorageEffect(long amount) => new JObject { ["kind"] = "startingStorageLevel", ["valuePerLevel"] = amount };

        // ---- prestige.threshold.bound -----------------------------------------------------------------------

        [Test]
        public void TheThresholdAtMaxIndexMayNotExceedTenToTheEighteenth()
        {
            AssertClean(Run(null));
            // 10^9 x 10^n: n = 9 gives exactly 10^18, n = 10 exceeds it.
            AssertClean(Run(d => Threshold(d, 1_000_000_000L, 10_000_000L, 9)));
            AssertOnly(Run(d => Threshold(d, 1_000_000_000L, 10_000_000L, 10)), "prestige.threshold.bound", "/prestige/threshold/maxIndex");
            AssertOnly(Run(d => Threshold(d, 10_000_000L, 1_050_000L, 1_000)), "prestige.threshold.bound", "/prestige/threshold/maxIndex");
            AssertClean(Run(d => Threshold(d, 10_000_000L, 1_000_000L, 10_000L)));
        }

        [Test]
        public void MaxIndexIsAtMostTenThousandAndIsRangeCheckedBeforeTheBound()
        {
            // Owner decision 2026-10-13-b (#396): the walk is bounded in content. 10,000 is valid, 10,001 is value.out-of-range.
            AssertClean(Run(d => Threshold(d, 10_000_000L, 1_000_001L, 10_000L)));
            var over = Run(d => Threshold(d, 10_000_000L, 1_000_001L, 10_001L));
            AssertOnly(over, "value.out-of-range", "/prestige/threshold/maxIndex");
            Assert.That(over[0].Message, Does.Contain("10,000"));
            AssertOnly(Run(d => Threshold(d, 10_000_000L, 1_000_000L, 1_000_000_000_000_000_000L)), "value.out-of-range", "/prestige/threshold/maxIndex");
        }

        [Test]
        public void TheBoundUsesTheIteratedFloorsOfTheRuntimeNotTheExactPower()
        {
            // base 10^18 / 2 x 2.000001^1: the exact product is 10^18 + 5 x 10^11 (above), the floor does not rescue it.
            AssertOnly(Run(d => Threshold(d, 500_000_000_000_000_000L, 2_000_001L, 1)), "prestige.threshold.bound", "/prestige/threshold/maxIndex");
            AssertClean(Run(d => Threshold(d, 500_000_000_000_000_000L, 2_000_000L, 1)));
        }

        [Test]
        public void AnInvalidGrowthOrIndexIsReportedByItsOwnCodeOnly()
        {
            var diagnostics = Run(d => Threshold(d, 10_000_000L, 999_999L, 10));
            Assert.That(diagnostics.Any(x => x.Code == "prestige.threshold.bound"), Is.False, Describe(diagnostics));
            Assert.That(diagnostics.Any(x => x.Code == "value.out-of-range" && x.Path == "/prestige/threshold/growthPpm"), Is.True, Describe(diagnostics));
        }

        private static void Threshold(JObject d, long baseValue, long growthPpm, long maxIndex)
        {
            d["prestige"]["threshold"]["base"] = baseValue;
            d["prestige"]["threshold"]["growthPpm"] = growthPpm;
            d["prestige"]["threshold"]["maxIndex"] = maxIndex;
        }

        // ---- industry.ceiling.level ---------------------------------------------------------------------------

        [Test]
        public void ThePracticalCeilingIsTheLastLevelThatStillReachesTheMinimumGainOrTheLastLevel()
        {
            // Seed Capital: 500 Cash, decay 0.95, 10 levels: the amounts 500, 475, 451, 428, ... 311 give the relative gains
            // 1,000,000, 950,000, 902,000, 856,000, ... 622,000 (owner decision 2026-10-13, #396: the last level with gain >= minGainPpm).
            AssertClean(Run(null));
            AssertOnly(Run(d => d["industryUpgrades"][0]["minGainPpm"] = 900_000), "industry.ceiling.level", "/industryUpgrades/0/practicalCeilingLevel");
            AssertClean(Run(d => { d["industryUpgrades"][0]["minGainPpm"] = 900_000; d["industryUpgrades"][0]["practicalCeilingLevel"] = 3; }));
            AssertClean(Run(d => { d["industryUpgrades"][0]["minGainPpm"] = 622_000; d["industryUpgrades"][0]["practicalCeilingLevel"] = 10; }));
            AssertClean(Run(d => { d["industryUpgrades"][0]["minGainPpm"] = 623_000; d["industryUpgrades"][0]["practicalCeilingLevel"] = 9; }));
            AssertOnly(Run(d => d["industryUpgrades"][0]["practicalCeilingLevel"] = 9), "industry.ceiling.level", "/industryUpgrades/0/practicalCeilingLevel");
            var message = Run(d => d["industryUpgrades"][0]["minGainPpm"] = 900_000).Single().Message;
            Assert.That(message, Does.Contain("last level").And.Contain("at least minGainPpm"));
        }

        [Test]
        public void AMinimumGainAboveTheGainOfLevelOneIsRejectedAtMinGainPpm()
        {
            // Seed Capital (additive): level 1 gains exactly 1,000,000 ppm of itself, so a minimum gain of 1,000,001 can never be met.
            var additive = Run(d => d["industryUpgrades"][0]["minGainPpm"] = 1_000_001);
            AssertOnly(additive, "industry.ceiling.level", "/industryUpgrades/0/minGainPpm");
            Assert.That(additive[0].Message, Does.Contain("minGainPpm is above the gain of level 1"));
            AssertClean(Run(d => { d["industryUpgrades"][0]["minGainPpm"] = 1_000_000; d["industryUpgrades"][0]["practicalCeilingLevel"] = 1; }));

            // A factor kind: Toolmaker 0.80 gains 200,000 at level 1.
            var factor = Run(d => ((JArray)d["industryUpgrades"]).Add(Upgrade("industry.toolmaker", 3, 500_000, 3, 200_001, CostEffect(800_000))));
            Assert.That(factor.Any(x => x.Code == "industry.ceiling.level" && x.Path == "/industryUpgrades/2/minGainPpm"), Is.True, Describe(factor));
            Assert.That(factor.Count(x => x.Code == "industry.ceiling.level"), Is.EqualTo(1), "the mismatch of practicalCeilingLevel is not reported on top");
            Assert.That(Run(d => ((JArray)d["industryUpgrades"]).Add(Upgrade("industry.toolmaker", 3, 500_000, 1, 200_000, CostEffect(800_000)))).Any(x => x.Code == "industry.ceiling.level"), Is.False);
        }

        [Test]
        public void ARevisionBelowThreeChecksOnlyAnUpgradeThatDeclaresACeilingOrAMinimumGain()
        {
            var unset = Run(d =>
            {
                d["contentRevision"] = 2;
                d["industryUpgrades"][0]["practicalCeilingLevel"] = 0;
                d["industryUpgrades"][0]["minGainPpm"] = 0;
            });
            Assert.That(unset.Any(x => x.Code == "industry.ceiling.level"), Is.False, Describe(unset));

            var declared = Run(d => { d["contentRevision"] = 2; d["industryUpgrades"][0]["practicalCeilingLevel"] = 3; });
            Assert.That(declared.Any(x => x.Code == "industry.ceiling.level"), Is.True, Describe(declared));
        }

        // ---- decayPpm from revision 3 (owner decision 2026-10-13, #396, A) --------------------------------------

        private static bool HasAt(IReadOnlyList<ContentDiagnostic> diagnostics, string code, string path) =>
            diagnostics.Any(x => x.Code == code && x.Path == path);

        [Test]
        public void DecayPpmZeroIsLegacyBelowRevisionThreeAndRejectedFromRevisionThree()
        {
            const string path = "/industryUpgrades/0/decayPpm";
            var r2 = Run(d => { d["contentRevision"] = 2; d["industryUpgrades"][0]["decayPpm"] = 0; });
            Assert.That(r2.Any(x => x.Path == path), Is.False, Describe(r2));
            var r1 = Run(d => { d["contentRevision"] = 1; d["industryUpgrades"][0]["decayPpm"] = 0; });
            Assert.That(r1.Any(x => x.Path == path), Is.False, Describe(r1));

            var r3 = Run(d => d["industryUpgrades"][0]["decayPpm"] = 0);
            Assert.That(HasAt(r3, "value.out-of-range", path), Is.True, Describe(r3));
            Assert.That(r3.Single(x => x.Path == path).Message, Does.Contain("1 to 10^6").And.Contain("no decay"));

            var one = Run(d => d["industryUpgrades"][0]["decayPpm"] = 1);
            Assert.That(one.Any(x => x.Path == path), Is.False, Describe(one));
            AssertClean(Run(d => d["industryUpgrades"][0]["decayPpm"] = 1_000_000));
            Assert.That(HasAt(Run(d => d["industryUpgrades"][0]["decayPpm"] = 1_000_001), "value.out-of-range", path), Is.True);
        }

        // ---- the dead level uses the raw amount (F4) -------------------------------------------------------------

        [Test]
        public void ALongDecayWhoseNormalisedGainFloorsToZeroIsNotDeadWhileTheAmountIsPositive()
        {
            var diagnostics = Run(d =>
            {
                var upgrade = (JObject)d["industryUpgrades"][0];
                upgrade["maxLevel"] = 36;
                upgrade["decayPpm"] = 500_000;
                upgrade["minGainPpm"] = 0;
                upgrade["practicalCeilingLevel"] = 36;
                upgrade["effect"]["valuePerLevel"] = 1_000_000_000_000L;
            });
            Assert.That(diagnostics.Any(x => x.Code == "speed.level.dead"), Is.False, Describe(diagnostics));
            Assert.That(diagnostics.Any(x => x.Code == "industry.ceiling.level"), Is.False, Describe(diagnostics));
        }

        // ---- cost.floor with decay (#376 F2) ----------------------------------------------------------------

        [Test]
        public void CostFloorFoldsTheDecayedIndustryValuesNotTheUndecayedOnes()
        {
            // Research 0.95^3 = 0.857375. An Industry discount of 0.80 over 3 levels:
            //   decayed (0.5):   factors 0.80, 0.90, 0.95 -> 0.684 -> 0.586 combined, above the 0.5 floor;
            //   undecayed:       0.80^3 = 0.512 -> 0.439 combined, below it.
            AssertClean(Run(d => ((JArray)d["industryUpgrades"]).Add(Upgrade("industry.toolmaker", 3, 500_000, 3, 5_000, CostEffect(800_000)))));
            var undecayed = Run(d => ((JArray)d["industryUpgrades"]).Add(Upgrade("industry.toolmaker", 3, 1_000_000, 3, 5_000, CostEffect(800_000))));
            Assert.That(undecayed.Count(x => x.Code == "cost.floor"), Is.EqualTo(4), Describe(undecayed));
        }

        [Test]
        public void CostFloorStillTripsWhenTheDecayedDiscountIsDeepEnough()
        {
            // 0.5, 0.75, 0.875 -> 0.328 (Industry alone), below the floor even before the Research.
            var deep = Run(d => ((JArray)d["industryUpgrades"]).Add(Upgrade("industry.toolmaker", 3, 500_000, 3, 5_000, CostEffect(500_000))));
            Assert.That(deep.Count(x => x.Code == "cost.floor"), Is.EqualTo(4), Describe(deep));
        }

        // ---- speed.level.dead, startingStorageLevel (#376 F6) ---------------------------------------------

        [Test]
        public void AStartingStorageLevelUpgradeWhoseDecayedLevelRoundsToZeroIsDead()
        {
            // +1 per level decayed by 0.95 floors to 0 at level 2, so the second level adds nothing.
            var dead = Run(d => d["industryUpgrades"][1]["decayPpm"] = 950_000);
            Assert.That(dead.Any(x => x.Code == "speed.level.dead" && x.Path == "/industryUpgrades/1/maxLevel"), Is.True, Describe(dead));
        }

        [Test]
        public void SeveralStartingStorageLevelUpgradesAreSummedBeforeTheTopIsReached()
        {
            // The guide example has 3 storage levels and Bigger Warehouse (+1, +1): the start level reaches 3, the top, only at its last level.
            AssertClean(Run(null));
            // A second upgrade of +1 already brings the start level to 3 together with level 1 of the warehouse: both last levels are dead.
            var two = Run(d => ((JArray)d["industryUpgrades"]).Add(Upgrade("industry.depot", 1, 1_000_000, 1, 5_000, StorageEffect(1))));
            var dead = two.Where(x => x.Code == "speed.level.dead").Select(x => x.Path).ToArray();
            Assert.That(dead, Is.EquivalentTo(new[] { "/industryUpgrades/1/maxLevel", "/industryUpgrades/2/maxLevel" }), Describe(two));
            // A fourth storage level makes room for both.
            AssertClean(Run(d =>
            {
                ((JArray)d["industryUpgrades"]).Add(Upgrade("industry.depot", 1, 1_000_000, 1, 5_000, StorageEffect(1)));
                d["storageLevels"] = new JArray(
                    d["storageLevels"][0].DeepClone(), d["storageLevels"][1].DeepClone(), d["storageLevels"][2].DeepClone(),
                    new JObject { ["level"] = 4, ["capacity"] = 8_000, ["cost"] = 40_000 },
                    new JObject { ["level"] = 5, ["capacity"] = 16_000, ["cost"] = 80_000 });
            }));
        }
    }
}
