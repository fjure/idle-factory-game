using System.Linq;
using System.Reflection;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T540 (Feature 006, FR-036, research R15, contracts/world-presentation.md section 3): the development stage rule <c>t &lt;= 0 gives Early; otherwise
    /// clamp(floor(3 * u / t), 0, 2)</c>, its secondary bins, and the owner decision that the prestige count is never an input.
    /// </summary>
    public sealed class DevelopmentStageTests
    {
        [TestCase(0, 0)]
        [TestCase(3, 0)]
        [TestCase(4, 1)]
        [TestCase(7, 1)]
        [TestCase(8, 2)]
        [TestCase(12, 2)]
        public void TheStepsOfTwelveLocationsFallAtFourAndEight(int unlocked, int expected)
        {
            Assert.That(DevelopmentStage.StageOf(unlocked, 12), Is.EqualTo(expected), unlocked + " of 12");
        }

        [Test]
        public void ThreeLocationsStepAtOneAndTwoAndOverTheMaximumClampsToEstablished()
        {
            Assert.That(DevelopmentStage.StageOf(0, 3), Is.EqualTo(0));
            Assert.That(DevelopmentStage.StageOf(1, 3), Is.EqualTo(1));
            Assert.That(DevelopmentStage.StageOf(2, 3), Is.EqualTo(2));
            Assert.That(DevelopmentStage.StageOf(99, 12), Is.EqualTo(2), "more unlocked than the maximum clamps to 2");
            Assert.That(DevelopmentStage.StageOf(-5, 12), Is.EqualTo(0), "a negative count clamps to 0");
        }

        [TestCase(0, 0)]
        [TestCase(-1, 0)]
        [TestCase(5, 0)]
        [TestCase(12, -3)]
        public void ANonPositiveMaximumGivesEarly(int unlocked, int total)
        {
            Assert.That(DevelopmentStage.StageOf(unlocked, total), Is.EqualTo(0));
            Assert.That(DevelopmentStage.Evaluate(unlocked, total, 3, 4, 5).Stage, Is.EqualTo(0));
        }

        [Test]
        public void TheShippedFactoryLevelsMapToTheirStages()
        {
            // The shipped Iron content opens 4, 6, 8, 10 and 12 locations (levels 1 to 5) of 12: Partial from level 1, Established from level 3. Early is reachable only
            // below a third of the locations (a smaller catalog or a development catalog).
            var stages = new[] { 4, 6, 8, 10, 12 }.Select(u => DevelopmentStage.StageOf(u, 12)).ToArray();
            Assert.That(stages, Is.EqualTo(new[] { 1, 1, 2, 2, 2 }));
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

            var value = DevelopmentStage.Evaluate(8, 12, 8, 30, 40);
            Assert.That(value.Stage, Is.EqualTo(2));
            Assert.That(value.ActivityDensity, Is.EqualTo(2), "8 active of 8 unlocked");
            Assert.That(value.LevelDensity, Is.EqualTo(2), "levels 30 of 40 is three quarters: bin 2");
            Assert.That(value.DensityBin, Is.EqualTo(4));
            Assert.That(DevelopmentStage.Evaluate(8, 12, 0, 0, 40).DensityBin, Is.Zero);
        }

        [Test]
        public void ThePrestigeCountIsNotAnInput()
        {
            // The rule is a function of the current run's locations, activity and levels only: none of its entry points names or accepts a prestige count, so a
            // run that has prestiged ten times and a first run with the same factory look exactly alike (the stage resets with the run, owner decision).
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

            var first = DevelopmentStage.Evaluate(8, 12, 5, 12, 40);
            var later = DevelopmentStage.Evaluate(8, 12, 5, 12, 40);
            Assert.That(later, Is.EqualTo(first), "the same factory gives the same value, however many prestiges came before");
        }

        [Test]
        public void ARunThatStartsAgainStartsAtItsOwnStage()
        {
            // After a prestige the new run has its starting factory level again (4 of 12) and its machines are gone: the value is that of a fresh run.
            var fresh = DevelopmentStage.Evaluate(4, 12, 0, 0, 0);
            Assert.That(fresh.Stage, Is.EqualTo(1));
            Assert.That(fresh.DensityBin, Is.Zero);
            var grown = DevelopmentStage.Evaluate(12, 12, 12, 40, 40);
            Assert.That(grown.Stage, Is.EqualTo(2));
            Assert.That(DevelopmentStage.Evaluate(4, 12, 0, 0, 0), Is.EqualTo(fresh), "reset: same inputs, same value");
        }
    }
}
