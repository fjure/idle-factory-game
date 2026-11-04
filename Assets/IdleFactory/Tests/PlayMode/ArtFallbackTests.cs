using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T428 / FR-024: when the primary F004 art of a key fails validation the slot grid keeps working on the retained
    /// F003 fallback frames. The slot stays tappable, the state cues stay anchored to the slot footprint, and art that
    /// loads late swaps in without a layout shift because pivot and world size come from the catalog entry.
    /// </summary>
    public sealed class ArtFallbackTests
    {
        private const float Epsilon = 0.001f;

        private readonly List<Object> created = new List<Object>();
        private GameObject host;

        [TearDown]
        public void Cleanup()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }

            foreach (var asset in created)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }

            created.Clear();
        }

        private Sprite Make(string name, float worldWidth, float worldHeight, float pivot, float pixelsPerUnit)
        {
            var width = Mathf.RoundToInt(worldWidth * pixelsPerUnit);
            var height = Mathf.RoundToInt(worldHeight * pixelsPerUnit);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(pivot, pivot), pixelsPerUnit);
            sprite.name = name;
            created.Add(texture);
            created.Add(sprite);
            return sprite;
        }

        // Fallback (F003 placeholder, 64 PPU) and valid primary (F004, D = PPU = 24) frames for the keys the test draws.
        private Sprite PlateFallback() => Make("f003-plate", 2f, 2f, 0f, 64f);
        private Sprite PlatePrimary() => Make("f004-plate", 2f, 2f, 0f, 24f);
        private Sprite BadgeFallback() => Make("f003-badge", 72f / 64f, 0.5f, 0.5f, 64f);
        private Sprite BadgePrimary() => Make("f004-badge", 72f / 64f, 0.5f, 0.5f, 24f);

        private PresentationSpriteCatalog BrokenPrimaryCatalog()
        {
            var catalog = new PresentationSpriteCatalog();
            LogAssert.Expect(LogType.Warning, "presentation.art.fallback: " + SlotGridModel.SlotOccupiedKey);
            LogAssert.Expect(LogType.Warning, "presentation.art.fallback: " + SlotGridModel.BadgeWaitingKey);
            catalog.Configure(new[]
            {
                new PresentationSpriteEntry(SlotGridModel.SlotOccupiedKey, Make("broken-plate", 3f, 3f, 0f, 24f)).WithFallback(PlateFallback()),
                new PresentationSpriteEntry(SlotGridModel.BadgeWaitingKey, Make("broken-badge", 2f, 2f, 0f, 24f)).WithFallback(BadgeFallback()),
            });
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            return catalog;
        }

        private PresentationSpriteCatalog PrimaryCatalog()
        {
            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(new[]
            {
                new PresentationSpriteEntry(SlotGridModel.SlotOccupiedKey, PlatePrimary()).WithFallback(PlateFallback()),
                new PresentationSpriteEntry(SlotGridModel.BadgeWaitingKey, BadgePrimary()).WithFallback(BadgeFallback()),
            });
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            Assert.That(catalog.FallbackKeys, Is.Empty);
            return catalog;
        }

        private SlotGridView NewView(PresentationSpriteCatalog catalog)
        {
            host = new GameObject("ArtFallbackHost");
            var view = host.AddComponent<SlotGridView>();
            view.Configure(catalog);
            return view;
        }

        private static SlotVisual OccupiedRunning(int index) =>
            new SlotVisual(
                index, SlotCondition.Occupied, SlotGridModel.SlotOccupiedKey, null, SlotGridModel.BadgeWaitingKey, "WAIT",
                false, false, false, 0, "Slot " + (index + 1));

        private static SlotVisual[] AllOccupied() =>
            Enumerable.Range(0, SlotGridLayout.SlotCount).Select(OccupiedRunning).ToArray();

        private Bounds BoundsOf(string child, int slotIndex = 0)
        {
            var view = host.GetComponent<SlotGridView>();
            var role = child == "Plate" ? SlotGridView.SlotRendererRole.Plate : SlotGridView.SlotRendererRole.Badge;
            var renderer = view.RendererOf(slotIndex, role);
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.enabled, Is.True, child + " must draw");
            return renderer.bounds;
        }

        private void AssertAnchoredToTheFootprint(SlotGridView view, int slotIndex)
        {
            // T456: the fallback plate is the F003 2 x 2 cell square whose bottom-left corner sits 1 cell left of and 15 art px below the
            // slot pivot, so it is centred on the pivot exactly as before the lattice; the footprint is the lattice diamond.
            var footprint = view.WorldRectOf(slotIndex);
            var pivot = view.FootprintPivotOf(slotIndex);
            var plate = BoundsOf("Plate", slotIndex);
            Assert.That(plate.min.x, Is.EqualTo(pivot.x - 1f).Within(Epsilon));
            Assert.That(plate.min.y, Is.EqualTo(pivot.y - 15f / 24f).Within(Epsilon));
            Assert.That(plate.size.x, Is.EqualTo(2f).Within(Epsilon));
            Assert.That(plate.size.y, Is.EqualTo(2f).Within(Epsilon));

            var badge = BoundsOf("Badge", slotIndex);
            // T429: the badge rests bottom-centre on the Art Bible anchor, relative to the footprint pivot (D = 24).
            var anchor = pivot + StateCueGeometry.BadgeAnchorOffset;
            Assert.That(badge.center.x, Is.EqualTo(anchor.x).Within(Epsilon), "the badge keeps its anchor on the footprint pivot");
            Assert.That(badge.min.y, Is.EqualTo(anchor.y).Within(Epsilon));
            Assert.That(footprint.center.x, Is.EqualTo(pivot.x).Within(Epsilon));
        }

        [Category("StandalonePlayer")]
        [Test]
        public void ASlotOnFallbackArtStaysTappableOnEveryColumnCount([Values(3, 4)] int columns)
        {
            var view = NewView(BrokenPrimaryCatalog());
            view.SetColumns(columns);
            view.Render(AllOccupied());

            for (var i = 0; i < SlotGridLayout.SlotCount; i++)
            {
                var rect = view.WorldRectOf(i);
                Assert.That(view.TryHitSlot(rect.center, out var hit), Is.True, "slot " + (i + 1));
                Assert.That(hit, Is.EqualTo(i));
                Assert.That(BoundsOf("Plate", i).Contains(new Vector3(rect.center.x, rect.center.y, 0f)), Is.True,
                    "the fallback plate covers the tap point of slot " + (i + 1));
            }
        }

        [Category("StandalonePlayer")]
        [Test]
        public void ASlotWithoutAnyArtForItsKeysIsStillTappableAndDrawsNothing()
        {
            var empty = new PresentationSpriteCatalog();
            empty.Configure(new PresentationSpriteEntry[0]);
            Assert.That(empty.IsValid, Is.True, empty.Error);
            var view = NewView(empty);
            view.Render(new[] { OccupiedRunning(0) });

            Assert.That(view.ActiveRendererCount, Is.EqualTo(0));
            Assert.That(view.TryHitSlot(view.WorldRectOf(0).center, out var hit), Is.True);
            Assert.That(hit, Is.EqualTo(0));
        }

        [Category("StandalonePlayer")]
        [Test]
        public void CuesStayAnchoredToTheFootprintOnFallbackArt()
        {
            var catalog = BrokenPrimaryCatalog();
            var view = NewView(catalog);
            view.Render(AllOccupied());

            Assert.That(catalog.IsFallback(SlotGridModel.SlotOccupiedKey), Is.True);
            Assert.That(catalog.IsFallback(SlotGridModel.BadgeWaitingKey), Is.True);
            AssertAnchoredToTheFootprint(view, 0);
            AssertAnchoredToTheFootprint(view, 5);
        }

        [Category("StandalonePlayer")]
        [UnityTest]
        public IEnumerator ArtThatLoadsLateSwapsInWithoutALayoutShift()
        {
            var view = NewView(BrokenPrimaryCatalog());
            view.Render(AllOccupied());
            var plateBefore = BoundsOf("Plate", 7);
            var badgeBefore = BoundsOf("Badge", 7);
            var spriteBefore = host.GetComponent<SlotGridView>().RendererOf(7, SlotGridView.SlotRendererRole.Plate).sprite;

            // The primary art arrives (a corrected catalog is configured): same view, same visuals.
            var primary = PrimaryCatalog();
            view.Configure(primary);
            yield return null; // the replaced slot objects are destroyed at the end of the frame
            view.Render(AllOccupied());

            var spriteAfter = host.GetComponent<SlotGridView>().RendererOf(7, SlotGridView.SlotRendererRole.Plate).sprite;
            Assert.That(spriteAfter, Is.Not.SameAs(spriteBefore), "the primary sprite replaced the fallback");
            Assert.That(spriteAfter.pixelsPerUnit, Is.EqualTo(24f));

            var plateAfter = BoundsOf("Plate", 7);
            var badgeAfter = BoundsOf("Badge", 7);
            Assert.That(plateAfter.min.x, Is.EqualTo(plateBefore.min.x).Within(Epsilon));
            Assert.That(plateAfter.min.y, Is.EqualTo(plateBefore.min.y).Within(Epsilon));
            Assert.That(plateAfter.size.x, Is.EqualTo(plateBefore.size.x).Within(Epsilon));
            Assert.That(plateAfter.size.y, Is.EqualTo(plateBefore.size.y).Within(Epsilon));
            Assert.That(badgeAfter.center.x, Is.EqualTo(badgeBefore.center.x).Within(Epsilon));
            Assert.That(badgeAfter.center.y, Is.EqualTo(badgeBefore.center.y).Within(Epsilon));
            Assert.That(badgeAfter.size.x, Is.EqualTo(badgeBefore.size.x).Within(Epsilon));
            Assert.That(badgeAfter.size.y, Is.EqualTo(badgeBefore.size.y).Within(Epsilon));
            AssertAnchoredToTheFootprint(view, 7);
        }
    }
}
