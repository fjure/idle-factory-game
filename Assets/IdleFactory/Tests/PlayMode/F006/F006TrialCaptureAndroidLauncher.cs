#if IDLEFACTORY_T562_CAPTURE && UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T562 (Feature 006): starts the CAPTURE-ONLY driver of <see cref="F006TrialCapturePlayerTests"/> inside an emulator player, so the emuP class (1080 x 2400, the AVD
    /// <c>IdleFactory_Test_API_36</c>) is captured by the same method as the Windows classes. It compiles only into the capture build that
    /// <c>IdleFactory.Editor.F006EmulatorCaptureBuild</c> makes (scripting define <c>IDLEFACTORY_T562_CAPTURE</c> plus the test assemblies); no shipped or release build contains it.
    ///
    /// The first scene of that build is an empty launcher scene (like the test player's), so the driver can load the authored scene additively exactly as on Windows. The
    /// capture specification holds the answers: it is pushed with adb after the launch and never lives in the repository. The driver waits for it, writes its images below
    /// <c>t562-out</c> in the app's files folder and a <c>done.txt</c> or <c>failed.txt</c> marker, then quits.
    ///   adb push spec.json /sdcard/Android/data/&lt;package&gt;/files/t562-spec.json
    ///   adb pull /sdcard/Android/data/&lt;package&gt;/files/t562-out
    /// </summary>
    internal sealed class F006TrialCaptureAndroidLauncher : MonoBehaviour
    {
        private const string SpecName = "t562-spec.json";
        private const string OutName = "t562-out";
        private const float WaitForSpecSeconds = 1800f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            var host = new GameObject("T562 capture launcher");
            DontDestroyOnLoad(host);
            host.AddComponent<F006TrialCaptureAndroidLauncher>();
        }

        private IEnumerator Start()
        {
            var specPath = Path.Combine(Application.persistentDataPath, SpecName);
            var outDir = Path.Combine(Application.persistentDataPath, OutName);
            var deadline = Time.realtimeSinceStartup + WaitForSpecSeconds;
            while (!File.Exists(specPath) && Time.realtimeSinceStartup < deadline) yield return new WaitForSeconds(1f);
            if (!File.Exists(specPath))
            {
                Debug.Log("T562 capture launcher: no capture specification arrived; nothing captured.");
                yield break;
            }

            yield return new WaitForSeconds(2f); // the push may still be writing
            Directory.CreateDirectory(outDir);
            Exception failure = null;
            var body = F006TrialCapturePlayerTests.CaptureAll(specPath, outDir);
            while (true)
            {
                bool moved;
                try
                {
                    moved = body.MoveNext();
                }
                catch (Exception exception)
                {
                    failure = exception;
                    break;
                }

                if (!moved) break;
                yield return body.Current;
            }

            File.WriteAllText(Path.Combine(outDir, failure == null ? "done.txt" : "failed.txt"), failure == null ? "done" : failure.ToString());
            Debug.Log(failure == null ? "T562 capture launcher: done." : "T562 capture launcher: failed: " + failure);
            yield return new WaitForSeconds(3f);
            Application.Quit();
        }
    }
}
#endif
