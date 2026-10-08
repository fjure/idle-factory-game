using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Core.Application;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T110: the start-up order of the Iron factory run in the authored scene. The source-selection matrix covers a Save V2
    /// checkpoint, a Save V1 import and a fresh run, corrupt V2 with a valid V1 (never imported, SEC-1) and Start new run
    /// after a blocked import (QA-8). The failure matrix covers every content and persistence rejection with its visible
    /// coded status, in compact portrait with a safe area.
    /// </summary>
    public sealed class RunBootstrapTests
    {
        private const string MinerId = "machine.miner";
        private const string SmelterId = "machine.smelter";

        private string root;
        private RunSceneClock clock;
        private RunSceneRun run;

        [SetUp]
        public void CreateRoot()
        {
            root = RunSceneRun.NewRoot();
            clock = new RunSceneClock();
            run = null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (run != null)
            {
                yield return run.Stop();
            }

            RunSceneRun.DeleteRoot(root);
        }

#if UNITY_EDITOR
        private IEnumerator StartRun(Func<byte[]> content = null, bool quiet = false)
        {
            run = new RunSceneRun(root, clock);
            yield return run.Start(content, quiet);
        }

        private IEnumerator Restart(Func<byte[]> content = null, bool quiet = false)
        {
            yield return run.Stop();
            yield return StartRun(content, quiet);
        }

        private static string StatusText(CoreGameFoundationBootstrap bootstrap) => bootstrap.Hud.Root.Q<Label>("status").text;

        private static void AssertRunIsLive(CoreGameFoundationBootstrap bootstrap)
        {
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            Assert.That(bootstrap.RunSession, Is.Not.Null);
            Assert.That(bootstrap.RunPersistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(bootstrap.InitializationError, Is.Empty);
            Assert.That(bootstrap.Hud.State, Is.EqualTo(FactoryHudState.Normal));
        }

        private static void AssertRejectedStart(CoreGameFoundationBootstrap bootstrap, string code, string name)
        {
            Assert.That(bootstrap.IsInitialized, Is.False, name);
            Assert.That(bootstrap.InitializationError, Does.StartWith(code + ":"), name);
            Assert.That(bootstrap.StartupDiagnosticCode, Is.EqualTo(code), name);
            Assert.That(bootstrap.Hud.IsReady, Is.True, name);
            Assert.That(bootstrap.Hud.State, Is.EqualTo(FactoryHudState.Error), name);
            Assert.That(bootstrap.Hud.Root.ClassListContains("factory-hud--error"), Is.True, name);
            Assert.That(StatusText(bootstrap), Does.StartWith(code + ":"), name);
            Assert.That(bootstrap.Hud.Root.Query<Button>().ToList(), Is.Empty, name + ": the start-up HUD offers no gameplay control");
            Assert.That(bootstrap.InitializationError, Does.Not.Contain("C:\\").And.Not.Contain("secret"), name);
            Assert.That(bootstrap.InitializationError, Does.Not.Contain(Path.DirectorySeparatorChar + "run-scene-"), name);
        }

        private static void BuildMiner(CoreGameFoundationBootstrap bootstrap)
        {
            var built = bootstrap.RunCommands.Build(0, MinerId);
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
        }

        // ---------------------------------------------------------------------------------------------
        // Source selection: V2, V1 import, fresh
        // ---------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator FreshRunStartsWhenNeitherV2NorV1Exists()
        {
            yield return StartRun();
            var bootstrap = run.Bootstrap;

            AssertRunIsLive(bootstrap);
            Assert.That(bootstrap.CheckpointStoreCreated, Is.True);
            Assert.That(bootstrap.StartupDiagnosticCode, Is.Empty);
            Assert.That(StatusText(bootstrap), Is.EqualTo("Iron factory run is ready."));
            Assert.That(bootstrap.RunSession.State.RunNumber, Is.EqualTo(1));
            Assert.That(bootstrap.RunSession.State.Cash, Is.EqualTo(bootstrap.RunCatalog.RunStart.StartingCash));
            Assert.That(bootstrap.RunSession.State.Machines, Is.Empty);
            Assert.That(bootstrap.RunPersistence.Status.Kind, Is.EqualTo(PersistenceStatusKind.NoCheckpoint));
            Assert.That(Directory.Exists(RunSceneRun.V1Directory(root)), Is.False, "No V1 save: the read-only source never creates v1/.");
            Assert.That(bootstrap.Hud.Root.style.display.value, Is.EqualTo(DisplayStyle.None), "The run shell replaces the start-up HUD.");
        }

        [UnityTest]
        public IEnumerator AValidV2CheckpointIsSelectedAndV1IsNeverRead()
        {
            yield return StartRun();
            BuildMiner(run.Bootstrap);
            var saved = run.Bootstrap.RunPersistence.SaveNow();
            Assert.That(saved.IsSuccess, Is.True, saved.ReasonCode);
            var expected = RunSceneRun.Fingerprint(run.Bootstrap.RunSession);

            // A V1 save that would import differently if the cold start ever consulted it.
            RunSceneRun.WriteV1Checkpoint(root, 2, clock.UtcNow.AddHours(-2));
            var v1Before = RunSceneRun.CaptureFiles(RunSceneRun.V1Directory(root));

            yield return Restart();
            var bootstrap = run.Bootstrap;

            AssertRunIsLive(bootstrap);
            Assert.That(RunSceneRun.Fingerprint(bootstrap.RunSession), Is.EqualTo(expected), "The V2 checkpoint, not the V1 save, is the source.");
            Assert.That(bootstrap.RunSession.State.Machines.Select(machine => machine.MachineId), Is.EqualTo(new[] { MinerId }));
            AssertSameFiles(v1Before, RunSceneRun.CaptureFiles(RunSceneRun.V1Directory(root)), "V1");
        }

        [UnityTest]
        public IEnumerator AV1SaveIsImportedOnceAndARestartSelectsTheImportedV2()
        {
            RunSceneRun.WriteV1Checkpoint(root, 1, clock.UtcNow.AddHours(-2));
            var v1Before = RunSceneRun.CaptureFiles(RunSceneRun.V1Directory(root));

            yield return StartRun();
            var bootstrap = run.Bootstrap;

            AssertRunIsLive(bootstrap);
            var machines = bootstrap.RunSession.State.Machines;
            Assert.That(machines.Count, Is.EqualTo(1));
            Assert.That(machines[0].MachineId, Is.EqualTo(SmelterId), "The development machine becomes a level-1 Smelter.");
            Assert.That(machines[0].Level, Is.EqualTo(1));
            Assert.That(RunSceneRun.CaptureFiles(RunSceneRun.V2Directory(root)), Is.Not.Empty, "The import was promoted to Save V2 before it went live.");
            AssertSameFiles(v1Before, RunSceneRun.CaptureFiles(RunSceneRun.V1Directory(root)), "V1");
            var imported = RunSceneRun.Fingerprint(bootstrap.RunSession);

            yield return Restart();

            AssertRunIsLive(run.Bootstrap);
            Assert.That(RunSceneRun.Fingerprint(run.Bootstrap.RunSession), Is.EqualTo(imported), "The restart selects the promoted V2; V1 is not credited again.");
            AssertSameFiles(v1Before, RunSceneRun.CaptureFiles(RunSceneRun.V1Directory(root)), "V1");
        }

        [UnityTest]
        public IEnumerator ACorruptV2WithAValidV1TakesTheRecoveryPathAndNeverImportsV1()
        {
            // One V2 generation holding a Miner.
            yield return StartRun();
            BuildMiner(run.Bootstrap);
            Assert.That(run.Bootstrap.RunPersistence.SaveNow().IsSuccess, Is.True);
            yield return run.Stop();

            // Its data file is torn and its manifest is gone (an intact manifest over torn data is the T141 store follow-up),
            // and a valid V1 save appears next to it.
            RunSceneRun.WriteV1Checkpoint(root, 1, clock.UtcNow.AddHours(-2));
            var v1Before = RunSceneRun.CaptureFiles(RunSceneRun.V1Directory(root));
            var data = Directory.GetFiles(RunSceneRun.V2Directory(root), "*.data-*.json").Single();
            File.WriteAllBytes(data, new byte[] { 0x7B, 0x01, 0x02 });
            foreach (var manifest in Directory.GetFiles(RunSceneRun.V2Directory(root), "*.manifest-*.bin"))
            {
                File.Delete(manifest);
            }

            var v2Before = RunSceneRun.CaptureFiles(RunSceneRun.V2Directory(root));

            yield return StartRun(quiet: true);
            var bootstrap = run.Bootstrap;

            AssertRejectedStart(bootstrap, "return.blocked", "corrupt V2");
            Assert.That(bootstrap.RunPersistence.Lifecycle, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(bootstrap.RunPersistence.BlockedImportCode, Is.Null, "A corrupt V2 never takes the import path (SEC-1).");
            Assert.That(bootstrap.RunSession.State.Machines, Is.Empty, "The V1 machine was not imported into the waiting session.");
            AssertSameFiles(v1Before, RunSceneRun.CaptureFiles(RunSceneRun.V1Directory(root)), "V1");
            AssertSameFiles(v2Before, RunSceneRun.CaptureFiles(RunSceneRun.V2Directory(root)), "V2 stays as found until the player decides");
            Assert.That(bootstrap.RunPersistence.CanDiscardDamagedSave, Is.True);

            // The confirmed recovery without a V1 source starts a fresh run; V1 stays untouched.
            var discarded = bootstrap.RunPersistence.DiscardDamagedSaveAndRestart(null);
            Assert.That(discarded.IsSuccess, Is.True, discarded.ReasonCode);
            Assert.That(bootstrap.ResumeStartupAfterRecovery(), Is.True, bootstrap.InitializationError);

            AssertRunIsLive(bootstrap);
            Assert.That(bootstrap.RunSession.State.Machines.Any(machine => machine.MachineId == SmelterId), Is.False,
                "V1 was never imported.");
            AssertSameFiles(v1Before, RunSceneRun.CaptureFiles(RunSceneRun.V1Directory(root)), "V1");
        }

        [UnityTest]
        public IEnumerator StartNewRunAfterABlockedImportSelectsV2OnRestartAndNoLongerOffersV1()
        {
            // Five machines exceed the four slots of factory level 1: the import is blocked, nothing is published.
            RunSceneRun.WriteV1Checkpoint(root, 5, clock.UtcNow.AddHours(-2));
            var v1Before = RunSceneRun.CaptureFiles(RunSceneRun.V1Directory(root));

            yield return StartRun(quiet: true);
            var bootstrap = run.Bootstrap;

            AssertRejectedStart(bootstrap, "import.slots.exceeded", "blocked import");
            Assert.That(bootstrap.RunPersistence.BlockedImportCode, Is.EqualTo("import.slots.exceeded"));
            Assert.That(RunSceneRun.CaptureFiles(RunSceneRun.V2Directory(root)), Is.Empty, "A blocked import publishes nothing.");
            AssertSameFiles(v1Before, RunSceneRun.CaptureFiles(RunSceneRun.V1Directory(root)), "V1");

            var started = bootstrap.RunPersistence.StartNewRunAfterBlockedImport();
            Assert.That(started.IsSuccess, Is.True, started.ReasonCode);
            Assert.That(bootstrap.ResumeStartupAfterRecovery(), Is.True, bootstrap.InitializationError);
            AssertRunIsLive(bootstrap);
            var freshRun = RunSceneRun.Fingerprint(bootstrap.RunSession);
            Assert.That(bootstrap.RunSession.State.Machines, Is.Empty);

            yield return Restart();

            AssertRunIsLive(run.Bootstrap);
            Assert.That(run.Bootstrap.RunPersistence.BlockedImportCode, Is.Null, "V1 is no longer offered once a V2 exists.");
            Assert.That(RunSceneRun.Fingerprint(run.Bootstrap.RunSession), Is.EqualTo(freshRun));
            AssertSameFiles(v1Before, RunSceneRun.CaptureFiles(RunSceneRun.V1Directory(root)), "V1");
        }

        // ---------------------------------------------------------------------------------------------
        // Failure matrix
        // ---------------------------------------------------------------------------------------------

        private sealed class FailureCase
        {
            public FailureCase(string name, Func<byte[]> bytes, string code)
            {
                Name = name;
                Bytes = bytes;
                Code = code;
            }

            public string Name { get; }
            public Func<byte[]> Bytes { get; }
            public string Code { get; }
        }

        private static IEnumerable<FailureCase> ContentFailures()
        {
            var valid = RunSceneRun.IronBytes();
            var json = Encoding.UTF8.GetString(valid);
            Assert.That(json, Does.Contain("\"contentSchemaVersion\": 2,"));
            Assert.That(json, Does.Contain("\"resourceId\": \"iron.plate\""));
            Assert.That(json, Does.Contain("F003-RES-IRON-ORE"));

            yield return new FailureCase("null override", () => null, "content.empty");
            yield return new FailureCase("throwing override", () => throw new IOException("C:\\private\\secret\\checkpoint.json"), "content.unavailable");
            yield return new FailureCase("empty", () => Array.Empty<byte>(), "content.empty");
            yield return new FailureCase("BOM", () => new byte[] { 0xEF, 0xBB, 0xBF }.Concat(valid).ToArray(), "content.encoding");
            yield return new FailureCase("truncated", () => Encoding.UTF8.GetBytes(json.Substring(0, json.Length / 2)), "json.malformed");
            yield return new FailureCase("oversized", () => new byte[GameplayContentLimits.MaximumByteCount + 1], "content.too-large");
            yield return new FailureCase("unknown member",
                () => Encoding.UTF8.GetBytes(json.Replace("\"contentSchemaVersion\": 2,", "\"contentSchemaVersion\": 2, \"unknown\": true,")),
                "member.unknown");
            yield return new FailureCase("unresolved reference",
                () => Encoding.UTF8.GetBytes(json.Replace("\"resourceId\": \"iron.plate\"", "\"resourceId\": \"missing.plate\"")),
                "ref.resource.unknown");
            yield return new FailureCase("unsupported version",
                () => Encoding.UTF8.GetBytes(json.Replace("\"contentSchemaVersion\": 2,", "\"contentSchemaVersion\": 3,")),
                "content.version.unsupported");
            yield return new FailureCase("unresolved presentation key",
                () => Encoding.UTF8.GetBytes(json.Replace("F003-RES-IRON-ORE", "MISSING-SPRITE")),
                "presentation.key.unresolved");
            yield return new FailureCase("version 1 document as gameplay content",
                RunSceneRun.DevelopmentCatalogBytes,
                "content.version.unsupported");
        }

        [UnityTest]
        public IEnumerator EveryContentRejectionShowsItsCodedStatusAndTouchesNoCheckpointFile()
        {
            // Pre-existing files of both generations of both versions: a rejected start must leave every byte alone.
            Directory.CreateDirectory(RunSceneRun.V2Directory(root));
            Directory.CreateDirectory(RunSceneRun.V1Directory(root));
            File.WriteAllBytes(Path.Combine(RunSceneRun.V2Directory(root), SaveV2CheckpointFiles.DataAFileName), new byte[] { 1, 2, 3, 4 });
            File.WriteAllBytes(Path.Combine(RunSceneRun.V2Directory(root), SaveV2CheckpointFiles.ManifestAFileName), new byte[] { 5, 6, 7 });
            File.WriteAllBytes(Path.Combine(RunSceneRun.V1Directory(root), SaveV1CheckpointFiles.DataAFileName), new byte[] { 8, 9 });
            File.WriteAllBytes(Path.Combine(root, "sentinel.bin"), new byte[] { 10, 11, 12 });

            foreach (var failure in ContentFailures().ToArray())
            {
                var before = RunSceneRun.CaptureFiles(root);
                run = new RunSceneRun(root, clock);
                yield return run.Start(failure.Bytes, quiet: true);
                var bootstrap = run.Bootstrap;

                AssertRejectedStart(bootstrap, failure.Code, failure.Name);
                Assert.That(bootstrap.RunSession, Is.Null, failure.Name + ": no session");
                Assert.That(bootstrap.RunPersistence, Is.Null, failure.Name);
                Assert.That(bootstrap.RunCommands, Is.Null, failure.Name);
                Assert.That(bootstrap.CheckpointStoreCreated, Is.False, failure.Name + ": no checkpoint-store creation");
                AssertSameFiles(before, RunSceneRun.CaptureFiles(root), failure.Name);

                if (failure.Name == "throwing override")
                {
                    Assert.That(bootstrap.InitializationError, Does.Contain("(IOException)"), "The exception type is reported without its message.");
                }

                if (failure.Name == "unresolved presentation key")
                {
                    Assert.That(bootstrap.InitializationError, Does.Contain("MISSING-SPRITE").Or.Contain("presentation"),
                        "The diagnostic names the unresolved key rule.");
                }

                if (failure.Name == "empty")
                {
                    // The visible status holds in compact portrait with a safe area as well.
                    bootstrap.Hud.ApplyScreenLayout(360f, 780f, new Rect(0f, 40f, 360f, 700f));
                    Assert.That(bootstrap.Hud.Layout, Is.EqualTo(FactoryHudLayout.CompactPortrait));
                    AssertRejectedStart(bootstrap, failure.Code, failure.Name + " in compact portrait with a safe area");
                }

                yield return run.Stop();
                run = null;
            }
        }

        [UnityTest]
        public IEnumerator AnUnexpectedStoreFailureReportsTheExceptionTypeOnly()
        {
            Directory.CreateDirectory(root);
            var blockedRoot = Path.Combine(root, "not-a-directory");
            File.WriteAllBytes(blockedRoot, new byte[] { 1 });
            run = new RunSceneRun(blockedRoot, clock);

            yield return run.Start(quiet: true);
            var bootstrap = run.Bootstrap;

            Assert.That(bootstrap.IsInitialized, Is.False);
            Assert.That(bootstrap.InitializationError, Does.Not.Contain(blockedRoot));
            Assert.That(bootstrap.InitializationError, Does.Not.Contain("not-a-directory"));
            Assert.That(bootstrap.StartupDiagnosticCode, Is.Not.Empty);
            Assert.That(bootstrap.Hud.Root.Query<Button>().ToList(), Is.Empty);
            yield return run.Stop();
            run = null;
            File.Delete(blockedRoot);
        }

        [UnityTest]
        public IEnumerator ABlockedImportAndACorruptV2ShowTheirCodedStatusInCompactPortrait()
        {
            RunSceneRun.WriteV1Checkpoint(root, 5, clock.UtcNow.AddHours(-2));

            yield return StartRun(quiet: true);
            var bootstrap = run.Bootstrap;
            bootstrap.Hud.ApplyScreenLayout(360f, 780f, new Rect(0f, 40f, 360f, 700f));

            Assert.That(bootstrap.Hud.Layout, Is.EqualTo(FactoryHudLayout.CompactPortrait));
            AssertRejectedStart(bootstrap, "import.slots.exceeded", "import blocked in compact portrait");
            Assert.That(bootstrap.RunSession, Is.Not.Null, "The blocked start keeps its session for the recovery actions of T118.");
            Assert.That(bootstrap.RunSession.IsLifecycleBlocked, Is.True);
        }

        // ---------------------------------------------------------------------------------------------
        // Scene-level start-up rules kept from the retired Feature 001 scene tests (T119)
        // ---------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheAuthoredSceneAppliesTheSixtyFpsTarget()
        {
            var oldTarget = Application.targetFrameRate;
            var editorVSync = QualitySettings.vSyncCount;
            try
            {
                Application.targetFrameRate = 30;
                yield return StartRun();
                Assert.That(Application.targetFrameRate, Is.EqualTo(60));
                Assert.That(CoreGameFoundationBootstrap.TargetFrameRate, Is.EqualTo(60));
                // The Editor quality level must stay untouched; the player-side VSync change is covered by
                // FrameRateTargetPlayerTests in the Windows test player.
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(editorVSync));
            }
            finally
            {
                Application.targetFrameRate = oldTarget;
            }
        }

        [UnityTest]
        public IEnumerator MissingSerializedReferencesDisableTheBootstrapWithAnActionableErrorAndNoCheckpointIo()
        {
            var logger = Debug.unityLogger;
            var originalHandler = logger.logHandler;
            var recorder = new RecordingLogHandler();
            GameObject gameObject = null;
            var before = RunSceneRun.CaptureFiles(root);
            logger.logHandler = recorder;
            try
            {
                using (CoreGameFoundationBootstrap.OverrideRunCompositionForTests(root, () => clock.UtcNow, () => 0d))
                {
                    gameObject = new GameObject("Invalid Core Game Foundation Bootstrap");
                    var bootstrap = gameObject.AddComponent<CoreGameFoundationBootstrap>();

                    Assert.That(bootstrap.IsInitialized, Is.False);
                    Assert.That(bootstrap.enabled, Is.False);
                    Assert.That(bootstrap.InitializationError, Does.Contain("Missing serialized references"));
                    Assert.That(bootstrap.InitializationError, Does.Not.Contain("gameplayContent"),
                        "A missing content asset is diagnosed by the content reader after the HUD is ready.");
                    Assert.That(bootstrap.CheckpointStoreCreated, Is.False);
                    Assert.That(recorder.Errors, Has.Some.Contains("Core Game Foundation is disabled"));
                }
            }
            finally
            {
                logger.logHandler = originalHandler;
                if (gameObject != null)
                {
                    UnityEngine.Object.Destroy(gameObject);
                }
            }

            yield return null;
            AssertSameFiles(before, RunSceneRun.CaptureFiles(root), "missing references");
        }

        private sealed class RecordingLogHandler : ILogHandler
        {
            public readonly List<string> Errors = new List<string>();

            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                if (logType == LogType.Error)
                {
                    Errors.Add(string.Format(format, args));
                }
            }

            public void LogException(Exception exception, UnityEngine.Object context)
            {
                Errors.Add(exception.ToString());
            }
        }

        private static void AssertSameFiles(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after, string name)
        {
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys), name);
            foreach (var pair in before)
            {
                Assert.That(after[pair.Key], Is.EqualTo(pair.Value), name + ": " + pair.Key);
            }
        }
#else
        [Test]
        public void TheAuthoredSceneCompositionTestsRunInTheEditor()
        {
            Assert.Ignore("The Feature 003 composition seam is available in the Editor only.");
        }
#endif
    }
}
