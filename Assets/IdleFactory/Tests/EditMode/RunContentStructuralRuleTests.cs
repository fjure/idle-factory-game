using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// Boundary tests for the structural content rules of T314 (issue #376): <c>sell.steps</c>, <c>live-gap.range</c>,
    /// <c>contract.bonus.fields</c>, <c>contract.ratio.standing</c>, <c>contract.ratio.bonus</c>, <c>contract.quantity.cap</c>,
    /// <c>contract.produce.min</c>, <c>milestone.order</c>, <c>build.base.bound</c>, <c>cost.floor</c> and the extension of
    /// <c>speed.level.dead</c>. Every case starts from the guide example (revision 3) and applies one mutation; the revision-gated
    /// rules are also run at revision 2.
    /// </summary>
    public sealed class RunContentStructuralRuleTests
    {
        // The story rules of T320 (contract.ratio.produce, contract.quantity.capacity, ladder.uptime) are at the end of this class.
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

        private static void AssertOnly(IReadOnlyList<ContentDiagnostic> diagnostics, string code, string path)
        {
            Assert.That(diagnostics.Count, Is.EqualTo(1), Describe(diagnostics));
            Assert.That(diagnostics[0].Code, Is.EqualTo(code), Describe(diagnostics));
            Assert.That(diagnostics[0].Path, Is.EqualTo(path), Describe(diagnostics));
        }

        private static void AssertClean(IReadOnlyList<ContentDiagnostic> diagnostics) =>
            Assert.That(diagnostics, Is.Empty, Describe(diagnostics));

        // ---- sell.steps -----------------------------------------------------------------------------------

        [Test]
        public void SellStepsAreOneToEightAscendingPositiveIntegers()
        {
            AssertClean(Run(d => d["runStart"]["sellSteps"] = new JArray(1)));
            AssertClean(Run(d => d["runStart"]["sellSteps"] = new JArray(1, 2, 3, 4, 5, 6, 7, 8)));
            AssertClean(Run(d => d["runStart"]["sellSteps"] = new JArray(1, 2, 3, 4, 5, 6, 7, 8, "all")));
            AssertClean(Run(d => d["runStart"]["sellSteps"] = new JArray(1, 1_000_000_000_000_000_000L)));

            AssertOnly(Run(d => d["runStart"]["sellSteps"] = new JArray(1, 2, 3, 4, 5, 6, 7, 8, 9)), "sell.steps", "/runStart/sellSteps");
            AssertOnly(Run(d => d["runStart"]["sellSteps"] = new JArray("all")), "sell.steps", "/runStart/sellSteps");
            AssertOnly(Run(d => d["runStart"]["sellSteps"] = new JArray()), "sell.steps", "/runStart/sellSteps");
        }

        [Test]
        public void SellStepsRejectNonPositiveDuplicateDescendingAndTooLargeEntriesAtTheirIndex()
        {
            AssertOnly(Run(d => d["runStart"]["sellSteps"] = new JArray(1, 0)), "sell.steps", "/runStart/sellSteps/1");
            AssertOnly(Run(d => d["runStart"]["sellSteps"] = new JArray(1, -5)), "sell.steps", "/runStart/sellSteps/1");
            AssertOnly(Run(d => d["runStart"]["sellSteps"] = new JArray(5, 5)), "sell.steps", "/runStart/sellSteps/1");
            AssertOnly(Run(d => d["runStart"]["sellSteps"] = new JArray(10, 5)), "sell.steps", "/runStart/sellSteps/1");
            AssertOnly(Run(d => d["runStart"]["sellSteps"] = new JArray(1, 1_000_000_000_000_000_001L)), "sell.steps", "/runStart/sellSteps/1");
        }

        [Test]
        public void SellStepsOverTheReaderCapAreStillThisRuleNotAGenericRangeError()
        {
            var diagnostics = Run(d => d["runStart"]["sellSteps"] = new JArray(Enumerable.Range(1, 64).Cast<object>().ToArray()));
            Assert.That(diagnostics.Select(x => x.Code).Distinct(), Is.EqualTo(new[] { "sell.steps" }), Describe(diagnostics));
        }

        // ---- live-gap.range -------------------------------------------------------------------------------

        [Test]
        public void LiveGapThresholdIsOneSecondToEightHours()
        {
            AssertOnly(Run(d => d["runStart"]["liveGapThresholdMs"] = 999), "live-gap.range", "/runStart/liveGapThresholdMs");
            AssertClean(Run(d => d["runStart"]["liveGapThresholdMs"] = 1_000));
            AssertClean(Run(d => d["runStart"]["liveGapThresholdMs"] = 28_800_000));
            AssertOnly(Run(d => d["runStart"]["liveGapThresholdMs"] = 28_800_001), "live-gap.range", "/runStart/liveGapThresholdMs");
            AssertOnly(Run(d => d["runStart"]["liveGapThresholdMs"] = 0), "live-gap.range", "/runStart/liveGapThresholdMs");
            AssertOnly(Run(d => d["runStart"]["liveGapThresholdMs"] = -1), "live-gap.range", "/runStart/liveGapThresholdMs");
        }

        // ---- contract.bonus.fields ------------------------------------------------------------------------

        [Test]
        public void BonusMemberIsPresentExactlyForBonusClassOffers()
        {
            AssertClean(Run(null));
            AssertOnly(Run(d => ((JObject)d["contracts"][3]).Remove("bonus")), "contract.bonus.fields", "/contracts/3/bonus");
            AssertOnly(Run(d => d["contracts"][0]["bonus"] = d["contracts"][3]["bonus"].DeepClone()), "contract.bonus.fields", "/contracts/0/bonus");
            AssertOnly(
                Run(d =>
                {
                    d["contracts"][3]["class"] = "standing";
                    d["contracts"][3]["rewardRatioPpm"] = 1_500_000;
                }),
                "contract.bonus.fields",
                "/contracts/3/bonus");
        }

        [Test]
        public void AnUnknownClassIsOnlyTheClassCode()
        {
            var diagnostics = Run(d =>
            {
                d["contracts"][3]["class"] = "legendary";
                ((JObject)d["contracts"][3]).Remove("bonus");
            });
            Assert.That(diagnostics.Select(x => x.Code), Is.EqualTo(new[] { "contract.class.unknown" }), Describe(diagnostics));
        }

        [Test]
        public void BonusTriggerGetsTheFullUnlockValidationIncludingReachability()
        {
            // A resource nothing produces: its production trigger can never hold.
            Action<JObject> ghost = d => ((JArray)d["resources"]).Add(new JObject
            {
                ["id"] = "iron.zzz",
                ["label"] = "Ghost",
                ["presentationKey"] = "F003-RES-IRON-ORE",
                ["basePrice"] = 1,
                ["unlock"] = new JObject { ["kind"] = "always" },
            });
            AssertClean(Run(d =>
            {
                ghost(d);
                d["contracts"][3]["bonus"]["trigger"] = new JObject { ["kind"] = "resourceProduced", ["id"] = "iron.gear" };
            }));
            AssertOnly(
                Run(d =>
                {
                    ghost(d);
                    d["contracts"][3]["bonus"]["trigger"] = new JObject { ["kind"] = "resourceProduced", ["id"] = "iron.zzz" };
                }),
                "unlock.unreachable",
                "/contracts/3/bonus/trigger");
            AssertOnly(
                Run(d => d["contracts"][3]["bonus"]["trigger"] = new JObject { ["kind"] = "researchPurchased", ["id"] = "research.missing" }),
                "ref.research.unknown",
                "/contracts/3/bonus/trigger/id");
        }

        // ---- contract.ratio.* ---------------------------------------------------------------------------------

        [Test]
        public void StandingDeliveryRatioCeilingAppliesFromRevisionThree()
        {
            AssertClean(Run(d => d["contracts"][0]["rewardRatioPpm"] = 1_500_000));
            AssertOnly(Run(d => d["contracts"][0]["rewardRatioPpm"] = 1_500_001), "contract.ratio.standing", "/contracts/0/rewardRatioPpm");
            AssertClean(Run(d =>
            {
                d["contentRevision"] = 2;
                d["contracts"][0]["rewardRatioPpm"] = 1_750_000;
            }));
            AssertClean(Run(d =>
            {
                d["contentRevision"] = 2;
                d["contracts"][0]["rewardRatioPpm"] = 2_000_000;
            }));
        }

        [Test]
        public void BonusRatioCeilingAppliesFromRevisionThree()
        {
            Action<JObject, long, int> set = (d, ratio, revision) =>
            {
                d["runStart"]["maxRewardRatioPpm"] = 3_000_000;
                d["contentRevision"] = revision;
                d["contracts"][3]["rewardRatioPpm"] = ratio;
            };
            AssertClean(Run(d => set(d, 2_000_000, 3)));
            AssertOnly(Run(d => set(d, 2_000_001, 3)), "contract.ratio.bonus", "/contracts/3/rewardRatioPpm");
            AssertClean(Run(d => set(d, 2_000_001, 2)));
            AssertClean(Run(d => set(d, 2_500_000, 2)));
        }

        [Test]
        public void TheV2RatioCheckStaysUngatedAndAViolationIsNeverReportedTwice()
        {
            // Floor and ceiling of the v2 rule: one diagnostic at either revision, never the v3 code on top of it.
            foreach (var revision in new[] { 2, 3 })
            {
                AssertOnly(
                    Run(d =>
                    {
                        d["contentRevision"] = revision;
                        d["contracts"][0]["rewardRatioPpm"] = 999_999;
                    }),
                    "contract.reward.ratio",
                    "/contracts/0/rewardRatioPpm");
                AssertOnly(
                    Run(d =>
                    {
                        d["contentRevision"] = revision;
                        d["contracts"][0]["rewardRatioPpm"] = 2_000_001;
                    }),
                    "contract.reward.ratio",
                    "/contracts/0/rewardRatioPpm");
                AssertOnly(
                    Run(d =>
                    {
                        d["contentRevision"] = revision;
                        d["contracts"][3]["rewardRatioPpm"] = 2_000_001;
                    }),
                    "contract.reward.ratio",
                    "/contracts/3/rewardRatioPpm");
            }
        }

        [Test]
        public void ProductionOffersAreOutsideTheStandingRatioRule()
        {
            // The 1.5 ceiling is for delivery; a standing production offer above it is judged by contract.ratio.produce (T320), which allows 1.9 here.
            AssertClean(Run(d =>
            {
                d["contracts"][0]["objective"] = "produce";
                d["contracts"][0]["rewardRatioPpm"] = 1_900_000;
            }));
        }

        // ---- contract.quantity.cap and contract.produce.min -------------------------------------------------

        [Test]
        public void DeliveryQuantityStartsAtMostAtTheTopStorageCapacity()
        {
            // T320: index 0 alone is contract.quantity.cap; the next indices of the ladder's range are contract.quantity.capacity (see below), so the
            // base is chosen to stay within the capacity through the highest ladder index (9).
            AssertClean(Run(d => d["contracts"][0]["quantity"]["base"] = 3_000));
            AssertOnly(Run(d => d["contracts"][0]["quantity"]["base"] = 4_001), "contract.quantity.cap", "/contracts/0/quantity/base");
            AssertOnly(Run(d => d["contracts"][0]["quantity"]["base"] = 4_000), "contract.quantity.capacity", "/contracts/0/quantity");
            AssertClean(Run(d =>
            {
                d["storageLevels"][2]["capacity"] = 5_000;
                d["contracts"][0]["quantity"]["base"] = 4_400;
            }));

            // A production offer is not held to the storage capacity.
            AssertClean(Run(d =>
            {
                d["contracts"][0]["objective"] = "produce";
                d["contracts"][0]["quantity"]["base"] = 4_001;
            }));
        }

        [Test]
        public void ProductionQuantityStartsAtLeastAtTheContentMinimum()
        {
            Action<JObject, long> produce = (d, quantity) =>
            {
                d["contracts"][0]["objective"] = "produce";
                d["contracts"][0]["quantity"]["base"] = quantity;
            };
            AssertOnly(Run(d => produce(d, 9)), "contract.produce.min", "/contracts/0/quantity/base");
            AssertClean(Run(d => produce(d, 10)));

            // A delivery offer is not held to the production minimum.
            AssertClean(Run(d => d["contracts"][0]["quantity"]["base"] = 1));
        }

        // ---- milestone.order ------------------------------------------------------------------------------

        [Test]
        public void MilestonesStrictlyAscendInsideZeroAndOneMillion()
        {
            Action<JObject, long[]> set = (d, values) => d["milestones"] = new JArray(values.Select(v => (object)new JObject { ["fractionPpm"] = v }).ToArray());

            AssertClean(Run(d => set(d, new long[] { 1 })));
            AssertClean(Run(d => set(d, new long[] { 999_999 })));
            AssertClean(Run(d => set(d, new long[] { 1, 2, 999_999 })));
            AssertClean(Run(d => set(d, new long[0])));

            AssertOnly(Run(d => set(d, new long[] { 0 })), "milestone.order", "/milestones/0/fractionPpm");
            AssertOnly(Run(d => set(d, new long[] { 1_000_000 })), "milestone.order", "/milestones/0/fractionPpm");
            AssertOnly(Run(d => set(d, new long[] { 500, 500 })), "milestone.order", "/milestones/1/fractionPpm");
            AssertOnly(Run(d => set(d, new long[] { 500, 400 })), "milestone.order", "/milestones/1/fractionPpm");
            AssertOnly(Run(d => set(d, new long[] { 100, 1_000_000, 200 })), "milestone.order", "/milestones/1/fractionPpm");
        }

        // ---- build.base.bound -------------------------------------------------------------------------------

        [Test]
        public void BuildBaseAndCapAreAtMostTenToTheTwelfth()
        {
            AssertClean(Run(d =>
            {
                d["machines"][1]["buildPrice"]["base"] = 1_000_000_000_000L;
                d["machines"][1]["buildPrice"]["cap"] = 1_000_000_000_000L;
                d["runStart"]["startingCash"] = 1_000_000_000_000L; // T358: start.reachable needs the Cash to cover the 10^12 Miner
            }));
            AssertOnly(Run(d => d["machines"][1]["buildPrice"]["base"] = 1_000_000_000_001L), "build.base.bound", "/machines/1/buildPrice/base");
            AssertClean(Run(d => d["machines"][1]["buildPrice"]["cap"] = 1_000_000_000_000L));
            AssertOnly(Run(d => d["machines"][1]["buildPrice"]["cap"] = 1_000_000_000_001L), "build.base.bound", "/machines/1/buildPrice/cap");
            AssertOnly(Run(d => d["machines"][1]["buildPrice"]["cap"] = 1_000_000_000_000_000_000L), "build.base.bound", "/machines/1/buildPrice/cap");

            // A negative number stays a plain range error.
            AssertOnly(Run(d => d["machines"][1]["buildPrice"]["base"] = -1), "value.out-of-range", "/machines/1/buildPrice/base");
        }

        // ---- cost.floor -------------------------------------------------------------------------------------

        [Test]
        public void CombinedUpgradeCostDiscountMayNotFallBelowHalf()
        {
            // research.tool-discount: allMachines, 3 levels. 0.7937^3 is just below 0.5, 0.793701^3 just above.
            var below = Run(d => d["research"][2]["effect"]["valuePerLevelPpm"] = 793_700);
            Assert.That(below.Select(x => x.Code + "@" + x.Path), Is.EqualTo(new[] { "cost.floor@/machines/0", "cost.floor@/machines/1", "cost.floor@/machines/2", "cost.floor@/machines/3" }));
            AssertClean(Run(d => d["research"][2]["effect"]["valuePerLevelPpm"] = 793_701));
            AssertClean(Run(d => d["research"][2]["effect"]["valuePerLevelPpm"] = 800_000));

            var all = Run(d => d["research"][2]["effect"]["valuePerLevelPpm"] = 500_000);
            Assert.That(all.Count(x => x.Code == "cost.floor"), Is.EqualTo(4), "one per machine; " + Describe(all));
            Assert.That(all.Select(x => x.Code).Distinct(), Is.EqualTo(new[] { "cost.floor" }), Describe(all));
        }

        [Test]
        public void CostFloorCombinesResearchAndIndustryAndSeparatesMachinesFromTheExpansionTrack()
        {
            // Research 0.95^3 = 0.857375; an Industry discount of 0.58 brings the product to 0.4973.
            Action<JObject, long, string> industryDiscount = (d, ppm, target) =>
            {
                var upgrades = (JArray)d["industryUpgrades"];
                upgrades.Add(new JObject
                {
                    ["id"] = "industry.toolmaker",
                    ["label"] = "Toolmaker",
                    ["maxLevel"] = 1,
                    ["ipCost"] = new JObject { ["base"] = 5, ["growthPpm"] = 1_500_000, ["rounding"] = "ceil" },
                    ["decayPpm"] = 950_000,
                    ["practicalCeilingLevel"] = 1,
                    ["minGainPpm"] = 5_000,
                    ["effect"] = new JObject
                    {
                        ["kind"] = "upgradeCost",
                        ["target"] = target == "expansion"
                            ? new JObject { ["kind"] = "expansion" }
                            : target == "allMachines"
                                ? new JObject { ["kind"] = "allMachines" }
                                : new JObject { ["kind"] = "machine", ["id"] = target },
                        ["valuePerLevelPpm"] = ppm,
                    },
                });
            };

            AssertClean(Run(d => industryDiscount(d, 600_000, "allMachines")));
            var combined = Run(d => industryDiscount(d, 580_000, "allMachines"));
            Assert.That(combined.Count(x => x.Code == "cost.floor"), Is.EqualTo(4), Describe(combined));

            // A single-machine discount only trips that machine.
            var one = Run(d => industryDiscount(d, 580_000, "machine.press"));
            AssertOnly(one, "cost.floor", "/machines/2");
            Assert.That(one[0].EntryId, Is.EqualTo("machine.press"));

            // The expansion track is separate from the machine track.
            AssertOnly(Run(d => industryDiscount(d, 499_999, "expansion")), "cost.floor", "/storageLevels");
            AssertClean(Run(d => industryDiscount(d, 500_000, "expansion")));
        }

        // ---- speed.level.dead (extension) --------------------------------------------------------------------

        [Test]
        public void AStartingStorageLevelUpgradeWhoseLastLevelAddsNothingIsDead()
        {
            // The guide example has 3 storage levels and Bigger Warehouse with 2 levels of +1: the start level reaches 3 = the top, level 2 adds one.
            AssertClean(Run(null));
            AssertOnly(Run(d => { d["industryUpgrades"][1]["maxLevel"] = 3; d["industryUpgrades"][1]["practicalCeilingLevel"] = 3; }), "speed.level.dead", "/industryUpgrades/1/maxLevel");
            AssertOnly(Run(d => d["industryUpgrades"][1]["effect"]["valuePerLevel"] = 2), "speed.level.dead", "/industryUpgrades/1/maxLevel");
            AssertClean(Run(d => { d["industryUpgrades"][1]["maxLevel"] = 1; d["industryUpgrades"][1]["practicalCeilingLevel"] = 1; }));
            AssertClean(Run(d => d["storageLevels"] = new JArray(d["storageLevels"][0].DeepClone(), d["storageLevels"][1].DeepClone(), d["storageLevels"][2].DeepClone(), new JObject
            {
                ["level"] = 4,
                ["capacity"] = 8_000,
                ["cost"] = 40_000,
            })));
        }

        // ---- cost.floor performance ---------------------------------------------------------------------

        [Test]
        public void CostFloorStaysFastWithManyMachinesAndManyNearOneSources()
        {
            // 304 machines x 300 allMachines sources x 1024 levels (0.999999^(300 x 1024) is about 0.74, so no early exit): the old per-machine
            // fold cost about 304 x 300 x 1024 = 9.3 x 10^7 multiplies; the shared fold costs 300 x 1024 = 3 x 10^5 once plus each machine's own sources.
            var diagnostics = default(IReadOnlyList<ContentDiagnostic>);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            diagnostics = Run(d =>
            {
                var template = d["machines"][1];
                var machines = (JArray)d["machines"];
                for (var i = 0; i < 300; i++)
                {
                    var clone = template.DeepClone();
                    clone["id"] = "machine.bulk" + i.ToString("D4");
                    machines.Add(clone);
                }

                var research = (JArray)d["research"];
                for (var i = 0; i < 300; i++)
                {
                    var clone = research[2].DeepClone();
                    clone["id"] = "research.bulk" + i.ToString("D4");
                    clone["prerequisites"] = new JArray();
                    clone["effect"]["valuePerLevelPpm"] = 999_999;
                    clone["maxLevel"] = 1024;
                    research.Add(clone);
                }
            });
            watch.Stop();

            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(2000), Describe(diagnostics));
            Assert.That(diagnostics.Any(x => x.Code == "cost.floor"), Is.False, Describe(diagnostics));
        }

        // ---- chain dominance margin and ladder order ------------------------------------------------------------

        [Test]
        public void ChainDominanceMarginMustBePositive()
        {
            AssertOnly(Run(d => d["chainDominanceMarginPpm"] = 0), "value.out-of-range", "/chainDominanceMarginPpm");
            AssertClean(Run(d => d["chainDominanceMarginPpm"] = 1));
        }

        [Test]
        public void LadderEntriesAreAPermutableFamilyAndNeedNoDocumentOrder()
        {
            // The contract's "ascending" is not enforced on the document; RunCatalogBuilder sorts the entries by (index, itemId) since T320.
            AssertClean(Run(d => d["contractItemLadder"]["entries"][0]["index"] = 5));
            AssertClean(Run(d => d["contractItemLadder"]["entries"][1]["index"] = 0));
        }

        // ---- T320: contract.ratio.produce, contract.quantity.capacity, ladder.uptime ----------------------------

        [Test]
        public void ProductionRatioIsPositiveAndStaysWithinTwoTogetherWithTheStandingDeliveryRatioOfTheResource()
        {
            // contracts[1] is the Plate delivery offer; as a production offer it has no standing delivery offer left for iron.plate.
            Action<JObject, long> producePlate = (d, ratio) =>
            {
                d["contracts"][1]["objective"] = "produce";
                d["contracts"][1]["rewardRatioPpm"] = ratio;
            };
            AssertOnly(Run(d => producePlate(d, 0)), "contract.ratio.produce", "/contracts/1/rewardRatioPpm");
            AssertClean(Run(d => producePlate(d, 1)));
            AssertClean(Run(d => producePlate(d, 2_000_000)));
            AssertOnly(Run(d => producePlate(d, 2_000_001)), "contract.ratio.produce", "/contracts/1/rewardRatioPpm");

            // contracts[3] is the bonus Gear offer; the standing Gear delivery offer (contracts[0]) pays 1,500,000, so a production Gear offer gets 500,000.
            Action<JObject, long> produceGear = (d, ratio) =>
            {
                d["contracts"][3]["objective"] = "produce";
                d["contracts"][3]["rewardRatioPpm"] = ratio;
            };
            AssertClean(Run(d => produceGear(d, 500_000)));
            AssertOnly(Run(d => produceGear(d, 500_001)), "contract.ratio.produce", "/contracts/3/rewardRatioPpm");

            // The highest standing delivery ratio of the resource counts, whichever offer carries it.
            AssertClean(Run(d =>
            {
                produceGear(d, 500_001);
                d["contracts"][0]["rewardRatioPpm"] = 1_100_000;
            }));
            AssertOnly(Run(d =>
            {
                produceGear(d, 500_001);
                d["contracts"][0]["rewardRatioPpm"] = 1_100_000;
                d["contracts"][1]["resourceId"] = "iron.gear";
                d["contracts"][1]["rewardRatioPpm"] = 1_500_000;
            }), "contract.ratio.produce", "/contracts/3/rewardRatioPpm");

            // A bonus delivery offer is not a standing one: it adds nothing to the stack.
            AssertClean(Run(d =>
            {
                d["contracts"][0]["objective"] = "produce";
                d["contracts"][0]["rewardRatioPpm"] = 1_900_000;
            }));

            // Revision 2 is not held to the rule.
            AssertClean(Run(d =>
            {
                d["contentRevision"] = 2;
                producePlate(d, 0);
            }));
            AssertClean(Run(d =>
            {
                d["contentRevision"] = 2;
                produceGear(d, 1_900_000);
            }));
        }

        [Test]
        public void DeliveryQuantityFitsTheTopCapacityThroughTheHighestLadderIndex()
        {
            // The guide ladder reaches index 9 and the top capacity is 4,000: 3,990 x 1.01 (ceil) is 4,030 at index 1.
            AssertOnly(Run(d => d["contracts"][0]["quantity"]["base"] = 3_990), "contract.quantity.capacity", "/contracts/0/quantity");

            // The highest index decides: 3,990 only has to fit through index 0 when the ladder has no later entry.
            AssertClean(Run(d =>
            {
                d["contracts"][0]["quantity"]["base"] = 3_990;
                var entries = (JArray)d["contractItemLadder"]["entries"];
                entries[1]["index"] = 0;
                entries[2]["index"] = 0;
            }));

            // A flat requirement at the top capacity is allowed; a production offer is not held to it; revision 2 is not either.
            AssertClean(Run(d =>
            {
                d["contracts"][0]["quantity"] = new JObject { ["base"] = 4_000, ["growthPpm"] = 1_000_000, ["rounding"] = "ceil" };
            }));
            AssertClean(Run(d =>
            {
                d["contracts"][0]["objective"] = "produce";
                d["contracts"][0]["quantity"]["base"] = 3_990;
                d["contracts"][0]["rewardRatioPpm"] = 1_000_000;
            }));
            AssertClean(Run(d =>
            {
                d["contentRevision"] = 2;
                d["contracts"][0]["quantity"]["base"] = 3_990;
            }));
        }

        [Test]
        public void LadderUptimeComparesPerRunCapTimesDurationWithTheUptimeShareOfTheMinimumRun()
        {
            // Guide values: perRunCap 3, every ladder Item 60,000 ms, minRunDurationMs 1,800,000: 3 x 60,000 = 180,000 ms of up to 1,800,000 x share.
            AssertClean(Run(d => d["runStart"]["uptimeMaxPpm"] = 100_000));
            var all = Run(d => d["runStart"]["uptimeMaxPpm"] = 99_999);
            Assert.That(all.Count, Is.EqualTo(3), Describe(all));
            Assert.That(all.All(x => x.Code == "ladder.uptime" && x.Path == "/contractItemLadder/perRunCap"), Is.True, Describe(all));

            // One Item decides: 3 x 300,000 ms is exactly 500,000 ppm of 1,800,000 ms; one millisecond more is not.
            AssertClean(Run(d => d["items"][2]["durationMs"] = 300_000));
            var one = Run(d => d["items"][2]["durationMs"] = 300_001);
            Assert.That(one.Count, Is.EqualTo(1), Describe(one));
            Assert.That(one[0].Code, Is.EqualTo("ladder.uptime"));
            Assert.That(one[0].EntryId, Is.EqualTo("item.tuned-gearbox"));

            // The cap scales the bound; a cap of 0 grants nothing.
            AssertClean(Run(d => { d["contractItemLadder"]["perRunCap"] = 2; d["runStart"]["uptimeMaxPpm"] = 66_667; }));
            Assert.That(Run(d => { d["contractItemLadder"]["perRunCap"] = 2; d["runStart"]["uptimeMaxPpm"] = 66_666; }).Count, Is.EqualTo(3));
            AssertClean(Run(d => { d["contractItemLadder"]["perRunCap"] = 0; d["runStart"]["uptimeMaxPpm"] = 0; }));

            // Revision 2 is not held to the rule.
            AssertClean(Run(d =>
            {
                d["contentRevision"] = 2;
                d["runStart"]["uptimeMaxPpm"] = 1;
            }));
        }
    }
}
