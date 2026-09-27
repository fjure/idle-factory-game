using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using UnityEditor;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T058: the Android player follows runtime rotation because <c>IdleFactoryGameActivity</c> leaves orientation to
    /// Android (<c>fullUser</c>). Unity's own auto-rotation cannot do it while the Input System is the only input
    /// backend (R09). These checks keep the entry activity, the custom main manifest and the orientation settings that
    /// the activity relies on consistent. Runtime transitions are verified on the emulator
    /// (<c>reviews/windows-android-validation.md</c>).
    /// </summary>
    public sealed class AndroidRuntimeRotationTests
    {
        private const string ActivityName = "com.idlefactory.player.IdleFactoryGameActivity";
        private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

        private static string ProjectPath(params string[] parts) =>
            Path.Combine(new[] { Directory.GetCurrentDirectory() }.Concat(parts).ToArray());

        [Test]
        public void CustomMainManifestLaunchesTheRotationActivity()
        {
            var manifest = XDocument.Load(ProjectPath("Assets", "Plugins", "Android", "AndroidManifest.xml"));
            var activities = manifest.Descendants("activity").ToArray();

            Assert.That(activities.Select(a => (string)a.Attribute(Android + "name")), Is.EqualTo(new[] { ActivityName }),
                "The custom main manifest must declare exactly the rotation activity.");

            var activity = activities[0];
            var metaData = activity.Elements("meta-data")
                .ToDictionary(m => (string)m.Attribute(Android + "name"), m => (string)m.Attribute(Android + "value"));
            Assert.That(metaData.TryGetValue("unityplayer.UnityActivity", out var unityActivity) ? unityActivity : null,
                Is.EqualTo("true"));
            Assert.That(metaData.TryGetValue("android.app.lib_name", out var libName) ? libName : null, Is.EqualTo("game"));
            Assert.That((string)activity.Attribute(Android + "theme"), Is.EqualTo("@style/BaseUnityGameActivityTheme"));

            var filter = activity.Element("intent-filter");
            Assert.That(filter, Is.Not.Null);
            Assert.That(filter.Elements("action").Select(a => (string)a.Attribute(Android + "name")),
                Does.Contain("android.intent.action.MAIN"));
            Assert.That(filter.Elements("category").Select(c => (string)c.Attribute(Android + "name")),
                Does.Contain("android.intent.category.LAUNCHER"));
        }

        [Test]
        public void RotationActivityLeavesEveryOrientationRequestToAndroid()
        {
            var source = File.ReadAllText(ProjectPath("Assets", "Plugins", "Android", "IdleFactoryGameActivity.java"));

            Assert.That(source, Does.Contain("package com.idlefactory.player;"));
            Assert.That(source, Does.Contain("class IdleFactoryGameActivity extends UnityPlayerGameActivity"));
            Assert.That(source, Does.Contain("public void setRequestedOrientation(int requestedOrientation)"));
            Assert.That(source, Does.Contain("super.setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_FULL_USER);"));
        }

        [Test]
        public void ApplicationEntryIsGameActivityOnly()
        {
            Assert.That(PlayerSettings.Android.applicationEntry, Is.EqualTo(AndroidApplicationEntry.GameActivity),
                "The custom main manifest declares only the GameActivity-based entry.");
        }

        [Test]
        public void PlayerAllowsAutoRotationToAllFourOrientations()
        {
            Assert.That(PlayerSettings.defaultInterfaceOrientation, Is.EqualTo(UIOrientation.AutoRotation),
                "Auto Rotation makes Unity declare fullUser, the orientation the activity keeps.");
            Assert.That(PlayerSettings.allowedAutorotateToPortrait, Is.True);
            Assert.That(PlayerSettings.allowedAutorotateToPortraitUpsideDown, Is.True);
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeLeft, Is.True);
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeRight, Is.True);
        }

        [Test]
        public void InputSystemRemainsTheOnlyInputBackend()
        {
            var settings = File.ReadAllText(ProjectPath("ProjectSettings", "ProjectSettings.asset"));

            Assert.That(settings, Does.Contain("activeInputHandler: 1"),
                "R09 selects the Input System only; the rotation fix must not depend on the legacy Input Manager.");
        }
    }
}
