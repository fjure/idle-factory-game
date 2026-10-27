using System.Collections;
using System.IO;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T308 (F5-R11, AR-10, OPS-1): the null-default <see cref="ITimeScaleSource"/> seam of <see cref="FactoryRuntimeHost"/> and
    /// its S_max clamp, driven through a fake inside the normal Play Mode suite (no <c>IDLEFACTORY_DEV</c> needed).
    /// </summary>
    public sealed class TimeScaleSeamTests
    {
        private const long TicksPerSecond = 10_000_000L;
        private const long ClampTicks = 10L * TicksPerSecond;

        private GameObject hostObject;
        private FactoryRuntimeHost runtimeHost;
        private RunSession session;
        private double currentActiveSeconds;
        private readonly MachineStateView[] buffer = new MachineStateView[4];

        private sealed class FakeTimeScaleSource : ITimeScaleSource
        {
            public double Scale { get; set; } = 1d;
            public double Reported { get; private set; } = double.NaN;
            public int ReportCount { get; private set; }

            public void ReportEffectiveMultiplier(double effectiveMultiplier)
            {
                Reported = effectiveMultiplier;
                ReportCount++;
            }
        }

        [UnitySetUp]
        public IEnumerator CreateProducingRun()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The run fixture loads the packaged content through the Editor asset database.");
            yield break;
#else
            FactoryRuntimeHost.DefaultTimeScaleSource = null;
            session = RunShellFixtures.NewSession(out var commands, cash: 500);
            var built = commands.Build(0, "machine.miner");
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
            var views = buffer;
            Assert.That(FactoryRuntimeHost.RunNeedsAdvance(session, ref views), Is.True, "The Miner must be producing.");

            currentActiveSeconds = 100d;
            hostObject = new GameObject("Time Scale Seam Test");
            runtimeHost = hostObject.AddComponent<FactoryRuntimeHost>();
            runtimeHost.ConfigureRun(session, () => currentActiveSeconds);
            yield return null;
#endif
        }

        [UnityTearDown]
        public IEnumerator DestroyHost()
        {
            FactoryRuntimeHost.DefaultTimeScaleSource = null;
            if (hostObject != null)
            {
                Object.Destroy(hostObject);
                yield return null;
            }

            hostObject = null;
            runtimeHost = null;
            session = null;
        }

        private long Credit(double realSeconds)
        {
            var before = session.ActiveTicksSinceAnchor;
            currentActiveSeconds += realSeconds;
            runtimeHost.FlushActiveInterval();
            return session.ActiveTicksSinceAnchor - before;
        }

        [Test]
        public void MaxSimulatedSecondsPerFrameIsTen()
        {
            Assert.That(FactoryRuntimeHost.MaxSimulatedSecondsPerFrame, Is.EqualTo(10d));
        }

        [Test]
        public void NullSourceIsTheDefaultAndAdvancesTheRealDeltaUnclamped()
        {
            Assert.That(FactoryRuntimeHost.DefaultTimeScaleSource, Is.Null);
            Assert.That(runtimeHost.TimeScaleSource, Is.Null);
            Assert.That(Credit(2.5d), Is.EqualTo(25_000_000L));
            Assert.That(Credit(30d), Is.EqualTo(300_000_000L), "With no source there is no S_max clamp (behaviour unchanged).");
        }

        [Test]
        public void ScaleMultipliesTheDeltaAndReportsTheEffectiveMultiplier()
        {
            var fake = new FakeTimeScaleSource { Scale = 10d };
            runtimeHost.TimeScaleSource = fake;

            Assert.That(Credit(0.5d), Is.EqualTo(50_000_000L));
            Assert.That(fake.Reported, Is.EqualTo(10d).Within(1e-9));
        }

        [Test]
        public void ScaledDeltaIsClampedToSMaxAndTheClampedMultiplierIsReported()
        {
            var fake = new FakeTimeScaleSource { Scale = 600d };
            runtimeHost.TimeScaleSource = fake;

            Assert.That(Credit(1d), Is.EqualTo(ClampTicks), "600 x 1 s = 600 s must be clamped to 10 simulated seconds.");
            Assert.That(fake.Reported, Is.EqualTo(10d).Within(1e-9), "10 simulated seconds over 1 real second.");
        }

        [Test]
        public void MaxIsTheClampAndReportsWhatItDelivers()
        {
            var fake = new FakeTimeScaleSource { Scale = double.PositiveInfinity };
            runtimeHost.TimeScaleSource = fake;

            Assert.That(Credit(0.015625d), Is.EqualTo(ClampTicks));
            Assert.That(fake.Reported, Is.EqualTo(10d / 0.015625d).Within(1e-6));
        }

        [Test]
        public void ScaleAtOrBelowTheClampIsNotClamped()
        {
            var fake = new FakeTimeScaleSource { Scale = 600d };
            runtimeHost.TimeScaleSource = fake;

            // 600 x 1/64 s = 9.375 s, under S_max.
            Assert.That(Credit(0.015625d), Is.EqualTo(93_750_000L));
            Assert.That(fake.Reported, Is.EqualTo(600d).Within(1e-9));
        }

        [TestCase(double.NaN)]
        [TestCase(0d)]
        [TestCase(-3d)]
        public void InvalidScaleCountsAsOne(double scale)
        {
            runtimeHost.TimeScaleSource = new FakeTimeScaleSource { Scale = scale };
            Assert.That(Credit(2d), Is.EqualTo(20_000_000L));
        }

        [Test]
        public void ScaleOneAdvancesAHitchFrameUnclampedAndReportsOne()
        {
            var fake = new FakeTimeScaleSource { Scale = 1d };
            runtimeHost.TimeScaleSource = fake;
            Assert.That(Credit(25d), Is.EqualTo(250_000_000L), "A 25 s frame at x1 matches the release behaviour.");
            Assert.That(fake.Reported, Is.EqualTo(1d));
        }

        [Test]
        public void AcceleratedHitchFrameIsClamped()
        {
            runtimeHost.TimeScaleSource = new FakeTimeScaleSource { Scale = 10d };
            Assert.That(Credit(2d), Is.EqualTo(ClampTicks), "10 x 2 s = 20 s is clamped to 10 simulated seconds.");
        }

        [Test]
        public void TheDefaultSourceServesHostsWithoutOneAndTheInstanceSourceWins()
        {
            var shared = new FakeTimeScaleSource { Scale = 10d };
            FactoryRuntimeHost.DefaultTimeScaleSource = shared;
            Assert.That(runtimeHost.TimeScaleSource, Is.SameAs(shared));
            Assert.That(Credit(0.125d), Is.EqualTo(12_500_000L));

            var own = new FakeTimeScaleSource { Scale = 60d };
            runtimeHost.TimeScaleSource = own;
            Assert.That(runtimeHost.TimeScaleSource, Is.SameAs(own));
            Assert.That(Credit(0.125d), Is.EqualTo(75_000_000L));
            Assert.That(shared.ReportCount, Is.EqualTo(1));
        }

        [Test]
        public void ReleaseBuildsUseTheNormalPersistentRoot()
        {
            var path = Path.Combine(Application.persistentDataPath, "probe");
#if IDLEFACTORY_DEV
            Assert.That(PersistenceRoots.IsDevelopmentRoot, Is.True);
            Assert.That(PersistenceRoots.Resolve(path), Is.EqualTo(Path.Combine(path, "IdleFactory-Dev")));
#else
            Assert.That(PersistenceRoots.IsDevelopmentRoot, Is.False);
            Assert.That(PersistenceRoots.Resolve(path), Is.EqualTo(path));
#endif
        }
    }
}
