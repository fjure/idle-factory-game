using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T527 composite proofs on real pixels (Art Bible 21.5, 21.8, 21.9; OD-9b Art Director conditions 2, 3 and 6; P2-F6, P2-F7, P2-F23, A-13): the world
    /// layers are composed from the imported PNGs in sort order on the densest frame of every layout (every location occupied, a K4 badge and both markers
    /// on every location, one selection frame) and the 1 px C8 rings of the overlays are read from it. Nothing here is a mock: the layout is the real
    /// <see cref="FactoryWorldLayout"/>, the sprites are the shipped ones and the placement is the real <see cref="DeckPlanner"/>.
    /// Set <c>IDLEFACTORY_WRITE_PROOFS=1</c> to write the composite captures of the evidence note.
    /// </summary>
    public sealed class DeckCompositeProofTests
    {
        // Art Bible 9.4 rule C1 (A_ov), the frame backdrop rule C3 and the OD-9b ring-safe set R (A_ov without steel_*, teal_1, lit_* and the unused res_*).
        internal static readonly string[] AllowedOverlay =
        {
            "ink_0", "ink_1", "steel_1", "steel_2", "steel_3", "copper_1", "copper_2", "teal_1", "dim_0", "lit_0", "lit_1",
            "floor_0", "floor_1", "plate_1", "plate_2", "res_ore_1", "res_coal", "res_crystal", "res_leaf", "res_gold",
            "res_water", "res_plank", "ui_bg", "ui_panel", "ui_line", "ui_text",
        };

        private static readonly string[] FrameForbidden = { "warn_0", "plate_0", "res_ore_0" };

        internal static bool InRingSafeSet(string tone) =>
            AllowedOverlay.Contains(tone) && !tone.StartsWith("steel_", StringComparison.Ordinal) && !tone.StartsWith("lit_", StringComparison.Ordinal)
            && tone != "teal_1" && tone != "res_crystal" && tone != "res_leaf" && tone != "res_water";

        private static readonly DeckLayer[] Decor = { DeckLayer.Base, DeckLayer.Deck, DeckLayer.Prop, DeckLayer.Edge, DeckLayer.Link, DeckLayer.Deposit, DeckLayer.Depot };

        private static IEnumerable<string> Names => DeckTestSupport.LayoutNames;

        private static string ProofFolder => "specs/006-world-first-factory-experience/reviews/t527-proofs";

        private static bool WriteProofs => Environment.GetEnvironmentVariable("IDLEFACTORY_WRITE_PROOFS") == "1";

        private static string[] DepotKeys(int stage) => new[] { "EMPTY", "LOW", "HIGH", "FULL" }.Select(f => "F006-DEPOT-S" + (stage + 1) + "-" + f).ToArray();

        private sealed class Overlays
        {
            public readonly List<HashSet<Vector2Int>> Badges = new List<HashSet<Vector2Int>>();
            public readonly List<HashSet<Vector2Int>> Markers = new List<HashSet<Vector2Int>>();
            public HashSet<Vector2Int> Frame;
            public readonly HashSet<Vector2Int> All = new HashSet<Vector2Int>();
            public int Selected;
        }

        private static Overlays BuildOverlays(DeckComposite composite)
        {
            var overlays = new Overlays { Selected = composite.Layout.LocationCount / 2 };
            for (var i = 0; i < composite.Layout.LocationCount; i++)
            {
                overlays.Badges.Add(composite.OverlayMask(DeckComposite.BadgeKeyOf(i), i));
                overlays.Markers.Add(composite.OverlayMask("F003-MARKER-BOOSTED", i));
                overlays.Markers.Add(composite.OverlayMask("F003-MARKER-OVERDRIVE", i));
            }

            overlays.Frame = composite.OverlayMask("F003-SLOT-SELECTED-FRAME", overlays.Selected);
            foreach (var mask in overlays.Badges.Concat(overlays.Markers).Append(overlays.Frame))
            {
                overlays.All.UnionWith(mask);
            }

            return overlays;
        }

        private static string ToneAt(DeckComposite composite, Vector2Int p) =>
            composite.TopAt(p.x, p.y) == DeckLayer.None ? "ui_bg" : ShippedArt.ToneOf(composite.PixelAt(p.x, p.y));

        // ---- A-13 and OD-9b: the C8 ring proof on the F006 composite --------------------------------------------------------------

        [TestCaseSource(nameof(Names))]
        public void C8TheRingsOfEveryBadgeWithItsK4TailAndEveryMarkerLieInTheAllowedTonesAndNoNewArtPutsSteelInOne(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var settings = DeckTestSupport.Settings();
            var report = new StringBuilder();
            for (var stage = 0; stage < 3; stage++)
            {
                var plan = DeckPlanner.Build(layout, DeckTestSupport.All(layout.LocationCount, SlotCondition.Occupied), stage, settings);
                foreach (var depotKey in DepotKeys(stage))
                {
                    var composite = new DeckComposite(layout, plan, depotKey, "F006-DEPOSIT-REST-S" + (stage + 1), true);
                    var overlays = BuildOverlays(composite);
                    var sampled = 0;
                    var decorPixels = 0;
                    var machineSteel = 0;
                    var tailOverLink = 0;
                    var linkRing = 0;
                    var byLayer = new Dictionary<DeckLayer, int>();
                    foreach (var mask in overlays.Badges.Concat(overlays.Markers))
                    {
                        foreach (var p in DeckComposite.Ring(mask))
                        {
                            if (overlays.All.Contains(p) || !composite.Inside(p.x, p.y))
                            {
                                continue;
                            }

                            sampled++;
                            var layer = composite.TopAt(p.x, p.y);
                            var tone = ToneAt(composite, p);
                            Assert.That(AllowedOverlay, Does.Contain(tone), name + " stage " + stage + " " + depotKey + ": ring pixel " + p + " over " + layer + " has tone " + tone + ", outside A_ov");
                            byLayer[layer] = byLayer.TryGetValue(layer, out var n) ? n + 1 : 1;
                            if (Decor.Contains(layer))
                            {
                                decorPixels++;
                                Assert.That(InRingSafeSet(tone), Is.True, name + " stage " + stage + " " + depotKey + ": the F006 layer " + layer + " puts " + tone + " in a ring at " + p);
                            }
                            else if (tone.StartsWith("steel_", StringComparison.Ordinal))
                            {
                                machineSteel++;
                            }

                            if (layer == DeckLayer.Link)
                            {
                                linkRing++;
                            }
                        }
                    }

                    foreach (var badge in overlays.Badges)
                    {
                        tailOverLink += badge.Count(p => composite.TopAt(p.x, p.y) == DeckLayer.Link);
                    }

                    // The frame ring: A_fr is every tone except the three forbidden ones.
                    foreach (var p in DeckComposite.Ring(overlays.Frame))
                    {
                        if (overlays.All.Contains(p) || !composite.Inside(p.x, p.y))
                        {
                            continue;
                        }

                        Assert.That(FrameForbidden, Does.Not.Contain(ToneAt(composite, p)), name + " stage " + stage + ": frame ring at " + p);
                    }

                    if (depotKey.EndsWith("-EMPTY", StringComparison.Ordinal))
                    {
                        report.AppendLine(name + " stage " + (stage + 1) + ": ring px sampled " + sampled + " (F006 layers " + decorPixels + ", of which links " + linkRing + "), pixels of the F004 machines and plates in rings that are steel_* "
                            + machineSteel + " (F004 art, allowed by C1), badge pixels (with the K4 tail) lying over a trunk or branch " + tailOverLink
                            + "; ring pixels by top layer: " + string.Join(", ", byLayer.OrderBy(kv => (int)kv.Key).Select(kv => kv.Key + " " + kv.Value)));
                    }

                    if (WriteProofs && depotKey.EndsWith("-FULL", StringComparison.Ordinal) && (name == "3x4" || name == "4x3"))
                    {
                        composite.WritePng(ProofFolder + "/" + name + "-stage" + (stage + 1) + ".png", 2, overlays.Selected);
                    }
                }
            }

            Debug.Log("[T527 C8] " + name + "\n" + report);
        }

        [TestCaseSource(nameof(Names))]
        public void TheK4TailLiesOverATrunkOrBranchOnTheDensestFrameAndItsRingStaysInSetR(string name)
        {
            // A-13: "C8 proof with the K4 tail over a trunk". At least one badge overlaps a lane (the lanes run behind the row and each badge sits over its
            // own branch); every ring pixel that lies on a lane is in set R (no steel_*, no teal_1, no lit_*).
            var layout = DeckTestSupport.Layout(name);
            var plan = DeckPlanner.Build(layout, DeckTestSupport.All(layout.LocationCount, SlotCondition.Occupied), 0, DeckTestSupport.Settings());
            var composite = new DeckComposite(layout, plan, "F006-DEPOT-S1-FULL", "F006-DEPOSIT-REST-S1", true);
            var overlays = BuildOverlays(composite);
            var overLink = overlays.Badges.Sum(b => b.Count(p => composite.TopAt(p.x, p.y) == DeckLayer.Link));
            Assert.That(overLink, Is.GreaterThan(0), name + ": the K4 badge (tail included) covers lane pixels");
            var tones = new SortedDictionary<string, int>();
            foreach (var badge in overlays.Badges)
            {
                foreach (var p in DeckComposite.Ring(badge))
                {
                    if (overlays.All.Contains(p) || composite.TopAt(p.x, p.y) != DeckLayer.Link)
                    {
                        continue;
                    }

                    var tone = ToneAt(composite, p);
                    tones[tone] = tones.TryGetValue(tone, out var n) ? n + 1 : 1;
                    Assert.That(InRingSafeSet(tone), Is.True, name + ": lane ring pixel " + p + " is " + tone);
                }
            }

            Debug.Log("[T527 K4 over trunk] " + name + ": " + overLink + " badge px over lane art; ring tones on lanes " + string.Join(", ", tones.Select(kv => kv.Key + " " + kv.Value)));
        }

        // ---- P2-F6: depot and deposit pixels never enter a ring ------------------------------------------------------------------

        [TestCaseSource(nameof(Names))]
        public void NoDepotOrDepositPixelEntersAC8RingOrAKeepOutAtAnyStageAndFillState(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var composite0 = new DeckComposite(layout, DeckPlanner.Build(layout, null, 0, null), "F006-DEPOT-S1-EMPTY", "F006-DEPOSIT-REST-S1", false);
            var overlays = BuildOverlays(composite0);
            var rings = new HashSet<Vector2Int>();
            foreach (var mask in overlays.Badges.Concat(overlays.Markers).Append(overlays.Frame))
            {
                rings.UnionWith(DeckComposite.Ring(mask));
                rings.UnionWith(mask);
            }

            var checkedPixels = 0;
            var depotAnchor = new Vector2Int(Mathf.RoundToInt(layout.DepotAnchor.x * 24f), Mathf.RoundToInt(layout.DepotAnchor.y * 24f));
            for (var stage = 0; stage < 3; stage++)
            {
                foreach (var key in DepotKeys(stage))
                {
                    DeckSpriteSize.TryOf(key, out var size);
                    var rect = size.RectAt(depotAnchor);
                    var pixels = DeckTestSupport.PixelsOfKey(key);
                    for (var y = 0; y < pixels.Height; y++)
                    {
                        for (var x = 0; x < pixels.Width; x++)
                        {
                            if (pixels.IsOpaque(x, y))
                            {
                                checkedPixels++;
                                Assert.That(rings.Contains(new Vector2Int(rect.X + x, rect.Y + y)), Is.False, name + " " + key + ": a depot pixel lies in a badge, marker or frame ring at " + new Vector2Int(rect.X + x, rect.Y + y));
                            }
                        }
                    }
                }
            }

            var depositAnchor = new Vector2Int(Mathf.RoundToInt(layout.DepositAnchor.x * 24f), Mathf.RoundToInt(layout.DepositAnchor.y * 24f));
            foreach (var key in new[] { "F006-DEPOSIT-REST-S1", "F006-DEPOSIT-REST-S2", "F006-DEPOSIT-REST-S3", "F006-DEPOSIT-HIGHLIGHT" })
            {
                DeckSpriteSize.TryOf(key, out var size);
                var rect = size.RectAt(depositAnchor);
                var pixels = DeckTestSupport.PixelsOfKey(key);
                for (var y = 0; y < pixels.Height; y++)
                {
                    for (var x = 0; x < pixels.Width; x++)
                    {
                        if (pixels.IsOpaque(x, y))
                        {
                            checkedPixels++;
                            Assert.That(rings.Contains(new Vector2Int(rect.X + x, rect.Y + y)), Is.False, name + " " + key + ": a deposit pixel lies in a ring at " + new Vector2Int(rect.X + x, rect.Y + y));
                        }
                    }
                }
            }

            Debug.Log("[T527 P2-F6] " + name + ": " + checkedPixels + " opaque depot (12 variants) and deposit (4 variants) pixels checked against " + rings.Count + " ring and badge pixels: none inside");
        }

        [Test]
        public void TheDepotKeepsItsLampHopperAndCopperCollarVisibleAtTheSmallScreenClasses()
        {
            // Art Bible 21.7: the lamp tip is inside the sprite canvas with at least 2 art px of headroom (so a partial depot view does not crop it), and
            // the hopper opening and the copper collar show the delivery. P2-F6 asks for the visibility at 640 x 360 and at 200 % text.
            foreach (var stage in new[] { 1, 2, 3 })
            {
                foreach (var fill in new[] { "EMPTY", "LOW", "HIGH", "FULL" })
                {
                    var key = "F006-DEPOT-S" + stage + "-" + fill;
                    var pixels = DeckTestSupport.PixelsOfKey(key);
                    Assert.That(pixels.Height - pixels.OpaqueTop(), Is.GreaterThanOrEqualTo(2), key + ": at least 2 art px of headroom above the lamp tip");
                    var copper = pixels.Pixels.Count(p => p.a > 0 && ShippedArt.ToneOf(p).StartsWith("copper_", StringComparison.Ordinal));
                    Assert.That(copper, Is.GreaterThanOrEqualTo(8), key + ": the copper collar is drawn");
                    var ink = pixels.Pixels.Count(p => p.a > 0 && ShippedArt.ToneOf(p) == "ink_0");
                    Assert.That(ink, Is.GreaterThan(0), key + ": the hopper opening carries its ink outline");
                }
            }

            // Visibility in the camera model at the two small classes. The shell area is the screen less the persistent chrome: at 200 % text the strip
            // and the tab bar double. The depot is out of the fit-all view when the view extent would cost a scale; it is then reachable by panning
            // (P1-F2, T534). The result is recorded, never hidden by art (Art Bible 21.7).
            var report = new StringBuilder("[T527 depot visibility]\n");
            foreach (var (label, width, height, chromeTop, chromeBottom) in new[] { ("640x360 100%", 640, 360, 48, 56), ("640x360 200%", 640, 360, 96, 112) })
            {
                var layout = LatticeLayoutBuilder.Shipped(width, height);
                var area = new Rect(0f, chromeBottom, width, height - chromeTop - chromeBottom);
                var model = new CameraScaleModel(new ArtScaleSettings(24, 24, 6));
                model.Fit(ScaleLayout.WithView(width, height, 1f, area, layout));
                var lamp = layout.DepotRect;
                var low = model.WorldToScreen(lamp.min, model.Position, model.Scale);
                var high = model.WorldToScreen(lamp.max, model.Position, model.Scale);
                var inside = low.x >= area.xMin - 1f && high.x <= area.xMax + 1f && low.y >= area.yMin - 1f && high.y <= area.yMax + 1f;
                model.Pan(new Vector2(-1000f, 0f));
                var panLow = model.WorldToScreen(lamp.min, model.Position, model.Scale);
                var panHigh = model.WorldToScreen(lamp.max, model.Position, model.Scale);
                var reachable = panLow.x >= area.xMin - 1f && panHigh.x <= area.xMax + 1f;
                report.AppendLine(label + ": area " + area.width + " x " + area.height + " points, scale n = " + model.Scale + ", depot (80 x 76 canvas with the lamp) fully in the fit-all view: " + inside
                    + "; after panning to the depot side it is fully in view: " + reachable);
            }

            Debug.Log(report.ToString());
        }

        // ---- P2-F7, P2-F23: the stage-2 wall -----------------------------------------------------------------------------------------

        [TestCaseSource(nameof(Names))]
        public void NoWallOrGantryPixelEntersAnyRingAndTheWallDoesNotOutweighTheFrontRowMachinePlates(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var settings = DeckTestSupport.Settings();
            var report = new StringBuilder();
            for (var stage = 0; stage < 3; stage++)
            {
                var plan = DeckPlanner.Build(layout, DeckTestSupport.All(layout.LocationCount, SlotCondition.Occupied), stage, settings);
                var composite = new DeckComposite(layout, plan, "F006-DEPOT-S" + (stage + 1) + "-FULL", "F006-DEPOSIT-REST-S" + (stage + 1), true);
                var overlays = BuildOverlays(composite);
                var rings = new HashSet<Vector2Int>();
                foreach (var mask in overlays.Badges.Concat(overlays.Markers).Append(overlays.Frame))
                {
                    rings.UnionWith(DeckComposite.Ring(mask));
                    rings.UnionWith(mask);
                }

                // Every opaque edge pixel, occluded or not, against every ring: zero.
                var edgePixels = 0;
                foreach (var edge in plan.Edges)
                {
                    var pixels = DeckTestSupport.PixelsOfKey(edge.Key);
                    for (var y = 0; y < pixels.Height; y++)
                    {
                        for (var x = 0; x < pixels.Width; x++)
                        {
                            if (pixels.IsOpaque(x, y))
                            {
                                edgePixels++;
                                Assert.That(rings.Contains(new Vector2Int(edge.Rect.X + x, edge.Rect.Y + y)), Is.False,
                                    name + " stage " + (stage + 1) + " " + edge.Key + ": an edge pixel lies in a C8 ring or on an overlay at " + new Vector2Int(edge.Rect.X + x, edge.Rect.Y + y));
                            }
                        }
                    }
                }

                // Luminance mass (Art Director E1): the visible edge pixels against the visible plate and machine pixels of the front row of locations.
                var frontRow = Enumerable.Range(0, layout.LocationCount).Where(i => layout.CellOf(new BuildingLocationId(i)).y == 0).ToList();
                double edgeMass = 0, plateMass = 0, plateAndBodyMass = 0, edgeBright = 0, plateBright = 0;
                var edgeCount = 0;
                for (var y = composite.Area.Y; y < composite.Area.YMax; y++)
                {
                    for (var x = composite.Area.X; x < composite.Area.XMax; x++)
                    {
                        var layer = composite.TopAt(x, y);
                        if (layer != DeckLayer.Edge)
                        {
                            continue;
                        }

                        var l = ShippedArt.Luminance(composite.PixelAt(x, y));
                        edgeMass += l;
                        edgeCount++;
                        var tone = ShippedArt.ToneOf(composite.PixelAt(x, y));
                        edgeBright += tone == "steel_2" || tone == "steel_3" ? 1 : 0;
                    }
                }

                foreach (var i in frontRow)
                {
                    var pivot = composite.Pivot(i);
                    foreach (var (layerKey, pixels, isPlate) in new[]
                    {
                        (DeckLayer.Plate, ShippedArt.F004("F003-SLOT-OCCUPIED-BASE"), true),
                        (DeckLayer.Machine, ShippedArt.F004("F003-MACHINE-" + new[] { "MINER", "SMELTER", "PRESS", "ASSEMBLER" }[i % 4]), false),
                    })
                    {
                        for (var y = 0; y < pixels.Height; y++)
                        {
                            for (var x = 0; x < pixels.Width; x++)
                            {
                                var wx = pivot.x - 40 + x;
                                var wy = pivot.y - 15 + y;
                                if (!pixels.IsOpaque(x, y) || composite.TopAt(wx, wy) != layerKey)
                                {
                                    continue;
                                }

                                var l = ShippedArt.Luminance(pixels.Pixels[y * pixels.Width + x]);
                                plateAndBodyMass += l;
                                if (isPlate)
                                {
                                    plateMass += l;
                                    var tone = ShippedArt.ToneOf(pixels.Pixels[y * pixels.Width + x]);
                                    plateBright += tone == "plate_2" ? 1 : 0;
                                }
                            }
                        }
                    }
                }

                if (stage == 1)
                {
                    // Art Director E1 (P2-F23): the wall must not dominate the front-row machine plates (a machine on its plate). It is measured at under 0.6 of
                    // the plate-and-machine mass; against the bare plate alone it is larger (recorded in the evidence note for the Art Director).
                    Assert.That(edgeMass, Is.LessThan(plateAndBodyMass), name + ": the stage-2 wall's luminance mass stays below the front-row plate-and-machine mass");
                    Assert.That(edgeMass / plateAndBodyMass, Is.LessThan(0.75), name);
                }

                report.AppendLine(name + " stage " + (stage + 1) + " (" + plan.EdgeRule + "): " + plan.Edges.Count + " pieces, " + edgePixels + " opaque px, none in any ring; visible edge px " + edgeCount
                    + ", luminance mass " + edgeMass.ToString("0.0") + " (steel_2/steel_3 px " + edgeBright + "); front row of " + frontRow.Count + " locations: plate mass " + plateMass.ToString("0.0")
                    + ", plate + machine mass " + plateAndBodyMass.ToString("0.0") + "; edge / plate = " + (edgeMass / plateMass).ToString("0.00") + ", edge / (plate + machine) = " + (edgeMass / plateAndBodyMass).ToString("0.00"));
            }

            Debug.Log("[T527 E1 luminance mass] " + name + "\n" + report);
        }
    }
}
