using System.Linq;
using System.Reflection;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T540 (Feature 006, FR-036, research R15, contracts/world-presentation.md section 3): the development stage rule with the owner decision of 2026-11-18,
    /// <c>t &lt;= u0 gives Early; otherwise clamp(floor(3 * (u - u0) / (t - u0)), 0, 2)</c> (u0: the locations unlocked at factory level 1), its secondary bins, and the
    /// rule that the prestige count is never an input.
    /// </summary>
    public sealed class DevelopmentStageTests
    {
        // 12 locations, u0 = 4 (the shipped factory level 1): the steps fall above 6 and above 9 (floor(3 * (u - 4) / 8) is 1 from u = 7 and 2 from u = 10).
        [TestCase(0, 0)]
        [TestCase(3, 0)]
        [TestCase(4, 0)]
        [TestCase(6, 0)]
        [TestCase(7, 1)]
        [TestCase(8, 1)]
        [TestCase(9, 1)]
        [TestCase(10, 2)]
        [TestCase(12, 2)]
        public void TheStepsOfTwelveLocationsWithFourAtLevelOneFallAtSevenAndTen(int unlocked, int expected)
        {
            Assert.That(DevelopmentStage.StageOf(unlocked, 12, 4), Is.EqualTo(expected), unlocked + " of 12, u0 4");
        }

        [Test]
        public void TheShippedFactoryLevelsGiveEarlyEarlyPartialEstablishedEstablished()
        {
            // Iron content: 4, 6, 8, 10 and 12 locations at factory levels 1 to 5, u0 = 4 (owner decision 2026-11-18).
            var stages = new[] { 4, 6, 8, 10, 12 }.Select(u => DevelopmentStage.StageOf(u, 12, 4)).ToArray();
            Assert.That(stages, Is.EqualTo(new[] { 0, 0, 1, 2, 2 }));
        }

        [Test]
        public void AFreshRunStartsAtEarlyForAnyStartingFactorySize()
        {
            foreach (var (u0, t) in new[] { (4, 12), (3, 12), (1, 3), (6, 20), (2, 7) })
            {
                Assert.That(DevelopmentStage.StageOf(u0, t, u0), Is.Zero, "u = u0 = " + u0 + " of " + t);
                Assert.That(DevelopmentStage.StageOf(t, t, u0), Is.EqualTo(2), "the maximum factory is Established");
            }
        }

        [Test]
        public void ThreeStepsBetweenTheStartAndTheMaximum()
        {
            // u0 = 0 reduces to the original floor(3u/t).
            Assert.That(DevelopmentStage.StageOf(0, 3, 0), Is.EqualTo(0));
            Assert.That(DevelopmentStage.StageOf(1, 3, 0), Is.EqualTo(1));
            Assert.That(DevelopmentStage.StageOf(2, 3, 0), Is.EqualTo(2));
            Assert.That(DevelopmentStage.StageOf(99, 12, 4), Is.EqualTo(2), "more unlocked than the maximum clamps to 2");
            Assert.That(DevelopmentStage.StageOf(-5, 12, 4), Is.EqualTo(0), "a negative count clamps to 0");
            Assert.That(DevelopmentStage.StageOf(2, 12, 4), Is.EqualTo(0), "fewer than u0 is still Early");
        }

        [TestCase(0, 0, 0)]
        [TestCase(5, 0, 0)]
        [TestCase(12, 12, 12)]
        [TestCase(12, 4, 4)]
        [TestCase(12, -3, -3)]
        [TestCase(12, 8, 12)]
        public void AMaximumNotAboveTheStartGivesEarly(int unlocked, int total, int baseUnlocked)
        {
            // t <= u0: there is nothing left to develop, so the stage is Early (and nothing divides by zero or a negative).
            Assert.That(DevelopmentStage.StageOf(unlocked, total, baseUnlocked), Is.EqualTo(0), "u " + unlocked + ", t " + total + ", u0 " + baseUnlocked);
            Assert.That(DevelopmentStage.Evaluate(unlocked, total, baseUnlocked, 3, 4, 5).Stage, Is.EqualTo(0));
        }

        [Test]
        public void TheSecondaryBinsAreThreeBinsOfActivityAndMeanLevel()
        {
            Assert.That(DevelopmentStage.BinOf(0, 8), Is.EqualTo(0));
            Assert.That(DevelopmentStage.BinOf(2, 8), Is.EqualTo(0));
            Assert.That(DevelopmentStage.BinOf(3, 8), Is.EqualTo(1));
            Assert.That(DevelopmentStage.BinOf(5, 8), Is.EqualTo(1));
            Assert.That(DevelopmentStage.BinOf(6, 8), Is.EqualTo(2));
            Assert.That(DevelopmentStage.BinOf(8, 8), Is.EqualTo(2));
            Assert.That(DevelopmentStage.BinOf(20, 8), Is.EqualTo(2), "a share over 1 clamps");
            Assert.That(DevelopmentStage.BinOf(3, 0), Is.EqualTo(0), "no denominator: bin 0");

            var value = DevelopmentStage.Evaluate(10, 12, 4, 10, 30, 40);
            Assert.That(value.Stage, Is.EqualTo(2));
            Assert.That(value.ActivityDensity, Is.EqualTo(2), "10 active of 10 unlocked");
            Assert.That(value.LevelDensity, Is.EqualTo(2), "levels 30 of 40 is three quarters: bin 2");
            Assert.That(value.DensityBin, Is.EqualTo(4));
            Assert.That(DevelopmentStage.Evaluate(10, 12, 4, 0, 0, 40).DensityBin, Is.Zero);
        }

        [Test]
        public void ThePrestigeCountIsNotAnInput()
        {
            // The rule is a function of the current run's locations (u, t and u0 from the catalog), activity and levels only: none of its entry points names or accepts a
            // prestige count, so a run that has prestiged ten times and a first run with the same factory look exactly alike (the stage resets with the run, owner decision).
            foreach (var method in typeof(DevelopmentStage).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                foreach (var parameter in method.GetParameters())
                {
                    Assert.That(parameter.Name.ToLowerInvariant(), Does.Not.Contain("prestige"), method.Name + " must not take a prestige input");
                }
            }

            foreach (var member in typeof(DevelopmentStageValue).GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.That(member.Name.ToLowerInvariant(), Does.Not.Contain("prestige"));
            }

            var first = DevelopmentStage.Evaluate(8, 12, 4, 5, 12, 40);
            var later = DevelopmentStage.Evaluate(8, 12, 4, 5, 12, 40);
            Assert.That(later, Is.EqualTo(first), "the same factory gives the same value, however many prestiges came before");
        }

        [Test]
        public void ARunThatStartsAgainStartsAtEarly()
        {
            var fresh = DevelopmentStage.Evaluate(4, 12, 4, 0, 0, 0);
            Assert.That(fresh.Stage, Is.EqualTo(0));
            Assert.That(fresh.DensityBin, Is.Zero);
            var grown = DevelopmentStage.Evaluate(12, 12, 4, 12, 40, 40);
            Assert.That(grown.Stage, Is.EqualTo(2));
            Assert.That(DevelopmentStage.Evaluate(4, 12, 4, 0, 0, 0), Is.EqualTo(fresh), "reset: same inputs, same value");
        }
    }
}
