#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.OfflineSummaryPanelTestSupport;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T441 (R13, OD-17): prepares the independent blind-naming material. It names nothing and judges nothing. For every Feature 003 surface it
    /// captures the in-context icons at 24 pt and at 200 % text, with the label visible and hidden, cuts one tile per icon, shuffles the tiles
    /// into numbered contact sheets, and writes the answer key to a separate file. It also writes the shuffled unlabelled sheet of the confusable
    /// set and the in-context captures (locked plates L2 to L5, the cash icon) for the Art Direction review. Output: <c>test-results/t441/</c>
    /// (evidence only, never committed); set <c>IDLEFACTORY_T441_OUT</c> to write elsewhere.
    /// </summary>
    public sealed class UiIconBlindNamingCaptures
    {
        private const int Seed = 441;
        private const int PhoneWidth = 390;
        private const int PhoneHeight = 844;
        private static readonly string[] Percents = { "100", "200" };

        // OD-17: the ring cluster (20 pairs above IoU 0.70) and the blind misses of the art-direction review (F-06 clusters), with their brief peers.
        private static readonly string[] RingCluster =
        {
            "F003-UI-ICON-INFO", "F003-UI-ICON-CASH", "F003-UI-ICON-TIMER", "F003-UI-ICON-ACTIVATE",
            "F003-UI-ICON-RESEARCH-NODE-LOCKED", "F003-UI-ICON-RESEARCH-NODE-AVAILABLE", "F003-UI-ICON-RESEARCH-NODE-OWNED",
            "F003-UI-ICON-RESEARCH-NODE-MAXED", "F003-UI-ICON-OVERDRIVE-ACTIVE",
        };

        private static readonly string[] BlindMisses =
        {
            "F003-UI-ICON-ACCEPT", "F003-UI-ICON-TAB-PRESTIGE", "F003-UI-ICON-EFFECT-OUTPUT", "F003-UI-ICON-CLOSE", "F003-UI-ICON-IP",
            "F003-UI-ICON-TAB-MARKET", "F003-UI-ICON-FV", "F003-UI-ICON-ABANDON", "F003-UI-ICON-AUTOSELL", "F003-UI-ICON-PRIORITY-DOWN",
            "F003-UI-ICON-STORAGE", "F003-UI-ICON-INDUSTRY",
        };

        private static readonly string[] Peers =
        {
            "F003-UI-ICON-DELIVER", "F003-UI-ICON-IMPORT", "F003-UI-ICON-UPGRADE", "F003-UI-ICON-PRIORITY-UP", "F003-UI-ICON-RETRY",
            "F003-UI-ICON-SPINNER", "F003-UI-ICON-TAB-FACTORY", "F003-UI-ICON-TAB-CONTRACTS", "F003-UI-ICON-WARNING", "F004-UI-ICON-SETTINGS",
            "F003-UI-ICON-BUILD", "F003-UI-ICON-RP", "F003-UI-ICON-OVERDRIVE-READY", "F003-UI-ICON-LOCK", "F003-UI-ICON-EFFECT-UPGRADE-COST",
        };

        [Serializable]
        private sealed class KeyEntry
        {
            public string sheet;
            public int number;
            public string key;
            public string surface;
            public string textScale;
            public string label;
            public string element;
            public bool ringCluster;
        }

        [Serializable]
        private sealed class AnswerKey
        {
            public string note = "Answer key for the T441 blind-naming sheets. Do not show it to the namer.";
            public int seed = Seed;
            public List<KeyEntry> entries = new List<KeyEntry>();
        }

        private sealed class Tile
        {
            public Texture2D Image;
            public KeyEntry Entry;
        }

        private readonly List<IDisposable> owned = new List<IDisposable>();
        private string outputRoot;

        [TearDown]
        public void TearDown()
        {
            foreach (var item in owned)
            {
                item.Dispose();
            }

            owned.Clear();
        }

        private static string Output()
        {
            var configured = Environment.GetEnvironmentVariable("IDLEFACTORY_T441_OUT");
            var folder = string.IsNullOrEmpty(configured) ? UiIconIntegrationTests.EvidenceFolder() : configured;
            Directory.CreateDirectory(Path.Combine(folder, "sheets"));
            Directory.CreateDirectory(Path.Combine(folder, "key"));
            Directory.CreateDirectory(Path.Combine(folder, "ad-review"));
            Directory.CreateDirectory(Path.Combine(folder, "surfaces"));
            return folder;
        }

        private static IEnumerator Snapshot(ShellRig rig, Action<Texture2D> done)
        {
            rig.Document.panelSettings.clearColor = true;
            rig.Document.panelSettings.colorClearValue = new Color(0.07f, 0.09f, 0.15f, 1f);
            yield return ShellRig.Settle();
            yield return ShellRig.Settle();
            var texture = rig.Texture;
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            done(image);
        }

        private static void HideText(VisualElement root, bool hide)
        {
            foreach (var text in root.Query<TextElement>().ToList())
            {
                text.style.color = hide ? new StyleColor(Color.clear) : new StyleColor(StyleKeyword.Null);
            }
        }

        private static Texture2D Crop(Texture2D source, Rect panelRect, float pixelsPerPoint, float padLeft, float padTop, float padRight, float padBottom)
        {
            var x0 = Mathf.Max(0, Mathf.FloorToInt(panelRect.xMin * pixelsPerPoint - padLeft));
            var x1 = Mathf.Min(source.width, Mathf.CeilToInt(panelRect.xMax * pixelsPerPoint + padRight));
            var yTop = Mathf.Max(0, Mathf.FloorToInt(panelRect.yMin * pixelsPerPoint - padTop));
            var yBottom = Mathf.Min(source.height, Mathf.CeilToInt(panelRect.yMax * pixelsPerPoint + padBottom));
            var width = Mathf.Max(1, x1 - x0);
            var height = Mathf.Max(1, yBottom - yTop);
            var tile = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tile.SetPixels(source.GetPixels(x0, source.height - yBottom, width, height));
            tile.Apply();
            return tile;
        }

        private static void Save(Texture2D texture, string path) => File.WriteAllBytes(path, ImageConversion.EncodeToPNG(texture));

        // ---------------------------------------------------------------------------------------------------
        // Contact sheets
        // ---------------------------------------------------------------------------------------------------

        private IEnumerator RenderSheet(string path, IList<Tile> tiles, int columns, int scale, int cellWidth, int cellHeight, AnswerKey key, string sheetName)
        {
            var rows = Mathf.CeilToInt(tiles.Count / (float)columns);
            var width = columns * cellWidth + 16;
            var height = rows * cellHeight + 16;
            var screen = new ShellScreen("sheet", width, height, 0, 0, 0, 0, FactoryHudLayout.Desktop);
            var rig = ShellRig.Create(screen);
            owned.Add(rig);
            yield return rig.Layout();
            var overlay = new VisualElement { name = "sheet" };
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0f;
            overlay.style.top = 0f;
            overlay.style.right = 0f;
            overlay.style.bottom = 0f;
            overlay.style.backgroundColor = new Color(0.07f, 0.09f, 0.15f, 1f);
            overlay.style.flexDirection = FlexDirection.Row;
            overlay.style.flexWrap = Wrap.Wrap;
            overlay.style.alignContent = Align.FlexStart;
            overlay.style.paddingLeft = 8f;
            overlay.style.paddingTop = 8f;
            for (var i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                tile.Entry.sheet = sheetName;
                tile.Entry.number = i + 1;
                key.entries.Add(tile.Entry);
                var cell = new VisualElement();
                cell.style.width = cellWidth;
                cell.style.height = cellHeight;
                var number = new Label((i + 1).ToString());
                number.style.color = new Color(1f, 0.85f, 0.2f, 1f);
                number.style.fontSize = Mathf.Max(11, 4 * scale);
                number.style.height = Mathf.Max(14, 5 * scale);
                var picture = new VisualElement();
                picture.style.width = tile.Image.width * scale;
                picture.style.height = tile.Image.height * scale;
                picture.style.backgroundImage = new StyleBackground(tile.Image);
                picture.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
                cell.Add(number);
                cell.Add(picture);
                overlay.Add(cell);
            }

            rig.Root.Add(overlay);
            Texture2D shot = null;
            yield return Snapshot(rig, image => shot = image);
            Save(shot, path);
            UnityEngine.Object.DestroyImmediate(shot);
            overlay.RemoveFromHierarchy();
        }

        private static List<T> Shuffle<T>(IEnumerable<T> items, System.Random random)
        {
            var list = items.ToList();
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                var swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }

            return list;
        }

        // ---------------------------------------------------------------------------------------------------
        // The capture run
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator PrepareTheBlindNamingSheetsAndTheArtDirectionCaptures()
        {
            outputRoot = Output();
            var answers = new AnswerKey();
            var random = new System.Random(Seed);
            var ring = new HashSet<string>(RingCluster);
            var screen = new ShellScreen("phone-portrait 390x844", PhoneWidth, PhoneHeight, 0, 47, 0, 34, FactoryHudLayout.CompactPortrait);

            // 1. In-context tiles per surface, text scale and label state.
            var tilesByVariant = new Dictionary<string, List<Tile>>();
            foreach (var percent in Percents)
            {
                var rig = UiIconSurfaceRig.Create(screen, int.Parse(percent));
                owned.Add(rig);
                yield return rig.Rig.Layout();
                var seen = new HashSet<string>();
                foreach (var surface in UiIconSurfaceRig.Surfaces())
                {
                    yield return rig.Show(surface);
                    foreach (var hidden in new[] { false, true })
                    {
                        var labelState = hidden ? "hidden" : "visible";
                        HideText(rig.Rig.Root, hidden);
                        Texture2D shot = null;
                        yield return Snapshot(rig.Rig, image => shot = image);
                        Save(shot, Path.Combine(outputRoot, "surfaces", $"{surface}-{percent}pct-label-{labelState}.png"));
                        var variant = $"{percent}pct-label-{labelState}";
                        if (!tilesByVariant.TryGetValue(variant, out var tiles))
                        {
                            tilesByVariant[variant] = tiles = new List<Tile>();
                        }

                        foreach (var hit in UiIconScan.Scan(rig.Rig.Root))
                        {
                            // The tab bar, the strip and the sheet header repeat on every surface: one tile each.
                            var chrome = hit.Element.name.StartsWith("tab-", StringComparison.Ordinal) || hit.Element.name.EndsWith("-icon", StringComparison.Ordinal) || hit.Element.name.EndsWith("-pin-hint", StringComparison.Ordinal);
                            var identity = chrome
                                ? $"{hit.Key}|{hit.Element.name}|{labelState}|{percent}"
                                : $"{surface}|{hit.Key}|{hit.Element.name}|{labelState}|{percent}";
                            if (!seen.Add(identity))
                            {
                                continue;
                            }

                            var rightPad = hidden ? 10f : 120f;
                            var image = Crop(shot, hit.IconRect, 1f, 10f, 10f, rightPad, 10f);
                            tiles.Add(new Tile
                            {
                                Image = image,
                                Entry = new KeyEntry
                                {
                                    key = hit.Key,
                                    surface = surface.ToString(),
                                    textScale = percent,
                                    label = labelState,
                                    element = hit.Element.name,
                                    ringCluster = ring.Contains(hit.Key),
                                },
                            });
                        }

                        UnityEngine.Object.DestroyImmediate(shot);
                        HideText(rig.Rig.Root, false);
                    }
                }
            }

            // 2. The Offline and Start-up surfaces (standalone, no shell).
            foreach (var percent in Percents)
            {
                var run = StatusPersistenceRig.Create(TempRoot(), new StatusTestClock());
                var catalog = RunShellFixtures.ShippedCatalog;
                foreach (var surfaceName in new[] { "Offline", "Startup" })
                {
                    var standalone = new StandaloneRig(screen);
                    owned.Add(standalone);
                    StatusSurface panel;
                    if (surfaceName == "Offline")
                    {
                        var offline = new OfflineSummaryPanel(catalog, null, Styles("OfflineSummaryPanel.uss"), UiIconSprites.Load);
                        yield return standalone.Mount(offline, int.Parse(percent));
                        offline.ShowSummary(NormalReturn());
                        panel = offline;
                    }
                    else
                    {
                        var startup = new StartupStatusPanel(run.Persistence, run.Legacy, Styles("StartupStatusPanel.uss"), UiIconSprites.Load, () => true);
                        yield return standalone.Mount(startup, int.Parse(percent));
                        startup.ShowImporting();
                        panel = startup;
                    }

                    yield return ShellRig.Settle();
                    foreach (var hidden in new[] { false, true })
                    {
                        var labelState = hidden ? "hidden" : "visible";
                        HideText(standalone.Root, hidden);
                        yield return ShellRig.Settle();
                        var texture = standalone.Texture;
                        standalone.Document.panelSettings.clearColor = true;
                        standalone.Document.panelSettings.colorClearValue = new Color(0.07f, 0.09f, 0.15f, 1f);
                        yield return ShellRig.Settle();
                        yield return ShellRig.Settle();
                        var previous = RenderTexture.active;
                        RenderTexture.active = texture;
                        var shot = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                        shot.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                        shot.Apply();
                        RenderTexture.active = previous;
                        Save(shot, Path.Combine(outputRoot, "surfaces", $"{surfaceName}-{percent}pct-label-{labelState}.png"));
                        var variant = $"{percent}pct-label-{labelState}";
                        if (!tilesByVariant.TryGetValue(variant, out var tiles))
                        {
                            tilesByVariant[variant] = tiles = new List<Tile>();
                        }

                        foreach (var hit in UiIconScan.Scan(standalone.Root))
                        {
                            tiles.Add(new Tile
                            {
                                Image = Crop(shot, hit.IconRect, 1f, 10f, 10f, hidden ? 10f : 120f, 10f),
                                Entry = new KeyEntry
                                {
                                    key = hit.Key,
                                    surface = surfaceName,
                                    textScale = percent,
                                    label = labelState,
                                    element = hit.Element.name,
                                    ringCluster = ring.Contains(hit.Key),
                                },
                            });
                        }

                        UnityEngine.Object.DestroyImmediate(shot);
                        HideText(standalone.Root, false);
                    }

                    panel.Unmount();
                }
            }

            // 3. Shuffled numbered contact sheets of the in-context tiles.
            foreach (var pair in tilesByVariant.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var labelled = pair.Key.EndsWith("visible", StringComparison.Ordinal);
                var tiles = Shuffle(pair.Value, random);
                var scale = labelled ? 3 : 4;
                var columns = labelled ? 3 : 7;
                var cellW = labelled ? 3 * (24 + 130) + 10 : 4 * 44 + 10;
                var cellH = labelled ? 3 * 44 + 30 : 4 * 44 + 30;
                var page = 0;
                for (var offset = 0; offset < tiles.Count; offset += columns * 6)
                {
                    page++;
                    var slice = tiles.Skip(offset).Take(columns * 6).ToList();
                    var name = $"in-context-{pair.Key}-p{page}";
                    yield return RenderSheet(Path.Combine(outputRoot, "sheets", name + ".png"), slice, columns, scale, cellW, cellH, answers, name);
                }

            }

            // 4. The unlabelled shuffled sheet of the confusable set at 24 pt (native and x4).
            var confusable = RingCluster.Concat(BlindMisses).Concat(Peers).Distinct().ToList();
            foreach (var scale in new[] { 1, 4 })
            {
                var iconTiles = Shuffle(confusable, new System.Random(Seed + scale)).Select(iconKey =>
                {
                    var sprite = UiIconSprites.Load(iconKey);
                    Assert.That(sprite, Is.Not.Null, iconKey);
                    return new Tile
                    {
                        Image = TintedTile(sprite),
                        Entry = new KeyEntry
                        {
                            key = iconKey,
                            surface = "unlabelled-sheet",
                            textScale = "n/a",
                            label = "none",
                            element = "sprite",
                            ringCluster = ring.Contains(iconKey),
                        },
                    };
                }).ToList();
                var name = $"unlabelled-confusable-24pt-x{scale}";
                var columns = scale == 1 ? 14 : 7;
                var cellW = 24 * scale + 16;
                var cellH = 24 * scale + 16 + (scale == 1 ? 16 : 26);
                yield return RenderSheet(Path.Combine(outputRoot, "sheets", name + ".png"), iconTiles, columns, scale, cellW, cellH, answers, name);
            }

            File.WriteAllText(Path.Combine(outputRoot, "key", "answer-key.json"), JsonUtility.ToJson(answers, true));

            // 5. Art Direction review: the locked plates L2 to L5 and the cash icon in context (carried from T440 / #553).
            foreach (var percent in new[] { 100, 200 })
            {
                var session = RunShellFixtures.NewSession(out var commands, factoryLevel: 1, cash: RunShellFixtures.ComfortableCash, unlockEverything: true);
                Assert.That(commands.Build(0, "machine.miner").IsSuccess, Is.True);
                foreach (var captureScreen in new[]
                {
                    new ShellScreen("phone-portrait 390x844", 390, 844, 0, 47, 0, 34, FactoryHudLayout.CompactPortrait),
                    new ShellScreen("scene 640x480", 640, 480, 0, 0, 0, 0, FactoryHudDocument.Classify(640, 480)),
                    new ShellScreen("desktop 1280x720", 1280, 720, 0, 0, 0, 0, FactoryHudLayout.Desktop),
                })
                {
                    Texture2D composite = null;
                    yield return RenderCaptureHarness.CaptureComposite(captureScreen, session, commands, percent, image => composite = image);
                    Save(composite, Path.Combine(outputRoot, "ad-review", $"locked-plates-and-cash-{captureScreen.Width}x{captureScreen.Height}-{percent}pct.png"));
                    UnityEngine.Object.DestroyImmediate(composite);
                }
            }
        }

        private static Texture2D TintedTile(Sprite sprite)
        {
            // The sprite on the surface token, tinted by --text-primary exactly as the UI shows it, at native size (the sheet scales it).
            var source = sprite.texture;
            var rect = sprite.textureRect;
            var readable = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, readable);
            var previous = RenderTexture.active;
            RenderTexture.active = readable;
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(readable);
            var tint = new Color32(0xE5, 0xE7, 0xEB, 255);
            var background = new Color32(0x1F, 0x29, 0x37, 255);
            var pixels = copy.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
            var result = new Color[pixels.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                var shown = new Color(pixel.r * tint.r / 255f, pixel.g * tint.g / 255f, pixel.b * tint.b / 255f, 1f);
                result[i] = Color.Lerp((Color)background, shown, pixel.a);
            }

            var tile = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tile.SetPixels(result);
            tile.Apply();
            UnityEngine.Object.DestroyImmediate(copy);
            return tile;
        }
    }
}
#endif
