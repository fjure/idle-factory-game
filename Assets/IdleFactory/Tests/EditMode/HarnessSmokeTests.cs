using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    public sealed class HarnessSmokeTests
    {
        [Test]
        public void DiscoversPinnedUnityEditor()
        {
            Assert.That(Application.unityVersion, Is.EqualTo("6000.6.0f1"));
        }
    }
}
