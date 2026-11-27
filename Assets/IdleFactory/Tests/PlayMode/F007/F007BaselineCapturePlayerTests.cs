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
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace IdleFactory.Tests.PlayMode.F007
{
    /// <summary>
    /// T603 (Feature 007, research R03, R08, R18): the CAPTURE-ONLY driver that records the shipped F006 look as comparison evidence. It asserts nothing about the game; without the two
    /// environment variables it passes without doing anything, so the tagged player suite stays unchanged.
    ///
    ///   IDLEFACTORY_T603_SPEC  capture plan written by <c>tools/art-pipeline/metrics/capture_plan.py</c> (jobs per screen class, text scale and factory state, from <c>fixtures.json</c>)
    ///   IDLEFACTORY_T603_OUT   scratch output folder (outside the repository): per job <c>stem.png</c>, <c>stem-bodymask.png</c>, <c>stem-layout.json</c>, <c>stem-raw.json</c>
    ///
    /// Method (the T545 real-scene pattern, no host input): the packaged scene starts from the committed W005-L checkpoint, the window is set with <see cref="Screen.SetResolution(int,int,FullScreenMode)"/>,
    /// the device class is applied through the shipped <see cref="RunShellDocument.ApplyDeviceProfile"/> (touch profile with the class's dpi, so the panel scale is the device's own) and
    /// <see cref="RunShellDocument.ApplyTextScale"/>, and per job the per-slot visuals of the fixture are forced the way T545 forces them. Capture builds only: the world camera clears to the reserved key
    /// colour (<c>metrics.common.KEY_COLOUR</c>), animation is frozen (reduced motion on, time scale 0) so a repeat capture is pixel-identical, and a second frame draws only the machine body renderers
    /// on the key colour (the SC-003 and SC-004 input: no plate, deck, shadow, cue or interface). The layout dump holds the UI Toolkit world bounds of every visible painted element, the card rectangle,
    /// the safe area, the framing rectangle and each machine's cell and body bounds in capture pixels (top-left origin).
    /// </summary>
    [Category("StandalonePlayer")]
    public sealed class F007BaselineCapturePlayerTests
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private const string FixtureResource = "W005PlayerFixtures/01-w005-l";
        private const string SpecVariable = "IDLEFACTORY_T603_SPEC";
        private const string OutVariable = "IDLEFACTORY_T603_OUT";
        private static readonly Color32 KeyColour = new Color32(0, 255, 0, 255); // metrics.common.KEY_COLOUR

        [Serializable]
        private sealed class SpecSlot
        {
            public string cond;
            public string machine;
            public string state;
            public int level;
            public bool boosted;
            public bool overdrive;
            public bool upgrade;
        }

        [Serializable]
        private sealed class Job
        {
            public string stem;
            public string look;
            public string geometry;
            public string cls;
            public int w;
            public int h;
            public float dpi;
            public string input;
            public int textScale;
            public string state;
            public int seed;
            public int stage;
            public int cardSlot;
            public string commit;
            public string profile;
            public string route;
            public string notes;
            public SpecSlot[] slots;
        }

        [Serializable]
        private sealed class Spec
        {
            public Job[] jobs;
        }

        [Serializable]
        private sealed class R
        {
            public float x;
            public float y;
            public float w;
            public float h;
            public string name;
        }

        [Serializable]
        private sealed class MachineRect
        {
            public int slot;
            public string machine;
            public R cell;
            public R body;
        }

        [Serializable]
        private sealed class Layout
        {
            public int schema = 1;
            public int width;
            public int height;
            public float pixelsPerPoint;
            public R safeArea;
            public R framingRect;
            public List<R> uiRects = new List<R>();
            public List<R> cardHiddenRects = new List<R>();
            public List<MachineRect> machines = new List<MachineRect>();
        }

        [Serializable]
        private sealed class Raw
        {
            public string look;
            public string geometry;
            public string @class;
            public int textScale;
            public string factoryState;
            public int seed;
            public string commit;
            public string buildKind = "editor-playmode";
            public string device;
            public RawRes resolution;
            public string capturedAt;
            public string profile;
            public string buildId;
            public R framingRect;
            public string route;
            public string layout;
            public string bodyMask;
            public string notes;
            public float pixelsPerPoint;
            public float dpi;
            public string scaleReason;
            public int columns;
            public string cardOpen;
            public int realStage;
        }

        [Serializable]
        private sealed class RawRes
        {
            public int w;
            public int h;
        }

        private static void Log(string line)
        {
            TestContext.Out.WriteLine(line);
            Debug.Log(line);
        }

        [UnityTest]
        public IEnumerator TheF006BaselineIsCapturedPerClassAndFactoryState()
        {
            var specPath = Environment.GetEnvironmentVariable(SpecVariable);
            var outDir = Environment.GetEnvironmentVariable(OutVariable);
            if (string.IsNullOrEmpty(specPath) || string.IsNullOrEmpty(outDir))
            {
                Log("T603 capture driver: " + SpecVariable + " and " + OutVariable + " are not set; nothing captured.");
                yield break;
            }

            var spec = JsonUtility.FromJson<Spec>(File.ReadAllText(specPath));
            Assert.That(spec.jobs, Is.Not.Null.And.Not.Empty, "the capture plan has jobs");
            Directory.CreateDirectory(outDir);

            // The real scene reads the player's persistence folder: move an existing save aside and put it back (the T545 protection).
            var checkpoints = Path.Combine(Application.persistentDataPath, "IdleFactory", "Checkpoints", "v1");
            var runCheckpoints = Path.Combine(Application.persistentDataPath, "IdleFactory", "Checkpoints", "v2");
            var backup1 = MoveAside(checkpoints);
            var backup2 = MoveAside(runCheckpoints);
            Exception failure = null;
            var previousTimeScale = Time.timeScale;

            var catalog = RunPlayerFixtures.IronCatalog();
            var asset = Resources.Load<TextAsset>(FixtureResource);
            Assert.That(asset, Is.Not.Null, "the packaged W005-L fixture");
            var decoded = new SaveV2CheckpointCodec(catalog).Deserialize(asset.bytes);
            Assert.That(decoded.Succeeded, Is.True, decoded.Message);
            var dataRoot = Application.persistentDataPath;
            IDisposable clockOverride = null;
#if UNITY_EDITOR
            // Editor route: the run lives under an isolated root with FIXED clocks (the Feature 003 composition seam), so the simulation never advances and every HUD readout
            // (Store bar, cash, attention count) is the same in every capture and every run.
            dataRoot = RunSceneRun.NewRoot();
            var fixedNow = new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
            clockOverride = CoreGameFoundationBootstrap.OverrideRunCompositionForTests(dataRoot, () => fixedNow, () => 0.0);
#endif
            Assert.That(new JsonRunCheckpointStore(dataRoot, catalog).Promote(decoded.Checkpoint).Succeeded, Is.True);

            // Text raster must not depend on which glyphs earlier jobs or sessions asked for first: a throw-away scene load creates the dynamic font assets, then their atlases are
            // cleared and refilled from one fixed, sorted character set (runtime data only; no asset on disk is written).
            yield return PrewarmFonts();

            foreach (var group in spec.jobs.GroupBy(j => j.w + "x" + j.h))
            {
                var first = group.First();
#if UNITY_EDITOR
                // The Windows window cannot exceed the 1920 x 1080 display, so the editor's Game view renders at the class size (custom rendering resolution).
                PlayModeWindow.SetCustomRenderingResolution((uint)first.w, (uint)first.h, "T603 " + first.w + "x" + first.h);
#else
                Screen.SetResolution(first.w, first.h, FullScreenMode.Windowed);
#endif
                for (var i = 0; i < 90; i++) yield return null;
                Log("T603 window " + first.w + "x" + first.h + " requested; Screen=" + Screen.width + "x" + Screen.height + " dpi=" + Screen.dpi.ToString(CultureInfo.InvariantCulture) + " display=" + Screen.currentResolution);

#if UNITY_EDITOR
                EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
                while (!SceneManager.GetSceneByPath(ScenePath).isLoaded) yield return null;
#else
                var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
                Assert.That(load, Is.Not.Null);
                while (!load.isDone) yield return null;
#endif
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

                Time.timeScale = previousTimeScale;
                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone) yield return null;
                if (failure != null) break;
            }

            try
            {
                clockOverride?.Dispose();
            }
            finally
            {
#if UNITY_EDITOR
                RunSceneRun.DeleteRoot(dataRoot);
#endif
            }

            DeleteIfExists(checkpoints);
            DeleteIfExists(runCheckpoints);
#if !UNITY_EDITOR
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
#endif
            MoveBack(backup1, checkpoints);
            MoveBack(backup2, runCheckpoints);
            Time.timeScale = previousTimeScale;
            if (failure != null) throw failure;
        }

        private const string WarmCharacters = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~ °·×–—‘’“”•…‹›←↑→↓";

        private static IEnumerator PrewarmFonts()
        {
            var first = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!SceneManager.GetSceneByPath(ScenePath).isLoaded) yield return null;
#else
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            while (!load.isDone) yield return null;
#endif
            for (var i = 0; i < 120; i++) yield return null;
            var scene = SceneManager.GetSceneByPath(ScenePath);
            foreach (var lifecycle in UnityEngine.Object.FindObjectsByType<RunLifecycleAdapter>(FindObjectsInactive.Include)) lifecycle.AutosaveEnabled = false;
            var unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone) yield return null;
            for (var i = 0; i < 10; i++) yield return null;

            var assets = Resources.FindObjectsOfTypeAll<UnityEngine.TextCore.Text.FontAsset>().OrderBy(f => f.name, StringComparer.Ordinal).ToArray();
            foreach (var font in assets)
            {
                font.ClearFontAssetData(true);
                var ok = font.TryAddCharacters(WarmCharacters, out var missing);
                Log("T603 font warm-up: " + font.name + " populationMode=" + font.atlasPopulationMode + " ok=" + ok + " missing=" + (missing ?? string.Empty).Length + " glyphs=" + font.glyphTable.Count);
            }

            Log("T603 font warm-up: " + assets.Length + " font assets");
            for (var i = 0; i < 10; i++) yield return null;
            if (first.IsValid()) SceneManager.SetActiveScene(first);
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
            if (bootstrap.OfflinePanel != null && bootstrap.OfflinePanel.IsOpen)
            {
                bootstrap.OfflinePanel.Dismiss();
                for (var i = 0; i < 30; i++) yield return null;
            }

            for (var back = 0; back < 6 && (shell.IsSheetOpen || shell.IsCardOpen); back++)
            {
                shell.HandleBack();
                yield return null;
                yield return null;
            }

            // The simulation stops advancing (the runtime host drives the run from real time), so the attention count and the HUD values are the same in every capture of the group.
            if (bootstrap.RuntimeHost != null) bootstrap.RuntimeHost.enabled = false;
            for (var i = 0; i < 20; i++) yield return null;
            // Frozen presentation: reduced motion on (animation clock stops) and no simulation advance, so HUD numbers and frames do not change between two captures.
            var reducedBefore = bootstrap.DisplayPreferences.Preference;
            bootstrap.DisplayPreferences.TrySave(ReducedMotionPreference.On);
            for (var i = 0; i < 30; i++) yield return null;
            shell.AutoProbeDevice = false;
            var camera = bootstrap.FactoryCamera;
            var clearFlags = camera.clearFlags;
            var background = camera.backgroundColor;
            try
            {
                foreach (var job in jobs)
                {
                    yield return CaptureJob(bootstrap, job, outDir);
                }
            }
            finally
            {
                camera.clearFlags = clearFlags;
                camera.backgroundColor = background;
                bootstrap.DisplayPreferences.TrySave(reducedBefore);
                shell.AutoProbeDevice = true;
            }
        }

        private static IEnumerator CaptureJob(CoreGameFoundationBootstrap bootstrap, Job job, string outDir)
        {
            var shell = bootstrap.RunShell;
            var presenter = bootstrap.ShellPresenter;
            Assert.That(Screen.width, Is.EqualTo(job.w), job.stem + ": the window width is the class width");
            Assert.That(Screen.height, Is.EqualTo(job.h), job.stem + ": the window height is the class height");

            // The class: a touch profile with the class's dpi gives the device's own panel scale (the 7 mm rule), then the text scale.
            var profile = job.input == "pointer" ? ShellDeviceProfile.Pointer(job.w, job.h, job.dpi) : ShellDeviceProfile.Touch(job.w, job.h, job.dpi);
            var decision = shell.ApplyDeviceProfile(profile);
            shell.ApplyTextScale(job.textScale / 100f);
            for (var i = 0; i < 40; i++) yield return null;
            for (var back = 0; back < 6 && (shell.IsSheetOpen || shell.IsCardOpen); back++)
            {
                shell.HandleBack();
                yield return null;
                yield return null;
            }

            if (job.cardSlot >= 0)
            {
                presenter.OpenSlot(job.cardSlot);
                for (var i = 0; i < 300 && !shell.IsCardOpen; i++) yield return null;
                for (var i = 0; i < 60; i++) yield return null;
            }

            var camera = bootstrap.FactoryCamera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = KeyColour;
            if (bootstrap.MarkerView != null) bootstrap.MarkerView.gameObject.SetActive(false); // the W005-L session's markers are not the fixture's states

            var stage = job.stage;
            var visuals = Visuals(bootstrap, job.slots, job.cardSlot);
            var development = Development(job.slots, stage);
            Time.timeScale = 0f;
            bootstrap.AnimationClock.SetSuspended(true); // presentation time stops: world frames repeat exactly
            for (var pass = 0; pass < 4; pass++)
            {
                yield return null;
                bootstrap.SlotGrid.Render(visuals);
                bootstrap.DeckView.SetDevelopment(development);
            }

            yield return new WaitForEndOfFrame();
            var width = Screen.width;
            var height = Screen.height;
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            RenderTexture target = null;
            Texture2D mask = null;
            try
            {
                Assert.That(texture.width, Is.EqualTo(job.w), job.stem + ": captured width");
                Assert.That(texture.height, Is.EqualTo(job.h), job.stem + ": captured height");
                var layout = BuildLayout(bootstrap, job, camera);

                // The body-mask frame: the same camera and framing, every renderer off except the machine bodies, drawn on the key colour (in this same frame, before anything can re-enable them).
                var disabled = new List<Renderer>();
                var keep = new HashSet<Renderer>();
                for (var slot = 0; slot < job.slots.Length; slot++)
                {
                    if (job.slots[slot].cond != "occupied") continue;
                    var renderer = bootstrap.SlotGrid.RendererOf(slot, SlotGridView.SlotRendererRole.Machine);
                    if (renderer != null && renderer.enabled && renderer.sprite != null) keep.Add(renderer);
                }

                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default) { antiAliasing = 1, filterMode = FilterMode.Point };
                target.Create();
                var previousTarget = camera.targetTexture;
                var previousActive = RenderTexture.active;
                foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                {
                    if (renderer.enabled && !keep.Contains(renderer))
                    {
                        renderer.enabled = false;
                        disabled.Add(renderer);
                    }
                }

                try
                {
                    camera.targetTexture = target;
                    camera.Render();
                    RenderTexture.active = target;
                    mask = new Texture2D(width, height, TextureFormat.RGB24, false, false);
                    mask.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                    mask.Apply(false);
                }
                finally
                {
                    camera.targetTexture = previousTarget;
                    RenderTexture.active = previousActive;
                    foreach (var renderer in disabled)
                    {
                        if (renderer != null) renderer.enabled = true;
                    }
                }

                File.WriteAllBytes(Path.Combine(outDir, job.stem + ".png"), texture.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(outDir, job.stem + "-bodymask.png"), mask.EncodeToPNG());
                File.WriteAllText(Path.Combine(outDir, job.stem + "-layout.json"), JsonUtility.ToJson(layout, true));
                var raw = new Raw
                {
                    look = job.look,
                    geometry = job.geometry,
                    @class = job.cls,
                    textScale = job.textScale,
                    factoryState = job.state,
                    seed = job.seed,
                    commit = job.commit,
                    device = (Application.isEditor ? "unity-editor-playmode | " : "") + SystemInfo.operatingSystem + " | " + SystemInfo.graphicsDeviceName + " | " + Screen.width + "x" + Screen.height + " window, class dpi " + job.dpi.ToString(CultureInfo.InvariantCulture),
                    resolution = new RawRes { w = job.w, h = job.h },
                    capturedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                    profile = job.profile + " (" + profile + ")",
                    buildId = (Application.isEditor ? "editor-playmode " : "windows-test-player ") + Application.unityVersion + " " + Application.version + " " + job.commit.Substring(0, 8),
                    framingRect = layout.framingRect,
                    route = job.route,
                    layout = job.stem + "-layout.json",
                    bodyMask = job.stem + "-bodymask.png",
                    notes = job.notes + " | reducedMotion=on timeScale=0 markersHidden=true clear=key(0,255,0) cardSlot=" + job.cardSlot,
                    pixelsPerPoint = layout.pixelsPerPoint,
                    dpi = job.dpi,
                    scaleReason = decision.ToString(),
                    columns = SlotGridLayout.ColumnsFor(Screen.width, Screen.height),
                    cardOpen = shell.IsCardOpen ? "yes" : "no",
                    realStage = presenter.Development.Stage,
                };
                File.WriteAllText(Path.Combine(outDir, job.stem + "-raw.json"), JsonUtility.ToJson(raw, true));
                Log("T603 captured " + job.stem + " " + texture.width + "x" + texture.height + " ppp=" + layout.pixelsPerPoint.ToString(CultureInfo.InvariantCulture) + " cardOpen=" + shell.IsCardOpen + " columns=" + raw.columns
                    + " ui=" + layout.uiRects.Count + " machines=" + layout.machines.Count);
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
                if (mask != null) UnityEngine.Object.Destroy(mask);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.Destroy(target);
                }

                Time.timeScale = 1f;
                bootstrap.AnimationClock.SetSuspended(false);
                if (bootstrap.MarkerView != null) bootstrap.MarkerView.gameObject.SetActive(true);
            }

            if (job.cardSlot >= 0)
            {
                shell.HandleBack();
                for (var i = 0; i < 20; i++) yield return null;
            }
        }

        // ---- layout dump -----------------------------------------------------------------------------------------------------------------

        private static R TopLeft(Rect bottomLeft, int height, string name = null) =>
            new R { x = bottomLeft.xMin, y = height - bottomLeft.yMax, w = bottomLeft.width, h = bottomLeft.height, name = name };

        private static Layout BuildLayout(CoreGameFoundationBootstrap bootstrap, Job job, Camera camera)
        {
            var shell = bootstrap.RunShell;
            var width = Screen.width;
            var height = Screen.height;
            var root = shell.Root;
            var ppp = root.panel != null ? root.panel.scaledPixelsPerPoint : 1f;
            var layout = new Layout { width = width, height = height, pixelsPerPoint = ppp };
            layout.safeArea = TopLeft(Screen.safeArea, height, "safe-area");
            layout.framingRect = shell.TryGetUnobstructedScreenRect(height, out var world)
                ? TopLeft(world, height, "unobstructed-world-rect")
                : TopLeft(new Rect(0, 0, width, height), height, "unobstructed-world-rect-unavailable");

            foreach (var element in root.Query<VisualElement>().ToList())
            {
                if (!IsPainted(element)) continue;
                var bound = element.worldBound; // panel points, top-left origin
                if (bound.width <= 0f || bound.height <= 0f) continue;
                var px = new R { x = bound.x * ppp, y = bound.y * ppp, w = bound.width * ppp, h = bound.height * ppp, name = string.IsNullOrEmpty(element.name) ? element.GetType().Name : element.name };
                if (px.w >= 0.9f * width && px.h >= 0.9f * height) continue; // a full-screen container is not an interface element
                layout.uiRects.Add(px);
            }

            var card = shell.CardRoot;
            if (shell.IsCardOpen && card != null && card.worldBound.width > 0f)
            {
                var b = card.worldBound;
                layout.cardHiddenRects.Add(new R { x = b.x * ppp, y = b.y * ppp, w = b.width * ppp, h = b.height * ppp, name = "machine-card" });
            }

            var columns = SlotGridLayout.ColumnsFor(width, height);
            for (var slot = 0; slot < job.slots.Length; slot++)
            {
                if (job.slots[slot].cond != "occupied") continue;
                var cell = SlotGridLayout.SlotRect(slot, columns);
                var cellPx = WorldToScreenRect(camera, new Vector3(cell.xMin, cell.yMin, 0f), new Vector3(cell.xMax, cell.yMax, 0f), height);
                var renderer = bootstrap.SlotGrid.RendererOf(slot, SlotGridView.SlotRendererRole.Machine);
                if (renderer == null || !renderer.enabled || renderer.sprite == null) continue;
                var b = renderer.bounds;
                var bodyPx = WorldToScreenRect(camera, b.min, b.max, height);
                layout.machines.Add(new MachineRect { slot = slot, machine = job.slots[slot].machine, cell = cellPx, body = bodyPx });
            }

            return layout;
        }

        private static R WorldToScreenRect(Camera camera, Vector3 a, Vector3 b, int height)
        {
            var low = camera.WorldToScreenPoint(new Vector3(a.x, a.y, a.z));
            var high = camera.WorldToScreenPoint(new Vector3(b.x, b.y, b.z));
            var minX = Mathf.Min(low.x, high.x);
            var maxX = Mathf.Max(low.x, high.x);
            var minY = Mathf.Min(low.y, high.y);
            var maxY = Mathf.Max(low.y, high.y);
            return new R { x = minX, y = height - maxY, w = maxX - minX, h = maxY - minY };
        }

        /// <summary>True when the element draws something: a background colour or image, or text; and it and its ancestors are displayed and not fully transparent.</summary>
        private static bool IsPainted(VisualElement element)
        {
            for (var e = element; e != null; e = e.parent)
            {
                var style = e.resolvedStyle;
                if (style.display == DisplayStyle.None || style.visibility == Visibility.Hidden || style.opacity <= 0.01f) return false;
            }

            var own = element.resolvedStyle;
            if (own.backgroundColor.a > 0.05f) return true;
            if (own.backgroundImage.texture != null || own.backgroundImage.sprite != null || own.backgroundImage.vectorImage != null || own.backgroundImage.renderTexture != null) return true;
            return element is TextElement text && !string.IsNullOrEmpty(text.text);
        }

        // ---- forcing (the T545 method) ---------------------------------------------------------------------------------------------------

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
                    visuals[i] = new SlotVisual(i, SlotCondition.Occupied, SlotGridModel.SlotOccupiedKey, machineKey, badge, text, s.boosted, s.overdrive, i == selected, 0,
                        label + ", " + word + (upgrade ? ", upgrade available" : string.Empty), upgrade, upgrade ? 10L : SlotGridModel.NoUpgradeCost);
                }
                else
                {
                    visuals[i] = new SlotVisual(i, SlotCondition.Empty, SlotGridModel.SlotEmptyKey, null, null, null, false, false, i == selected, 0, label + ", empty");
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

        // ---- save folder protection (the T545 helpers) -------------------------------------------------------------------------------------

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
            var backup = path + ".t603-backup";
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
