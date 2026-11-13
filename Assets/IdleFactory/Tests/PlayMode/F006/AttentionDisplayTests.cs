#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T532 (Feature 006, FR-026, FR-028, FR-030): the production attention markers in the world. A real run session on the shipped Iron content drives
    /// the <see cref="AttentionModel"/>; <see cref="AttentionMarkerView"/> draws its markers on the overlay layer. The art is stand-in sprites: the code
    /// under test is the model-to-view path, the layer, the position from the layout, the icon per marker, the touch target and the absence of text.
    /// </summary>
    public sealed class AttentionDisplayTests
    {
        private const string Miner = "machine.miner";
        private const string Smelter = "machine.smelter";
        private const string Press = "machine.press";
        private const string Assembler = "machine.assembler";

        private readonly List<Object> owned = new List<Object>();
        private RunCatalog catalog;
        private RunSession session;
        private RunCommands commands;
        private FactoryWorldLayout layout;
        private SlotGridView grid;
        private AttentionMarkerView view;
        private AttentionModel model;
        private PresentationSpriteCatalog sprites;
        private readonly Dictionary<string, Sprite> iconByKey = new Dictionary<string, Sprite>();

        [SetUp]
        public void SetUp()
        {
            catalog = RunShellFixtures.ShippedCatalog;
            layout = LatticeLayoutBuilder.Shipped(WorldOrientation.Portrait);
            model = new AttentionModel();
            sprites = StandInCatalog();
            var host = new GameObject("attention-display");
            owned.Add(host);
            grid = host.AddComponent<SlotGridView>();
            grid.Configure(sprites);
            grid.SetLayout(layout);
            grid.SetProjectedPixelsPerCell(48f);
            view = host.AddComponent<AttentionMarkerView>();
            view.Configure(sprites, id => catalog.Resources[id].PresentationKey);
            view.SetProjectedPixelsPerCell(48f);
            NewSession(50_000_000);
        }

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
            iconByKey.Clear();
        }

        // ---------------------------------------------------------------------------------------------------
        // Fixtures
        // ---------------------------------------------------------------------------------------------------

        private void NewSession(long cash)
        {
            var permanent = new PermanentState(catalog);
            var start = NextRunStart.Compute(catalog, permanent);
            var unlocked = new List<string>(catalog.RunStart.InitialUnlocks);
            unlocked.AddRange(catalog.Machines.Keys);
            unlocked.AddRange(catalog.Resources.Keys);
            var startMs = new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            var state = new RunState(catalog, Guid.NewGuid(), 1, null, cash, 0, start.StorageLevel, 5, unlocked.Distinct());
            Assert.That(new RunSimulator(catalog).Advance(state, permanent, 0, startMs, null), Is.EqualTo(RunAdvanceResult.Ok));
            session = new RunSession(catalog, state, permanent, startMs);
            commands = new RunCommands(session);
        }

        private void Build(int slot, string machine)
        {
            var result = commands.Build(slot, machine);
            Assert.That(result.IsSuccess, Is.True, machine + " at " + slot + ": " + result.ReasonCode);
        }

        private Sprite Icon(string key, Color tone)
        {
            // A 32 x 24 resource icon with an asymmetric mark, so a swapped or flipped icon is noticed.
            var texture = new Texture2D(32, 24, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, name = "icon-" + key };
            var pixels = new Color32[32 * 24];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = tone;
            }

            for (var y = 0; y < 10; y++)
            {
                pixels[y * 32 + 3] = Color.black;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 24), new Vector2(0.5f, 0.5f), 24f, 0, SpriteMeshType.FullRect);
            sprite.name = "icon-" + key;
            owned.Add(texture);
            owned.Add(sprite);
            iconByKey[key] = sprite;
            return sprite;
        }

        private PresentationSpriteCatalog StandInCatalog()
        {
            var entries = new List<PresentationSpriteEntry>();
            var requirements = new List<PresentationSpriteRequirement>();
            var tones = new[] { Color.red, Color.green, Color.blue, Color.yellow, Color.magenta };
            var keys = new[] { "F003-RES-IRON-ORE", "F003-RES-IRON-BAR", "F003-RES-IRON-PLATE", "F003-RES-GEAR", SlotGridModel.BadgeStorageFullKey };
            for (var i = 0; i < keys.Length; i++)
            {
                entries.Add(new PresentationSpriteEntry(keys[i], new[] { Icon(keys[i], tones[i]) }, null));
                requirements.Add(new PresentationSpriteRequirement(keys[i], 32f / 24f, 1f, 0.5f, 0.5f, 1, 1, false));
            }

            var result = new PresentationSpriteCatalog();
            result.Configure(entries, requirements);
            Assert.That(result.IsValid, Is.True, result.Error);
            return result;
        }

        private void Show()
        {
            model.Refresh(layout, RunSnapshots.Rates(session));
            view.Render(model);
        }

        private static Rect Bounds(SpriteRenderer renderer)
        {
            var b = renderer.bounds;
            return new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
        }

        // ---------------------------------------------------------------------------------------------------
        // Tests
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ThreeSimultaneousShortagesDrawTwoMarkersOnTheOverlayLayerWithTheirResourceIcons()
        {
            Build(0, Smelter); // short of Ore: coverage 0
            Build(4, Press);   // short of Bars
            Build(8, Assembler); // short of Plates
            yield return null;
            Show();

            Assert.That(model.ShortageCount, Is.EqualTo(3));
            Assert.That(view.VisibleMarkerCount, Is.EqualTo(2), "at most two markers are visible");
            var overlay = SortingLayer.NameToID(SlotGridView.OverlaySortingLayer);
            Assert.That(SortingLayer.IsValid(overlay), Is.True, "the Overlay sorting layer exists");
            for (var i = 0; i < view.VisibleMarkerCount; i++)
            {
                var marker = model.GetMarker(i);
                var renderer = view.RendererOf(i);
                Assert.That(renderer.enabled, Is.True);
                Assert.That(renderer.sortingLayerID, Is.EqualTo(overlay), "world markers sit on the Overlay layer");
                Assert.That(renderer.sprite, Is.SameAs(iconByKey[catalog.Resources[marker.ResourceId].PresentationKey]), "marker " + i + " shows its resource icon");
                Assert.That(renderer.transform.position.x, Is.EqualTo(marker.Position.x).Within(1e-4f));
                Assert.That(renderer.transform.position.y, Is.EqualTo(marker.Position.y).Within(1e-4f));
            }

            Assert.That(view.RendererOf(2).enabled, Is.False, "the third renderer is idle");
            Assert.That(view.RendererOf(0).sprite, Is.Not.SameAs(view.RendererOf(1).sprite), "two different resources show two different icons");
            Assert.That(model.Strip.MoreCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator MarkersDrawAboveEveryMachineBadgeAndSlotOverlay()
        {
            Build(0, Smelter);
            Build(4, Press);
            yield return null;
            Show();

            var highestSlotOverlay = SlotGridView.OverlayOrder + 4 * layout.RowCount + 3;
            for (var i = 0; i < view.VisibleMarkerCount; i++)
            {
                Assert.That(view.RendererOf(i).sortingOrder, Is.GreaterThan(highestSlotOverlay), "no slot overlay draws over a marker");
            }
        }

        [UnityTest]
        public IEnumerator AMarkerBoxNeverCoversAMachineABadgeOrAKeepOut()
        {
            Build(0, Smelter);
            Build(1, Press);
            Build(2, Assembler);
            yield return null;
            Show();

            Assert.That(view.VisibleMarkerCount, Is.EqualTo(2));
            for (var m = 0; m < view.VisibleMarkerCount; m++)
            {
                var box = Bounds(view.RendererOf(m));
                var allowed = AttentionModel.MarkerBoxAt(model.GetMarker(m).Position);
                Assert.That(box.xMin, Is.GreaterThanOrEqualTo(allowed.xMin - 1e-3f));
                Assert.That(box.xMax, Is.LessThanOrEqualTo(allowed.xMax + 1e-3f));
                Assert.That(box.yMin, Is.GreaterThanOrEqualTo(allowed.yMin - 1e-3f));
                Assert.That(box.yMax, Is.LessThanOrEqualTo(allowed.yMax + 1e-3f), "the icon fits its marker box");
                for (var i = 0; i < layout.LocationCount; i++)
                {
                    var id = new BuildingLocationId(i);
                    Assert.That(FactoryWorldLayout.Overlaps(box, layout.FootprintOf(id)), Is.False, "marker " + m + " covers the machine at " + id);
                    Assert.That(FactoryWorldLayout.Overlaps(box, layout.LocationCellRectOf(id)), Is.False, "marker " + m + " covers the cell of " + id);
                    for (var k = 0; k < FactoryWorldLayout.KeepOutCount; k++)
                    {
                        Assert.That(FactoryWorldLayout.Overlaps(box, layout.KeepOutOf(id, k)), Is.False, "marker " + m + " covers keep-out " + k + " of " + id);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator NoWorldSpaceTextShowsANumber()
        {
            Build(0, Smelter);
            Build(4, Press);
            Build(8, Assembler);
            yield return null;
            Show();
            yield return null;

            // The marker view is sprites only: no text component of any kind sits in its hierarchy.
            for (var i = 0; i < view.transform.childCount; i++)
            {
                var child = view.transform.GetChild(i);
                if (!child.name.StartsWith("AttentionMarker", StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.That(child.childCount, Is.Zero, "a marker has no child objects");
                foreach (var component in child.GetComponents<Component>())
                {
                    Assert.That(component is Transform || component is SpriteRenderer, Is.True, "unexpected " + component.GetType().Name + " on a marker");
                }
            }

            // And no text element anywhere in the world (a TextMesh or TextMeshPro object) shows a digit (FR-030).
            foreach (var root in owned.OfType<GameObject>())
            {
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    var type = component.GetType();
                    if (type.Name != "TextMesh" && type.Name != "TextMeshPro")
                    {
                        continue;
                    }

                    var text = (string)type.GetProperty("text").GetValue(component);
                    Assert.That(text.Any(char.IsDigit), Is.False, "a world-space text element shows a number: '" + text + "'");
                }
            }

            foreach (var mesh in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            {
                Assert.That(mesh.text.Any(char.IsDigit), Is.False, "a world-space TextMesh shows a number: '" + mesh.text + "'");
            }
        }

        [UnityTest]
        public IEnumerator AStoreFullStateDrawsTheDepotMarkerWithTheStorageFullIcon()
        {
            Build(2, Miner);
            session.State.Inventory.TryImport(new[] { new ResourceAmount("iron.ore", session.State.Inventory.Free - 1) });
            yield return null;
            Show();

            Assert.That(model.StoreFull, Is.True);
            Assert.That(view.VisibleMarkerCount, Is.EqualTo(1));
            var renderer = view.RendererOf(0);
            Assert.That(renderer.sprite, Is.SameAs(iconByKey[SlotGridModel.BadgeStorageFullKey]));
            Assert.That(renderer.transform.position.x, Is.EqualTo(layout.DepotAnchor.x).Within(1e-4f));
            Assert.That(renderer.transform.position.y, Is.EqualTo(layout.DepotAnchor.y).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator AMarkerDisappearsWhenTheShortageIsResolved()
        {
            Build(0, Smelter);
            yield return null;
            Show();
            Assert.That(view.VisibleMarkerCount, Is.EqualTo(1));

            session.State.Inventory.TryImport(new[] { new ResourceAmount("iron.ore", 10) });
            Assert.That(session.Advance(10_000).IsSuccess, Is.True, "an advance publishes the new stock to the read models");
            Show();
            Assert.That(model.ShortageCount, Is.Zero);
            Assert.That(view.VisibleMarkerCount, Is.Zero);
            Assert.That(view.RendererOf(0).enabled, Is.False);
            Assert.That(view.RendererOf(0).sprite, Is.Null);
        }

        [UnityTest]
        public IEnumerator ATapOnAMarkerSelectsTheFirstAffectedLocationAndAMissOutsideTheHitAreaSelectsNothing()
        {
            Build(5, Smelter);
            Build(2, Press);
            yield return null;
            Show();

            Assert.That(view.VisibleMarkerCount, Is.EqualTo(2));
            for (var i = 0; i < view.VisibleMarkerCount; i++)
            {
                var marker = model.GetMarker(i);
                Assert.That(view.TryHit(marker.Position, out var target), Is.True);
                Assert.That(target, Is.EqualTo(marker.FirstAffected), "a tap selects the first affected location");
                Assert.That(view.TryHit(marker.Position + new Vector2(0f, view.HitRadiusWorld * 0.9f), out var edge), Is.True, "inside the hit radius");
                Assert.That(edge, Is.EqualTo(marker.FirstAffected));
                Assert.That(view.TryHit(marker.Position + new Vector2(view.HitRadiusWorld * 3f, 0f), out _), Is.False, "outside the hit radius");
            }
        }

        [Test]
        public void TheTouchTargetIsAtLeastFortyEightPointsAtEveryScale()
        {
            foreach (var pointsPerCell in new[] { 24f, 32f, 48f, 64f, 96f, 128f })
            {
                view.SetProjectedPixelsPerCell(pointsPerCell);
                Assert.That(view.HitRadiusWorld * 2f * pointsPerCell, Is.GreaterThanOrEqualTo(AttentionMarkerView.MinimumHitDiameterPoints - 1e-3f), "scale " + pointsPerCell);
            }
        }

        [UnityTest]
        public IEnumerator AMarkerIsStaticSoReducedMotionNeedsNoFallback()
        {
            Build(0, Smelter);
            Build(4, Press);
            yield return null;
            Show();

            var before = Enumerable.Range(0, view.VisibleMarkerCount)
                .Select(i => (view.RendererOf(i).sprite, view.RendererOf(i).transform.position, view.RendererOf(i).transform.localScale)).ToArray();
            for (var frame = 0; frame < 120; frame++)
            {
                yield return null;
            }

            for (var i = 0; i < before.Length; i++)
            {
                Assert.That(view.RendererOf(i).sprite, Is.SameAs(before[i].sprite), "no frame loop");
                Assert.That(view.RendererOf(i).transform.position, Is.EqualTo(before[i].position), "no motion");
                Assert.That(view.RendererOf(i).transform.localScale, Is.EqualTo(before[i].localScale), "no pulse");
            }

            Assert.That(view.GetComponentsInChildren<Animator>(true), Is.Empty);
        }

        [UnityTest]
        public IEnumerator ASteadyStateRenderAllocatesNothing()
        {
            Build(0, Smelter);
            Build(4, Press);
            yield return null;
            Show();
            Show();

            var recorder = UnityEngine.Profiling.Recorder.Get("GC.Alloc");
            recorder.enabled = false;
            recorder.FilterToCurrentThread();
            recorder.enabled = true;
            for (var i = 0; i < 50; i++)
            {
                view.Render(model);
                view.TryHit(Vector2.zero, out _);
            }

            recorder.enabled = false;
            recorder.CollectFromAllThreads();
            Assert.That(recorder.sampleBlockCount, Is.Zero, "rendering the same model again allocates nothing");
        }
    }
}
#endif
