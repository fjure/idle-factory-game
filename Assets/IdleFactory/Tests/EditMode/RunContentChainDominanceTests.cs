using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T316 (issue #379): the <c>chain.dominance</c> rule. Efficiency E(P) = price / slot-time per unit over a balanced mix must not fall along
    /// the chain, and every terminal resource must beat Ore-only by the margin, at base level and at every machine milestone level. Gated on
    /// contentRevision 3. Every case starts from the guide example (revision 3; resources sorted by id: bar, gear, ore, plate; recipes: forge,
    /// mine, press, smelt). The exact threshold below comes from the integer speed tables: at machine level 25 the Miner (x1.08 per level) has
    /// pulled ahead of the downstream machines (x1.05, capped at level 20), so the tightest terminal is the Plate with E(Plate) / E(Ore) =
    /// 941651000000 / 4112229 / 10^6 + 1, a margin below 228,988 ppm. At base level the ratio would allow 599,999 ppm.
    /// </summary>
    public sealed class RunContentChainDominanceTests
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
            string.Join(" | ", diagnostics.Select(d => d.Code + "@" + d.Path + (d.EntryId == null ? string.Empty : "#" + d.EntryId)));

        private static List<ContentDiagnostic> Chain(IEnumerable<ContentDiagnostic> diagnostics) =>
            diagnostics.Where(d => d.Code == "chain.dominance").ToList();

        [Test]
        public void GuideExampleSatisfiesTheRuleAtRevisionThree()
        {
            var diagnostics = Run(null);

            Assert.That(Chain(diagnostics), Is.Empty, Describe(diagnostics));
        }

        [Test]
        public void MarginBoundaryIsExactAndIsDecidedAtTheTightestMilestoneLevel()
        {
            Assert.That(Run(d => d["chainDominanceMarginPpm"] = 228_987), Is.Empty);

            var plateOnly = Run(d => d["chainDominanceMarginPpm"] = 228_988);
            Assert.That(plateOnly.Count, Is.EqualTo(1), Describe(plateOnly));
            Assert.That(plateOnly[0].Code, Is.EqualTo("chain.dominance"));
            Assert.That(plateOnly[0].Path, Is.EqualTo("/chainDominanceMarginPpm"));
            Assert.That(plateOnly[0].EntryId, Is.EqualTo("iron.plate"));

            // The Gear has more headroom (ratio 1.26141 at level 25): it fails from 261,407 ppm on, and both terminals are then reported.
            Assert.That(Chain(Run(d => d["chainDominanceMarginPpm"] = 261_406)).Count, Is.EqualTo(1));
            var failing = Run(d => d["chainDominanceMarginPpm"] = 261_407);
            Assert.That(failing.Select(x => x.EntryId).OrderBy(x => x, StringComparer.Ordinal), Is.EqualTo(new[] { "iron.gear", "iron.plate" }).AsCollection);
            Assert.That(failing.All(x => x.Code == "chain.dominance" && x.Path == "/chainDominanceMarginPpm"), Is.True, Describe(failing));

            // Base level alone would accept this margin (the ratio at level 1 is 1.6): the milestone levels are what reject it.
            Assert.That(Chain(Run(d => d["chainDominanceMarginPpm"] = 599_999)).Count, Is.EqualTo(2));
            Assert.That(Chain(Run(d => d["chainDominanceMarginPpm"] = 1_000_000_000_000_000_000L)).Count, Is.EqualTo(2));
        }

        [Test]
        public void ARuleViolationIsReportedOncePerResourceNotPerLevel()
        {
            var diagnostics = Chain(Run(d => d["chainDominanceMarginPpm"] = 900_000));
            // 900,000 ppm fails at levels 10, 20 and 25 (and not at 1) for both terminals, yet each terminal appears once.
            Assert.That(diagnostics.Count, Is.EqualTo(2), Describe(diagnostics));
            Assert.That(diagnostics.Any(x => x.Message.Contains("level")), Is.True);
        }

        [Test]
        public void EfficiencyThatFallsAlongTheChainIsReportedAtTheProducingRecipe()
        {
            // Bar price 3 -> 1: the Bar is worth less per slot-second than the Ore it is smelted from.
            var diagnostics = Chain(Run(d => d["resources"][0]["basePrice"] = 1));

            Assert.That(diagnostics.Any(x => x.Path == "/recipes/3" && x.EntryId == "recipe.smelt-iron-bar"), Is.True, Describe(diagnostics));
            Assert.That(diagnostics.All(x => x.Path.StartsWith("/recipes/", StringComparison.Ordinal) || x.Path == "/chainDominanceMarginPpm"), Is.True);
        }

        [Test]
        public void ARuleOnlyAppliesFromRevisionThree()
        {
            Assert.That(Run(d => { d["contentRevision"] = 2; d["resources"][0]["basePrice"] = 1; d["chainDominanceMarginPpm"] = 1_000_000_000L; }), Is.Empty);
            Assert.That(Chain(Run(d => { d["contentRevision"] = 3; d["resources"][0]["basePrice"] = 1; })), Is.Not.Empty);
        }

        [Test]
        public void ChainSlowerOrCheaperThanOreFailsTheTerminalPredicate()
        {
            // Gear and Plate worth as little as the Ore: no terminal beats Ore-only by any margin.
            var diagnostics = Chain(Run(d =>
            {
                d["resources"][1]["basePrice"] = 1;
                d["resources"][3]["basePrice"] = 1;
            }));

            Assert.That(diagnostics.Count(x => x.Path == "/chainDominanceMarginPpm"), Is.EqualTo(2), Describe(diagnostics));
        }

        [Test]
        public void InputsAnotherRuleRejectsAreSkippedSoNothingIsReportedTwice()
        {
            var negative = Run(d => d["resources"][0]["basePrice"] = -1);
            Assert.That(negative.Any(x => x.Code == "resource.price.negative"), Is.True);
            Assert.That(Chain(negative), Is.Empty, Describe(negative));

            var zeroDuration = Run(d => d["recipes"][0]["durationTicks"] = 0);
            Assert.That(zeroDuration.Any(x => x.Code == "recipe.duration.non-positive"), Is.True);
            Assert.That(Chain(zeroDuration), Is.Empty, Describe(zeroDuration));
        }

        [Test]
        public void ARecipeCycleHasNoSlotTimeAndDoesNotThrowOrReport()
        {
            // Mining now consumes a Bar: Ore needs Bar needs Ore.
            IReadOnlyList<ContentDiagnostic> diagnostics = null;
            Assert.DoesNotThrow(() => diagnostics = Run(d =>
                d["recipes"][1]["inputs"] = new JArray(new JObject { ["resourceId"] = "iron.bar", ["amount"] = 1 })));

            Assert.That(Chain(diagnostics), Is.Empty, Describe(diagnostics));
        }
    }
}
