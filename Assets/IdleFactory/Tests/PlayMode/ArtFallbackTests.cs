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
    /// FR-024, rewritten by T476 (owner decision 2026-11-05, option D: the Feature 003 placeholder sprites are deleted). There is no
    /// placeholder to show when art cannot be used. A key whose frames fail validation makes the catalog fail closed with
    /// <c>presentation.key.unresolved</c> (start-up then reports <c>presentation.catalog.invalid</c>, so the shipped game never runs on
    /// such a catalog). A view that is nevertheless configured with a failed catalog draws nothing, throws nothing and keeps every slot
    /// tappable; a corrected catalog replaces it and the art lands on the footprint anchor. A development or unit-test catalog may be smaller
    /// than the shipped one: its missing keys draw nothing and the slots stay tappable.
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

        // Valid tight development sprites (the legacy shape of the registry rows: 2 x 2 cell plate, 72 x 32 px badge at 64 PPU).
        private Sprite PlateTight() => Make("tight-plate", 2f, 2f, 0f, 64f);
        private Sprite BadgeTight() => Make("tight-badge", 72f / 64f, 0.5f, 0.5f, 64f);

        // Frame sets that fail their rows: a 3 x 3 plate and a 2 x 2 badge.
        private Sprite PlateWrongSize() => Make("broken-plate", 3f, 3f, 0f, 24f);
        private Sprite BadgeWrongSize() => Make("broken-badge", 2f, 2f, 0f, 24f);

        private PresentationSpriteCatalog FailedCatalog()
        {
            var catalog = new PresentationSpriteCatalog();
            LogAssert.Expect(LogType.Warning, "presentation.key.unresolved: " + SlotGridModel.SlotOccupiedKey);
            catalog.Configure(new[]
            {
                new PresentationSpriteEntry(SlotGridModel.SlotOccupiedKey, PlateWrongSize()),
                new PresentationSpriteEntry(SlotGridModel.BadgeWaitingKey, BadgeWrongSize()),
            });
            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.Error, Does.StartWith("presentation.key.unresolved"));
            Assert.That(catalog.Diagnostics, Is.EqualTo(new[] { "presentation.key.unresolved: " + SlotGridModel.SlotOccupiedKey }));
            return catalog;
        }

        private PresentationSpriteCatalog TightCatalog()
        {
            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(new[]
            {
                new PresentationSpriteEntry(SlotGridModel.SlotOccupiedKey, PlateTight()),
                new PresentationSpriteEntry(SlotGridModel.BadgeWaitingKey, BadgeTight()),
            });
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            Assert.That(catalog.Diagnostics, Is.Empty);
            return catalog;
        }

        private SlotGridView NewView(PresentationSpriteCatalog catalog)
        {
            host = new GameObject("ArtFallbackHost");
            var view = host.AddComponent<SlotGridView>();
            view.Configure(catalog);
            return view;
        }

        private static SlotVisual OccupiedWaiting(int index) =>
            new SlotVisual(
                index, SlotCondition.Occupied, SlotGridModel.SlotOccupiedKey, null, SlotGridModel.BadgeWaitingKey, "WAIT",
                false, false, false, 0, "Slot " + (index + 1));

        private static SlotVisual[] AllOccupied() =>
            Enumerable.Range(0, SlotGridLayout.SlotCount).Select(OccupiedWaiting).ToArray();

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
            // T456: a tight 2 x 2 cell plate has its bottom-left corner 1 cell left of and 15 art px below the slot pivot, so it is centred on
            // the pivot exactly as before the lattice; the footprint is the lattice diamond.
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
        public void AViewConfiguredWithAFailedCatalogDrawsNothingAndEverySlotStaysTappable([Values(3, 4)] int columns)
        {
            var view = NewView(FailedCatalog());
            view.SetColumns(columns);
            view.Render(AllOccupied());

            Assert.That(view.ActiveRendererCount, Is.EqualTo(0), "a failed catalog resolves no key: nothing is substituted and nothing half-valid is drawn");
            for (var i = 0; i < SlotGridLayout.SlotCount; i++)
            {
                var rect = view.WorldRectOf(i);
                Assert.That(view.TryHitSlot(rect.center, out var hit), Is.True, "slot " + (i + 1));
                Assert.That(hit, Is.EqualTo(i));
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
            view.Render(new[] { OccupiedWaiting(0) });

            Assert.That(view.ActiveRendererCount, Is.EqualTo(0));
            Assert.That(view.TryHitSlot(view.WorldRectOf(0).center, out var hit), Is.True);
            Assert.That(hit, Is.EqualTo(0));
        }

        [Category("StandalonePlayer")]
        [Test]
        public void CuesStayAnchoredToTheFootprintOnTightDevelopmentArt()
        {
            var view = NewView(TightCatalog());
            view.Render(AllOccupied());

            AssertAnchoredToTheFootprint(view, 0);
            AssertAnchoredToTheFootprint(view, 5);
        }

        [Category("StandalonePlayer")]
        [UnityTest]
        public IEnumerator ACorrectedCatalogReplacesAFailedOneAndTheArtLandsOnTheFootprintAnchor()
        {
            var view = NewView(FailedCatalog());
            view.Render(AllOccupied());
            Assert.That(view.ActiveRendererCount, Is.EqualTo(0));

            // The corrected art arrives (a valid catalog is configured): same view, same visuals.
            view.Configure(TightCatalog());
            yield return null; // the replaced slot objects are destroyed at the end of the frame
            view.Render(AllOccupied());

            Assert.That(view.ActiveRendererCount, Is.GreaterThan(0));
            Assert.That(host.GetComponent<SlotGridView>().RendererOf(7, SlotGridView.SlotRendererRole.Plate).sprite.pixelsPerUnit, Is.EqualTo(64f));
            AssertAnchoredToTheFootprint(view, 7);
        }
    }
}
