using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode
{
    public sealed class HarnessSmokeTests
    {
        [Category("StandalonePlayer")]
        [UnityTest]
        public IEnumerator DiscoversRunningPlayerLoop()
        {
            yield return null;
            Assert.That(Application.isPlaying, Is.True);
        }
    }
}
