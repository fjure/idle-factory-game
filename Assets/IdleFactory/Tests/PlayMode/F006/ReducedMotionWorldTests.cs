using System.Collections;
using System.Linq;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T527 (FR-015, contracts/world-presentation.md section 1, Art Bible 21.7): under reduced motion no decorative pixel moves. The deck (base and deck
    /// tiles, hatch, props, edge pieces) is static by construction, and the machine loops hold frame 0. The test renders the whole world for 120 frames
    /// and counts the pixels that differ from the first frame: zero is required with reduced motion on. A control run of the same world with the loops
    /// running must show moving pixels, so the harness is proven able to see motion. Runs in the editor and in the Windows test player.
    /// </summary>
    [Category("StandalonePlayer")]
    public sealed class ReducedMotionWorldTests
    {
        private const int Frames = 120;
        private const int Width = 480;
        private const int Height = 480;

        private DeckTestRig rig;
        private RenderTexture target;
        private Camera camera;

        [SetUp]
        public void SetUp() => rig = new DeckTestRig();

        [TearDown]
        public void TearDown()
        {
            if (target != null)
            {
                RenderTexture.active = null;
                Object.DestroyImmediate(target);
            }

            rig.Dispose();
        }

        private SlotGridView BuildWorld(out FactoryDeckView deck)
        {
            var layout = LatticeLayoutBuilder.ShippedForColumns(3);
            var catalog = rig.Catalog();
            var grid = rig.NewGrid(catalog, layout);
            deck = rig.NewDeck(catalog, rig.Settings(), grid);
            deck.SetStage(1);
            grid.SetProjectedPixelsPerCell(24f);
            grid.Render(DeckTestRig.Frame(12, i => i < 8 ? SlotCondition.Occupied : i < 10 ? SlotCondition.Empty : SlotCondition.Locked));

            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            {
                antiAliasing = 1,
                filterMode = FilterMode.Point,
                name = "reduced-motion-world",
            };
            target.Create();
            var cameraObject = new GameObject("reduced-motion-world-camera");
            rig.Owned.Add(cameraObject);
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = Height / (2f * DeckTestRig.D);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(17, 24, 39, 255);
            camera.allowMSAA = false;
            camera.allowHDR = false;
            camera.targetTexture = target;
            camera.enabled = false;

            // Centre on the deck so the margin ring, the props and the edge V are in view at n = 1.
            var bounds = deck.Plan.BaseCells.Select(c => layout.DeckTileRect(c)).Aggregate((a, b) => Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax)));
            var centre = new Vector2(Mathf.Round(bounds.center.x * 24f) / 24f, Mathf.Round(bounds.center.y * 24f) / 24f);
            cameraObject.transform.position = new Vector3(centre.x, centre.y, -10f);
            return grid;
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

        private IEnumerator MovingPixels(SlotGridView grid, bool reduced, int[] result)
        {
            // Warm up: the first frames of a run can lose draws while the pipeline initialises.
            for (var i = 0; i < 3; i++)
            {
                grid.AdvanceFrames(0, reduced);
                Capture();
                yield return null;
            }

            grid.AdvanceFrames(0, reduced);
            var first = Capture();
            var moved = new bool[first.Length];
            for (var frame = 1; frame <= Frames; frame++)
            {
                grid.AdvanceFrames(frame * 125L, reduced);
                var pixels = Capture();
                for (var i = 0; i < pixels.Length; i++)
                {
                    if (!moved[i] && (pixels[i].r != first[i].r || pixels[i].g != first[i].g || pixels[i].b != first[i].b))
                    {
                        moved[i] = true;
                    }
                }

                yield return null;
            }

            result[0] = moved.Count(m => m);
            result[1] = first.Count(p => p.r != 17 || p.g != 24 || p.b != 39);
        }

        [UnityTest]
        public IEnumerator ZeroDecorativePixelsMoveOver120FramesUnderReducedMotion()
        {
            var grid = BuildWorld(out var deck);
            var result = new int[2];
            yield return MovingPixels(grid, true, result);
            Assert.That(result[1], Is.GreaterThan(Width * Height / 4), "the deck, props and edges drew a substantial image");
            Assert.That(result[0], Is.EqualTo(0), "no pixel of the deck, the props, the edge pieces or the held machine frames changed over " + Frames + " frames");
            Assert.That(deck.PropRenderers.Count(r => r.enabled), Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator TheControlRunWithTheLoopsRunningShowsMovingPixelsSoTheHarnessSeesMotion()
        {
            var grid = BuildWorld(out _);
            var result = new int[2];
            yield return MovingPixels(grid, false, result);
            Assert.That(result[0], Is.GreaterThan(0), "running machine loops change pixels: the counter is able to see motion");
        }

        [Test]
        public void TheDecorativeLayersCarryNoAnimator()
        {
            var layout = LatticeLayoutBuilder.ShippedForColumns(3);
            var catalog = rig.Catalog();
            var grid = rig.NewGrid(catalog, layout);
            var deck = rig.NewDeck(catalog, rig.Settings(), grid);
            grid.Render(DeckTestRig.Frame(12, i => SlotCondition.Locked));
            Assert.That(deck.PropRenderers.Count, Is.GreaterThan(0));
            Assert.That(deck.GetComponentsInChildren<Animator>(true), Is.Empty, "nothing on the deck animates");
            Assert.That(deck.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b != deck), Is.Empty, "no script runs per frame on a deck object");
        }
    }
}
