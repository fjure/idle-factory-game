using System.IO;
using System.Linq;
using NUnit.Framework;
using IdleFactory.Unity.Bootstrap;
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
    ///
    /// T251 (Android Back regression found in T133): with the Player setting <c>runInBackground</c> on (T250), Android Back on
    /// an open confirmation was handed to the system and closed the task, while the T247 build cancelled it. The bisection on
    /// the emulator (<c>reviews/android-emulator-evidence.md</c>, section "T251 Android Back regression") isolated that one
    /// setting: the base build with it on fails, the same build with only it off passes. The setting therefore stays off, and a
    /// desktop player gets the T250 behaviour at runtime through <see cref="LifecyclePlatform"/>.
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

        [Test]
        public void RunInBackgroundPlayerSettingStaysOffBecauseItBreaksAndroidBack()
        {
            Assert.That(PlayerSettings.runInBackground, Is.False,
                "T251: the project-wide Run In Background setting makes the Android GameActivity pass Back on an open confirmation to the " +
                "system (the task closes, logcat 'Transition CLOSE' and 'Quitting process') instead of cancelling it. Desktop gets " +
                "it at runtime through LifecyclePlatform; changing this setting needs a new BackGesture measurement on the emulator.");
        }

        [Test]
        public void DesktopRunsInBackgroundAtRuntimeAndMobileDoesNot()
        {
            using (LifecyclePlatform.Override(false))
            {
                Assert.That(LifecyclePlatform.RunsInBackground, Is.True, "T250: a desktop player keeps the run live while unfocused.");
            }

            using (LifecyclePlatform.Override(true))
            {
                Assert.That(LifecyclePlatform.RunsInBackground, Is.False, "Phones and tablets keep the FR-037 departure behaviour.");
            }
        }

        /// <summary>
        /// T535 (R11, FR-021), re-baselined: Android Back arrives as Escape, and the shell is the only place that acts on it. The hub ("Office") is one more
        /// sheet frame of that same <c>HandleBack</c> path (hub or system view, then the factory), so the activity still adds no back handling and no second
        /// path pops the hub. The behaviour itself is covered in Play Mode by <c>BackOrderTests</c>.
        /// </summary>
        [Test]
        public void TheHubIsPoppedByTheSameShellHandleBackPathAsEveryOtherFrame()
        {
            var shell = File.ReadAllText(ProjectPath("Assets", "IdleFactory", "Unity", "UI", "RunShellDocument.cs"));
            var hub = File.ReadAllText(ProjectPath("Assets", "IdleFactory", "Unity", "UI", "RunShellDocument.Hub.cs"));

            Assert.That(shell, Does.Contain("Android Back arrives as Escape"), "T247: the shell reads the Back key as Escape");
            Assert.That(shell, Does.Contain("IsHubFamilyOpen"), "the hub counts as a closable sheet frame of the one back path");
            Assert.That(hub, Does.Not.Contain("HandleBack"), "the hub has no back handling of its own");
            Assert.That(hub, Does.Not.Contain("KeyCode.Escape"));
        }

        [Test]
        public void DesktopBackgroundExecutionIsAppliedByARuntimeInitializerThatSkipsTheEditor()
        {
            var source = File.ReadAllText(ProjectPath("Assets", "IdleFactory", "Unity", "Bootstrap", "LifecyclePlatform.cs"));

            Assert.That(source, Does.Contain("RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)"),
                "T251: the runtime call replaces the Player setting for desktop players.");
            Assert.That(source, Does.Contain("Application.runInBackground = RunsInBackground;"));
            Assert.That(source, Does.Contain("Application.isEditor"),
                "In the editor Application.runInBackground writes the project's Player setting, which must stay off.");
        }
    }
}
