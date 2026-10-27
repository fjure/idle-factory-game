// T410 measurement helper (Feature 004, research R12). NOT a committed test: scripts/f004-b1/run-w004v.ps1 copies this
// file to Assets/IdleFactory/Tests/PlayMode/ for one graphical editor run and deletes it afterwards, because a committed
// test would change the test manifest that T411 compares against. It reuses the T409 capture code (copied below with the
// class renamed) and, for the W004-V frame, records
//   - Frame Debugger events whose render target is the world texture (batches), shader passes and the SetPass estimate,
//   - ProfilerRecorder Batches/SetPass/Draw Calls counters as a with-minus-without difference over N frames,
//   - the runtime size of the textures behind the drawn sprites (active build target's texture format).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;

namespace IdleFactory.Tests.PlayMode
{
    internal static class B1ProbeHarness
    {
        private static readonly Color ShellClear = new Color32(40, 40, 40, 255);
        private static PresentationSpriteCatalog sprites;
        private static Color worldBackground;
        private static CameraClearFlags worldClearFlags;
        private static float cameraZ;
        private static float nearClip;
        private static float farClip;

        /// <summary>The screen classes of Feature 003 (<see cref="ShellScreens.All"/>) and the 640x480 Game view of the scene suites (R13).</summary>
        public static IEnumerable<ShellScreen> Screens()
        {
            yield return new ShellScreen("desktop 640x480", 640, 480, 0, 0, 0, 0, FactoryHudDocument.Classify(640, 480));
            foreach (var screen in ShellScreens.All())
            {
                yield return screen;
            }
        }

        public static string Slug(ShellScreen screen) => screen.Width + "x" + screen.Height;

        public static IEnumerable<string> AllStems()
        {
            foreach (var screen in Screens())
            {
                var slug = Slug(screen);
                yield return "w003s-" + slug + "-world";
                yield return "w003s-" + slug + "-shell";
                yield return "w004v-nmin-" + slug + "-world";
                yield return "w004v-fitall-" + slug + "-world";
            }
        }

        /// <summary>Loads the authored scene once to take over its sprite catalog and camera look, then unloads it.</summary>
        private static IEnumerator EnsureSceneArt()
        {
            if (sprites != null)
            {
                yield break;
            }

            var root = RunSceneRun.NewRoot();
            var run = new RunSceneRun(root, new RunSceneClock());
            yield return run.Start(RunSceneRun.IronBytes, true);
            var bootstrap = run.Bootstrap;
            var valid = bootstrap.SpriteCatalog.IsValid;
            var camera = bootstrap.FactoryCamera;
            var catalogRef = bootstrap.SpriteCatalog;
            var background = camera.backgroundColor;
            background.a = 1f;
            var flags = camera.clearFlags;
            var z = camera.transform.position.z;
            var near = camera.nearClipPlane;
            var far = camera.farClipPlane;
            yield return run.Stop();
            RunSceneRun.DeleteRoot(root);
            Assert.That(valid, Is.True, "the scene's sprite catalog is valid");
            sprites = catalogRef;
            worldBackground = background;
            worldClearFlags = flags;
            cameraZ = z;
            nearClip = near;
            farClip = far;
        }

        private static IEnumerator Wait(float seconds, int minimumFrames)
        {
            var until = Time.realtimeSinceStartup + seconds;
            var frames = 0;
            while (frames < minimumFrames || Time.realtimeSinceStartup < until)
            {
                frames++;
                yield return null;
            }
        }

        /// <summary>
        /// Captures the four frames of one screen class into <paramref name="into"/>: the W003-S world and shell at fit-all,
        /// and the W004-V world at <c>n_min</c> and fit-all. Metric lines for the W004-V world go to <paramref name="metrics"/>.
        /// </summary>
        public static IEnumerator Measure(ShellScreen screen, List<string> metrics)
        {
            if (sprites != null && !sprites.Validate())
            {
                sprites = null;
            }

            yield return EnsureSceneArt();
            var slug = Slug(screen);
            var owned = new List<Object>();
            ShellRig rig = null;
            RunShellPresenter presenter = null;
            try
            {
                rig = ShellRig.Create(screen);

                // The shell column draws nothing, so the panel must clear its target (workstation pitfall).
                var settings = rig.Document.panelSettings;
                settings.clearColor = true;
                settings.colorClearValue = ShellClear;

                var fixture = W003PlayerFixtures.Stress;
                var catalog = W003PlayerFixtures.Catalog(fixture);
                var document = W003PlayerFixtures.Load(fixture, catalog);
                var session = new RunSession(catalog, document.Run, document.Permanent, document.Meta.DepartureUtcMs);
                Assert.That(session.Advance(0).IsSuccess, Is.True, "W003-S settles at the load instant");
                var commands = new RunCommands(session);

                var gridObject = new GameObject("render-parity-grid");
                owned.Add(gridObject);
                var grid = gridObject.AddComponent<SlotGridView>();
                grid.Configure(sprites);
                var columns = SlotGridLayout.ColumnsFor(screen.Width, screen.Height);
                grid.SetColumns(columns);
                presenter = new RunShellPresenter(session, commands, rig.Shell, grid,
                    id => catalog.Machines.TryGetValue(id, out var machine) ? machine.PresentationKey : string.Empty);
                presenter.Invalidate();
                presenter.Refresh();

                var worldTexture = new RenderTexture(screen.Width, screen.Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
                {
                    antiAliasing = 1,
                    useMipMap = false,
                    filterMode = FilterMode.Point,
                    name = "render-parity-world",
                };
                worldTexture.Create();
                owned.Add(worldTexture);

                var cameraObject = new GameObject("render-parity-camera");
                owned.Add(cameraObject);
                cameraObject.transform.position = new Vector3(0f, 0f, cameraZ);
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = worldClearFlags;
                camera.backgroundColor = worldBackground;
                camera.nearClipPlane = nearClip;
                camera.farClipPlane = farClip;
                camera.allowMSAA = false;
                camera.allowHDR = false;
                camera.targetTexture = worldTexture;
                camera.enabled = false;
                var controller = cameraObject.AddComponent<FactoryCameraController>();
                var zoom = new RunZoomSettings();
                var framing = new RunCameraFraming(camera, controller, zoom);

                yield return rig.Layout();
                yield return Wait(0.75f, 6);
                Assert.That(rig.Shell.TryGetUnobstructedScreenRect(screen.Height, out var area), Is.True, slug + ": the shell reports its world area");
                var pixelsPerPoint = rig.PixelsPerPoint;
                framing.ConfigureLimits(columns, pixelsPerPoint);

                // W003-S, fit-all.
                var fitAll = framing.FitAll(area, columns, pixelsPerPoint);
                grid.SetProjectedPixelsPerCell(fitAll);
                camera.Render();
                yield return MeasureFrame(camera, worldTexture, "w003s-fitall-" + slug, grid, metrics);

                // W004-V: forced presentation states over the same checkpoint, both camera presets.
                grid.Render(W004VFixture.Apply(grid.Visuals));
                foreach (var preset in new[] { W004VCameraPreset.NMin, W004VCameraPreset.FitAll })
                {
                    var ppc = preset == W004VCameraPreset.FitAll
                        ? framing.FitAll(area, columns, pixelsPerPoint)
                        : ApplyNMin(camera, controller, area, columns, zoom, pixelsPerPoint);
                    grid.SetProjectedPixelsPerCell(ppc);
                    camera.Render();
                    yield return MeasureFrame(camera, worldTexture, "w004v-" + W004VFixture.PresetStem(preset) + "-" + slug, grid, metrics);
                    metrics?.Add(string.Format(CultureInfo.InvariantCulture, "RENDER-FRAME fixture=W004-V preset={0} screen={1} ppc={2:F2} sprites-drawn={3}",
                        W004VFixture.PresetStem(preset), slug, ppc, grid.ActiveRendererCount));
                }

                // Metrics: the Editor cannot report batches or SetPass for this frame (UnityStats also counts the Editor's own
                // rendering and the profiler recorders read 0 in batch mode), so batches, SetPass and shader variants are
                // collected from the Windows player and the build report in T410. What is exact here: the sprites the grid draws
                // and the runtime size of the textures behind them.
                metrics?.Add(string.Format(CultureInfo.InvariantCulture,
                    "RENDER-METRIC fixture=W004-V screen={0} sprite-renderers={1} texture-bytes={2} textures={3}",
                    slug, grid.ActiveRendererCount, TextureBytes(gridObject, out var textureCount), textureCount));
            }
            finally
            {
                presenter?.Dispose();
                rig?.Dispose();
                foreach (var item in owned)
                {
                    if (item is RenderTexture texture)
                    {
                        texture.Release();
                    }

                    if (item != null)
                    {
                        Object.DestroyImmediate(item);
                    }
                }
            }
        }

        private static readonly string[] CounterNames = { "Batches Count", "SetPass Calls Count", "Draw Calls Count", "Triangles Count", "Vertices Count" };

        private static IEnumerator MeasureFrame(Camera camera, RenderTexture target, string label, SlotGridView grid, List<string> metrics)
        {
            // ProfilerRecorder differential. One camera.Render() is too small against the Editor's own rendering, so every
            // "with" frame renders the world camera K times and every "without" frame renders nothing; phases alternate
            // (without, with, without, with) to cancel drift. Value per render = (with - without) / K.
            const int K = 20;
            const int framesPerPhase = 30;
            var recorders = CounterNames.Select(n => ProfilerRecorder.StartNew(ProfilerCategory.Render, n, 1)).ToArray();
            try
            {
                var sumWith = new double[recorders.Length];
                var sumWithout = new double[recorders.Length];
                var nWith = 0;
                var nWithout = 0;
                var minWith = Enumerable.Repeat(double.MaxValue, recorders.Length).ToArray();
                var maxWithout = Enumerable.Repeat(double.MinValue, recorders.Length).ToArray();
                for (var phase = 0; phase < 4; phase++)
                {
                    var with = phase % 2 == 1;
                    yield return null;
                    yield return null;
                    for (var f = 0; f < framesPerPhase; f++)
                    {
                        if (with)
                        {
                            for (var k = 0; k < K; k++) camera.Render();
                        }

                        yield return null;
                        if (with) nWith++; else nWithout++;
                        for (var r = 0; r < recorders.Length; r++)
                        {
                            var v = recorders[r].Valid ? recorders[r].LastValue : double.NaN;
                            if (with) { sumWith[r] += v; minWith[r] = Math.Min(minWith[r], v); }
                            else { sumWithout[r] += v; maxWithout[r] = Math.Max(maxWithout[r], v); }
                        }
                    }
                }

                var line = new StringBuilder(string.Format(CultureInfo.InvariantCulture, "B1-COUNTERS label={0} renders-per-with-frame={1} frames-with={2} frames-without={3} sprite-renderers={4}", label, K, nWith, nWithout, grid.ActiveRendererCount));
                for (var r = 0; r < recorders.Length; r++)
                {
                    var perRender = (sumWith[r] / nWith - sumWithout[r] / nWithout) / K;
                    line.Append(string.Format(CultureInfo.InvariantCulture, " {0}(with={1:F1} without={2:F1} perRender={3:F2} minWith={4:F0} maxWithout={5:F0})", CounterNames[r].Replace(' ', '_'), sumWith[r] / nWith, sumWithout[r] / nWithout, perRender, minWith[r], maxWithout[r]));
                }

                metrics.Add(line.ToString());
            }
            finally
            {
                foreach (var r in recorders) r.Dispose();
            }

            yield break;
        }

        /// <summary>Zooms to the readability floor and centres the grid (the pre-scale-model stand-in for <c>n_min</c>).</summary>
        private static float ApplyNMin(Camera camera, FactoryCameraController controller, Rect area, int columns, RunZoomSettings zoom, float pixelsPerPoint)
        {
            var ppc = zoom.Clamp(zoom.fitAllFloorPixelsPerCell);
            camera.orthographicSize = Mathf.Clamp(camera.pixelHeight / pixelsPerPoint / (2f * ppc), controller.MinimumOrthographicSize, controller.MaximumOrthographicSize);
            controller.SetUnobstructedScreenRect(area);
            controller.FrameContent(SlotGridLayout.Extent(columns));
            return RunCameraFraming.PixelsPerCell(camera, pixelsPerPoint);
        }

        /// <summary>Runtime size of the distinct textures behind the sprites the grid currently draws (Editor figure, not a player figure).</summary>
        private static long TextureBytes(GameObject gridObject, out int textureCount)
        {
            var textures = new HashSet<Texture>();
            foreach (var renderer in gridObject.GetComponentsInChildren<SpriteRenderer>())
            {
                if (renderer.enabled && renderer.sprite != null)
                {
                    textures.Add(renderer.sprite.texture);
                }
            }

            textureCount = textures.Count;
            return textures.Sum(texture => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture));
        }
    }

    public sealed class B1W004VProbeTests
    {
        public static IEnumerable<ShellScreen> Screens()
        {
            yield return new ShellScreen("desktop 640x480", 640, 480, 0, 0, 0, 0, FactoryHudDocument.Classify(640, 480));
            yield return new ShellScreen("phone 390x844", 390, 844, 0, 0, 0, 0, FactoryHudDocument.Classify(390, 844));
            yield return new ShellScreen("desktop 1280x720", 1280, 720, 0, 0, 0, 0, FactoryHudDocument.Classify(1280, 720));
        }

        [UnityTest]
        public IEnumerator MeasureW004V([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            var metrics = new List<string>();
            Debug.Log("B1-ENV graphicsDevice=" + SystemInfo.graphicsDeviceType + " " + SystemInfo.graphicsDeviceName + " activeBuildTarget=" + EditorUserBuildSettings.activeBuildTarget + " fixture=" + W004VFixture.Name + " hash=" + W004VFixture.Hash());
            yield return B1ProbeHarness.Measure(screen, metrics);
            foreach (var line in metrics) Debug.Log(line);
        }
    }
}
#endif
