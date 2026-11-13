using System;
using System.Collections;
using System.IO;
using System.Text;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// F006 T538 (FR-008a, R20): the display-preferences round trip in a real player (Windows test player, IL2CPP-safe token walk,
    /// real file system): v1 read, migration to display-v2.json through the temp file, v1 left in place, v2 read back.
    /// </summary>
    public sealed class DisplayPreferencesPlayerTests
    {
        [Category("StandalonePlayer")]
        [UnityTest]
        public IEnumerator PreferencesRoundTripThroughV1MigrationAndV2()
        {
            var root = Path.Combine(Path.GetTempPath(), "idlefactory-display-player-" + Guid.NewGuid().ToString("N"));
            try
            {
                var first = new DisplayPreferencesStore(root);
                var directory = Path.GetDirectoryName(first.FilePath);
                Directory.CreateDirectory(directory);
                var v1Bytes = Encoding.UTF8.GetBytes("{\"version\":1,\"reducedMotion\":\"on\"}");
                File.WriteAllBytes(first.V1FilePath, v1Bytes);
                yield return null;

                Assert.That(first.Reload(), Is.EqualTo(ReducedMotionPreference.On), "v1 is read while v2 is absent");
                Assert.That(first.ShowZoomButtons, Is.False, "a v1 file never shows the zoom buttons");

                Assert.That(first.TrySaveShowZoomButtons(true), Is.True);
                yield return null;

                Assert.That(File.Exists(first.FilePath), Is.True);
                Assert.That(File.Exists(first.TempFilePath), Is.False, "the temp file is consumed by the replace");
                Assert.That(File.ReadAllBytes(first.V1FilePath), Is.EqualTo(v1Bytes), "v1 stays in place");

                var second = new DisplayPreferencesStore(root);
                Assert.That(second.Reload(), Is.EqualTo(ReducedMotionPreference.On));
                Assert.That(second.ShowZoomButtons, Is.True);

                Assert.That(second.TrySave(ReducedMotionPreference.Off), Is.True);
                var third = new DisplayPreferencesStore(root);
                Assert.That(third.Reload(), Is.EqualTo(ReducedMotionPreference.Off));
                Assert.That(third.ShowZoomButtons, Is.True, "a motion change keeps the zoom choice");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(root))
                    {
                        Directory.Delete(root, true);
                    }
                }
                catch (Exception)
                {
                    // A leftover temp directory is harmless.
                }
            }
        }
    }
}
