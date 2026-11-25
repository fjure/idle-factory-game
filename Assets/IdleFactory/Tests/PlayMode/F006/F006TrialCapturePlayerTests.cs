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
using IdleFactory.Unity.UI;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

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
            public bool upgrade;
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
            public int frames;
            public bool frozen;
        }

        [Serializable]
        private sealed class Spec
        {
            public Job[] jobs;
        }

        [Serializable]
        private sealed class Pt
        {
            public float x;
            public float y;
        }

        [Serializable]
        private sealed class Sidecar
        {
            public int w;
            public int h;
            public int n;
            public int lift;
            public Pt[] centres;
            public string notes;
            public bool cueShown = true;
        }

        [Serializable]
        private sealed class TimedShot
        {
            public string id;
            public int frames;
        }

        [Serializable]
        private sealed class TimedInfo
        {
            public string id;
            public int w;
            public int h;
            public int scale;
            public float pixelsPerPoint;
            public bool hasFloor;
            public bool belowFloor;
            public float fps;
            public int frameCount;
            public Pt[] pivots;
            public TimedShot[] shots;
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
                Log("T545 capture driver: " + SpecVariable + " and " + OutVariable + " are not set; nothing captured (the runner counts an ignored test as skipped, so this passes).");
                yield break;
            }

            yield return CaptureAll(specPath, outDir);
        }

        /// <summary>
        /// The capture loop shared by the Windows test above and the emulator launcher (T562, <c>F006TrialCaptureAndroidLauncher</c>): the same scene, the same forcing and
        /// the same read-back, so the emuP images come from the same method as the Windows classes. On Android the window is the device's own (no SetResolution).
        /// </summary>
        internal static IEnumerator CaptureAll(string specPath, string outDir)
        {
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
                if (Application.platform != RuntimePlatform.Android)
                {
                    Screen.SetResolution(first.w, first.h, FullScreenMode.Windowed);
                }

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
            if (Application.platform != RuntimePlatform.Android) Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
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
            // The offline return summary ("Welcome back") is a surface of its own, not a shell sheet: dismiss it first.
            if (bootstrap.OfflinePanel != null && bootstrap.OfflinePanel.IsOpen)
            {
                bootstrap.OfflinePanel.Dismiss();
                for (var i = 0; i < 30; i++) yield return null;
            }

            for (var back = 0; back < 6 && (shell.IsSheetOpen || shell.IsCardOpen); back++)
            {
                shell.HandleBack(); // the Welcome back sheet of the offline return
                yield return null;
                yield return null;
            }

            // Forced-visual jobs first; the P3 jobs change the real session (removals, an upgrade), so they run last in a fixed order.
            var isP3 = new Func<Job, bool>(j => j.trial == "sc005" && j.shot != null && !string.IsNullOrEmpty(j.shot.p3));
            var ordered = jobs.Where(j => !isP3(j)).Concat(jobs.Where(isP3).OrderBy(j => Array.IndexOf(P3Order, j.shot.id))).ToList();
            var timedPivots = new Dictionary<string, TimedInfo>();
            var reducedBefore = bootstrap.DisplayPreferences.Preference;
            var reducedNow = reducedBefore;
            foreach (var job in ordered)
            {
                if (isP3(job))
                {
                    yield return CaptureP3(bootstrap, job, outDir);
                }
                else if (job.trial == "sc002")
                {
                    // T562 (C-90): the SC-005 timed cue trial. The reduced-motion variant sets the shipped preference for its jobs and puts the previous one back at the end.
                    var wantReduced = job.frozen ? ReducedMotionPreference.On : ReducedMotionPreference.Off;
                    if (reducedNow != wantReduced)
                    {
                        bootstrap.DisplayPreferences.TrySave(wantReduced);
                        reducedNow = wantReduced;
                        for (var i = 0; i < 30; i++) yield return null;
                    }

                    yield return CaptureTimed(bootstrap, job, outDir, timedPivots);
                }
                else
                {
                    var slots = job.trial == "sc005" ? job.shot.slots : job.slots;
                    yield return CaptureForced(bootstrap, job, slots, outDir);
                }
            }

            if (reducedNow != reducedBefore) bootstrap.DisplayPreferences.TrySave(reducedBefore);
            foreach (var pair in timedPivots)
            {
                var dir = Path.Combine(outDir, "sc002-" + pair.Key);
                File.WriteAllText(Path.Combine(dir, "job.json"), JsonUtility.ToJson(pair.Value));
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
                    var upgrade = s.upgrade && !s.boosted && blocker == MachineBlocker.None && active;
                    visuals[i] = new SlotVisual(i, SlotCondition.Occupied, SlotGridModel.SlotOccupiedKey, machineKey, badge, text, s.boosted, s.overdrive, s.selected || i == selected, 0,
                        label + ", " + word + (upgrade ? ", upgrade available" : string.Empty), upgrade, upgrade ? 10L : SlotGridModel.NoUpgradeCost);
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

        /// <summary>T562 (C-93): the world attention markers of the W005-L session are not the forced states, so a tile or a timed screen that tests a different cue hides the marker view.</summary>
        private static void ShowAttentionMarkers(CoreGameFoundationBootstrap bootstrap, bool on)
        {
            if (bootstrap.MarkerView != null) bootstrap.MarkerView.gameObject.SetActive(on);
        }

        private static SpecSlot[] Quiet(int count, int target, string machine, bool upgrade)
        {
            var slots = new SpecSlot[count];
            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = i == target
                    ? new SpecSlot { cond = "occupied", machine = machine, state = "running", level = 2, upgrade = upgrade }
                    : new SpecSlot { cond = "empty", hidden = true };
            }

            return slots;
        }

        // ---- captures ------------------------------------------------------------------------------------------------------------------

        private static IEnumerator CaptureForced(CoreGameFoundationBootstrap bootstrap, Job job, SpecSlot[] slots, string outDir)
        {
            var stage = job.trial == "sc006" ? job.expectStage : 2;
            ShowAttentionMarkers(bootstrap, job.trial != "sc005");
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
                ShowAttentionMarkers(bootstrap, true);
            }
        }

        /// <summary>
        /// T562 (C-90): one screen of the SC-005 timed cue trial (the F004 builder's raw layout <c>sc002-&lt;variant&gt;/&lt;id&gt;_f&lt;k&gt;.png</c> and <c>job.json</c>): the forced 12-slot
        /// screen with the world attention markers hidden, one frame per 100 ms (six frames, running machines animate) or one frozen frame under reduced motion.
        /// </summary>
        private static IEnumerator CaptureTimed(CoreGameFoundationBootstrap bootstrap, Job job, string outDir, Dictionary<string, TimedInfo> infos)
        {
            const float Fps = 10f;
            var dir = Path.Combine(outDir, "sc002-" + job.set);
            Directory.CreateDirectory(dir);
            var visuals = Visuals(bootstrap, job.slots, -1);
            var development = Development(job.slots, 2);
            var frames = Mathf.Max(1, job.frozen ? 1 : job.frames);
            ShowAttentionMarkers(bootstrap, false);
            for (var k = 0; k < frames; k++)
            {
                if (k == 0)
                {
                    yield return Apply(bootstrap, job.slots, 2);
                }
                else
                {
                    yield return new WaitForSeconds(1f / Fps);
                    bootstrap.SlotGrid.Render(visuals);
                    bootstrap.DeckView.SetDevelopment(development);
                    yield return new WaitForEndOfFrame();
                }

                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    File.WriteAllBytes(Path.Combine(dir, job.id + "_f" + k + ".png"), texture.EncodeToPNG());
                    if (!infos.TryGetValue(job.set, out var info))
                    {
                        var side = Geometry(bootstrap, job, texture, job.slots, 2);
                        var n = side.n;
                        var pivots = new Pt[side.centres.Length];
                        for (var i = 0; i < pivots.Length; i++) pivots[i] = new Pt { x = side.centres[i].x, y = side.centres[i].y + 12 * n }; // the timing page lifts the pivot by 12 n to the machine body
                        info = new TimedInfo
                        {
                            id = "sc002-" + job.set, w = texture.width, h = texture.height, scale = n, pixelsPerPoint = Screen.dpi / 160f, hasFloor = true, belowFloor = false, fps = Fps,
                            frameCount = Mathf.Max(1, job.frozen ? 1 : job.frames), pivots = pivots, shots = new TimedShot[0],
                        };
                        infos[job.set] = info;
                    }

                    if (k == 0) info.shots = info.shots.Concat(new[] { new TimedShot { id = job.id, frames = frames } }).ToArray();
                }
                finally
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }

            ShowAttentionMarkers(bootstrap, true);
        }

        // ---- P3 tiles: REAL session states (no forced visuals) ----------------------------------------------------------------------------

        private static readonly string[] P3Order =
        {
            "p3-hub-dot", "p3-hub-no-dot", "p3-attention-store-full", "p3-card-next-to-machine", "p3-upgrade-marker", "p3-ghost-preview", "p3-attention-shortage",
        };

        private static string AttentionState(CoreGameFoundationBootstrap bootstrap)
        {
            var model = bootstrap.ShellPresenter.AttentionModel;
            var marker = model.MarkerCount > 0 ? model.GetMarker(0) : default;
            return "attention{shortage=" + model.ShortageCount + " storeFull=" + model.StoreFullCount + " markers=" + model.MarkerCount + " visible=" + (bootstrap.MarkerView != null ? bootstrap.MarkerView.VisibleMarkerCount : -1)
                + (model.MarkerCount > 0 ? " first=" + marker.Kind + "/" + marker.ResourceId + "@" + marker.FirstAffected.Value : string.Empty) + " hubDot=" + bootstrap.RunShell.IsHubDotShown + "}";
        }

        private static IEnumerator WaitFor(Func<bool> condition, float seconds)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            for (var i = 0; i < 20; i++) yield return null;
        }

        /// <summary>Waits until the condition holds at the end of a frame (the real attention state flickers, so it is checked in the frame that is then captured).</summary>
        private static IEnumerator Until(Func<bool> condition, float seconds)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForEndOfFrame();
                if (condition()) yield break;
            }
        }

        private static Texture2D Region(Texture2D full, Rect pixels, int pad)
        {
            var x = Mathf.Clamp(Mathf.RoundToInt(pixels.xMin) - pad, 0, full.width - 1);
            var y = Mathf.Clamp(Mathf.RoundToInt(pixels.yMin) - pad, 0, full.height - 1);
            var w = Mathf.Min(full.width - x, Mathf.RoundToInt(pixels.width) + 2 * pad);
            var h = Mathf.Min(full.height - y, Mathf.RoundToInt(pixels.height) + 2 * pad);
            var crop = new Texture2D(w, h, TextureFormat.RGB24, false);
            crop.SetPixels(full.GetPixels(x, y, w, h));
            crop.Apply(false);
            return crop;
        }

        private static Rect Around(Vector3 screenPoint, int half) => new Rect(screenPoint.x - half, screenPoint.y - half, 2 * half, 2 * half);

        private static Rect AroundSlot(CoreGameFoundationBootstrap bootstrap, int slot, int half)
        {
            var columns = SlotGridLayout.ColumnsFor(Screen.width, Screen.height);
            var c = SlotGridLayout.SlotRect(slot, columns).center;
            return Around(bootstrap.FactoryCamera.WorldToScreenPoint(new Vector3(c.x, c.y, 0f)), half);
        }

        private static Rect AroundMarker(CoreGameFoundationBootstrap bootstrap, int half)
        {
            var model = bootstrap.ShellPresenter.AttentionModel;
            if (model.MarkerCount == 0) return bootstrap.FactoryCamera.pixelRect;
            var position = model.GetMarker(0).Position;
            return Around(bootstrap.FactoryCamera.WorldToScreenPoint(new Vector3(position.x, position.y, 0f)), half);
        }

        /// <summary>The first marker of a kind (T562, C-93: the tile of one marker kind frames that marker, not the first one of the model).</summary>
        private static int MarkerIndexOf(CoreGameFoundationBootstrap bootstrap, AttentionKind kind)
        {
            var model = bootstrap.ShellPresenter.AttentionModel;
            for (var i = 0; i < model.MarkerCount; i++)
            {
                if (model.GetMarker(i).Kind == kind) return i;
            }

            return -1;
        }

        private static Rect AroundMarkerOf(CoreGameFoundationBootstrap bootstrap, AttentionKind kind, int half)
        {
            var index = MarkerIndexOf(bootstrap, kind);
            if (index < 0) return bootstrap.FactoryCamera.pixelRect;
            var position = bootstrap.ShellPresenter.AttentionModel.GetMarker(index).Position;
            return Around(bootstrap.FactoryCamera.WorldToScreenPoint(new Vector3(position.x, position.y, 0f)), half);
        }

        /// <summary>Hides every drawn marker that is not of the wanted kind for the frame that is captured next (a store-full tile shows no shortage arrow and the reverse).</summary>
        private static IEnumerator OnlyMarkerKind(CoreGameFoundationBootstrap bootstrap, AttentionKind kind)
        {
            for (var pass = 0; pass < 2; pass++)
            {
                yield return null;
                var model = bootstrap.ShellPresenter.AttentionModel;
                for (var i = 0; i < model.MarkerCount; i++)
                {
                    if (model.GetMarker(i).Kind != kind) bootstrap.MarkerView.RendererOf(i).enabled = false;
                }
            }

            yield return new WaitForEndOfFrame();
        }

        private static int ArtN(CoreGameFoundationBootstrap bootstrap) => ArtScale(bootstrap.FactoryCamera, SlotGridLayout.ColumnsFor(Screen.width, Screen.height));

        private static Rect OfficeRect(CoreGameFoundationBootstrap bootstrap)
        {
            var shell = bootstrap.RunShell;
            var k = Screen.width / Mathf.Max(1f, shell.Root.worldBound.width);
            var b = shell.HubEntryButton.worldBound;
            return new Rect(b.xMin * k, Screen.height - b.yMax * k, b.width * k, b.height * k); // Texture2D rows run bottom to top
        }

        private static void ForceHubDot(CoreGameFoundationBootstrap bootstrap, bool on)
        {
            var label = bootstrap.RunShell.Root.Q<UnityEngine.UIElements.Label>("hub-entry-dot");
            if (label == null) return;
            label.text = on ? RunShellDocument.DotGlyph : string.Empty;
            label.style.display = on ? UnityEngine.UIElements.DisplayStyle.Flex : UnityEngine.UIElements.DisplayStyle.None;
        }

        private static IEnumerator CaptureP3(CoreGameFoundationBootstrap bootstrap, Job job, string outDir)
        {
            var shell = bootstrap.RunShell;
            var presenter = bootstrap.ShellPresenter;
            var session = bootstrap.RunSession;
            var id = job.shot.id;
            var notes = string.Empty;
            var region = new Rect(0, 0, Screen.width, Screen.height);
            var world = bootstrap.FactoryCamera.pixelRect;
            var mid = 4;
            var atEndOfFrame = false;
            var canvasSlot = -1;
            var cueShown = true;

            if (id == "p3-hub-dot" || id == "p3-hub-no-dot")
            {
                var on = id == "p3-hub-dot";
                for (var pass = 0; pass < 3; pass++)
                {
                    yield return null;
                    ForceHubDot(bootstrap, on); // the real feed re-sets the dot every 0.25 s, so the label is forced after its Update; the real flag state is recorded below
                }

                yield return new WaitForEndOfFrame();
                region = OfficeRect(bootstrap);
                notes = "hub dot forced " + (on ? "on" : "off") + " (visual only; the real flags decide the rest)";
            }
            else if (id == "p3-attention-store-full")
            {
                yield return Until(() => presenter.AttentionModel.StoreFull && bootstrap.MarkerView.VisibleMarkerCount > 0, 180f);
                yield return OnlyMarkerKind(bootstrap, AttentionKind.StoreFull);
                atEndOfFrame = true;
                region = AroundMarkerOf(bootstrap, AttentionKind.StoreFull, 40 * ArtN(bootstrap));
                notes = "real store-full state of the W005-L run; shortage markers hidden for the frame so the tile shows only the store-full marker (C-93)";
            }
            else if (id == "p3-card-next-to-machine")
            {
                presenter.OpenSlot(mid);
                yield return WaitFor(() => shell.IsCardOpen, 5f);
                for (var i = 0; i < 40; i++) yield return null;
                notes = "real card of the selected machine at slot " + mid + " (selection frame drawn by the presenter); cardOpen=" + shell.IsCardOpen + " selectedSlot=" + shell.SelectedSlot;
            }
            else if (id == "p3-upgrade-marker")
            {
                // T563 draws the FR-027 marker (the reused upgrade icon) for an affordable upgrade without a problem badge. T562 re-captures the tile on a QUIET screen: one running,
                // not boosted machine with the marker forced on through the shipped SlotVisual, every other location an empty plate, the world attention markers hidden (C-93).
                ShowAttentionMarkers(bootstrap, false);
                yield return Apply(bootstrap, Quiet(bootstrap.SlotGrid.Layout.LocationCount, mid, "miner", true), 2);
                atEndOfFrame = true;
                canvasSlot = mid;
                var marker = bootstrap.SlotGrid.RendererOf(mid, SlotGridView.SlotRendererRole.UpgradeMarker);
                cueShown = marker != null && marker.enabled;
                notes = "forced visual (SlotVisual.UpgradeMarker on a running level-2 miner at slot " + mid + ", not boosted, no badge); upgrade marker renderer enabled=" + cueShown
                    + " (the marker is hidden below 24 points per cell, SlotGridView.MarkersVisibleAt); attention markers hidden";
            }
            else if (id == "p3-ghost-preview")
            {
                ShowAttentionMarkers(bootstrap, false);
                shell.HandleBack();
                yield return null;
                var last = session.State.Machines.OrderBy(m => m.SlotIndex).Last();
                var slot = last.SlotIndex;
                var removed = bootstrap.RunCommands.Remove(last.InstanceId, true);
                for (var i = 0; i < 30; i++) yield return null;
                bootstrap.SlotGrid.ShowPreview(slot, session.Catalog.Machines["machine.miner"].PresentationKey);
                yield return null;
                yield return null;
                region = AroundSlot(bootstrap, slot, 45 * ArtN(bootstrap));
                notes = "one ghost miner at location " + slot + " (real removal ok=" + removed.IsSuccess + "); no available-locations highlight; the shipped preview mutes the other machines; attention markers hidden (C-93)";
            }
            else if (id == "p3-attention-shortage")
            {
                foreach (var miner in session.State.Machines.Where(m => m.MachineId == "machine.miner").ToList()) bootstrap.RunCommands.Remove(miner.InstanceId, true);
                ShowAttentionMarkers(bootstrap, true);
                yield return Until(() => presenter.AttentionModel.ShortageCount > 0 && bootstrap.MarkerView.VisibleMarkerCount > 0, 180f);
                yield return OnlyMarkerKind(bootstrap, AttentionKind.Shortage);
                atEndOfFrame = true;
                region = AroundMarkerOf(bootstrap, AttentionKind.Shortage, 40 * ArtN(bootstrap));
                notes = "real shortage after removing every miner; store-full markers hidden for the frame so the tile shows only the shortage marker (C-93)";
            }

            if (!atEndOfFrame) yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                var side = Geometry(bootstrap, job, texture, BaseSlots(bootstrap.SlotGrid.Layout.LocationCount), 2);
                side.cueShown = cueShown;
                side.notes += " p3=" + id + " " + AttentionState(bootstrap) + " cardOpen=" + shell.IsCardOpen + " selected=" + shell.SelectedSlot + " " + notes + " D6=marker-captured-as-shipped";
                var dir = Path.Combine(outDir, job.set);
                Directory.CreateDirectory(dir);
                var crop = canvasSlot >= 0 ? Crop(bootstrap, texture, canvasSlot, out _) : Region(texture, region, id.StartsWith("p3-hub") ? 24 : 0);
                File.WriteAllBytes(Path.Combine(dir, id + ".png"), (id == "p3-card-next-to-machine" ? texture : crop).EncodeToPNG());
                File.WriteAllText(Path.Combine(dir, id + ".json"), JsonUtility.ToJson(side));
                UnityEngine.Object.Destroy(crop);
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
                ShowAttentionMarkers(bootstrap, true);
            }

            if (id == "p3-card-next-to-machine")
            {
                shell.HandleBack();
                yield return null;
            }

            bootstrap.SlotGrid.ClearBuildPreview();
        }

        private static SpecSlot[] BaseSlots(int count)
        {
            var machines = new[] { "miner", "smelter", "press", "assembler" };
            var slots = new SpecSlot[count];
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
            var n = ArtScale(camera, columns);
            var centres = new Pt[bootstrap.SlotGrid.Layout.LocationCount];
            for (var slot = 0; slot < centres.Length; slot++)
            {
                var rect = SlotGridLayout.SlotRect(slot, columns);
                var p = camera.WorldToScreenPoint(new Vector3(rect.center.x, rect.center.y, 0f));
                centres[slot] = new Pt { x = p.x, y = texture.height - p.y }; // image rows run top to bottom
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

        /// <summary>The integer art-pixel scale n: the screen distance of two neighbouring slot centres in one row over the 70 art px cell width.</summary>
        private static int ArtScale(Camera camera, int columns)
        {
            var a = camera.WorldToScreenPoint(new Vector3(SlotGridLayout.SlotRect(0, columns).center.x, 0f, 0f));
            var b = camera.WorldToScreenPoint(new Vector3(SlotGridLayout.SlotRect(1, columns).center.x, 0f, 0f));
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(b.x - a.x) / CellArtPixels));
        }

        private static Texture2D Crop(CoreGameFoundationBootstrap bootstrap, Texture2D full, int slot, out RectInt rect)
        {
            var columns = SlotGridLayout.ColumnsFor(Screen.width, Screen.height);
            var world = SlotGridLayout.SlotRect(slot, columns);
            var p = bootstrap.FactoryCamera.WorldToScreenPoint(new Vector3(world.center.x, world.center.y, 0f));
            var n = ArtScale(bootstrap.FactoryCamera, columns);
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
