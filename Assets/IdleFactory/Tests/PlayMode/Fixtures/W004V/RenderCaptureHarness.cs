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

        /// <summary>Pixels where at least one channel differs at all.</summary>
        public long Differing { get; }

        public int MaxChannelDelta { get; }
        public double DifferingFraction => Pixels == 0 ? 0d : (double)Differing / Pixels;

        public bool Passes => SameSize && DifferingFraction <= RenderParity.MaxDifferingFraction && MaxChannelDelta <= RenderParity.MaxChannelDelta;

        public override string ToString() =>
            SameSize
                ? string.Format(CultureInfo.InvariantCulture, "{0}/{1} pixels differ ({2:P4}), max channel delta {3}/255", Differing, Pixels, DifferingFraction, MaxChannelDelta)
                : "image sizes differ";
    }

    /// <summary>
    /// The comparison helper of research R13. After the phase-0 switch at most 0.1 percent of the pixels may differ and no
    /// channel of any pixel may differ by more than 2/255. T411 runs the same helper against the same baselines.
    /// </summary>
    internal static class RenderParity
    {
        public const double MaxDifferingFraction = 0.001d;
        public const int MaxChannelDelta = 2;

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
                if (d > 0)
                {
                    differing++;
                    if (d > maxDelta) maxDelta = d;
                }
            }

            return new ParityResult(true, expected.Pixels.Length, differing, maxDelta);
        }
    }

    /// <summary>The committed baseline frames under <c>Fixtures/Screenshots/F004-B1/</c>.</summary>
    internal static class RenderBaselines
    {
        public const string Directory = "Assets/IdleFactory/Tests/PlayMode/Fixtures/Screenshots/F004-B1";
        public const string WriteEnvironmentVariable = "IDLEFACTORY_RENDER_BASELINE_WRITE";

        public static bool WriteMode => Environment.GetEnvironmentVariable(WriteEnvironmentVariable) == "1";

        public static string AssetPath(string stem) => Directory + "/" + stem + ".png";

        public static string FullPath(string stem) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", AssetPath(stem)));

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
                into["w003s-" + slug + "-world"] = Read(worldTexture);
                into["w003s-" + slug + "-shell"] = Read(rig.Texture);

                // W004-V: forced presentation states over the same checkpoint, both camera presets.
                grid.Render(W004VFixture.Apply(grid.Visuals));
                foreach (var preset in new[] { W004VCameraPreset.NMin, W004VCameraPreset.FitAll })
                {
                    var ppc = preset == W004VCameraPreset.FitAll
                        ? framing.FitAll(area, columns, pixelsPerPoint)
                        : ApplyNMin(camera, controller, area, columns, zoom, pixelsPerPoint);
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
}
#endif
