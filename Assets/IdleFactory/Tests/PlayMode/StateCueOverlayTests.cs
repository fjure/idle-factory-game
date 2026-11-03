#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T429: footprint-anchored overlays (badge, markers, selection frame, lock label), the overlay sorting layer, one Sorting Group per
    /// machine and row-based sorting. The F004 art is not integrated yet, so the densest-frame tests use stand-in canvas layers
    /// (80 x 64 px at 24 PPU, pivot (40, 15), palette-v1 tones, Art Bible geometry); the code under test is the view, the cue set, the
    /// camera mapping and the hit test. The C8 acceptance runs on the real render at every n_min.
    /// </summary>
    public sealed class StateCueOverlayTests
    {
        private const int D = 24;
        private const int W = 80;
        private const int H = 64;

        private static readonly string[] MachineKeys =
            { "F003-MACHINE-MINER", "F003-MACHINE-SMELTER", "F003-MACHINE-PRESS", "F003-MACHINE-ASSEMBLER" };

        private readonly List<Object> owned = new List<Object>();
        private Dictionary<string, Color32> palette;
        private ShellRig shellRig;

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
            shellRig?.Dispose();
            shellRig = null;
        }

        // ---------------------------------------------------------------------------------------------------
        // Stand-in art
        // ---------------------------------------------------------------------------------------------------

        private Dictionary<string, Color32> Palette()
        {
            if (palette != null)
            {
                return palette;
            }

            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "tools", "art-pipeline", "palette", "palette-v1.json"));
            var text = File.ReadAllText(path);
            palette = new Dictionary<string, Color32>();
            foreach (Match match in Regex.Matches(text, "\"id\"\\s*:\\s*\"([a-z0-9_]+)\"\\s*,\\s*\"hex\"\\s*:\\s*\"#([0-9a-fA-F]{6})\""))
            {
                var value = Convert.ToInt32(match.Groups[2].Value, 16);
                palette[match.Groups[1].Value] = new Color32((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);
            }

            Assert.That(palette.Count, Is.GreaterThan(40));
            return palette;
        }

        private sealed class Canvas
        {
            public readonly Color32[] Pixels = new Color32[W * H];

            public bool Opaque(int x, int row) => x >= 0 && x < W && row >= 0 && row < H && Pixels[(H - 1 - row) * W + x].a != 0;

            public void Set(int x, int row, Color32 color)
            {
                if (x >= 0 && x < W && row >= 0 && row < H)
                {
                    Pixels[(H - 1 - row) * W + x] = color;
                }
            }

            public void Fill(int x0, int row0, int x1, int row1, Color32 color)
            {
                for (var row = row0; row <= row1; row++)
                {
                    for (var x = x0; x <= x1; x++)
                    {
                        Set(x, row, color);
                    }
                }
            }

            public void Box(int x0, int row0, int x1, int row1, Color32 fill, Color32 ink)
            {
                Fill(x0, row0, x1, row1, ink);
                Fill(x0 + 1, row0 + 1, x1 - 1, row1 - 1, fill);
            }
        }

        private Sprite ToSprite(Canvas canvas, string name)
        {
            var texture = new Texture2D(W, H, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
            };
            texture.SetPixels32(canvas.Pixels);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, W, H), new Vector2(40f / W, 15f / H), D, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            owned.Add(texture);
            owned.Add(sprite);
            return sprite;
        }

        private Canvas PlateCanvas(Color32 interior, Color32 rim)
        {
            var canvas = new Canvas();
            for (var row = 29; row <= 63; row++)
            {
                var width = Mathf.Min(70, 4 + (row <= 46 ? row - 29 : 63 - row) * 4);
                var left = 40 - width / 2;
                for (var x = left; x < left + width; x++)
                {
                    var edge = x == left || x == left + width - 1 || row == 29 || row == 63;
                    canvas.Set(x, row, edge ? rim : interior);
                }
            }

            return canvas;
        }

        private Sprite FrameSprite(Canvas plate, string name)
        {
            var frame = new Canvas();
            for (var row = 0; row < H; row++)
            {
                for (var x = 0; x < W; x++)
                {
                    if (plate.Opaque(x, row))
                    {
                        continue;
                    }

                    if (plate.Opaque(x + 1, row) || plate.Opaque(x - 1, row) || plate.Opaque(x, row + 1) || plate.Opaque(x, row - 1))
                    {
                        frame.Set(x, row, Palette()["ui_accent"]);
                    }
                }
            }

            return ToSprite(frame, name);
        }

        private Sprite[] MachineFrames(int kind, bool accent)
        {
            var tones = Palette();
            var frames = new Sprite[6];
            for (var f = 0; f < 6; f++)
            {
                var canvas = new Canvas();
                if (accent)
                {
                    canvas.Fill(30 + f, 44, 31 + f, 45, tones["lit_1"]);
                }
                else
                {
                    canvas.Box(24, 36, 55 + kind, 60, tones["steel_2"], tones["ink_0"]);
                    canvas.Fill(30 + f, 44, 31 + f, 45, tones["lit_0"]);
                }

                frames[f] = ToSprite(canvas, (accent ? "accent-" : "machine-") + kind + "-" + f);
            }

            return frames;
        }

        private Sprite BadgeSprite(int index, string tone)
        {
            var tones = Palette();
            var canvas = new Canvas();
            canvas.Box(48, 10, 70, 32, tones[tone], tones["ink_0"]);
            canvas.Fill(52 + index * 2, 14, 53 + index * 2, 28, tones["ink_0"]); // a glyph that differs per state
            return ToSprite(canvas, "badge-" + index);
        }

        private Sprite MarkerSprite(int x0, string tone, string name)
        {
            var canvas = new Canvas();
            canvas.Box(x0, 16, x0 + 10, 26, Palette()[tone], Palette()["ink_0"]);
            return ToSprite(canvas, name);
        }

        private Sprite LockLabel(int level)
        {
            var canvas = new Canvas();
            canvas.Fill(36, 40, 44, 54, Palette()["ui_text"]);
            canvas.Fill(36, 36, 36 + level, 37, Palette()["ui_text"]);
            return ToSprite(canvas, "lock-" + level);
        }

        private PresentationSpriteCatalog StandInCatalog()
        {
            var tones = Palette();
            var entries = new List<PresentationSpriteEntry>();
            var requirements = new List<PresentationSpriteRequirement>();

            void Add(string key, Sprite[] frames, float? fps, int maxFrames)
            {
                entries.Add(new PresentationSpriteEntry(key, frames, fps));
                requirements.Add(new PresentationSpriteRequirement(key, W / (float)D, H / (float)D, 0.5f, 15f / H, 1, maxFrames, false));
            }

            var plate = PlateCanvas(tones["plate_1"], tones["plate_2"]);
            Add(SlotGridModel.SlotEmptyKey, new[] { ToSprite(PlateCanvas(tones["plate_1"], tones["plate_2"]), "empty") }, null, 1);
            Add(SlotGridModel.SlotOccupiedKey, new[] { ToSprite(plate, "occupied") }, null, 1);
            for (var level = 2; level <= 5; level++)
            {
                Add(SlotGridModel.LockedKey(level), new[] { ToSprite(PlateCanvas(tones["dim_0"], tones["plate_2"]), "locked" + level) }, null, 1);
                Add(PresentationSpriteCatalog.LockLabelKeyPrefix + level, new[] { LockLabel(level) }, null, 1);
            }

            Add(SlotGridModel.SlotSelectedFrameKey, new[] { FrameSprite(plate, "frame") }, null, 1);
            for (var i = 0; i < MachineKeys.Length; i++)
            {
                Add(MachineKeys[i], MachineFrames(i, false), 8f, 16);
                Add(StateCueSet.AccentKeyFor(MachineKeys[i]), MachineFrames(i, true), 8f, 16);
            }

            // The running badge is a decoy: the view must draw nothing for it (intentionally empty entry).
            var badgeTones = new[] { "cue_neutral", "cue_info", "cue_alert", "cue_warn", "cue_neutral", "cue_dim" };
            var states = (MachineVisualState[])Enum.GetValues(typeof(MachineVisualState));
            for (var i = 0; i < states.Length; i++)
            {
                Add(SlotGridModel.BadgeKeyOf(states[i]), new[] { BadgeSprite(i, badgeTones[i]) }, null, 1);
            }

            Add(SlotGridModel.MarkerBoostedKey, new[] { MarkerSprite(24, "cvd_green", "boosted") }, null, 1);
            Add(SlotGridModel.MarkerOverdriveKey, new[] { MarkerSprite(36, "cvd_pink", "overdrive") }, null, 1);

            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(entries, requirements);
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            return catalog;
        }

        private static SlotVisual Occupied(int index, MachineVisualState state, bool boosted, bool overdrive, bool selected)
        {
            var badge = SlotGridModel.BadgeKeyOf(state);
            return new SlotVisual(index, SlotCondition.Occupied, SlotGridModel.SlotOccupiedKey, MachineKeys[index % MachineKeys.Length], badge,
                state.ToString(), boosted, overdrive, selected, 0, "Slot " + (index + 1) + ", " + state);
        }

        private static SlotVisual[] Densest(int selected = 5)
        {
            var states = (MachineVisualState[])Enum.GetValues(typeof(MachineVisualState));
            return Enumerable.Range(0, SlotGridLayout.SlotCount)
                .Select(i => Occupied(i, states[i % states.Length], true, true, i == selected))
                .ToArray();
        }

        private SlotGridView NewView(PresentationSpriteCatalog catalog, int columns = 3, float pointsPerCell = 48f)
        {
            var host = new GameObject("state-cue-grid");
            owned.Add(host);
            var view = host.AddComponent<SlotGridView>();
            view.Configure(catalog);
            view.SetColumns(columns);
            view.SetProjectedPixelsPerCell(pointsPerCell);
            return view;
        }

        // ---------------------------------------------------------------------------------------------------
        // Pixel masks in the world art grid
        // ---------------------------------------------------------------------------------------------------

        private static long Key(int gx, int gy) => ((long)(gx + 100000) << 24) | (long)(gy + 100000);

        private static Dictionary<long, Color32> PixelsOf(SpriteRenderer renderer)
        {
            var result = new Dictionary<long, Color32>();
            var sprite = renderer.sprite;
            var pixels = sprite.texture.GetPixels32();
            var width = sprite.texture.width;
            var ox = Mathf.RoundToInt(renderer.transform.position.x * D) - Mathf.RoundToInt(sprite.pivot.x);
            var oy = Mathf.RoundToInt(renderer.transform.position.y * D) - Mathf.RoundToInt(sprite.pivot.y);
            for (var ty = 0; ty < sprite.texture.height; ty++)
            {
                for (var tx = 0; tx < width; tx++)
                {
                    var color = pixels[ty * width + tx];
                    if (color.a != 0)
                    {
                        result[Key(ox + tx, oy + ty)] = color;
                    }
                }
            }

            return result;
        }

        private static readonly SlotGridView.SlotRendererRole[] OverlayRoles =
        {
            SlotGridView.SlotRendererRole.Frame, SlotGridView.SlotRendererRole.Label, SlotGridView.SlotRendererRole.Badge,
            SlotGridView.SlotRendererRole.Boosted, SlotGridView.SlotRendererRole.Overdrive,
        };

        private static readonly SlotGridView.SlotRendererRole[] BodyRoles =
        {
            SlotGridView.SlotRendererRole.Plate, SlotGridView.SlotRendererRole.Machine, SlotGridView.SlotRendererRole.Accent,
        };

        private static List<(int Slot, SlotGridView.SlotRendererRole Role, Dictionary<long, Color32> Pixels)> Collect(
            SlotGridView view, SlotGridView.SlotRendererRole[] roles)
        {
            var list = new List<(int, SlotGridView.SlotRendererRole, Dictionary<long, Color32>)>();
            for (var slot = 0; slot < SlotGridLayout.SlotCount; slot++)
            {
                foreach (var role in roles)
                {
                    var renderer = view.RendererOf(slot, role);
                    if (renderer != null && renderer.enabled && renderer.sprite != null)
                    {
                        list.Add((slot, role, PixelsOf(renderer)));
                    }
                }
            }

            return list;
        }

        // ---------------------------------------------------------------------------------------------------
        // Densest frame: counts, overlap, sorting
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void TheDensestFrameHasOneAccentPerRunningSlotAndOneBadgePerOtherState()
        {
            var view = NewView(StandInCatalog());
            view.Render(Densest());
            var running = view.Visuals.Count(v => v.BadgeKey == SlotGridModel.BadgeRunningKey);
            Assert.That(running, Is.EqualTo(2));
            Assert.That(view.ActiveAccentCount, Is.EqualTo(running));
            Assert.That(view.ActiveAccentCount, Is.LessThanOrEqualTo(12), "at most 12 F004-ACCENT renderers");

            for (var slot = 0; slot < SlotGridLayout.SlotCount; slot++)
            {
                var isRunning = view.Visuals[slot].BadgeKey == SlotGridModel.BadgeRunningKey;
                Assert.That(view.RendererOf(slot, SlotGridView.SlotRendererRole.Badge).enabled, Is.EqualTo(!isRunning), "badge of slot " + (slot + 1));
                Assert.That(view.RendererOf(slot, SlotGridView.SlotRendererRole.Accent).enabled, Is.EqualTo(isRunning), "accent of slot " + (slot + 1));
                Assert.That(view.RendererOf(slot, SlotGridView.SlotRendererRole.Boosted).enabled, Is.True);
                Assert.That(view.RendererOf(slot, SlotGridView.SlotRendererRole.Overdrive).enabled, Is.True);
                Assert.That(view.RendererOf(slot, SlotGridView.SlotRendererRole.Frame).enabled, Is.EqualTo(slot == 5));
            }
        }

        [Test]
        public void EveryNonRunningBadgeForResultRendersADistinctNonEmptyBadgeAndRunningRendersNone()
        {
            var view = NewView(StandInCatalog());
            var masks = new Dictionary<string, HashSet<long>>();
            foreach (var active in new[] { true, false })
            {
                foreach (var blocker in Enum.GetValues(typeof(MachineBlocker)).Cast<MachineBlocker>())
                {
                    foreach (var phase in Enum.GetValues(typeof(MachinePhase)).Cast<MachinePhase>())
                    {
                        var key = SlotGridModel.BadgeFor(active, blocker, phase, out var text, out _);
                        view.Render(new[] { new SlotVisual(0, SlotCondition.Occupied, SlotGridModel.SlotOccupiedKey, MachineKeys[0], key, text, false, false, false, 0, "s") });
                        var badge = view.RendererOf(0, SlotGridView.SlotRendererRole.Badge);
                        if (key == SlotGridModel.BadgeRunningKey)
                        {
                            Assert.That(badge.enabled, Is.False, "running draws no badge even though the catalog holds a sprite for its key");
                            continue;
                        }

                        Assert.That(badge.enabled && badge.sprite != null, Is.True, key);
                        var pixels = PixelsOf(badge);
                        Assert.That(pixels.Count, Is.GreaterThan(0), key + " is a non-empty badge");
                        masks[key] = InkMask(pixels);
                    }
                }
            }

            Assert.That(masks.Count, Is.EqualTo(5));
            Assert.That(ShapesDistinct(masks.Values.ToList()), Is.True, "FR-010: each state has its own glyph shape, whatever its colour");

            // Negative control: five badges with the same glyph in five fill colours are colour-only differences and must fail.
            var sameShape = new[] { "cue_neutral", "cue_info", "cue_alert", "cue_warn", "cue_dim" }
                .Select(tone => InkMask(SpritePixels(BadgeSprite(0, tone)))).ToList();
            Assert.That(ShapesDistinct(sameShape), Is.False, "badges that differ only by colour are not distinct shapes");
        }

        // The glyph and outline are drawn in the darkest tone of the badge, so the mask of that tone is the shape, independent of the fill.
        private static HashSet<long> InkMask(Dictionary<long, Color32> pixels)
        {
            int Luma(Color32 c) => c.r * 299 + c.g * 587 + c.b * 114;
            var ink = pixels.Values.OrderBy(Luma).First();
            return new HashSet<long>(pixels.Where(p => p.Value.r == ink.r && p.Value.g == ink.g && p.Value.b == ink.b).Select(p => p.Key));
        }

        private static bool ShapesDistinct(List<HashSet<long>> masks)
        {
            for (var i = 0; i < masks.Count; i++)
            {
                for (var j = i + 1; j < masks.Count; j++)
                {
                    var differing = masks[i].Count(k => !masks[j].Contains(k)) + masks[j].Count(k => !masks[i].Contains(k));
                    if (differing < 8)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static Dictionary<long, Color32> SpritePixels(Sprite sprite)
        {
            var result = new Dictionary<long, Color32>();
            var pixels = sprite.texture.GetPixels32();
            for (var i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a != 0)
                {
                    result[Key(i % sprite.texture.width, i / sprite.texture.width)] = pixels[i];
                }
            }

            return result;
        }

        [Test]
        public void NoOverlayPixelSharesAnArtPixelWithAnotherSlotsOverlayOrWithTheBodyBehind([Values(3, 4)] int columns)
        {
            var view = NewView(StandInCatalog(), columns);
            view.Render(Densest());
            var overlays = Collect(view, OverlayRoles);
            var bodies = Collect(view, BodyRoles);
            Assert.That(overlays.Count, Is.GreaterThan(30));

            for (var i = 0; i < overlays.Count; i++)
            {
                for (var j = i + 1; j < overlays.Count; j++)
                {
                    if (overlays[i].Slot == overlays[j].Slot)
                    {
                        continue;
                    }

                    Assert.That(overlays[i].Pixels.Keys.Any(overlays[j].Pixels.ContainsKey), Is.False,
                        $"{overlays[i].Role} of slot {overlays[i].Slot + 1} overlaps {overlays[j].Role} of slot {overlays[j].Slot + 1}");
                }
            }

            foreach (var overlay in overlays.Where(o => o.Role != SlotGridView.SlotRendererRole.Frame))
            {
                var behind = overlay.Slot - columns;
                if (behind < 0)
                {
                    continue;
                }

                foreach (var body in bodies.Where(b => b.Slot == behind && b.Role != SlotGridView.SlotRendererRole.Plate))
                {
                    Assert.That(overlay.Pixels.Keys.Any(body.Pixels.ContainsKey), Is.False,
                        $"{overlay.Role} of slot {overlay.Slot + 1} covers the {body.Role} of the row behind");
                }
            }
        }

        [Test]
        public void EachMachineIsOneSortingGroupSortedByRowAndOverlaysSitOnTheOverlayLayerAboveAllMachineArt([Values(3, 4)] int columns)
        {
            var overlayLayer = SortingLayer.GetLayerValueFromName(SlotGridView.OverlaySortingLayer);
            Assert.That(SortingLayer.layers.Any(l => l.name == SlotGridView.OverlaySortingLayer), Is.True, "the overlay layer is declared in TagManager");
            Assert.That(overlayLayer, Is.GreaterThan(SortingLayer.GetLayerValueFromName("Default")));

            var view = NewView(StandInCatalog(), columns);
            view.Render(Densest());
            for (var slot = 0; slot < SlotGridLayout.SlotCount; slot++)
            {
                var plate = view.RendererOf(slot, SlotGridView.SlotRendererRole.Plate);
                var groups = view.transform.GetChild(slot).GetComponentsInChildren<UnityEngine.Rendering.SortingGroup>();
                Assert.That(groups.Length, Is.EqualTo(1), "one Sorting Group per machine");
                Assert.That(groups[0].sortingOrder, Is.EqualTo(slot / columns), "row-based order, back row first");
                Assert.That(plate.GetComponentInParent<UnityEngine.Rendering.SortingGroup>(), Is.SameAs(groups[0]));
                foreach (var role in OverlayRoles)
                {
                    var renderer = view.RendererOf(slot, role);
                    Assert.That(renderer.GetComponentInParent<UnityEngine.Rendering.SortingGroup>(), Is.Null, role + " is outside the machine group");
                    Assert.That(renderer.sortingLayerName, Is.EqualTo(SlotGridView.OverlaySortingLayer));
                    Assert.That(SortingLayer.GetLayerValueFromID(renderer.sortingLayerID), Is.GreaterThan(SortingLayer.GetLayerValueFromID(plate.sortingLayerID)));
                }

                if (slot >= columns)
                {
                    foreach (var role in OverlayRoles)
                    {
                        Assert.That(view.RendererOf(slot, role).sortingOrder, Is.GreaterThan(view.RendererOf(slot - columns, role).sortingOrder), role + " front row draws later");
                    }
                }
            }
        }

        [Test]
        public void ATapOnATallMachinesOverhangSelectsTheSlotBehindBecauseHitTestingIsFootprintOnly()
        {
            var tones = Palette();
            var tall = new Canvas();
            tall.Fill(30, 0, 49, 63, tones["steel_2"]); // a machine that towers over the gap into the row behind
            var catalog = StandInCatalog();
            var entries = new List<PresentationSpriteEntry>();
            var frames = new[] { ToSprite(tall, "tall") };
            var withTall = new PresentationSpriteCatalog();
            var rows = new List<PresentationSpriteRequirement>();
            foreach (var key in MachineKeys)
            {
                Assert.That(catalog.TryGetEntry(key, out var original), Is.True);
                entries.Add(new PresentationSpriteEntry(key, frames, null));
                rows.Add(new PresentationSpriteRequirement(key, W / (float)D, H / (float)D, 0.5f, 15f / H, 1, 16, false));
            }

            foreach (var key in new[] { SlotGridModel.SlotOccupiedKey })
            {
                Assert.That(catalog.TryGetEntry(key, out var entry), Is.True);
                entries.Add(entry);
                rows.Add(new PresentationSpriteRequirement(key, W / (float)D, H / (float)D, 0.5f, 15f / H, 1, 1, false));
            }

            withTall.Configure(entries, rows);
            Assert.That(withTall.IsValid, Is.True, withTall.Error);
            var view = NewView(withTall);
            view.Render(Enumerable.Range(0, 12).Select(i => Occupied(i, MachineVisualState.Paused, false, false, false)).ToArray());

            var front = 4; // second row, middle column
            var machine = view.RendererOf(front, SlotGridView.SlotRendererRole.Machine);
            var pixels = PixelsOf(machine);
            var rect = view.WorldRectOf(front);
            var behind = front - view.Columns;
            var probe = new Vector2(rect.center.x, rect.yMax + 0.4f); // beyond the 0.25 gap tolerance, inside the tall art
            Assert.That(pixels.ContainsKey(Key(Mathf.FloorToInt(probe.x * D), Mathf.FloorToInt(probe.y * D))), Is.True, "the probe lies on the front machine's art");
            Assert.That(view.TryHitSlot(probe, out var hit), Is.True);
            Assert.That(hit, Is.EqualTo(behind), "the overhang selects the slot behind");
            Assert.That(view.TryHitSlot(new Vector2(rect.center.x, rect.yMax + 0.1f), out hit) && hit == front, Is.True, "the near gap still belongs to the footprint");
        }

        // ---------------------------------------------------------------------------------------------------
        // Anchors on the placeholder (tight) art, label threshold, loop resume
        // ---------------------------------------------------------------------------------------------------

        private Sprite Tight(string name, int width, int height, float ppu)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point };
            var fill = new Color32[width * height];
            for (var i = 0; i < fill.Length; i++)
            {
                fill[i] = new Color32(200, 200, 200, 255);
            }

            texture.SetPixels32(fill);
            texture.Apply();
            owned.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), ppu);
            owned.Add(sprite);
            return sprite;
        }

        [Test]
        public void PlaceholderArtAnchorsTheBadgeBottomCentreAndTheMarkersOnTheirBoxesAtD24()
        {
            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(new[]
            {
                new PresentationSpriteEntry(SlotGridModel.BadgeWaitingKey, Tight("b", 72, 32, 64f)),
                new PresentationSpriteEntry(SlotGridModel.MarkerBoostedKey, Tight("m1", 28, 28, 64f)),
                new PresentationSpriteEntry(SlotGridModel.MarkerOverdriveKey, Tight("m2", 28, 28, 64f)),
            });
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            var view = NewView(catalog, 3, 64f);
            view.Render(new[] { Occupied(0, MachineVisualState.Waiting, true, true, false) });

            var pivot = view.FootprintPivotOf(0);
            var badge = view.RendererOf(0, SlotGridView.SlotRendererRole.Badge).bounds;
            Assert.That(badge.center.x, Is.EqualTo(pivot.x + StateCueGeometry.BadgeAnchorOffset.x).Within(1e-3f));
            Assert.That(badge.min.y, Is.EqualTo(pivot.y + StateCueGeometry.BadgeAnchorOffset.y).Within(1e-3f), "bottom-centre anchor (59, 33)");
            var m1 = view.RendererOf(0, SlotGridView.SlotRendererRole.Boosted).bounds.center;
            var m2 = view.RendererOf(0, SlotGridView.SlotRendererRole.Overdrive).bounds.center;
            Assert.That(m1.x, Is.EqualTo(pivot.x + StateCueGeometry.Marker1Offset.x).Within(1e-3f));
            Assert.That(m2.x, Is.EqualTo(pivot.x + StateCueGeometry.Marker2Offset.x).Within(1e-3f));
            Assert.That(m1.y, Is.EqualTo(pivot.y + StateCueGeometry.Marker1Offset.y).Within(1e-3f));
            Assert.That(m2.y, Is.EqualTo(m1.y).Within(1e-3f));
        }

        [Test]
        public void TheLockDigitIsShownOnlyFromACapHeightOfTwelvePoints()
        {
            var view = NewView(StandInCatalog(), 3, 48f);
            var locked = new SlotVisual(11, SlotCondition.Locked, SlotGridModel.LockedKey(4), null, null, null, false, false, false, 4, "Slot 12, locked");
            view.Render(Enumerable.Range(0, 12).Select(i => i == 11 ? locked : Occupied(i, MachineVisualState.Paused, false, false, false)).ToArray());
            var label = view.RendererOf(11, SlotGridView.SlotRendererRole.Label);
            Assert.That(label.enabled, Is.True, "48 pt per cell: cap 30 pt");
            view.SetProjectedPixelsPerCell(23.95f);
            Assert.That(label.enabled, Is.True, "n_min of the smallest class: 14.97 pt");
            view.SetProjectedPixelsPerCell(19.0f);
            Assert.That(label.enabled, Is.False, "below 12 pt the digit is hidden; the notice and panel still name the level");
            view.SetProjectedPixelsPerCell(30f);
            Assert.That(label.enabled, Is.True);
            Assert.That(view.RendererOf(3, SlotGridView.SlotRendererRole.Label).enabled, Is.False, "an occupied slot has no label");
        }

        [Test]
        public void ARunningMachineResumesAtTheSharedClockFrameWithoutAPerMachineReset()
        {
            var view = NewView(StandInCatalog());
            var paused = Enumerable.Range(0, 12).Select(i => Occupied(i, MachineVisualState.Paused, false, false, false)).ToArray();
            view.AdvanceFrames(1000, false);
            view.Render(paused);
            var machine = view.RendererOf(0, SlotGridView.SlotRendererRole.Machine);
            Assert.That(machine.sprite.name, Does.EndWith("-0"), "a paused machine shows frame 0");

            view.AdvanceFrames(1000, false); // floor(1.0 s * 8) mod 6 = 2
            Assert.That(machine.sprite.name, Does.EndWith("-0"), "still states never animate");

            var resumed = paused.ToArray();
            resumed[0] = Occupied(0, MachineVisualState.Running, false, false, false);
            view.Render(resumed);
            Assert.That(machine.sprite.name, Does.EndWith("-2"), "resumes at the clock frame, not at frame 0");
            Assert.That(view.RendererOf(0, SlotGridView.SlotRendererRole.Accent).sprite.name, Does.EndWith("-2"), "the accent follows the body");

            view.AdvanceFrames(1125, false);
            Assert.That(machine.sprite.name, Does.EndWith("-3"));
            view.AdvanceFrames(1125, true);
            Assert.That(machine.sprite.name, Does.EndWith("-0"), "reduced motion forces frame 0");
            Assert.That(view.RendererOf(0, SlotGridView.SlotRendererRole.Accent).sprite.name, Does.EndWith("-0"));

            view.Render(paused);
            Assert.That(machine.sprite.name, Does.EndWith("-0"));
        }

        [Test]
        public void TheClockSeamAllocatesNothingOnceWarm()
        {
            var view = NewView(StandInCatalog());
            view.Render(Densest());
            for (var i = 0; i < 20; i++)
            {
                view.AdvanceFrames(i * 130L, false);
            }

            var allocations = GcAllocationCounter.Count(() =>
            {
                for (var i = 0; i < 100; i++)
                {
                    view.AdvanceFrames(5000 + i * 130L, i % 7 == 0);
                }
            });
            Assert.That(allocations, Is.EqualTo(0));
        }

        // ---------------------------------------------------------------------------------------------------
        // Real render: overlays on top, backdrop acceptance (C8), top row not clipped
        // ---------------------------------------------------------------------------------------------------

        public static IEnumerable<float> PixelsPerPointOfTheClasses() => new[] { 1f, 1.68f, 2f, 2.0046f, 2.625f, 3f };

        private sealed class RenderRig
        {
            public Camera Camera;
            public RenderTexture Target;
            public int N;
            public Vector2 CameraPosition;
            public int Width;
            public int Height;

            public Color32[] Capture()
            {
                Camera.Render();
                var previous = RenderTexture.active;
                var readback = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                try
                {
                    RenderTexture.active = Target;
                    readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    readback.Apply(false);
                    return readback.GetPixels32();
                }
                finally
                {
                    RenderTexture.active = previous;
                    Object.DestroyImmediate(readback);
                }
            }

            public Color32 Sample(Color32[] pixels, int gx, int gy)
            {
                var x = Mathf.FloorToInt(Width * 0.5f + ((gx + 0.5f) / D - CameraPosition.x) * N * D);
                var y = Mathf.FloorToInt(Height * 0.5f + ((gy + 0.5f) / D - CameraPosition.y) * N * D);
                return pixels[y * Width + x];
            }
        }

        private IEnumerator NewRender(int columns, int n, Action<RenderRig> ready)
        {
            var extent = SlotGridLayout.Extent(columns);
            var rig = new RenderRig { N = n };
            rig.Width = Mathf.CeilToInt((extent.width + 2f) * D * n);
            rig.Height = Mathf.CeilToInt((extent.height + 2f) * D * n);
            rig.Width += rig.Width & 1;
            rig.Height += rig.Height & 1;
            rig.Target = new RenderTexture(rig.Width, rig.Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default) { antiAliasing = 1, filterMode = FilterMode.Point };
            rig.Target.Create();
            owned.Add(rig.Target);
            var cameraObject = new GameObject("state-cue-camera");
            owned.Add(cameraObject);
            rig.Camera = cameraObject.AddComponent<Camera>();
            rig.Camera.orthographic = true;
            rig.Camera.clearFlags = CameraClearFlags.SolidColor;
            rig.Camera.backgroundColor = Palette()["ui_bg"];
            rig.Camera.allowMSAA = false;
            rig.Camera.allowHDR = false;
            rig.Camera.targetTexture = rig.Target;
            rig.Camera.enabled = false;
            rig.Camera.orthographicSize = CameraScaleModel.OrthographicSize(rig.Height, n, D);
            rig.CameraPosition = new Vector2(
                CameraScaleModel.SnapCoordinate(extent.center.x, rig.Width, n, D),
                CameraScaleModel.SnapCoordinate(extent.center.y, rig.Height, n, D));
            cameraObject.transform.position = new Vector3(rig.CameraPosition.x, rig.CameraPosition.y, -10f);
            for (var i = 0; i < 3; i++)
            {
                rig.Camera.Render();
                yield return null;
            }

            ready(rig);
        }

        private static int NMin(float pixelsPerPoint) => CameraScaleModel.MinScale(new RunZoomSettings().ToScaleSettings(), pixelsPerPoint);

        [UnityTest]
        public IEnumerator EveryOverlayPixelOfTheDensestFrameIsVisibleOnTopAtNMin([ValueSource(nameof(PixelsPerPointOfTheClasses))] float pixelsPerPoint)
        {
            var n = NMin(pixelsPerPoint);
            var view = NewView(StandInCatalog(), 3, n * D / pixelsPerPoint);
            view.Render(Densest());
            RenderRig rig = null;
            yield return NewRender(3, n, r => rig = r);
            var pixels = rig.Capture();
            var hidden = 0;
            var total = 0;
            var details = new List<string>();
            var all = Collect(view, OverlayRoles);
            foreach (var overlay in all)
            {
                foreach (var pair in overlay.Pixels)
                {
                    // The selection frame lies under the slot's own cues (Art Bible 8.5: the frame never covers a badge); where the
                    // badge corner touches the plate edge the badge wins, so those shared pixels are not frame pixels on screen.
                    if (overlay.Role == SlotGridView.SlotRendererRole.Frame &&
                        all.Any(o => o.Slot == overlay.Slot && o.Role != SlotGridView.SlotRendererRole.Frame && o.Pixels.ContainsKey(pair.Key)))
                    {
                        continue;
                    }

                    var gx = (int)(pair.Key >> 24) - 100000;
                    var gy = (int)(pair.Key & 0xFFFFFF) - 100000;
                    var actual = rig.Sample(pixels, gx, gy);
                    total++;
                    if (Mathf.Abs(actual.r - pair.Value.r) > 2 || Mathf.Abs(actual.g - pair.Value.g) > 2 || Mathf.Abs(actual.b - pair.Value.b) > 2)
                    {
                        hidden++;
                        if (details.Count < 4)
                        {
                            details.Add($"slot {overlay.Slot + 1} {overlay.Role} ({gx},{gy}) expected {pair.Value} got {actual}");
                        }
                    }
                }
            }

            Assert.That(total, Is.GreaterThan(1000));
            Assert.That(hidden, Is.EqualTo(0), "overlay pixels covered by other art: " + string.Join("; ", details));
        }

        private List<string> BackdropViolations(SlotGridView view, RenderRig rig, Color32[] pixels)
        {
            var tones = Palette();
            var aOv = new HashSet<string>
            {
                "ink_0", "ink_1", "steel_1", "steel_2", "steel_3", "copper_1", "copper_2", "teal_1", "dim_0", "lit_0", "lit_1",
                "floor_0", "floor_1", "plate_1", "plate_2", "res_ore_1", "res_coal", "res_crystal", "res_leaf", "res_gold",
                "res_water", "res_plank", "ui_bg", "ui_panel", "ui_line", "ui_text",
            };
            var aFr = new HashSet<string>(tones.Keys.Where(k => k != "warn_0" && k != "plate_0" && k != "res_ore_0"));
            var overlays = Collect(view, OverlayRoles);
            var covered = new HashSet<long>(overlays.SelectMany(o => o.Pixels.Keys));
            var violations = new List<string>();
            foreach (var overlay in overlays.Where(o => o.Role != SlotGridView.SlotRendererRole.Label))
            {
                var allowed = overlay.Role == SlotGridView.SlotRendererRole.Frame ? aFr : aOv;
                var ring = new HashSet<long>();
                foreach (var key in overlay.Pixels.Keys)
                {
                    var gx = (int)(key >> 24) - 100000;
                    var gy = (int)(key & 0xFFFFFF) - 100000;
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            var neighbour = Key(gx + dx, gy + dy);
                            if (!overlay.Pixels.ContainsKey(neighbour) && !covered.Contains(neighbour))
                            {
                                ring.Add(neighbour);
                            }
                        }
                    }
                }

                foreach (var key in ring)
                {
                    var color = rig.Sample(pixels, (int)(key >> 24) - 100000, (int)(key & 0xFFFFFF) - 100000);
                    var tone = tones.FirstOrDefault(p => Mathf.Abs(p.Value.r - color.r) <= 2 && Mathf.Abs(p.Value.g - color.g) <= 2 && Mathf.Abs(p.Value.b - color.b) <= 2).Key;
                    if (tone == null || !allowed.Contains(tone))
                    {
                        violations.Add($"slot {overlay.Slot + 1} {overlay.Role}: {tone ?? color.ToString()}");
                    }
                }
            }

            return violations;
        }

        [UnityTest]
        public IEnumerator TheBackdropUnderEveryBadgeMarkerAndFrameLiesInTheAllowedToneSetAtNMin([ValueSource(nameof(PixelsPerPointOfTheClasses))] float pixelsPerPoint, [Values(3, 4)] int columns)
        {
            var n = NMin(pixelsPerPoint);
            var view = NewView(StandInCatalog(), columns, n * D / pixelsPerPoint);
            view.Render(Densest());
            RenderRig rig = null;
            yield return NewRender(columns, n, r => rig = r);

            // World layers without the overlay layer (Art Bible C8).
            var overlayRenderers = OverlayRoles.SelectMany(role => Enumerable.Range(0, 12).Select(slot => view.RendererOf(slot, role))).ToList();
            var enabledBefore = overlayRenderers.Select(r => r.enabled).ToList();
            foreach (var renderer in overlayRenderers)
            {
                renderer.enabled = false;
            }

            var world = rig.Capture();
            for (var i = 0; i < overlayRenderers.Count; i++)
            {
                overlayRenderers[i].enabled = enabledBefore[i];
            }

            var violations = BackdropViolations(view, rig, world);
            Assert.That(violations, Is.Empty, string.Join("; ", violations.Take(5)));

            // Negative control: a forbidden tone (plate_0) behind everything must be reported.
            var tones = Palette();
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, tones["plate_0"]);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            var quad = new GameObject("forbidden-backdrop");
            owned.Add(quad);
            owned.Add(texture);
            owned.Add(sprite);
            var quadRenderer = quad.AddComponent<SpriteRenderer>();
            quadRenderer.sprite = sprite;
            quadRenderer.sortingOrder = -100;
            quad.transform.position = new Vector3(SlotGridLayout.Extent(columns).center.x, SlotGridLayout.Extent(columns).center.y, 0f);
            quad.transform.localScale = new Vector3(40f, 40f, 1f);
            foreach (var renderer in overlayRenderers)
            {
                renderer.enabled = false;
            }

            var forbidden = rig.Capture();
            for (var i = 0; i < overlayRenderers.Count; i++)
            {
                overlayRenderers[i].enabled = enabledBefore[i];
            }

            Assert.That(BackdropViolations(view, rig, forbidden), Is.Not.Empty, "a forbidden backdrop tone fails the acceptance");
        }

        public static IEnumerable<object[]> TopRowClasses()
        {
            yield return new object[] { 720, 1280, 2f, 3, 160 };
            yield return new object[] { 1280, 720, 2.0046f, 4, 96 };
            yield return new object[] { 1536, 2048, 1.65f, 3, 140 };
        }

        [UnityTest]
        public IEnumerator TheTopRowsOverlaysAreNotClippedByTheHudEdge([ValueSource(nameof(TopRowClasses))] object[] device)
        {
            int width = (int)device[0], height = (int)device[1], columns = (int)device[3], hud = (int)device[4];
            var pixelsPerPoint = (float)device[2];
            var view = NewView(StandInCatalog(), columns);
            view.Render(Densest());

            var target = new RenderTexture(width, height, 24);
            target.Create();
            owned.Add(target);
            var cameraObject = new GameObject("clip-camera");
            owned.Add(cameraObject);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.targetTexture = target;
            camera.enabled = false;
            var controller = cameraObject.AddComponent<FactoryCameraController>();
            controller.Configure(camera, new RunZoomSettings().ToScaleSettings());
            controller.Fit(new ScaleLayout(width, height, pixelsPerPoint, new Rect(0f, 0f, width, height - hud), columns));
            yield return null;

            var model = controller.Model;
            Assert.That(controller.Scale, Is.GreaterThanOrEqualTo(model.LowerBound));
            var top = float.MinValue;
            foreach (var overlay in Collect(view, OverlayRoles).Where(o => o.Slot < columns))
            {
                foreach (var key in overlay.Pixels.Keys)
                {
                    var world = new Vector2(((int)(key >> 24) - 100000) / (float)D, ((int)(key & 0xFFFFFF) - 100000 + 1) / (float)D);
                    top = Mathf.Max(top, model.WorldToScreen(world, model.Position, model.Scale).y);
                }
            }

            Assert.That(top, Is.LessThanOrEqualTo(height - hud + 0.5f), "the top row's highest overlay pixel stays below the HUD edge");
            Assert.That(top, Is.GreaterThan(0f));
        }

        // ---------------------------------------------------------------------------------------------------
        // Locked slot names the level at every class
        // ---------------------------------------------------------------------------------------------------

        public static IEnumerable<ScreenClassScalePlayTests.ScaleDevice> Devices() => ScreenClassScalePlayTests.Devices();

        [UnityTest]
        public IEnumerator ATapOnALockedSlotNamesTheUnlockingLevelAtEveryScreenClassAndTextScale(
            [ValueSource(nameof(Devices))] ScreenClassScalePlayTests.ScaleDevice device, [Values(100, 200)] int textScale)
        {
            var profile = ShellDeviceProfile.Create(device.Touch ? ShellPrimaryInput.Touch : ShellPrimaryInput.Pointer, device.Width, device.Height, device.Dpi);
            var screen = new ShellScreen(device.Name, device.Width, device.Height, 0, 0, 0, 0, device.Layout);
            shellRig = ShellRig.CreateAuthored(screen, profile, textScale);
            yield return shellRig.Layout();
            Assert.That(shellRig.Shell.OpenSlot(11, SlotCondition.Locked, 4), Is.False);
            yield return ShellRig.Settle();
            Assert.That(shellRig.Shell.NoticeText, Does.Contain("level 4"));
            Assert.That(shellRig.Shell.NoticeKind, Is.EqualTo(ShellNoticeKind.Lock));
            var bar = shellRig.Shell.NoticeBar;
            Assert.That(bar.resolvedStyle.display, Is.Not.EqualTo(UnityEngine.UIElements.DisplayStyle.None));
            var root = shellRig.Root.worldBound;
            Assert.That(root.Contains(bar.worldBound.center), Is.True, "the notice is on screen at " + device.Name + " " + textScale + " %");
        }
    }
}
#endif
