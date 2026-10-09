using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T247: the Android back gesture. Measured on the API 36 emulator (<c>reviews/android-emulator-evidence.md</c>, section
    /// "T247 back gesture"): the gesture does cancel an open confirmation exactly like <c>KEYCODE_BACK</c>, but only on the
    /// second edge swipe. While the player runs in immersive mode (Start In Fullscreen) SystemUI reports
    /// <c>navbar_hidden</c> with <c>backGestureDisabled=true</c> and the first swipe only reveals the transient system bars.
    /// Neither candidate fix helps: Predictive Back Support off still ships a manifest the target API 36 ignores, and an
    /// <c>OnBackInvokedCallback</c> in the activity is never invoked because SystemUI never sends the swallowed swipe.
    /// The activity therefore stays pure platform glue (R19) with no back handling, and these checks pin that decision and
    /// the setting it depends on. A deliberate change needs a new emulator measurement of one and two swipes.
    /// </summary>
    public sealed class AndroidBackGestureTests
    {
        private static string ProjectPath(params string[] parts) =>
            Path.Combine(new[] { Directory.GetCurrentDirectory() }.Concat(parts).ToArray());

        [Test]
        public void PredictiveBackSupportStaysAtTheUnityDefault()
        {
            Assert.That(PlayerSettings.Android.predictiveBackSupport, Is.True,
                "T247 measured no difference with it off (the target API 36 ignores the manifest opt-out); the default stays.");
        }

        [Test]
        public void ActivityAddsNoBackHandling()
        {
            var source = File.ReadAllText(ProjectPath("Assets", "Plugins", "Android", "IdleFactoryGameActivity.java"));

            Assert.That(source, Does.Not.Contain("OnBackInvoked"),
                "T247: a back callback in the activity is never invoked while SystemUI disables the gesture, and the Back key already reaches the player as Escape.");
            Assert.That(source, Does.Not.Contain("onBackPressed"));
            Assert.That(source, Does.Not.Contain("KEYCODE_BACK"));
            Assert.That(source, Does.Not.Contain("dispatchKeyEvent"),
                "The activity forwards no key events: back handling is a player decision (R19).");
        }

        [Test]
        public void ImmersiveStartIsTheKnownCauseOfTheFirstSwipeBeingConsumed()
        {
            Assert.That(PlayerSettings.Android.startInFullscreen, Is.True,
                "Start In Fullscreen hides the navigation bar; SystemUI then disables the back edge gesture until a first swipe " +
                "reveals the bars (T247, owner decision pending). Changing it changes the gesture, so measure BackGesture with 1 and 2 swipes again.");
        }
    }
}
