using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>One captured frame: opaque RGBA32 pixels, bottom-left origin.</summary>
    internal sealed class CapturedImage
    {
        public CapturedImage(int width, int height, Color32[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }

        public int Width { get; }
        public int Height { get; }
        public Color32[] Pixels { get; }
    }

    /// <summary>The outcome of comparing two frames under the phase-0 tolerance (research R13).</summary>
    internal readonly struct ParityResult
    {
        public ParityResult(bool sameSize, long pixels, long differing, int maxChannelDelta)
        {
            SameSize = sameSize;
            Pixels = pixels;
            Differing = differing;
            MaxChannelDelta = maxChannelDelta;
        }

        public bool SameSize { get; }
        public long Pixels { get; }

        /// <summary>Pixels where at least one channel differs by more than 2/255 (R13 counts only those).</summary>
        public long Differing { get; }

        public int MaxChannelDelta { get; }
        public double DifferingFraction => Pixels == 0 ? 0d : (double)Differing / Pixels;

        // Under the literal R13 rule (DifferingThreshold == MaxChannelDelta) a counted pixel already breaks the channel rule, so the fraction
        // clause can never decide on its own; it stays only because R13 states the 0.1 % budget (T457, phase0-review A6, F1).
        public bool Passes => SameSize && DifferingFraction <= RenderParity.MaxDifferingFraction && MaxChannelDelta <= RenderParity.MaxChannelDelta;

        public override string ToString() =>
            SameSize
                ? string.Format(CultureInfo.InvariantCulture, "{0}/{1} pixels differ ({2:P4}), max channel delta {3}/255", Differing, Pixels, DifferingFraction, MaxChannelDelta)
                : "image sizes differ";
    }

    /// <summary>
    /// The comparison helper of research R13. After the phase-0 switch at most 0.1 percent of the pixels may differ (a pixel counts as
    /// differing only when some channel differs by more than 2/255) and no channel of any pixel may differ by more than 2/255. T411 runs the same helper against the same baselines.
    /// Because the counting threshold equals the channel limit (owner decision, Issue #523), the 0.1 percent budget is vestigial: it can never
    /// fail a frame that the channel rule accepts.
    /// </summary>
    internal static class RenderParity
    {
        public const double MaxDifferingFraction = 0.001d;
        public const int MaxChannelDelta = 2;
        public const int DifferingThreshold = 2;

        public static ParityResult Compare(CapturedImage expected, CapturedImage actual)
        {
            if (expected.Width != actual.Width || expected.Height != actual.Height)
            {
                return new ParityResult(false, 0, 0, 0);
            }

            long differing = 0;
            var maxDelta = 0;
            for (var i = 0; i < expected.Pixels.Length; i++)
            {
                var a = expected.Pixels[i];
                var b = actual.Pixels[i];
                var d = Math.Max(Math.Max(Math.Abs(a.r - b.r), Math.Abs(a.g - b.g)), Math.Max(Math.Abs(a.b - b.b), Math.Abs(a.a - b.a)));
                if (d > maxDelta) maxDelta = d;
                if (d > DifferingThreshold) differing++;
            }

            return new ParityResult(true, expected.Pixels.Length, differing, maxDelta);
        }
    }

    /// <summary>
    /// The committed baseline frames. <see cref="B1Directory"/> holds the pre-switch Built-in frames (T409) and is never rewritten;
    /// <see cref="Directory"/> is the post-switch regression set B2 (owner decision 2026-11-03) that the parity tests compare against.
    /// </summary>
    internal static class RenderBaselines
    {
        public const string B1Directory = "Assets/IdleFactory/Tests/PlayMode/Fixtures/Screenshots/F004-B1";
        public const string Directory = "Assets/IdleFactory/Tests/PlayMode/Fixtures/Screenshots/F004-B2";
        public const string WriteEnvironmentVariable = "IDLEFACTORY_RENDER_BASELINE_WRITE";

        public static bool WriteMode => Environment.GetEnvironmentVariable(WriteEnvironmentVariable) == "1";

        public static string AssetPath(string stem, string directory = Directory) => directory + "/" + stem + ".png";

        public static string FullPath(string stem, string directory = Directory) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", AssetPath(stem, directory)));

        public static string FailureDirectory => Path.Combine(Application.temporaryCachePath, "render-parity");

        public static CapturedImage Load(string stem)
        {
            var bytes = File.ReadAllBytes(FullPath(stem));
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                Assert.That(texture.LoadImage(bytes, false), Is.True, stem + ".png decodes");
                return new CapturedImage(texture.width, texture.height, texture.GetPixels32());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        public static byte[] Encode(CapturedImage image)
        {
            var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false, false);
            try
            {
                texture.SetPixels32(image.Pixels);
                texture.Apply(false);
                return texture.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }

    /// <summary>
    /// The deterministic capture of the phase-0 parity frames (research R13): the W003-S checkpoint through the production
    /// read model, slot grid, camera framing and run shell, rendered into RenderTextures of the exact screen-class size
    /// (MSAA off, HDR off, opaque clear) and read back. The presentation is frozen: no host Update runs, the run clock never
    /// advances, and the grid draws static sprites. Nothing in this class is specific to the Built-in pipeline, so T411 reuses
    /// it unchanged after the switch.
    /// </summary>
    internal static class RenderCaptureHarness
    {
        /// <summary>Discarded renders before the first captured frame of every screen class (T457).</summary>
        public const int WarmUpFrames = 3;

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

        private static CapturedImage Read(RenderTexture texture)
        {
            var previous = RenderTexture.active;
            var readback = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, false);
            try
            {
                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0, false);
                readback.Apply(false);
                return new CapturedImage(texture.width, texture.height, readback.GetPixels32());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(readback);
            }
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
        public static IEnumerator Capture(ShellScreen screen, Dictionary<string, CapturedImage> into, List<string> metrics)
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
                var framing = new LegacyCameraFraming(camera);

                yield return rig.Layout();
                yield return Wait(0.75f, 6);
                Assert.That(rig.Shell.TryGetUnobstructedScreenRect(screen.Height, out var area), Is.True, slug + ": the shell reports its world area");
                var pixelsPerPoint = rig.PixelsPerPoint;
                framing.ConfigureLimits(columns, pixelsPerPoint);

                // W003-S, fit-all.
                var fitAll = framing.FitAll(area, columns, pixelsPerPoint);
                grid.SetProjectedPixelsPerCell(fitAll);

                // T457 warm-up (phase0-review F3): on a cold shader cache or after a target switch the first frames of the first test of a
                // run were the ones that lost draws while the pipeline and the sprite shaders initialised (desktop 640x480 outlier,
                // 18.9 % differing once). Render and discard a few frames of the exact scene first, so the captures below always see a
                // warm pipeline. The warm-up result is not stored.
                for (var warm = 0; warm < WarmUpFrames; warm++)
                {
                    camera.Render();
                    yield return null;
                }

                metrics?.Add("RENDER-WARMUP screen=" + slug + " frames=" + WarmUpFrames);

                camera.Render();
                into["w003s-" + slug + "-world"] = Read(worldTexture);
                into["w003s-" + slug + "-shell"] = Read(rig.Texture);

                // W004-V: forced presentation states over the same checkpoint, both camera presets.
                grid.Render(W004VFixture.Apply(grid.Visuals));
                foreach (var preset in new[] { W004VCameraPreset.NMin, W004VCameraPreset.FitAll })
                {
                    var ppc = preset == W004VCameraPreset.FitAll
                        ? framing.FitAll(area, columns, pixelsPerPoint)
                        : framing.ApplyNMin(area, columns, pixelsPerPoint);
                    grid.SetProjectedPixelsPerCell(ppc);
                    camera.Render();
                    into["w004v-" + W004VFixture.PresetStem(preset) + "-" + slug + "-world"] = Read(worldTexture);
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

        /// <summary>
        /// T441: one live frame of a run in context (the world and the shell composited as the game draws them), for the independent Art
        /// Direction review of the locked plates and the HUD icons. The shell panel clears transparent here, so the world shows through its
        /// world column. Evidence only; no baseline is compared with it.
        /// </summary>
        public static IEnumerator CaptureComposite(ShellScreen screen, RunSession session, RunCommands commands, int textScalePercent, Action<Texture2D> done)
        {
            if (sprites != null && !sprites.Validate())
            {
                sprites = null;
            }

            yield return EnsureSceneArt();
            var owned = new List<Object>();
            ShellRig rig = null;
            RunShellPresenter presenter = null;
            try
            {
                rig = ShellRig.Create(screen, null, textScalePercent);
                var settings = rig.Document.panelSettings;
                settings.clearColor = true;
                settings.colorClearValue = Color.clear;
                var catalog = session.Catalog;
                var gridObject = new GameObject("composite-grid");
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
                    name = "composite-world",
                };
                worldTexture.Create();
                owned.Add(worldTexture);
                var cameraObject = new GameObject("composite-camera");
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
                var framing = new LegacyCameraFraming(camera);

                yield return rig.Layout();
                yield return Wait(0.75f, 6);
                Assert.That(rig.Shell.TryGetUnobstructedScreenRect(screen.Height, out var area), Is.True, "the shell reports its world area");
                var pixelsPerPoint = rig.PixelsPerPoint;
                framing.ConfigureLimits(columns, pixelsPerPoint);
                grid.SetProjectedPixelsPerCell(framing.FitAll(area, columns, pixelsPerPoint));
                for (var warm = 0; warm < WarmUpFrames; warm++)
                {
                    camera.Render();
                    yield return null;
                }

                camera.Render();
                var world = Read(worldTexture);
                var shell = Read(rig.Texture);
                var composite = new Texture2D(screen.Width, screen.Height, TextureFormat.RGBA32, false, false);
                var pixels = new Color32[world.Pixels.Length];
                for (var i = 0; i < pixels.Length; i++)
                {
                    var top = shell.Pixels[i];
                    var under = world.Pixels[i];
                    var alpha = top.a / 255f;
                    pixels[i] = new Color32(
                        (byte)Mathf.RoundToInt(top.r * alpha + under.r * (1f - alpha)),
                        (byte)Mathf.RoundToInt(top.g * alpha + under.g * (1f - alpha)),
                        (byte)Mathf.RoundToInt(top.b * alpha + under.b * (1f - alpha)), 255);
                }

                composite.SetPixels32(pixels);
                composite.Apply();
                done(composite);
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
}
#endif
