using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// The depot and its links in the world (T528, FR-013, FR-029, research R07, contracts/world-presentation.md section 2). It draws:
    /// <list type="bullet">
    /// <item><description>the <b>link layer</b>, the third Tilemap of the world-layer budget (base, deck, links; Art Bible 2.2a): one static trunk tile per row cell
    /// from the row's far location to the depot column and a junction there, plus the branch tile on the cell of every occupied location. The Tilemap is
    /// shifted 12 art px up so the 70 x 54 tiles sit where the art pipeline's ring proof places them (lane 29 px above the row pivot);</description></item>
    /// <item><description>one <b>pip</b> sprite renderer per location, on the <c>Overlay</c> layer below every cue: it travels the branch (rear edge to trunk lane, whole pixels) at
    /// 800, 600 or 400 ms by pace while the machine is Running, and only then. Under reduced motion there is no pip and the Running branch shows the static
    /// active tile instead;</description></item>
    /// <item><description>the <b>depot</b> sprite at <see cref="FactoryWorldLayout.DepotAnchor"/>: the fill sprite of the shared store (four states) times the
    /// development stage (three variants), the full state when the store is full.</description></item>
    /// </list>
    /// The view decides nothing and owns no gameplay state: <see cref="DepotLinkModel"/> says what is shown, <see cref="DepotLinkGeometry"/> where. Trunks never
    /// change after the layout; only branch tiles swap, and only when a location's branch state changed. <see cref="Present"/> allocates nothing once warm.
    /// </summary>
    public sealed class DepotLinkView : MonoBehaviour
    {
        public const string LinksSortingLayer = "WorldLinks";
        public const string PipSortingLayer = SlotGridView.OverlaySortingLayer;

        /// <summary>The depot draws above the link Tilemap (order 0) on the links layer.</summary>
        public const int DepotOrder = 100;

        /// <summary>
        /// Pips draw on the Overlay layer above the machines (the Default layer) and <b>below every cue</b>: the order is this base plus the location id, under
        /// <see cref="SlotGridView.FrameOrder"/>. The Overdrive marker sits on the branch (x -4 to +6, 22 to 32 px above the pivot), so a pip passing it goes under it
        /// and never covers a cue (T528, C-11).
        /// </summary>
        public const int PipOrderBase = 100;

        private const int TrunkState = 0;
        private const int BranchTileState = 1;
        private const int ActiveTileState = 2;

        private PresentationSpriteCatalog sprites;
        private FactoryWorldLayout layout;
        private SlotGridView bound;
        private DepotLinkPlan plan;
        private int stage;
        private bool dirty;

        private Grid grid;
        private Tilemap tilemap;
        private TilemapRenderer tilemapRenderer;
        private SpriteRenderer depotRenderer;
        private Transform pipRoot;
        private Material slotMaterial;
        private readonly Dictionary<string, Tile> tiles = new Dictionary<string, Tile>(StringComparer.Ordinal);
        private Tile mirroredJunction;
        private readonly List<SpriteRenderer> pips = new List<SpriteRenderer>();
        private readonly TileChangeData[] swap = new TileChangeData[1];
        private int[] applied = Array.Empty<int>();
        private Vector3Int[] locationCells = Array.Empty<Vector3Int>();
        private string shownDepotKey;
        private PresentationSpriteEntry pipEntry;
        private bool hasPipArt;

        public DepotLinkPlan Plan => plan;
        public int Stage => stage;
        public Grid Grid => grid;
        public Tilemap Tilemap => tilemap;
        public TilemapRenderer TilemapRenderer => tilemapRenderer;
        public SpriteRenderer DepotRenderer => depotRenderer;

        /// <summary>The pip renderers created so far (one per location, created the first time the location runs); enabled only while a pip is drawn.</summary>
        public IReadOnlyList<SpriteRenderer> PipRenderers => pips;

        /// <summary>The catalog key of the depot sprite now shown, or null.</summary>
        public string ShownDepotKey => shownDepotKey;

        /// <summary>True when the catalog has the four link tiles, so the link layer draws (a smaller development catalog draws nothing, without error).</summary>
        public bool HasLinkArt => sprites != null && sprites.TryGetEntry(DepotLinkGeometry.TileKey(LinkTileKind.Trunk), out _) &&
            sprites.TryGetEntry(DepotLinkGeometry.TileKey(LinkTileKind.Junction), out _) && sprites.TryGetEntry(DepotLinkGeometry.BranchKey, out _) &&
            sprites.TryGetEntry(DepotLinkGeometry.BranchActiveKey, out _);

        /// <summary>The tile key the location's branch cell shows now: trunk, branch or active branch (for tests and diagnostics).</summary>
        public string TileKeyAt(int location)
        {
            if (location < 0 || location >= applied.Length)
            {
                return null;
            }

            switch (applied[location])
            {
                case BranchTileState: return DepotLinkGeometry.BranchKey;
                case ActiveTileState: return DepotLinkGeometry.BranchActiveKey;
                default: return DepotLinkGeometry.TileKey(LinkTileKind.Trunk);
            }
        }

        public void Configure(PresentationSpriteCatalog catalog)
        {
            sprites = catalog ?? throw new ArgumentNullException(nameof(catalog));
            EnsureHierarchy();
            pipEntry = sprites.TryGetEntry(DepotLinkGeometry.PipKey, out var entry) && entry.FrameCount > 0 ? entry : null;
            hasPipArt = pipEntry != null;
            dirty = true;
            Refresh();
        }

        public void Bind(SlotGridView slotGrid)
        {
            Unbind();
            bound = slotGrid ?? throw new ArgumentNullException(nameof(slotGrid));
            bound.LayoutChanged += SetLayout;
            SetLayout(bound.Layout);
        }

        private void Unbind()
        {
            if (bound == null)
            {
                return;
            }

            bound.LayoutChanged -= SetLayout;
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

        /// <summary>Sets the development stage (0 to 2): it picks the depot variant (S1 to S3). T540 wires the stage model.</summary>
        public void SetStage(int newStage)
        {
            newStage = Mathf.Clamp(newStage, 0, WorldCompositionSettings.StageCount - 1);
            if (newStage == stage)
            {
                return;
            }

            stage = newStage;
            shownDepotKey = null;
        }

        /// <summary>Re-plans and redraws the static link layer when the layout changed. Allocation-free otherwise.</summary>
        public void Refresh()
        {
            if (!dirty || sprites == null || layout == null || grid == null)
            {
                return;
            }

            dirty = false;
            plan = DepotLinkGeometry.Build(layout);
            ResizeLocationBuffers(layout.LocationCount);
            for (var i = 0; i < applied.Length; i++)
            {
                applied[i] = TrunkState;
            }

            shownDepotKey = null;
            // One disabled pip renderer per location, created with the layout: nothing is created in the steady state, when a machine first runs.
            for (var i = 0; hasPipArt && i < layout.LocationCount; i++)
            {
                PipRenderer(i);
            }

            if (!HasLinkArt)
            {
                tilemap.ClearAllTiles();
                return;
            }

            DrawStaticTiles();
        }

        /// <summary>
        /// One frame: swaps the branch tile of a location whose branch state changed, places the pip of every Running machine (normal motion) or hides it, and
        /// shows the depot sprite. <paramref name="milliseconds"/> is the shared presentation clock. Allocation-free once warm.
        /// </summary>
        public void Present(DepotLinkModel model, long milliseconds, bool reducedMotion)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            Refresh();
            if (plan == null || grid == null)
            {
                return;
            }

            var linkArt = HasLinkArtCached;
            var count = Math.Min(model.LocationCount, plan.LocationCount);
            var density = (float)SlotGridLayout.Density;
            for (var location = 0; location < count; location++)
            {
                var state = model.BranchOf(location);
                var want = state == BranchState.None ? TrunkState : state == BranchState.Running && reducedMotion ? ActiveTileState : BranchTileState;
                if (linkArt && applied[location] != want)
                {
                    // One reused change record: SetTile on a locked tile allocated per call in the contract run (F006AllocationTests).
                    swap[0] = new TileChangeData(locationCells[location], TileForState(want), Color.white, Matrix4x4.identity);
                    tilemap.SetTiles(swap, true);
                    applied[location] = want;
                }

                var showPip = hasPipArt && state == BranchState.Running && !reducedMotion;
                var renderer = showPip ? PipRenderer(location) : (location < pips.Count ? pips[location] : null);
                if (renderer == null)
                {
                    continue;
                }

                if (!showPip)
                {
                    renderer.enabled = false;
                    continue;
                }

                var steps = plan.PipEndY(location) - plan.PipStartY(location) + 1;
                var step = DepotLinkGeometry.PipStep(milliseconds, model.PaceOf(location), steps);
                var fps = pipEntry.FramesPerSecond;
                var frame = PresentationAnimationClock.FrameAt(milliseconds, fps.HasValue ? fps.Value : 0f, pipEntry.FrameCount);
                renderer.sprite = pipEntry.GetFrame(frame);
                renderer.transform.localPosition = new Vector3(plan.PipX(location) / density, (plan.PipStartY(location) + step) / density, 0f);
                renderer.enabled = true;
            }

            for (var location = count; location < pips.Count; location++)
            {
                pips[location].enabled = false;
            }

            PresentDepot(model.Fill);
        }

        private bool HasLinkArtCached => tilemap != null && tiles.Count > 0;

        private void PresentDepot(DepotFillState fill)
        {
            var key = DepotLinkGeometry.DepotKey(stage, fill);
            if (ReferenceEquals(key, shownDepotKey))
            {
                return;
            }

            if (!sprites.TryGetEntry(key, out var entry) || entry.Sprite == null)
            {
                depotRenderer.enabled = false;
                shownDepotKey = key;
                return;
            }

            depotRenderer.sprite = entry.Sprite;
            depotRenderer.transform.localPosition = new Vector3(layout.DepotAnchor.x, layout.DepotAnchor.y, 0f);
            depotRenderer.enabled = true;
            shownDepotKey = key;
        }

        // ------------------------------------------------------------------------------------------------------------------------------

        private void EnsureHierarchy()
        {
            if (grid != null)
            {
                return;
            }

            slotMaterial = FactoryDeckView.ProbeSlotMaterial();
            var root = new GameObject("Depot Links");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = FactoryDeckView.GridOrigin;
            grid = root.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            grid.cellSize = FactoryDeckView.CellSize;
            grid.cellGap = Vector3.zero;

            var child = new GameObject("Links");
            child.transform.SetParent(root.transform, false);
            // The 70 x 54 link tiles are centred 12 art px above their cell pivot (the ring proof's placement), so the Tilemap sits 12 px up.
            child.transform.localPosition = new Vector3(0f, DepotLinkGeometry.TileCentreAbovePivotPixels / (float)SlotGridLayout.Density, 0f);
            tilemap = child.AddComponent<Tilemap>();
            tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            tilemapRenderer = child.AddComponent<TilemapRenderer>();
            tilemapRenderer.sortingLayerName = LinksSortingLayer;
            tilemapRenderer.sortingOrder = 0;
            tilemapRenderer.sharedMaterial = slotMaterial;

            var depot = new GameObject("Depot");
            depot.transform.SetParent(transform, false);
            depotRenderer = depot.AddComponent<SpriteRenderer>();
            depotRenderer.sharedMaterial = slotMaterial;
            depotRenderer.sortingLayerID = SortingLayer.NameToID(LinksSortingLayer);
            depotRenderer.sortingOrder = DepotOrder;
            depotRenderer.enabled = false;

            pipRoot = new GameObject("Pips").transform;
            pipRoot.SetParent(transform, false);
        }

        private void ResizeLocationBuffers(int count)
        {
            if (applied.Length != count)
            {
                applied = new int[count];
                locationCells = new Vector3Int[count];
            }

            if (plan == null)
            {
                return;
            }

            foreach (var tile in plan.Tiles)
            {
                if (tile.Location >= 0 && tile.Location < count)
                {
                    locationCells[tile.Location] = new Vector3Int(tile.Cell.x, tile.Cell.y, 0);
                }
            }
        }

        private SpriteRenderer PipRenderer(int location)
        {
            while (pips.Count <= location)
            {
                var child = new GameObject("Pip " + pips.Count);
                child.transform.SetParent(pipRoot, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = slotMaterial;
                renderer.sortingLayerID = SortingLayer.NameToID(PipSortingLayer);
                renderer.sortingOrder = PipOrderBase + pips.Count;
                renderer.enabled = false;
                pips.Add(renderer);
            }

            return pips[location];
        }

        private Tile TileFor(string key, bool mirrored = false)
        {
            if (mirrored)
            {
                if (mirroredJunction == null)
                {
                    mirroredJunction = NewTile(key, true);
                }

                return mirroredJunction;
            }

            if (tiles.TryGetValue(key, out var tile) && tile != null)
            {
                return tile;
            }

            tile = NewTile(key, false);
            if (tile != null)
            {
                tiles[key] = tile;
            }

            return tile;
        }

        private Tile NewTile(string key, bool mirrored)
        {
            if (!sprites.TryGetEntry(key, out var entry))
            {
                return null;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.hideFlags = HideFlags.HideAndDontSave;
            tile.sprite = entry.Sprite;
            tile.colliderType = Tile.ColliderType.None;
            tile.flags = TileFlags.LockAll;
            if (mirrored)
            {
                tile.transform = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
            }

            return tile;
        }

        private Tile TileForState(int state)
        {
            switch (state)
            {
                case BranchTileState: return TileFor(DepotLinkGeometry.BranchKey);
                case ActiveTileState: return TileFor(DepotLinkGeometry.BranchActiveKey);
                default: return TileFor(DepotLinkGeometry.TileKey(LinkTileKind.Trunk));
            }
        }

        private void DrawStaticTiles()
        {
            tilemap.ClearAllTiles();
            var count = plan.Tiles.Count;
            var positions = new Vector3Int[count];
            var drawn = new TileBase[count];
            for (var i = 0; i < count; i++)
            {
                var tile = plan.Tiles[i];
                positions[i] = new Vector3Int(tile.Cell.x, tile.Cell.y, 0);
                drawn[i] = tile.Kind == LinkTileKind.Junction ? TileFor(DepotLinkGeometry.TileKey(LinkTileKind.Junction), tile.Mirrored) : TileFor(DepotLinkGeometry.TileKey(LinkTileKind.Trunk));
            }

            tilemap.SetTiles(positions, drawn);
            // Warm every tile the branches swap to, so the first swap in a frame allocates nothing.
            TileFor(DepotLinkGeometry.BranchKey);
            TileFor(DepotLinkGeometry.BranchActiveKey);
        }

        private void OnDestroy()
        {
            Unbind();
            foreach (var tile in tiles.Values)
            {
                DestroyTile(tile);
            }

            DestroyTile(mirroredJunction);
            tiles.Clear();
            mirroredJunction = null;
        }

        private static void DestroyTile(Tile tile)
        {
            if (tile == null)
            {
                return;
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
    }
}
