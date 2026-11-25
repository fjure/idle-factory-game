#if !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T545 (Feature 006, research R18): the CAPTURE-ONLY driver for the SC-004, SC-005 and SC-006 trial images at the Windows classes (phoneP, phoneL, tabL, tabP).
    /// It produces evidence inputs and asserts nothing about the game. Without the two environment variables it is ignored, so the tagged player suite stays unchanged.
    ///
    ///   IDLEFACTORY_T545_SPEC  path of the capture specification written by `tools/art-pipeline/trials/f006/f006_trials.py plan` (it holds the answers: outside the repository)
    ///   IDLEFACTORY_T545_OUT   output folder: <c>sc004-NN.png</c> + <c>.json</c>, <c>sc006-NN-s&lt;stage&gt;.png</c>, <c>&lt;set&gt;/&lt;tile id&gt;.png</c> (the raw layout the sheet builders read)
    ///
    /// Method (the W006-V real-scene pattern): the packaged scene starts from the committed W005-L checkpoint (12 machines), the window is set to the class with
    /// <see cref="Screen.SetResolution(int,int,FullScreenMode)"/> (no host input, no interactive session: the test runner starts the player), and per job the driver FORCES the
    /// per-slot visuals the spec names (<see cref="SlotGridView.Render"/>, like <c>W004VFixture.Apply</c>) and the development value
    /// (<see cref="FactoryDeckView.SetDevelopment"/>), then reads the frame back with <see cref="ScreenCapture.CaptureScreenshotAsTexture"/>. P3 tiles use the real shell: the build card
    /// ghost preview (<see cref="IBuildPreview"/>), the hub dot (<see cref="HubAttention.SetContracts"/>) and the docked card (<see cref="RunShellPresenter.OpenSlot"/>).
    ///
    /// Known limits, recorded with every capture in the sidecar <c>.json</c> (<c>notes</c>) so that T546 is not misled:
    /// - The session behind the scene is the W005-L run, so the attention markers (shortage, store full) show what ITS rates say, not what the forced slot states say. The sidecar records the
    ///   markers the real <see cref="AttentionMarkerView"/> shows next to the key's expectation.
    /// - D6: the shortage marker is captured as shipped (the generic arrow until T561 changes it; the sidecar records the build's own state, not a claim).
    /// - SC-005 slot tiles are crops of the full frame around the slot pivot, so a neighbouring plate may bleed (F004 isolated each slot); the sidecar records the crop rectangle.
    /// </summary>
    [Category("StandalonePlayer")]
    public sealed class F006TrialCapturePlayerTests
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private const string FixtureResource = "W005PlayerFixtures/01-w005-l";
        private const string SpecVariable = "IDLEFACTORY_T545_SPEC";
        private const string OutVariable = "IDLEFACTORY_T545_OUT";
        private const float CellArtPixels = 70f; // FactoryDeckView.CellSize width in art px

        [Serializable]
        private sealed class SpecSlot
        {
            public string cond;
            public string machine;
            public string state;
            public int level;
            public bool boosted;
            public bool overdrive;
            public bool selected;
            public bool hidden;
        }

        [Serializable]
        private sealed class SpecShot
        {
            public string id;
            public string p3;
            public int cropSlot;
            public SpecSlot[] slots;
        }

        [Serializable]
        private sealed class Job
        {
            public string id;
            public string trial;
            public string cls;
            public string set;
            public int w;
            public int h;
            public float dpi;
            public int factoryUnlocked;
            public int expectStage;
            public string expectShortage;
            public SpecSlot[] slots;
            public SpecShot shot;
        }

        [Serializable]
        private sealed class Spec
        {
            public Job[] jobs;
        }

        [Serializable]
        private sealed class Sidecar
        {
            public int w;
            public int h;
            public int n;
            public int lift;
            public float[][] centres;
            public string notes;
        }

        private static void Log(string line)
        {
            TestContext.Out.WriteLine(line);
            Debug.Log(line);
        }

        [UnityTest]
        public IEnumerator TheTrialImagesAreCapturedAtTheWindowsClasses()
        {
            var specPath = Environment.GetEnvironmentVariable(SpecVariable);
            var outDir = Environment.GetEnvironmentVariable(OutVariable);
            if (string.IsNullOrEmpty(specPath) || string.IsNullOrEmpty(outDir))
            {
                Assert.Ignore("T545 capture driver: " + SpecVariable + " and " + OutVariable + " are not set.");
            }

            var spec = JsonUtility.FromJson<Spec>(File.ReadAllText(specPath));
            Assert.That(spec.jobs, Is.Not.Null.And.Not.Empty, "the capture specification has jobs");
            Directory.CreateDirectory(outDir);

            // The real scene reads the player's persistence folder: move an existing save aside and put it back (same protection as W006VRealScenePlayerTests).
            var checkpoints = Path.Combine(Application.persistentDataPath, "IdleFactory", "Checkpoints", "v1");
            var runCheckpoints = Path.Combine(Application.persistentDataPath, "IdleFactory", "Checkpoints", "v2");
            var backup1 = MoveAside(checkpoints);
            var backup2 = MoveAside(runCheckpoints);
            Exception failure = null;

            var catalog = RunPlayerFixtures.IronCatalog();
            var asset = Resources.Load<TextAsset>(FixtureResource);
            Assert.That(asset, Is.Not.Null, "the packaged W005-L fixture");
            var decoded = new SaveV2CheckpointCodec(catalog).Deserialize(asset.bytes);
            Assert.That(decoded.Succeeded, Is.True, decoded.Message);
            Assert.That(new JsonRunCheckpointStore(Application.persistentDataPath, catalog).Promote(decoded.Checkpoint).Succeeded, Is.True);

            // Jobs grouped by window size so the resolution changes only between groups.
            foreach (var group in spec.jobs.GroupBy(j => j.w + "x" + j.h))
            {
                var first = group.First();
                Screen.SetResolution(first.w, first.h, FullScreenMode.Windowed);
                for (var i = 0; i < 90; i++) yield return null; // the window and the panel settle

                var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
                Assert.That(load, Is.Not.Null);
                while (!load.isDone) yield return null;
                yield return null;
                var scene = SceneManager.GetSceneByPath(ScenePath);
                foreach (var lifecycle in UnityEngine.Object.FindObjectsByType<RunLifecycleAdapter>(FindObjectsInactive.Include)) lifecycle.AutosaveEnabled = false;

                var body = RunGroup(scene, group.ToList(), outDir);
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

                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone) yield return null;
                if (failure != null) break;
            }

            DeleteIfExists(checkpoints);
            DeleteIfExists(runCheckpoints);
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            MoveBack(backup1, checkpoints);
            MoveBack(backup2, runCheckpoints);
            if (failure != null) throw failure;
        }

        private static IEnumerator RunGroup(Scene scene, List<Job> jobs, string outDir)
        {
            CoreGameFoundationBootstrap bootstrap = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                bootstrap = root.GetComponentInChildren<CoreGameFoundationBootstrap>(true);
                if (bootstrap != null) break;
            }

            Assert.That(bootstrap, Is.Not.Null, "the bootstrap is in the packaged scene");
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            for (var frame = 0; frame < 300 && !(bootstrap.RunShell != null && bootstrap.RunShell.IsReady && bootstrap.ShellPresenter != null); frame++) yield return null;
            var shell = bootstrap.RunShell;
            Assert.That(shell.IsReady, Is.True, shell.Error);
            for (var i = 0; i < 120; i++) yield return null;
            for (var back = 0; back < 6 && (shell.IsSheetOpen || shell.IsCardOpen); back++)
            {
                shell.HandleBack(); // the Welcome back sheet of the offline return
                yield return null;
                yield return null;
            }

            foreach (var job in jobs)
            {
                if (job.trial == "sc005" && job.shot != null && !string.IsNullOrEmpty(job.shot.p3))
                {
                    yield return CaptureP3(bootstrap, job, outDir);
                }
                else
                {
                    var slots = job.trial == "sc005" ? job.shot.slots : job.slots;
                    yield return CaptureForced(bootstrap, job, slots, outDir);
                }
            }
        }

        // ---- forcing -------------------------------------------------------------------------------------------------------------------

        private static SlotVisual[] Visuals(CoreGameFoundationBootstrap bootstrap, SpecSlot[] slots, int selected)
        {
            var catalog = bootstrap.RunSession.Catalog;
            var visuals = new SlotVisual[slots.Length];
            for (var i = 0; i < visuals.Length; i++)
            {
                var s = slots[i];
                var label = "Location " + (i + 1).ToString(CultureInfo.InvariantCulture);
                if (s.cond == "locked")
                {
                    var level = Mathf.Clamp(s.level, 2, 5);
                    visuals[i] = new SlotVisual(i, SlotCondition.Locked, SlotGridModel.LockedKey(level), null, null, null, false, false, false, level, label + ", locked");
                }
                else if (s.cond == "occupied")
                {
                    var blocker = s.state == "missing-input" ? MachineBlocker.MissingInput : s.state == "storage-full" ? MachineBlocker.StorageFull : s.state == "inactive" ? MachineBlocker.Inactive : MachineBlocker.None;
                    var phase = s.state == "paused" ? MachinePhase.Paused : s.state == "waiting" ? MachinePhase.Waiting : s.state == "running" ? MachinePhase.Running : MachinePhase.Paused;
                    var active = s.state != "inactive";
                    var badge = SlotGridModel.BadgeFor(active, blocker, phase, out var text, out var word);
                    var machineKey = catalog.Machines["machine." + s.machine].PresentationKey;
                    visuals[i] = new SlotVisual(i, SlotCondition.Occupied, SlotGridModel.SlotOccupiedKey, machineKey, badge, text, s.boosted, s.overdrive, s.selected || i == selected, 0, label + ", " + word);
                }
                else
                {
                    visuals[i] = new SlotVisual(i, SlotCondition.Empty, SlotGridModel.SlotEmptyKey, null, null, null, false, false, s.selected || i == selected, 0, label + ", empty");
                }
            }

            return visuals;
        }

        private static DevelopmentStageValue Development(SpecSlot[] slots, int stage)
        {
            var unlocked = slots.Count(s => s.cond != "locked");
            var machines = slots.Where(s => s.cond == "occupied").ToArray();
            var activity = DevelopmentStage.BinOf(machines.Count(s => s.state == "running"), Math.Max(1, unlocked));
            var level = DevelopmentStage.BinOf(machines.Sum(s => (long)s.level), Math.Max(1, machines.Length * 5L));
            return new DevelopmentStageValue(stage, activity, level);
        }

        /// <summary>Applies the forced state right before the read-back: the presenter refreshes on every session change, so the force is re-applied after its Update, then the frame is drawn.</summary>
        private static IEnumerator Apply(CoreGameFoundationBootstrap bootstrap, SpecSlot[] slots, int stage, Action extra = null)
        {
            var visuals = Visuals(bootstrap, slots, -1);
            var development = Development(slots, stage);
            for (var pass = 0; pass < 3; pass++)
            {
                yield return null;
                bootstrap.SlotGrid.Render(visuals);
                bootstrap.DeckView.SetDevelopment(development);
                extra?.Invoke();
            }

            yield return new WaitForEndOfFrame();
        }

        // ---- captures ------------------------------------------------------------------------------------------------------------------

        private static IEnumerator CaptureForced(CoreGameFoundationBootstrap bootstrap, Job job, SpecSlot[] slots, string outDir)
        {
            var stage = job.trial == "sc006" ? job.expectStage : 2;
            yield return Apply(bootstrap, slots, stage);
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                var side = Geometry(bootstrap, job, texture, slots, stage);
                if (job.trial == "sc005")
                {
                    var crop = Crop(bootstrap, texture, job.shot.cropSlot, out var rect);
                    var dir = Path.Combine(outDir, job.set);
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir, job.shot.id + ".png"), crop.EncodeToPNG());
                    side.notes += " crop=" + rect;
                    UnityEngine.Object.Destroy(crop);
                    File.WriteAllText(Path.Combine(dir, job.shot.id + ".json"), JsonUtility.ToJson(side));
                }
                else
                {
                    var stem = job.id;
                    File.WriteAllBytes(Path.Combine(outDir, stem + ".png"), texture.EncodeToPNG());
                    File.WriteAllText(Path.Combine(outDir, stem + ".json"), JsonUtility.ToJson(side));
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        private static IEnumerator CaptureP3(CoreGameFoundationBootstrap bootstrap, Job job, string outDir)
        {
            var shell = bootstrap.RunShell;
            var presenter = bootstrap.ShellPresenter;
            var id = job.shot.id;
            // All P3 tiles start from the same forced running factory so the cue is the only difference; the base slots come from the first SC-005 tile of the class.
            var slots = BaseSlots();
            Action extra = null;
            if (id == "p3-hub-dot")
            {
                extra = () => shell.Attention.SetContracts(1); // UNVERIFIED: the feed may overwrite; re-applied on every pass
            }
            else if (id == "p3-hub-no-dot")
            {
                extra = () => shell.Attention.SetContracts(0);
            }
            else if (id == "p3-ghost-preview")
            {
                slots[11] = new SpecSlot { cond = "empty" };
                extra = () => bootstrap.SlotGrid.ShowPreview(11, bootstrap.RunSession.Catalog.Machines["machine.miner"].PresentationKey);
            }

            yield return Apply(bootstrap, slots, 2, extra);
            if (id == "p3-card-next-to-machine")
            {
                presenter.OpenSlot(0);
                for (var i = 0; i < 60; i++) yield return null;
                yield return Apply(bootstrap, slots, 2);
            }

            // p3-upgrade-marker, p3-attention-shortage and p3-attention-store-full come from the real session (UNVERIFIED which state shows them in the W005-L run);
            // the sidecar records what the real marker view and the card show, so the reviewer sees what was captured.
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                var side = Geometry(bootstrap, job, texture, slots, 2);
                side.notes += " p3=" + id + " markerView=" + (bootstrap.MarkerView != null) + " hubDot=" + shell.IsHubDotShown + " cardOpen=" + shell.IsCardOpen
                    + " D6=shortage-marker-captured-as-shipped";
                var dir = Path.Combine(outDir, job.set);
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, id + ".png"), texture.EncodeToPNG());
                File.WriteAllText(Path.Combine(dir, id + ".json"), JsonUtility.ToJson(side));
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }

            if (id == "p3-card-next-to-machine")
            {
                shell.HandleBack();
                yield return null;
            }

            bootstrap.SlotGrid.ClearBuildPreview();
        }

        private static SpecSlot[] BaseSlots()
        {
            var machines = new[] { "miner", "smelter", "press", "assembler" };
            var slots = new SpecSlot[SlotGridLayout.SlotCount];
            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = new SpecSlot { cond = "occupied", machine = machines[i % machines.Length], state = "running", level = 2 };
            }

            return slots;
        }

        // ---- geometry ------------------------------------------------------------------------------------------------------------------

        private static Sidecar Geometry(CoreGameFoundationBootstrap bootstrap, Job job, Texture2D texture, SpecSlot[] slots, int stage)
        {
            var camera = bootstrap.FactoryCamera;
            var columns = SlotGridLayout.ColumnsFor(Screen.width, Screen.height);
            var ppc = RunCameraFraming.PixelsPerCell(camera, Screen.dpi > 0 ? Screen.dpi / 160f : 1f); // UNVERIFIED: the shell's own pixels-per-point is not exposed to the test
            var n = Mathf.Max(1, Mathf.RoundToInt(ppc / CellArtPixels));
            var centres = new float[SlotGridLayout.SlotCount][];
            for (var slot = 0; slot < centres.Length; slot++)
            {
                var rect = SlotGridLayout.SlotRect(slot, columns);
                var p = camera.WorldToScreenPoint(new Vector3(rect.center.x, rect.center.y, 0f));
                centres[slot] = new[] { p.x, texture.height - p.y }; // image rows run top to bottom
            }

            var value = Development(slots, stage);
            return new Sidecar
            {
                w = texture.width,
                h = texture.height,
                n = n,
                lift = 0, // the centres are the slot rect centres (machine bodies), so the timing page needs no lift
                centres = centres,
                notes = "job=" + job.id + " class=" + job.cls + " window=" + Screen.width + "x" + Screen.height + " dpi=" + Screen.dpi.ToString(CultureInfo.InvariantCulture) + " columns=" + columns
                    + " stageValue=" + value + " expectStage=" + job.expectStage + " realStage=" + bootstrap.ShellPresenter.Development.Stage + " expectShortage=" + job.expectShortage,
            };
        }

        private static Texture2D Crop(CoreGameFoundationBootstrap bootstrap, Texture2D full, int slot, out RectInt rect)
        {
            var columns = SlotGridLayout.ColumnsFor(Screen.width, Screen.height);
            var world = SlotGridLayout.SlotRect(slot, columns);
            var p = bootstrap.FactoryCamera.WorldToScreenPoint(new Vector3(world.center.x, world.center.y, 0f));
            var ppc = RunCameraFraming.PixelsPerCell(bootstrap.FactoryCamera, Screen.dpi > 0 ? Screen.dpi / 160f : 1f);
            var n = Mathf.Max(1, Mathf.RoundToInt(ppc / CellArtPixels));
            // The 80 x 64 art px canvas around the slot pivot (40 px left of the pivot, 49 px above the bottom edge pivot row), at integer scale n.
            var width = 80 * n;
            var height = 64 * n;
            var x = Mathf.Clamp(Mathf.RoundToInt(p.x) - width / 2, 0, full.width - width);
            var y = Mathf.Clamp(Mathf.RoundToInt(p.y) - height / 2, 0, full.height - height);
            rect = new RectInt(x, y, width, height);
            var crop = new Texture2D(width, height, TextureFormat.RGB24, false);
            crop.SetPixels(full.GetPixels(x, y, width, height));
            crop.Apply(false);
            return crop;
        }

        // ---- save folder protection ------------------------------------------------------------------------------------------------------

        private static void DeleteIfExists(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string MoveAside(string path)
        {
            if (!Directory.Exists(path)) return null;
            var backup = path + ".t545-backup";
            DeleteIfExists(backup);
            Directory.Move(path, backup);
            return backup;
        }

        private static void MoveBack(string backup, string path)
        {
            if (backup == null || !Directory.Exists(backup)) return;
            DeleteIfExists(path);
            Directory.Move(backup, path);
        }
    }
}
#endif
