using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Unity.Presentation;
using UnityEngine;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>The layers of the world composite, bottom to top.</summary>
    internal enum DeckLayer : byte
    {
        None = 0,
        Base,
        Deck,
        Prop,
        Edge,
        Link,
        Deposit,
        Depot,
        Plate,
        Machine,
        Accent,
    }

    [Serializable]
    internal sealed class ManifestFrameJson
    {
        public string file;
    }

    [Serializable]
    internal sealed class ManifestEntryJson
    {
        public string key;
        public ManifestFrameJson[] frames;
    }

    [Serializable]
    internal sealed class ManifestJson
    {
        public ManifestEntryJson[] entries;
    }

    [Serializable]
    internal sealed class PaletteColorJson
    {
        public string id;
        public string hex;
    }

    [Serializable]
    internal sealed class PaletteGroupJson
    {
        public PaletteColorJson[] colors;
    }

    [Serializable]
    internal sealed class PaletteFileJson
    {
        public PaletteGroupJson[] groups;
    }

    /// <summary>The F004 sprites of the shipped manifest by catalog key, and the palette tone of a colour.</summary>
    internal static class ShippedArt
    {
        public const string F004Root = "Assets/IdleFactory/Art/F004";
        private static Dictionary<string, string> f004Paths;
        private static Dictionary<int, string> toneOf;

        public static SpritePixels F004(string key, int frame = 0)
        {
            if (f004Paths == null)
            {
                var manifest = JsonUtility.FromJson<ManifestJson>(File.ReadAllText(F004Root + "/import-manifest.json"));
                f004Paths = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var entry in manifest.entries)
                {
                    if (entry.frames != null && entry.frames.Length > 0)
                    {
                        f004Paths[entry.key] = F004Root + "/" + entry.frames[0].file;
                    }
                }
            }

            var path = f004Paths[key];
            if (frame != 0)
            {
                path = path.Replace("_00.png", "_" + frame.ToString("00") + ".png");
            }

            return DeckTestSupport.Pixels(path);
        }

        public static string ToneOf(Color32 colour)
        {
            if (toneOf == null)
            {
                toneOf = new Dictionary<int, string>();
                var palette = JsonUtility.FromJson<PaletteFileJson>(File.ReadAllText("tools/art-pipeline/palette/palette-v1.json"));
                foreach (var group in palette.groups)
                {
                    foreach (var color in group.colors)
                    {
                        var rgb = (Convert.ToInt32(color.hex.Substring(1, 2), 16) << 16) | (Convert.ToInt32(color.hex.Substring(3, 2), 16) << 8) | Convert.ToInt32(color.hex.Substring(5, 2), 16);
                        toneOf[rgb] = color.id;
                    }
                }
            }

            return toneOf.TryGetValue((colour.r << 16) | (colour.g << 8) | colour.b, out var id) ? id : "other";
        }

        /// <summary>WCAG relative luminance of an sRGB colour in [0, 1].</summary>
        private const double SrgbLowDivisor = 1292.0 / 100.0;

        public static double Luminance(Color32 c)
        {
            double Lin(byte v)
            {
                var s = v / 255.0;
                return s <= 0.04045 ? s / SrgbLowDivisor : Math.Pow((s + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Lin(c.r) + 0.7152 * Lin(c.g) + 0.0722 * Lin(c.b);
        }
    }

    /// <summary>
    /// A CPU composite of the world layers on real pixels (T527 proofs): the base and deck tiles, props, edge pieces, links, deposit, depot, then the F004
    /// plates, machine bodies and accents, in sort order. Overlays (badge with the K4 tail, markers, selection frame) are NOT drawn into it: they are masks,
    /// because the C8 ring is the backdrop around them (Art Bible 9.4, 21.5). <see cref="TopAt"/> says which layer owns a pixel.
    /// </summary>
    internal sealed class DeckComposite
    {
        public const int Pad = 8;
        private static readonly string[] Machines = { "MINER", "SMELTER", "PRESS", "ASSEMBLER" };
        private static readonly string[] Badges = { "WAITING", "MISSING-INPUT", "STORAGE-FULL", "PAUSED", "INACTIVE" };

        private readonly Color32[] colour;
        private readonly DeckLayer[] top;

        public DeckComposite(FactoryWorldLayout layout, DeckPlan plan, string depotKey, string depositKey, bool withLinks, IReadOnlyList<SlotCondition> conditions = null)
        {
            Layout = layout;
            Plan = plan;
            var rect = PixelRect.FromWorld(layout.Bounds);
            foreach (var cell in plan.BaseCells)
            {
                rect = Union(rect, DeckPlanner.CellRect(cell));
            }

            foreach (var edge in plan.Edges)
            {
                rect = Union(rect, edge.Rect);
            }

            DeckSpriteSize.TryOf(depotKey, out var depotSize);
            DepotRect = depotSize.RectAt(new Vector2Int(Mathf.RoundToInt(layout.DepotAnchor.x * 24f), Mathf.RoundToInt(layout.DepotAnchor.y * 24f)));
            rect = Union(rect, DepotRect);
            Area = rect.Grown(Pad);
            Width = Area.Width;
            Height = Area.Height;
            colour = new Color32[Width * Height];
            top = new DeckLayer[Width * Height];

            var ground = DeckTestSupport.PixelsOfKey("F006-GROUND-BASE");
            // T540: a lit ring cell shows the deck tile in place of the dark base tile (the stage-driven deck-lit area).
            var lit = new HashSet<Vector2Int>(plan.LitRingCells);
            var litTile = DeckTestSupport.PixelsOfKey("F006-GROUND-DECK");
            foreach (var cell in plan.BaseCells)
            {
                Draw(lit.Contains(cell) ? DeckLayer.Deck : DeckLayer.Base, lit.Contains(cell) ? litTile : ground, DeckPlanner.CellRect(cell).X, DeckPlanner.CellRect(cell).Y);
            }

            for (var i = 0; i < plan.LocationCells.Count; i++)
            {
                var key = plan.LocationArt[i] == DeckCellArt.Hatch ? "F006-GROUND-HATCH" : "F006-GROUND-DECK";
                var cell = DeckPlanner.CellRect(plan.LocationCells[i]);
                Draw(DeckLayer.Deck, DeckTestSupport.PixelsOfKey(key), cell.X, cell.Y);
            }

            foreach (var prop in plan.Props)
            {
                Draw(DeckLayer.Prop, DeckTestSupport.PixelsOfKey(prop.Key), prop.Rect.X, prop.Rect.Y);
            }

            foreach (var edge in plan.Edges.OrderBy(e => e.Order))
            {
                Draw(DeckLayer.Edge, DeckTestSupport.PixelsOfKey(edge.Key), edge.Rect.X, edge.Rect.Y);
            }

            if (withLinks)
            {
                DrawLinks();
            }

            DeckSpriteSize.TryOf(depositKey, out var depositSize);
            var depositAnchor = new Vector2Int(Mathf.RoundToInt(layout.DepositAnchor.x * 24f), Mathf.RoundToInt(layout.DepositAnchor.y * 24f));
            DepositRect = depositSize.RectAt(depositAnchor);
            Draw(DeckLayer.Deposit, DeckTestSupport.PixelsOfKey(depositKey), DepositRect.X, DepositRect.Y);
            Draw(DeckLayer.Depot, DeckTestSupport.PixelsOfKey(depotKey), DepotRect.X, DepotRect.Y);

            var plate = ShippedArt.F004("F003-SLOT-OCCUPIED-BASE");
            for (var i = 0; i < layout.LocationCount; i++)
            {
                if (conditions != null && (i >= conditions.Count || conditions[i] != SlotCondition.Occupied))
                {
                    continue;
                }

                var pivot = Pivot(i);
                Draw(DeckLayer.Plate, plate, pivot.x - 40, pivot.y - 15);
                Draw(DeckLayer.Machine, ShippedArt.F004("F003-MACHINE-" + Machines[i % 4]), pivot.x - 40, pivot.y - 15);
                if (i % 2 == 0)
                {
                    Draw(DeckLayer.Accent, ShippedArt.F004("F004-ACCENT-" + Machines[i % 4]), pivot.x - 40, pivot.y - 15);
                }
            }
        }

        public FactoryWorldLayout Layout { get; }
        public DeckPlan Plan { get; }
        public PixelRect Area { get; }
        public int Width { get; }
        public int Height { get; }
        public PixelRect DepotRect { get; }
        public PixelRect DepositRect { get; }

        public Vector2Int Pivot(int location) => DeckPlanner.CellPivot(Layout.CellOf(new BuildingLocationId(location)));

        public bool Inside(int worldX, int worldY) => worldX >= Area.X && worldX < Area.XMax && worldY >= Area.Y && worldY < Area.YMax;

        public DeckLayer TopAt(int worldX, int worldY) => Inside(worldX, worldY) ? top[(worldY - Area.Y) * Width + (worldX - Area.X)] : DeckLayer.None;

        public Color32 PixelAt(int worldX, int worldY) => Inside(worldX, worldY) ? colour[(worldY - Area.Y) * Width + (worldX - Area.X)] : default;

        private static PixelRect Union(PixelRect a, PixelRect b)
        {
            var x0 = Mathf.Min(a.X, b.X);
            var y0 = Mathf.Min(a.Y, b.Y);
            return new PixelRect(x0, y0, Mathf.Max(a.XMax, b.XMax) - x0, Mathf.Max(a.YMax, b.YMax) - y0);
        }

        private void Draw(DeckLayer layer, SpritePixels sprite, int worldX, int worldY)
        {
            for (var y = 0; y < sprite.Height; y++)
            {
                for (var x = 0; x < sprite.Width; x++)
                {
                    var p = sprite.Pixels[y * sprite.Width + x];
                    if (p.a == 0 || !Inside(worldX + x, worldY + y))
                    {
                        continue;
                    }

                    var index = (worldY + y - Area.Y) * Width + (worldX + x - Area.X);
                    colour[index] = p;
                    top[index] = layer;
                }
            }
        }

        // The link layer as the art pipeline's ring proof places it (art_pipeline/f006/link_fx.py _link_layer): per occupied row, trunk tiles from the row's
        // far end to the depot column with a junction at the depot column, a branch tile on every occupied cell; every tile is centred 12 art px above its cell pivot.
        private void DrawLinks()
        {
            var depotColumn = Layout.DepotCell.x;
            var occupied = new HashSet<Vector2Int>();
            for (var i = 0; i < Layout.LocationCount; i++)
            {
                occupied.Add(Layout.CellOf(new BuildingLocationId(i)));
            }

            for (var row = 0; row < Layout.RowCount; row++)
            {
                var columns = occupied.Where(c => c.y == row).Select(c => c.x).ToList();
                if (columns.Count == 0)
                {
                    continue;
                }

                var low = Layout.DepotOnRight ? columns.Min() : depotColumn;
                var high = Layout.DepotOnRight ? depotColumn : columns.Max();
                for (var column = low; column <= high; column++)
                {
                    var key = column == depotColumn ? "F006-LINK-JUNCTION" : occupied.Contains(new Vector2Int(column, row)) ? "F006-LINK-BRANCH" : "F006-LINK-TRUNK";
                    var pivot = DeckPlanner.CellPivot(new Vector2Int(column, row));
                    Draw(DeckLayer.Link, DeckTestSupport.PixelsOfKey(key), pivot.x - 35, pivot.y + 12 - 27);
                }
            }
        }

        // ---- overlays as masks ---------------------------------------------------------------------------------------------------

        /// <summary>The opaque pixels of an F004 canvas-layer overlay sprite placed on a location pivot, in world pixels.</summary>
        public HashSet<Vector2Int> OverlayMask(string f004Key, int location)
        {
            var sprite = ShippedArt.F004(f004Key);
            var pivot = Pivot(location);
            var mask = new HashSet<Vector2Int>();
            for (var y = 0; y < sprite.Height; y++)
            {
                for (var x = 0; x < sprite.Width; x++)
                {
                    if (sprite.IsOpaque(x, y))
                    {
                        mask.Add(new Vector2Int(pivot.x - 40 + x, pivot.y - 15 + y));
                    }
                }
            }

            return mask;
        }

        public static string BadgeKeyOf(int location) => "F003-BADGE-" + Badges[location % Badges.Length];

        public static HashSet<Vector2Int> Ring(HashSet<Vector2Int> mask)
        {
            var ring = new HashSet<Vector2Int>();
            foreach (var p in mask)
            {
                foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    var q = p + d;
                    if (!mask.Contains(q))
                    {
                        ring.Add(q);
                    }
                }
            }

            return ring;
        }

        /// <summary>Writes the composite with the overlays drawn on top (the densest frame: a badge with the K4 tail and both markers on every location, one selection frame), scaled up.</summary>
        public void WritePng(string path, int scale, int selectedLocation)
        {
            var pixels = new Color32[Width * Height];
            Array.Copy(colour, pixels, pixels.Length);
            for (var i = 0; i < pixels.Length; i++)
            {
                if (top[i] == DeckLayer.None)
                {
                    pixels[i] = new Color32(17, 24, 39, 255);
                }
            }

            void Over(string key, int location)
            {
                var sprite = ShippedArt.F004(key);
                var pivot = Pivot(location);
                for (var y = 0; y < sprite.Height; y++)
                {
                    for (var x = 0; x < sprite.Width; x++)
                    {
                        var p = sprite.Pixels[y * sprite.Width + x];
                        var wx = pivot.x - 40 + x;
                        var wy = pivot.y - 15 + y;
                        if (p.a != 0 && Inside(wx, wy))
                        {
                            pixels[(wy - Area.Y) * Width + (wx - Area.X)] = p;
                        }
                    }
                }
            }

            for (var i = 0; i < Layout.LocationCount; i++)
            {
                Over(BadgeKeyOf(i), i);
                Over("F003-MARKER-BOOSTED", i);
                Over("F003-MARKER-OVERDRIVE", i);
            }

            Over("F003-SLOT-SELECTED-FRAME", selectedLocation);
            var texture = new Texture2D(Width * scale, Height * scale, TextureFormat.RGBA32, false);
            try
            {
                var scaled = new Color32[Width * scale * Height * scale];
                for (var y = 0; y < Height * scale; y++)
                {
                    for (var x = 0; x < Width * scale; x++)
                    {
                        scaled[y * Width * scale + x] = pixels[(y / scale) * Width + x / scale];
                    }
                }

                texture.SetPixels32(scaled);
                texture.Apply(false);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(texture));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
