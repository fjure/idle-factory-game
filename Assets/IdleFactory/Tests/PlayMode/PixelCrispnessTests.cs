using System.Collections;
using System.Text;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// FR-016 / SC-005 (T427): at the integer camera scale every art pixel is a uniform <c>n x n</c> block of device pixels, at rest and
    /// in every frame of a slow pan. The capture follows research R13: the production <see cref="FactoryCameraController"/> renders a
    /// synthetic art sprite (pixels per unit = D, point filtered, every neighbouring art pixel a different colour) into a
    /// <see cref="RenderTexture"/> of the exact size with MSAA off, and the frame is read back and compared block by block with the
    /// source texture. The synthetic sprite stands in for the F004 art until it lands; the camera is the code under test.
    /// </summary>
    public sealed class PixelCrispnessTests
    {
        private const int Density = 24;
        private const int SpriteCells = 2;
        private const int ArtSize = SpriteCells * Density;
        private const float Tolerance = 2.5f;

        private readonly System.Collections.Generic.List<Object> owned = new System.Collections.Generic.List<Object>();
        private RenderTexture target;
        private Camera camera;
        private FactoryCameraController controller;
        private Texture2D art;
        private Color32[] artPixels;
        private Vector2 spriteOrigin;

        [TearDown]
        public void TearDown()
        {
            foreach (var item in owned)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            owned.Clear();
            if (target != null)
            {
                RenderTexture.active = null;
                target.Release();
                Object.DestroyImmediate(target);
                target = null;
            }
        }

        private static Color32 ArtColor(int x, int y) =>
            new Color32((byte)((x * 53 + y * 17) & 255), (byte)((x * 29 + y * 71 + 40) & 255), (byte)(((x ^ y) * 11 + 90) & 255), 255);

        private IEnumerator Build(int width, int height)
        {
            art = new Texture2D(ArtSize, ArtSize, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "crispness-art",
            };
            artPixels = new Color32[ArtSize * ArtSize];
            for (var y = 0; y < ArtSize; y++)
            {
                for (var x = 0; x < ArtSize; x++)
                {
                    artPixels[y * ArtSize + x] = ArtColor(x, y);
                }
            }

            art.SetPixels32(artPixels);
            art.Apply(false, false);
            owned.Add(art);

            var sprite = Sprite.Create(art, new Rect(0f, 0f, ArtSize, ArtSize), Vector2.zero, Density, 0, SpriteMeshType.FullRect);
            owned.Add(sprite);
            var spriteObject = new GameObject("crispness-sprite");
            owned.Add(spriteObject);

            // The factory centre of the 3-column grid is (3.5, 4.75); the sprite is centred on it, on art pixel boundaries.
            spriteOrigin = new Vector2(3.5f - SpriteCells * 0.5f, 4.75f - SpriteCells * 0.5f);
            spriteObject.transform.position = new Vector3(spriteOrigin.x, spriteOrigin.y, 0f);
            spriteObject.AddComponent<SpriteRenderer>().sprite = sprite;

            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            {
                antiAliasing = 1,
                useMipMap = false,
                filterMode = FilterMode.Point,
                name = "crispness-target",
            };
            target.Create();

            var cameraObject = new GameObject("crispness-camera");
            owned.Add(cameraObject);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowMSAA = false;
            camera.allowHDR = false;
            camera.targetTexture = target;
            camera.enabled = false;
            controller = cameraObject.AddComponent<FactoryCameraController>();
            controller.Configure(camera, new RunZoomSettings().ToScaleSettings());
            controller.Fit(new ScaleLayout(width, height, 1f, new Rect(0f, 0f, width, height), 3));

            // Warm-up (the first frames of a run can lose draws while the pipeline initialises); the result is not used.
            for (var frame = 0; frame < 3; frame++)
            {
                camera.Render();
                yield return null;
            }
        }

        private Color32[] Capture()
        {
            camera.Render();
            var previous = RenderTexture.active;
            var readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, false);
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                readback.Apply(false);
                return readback.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(readback);
            }
        }

        /// <summary>Renders one frame and returns a failure description, or null when every visible art pixel is a uniform n x n block of its source colour.</summary>
        private string CheckFrame(out int blocks)
        {
            var model = controller.Model;
            var n = model.Scale;
            var width = target.width;
            var height = target.height;
            var pixels = Capture();
            blocks = 0;
            var bad = new StringBuilder();
            var failures = 0;
            for (var iy = 0; iy < ArtSize; iy++)
            {
                for (var ix = 0; ix < ArtSize; ix++)
                {
                    var corner = spriteOrigin + new Vector2(ix, iy) / Density;
                    var screen = model.WorldToScreen(corner, model.Position, n);
                    var sx = Mathf.RoundToInt(screen.x);
                    var sy = Mathf.RoundToInt(screen.y);
                    if (Mathf.Abs(screen.x - sx) > 0.02f || Mathf.Abs(screen.y - sy) > 0.02f)
                    {
                        failures++;
                        if (failures < 4)
                        {
                            bad.Append($"art pixel ({ix},{iy}) edge at ({screen.x:0.###},{screen.y:0.###}) is not on a device pixel; ");
                        }

                        continue;
                    }

                    if (sx < 0 || sy < 0 || sx + n > width || sy + n > height)
                    {
                        continue;
                    }

                    blocks++;
                    var expected = artPixels[iy * ArtSize + ix];
                    var mismatched = false;
                    for (var dy = 0; dy < n && !mismatched; dy++)
                    {
                        for (var dx = 0; dx < n; dx++)
                        {
                            var actual = pixels[(sy + dy) * width + sx + dx];
                            if (Mathf.Abs(actual.r - expected.r) > Tolerance || Mathf.Abs(actual.g - expected.g) > Tolerance || Mathf.Abs(actual.b - expected.b) > Tolerance)
                            {
                                mismatched = true;
                                failures++;
                                if (failures < 4)
                                {
                                    bad.Append($"art pixel ({ix},{iy}) block at ({sx},{sy}) n={n}: device pixel +({dx},{dy}) is {actual} not {expected}; ");
                                }

                                break;
                            }
                        }
                    }
                }
            }

            return failures == 0 ? null : $"{failures} blocks are not uniform n x n blocks of the art pixel: {bad}";
        }

        [Category("StandalonePlayer")]
        [UnityTest]
        public IEnumerator EveryAllowedScaleRendersUniformBlocksAtRest([Values(640, 641)] int width)
        {
            var height = width - 160; // 640x480 (even) and 641x481 (odd: the half-device-pixel grid offset).
            yield return Build(width, height);

            var seen = 0;
            do
            {
                yield return null;
                var failure = CheckFrame(out var blocks);
                Assert.That(failure, Is.Null, $"{width}x{height} n={controller.Scale}: {failure}");
                Assert.That(blocks, Is.GreaterThan(ArtSize * ArtSize / 4), $"{width}x{height} n={controller.Scale}: enough of the sprite is on screen to judge");
                Assert.That(camera.orthographicSize, Is.EqualTo(height / (2f * controller.Scale * Density)).Within(1e-5f), "rule 1");
                seen++;
                if (controller.ZoomInState != ZoomButtonState.Enabled)
                {
                    break;
                }

                controller.ZoomFromVisibleControl(1);
            }
            while (seen < 12);

            Assert.That(seen, Is.GreaterThanOrEqualTo(2), "more than one allowed scale was checked");
            Assert.That(controller.Scale, Is.EqualTo(6), "the walk reached maxScale");
        }

        [Category("StandalonePlayer")]
        [UnityTest]
        public IEnumerator EveryFrameOfAScriptedSlowPanKeepsUniformBlocks([Values(640, 641)] int width, [Values(3, 6)] int scale)
        {
            var height = width - 160;
            yield return Build(width, height);
            while (controller.Scale < scale)
            {
                controller.ZoomFromVisibleControl(1);
            }

            Assert.That(controller.Scale, Is.EqualTo(scale));
            var start = (Vector2)camera.transform.position;
            var frames = 0;
            var distinctPositions = 0;
            var previous = start;
            for (var frame = 0; frame < 48; frame++)
            {
                // A finger moving well below one device pixel per frame: the sub-pixel remainder accumulates, the camera moves a whole device pixel at a time.
                controller.PanByScreenDelta(new Vector2(0.37f, -0.21f));
                yield return null;
                var failure = CheckFrame(out var blocks);
                Assert.That(failure, Is.Null, $"{width}x{height} n={scale} pan frame {frame}: {failure}");
                Assert.That(blocks, Is.GreaterThan(ArtSize * ArtSize / 4), $"pan frame {frame}: the sprite stays on screen");
                var position = (Vector2)camera.transform.position;
                if (position != previous)
                {
                    distinctPositions++;
                    previous = position;
                }

                frames++;
            }

            Assert.That(frames, Is.EqualTo(48));
            Assert.That(distinctPositions, Is.GreaterThanOrEqualTo(8), "the scripted pan really moved the camera, one whole device pixel at a time");
            Assert.That(Vector2.Distance(start, previous), Is.GreaterThan(4f / (scale * Density)), "the camera travelled several device pixels");
        }
    }
}
