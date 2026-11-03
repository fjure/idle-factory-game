using System;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T431: the shared presentation clock (contracts/presentation-and-state-cues.md "Animation"): integer-millisecond unscaled time,
    /// paused while suspended, <c>frame = floor(t * fps) mod frameCount</c>, and the effective reduced-motion state. The view-level
    /// behaviour (only running machines animate, the accent follows its body) is covered by <c>StateCueOverlayTests</c> and, through
    /// the clock, by <c>PresentationAllocationTests</c>.
    /// </summary>
    public sealed class PresentationAnimationClockTests
    {
        [TestCase(0L, 8f, 6, 0)]
        [TestCase(124L, 8f, 6, 0)]
        [TestCase(125L, 8f, 6, 1)]
        [TestCase(1000L, 8f, 6, 2)]
        [TestCase(1125L, 8f, 6, 3)]
        [TestCase(750L, 8f, 6, 0)]
        [TestCase(999L, 12f, 16, 11)]
        [TestCase(1000L, 12f, 16, 12)]
        [TestCase(1334L, 12f, 16, 0)]
        [TestCase(500L, 8f, 1, 0)]
        [TestCase(500L, 0f, 6, 0)]
        public void TheFrameFollowsFloorOfTimeTimesRateModuloTheFrameCount(long milliseconds, float fps, int frameCount, int expected)
        {
            Assert.That(PresentationAnimationClock.FrameAt(milliseconds, fps, frameCount), Is.EqualTo(expected));
        }

        [Test]
        public void TheFrameAcrossTheFullMachineRangeStaysInsideTheFrameCount()
        {
            for (var frameCount = 1; frameCount <= 16; frameCount++)
            {
                for (var milliseconds = 0L; milliseconds < 5000L; milliseconds += 37L)
                {
                    var frame = PresentationAnimationClock.FrameAt(milliseconds, 8f, frameCount);
                    Assert.That(frame, Is.InRange(0, frameCount - 1));
                }
            }
        }

        [Test]
        public void TimeAdvancesInIntegerMillisecondsFromTheSecondTick()
        {
            var clock = new PresentationAnimationClock();
            Assert.That(clock.Tick(10_000), Is.EqualTo(0), "the first tick only takes a reference");
            Assert.That(clock.Tick(10_016), Is.EqualTo(16));
            Assert.That(clock.Tick(10_050), Is.EqualTo(50));
            Assert.That(clock.Milliseconds, Is.EqualTo(50));
        }

        [Test]
        public void ATimeReadingThatGoesBackwardsIsIgnored()
        {
            var clock = new PresentationAnimationClock();
            clock.Tick(1000);
            clock.Tick(1100);
            Assert.That(clock.Tick(1050), Is.EqualTo(100));
            Assert.That(clock.Tick(1150), Is.EqualTo(200), "advances from the lower reading again");
        }

        [Test]
        public void SuspendPausesTimeAndTheSuspendedIntervalNeverCounts()
        {
            var clock = new PresentationAnimationClock();
            clock.Tick(0);
            clock.Tick(400);

            clock.SetSuspended(true);
            Assert.That(clock.Suspended, Is.True);
            Assert.That(clock.Tick(900), Is.EqualTo(400), "no advance while suspended");

            clock.SetSuspended(false);
            Assert.That(clock.Tick(60_000), Is.EqualTo(400), "the first tick after the resume takes a new reference");
            Assert.That(clock.Tick(60_100), Is.EqualTo(500));
        }

        [Test]
        public void SettingTheSameSuspendStateTwiceChangesNothing()
        {
            var clock = new PresentationAnimationClock();
            clock.Tick(0);
            clock.Tick(100);
            clock.SetSuspended(false);
            Assert.That(clock.Tick(200), Is.EqualTo(200), "a redundant resume does not drop the reference");
            clock.SetSuspended(true);
            clock.SetSuspended(true);
            clock.SetSuspended(false);
            Assert.That(clock.Tick(5000), Is.EqualTo(200));
        }

        [Test]
        public void ReducedMotionIsOffUntilSetAndFollowsTheLastValue()
        {
            var clock = new PresentationAnimationClock();
            Assert.That(clock.ReducedMotion, Is.False);
            clock.SetReducedMotion(true);
            Assert.That(clock.ReducedMotion, Is.True);
            clock.SetReducedMotion(false);
            Assert.That(clock.ReducedMotion, Is.False);
        }

        [Test]
        public void DrivingWithoutAViewStillAdvancesTheClock()
        {
            var clock = new PresentationAnimationClock();
            clock.Drive(0, null);
            clock.Drive(250, null);
            Assert.That(clock.Milliseconds, Is.EqualTo(250));
        }
    }
}
