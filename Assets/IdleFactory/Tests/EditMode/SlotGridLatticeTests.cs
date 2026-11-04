using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T456: the pure mapping of the isometric 2:1 slot lattice (Art Bible v1.0 sections 3.2a, 3.4 and 3.5, research R04, FR-015). No scene:
    /// positions, the plate and hit shapes, row sorting, and the proof that no overlay box of a slot can hide another slot's cue, marker or
    /// selection frame.
    /// </summary>
    public sealed class SlotGridLatticeTests
    {
        private const float Px = 1f / 24f;

        [Test]
        public void EveryLatticePositionIsAWholeNumberOfArtPixels([Values(3, 4)] int columns)
        {
            for (var i = 0; i < SlotGridLayout.SlotCount; i++)
            {
                var pivot = SlotGridLayout.PivotOf(i, columns) * 24f;
                Assert.That(pivot.x, Is.EqualTo(Mathf.Round(pivot.x)).Within(1e-3f), "x of slot " + (i + 1));
                Assert.That(pivot.y, Is.EqualTo(Mathf.Round(pivot.y)).Within(1e-3f), "y of slot " + (i + 1));
            }
        }

        [Test]
        public void TheExtentIsDerivedFromTheLatticeAndContainsEveryPlateAndBadgeBand([Values(3, 4)] int columns)
        {
            var extent = SlotGridLayout.Extent(columns);
            Assert.That(extent.min, Is.EqualTo(Vector2.zero));
            var rows = SlotGridLayout.RowsFor(columns);
            Assert.That(extent.width * 24f, Is.EqualTo((columns - 1) * 70f + 70f).Within(1e-3f));
            Assert.That(extent.height * 24f, Is.EqualTo((rows - 1) * 54f + 15f + 39f).Within(1e-3f));
            var topBadge = float.MinValue;
            for (var i = 0; i < SlotGridLayout.SlotCount; i++)
            {
                var plate = SlotGridLayout.SlotRect(i, columns);
                Assert.That(plate.xMin, Is.GreaterThanOrEqualTo(extent.xMin - 1e-4f));
                Assert.That(plate.xMax, Is.LessThanOrEqualTo(extent.xMax + 1e-4f));
                Assert.That(plate.yMin, Is.GreaterThanOrEqualTo(extent.yMin - 1e-4f));
                var badge = StateCueGeometry.BadgeBox();
                topBadge = Mathf.Max(topBadge, SlotGridLayout.PivotOf(i, columns).y + badge.yMax);
            }

            Assert.That(topBadge, Is.EqualTo(extent.yMax).Within(1e-4f), "the badge band of the back row closes the extent exactly");
        }

        [Test]
        public void RowsSortFrontLaterAndAFrontRowIsLowerOnTheScreen([Values(3, 4)] int columns)
        {
            for (var i = columns; i < SlotGridLayout.SlotCount; i++)
            {
                Assert.That(SlotGridLayout.RowOf(i, columns), Is.GreaterThan(SlotGridLayout.RowOf(i - columns, columns)));
                Assert.That(SlotGridLayout.PivotOf(i, columns).y, Is.LessThan(SlotGridLayout.PivotOf(i - columns, columns).y));
            }
        }

        private static bool Overlaps(Rect a, Rect b) => a.xMin < b.xMax && b.xMin < a.xMax && a.yMin < b.yMax && b.yMin < a.yMax;

        private static Rect At(Rect box, Vector2 pivot) => new Rect(box.position + pivot, box.size);

        // The plate diamond grown by `grow` art pixels (|dx| / (35 + 2 grow) + |dy| / (17.5 + grow) <= 1) around the plate centre.
        private static bool BoxTouchesDiamond(Rect box, Vector2 centre, float grow)
        {
            var nearest = new Vector2(Mathf.Clamp(centre.x, box.xMin, box.xMax), Mathf.Clamp(centre.y, box.yMin, box.yMax));
            var d = (nearest - centre) * 24f;
            return Mathf.Abs(d.x) / (35f + 2f * grow) + Mathf.Abs(d.y) / (17.5f + grow) < 1f;
        }

        private static bool BoxInsideDiamond(Rect box, Vector2 centre)
        {
            foreach (var corner in new[] { new Vector2(box.xMin, box.yMin), new Vector2(box.xMin, box.yMax), new Vector2(box.xMax, box.yMin), new Vector2(box.xMax, box.yMax) })
            {
                var d = (corner - centre) * 24f;
                if (Mathf.Abs(d.x) / 35f + Mathf.Abs(d.y) / 17.5f > 1f)
                {
                    return false;
                }
            }

            return true;
        }

        [Test]
        public void NoOverlayBoxOfASlotCanHideAnotherSlotsCueMarkerOrSelectionFrame([Values(3, 4)] int columns)
        {
            // FR-015 and Art Bible 3.5: overlay boxes relative to each pivot (badge 23 x 23, two markers 11 x 11). The selection frame is the
            // plate diamond plus a 3 px band; a badge may lie on a neighbour's plate interior (C1 allows plate tones) but never on its frame band.
            var boxes = new[] { StateCueGeometry.BadgeBox(), StateCueGeometry.MarkerBox(0), StateCueGeometry.MarkerBox(1) };
            var names = new[] { "badge", "marker 1", "marker 2" };
            for (var i = 0; i < SlotGridLayout.SlotCount; i++)
            {
                var pivotI = SlotGridLayout.PivotOf(i, columns);
                for (var j = 0; j < SlotGridLayout.SlotCount; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    var pivotJ = SlotGridLayout.PivotOf(j, columns);
                    var centreJ = SlotGridLayout.PlateCentreOf(j, columns);
                    for (var a = 0; a < boxes.Length; a++)
                    {
                        var boxA = At(boxes[a], pivotI);
                        for (var b = 0; b < boxes.Length; b++)
                        {
                            Assert.That(Overlaps(boxA, At(boxes[b], pivotJ)), Is.False, $"{names[a]} of slot {i + 1} overlaps {names[b]} of slot {j + 1}");
                        }

                        var outer = BoxTouchesDiamond(boxA, centreJ, 3f);
                        Assert.That(!outer || BoxInsideDiamond(boxA, centreJ), Is.True,
                            $"{names[a]} of slot {i + 1} touches the selection-frame band of slot {j + 1}");
                    }
                }
            }
        }

        [Test]
        public void TheProofHasTeethAPitchOf52ArtPixelsWouldHideTheFrameOfTheRowBehind()
        {
            // Negative control for the proof above: with the row pitch reduced to 52 px the badge box reaches the frame band behind.
            var badge = At(StateCueGeometry.BadgeBox(), Vector2.zero);
            var centreBehind54 = new Vector2(0f, (54f + 2.5f) * Px);
            var centreBehind52 = new Vector2(0f, (52f + 2.5f) * Px);
            Assert.That(BoxTouchesDiamond(badge, centreBehind54, 3f), Is.False, "pitch 54 keeps the frame band clear");
            Assert.That(BoxTouchesDiamond(badge, centreBehind52, 3f), Is.True, "pitch 52 touches the frame band");
        }

        [Test]
        public void HitTestingIsFootprintOnlyOnTheDiamond([Values(3, 4)] int columns)
        {
            for (var i = 0; i < SlotGridLayout.SlotCount; i++)
            {
                var centre = SlotGridLayout.PlateCentreOf(i, columns);
                Assert.That(SlotGridLayout.HitTest(centre, columns), Is.EqualTo(i));
                foreach (var apex in new[] { new Vector2(34f, 0f), new Vector2(-34f, 0f), new Vector2(0f, 17f), new Vector2(0f, -17f) })
                {
                    Assert.That(SlotGridLayout.HitTest(centre + apex * Px, columns), Is.EqualTo(i), $"slot {i + 1} apex {apex}");
                }

                Assert.That(SlotGridLayout.HitTest(centre + new Vector2(34f, 17f) * Px, columns), Is.Not.EqualTo(i), "the bounding-box corner is not footprint");
                // The badge band and the machine overhang above the plate never capture a tap for this slot.
                var pivot = SlotGridLayout.PivotOf(i, columns);
                Assert.That(SlotGridLayout.HitTest(pivot + new Vector2(19f, 28f) * Px, columns), Is.Not.EqualTo(i), "the badge centre is not a tap target");
            }
        }

        [Test]
        public void NoPointResolvesToTwoSlotsAndEveryHitLiesNearItsPlate([Values(3, 4)] int columns)
        {
            var extent = SlotGridLayout.Extent(columns);
            var hits = 0;
            for (var x = -12; x <= extent.width * 24f + 12; x++)
            {
                for (var y = -12; y <= extent.height * 24f + 12; y++)
                {
                    var world = new Vector2(x * Px, y * Px);
                    var hit = SlotGridLayout.HitTest(world, columns);
                    if (hit < 0)
                    {
                        continue;
                    }

                    hits++;
                    // The hit diamond is the plate grown to 90 x 45: |dx| / 45 + |dy| / 22.5 <= 1 around the plate centre.
                    var d = (world - SlotGridLayout.PlateCentreOf(hit, columns)) * 24f;
                    Assert.That(Mathf.Abs(d.x) / 45f + Mathf.Abs(d.y) / 22.5f, Is.LessThanOrEqualTo(1f + 1e-3f));
                    for (var other = 0; other < SlotGridLayout.SlotCount; other++)
                    {
                        if (other == hit)
                        {
                            continue;
                        }

                        var e = (world - SlotGridLayout.PlateCentreOf(other, columns)) * 24f;
                        Assert.That(Mathf.Abs(e.x) / 35f + Mathf.Abs(e.y) / 17.5f, Is.GreaterThanOrEqualTo(1f - 1e-3f), $"{x},{y} lies on the plate of slot {other + 1}, not only {hit + 1}");
                    }
                }
            }

            Assert.That(hits, Is.GreaterThan(12 * 1000));
        }

        [Test]
        public void FitAllIsDerivedFromTheLatticeExtent()
        {
            // 3 columns: (210 + 24) x (216 + 24) px at D = 24; 4 columns: (280 + 24) x (162 + 24) px.
            Assert.That(SlotGridLayout.FitAllPixelsPerCell(234f, 240f, 3), Is.EqualTo(24f).Within(1e-3f));
            Assert.That(SlotGridLayout.FitAllPixelsPerCell(304f, 186f, 4), Is.EqualTo(24f).Within(1e-3f));
        }
    }
}
