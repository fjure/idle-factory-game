using System.Collections;
using IdleFactory.Unity.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode
{
    public sealed class FrameRateTargetPlayerTests
    {
        [Category("StandalonePlayer")]
        [UnityTest]
        public IEnumerator PlayerAppliesSixtyFpsTargetWithoutVSync()
        {
            var oldTarget = Application.targetFrameRate;
            var oldVSync = QualitySettings.vSyncCount;
            try
            {
                Application.targetFrameRate = 30;
                CoreGameFoundationBootstrap.ApplyFrameRateTarget();
                yield return null;

                Assert.That(Application.targetFrameRate, Is.EqualTo(60));
#if UNITY_EDITOR
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(oldVSync));
#else
                Assert.That(QualitySettings.vSyncCount, Is.Zero);
#endif
            }
            finally
            {
                Application.targetFrameRate = oldTarget;
                QualitySettings.vSyncCount = oldVSync;
            }
        }
    }
}
