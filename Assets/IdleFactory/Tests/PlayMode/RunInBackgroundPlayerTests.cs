using IdleFactory.Unity.Bootstrap;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T251: the Player setting <c>runInBackground</c> stays off because it breaks Android Back, so a desktop player gets the
    /// T250 behaviour (the run keeps simulating while unfocused) from <c>LifecyclePlatform.ApplyBackgroundExecution</c> before
    /// the first scene loads. This is the only check that observes the property in a built player. In the editor's Play Mode
    /// the runtime initializer is skipped on purpose (there <c>Application.runInBackground</c> writes the project's Player
    /// setting), so the value is not asserted and nothing is written.
    /// </summary>
    public sealed class RunInBackgroundPlayerTests
    {
        [Category("StandalonePlayer")]
        [Test]
        public void BuiltPlayerRunsInBackgroundExactlyOnDesktop()
        {
#if UNITY_EDITOR
            Assert.That(Application.isEditor, Is.True,
                "Editor Play Mode: the runtime initializer skips the editor, the player value is not asserted and no Player setting is written.");
#else
            var expected = !Application.isMobilePlatform;
            var original = Application.runInBackground;
            try
            {
                Assert.That(Application.isEditor, Is.False);
                // The test player harness may already have the property on, so first show that the initializer ran at startup.
                Assert.That(LifecyclePlatform.BackgroundExecutionApplied, Is.True,
                    "The BeforeSceneLoad initializer must have applied the platform class to Application.runInBackground.");
                Assert.That(Application.runInBackground, Is.EqualTo(expected),
                    "A desktop player must run in the background (T250) and a phone or tablet must not (T251, Android Back).");

                // And show that applying it sets the property whatever its previous value was.
                Application.runInBackground = !expected;
                LifecyclePlatform.ApplyBackgroundExecution();
                Assert.That(Application.runInBackground, Is.EqualTo(expected));
            }
            finally
            {
                Application.runInBackground = original;
            }
#endif
        }
    }
}
