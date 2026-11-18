using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T528 (C-11, Art Director T527 review item 3, Art Bible 9.4, 21.2, 21.8 item 1): a branch pip (<c>copper_2</c> with its <c>ink_0</c> ring) passes the C8 ring
    /// rule where it passes a K4 badge tail. The composite proofs of T527 draw no pips, so this walks the real pip frames along every branch of every test layout,
    /// at every whole-pixel step the view can place them on, and checks the pixels against the rings and masks of the densest frame (a badge with its K4 tail
    /// and both markers on every location). It uses the ring helpers of <see cref="DeckCompositeProofTests"/> and <see cref="DeckComposite"/>.
    /// </summary>
    public sealed class BranchPipRingTests
    {
        private const int PipFrames = 6;
        private static readonly string[] PipTones = { "copper_2", "ink_0" };

        private static IEnumerable<string> Names => DeckTestSupport.LayoutNames;

        [Test]
        public void ThePipArtUsesOnlyCopperTwoAndItsInkRingAndBothAreRingSafe()
        {
            var tones = new SortedSet<string>();
            for (var frame = 0; frame < PipFrames; frame++)
            {
                var pip = DeckTestSupport.PixelsOfKey(DepotLinkGeometry.PipKey, frame);
                Assert.That(pip.Width, Is.EqualTo(14));
                Assert.That(pip.Height, Is.EqualTo(10));
                foreach (var colour in pip.Pixels.Where(p => p.a > 0))
                {
                    tones.Add(ShippedArt.ToneOf(colour));
                }
            }

            Assert.That(tones, Is.EquivalentTo(PipTones), "the pip is copper_2 with an ink_0 ring, nothing else (Art Bible 21.2 FX row)");
            foreach (var tone in tones)
            {
                Assert.That(DeckCompositeProofTests.InRingSafeSet(tone), Is.True, tone + " is in the ring-safe set R");
            }
        }

        [TestCaseSource(nameof(Names))]
        public void APipPassingTheK4TailStaysInSetROnEveryBadgeRingAtEveryStepAndFrame(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var plan = DeckPlanner.Build(layout, DeckTestSupport.All(layout.LocationCount, SlotCondition.Occupied), 0, DeckTestSupport.Settings());
            var composite = new DeckComposite(layout, plan, "F006-DEPOT-S1-FULL", "F006-DEPOSIT-REST-S1", true);
            var linkPlan = DepotLinkGeometry.Build(layout);

            var badges = new List<HashSet<Vector2Int>>();
            var markers = new List<HashSet<Vector2Int>>();
            var all = new HashSet<Vector2Int>();
            var badgePixels = new HashSet<Vector2Int>();
            for (var i = 0; i < layout.LocationCount; i++)
            {
                badges.Add(composite.OverlayMask(DeckComposite.BadgeKeyOf(i), i));
                badgePixels.UnionWith(badges[i]);
                all.UnionWith(badges[i]);
                markers.Add(composite.OverlayMask("F003-MARKER-BOOSTED", i));
                markers.Add(composite.OverlayMask("F003-MARKER-OVERDRIVE", i));
                all.UnionWith(markers[markers.Count - 2]);
                all.UnionWith(markers[markers.Count - 1]);
            }

            var rings = new HashSet<Vector2Int>();
            foreach (var mask in badges.Concat(markers))
            {
                rings.UnionWith(DeckComposite.Ring(mask));
            }

            rings.ExceptWith(all);

            var pipPixels = new List<(Vector2Int Offset, string Tone)>[PipFrames];
            for (var frame = 0; frame < PipFrames; frame++)
            {
                var art = DeckTestSupport.PixelsOfKey(DepotLinkGeometry.PipKey, frame);
                pipPixels[frame] = new List<(Vector2Int, string)>();
                for (var y = 0; y < art.Height; y++)
                {
                    for (var x = 0; x < art.Width; x++)
                    {
                        if (art.IsOpaque(x, y))
                        {
                            pipPixels[frame].Add((new Vector2Int(x - 7, y - 5), ShippedArt.ToneOf(art.Pixels[y * art.Width + x])));
                        }
                    }
                }
            }

            var ringHits = 0;
            var maskHits = 0;
            var badgeHits = 0;
            var ringHitsOnLane = 0;
            var positions = 0;
            var tones = new SortedDictionary<string, int>();
            for (var location = 0; location < layout.LocationCount; location++)
            {
                for (var y = linkPlan.PipStartY(location); y <= linkPlan.PipEndY(location); y++)
                {
                    for (var frame = 0; frame < PipFrames; frame++)
                    {
                        positions++;
                        foreach (var (offset, tone) in pipPixels[frame])
                        {
                            var p = new Vector2Int(linkPlan.PipX(location) + offset.x, y + offset.y);
                            if (rings.Contains(p))
                            {
                                ringHits++;
                                tones[tone] = tones.TryGetValue(tone, out var n) ? n + 1 : 1;
                                Assert.That(PipTones, Does.Contain(tone), name + " location " + location + " step " + y + " frame " + frame + ": pip pixel " + p + " in a ring has tone " + tone);
                                Assert.That(DeckCompositeProofTests.InRingSafeSet(tone), Is.True, name + ": pip tone " + tone + " in a badge ring at " + p);
                                ringHitsOnLane += composite.TopAt(p.x, p.y) == DeckLayer.Link ? 1 : 0;
                            }

                            if (all.Contains(p))
                            {
                                maskHits++;
                            }

                            if (badgePixels.Contains(p))
                            {
                                badgeHits++;
                            }
                        }
                    }
                }
            }

            Assert.That(positions, Is.GreaterThan(0));
            Debug.Log("[T528 C-11] " + name + ": " + positions + " pip positions x frames; pip pixels inside a badge ring " + ringHits + " (tones "
                + string.Join(", ", tones.Select(kv => kv.Key + " " + kv.Value)) + ", of which over lane art " + ringHitsOnLane + "); pip pixels under a cue pixel (the pip draws below every cue: the Overdrive marker sits on the branch) " + maskHits + ", of which under a badge or its K4 tail " + badgeHits);
            Assert.That(badgeHits, Is.Zero, name + ": a pip never reaches a badge or its K4 tail; where it meets a marker it passes under it, and the cue layer draws above it");
        }
    }
}
