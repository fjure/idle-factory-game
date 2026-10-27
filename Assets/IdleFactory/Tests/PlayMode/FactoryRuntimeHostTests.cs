using System.Collections;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// The active-time accounting of <see cref="FactoryRuntimeHost"/>. Since T119 the host advances only the Feature 003 run
    /// (<see cref="FactoryRuntimeHost.ConfigureRun"/>), so these cases drive a run session with one producing Miner and read
    /// the credited time from <see cref="RunSession.ActiveTicksSinceAnchor"/> (100-ns ticks, like the former inspection).
    /// </summary>
    public sealed class FactoryRuntimeHostTests
    {
        private GameObject hostObject;
        private FactoryRuntimeHost runtimeHost;
        private RunSession session;
        private double currentActiveSeconds;

        [UnitySetUp]
        public IEnumerator CreateProducingRun()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The run fixture loads the packaged content through the Editor asset database.");
            yield break;
#else
            session = RunShellFixtures.NewSession(out var commands, cash: 500);
            var built = commands.Build(0, "machine.miner");
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
            Assert.That(FactoryRuntimeHost.RunNeedsAdvance(session, ref buffer), Is.True, "The Miner must be producing.");

            currentActiveSeconds = 100d;
            hostObject = new GameObject("Factory Runtime Host Test");
            runtimeHost = hostObject.AddComponent<FactoryRuntimeHost>();
            runtimeHost.ConfigureRun(session, () => currentActiveSeconds);
            yield return null;
#endif
        }

        private MachineStateView[] buffer = new MachineStateView[4];

        [UnityTearDown]
        public IEnumerator DestroyHost()
        {
            if (hostObject != null)
            {
                Object.Destroy(hostObject);
                yield return null;
            }

            hostObject = null;
            runtimeHost = null;
            session = null;
        }

        [UnityTest]
        public IEnumerator CumulativeBoundaryConversionRetainsSubtickTimeAcrossFrames()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The run fixture loads the packaged content through the Editor asset database.");
            yield break;
#else
            for (var sample = 1; sample <= 10; sample++)
            {
                currentActiveSeconds = 100d + sample * 0.00000006d;
                yield return null;
                yield return null;
            }

            Assert.That(CreditedTicks(), Is.EqualTo(6L),
                "Ten 0.6-tick frame intervals must become six cumulative 100-ns ticks.");

            currentActiveSeconds = 102.5d;
            yield return null;
            yield return null;
            Assert.That(CreditedTicks(), Is.EqualTo(25_000_000L),
                "The cumulative active-time boundary must not drift after fractional frame intervals.");
#endif
        }

        [UnityTest]
        public IEnumerator PauseAndFocusLossExcludeBackgroundTimeAndResumeFromFreshBoundaries()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The run fixture loads the packaged content through the Editor asset database.");
            yield break;
#else
            currentActiveSeconds = 101d;
            yield return null;
            yield return null;
            Assert.That(CreditedTicks(), Is.EqualTo(10_000_000L));

            hostObject.SendMessage("OnApplicationPause", true);
            currentActiveSeconds = 1001d;
            yield return null;
            yield return null;
            Assert.That(CreditedTicks(), Is.EqualTo(10_000_000L), "Paused time must not advance the run.");

            hostObject.SendMessage("OnApplicationPause", false);
            currentActiveSeconds = 1001.25d;
            yield return null;
            yield return null;
            Assert.That(CreditedTicks(), Is.EqualTo(12_500_000L), "Resume must begin at the return boundary.");

            hostObject.SendMessage("OnApplicationFocus", false);
            currentActiveSeconds = 2001.5d;
            yield return null;
            yield return null;
            Assert.That(CreditedTicks(), Is.EqualTo(12_500_000L), "Focus loss must stop active advancement.");

            hostObject.SendMessage("OnApplicationPause", false);
            currentActiveSeconds = 2002d;
            yield return null;
            yield return null;
            Assert.That(CreditedTicks(), Is.EqualTo(12_500_000L),
                "A pause callback must not resume while focus is still lost.");

            hostObject.SendMessage("OnApplicationFocus", true);
            currentActiveSeconds = 2002.25d;
            yield return null;
            yield return null;
            Assert.That(CreditedTicks(), Is.EqualTo(15_000_000L),
                "Focus return must start a fresh active-time segment without background credit.");
#endif
        }

        [UnityTest]
        public IEnumerator DisabledHostDoesNotCreditTimeBeforeItIsEnabledAgain()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The run fixture loads the packaged content through the Editor asset database.");
            yield break;
#else
            currentActiveSeconds = 100.5d;
            yield return null;
            yield return null;
            Assert.That(CreditedTicks(), Is.EqualTo(5_000_000L));

            runtimeHost.enabled = false;
            currentActiveSeconds = 900d;
            yield return null;
            Assert.That(CreditedTicks(), Is.EqualTo(5_000_000L));

            runtimeHost.enabled = true;
            currentActiveSeconds = 900.25d;
            yield return null;
            yield return null;
            Assert.That(CreditedTicks(), Is.EqualTo(7_500_000L),
                "Re-enabling the host must establish a new cumulative boundary.");
#endif
        }

        private long CreditedTicks() => session.ActiveTicksSinceAnchor;
    }
}
