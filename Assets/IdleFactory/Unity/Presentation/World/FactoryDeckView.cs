using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// The factory deck (T527, Feature 006, research R06, contracts/world-presentation.md section 1): a rectangle <see cref="UnityEngine.Grid"/> of 70 x 54
    /// art px cells (70/24 x 54/24 world units, not an isometric grid) with two Tilemaps, <c>WorldBase</c> (the dark base field under every deck cell)
    /// and <c>WorldDeck</c> (a deck tile on every unlocked location, a hatch tile on every locked one, the §8.5 padlock and level digit staying the
    /// slot's label overlay). It replaces the F004 floor pool. Static props and the front-row edge pieces are sprite renderers on the
    /// <c>WorldDeck</c> layer, always below the <c>Default</c> layer (plates and machines) and the <c>Overlay</c> layer (cues, markers, selection).
    /// Nothing here moves: lamps never flicker and no decorative pixel animates, so reduced motion needs no special case (FR-015).
    /// <para>
    /// The view owns no gameplay state. <see cref="DeckPlanner"/> decides what is drawn from the layout, the stage and each location's condition;
    /// <see cref="Bind"/> follows a <see cref="SlotGridView"/> (layout and conditions). The Tilemap renderers and the sprite renderers use the
    /// slot sprite material (the default 2D sprite material a slot renderer gets), so a deck pixel and a slot pixel are lit and filtered alike.
    /// The development stage is an input (<see cref="SetStage"/>, T540 wires the model); stage 0 is the Open deck.
    /// </para>
    /// </summary>
    public sealed class FactoryDeckView : MonoBehaviour
    {
        public const string BaseSortingLayer = "WorldBase";
        public const string DeckSortingLayer = "WorldDeck";

        /// <summary>The order of the props and edge pieces on the deck layer; the deck Tilemap itself is at 0 on the same layer.</summary>
        public const int PropOrderBase = 10;

        public const int EdgeOrderBase = 100;

        public const string BaseTileKey = "F006-GROUND-BASE";
        public const string DeckTileKey = "F006-GROUND-DECK";
        public const string HatchTileKey = "F006-GROUND-HATCH";

        private PresentationSpriteCatalog sprites;
        private WorldCompositionSettings settings;
        private FactoryWorldLayout layout;
        private SlotGridView bound;
        private int stage;
        private int densityBin;
        private bool dressed;
        private SlotCondition[] conditions = Array.Empty<SlotCondition>();
        private DeckPlan plan;
        private bool dirty;

        private Grid grid;
        private Tilemap baseTilemap;
        private Tilemap deckTilemap;
        private TilemapRenderer baseRenderer;
        private TilemapRenderer deckRenderer;
        private Transform propRoot;
        private Transform edgeRoot;
        private Transform highlightRoot;
        private readonly List<SpriteRenderer> highlightRenderers = new List<SpriteRenderer>();
        private int highlightCount;
        private long highlightStartMs;
        private bool highlightReduced;
        private long revealStartMs = long.MinValue;
        private bool revealActive;
        private Material slotMaterial;
        private readonly Dictionary<string, Tile> tiles = new Dictionary<string, Tile>(StringComparer.Ordinal);
        private readonly List<SpriteRenderer> propRenderers = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> edgeRenderers = new List<SpriteRenderer>();

        /// <summary>The plan the view currently draws, or null before <see cref="SetLayout"/>.</summary>
        public DeckPlan Plan => plan;

        public int Stage => stage;

        /// <summary>The secondary density bins (activity plus level, 0 to 4) the dressing uses.</summary>
        public int DensityBin => densityBin;

        /// <summary>The foundation highlights that draw now (T540), for tests.</summary>
        public int ActiveHighlightCount => highlightCount;

        /// <summary>The highlight renderers created so far (enabled while drawn).</summary>
        public IReadOnlyList<SpriteRenderer> HighlightRenderers => highlightRenderers;

        /// <summary>True while the deck reveal (the lit deck easing up) runs.</summary>
        public bool RevealActive => revealActive;

        /// <summary>How long the foundation highlight of a new location stays, in milliseconds (T540, R14).</summary>
        public const long HighlightMilliseconds = 2000;

        /// <summary>How long the deck reveal takes, in milliseconds (normal motion only).</summary>
        public const long RevealMilliseconds = 600;

        /// <summary>The highlight fades over its last this many milliseconds (normal motion only).</summary>
        public const long HighlightFadeMilliseconds = 400;

        public const string HighlightKey = "F006-FX-COMPLETION-HIGHLIGHT";
        public const int HighlightOrder = 120;

        public Grid Grid => grid;
        public Tilemap BaseTilemap => baseTilemap;
        public Tilemap DeckTilemap => deckTilemap;
        public TilemapRenderer BaseRenderer => baseRenderer;
        public TilemapRenderer DeckRenderer => deckRenderer;

        /// <summary>The material the Tilemap renderers and every sprite renderer of the deck share with the slot sprites.</summary>
        public Material SlotMaterial => slotMaterial;

        /// <summary>The prop sprite renderers that currently draw something.</summary>
        public IReadOnlyList<SpriteRenderer> PropRenderers => propRenderers;

        /// <summary>The edge piece renderers of the front margin row that currently draw something.</summary>
        public IReadOnlyList<SpriteRenderer> EdgeRenderers => edgeRenderers;

        /// <summary>True when the catalog has the base and deck tiles, so the deck draws (a smaller development catalog draws nothing, without error).</summary>
        public bool HasArt => sprites != null && sprites.TryGetEntry(BaseTileKey, out _) && sprites.TryGetEntry(DeckTileKey, out _) && sprites.TryGetEntry(HatchTileKey, out _);

        /// <summary>The cell size of the grid in world units.</summary>
        public static Vector3 CellSize => new Vector3(SlotGridLayout.PitchX, SlotGridLayout.PitchY, 0f);

        /// <summary>
        /// The grid origin offset in world units: the centre of cell (0, 0) must be the footprint pivot of the lattice cell, 15 art px above the
        /// bottom of the first plate, so the grid origin sits half a cell (27 art px) below it, at (0, -12) art px.
        /// </summary>
        public static Vector3 GridOrigin => new Vector3(
            0f,
            (SlotGridLayout.PivotAbovePlateBottomPixels - SlotGridLayout.FloorTilePivotYPixels) / (float)SlotGridLayout.Density,
            0f);

        /// <summary>Configures the catalog and the composition values (null: the F004-reproducing defaults, which carry no props) and builds the grid.</summary>
        public void Configure(PresentationSpriteCatalog catalog, WorldCompositionSettings compositionSettings)
        {
            sprites = catalog ?? throw new ArgumentNullException(nameof(catalog));
            settings = compositionSettings;
            EnsureHierarchy();
            dirty = true;
            Refresh();
        }

        /// <summary>Follows a slot grid: its layout now and later, and the condition of each location after every render.</summary>
        public void Bind(SlotGridView slotGrid)
        {
            Unbind();
            bound = slotGrid ?? throw new ArgumentNullException(nameof(slotGrid));
            bound.LayoutChanged += SetLayout;
            bound.VisualsRendered += OnVisualsRendered;
            SetLayout(bound.Layout);
            OnVisualsRendered(bound.Visuals);
        }

        private void Unbind()
        {
            if (bound == null)
            {
                return;
            }

            bound.LayoutChanged -= SetLayout;
            bound.VisualsRendered -= OnVisualsRendered;
            bound = null;
        }

        public void SetLayout(FactoryWorldLayout newLayout)
        {
            if (newLayout == null)
            {
                throw new ArgumentNullException(nameof(newLayout));
            }

            if (ReferenceEquals(layout, newLayout))
            {
                return;
            }

            layout = newLayout;
            dirty = true;
            Refresh();
        }

        /// <summary>Sets the development stage (0 Open deck, 1 Walled yard, 2 Gantry line); it picks the edge pieces, the prop weights and the prop density.</summary>
        public void SetStage(int newStage)
        {
            newStage = Mathf.Clamp(newStage, 0, WorldCompositionSettings.StageCount - 1);
            if (newStage == stage)
            {
                return;
            }

            stage = newStage;
            dirty = true;
            Refresh();
        }

        /// <summary>
        /// Sets the whole development value (T540, R15): the stage and the secondary bins. It switches the stage dressing on (the lit ring, props only on deck, the
        /// bin density). Redraws only when something changed, so calling it every frame allocates nothing.
        /// </summary>
        public void SetDevelopment(DevelopmentStageValue value)
        {
            var newStage = Mathf.Clamp(value.Stage, 0, WorldCompositionSettings.StageCount - 1);
            if (dressed && newStage == stage && value.DensityBin == densityBin)
            {
                return;
            }

            dressed = true;
            stage = newStage;
            densityBin = value.DensityBin;
            dirty = true;
            Refresh();
        }

        /// <summary>
        /// A milestone (T540, R14): the locations <paramref name="firstLocation"/> and the following <paramref name="count"/> - 1 get a 2 s foundation highlight, and a
        /// stage raise eases the lit deck up. Under reduced motion the highlight is static and nothing eases.
        /// </summary>
        public void ShowMilestone(int firstLocation, int count, bool stageRaised, long milliseconds, bool reducedMotion)
        {
            highlightStartMs = milliseconds;
            highlightReduced = reducedMotion;
            for (var i = 0; i < highlightCount; i++)
            {
                highlightRenderers[i].enabled = false;
            }

            highlightCount = 0;
            if (layout != null && sprites != null && sprites.TryGetEntry(HighlightKey, out var entry) && firstLocation >= 0)
            {
                EnsureHighlightRoot();
                var density = (float)SlotGridLayout.Density;
                var last = Mathf.Min(layout.LocationCount, firstLocation + Mathf.Max(0, count));
                for (var location = firstLocation; location < last; location++)
                {
                    while (highlightRenderers.Count <= highlightCount)
                    {
                        var child = new GameObject("Highlight");
                        child.transform.SetParent(highlightRoot, false);
                        var created = child.AddComponent<SpriteRenderer>();
                        created.sharedMaterial = slotMaterial;
                        created.sortingLayerID = SortingLayer.NameToID(SlotGridView.OverlaySortingLayer);
                        created.sortingOrder = HighlightOrder;
                        created.enabled = false;
                        highlightRenderers.Add(created);
                    }

                    var renderer = highlightRenderers[highlightCount++];
                    var anchor = layout.AnchorOf(new BuildingLocationId(location));
                    renderer.sprite = entry.Sprite;
                    renderer.color = Color.white;
                    renderer.transform.localPosition = new Vector3(Mathf.Round(anchor.x * density) / density, Mathf.Round(anchor.y * density) / density, 0f);
                    renderer.enabled = true;
                }
            }

            if (stageRaised && !reducedMotion && baseTilemap != null)
            {
                revealActive = true;
                revealStartMs = milliseconds;
                ApplyReveal(0f);
            }
        }

        /// <summary>
        /// One frame of the milestone feedback: expires the highlight after 2 s (fading over its last 0.4 s in normal motion, a plain static stop under reduced motion)
        /// and eases the lit deck up over 0.6 s. Allocation-free, and a no-op while nothing runs.
        /// </summary>
        public void TickFeedback(long milliseconds)
        {
            if (highlightCount > 0)
            {
                var elapsed = milliseconds - highlightStartMs;
                if (elapsed >= HighlightMilliseconds)
                {
                    for (var i = 0; i < highlightCount; i++)
                    {
                        highlightRenderers[i].enabled = false;
                    }

                    highlightCount = 0;
                }
                else if (!highlightReduced && elapsed > HighlightMilliseconds - HighlightFadeMilliseconds)
                {
                    var alpha = (HighlightMilliseconds - elapsed) / (float)HighlightFadeMilliseconds;
                    for (var i = 0; i < highlightCount; i++)
                    {
                        highlightRenderers[i].color = new Color(1f, 1f, 1f, alpha);
                    }
                }
            }

            if (revealActive)
            {
                var t = (milliseconds - revealStartMs) / (float)RevealMilliseconds;
                if (t >= 1f)
                {
                    revealActive = false;
                    ApplyReveal(1f);
                }
                else
                {
                    ApplyReveal(Mathf.Clamp01(t));
                }
            }
        }

        /// <summary>Ends every milestone effect at once (reduced motion switched on, a new run).</summary>
        public void ClearFeedback()
        {
            for (var i = 0; i < highlightCount; i++)
            {
                highlightRenderers[i].enabled = false;
            }

            highlightCount = 0;
            if (revealActive)
            {
                revealActive = false;
                ApplyReveal(1f);
            }
        }

        // The deck eases from 55 % to full brightness: a tint on the two tilemap renderers, no allocation.
        private void ApplyReveal(float t)
        {
            var shade = Mathf.Lerp(0.55f, 1f, t);
            var color = new Color(shade, shade, shade, 1f);
            baseTilemap.color = color;
            deckTilemap.color = color;
        }

        private void EnsureHighlightRoot()
        {
            if (highlightRoot == null)
            {
                highlightRoot = new GameObject("Highlights").transform;
                highlightRoot.SetParent(transform, false);
            }
        }

        /// <summary>Sets the condition of every location (locked, empty or occupied) and redraws only when one changed.</summary>
        public void SetConditions(IReadOnlyList<SlotCondition> newConditions)
        {
            var count = newConditions?.Count ?? 0;
            var changed = conditions.Length != count;
            if (changed)
            {
                conditions = new SlotCondition[count];
            }

            for (var i = 0; i < count; i++)
            {
                if (conditions[i] != newConditions[i] || changed)
                {
                    conditions[i] = newConditions[i];
                    changed = true;
                }
            }

            if (changed)
            {
                dirty = true;
                Refresh();
            }
        }

        private void OnVisualsRendered(IReadOnlyList<SlotVisual> visuals)
        {
            var count = visuals.Count;
            var changed = conditions.Length != count;
            if (changed)
            {
                conditions = new SlotCondition[count];
            }

            for (var i = 0; i < count; i++)
            {
                var condition = visuals[i].Condition;
                if (conditions[i] != condition || changed)
                {
                    conditions[i] = condition;
                    changed = true;
                }
            }

            if (changed)
            {
                dirty = true;
                Refresh();
            }
        }

        /// <summary>Re-plans and redraws when the layout, stage or a condition changed since the last call. Allocation-free otherwise.</summary>
        public void Refresh()
        {
            if (!dirty || sprites == null || layout == null || grid == null)
            {
                return;
            }

            dirty = false;
            plan = DeckPlanner.Build(layout, conditions, stage, settings, dressed ? new DeckDressing(densityBin) : (DeckDressing?)null);
            if (!HasArt)
            {
                ClearAll();
                return;
            }

            DrawTiles();
            DrawProps();
            DrawEdges();
        }

        // ------------------------------------------------------------------------------------------------------------------------------

        private void EnsureHierarchy()
        {
            if (grid != null)
            {
                return;
            }

            slotMaterial = ProbeSlotMaterial();
            var root = new GameObject("Factory Deck");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = GridOrigin;
            grid = root.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            grid.cellSize = CellSize;
            grid.cellGap = Vector3.zero;

            baseTilemap = CreateTilemap(root.transform, "Base", BaseSortingLayer, 0, out baseRenderer);
            deckTilemap = CreateTilemap(root.transform, "Deck", DeckSortingLayer, 0, out deckRenderer);

            // Props and edge pieces are positioned in the view's own world space, not in grid space, so they sit beside the grid.
            propRoot = new GameObject("Props").transform;
            propRoot.SetParent(transform, false);
            edgeRoot = new GameObject("Edges").transform;
            edgeRoot.SetParent(transform, false);
        }

        private Tilemap CreateTilemap(Transform parent, string name, string sortingLayer, int order, out TilemapRenderer renderer)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var tilemap = child.AddComponent<Tilemap>();
            tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            renderer = child.AddComponent<TilemapRenderer>();
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;
            renderer.sharedMaterial = slotMaterial;
            return tilemap;
        }

        /// <summary>The material a slot renderer (a plain sprite renderer) gets by default: the one the deck must share.</summary>
        public static Material ProbeSlotMaterial()
        {
            var probe = new GameObject("slot-material-probe") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                return probe.AddComponent<SpriteRenderer>().sharedMaterial;
            }
            finally
            {
                if (Application.isPlaying)
                {
                    Destroy(probe);
                }
                else
                {
                    DestroyImmediate(probe);
                }
            }
        }

        private Tile TileFor(string key)
        {
            if (tiles.TryGetValue(key, out var tile) && tile != null)
            {
                return tile;
            }

            if (!sprites.TryGetEntry(key, out var entry))
            {
                return null;
            }

            tile = ScriptableObject.CreateInstance<Tile>();
            tile.hideFlags = HideFlags.HideAndDontSave;
            tile.sprite = entry.Sprite;
            tile.colliderType = Tile.ColliderType.None;
            tile.flags = TileFlags.LockAll;
            tiles[key] = tile;
            return tile;
        }

        private void DrawTiles()
        {
            baseTilemap.ClearAllTiles();
            deckTilemap.ClearAllTiles();
            var baseTile = TileFor(BaseTileKey);
            var deckTile = TileFor(DeckTileKey);
            var hatchTile = TileFor(HatchTileKey);
            var positions = new Vector3Int[plan.BaseCells.Count];
            var baseTiles = new TileBase[positions.Length];
            // A lit ring cell (stage dressing, R15) shows the deck tile in place of the dark base tile: the same opaque 70 x 54 tile, so the tile count is unchanged.
            var lit = plan.LitRingCells.Count > 0 ? new HashSet<Vector2Int>(plan.LitRingCells) : null;
            for (var i = 0; i < positions.Length; i++)
            {
                positions[i] = new Vector3Int(plan.BaseCells[i].x, plan.BaseCells[i].y, 0);
                baseTiles[i] = lit != null && lit.Contains(plan.BaseCells[i]) ? deckTile : baseTile;
            }

            baseTilemap.SetTiles(positions, baseTiles);
            var locationPositions = new Vector3Int[plan.LocationCells.Count];
            var locationTiles = new TileBase[locationPositions.Length];
            for (var i = 0; i < locationPositions.Length; i++)
            {
                locationPositions[i] = new Vector3Int(plan.LocationCells[i].x, plan.LocationCells[i].y, 0);
                locationTiles[i] = plan.LocationArt[i] == DeckCellArt.Hatch ? hatchTile : deckTile;
            }

            deckTilemap.SetTiles(locationPositions, locationTiles);
        }

        private void DrawProps()
        {
            var layerId = SortingLayer.NameToID(DeckSortingLayer);
            var rowCeiling = layout.RowCount + layout.DeckBandCells;
            Place(propRenderers, propRoot, "Prop", plan.Props.Count, layerId, i =>
            {
                var prop = plan.Props[i];
                return (prop.Key, prop.Anchor, PropOrderBase + Mathf.Max(0, rowCeiling - prop.Cell.y));
            });
        }

        private void DrawEdges()
        {
            var layerId = SortingLayer.NameToID(DeckSortingLayer);
            Place(edgeRenderers, edgeRoot, "Edge", plan.Edges.Count, layerId, i =>
            {
                var edge = plan.Edges[i];
                return (edge.Key, edge.Anchor, EdgeOrderBase + edge.Order);
            });
        }

        private void Place(List<SpriteRenderer> pool, Transform root, string name, int count, int layerId, Func<int, (string Key, Vector2Int Anchor, int Order)> item)
        {
            while (pool.Count < count)
            {
                var child = new GameObject(name);
                child.transform.SetParent(root, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = slotMaterial;
                renderer.sortingLayerID = layerId;
                renderer.enabled = false;
                pool.Add(renderer);
            }

            for (var i = 0; i < pool.Count; i++)
            {
                var renderer = pool[i];
                if (i >= count)
                {
                    renderer.enabled = false;
                    continue;
                }

                var (key, anchor, order) = item(i);
                if (!sprites.TryGetEntry(key, out var entry))
                {
                    renderer.enabled = false;
                    continue;
                }

                renderer.sprite = entry.Sprite;
                renderer.sortingLayerID = layerId;
                renderer.sortingOrder = order;
                renderer.transform.localPosition = new Vector3(anchor.x / (float)SlotGridLayout.Density, anchor.y / (float)SlotGridLayout.Density, 0f);
                renderer.enabled = true;
            }
        }

        private void ClearAll()
        {
            if (baseTilemap != null)
            {
                baseTilemap.ClearAllTiles();
                deckTilemap.ClearAllTiles();
            }

            foreach (var renderer in propRenderers)
            {
                renderer.enabled = false;
            }

            foreach (var renderer in edgeRenderers)
            {
                renderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            Unbind();
            foreach (var tile in tiles.Values)
            {
                if (tile == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(tile);
                }
                else
                {
                    DestroyImmediate(tile);
                }
            }

            tiles.Clear();
        }
    }
}
