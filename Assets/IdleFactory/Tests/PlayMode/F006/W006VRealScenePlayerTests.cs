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
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T531 (B4a, FR-006, SC-008, research R16): W006-V in the REAL packaged scene in the Windows test player: the authored art, the real shell, the real docked machine card
    /// of T534 and the real build card. The run starts from the committed W005-L checkpoint (12 running machines, Established stage), so the scene composes the deck, the 12
    /// branches with pips, the vein and the effects itself; the test adds what the checkpoint does not carry: the forced W004-V visuals (SetPass blocks only), the ore-vein hold and
    /// 3 pops. It measures SetPass per world-camera render and per atlas (budget 8), the texture memory and pages (budgets 6 and 7), the renderer census, and 600 frames each with the
    /// card closed, the machine card open and the build card open (budget 5, C-20, C-41). T544 (B4b) re-runs it on the main that holds T560, T561 and T563 and adds the
    /// <c>RunShellPresenter.Refresh</c> allocation per frame (C-94) and the worst-case census and SetPass with the upgrade marker on every slot. Every measurement is a
    /// <c>B4B-*</c> log line; the verdicts are in <c>reviews/performance-b4.md</c>. Nothing here asserts a budget except robust facts, so a miss never fails the suite.
    /// </summary>
    [Category("StandalonePlayer")]
    public sealed class W006VRealScenePlayerTests
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private const string FixtureResource = "W005PlayerFixtures/01-w005-l";
        private const int Frames = 600;
        private const int Renders = 20;
        private const int SetPassFrames = 40;

        private static void Log(string line)
        {
            TestContext.Out.WriteLine(line);
            Debug.Log(line);
        }

        private static string Num(double value, string format = "F3") => value.ToString(format, CultureInfo.InvariantCulture);

        private static double Percentile(List<double> values, double p)
        {
            if (values.Count == 0) return 0;
            var sorted = values.OrderBy(v => v).ToList();
            return sorted[Math.Min(sorted.Count - 1, (int)Math.Floor(p * (sorted.Count - 1) + 0.5))];
        }

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
            var backup = path + ".b4b-backup";
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

        [UnityTest]
        public IEnumerator TheRealSceneMeasuresW006VWithTheCardClosedAndOpen()
        {
            // The real scene reads the player's persistence folder, which is the folder of the Editor too (same company and product). The class is compiled for players only
            // (#if !UNITY_EDITOR), and an existing save is moved aside and put back, so a run never destroys a save.
            var checkpoints = Path.Combine(Application.persistentDataPath, "IdleFactory", "Checkpoints", "v1");
            var runCheckpoints = Path.Combine(Application.persistentDataPath, "IdleFactory", "Checkpoints", "v2");
            var backup1 = MoveAside(checkpoints);
            var backup2 = MoveAside(runCheckpoints);

            // Uncapped from here on: the timed frames show the work of the frame, not the wait for the display. The previous values come back at the end.
            var previousVSync = QualitySettings.vSyncCount;
            var previousFrameRate = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            // The player remembers its last window size, so every run sets the landscape window explicitly first (the portrait window comes later in the body).
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            for (var i = 0; i < 90; i++) yield return null;

            // The framework's own per-frame allocation and frame time with nothing of the game loaded: the control the game frames are read against.
            var control = new Controls();
            Application.targetFrameRate = 60;
            yield return FrameRun("control-no-scene@60fps", 300, null, null, control.Capped);
            LogFrames("B4B-REAL-FRAMES", control.Capped, null);
            Application.targetFrameRate = -1;
            yield return FrameRun("control-no-scene@uncapped", 300, null, null, control.Uncapped);
            LogFrames("B4B-REAL-FRAMES", control.Uncapped, null);

            var catalog = RunPlayerFixtures.IronCatalog();
            var asset = Resources.Load<TextAsset>(FixtureResource);
            Assert.That(asset, Is.Not.Null, "the packaged W005-L fixture");
            var decoded = new SaveV2CheckpointCodec(catalog).Deserialize(asset.bytes);
            Assert.That(decoded.Succeeded, Is.True, decoded.Message);
            var promoted = new JsonRunCheckpointStore(Application.persistentDataPath, catalog).Promote(decoded.Checkpoint);
            Assert.That(promoted.Succeeded, Is.True, promoted.Message);

            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null, "Could not start loading " + ScenePath);
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;
            var scene = SceneManager.GetSceneByPath(ScenePath);
            foreach (var lifecycle in UnityEngine.Object.FindObjectsByType<RunLifecycleAdapter>(FindObjectsInactive.Include)) lifecycle.AutosaveEnabled = false;
            Exception failure = null;
            var body = Body(scene, control);
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
            while (unload != null && !unload.isDone)
            {
                yield return null;
            }

            DeleteIfExists(checkpoints);
            DeleteIfExists(runCheckpoints);
            QualitySettings.vSyncCount = previousVSync;
            Application.targetFrameRate = previousFrameRate;
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            MoveBack(backup1, checkpoints);
            MoveBack(backup2, runCheckpoints);
            if (failure != null) throw failure;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------
        // Per-frame allocation and frame time
        // ------------------------------------------------------------------------------------------------------------------------------------

        private sealed class FrameStats
        {
            public string Label;
            public int Frames;
            public long Allocations;
            public long GcBytes;
            public int FramesWithAllocations;
            public int MaxFrameAllocations;
            public int StructuralFrames;
            public long StructuralAllocations;
            public long SteadyAllocations;
            public int SteadyFramesWithAllocations;
            public int PresenterRebuilds;
            public int FeedRebuilds;
            public readonly List<double> MainThreadMs = new List<double>();
            public readonly List<double> DeltaMs = new List<double>();
        }

        private static string Stat(List<double> values)
        {
            return values.Count == 0
                ? "n/a"
                : Num(values.Average()) + "/" + Num(Percentile(values, 0.5)) + "/" + Num(Percentile(values, 0.95)) + "/" + Num(Percentile(values, 0.99)) + "/" + Num(values.Max());
        }

        private static void LogFrames(string tag, FrameStats s, FrameStats control)
        {
            var net = control == null ? 0 : control.Allocations / (double)Math.Max(1, control.Frames);
            Log(tag + " label=" + s.Label + " frames=" + s.Frames + " allocations=" + s.Allocations + " allocationsPerFrame=" + Num(s.Allocations / (double)Math.Max(1, s.Frames), "F1") +
                " netOfControlPerFrame=" + Num(s.Allocations / (double)Math.Max(1, s.Frames) - net, "F1") + " gcBytesPerFrame(approx)=" + Num(s.GcBytes / (double)Math.Max(1, s.Frames), "F0") +
                " framesWithAllocations=" + s.FramesWithAllocations + " maxPerFrame=" + s.MaxFrameAllocations +
                " structuralFrames=" + s.StructuralFrames + " structuralAllocations=" + s.StructuralAllocations +
                " allocationsPerStructuralFrame=" + Num(s.StructuralFrames == 0 ? 0 : s.StructuralAllocations / (double)s.StructuralFrames, "F1") +
                " steadyFrames=" + (s.Frames - s.StructuralFrames) + " steadyAllocations=" + s.SteadyAllocations + " steadyFramesWithAllocations=" + s.SteadyFramesWithAllocations +
                " steadyAllocationsPerFrame=" + Num(s.Frames == s.StructuralFrames ? 0 : s.SteadyAllocations / (double)(s.Frames - s.StructuralFrames), "F1") +
                " presenterRebuilds=" + s.PresenterRebuilds + " feedRebuilds=" + s.FeedRebuilds +
                " mainThreadMs mean/p50/p95/p99/max=" + Stat(s.MainThreadMs) + " deltaMs mean/p50/p95/p99/max=" + Stat(s.DeltaMs) +
                " controlAllocationsPerFrame=" + (control == null ? "n/a" : Num(net, "F3")));
        }

        // One run of frames: the GC.Alloc samples of the main thread across each whole frame (the player loop, Update, UI Toolkit, rendering submission and the test runner), the
        // managed bytes of the frame, the main-thread CPU time per frame, and which frames were structural rebuilds (the shell presenter's or the world feed's snapshot).
        private static IEnumerator FrameRun(string label, int frames, Action perFrame, CoreGameFoundationBootstrap bootstrap, FrameStats stats)
        {
            stats.Label = label;
            var recorder = Recorder.Get("GC.Alloc");
            var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            var bytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            try
            {
                var presenter = bootstrap != null ? bootstrap.ShellPresenter : null;
                var feed = bootstrap != null ? bootstrap.WorldEffects : null;
                var lastSnapshot = presenter != null ? presenter.Snapshot : null;
                var lastFeed = feed != null ? feed.Rebuilds : 0;
                for (var i = 0; i < frames; i++)
                {
                    recorder.enabled = false;
                    recorder.FilterToCurrentThread();
                    recorder.enabled = true;
                    perFrame?.Invoke();
                    yield return null;
                    recorder.enabled = false;
                    recorder.CollectFromAllThreads();
                    var count = recorder.sampleBlockCount;
                    var structural = false;
                    if (presenter != null && !ReferenceEquals(presenter.Snapshot, lastSnapshot))
                    {
                        lastSnapshot = presenter.Snapshot;
                        stats.PresenterRebuilds++;
                        structural = true;
                    }

                    if (feed != null && feed.Rebuilds != lastFeed)
                    {
                        stats.FeedRebuilds += feed.Rebuilds - lastFeed;
                        lastFeed = feed.Rebuilds;
                        structural = true;
                    }

                    stats.Frames++;
                    stats.Allocations += count;
                    stats.GcBytes += bytes.Valid ? bytes.LastValue : 0;
                    stats.FramesWithAllocations += count > 0 ? 1 : 0;
                    stats.MaxFrameAllocations = Math.Max(stats.MaxFrameAllocations, count);
                    if (structural)
                    {
                        stats.StructuralFrames++;
                        stats.StructuralAllocations += count;
                    }
                    else
                    {
                        stats.SteadyAllocations += count;
                        stats.SteadyFramesWithAllocations += count > 0 ? 1 : 0;
                    }

                    if (main.Valid && main.LastValue > 0) stats.MainThreadMs.Add(main.LastValue / 1_000_000.0);
                    stats.DeltaMs.Add(Time.unscaledDeltaTime * 1000.0);
                }
            }
            finally
            {
                main.Dispose();
                bytes.Dispose();
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------
        // The scene body
        // ------------------------------------------------------------------------------------------------------------------------------------

        private static IEnumerator Settle(int frames)
        {
            for (var i = 0; i < frames; i++) yield return null;
        }

        private sealed class Controls
        {
            public readonly FrameStats Capped = new FrameStats();
            public readonly FrameStats Uncapped = new FrameStats();
        }

        // Runs the same 600 frames twice: at the production cadence (60 FPS target, no vsync: allocations and rebuilds are a function of time too) and uncapped (the frame cost).
        private static IEnumerator FrameRuns(string label, Action perFrame, CoreGameFoundationBootstrap bootstrap, Controls control)
        {
            Application.targetFrameRate = 60;
            var capped = new FrameStats();
            yield return FrameRun(label + "@60fps", Frames, perFrame, bootstrap, capped);
            LogFrames("B4B-REAL-FRAMES", capped, control.Capped);
            Assert.That(capped.Frames, Is.EqualTo(Frames));
            Application.targetFrameRate = -1;
            var uncapped = new FrameStats();
            yield return FrameRun(label + "@uncapped", Frames, perFrame, bootstrap, uncapped);
            LogFrames("B4B-REAL-FRAMES", uncapped, control.Uncapped);
            Assert.That(uncapped.Frames, Is.EqualTo(Frames));
        }

        // C-94: the allocation of RunShellPresenter.Refresh alone (it also runs inside the bootstrap's own Update; this is an extra call per frame, measured by itself), over 600
        // frames at the production cadence. A frame in which the presenter rebuilt its snapshot is structural and counted apart; every other frame is the steady state.
        private static IEnumerator RefreshRun(string label, Action perFrame, CoreGameFoundationBootstrap bootstrap)
        {
            var presenter = bootstrap.ShellPresenter;
            var recorder = Recorder.Get("GC.Alloc");
            var snapshot = presenter.Snapshot;
            long steady = 0;
            long structural = 0;
            var steadyFrames = 0;
            var structuralFrames = 0;
            var steadyWithAllocations = 0;
            var maxSteady = 0;
            var refreshMs = new List<double>();
            var steadyMs = new List<double>();
            Application.targetFrameRate = 60;
            for (var i = 0; i < Frames; i++)
            {
                perFrame?.Invoke();
                yield return null;
                recorder.enabled = false;
                recorder.FilterToCurrentThread();
                recorder.enabled = true;
                var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                presenter.Refresh();
                var ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                recorder.enabled = false;
                recorder.CollectFromAllThreads();
                var count = recorder.sampleBlockCount;
                refreshMs.Add(ms);
                if (!ReferenceEquals(presenter.Snapshot, snapshot))
                {
                    snapshot = presenter.Snapshot;
                    structural += count;
                    structuralFrames++;
                    continue;
                }

                steadyMs.Add(ms);
                steady += count;
                steadyFrames++;
                steadyWithAllocations += count > 0 ? 1 : 0;
                maxSteady = Math.Max(maxSteady, count);
            }

            Application.targetFrameRate = -1;
            var markers = 0;
            for (var i = 0; i < presenter.Visuals.Count; i++) markers += presenter.Visuals[i].UpgradeMarker ? 1 : 0;
            Log("B4B-REFRESH-REAL label=" + label + " frames=" + Frames + " steadyFrames=" + steadyFrames + " steadyAllocations=" + steady + " steadyFramesWithAllocations=" + steadyWithAllocations +
                " maxSteadyPerFrame=" + maxSteady + " structuralFrames=" + structuralFrames + " structuralAllocations=" + structural + " upgradeMarkersOn=" + markers + " cardOpen=" + bootstrap.RunShell.IsCardOpen +
                " refreshMs(all) mean/p50/p95/p99/max=" + Stat(refreshMs) + " refreshMs(steady) mean/p50/p95/p99/max=" + Stat(steadyMs));
        }

        // C-94: 600 consecutive Refresh calls with nothing changing between them (no frame passes, so no machine state, cash or structure moves): the per-frame work of the affordability
        // mask comparison and the other per-frame reads, alone. With the card closed this is asserted to allocate nothing; with a card open the card's own refresh is part of the call.
        private static IEnumerator RefreshStill(string label, CoreGameFoundationBootstrap bootstrap, bool assertZero)
        {
            yield return null;
            var presenter = bootstrap.ShellPresenter;
            for (var warm = 0; warm < 10; warm++) presenter.Refresh();
            var snapshot = presenter.Snapshot;
            var recorder = Recorder.Get("GC.Alloc");
            recorder.enabled = false;
            recorder.FilterToCurrentThread();
            recorder.enabled = true;
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            for (var i = 0; i < Frames; i++) presenter.Refresh();
            var totalMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            recorder.enabled = false;
            recorder.CollectFromAllThreads();
            var count = recorder.sampleBlockCount;
            var markers = 0;
            for (var i = 0; i < presenter.Visuals.Count; i++) markers += presenter.Visuals[i].UpgradeMarker ? 1 : 0;
            Log("B4B-REFRESH-STILL label=" + label + " calls=" + Frames + " allocations=" + count + " rebuilt=" + !ReferenceEquals(presenter.Snapshot, snapshot) + " upgradeMarkersOn=" + markers +
                " cardOpen=" + bootstrap.RunShell.IsCardOpen + " meanMsPerCall=" + Num(totalMs / Frames, "F4"));
            if (assertZero)
            {
                Assert.That(count, Is.Zero, "C-94: 600 Refresh calls over an unchanged run allocate nothing (the per-frame affordability mask included)");
            }
        }

        private static SlotVisual[] WithMarkers(IReadOnlyList<SlotVisual> forced, bool boosted, bool overdrive, bool marker)
        {
            var result = new SlotVisual[forced.Count];
            for (var i = 0; i < result.Length; i++)
            {
                var v = forced[i];
                result[i] = new SlotVisual(v.Index, v.Condition, v.BaseKey, v.MachineKey, v.BadgeKey, v.BadgeText, boosted, overdrive, v.Selected, v.UnlockLevel, v.Description, marker,
                    marker ? 10 : SlotGridModel.NoUpgradeCost);
            }

            return result;
        }

        // C-94: the worst-case census in the real scene with the upgrade marker on all 12 slots (the forced W004-V states, the real catalog and the real art).
        private static IEnumerator WorstCaseCensus(string screenLabel, CoreGameFoundationBootstrap bootstrap, IReadOnlyList<SlotVisual> forced)
        {
            var grid = bootstrap.SlotGrid;
            var results = new Dictionary<string, int>();
            foreach (var variant in new[] { "boosted+overdrive", "boosted+overdrive+upgrade-marker", "overdrive+upgrade-marker", "upgrade-marker only" })
            {
                var visuals = WithMarkers(forced, variant.StartsWith("boosted"), variant.Contains("overdrive"), variant.Contains("upgrade-marker"));
                for (var settle = 0; settle < 3; settle++)
                {
                    grid.Render(visuals);
                    yield return null;
                }

                // The presenter re-renders the natural visuals whenever it rebuilds (about 11 times a second): render the forced ones again and count in the same frame, with no frame between.
                grid.Render(visuals);
                var census = RendererCensus.OfLoadedScenes();
                var on = 0;
                for (var i = 0; i < visuals.Length; i++) on += grid.RendererOf(i, SlotGridView.SlotRendererRole.UpgradeMarker).enabled ? 1 : 0;
                results[variant] = census.Total;
                Log("B4B-CENSUS-REAL-WORST screen=" + screenLabel + " variant=\"" + variant + "\" " + census + " upgradeMarkerRenderersOn=" + on + " vsSigned95=" + (census.Total - 95) + " vsDraft105=" +
                    (census.Total - 105) + " vsTier173=" + (census.Total - 173));
            }

            Assert.That(results["boosted+overdrive+upgrade-marker"], Is.EqualTo(results["boosted+overdrive"]), "C-94: the upgrade marker on all 12 slots does not raise the worst case");
        }

        private static IEnumerator Body(Scene scene, Controls control)
        {
            CoreGameFoundationBootstrap bootstrap = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                bootstrap = root.GetComponentInChildren<CoreGameFoundationBootstrap>(true);
                if (bootstrap != null) break;
            }

            Assert.That(bootstrap, Is.Not.Null, "the bootstrap is in the packaged scene");
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            for (var frame = 0; frame < 300 && !(bootstrap.RunShell != null && bootstrap.RunShell.IsReady && bootstrap.ShellPresenter != null && bootstrap.WorldEffects != null); frame++)
            {
                yield return null;
            }

            var shell = bootstrap.RunShell;
            Assert.That(shell.IsReady, Is.True, shell.Error);
            Assert.That(bootstrap.PanelDiagnostics, Is.Empty, "every panel composed from the packaged assets");
            // Uncapped: the timed frames show the work of the frame, not the wait for the display (the bootstrap sets 60 FPS at start-up).
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            yield return Settle(60);
            for (var back = 0; back < 6 && (shell.IsSheetOpen || shell.IsCardOpen); back++)
            {
                shell.HandleBack(); // dismisses the Welcome back sheet of the offline return
                yield return null;
                yield return null;
            }

            yield return Settle(120);
            var session = bootstrap.RunSession;
            var presenter = bootstrap.ShellPresenter;
            var occupied = session.State.Machines.Count;
            Log("B4B-REAL-STATE machines=" + occupied + " stage=" + presenter.Development.Stage + " screen=" + Screen.width + "x" + Screen.height + " dpi=" + Screen.dpi + " gfx=" + SystemInfo.graphicsDeviceType +
                " cameraPixelRect=" + bootstrap.FactoryCamera.pixelRect + " orthoSize=" + Num(bootstrap.FactoryCamera.orthographicSize) + " columns=" + bootstrap.SlotGrid.Layout.LayoutId + " sheetOpen=" + shell.IsSheetOpen + " cardOpen=" + shell.IsCardOpen +
                " platform=" + Application.platform + " dev=" + Debug.isDebugBuild + " unity=" + Application.unityVersion + " cpu=" + SystemInfo.processorType + " gpu=" + SystemInfo.graphicsDeviceName);
            Assert.That(occupied, Is.EqualTo(12), "the W005-L checkpoint was accepted: 12 machines");
            Assert.That(presenter.Development.Stage, Is.EqualTo(W006VFixture.Stage), "the Established stage");

            var grid = bootstrap.SlotGrid;
            var pool = bootstrap.EffectPool;
            var vein = shell.OreVein;
            Assert.That(vein, Is.Not.Null, "the ore vein control");

            void HoldVein()
            {
                if (vein.IsHolding || vein.IsHidden || !vein.IsBound) return;
                using (var down = KeyDownEvent.GetPooled('\0', KeyCode.Space, EventModifiers.None))
                {
                    down.target = vein.Root;
                    vein.Root.SendEvent(down);
                }
            }

            void ThreePops()
            {
                var count = grid.Layout.LocationCount;
                pool.TrySpawn(EffectKind.CompletionPop, 0);
                pool.TrySpawn(EffectKind.CompletionPop, count / 3);
                pool.TrySpawn(EffectKind.CompletionPop, 2 * count / 3);
            }

            // ---- landscape 1920x1080: the card closed (vein hold, chip loop), then the machine card open ----
            yield return Phase((Screen.width > Screen.height ? "landscape-" : "portrait-") + Screen.width + "x" + Screen.height, bootstrap, control, true, HoldVein, ThreePops);

            // ---- a portrait window (the shipped phone orientation): the same two states ----
            Screen.SetResolution(540, 960, FullScreenMode.Windowed);
            yield return Settle(90);
            Log("B4B-REAL-RESIZE screen=" + Screen.width + "x" + Screen.height + " cameraPixelRect=" + bootstrap.FactoryCamera.pixelRect + " layout=" + bootstrap.SlotGrid.Layout.LayoutId);
            yield return Settle(60);
            HoldVein();
            yield return Phase((Screen.width > Screen.height ? "landscape-" : "portrait-") + Screen.width + "x" + Screen.height, bootstrap, control, true, HoldVein, ThreePops);

            // ---- build card open (C-41): the free location comes from removing the last machine, a structural event outside every window ----
            var last = session.State.Machines.OrderBy(m => m.SlotIndex).Last();
            var freeSlot = last.SlotIndex;
            Assert.That(bootstrap.RunCommands.Remove(last.InstanceId, true).IsSuccess, Is.True, "a location is free");
            yield return Settle(60);
            Assert.That(presenter.OpenSlot(freeSlot), Is.True, "a tap on the free location opens the build card");
            Assert.That(shell.IsBuildCardOpen, Is.True, "the build card, docked");
            yield return Settle(60);
            Log("B4B-REAL-BUILDCARD open=" + shell.IsBuildCardOpen + " slot=" + freeSlot + " machines=" + session.State.Machines.Count + " screen=" + Screen.width + "x" + Screen.height);
            yield return SetPassBlock("build-card-open-11-machines", bootstrap, null, null, ThreePops);
            yield return FrameRuns("build-card-open", ThreePops, bootstrap, control);
            yield return RefreshRun("build-card-open", ThreePops, bootstrap);
            shell.HandleBack();
            yield return Settle(30);
        }

        // The card closed, then (when asked) the machine card of location 0 open: SetPass block, census, textures, then 600 frames.
        private static IEnumerator Phase(string screenLabel, CoreGameFoundationBootstrap bootstrap, Controls control, bool forcedVisuals, Action holdVein, Action pops)
        {
            var shell = bootstrap.RunShell;
            var presenter = bootstrap.ShellPresenter;
            var vein = shell.OreVein;
            holdVein();
            yield return Settle(30);
            Log("B4B-REAL-VEIN screen=" + screenLabel + " holding=" + vein.IsHolding + " depositMode=" + bootstrap.DepositView.Mode + " chipFrame=" + bootstrap.DepositView.ChipFrame);
            var forced = forcedVisuals ? W004VFixture.Apply(bootstrap.SlotGrid.Visuals) : null;
            yield return SetPassBlock(screenLabel + "/card-closed", bootstrap, forced, holdVein, pops);
            yield return WorstCaseCensus(screenLabel, bootstrap, forced);
            yield return SetPassBlock(screenLabel + "/card-closed+upgrade-marker", bootstrap, WithMarkers(forced, false, true, true), holdVein, pops);
            presenter.Invalidate();
            yield return Settle(30);
            yield return FrameRuns(screenLabel + "/card-closed", () =>
            {
                holdVein();
                pops();
            }, bootstrap, control);
            yield return RefreshRun(screenLabel + "/card-closed", () =>
            {
                holdVein();
                pops();
            }, bootstrap);
            yield return RefreshStill(screenLabel + "/card-closed", bootstrap, true);

            // ---- machine card open (the real docked card of T534) ----
            Assert.That(presenter.OpenSlot(W006VFixture.CardSlot), Is.True, "a tap on the machine opens the card");
            Assert.That(shell.IsCardOpen, Is.True, "the docked card, not a sheet");
            yield return Settle(60);
            Log("B4B-REAL-CARD screen=" + screenLabel + " open=" + shell.IsCardOpen + " slot=" + shell.CardSlot + " veinHidden=" + vein.IsHidden + " veinHolding=" + vein.IsHolding);
            yield return SetPassBlock(screenLabel + "/machine-card-open", bootstrap, forced, null, pops);
            yield return FrameRuns(screenLabel + "/machine-card-open", pops, bootstrap, control);
            yield return RefreshRun(screenLabel + "/machine-card-open", pops, bootstrap);
            yield return RefreshStill(screenLabel + "/machine-card-open", bootstrap, false);
            Assert.That(shell.HandleBack(), Is.True, "Back closes the card");
            yield return Settle(60);
            Assert.That(shell.IsCardOpen, Is.False);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------
        // SetPass per world-camera render, total and per atlas (budget 8), the census, the textures (budgets 6, 7)
        // ------------------------------------------------------------------------------------------------------------------------------------

        private sealed class AtlasGroup
        {
            public string Name;
            public Texture Texture;
            public readonly List<Renderer> Renderers = new List<Renderer>();

            /// <summary>The F006 atlases (the world atlas and the FX atlas); the F004 pages are the rest.</summary>
            public bool IsF006 => Texture != null && (Texture.name.Contains("-world-") || Texture.name.Contains("-fx-"));
        }

        private static Texture TextureOf(Renderer renderer)
        {
            if (renderer is SpriteRenderer sprite) return sprite.sprite != null ? sprite.sprite.texture : null;
            if (renderer is TilemapRenderer)
            {
                var tilemap = renderer.GetComponent<Tilemap>();
                if (tilemap == null) return null;
                foreach (var position in tilemap.cellBounds.allPositionsWithin)
                {
                    var tileSprite = tilemap.GetSprite(position);
                    if (tileSprite != null) return tileSprite.texture;
                }

                return null;
            }

            return null;
        }

        private static List<AtlasGroup> GroupsOfLiveRenderers()
        {
            var groups = new Dictionary<Texture, AtlasGroup>();
            var renderers = new List<Renderer>();
            renderers.AddRange(UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude).Where(r => r.enabled && r.sprite != null));
            renderers.AddRange(UnityEngine.Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Exclude).Where(r => r.enabled));
            foreach (var renderer in renderers)
            {
                var texture = TextureOf(renderer);
                if (texture == null) continue;
                if (!groups.TryGetValue(texture, out var group))
                {
                    group = new AtlasGroup { Texture = texture, Name = texture.name + "(" + texture.width + "x" + texture.height + ")" };
                    groups[texture] = group;
                }

                group.Renderers.Add(renderer);
            }

            return groups.Values.OrderByDescending(g => g.Renderers.Count).ToList();
        }

        private static IEnumerator SetPassBlock(string label, CoreGameFoundationBootstrap bootstrap, IReadOnlyList<SlotVisual> forced, Action holdVein, Action pops)
        {
            var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);
            RenderTexture target = null;
            var camera = bootstrap.FactoryCamera;
            var previousTarget = camera.targetTexture;
            var grid = bootstrap.SlotGrid;
            try
            {
                if (!setPass.Valid)
                {
                    Log("B4B-SETPASS label=" + label + " recorder=unavailable");
                    yield break;
                }

                target = new RenderTexture(Mathf.Max(16, (int)camera.pixelWidth), Mathf.Max(16, (int)camera.pixelHeight), 24);
                for (var settle = 0; settle < 3; settle++)
                {
                    holdVein?.Invoke();
                    pops?.Invoke();
                    if (forced != null) grid.Render(forced);
                    yield return null;
                }

                var census = RendererCensus.OfLoadedScenes();
                var groups = GroupsOfLiveRenderers();
                Log("B4B-CENSUS-REAL label=" + label + " " + census + " vsSigned95=" + (census.Total - 95) + " vsDraft105=" + (census.Total - 105) + " vsTier173=" + (census.Total - 173) +
                    " atlasTextures=" + groups.Count + " perTexture=" + string.Join(";", groups.Select(g => g.Name + "=" + g.Renderers.Count)));

                // Configurations: all renderers; each atlas texture's renderers disabled; F004 pages only; F006 atlases only. The extra renders go to an exact-size RenderTexture.
                // The recorder reads the frame two steps back, so each configuration runs 12 blocks of 10 frames (without and with the 20 extra renders, alternating) and the first
                // 4 readings of every block are discarded: a reading is the mode of its block.
                // camera.Render() CPU cost (budget 8 tier metric): 20 warm-up renders, then 200 timed ones into the exact-size RenderTexture.
                camera.targetTexture = target;
                for (var i = 0; i < 20; i++) camera.Render();
                var renderMs = new List<double>();
                for (var i = 0; i < 200; i++)
                {
                    var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    camera.Render();
                    renderMs.Add((System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                }

                camera.targetTexture = previousTarget;
                Log("B4B-CAMERA-RENDER label=" + label + " renders=200 cpuMs mean/p50/p95/p99/max=" + Stat(renderMs) + " renderers=" + census.Total);
                var configs = new List<KeyValuePair<string, List<Renderer>>> { new KeyValuePair<string, List<Renderer>>("all", null) };
                foreach (var group in groups) configs.Add(new KeyValuePair<string, List<Renderer>>("without " + group.Name, group.Renderers));
                configs.Add(new KeyValuePair<string, List<Renderer>>("F004 pages only (F006 atlases off)", groups.Where(g => g.IsF006).SelectMany(g => g.Renderers).ToList()));
                configs.Add(new KeyValuePair<string, List<Renderer>>("F006 atlases only (F004 pages off)", groups.Where(g => !g.IsF006).SelectMany(g => g.Renderers).ToList()));
                var perRender = new Dictionary<string, double>();
                foreach (var config in configs)
                {
                    var withValues = new List<double>();
                    var withoutValues = new List<double>();
                    for (var block = 0; block < SetPassFrames * 3 / 10; block++)
                    {
                        var with = block % 2 == 1;
                        for (var k = 0; k < 10; k++)
                        {
                            holdVein?.Invoke();
                            pops?.Invoke();
                            if (forced != null) grid.Render(forced);
                            if (config.Value != null)
                            {
                                foreach (var renderer in config.Value) if (renderer != null) renderer.enabled = false;
                            }

                            if (with)
                            {
                                camera.targetTexture = target;
                                for (var i = 0; i < Renders; i++) camera.Render();
                                camera.targetTexture = previousTarget;
                            }

                            yield return null;
                            if (k >= 4) (with ? withValues : withoutValues).Add(setPass.LastValue);
                        }
                    }

                    var meanWith = withValues.Average();
                    var meanWithout = withoutValues.Average();
                    perRender[config.Key] = (meanWith - meanWithout) / Renders;
                    Log("B4B-SETPASS label=" + label + " config=\"" + config.Key + "\" passesPerRender=" + Num(perRender[config.Key], "F2") + " frameWith mean/min/max=" + Num(meanWith, "F1") + "/" + Num(withValues.Min(), "F0") + "/" + Num(withValues.Max(), "F0") +
                        " frameWithout(natural frame, UI included) mean/min/max=" + Num(meanWithout, "F1") + "/" + Num(withoutValues.Min(), "F0") + "/" + Num(withoutValues.Max(), "F0"));
                    if (config.Value != null)
                    {
                        foreach (var renderer in config.Value) if (renderer != null) renderer.enabled = true;
                    }
                }

                foreach (var group in groups)
                {
                    Log("B4B-SETPASS-ATLAS label=" + label + " atlas=\"" + group.Name + "\" renderers=" + group.Renderers.Count + " f006=" + group.IsF006 +
                        " deltaPassesPerRender=" + Num(perRender["all"] - perRender["without " + group.Name], "F2") + " memoryBytes=" + Profiler.GetRuntimeMemorySizeLong(group.Texture));
                }

                // Textures: every distinct texture the world sprites reference (budgets 6 and 7), by the runtime memory the player reports.
                long total = 0;
                var pages = 0;
                var largest = 0;
                foreach (var group in groups)
                {
                    var bytes = Profiler.GetRuntimeMemorySizeLong(group.Texture);
                    total += bytes;
                    pages++;
                    largest = Math.Max(largest, Math.Max(group.Texture.width, group.Texture.height));
                    Log("B4B-TEXTURE label=" + label + " name=\"" + group.Texture.name + "\" size=" + group.Texture.width + "x" + group.Texture.height + " format=" + (group.Texture is Texture2D t2 ? t2.format.ToString() : "?") +
                        " runtimeBytes=" + bytes + " renderers=" + group.Renderers.Count);
                }

                Log("B4B-TEXTURE-TOTAL label=" + label + " pages=" + pages + " largestSide=" + largest + " runtimeBytes=" + total + " mib=" + Num(total / 1048576.0, "F2"));
            }
            finally
            {
                camera.targetTexture = previousTarget;
                if (target != null) UnityEngine.Object.Destroy(target);
                setPass.Dispose();
            }

            // The tilemap renderers are not re-enabled by any view per frame: make sure the world is as it was.
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Exclude)) renderer.enabled = true;
            yield return null;
        }
    }
}
#endif
