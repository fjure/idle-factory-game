using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.Rendering;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// Draws the 12 slots. Per slot there is one Sorting Group for the body art (plate, machine, accent; sorted by slot row, R04)
    /// and, outside that group, the overlays (selection frame, lock label, markers, state badge) on the dedicated
    /// <see cref="OverlaySortingLayer"/> above all machine art, with a row-based <c>sortingOrder</c>. At most 8 renderers per slot
    /// (Art Bible section 6.1 plus the accent and the lock label); the view owns no gameplay state: <see cref="Render"/> takes the pure
    /// <see cref="SlotVisual"/> values and the <see cref="StateCueSet"/> decides which cue sprite a state draws. Tapping resolves
    /// through <see cref="TryHitSlot"/>, which stays footprint-only.
    /// </summary>
    public sealed class SlotGridView : MonoBehaviour
    {
        /// <summary>The sorting layer of every overlay (frame, label, markers, badge); it is declared after Default in <c>TagManager</c>.</summary>
        public const string OverlaySortingLayer = "Overlay";

        // Sorting orders. Plate, machine and accent order inside the slot's Sorting Group; the group itself sorts by slot row.
        public const int PlateOrder = 10;
        public const int MachineOrder = 100;
        public const int AccentOrder = 110;

        /// <summary>The floor draws below every slot: a slot's Sorting Group has an order of at least 0 (its row), the floor tiles -100 on the same layer.</summary>
        public const int FloorOrder = -100;

        // Overlay orders on the overlay layer: a base plus the slot row, so a front row draws after the row behind it and every cue
        // draws after every selection frame.
        public const int FrameOrder = 150;
        public const int OverlayOrder = 300;

        private const float BadgeNaturalWidth = 72f / 64f;
        private const float MarkerNaturalSize = 28f / 64f;
        private const float MinimumBadgeWidthPoints = 27f;
        private const float MinimumMarkerPoints = 10f;
        private const float MarkerMinimumPixelsPerCell = 24f;

        private PresentationSpriteCatalog sprites;
        private StateCueSet cueSet = StateCueSet.Default;
        private SlotRenderers[] slots;
        private SlotVisual[] visuals = Array.Empty<SlotVisual>();
        private int columns = 3;
        private float pixelsPerCell = 64f;
        private long clockMilliseconds;
        private bool clockFrozen;

        // The floor (T472): a pool of tile renderers that grows with the visible area and is re-placed only when the covered cell block changes.
        private readonly List<SpriteRenderer> floorTiles = new List<SpriteRenderer>();
        private Transform floorRoot;
        private Sprite floorSprite;
        private Camera floorCamera;
        private FloorRange floorRange = FloorRange.Empty;
        private bool floorApplied;
        private int floorEnabled;

        public int Columns => columns;
        /// <summary>The live visual buffer: the next <see cref="Render"/> overwrites it in place, so copy it to keep a snapshot.</summary>
        public IReadOnlyList<SlotVisual> Visuals => visuals;

        /// <summary>What a slot renderer draws.</summary>
        public enum SlotRendererRole
        {
            None = 0,
            Plate = 1,
            Machine = 2,
            Accent = 3,
            Frame = 4,
            Label = 5,
            Badge = 6,
            Boosted = 7,
            Overdrive = 8,
        }

        /// <summary>The slot renderers that currently draw something, for tests and diagnostics.</summary>
        public int ActiveRendererCount
        {
            get
            {
                var count = 0;
                if (slots == null)
                {
                    return 0;
                }

                foreach (var slot in slots)
                {
                    count += slot.ActiveCount;
                }

                return count;
            }
        }

        /// <summary>The enabled accent renderers (<c>F004-ACCENT-*</c>); at most one per running occupied slot, so at most 12.</summary>
        public int ActiveAccentCount
        {
            get
            {
                var count = 0;
                if (slots == null)
                {
                    return 0;
                }

                foreach (var slot in slots)
                {
                    count += slot.AccentActive ? 1 : 0;
                }

                return count;
            }
        }

        public void Configure(PresentationSpriteCatalog catalog, int slotCount = SlotGridLayout.SlotCount) => Configure(catalog, null, slotCount);

        public void Configure(PresentationSpriteCatalog catalog, StateCueSet stateCues, int slotCount = SlotGridLayout.SlotCount)
        {
            sprites = catalog ?? throw new ArgumentNullException(nameof(catalog));
            cueSet = stateCues ?? StateCueSet.Default;
            var overlayLayerId = SortingLayer.NameToID(OverlaySortingLayer);
            if (slots != null)
            {
                foreach (var slot in slots)
                {
                    slot.Destroy();
                }
            }

            slots = new SlotRenderers[slotCount];
            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = new SlotRenderers(transform, i, overlayLayerId);
            }

            Reposition();
            ConfigureFloor();
        }

        /// <summary>True when the catalog resolved <see cref="PresentationSpriteCatalog.FloorTileKey"/> to a sprite; otherwise the view draws no floor.</summary>
        public bool HasFloor => floorSprite != null;

        /// <summary>
        /// The camera whose view the floor covers. Unset, the view follows the scene's main camera (the factory camera carries the
        /// <c>MainCamera</c> tag); a test or a capture rig sets its own.
        /// </summary>
        public Camera FloorCamera
        {
            get => floorCamera;
            set
            {
                floorCamera = value;
                floorApplied = false;
            }
        }

        /// <summary>The floor tiles that are drawn now (the pool may hold more, disabled).</summary>
        public int FloorTileCount => floorEnabled;

        /// <summary>The floor renderers of the pool; the first <see cref="FloorTileCount"/> are drawn, the rest are pooled and disabled.</summary>
        public IReadOnlyList<SpriteRenderer> FloorTiles => floorTiles;

        /// <summary>The cell block the floor covers now.</summary>
        public FloorRange FloorCells => floorEnabled == 0 ? FloorRange.Empty : floorRange;

        private void LateUpdate()
        {
            UpdateFloor();
        }

        /// <summary>
        /// Fits the floor to the floor camera (or the main camera) once per call. Allocation-free while the covered cell block is unchanged;
        /// the pool grows only when a zoom level or viewport needs more tiles than it holds.
        /// </summary>
        public void UpdateFloor()
        {
            if (floorSprite == null)
            {
                return;
            }

            var camera = floorCamera != null ? floorCamera : Camera.main;
            if (camera == null || !camera.orthographic)
            {
                ApplyFloor(FloorRange.Empty);
                return;
            }

            var centre = transform.InverseTransformPoint(camera.transform.position);
            var halfHeight = camera.orthographicSize;
            var halfWidth = halfHeight * camera.aspect;
            UpdateFloor(centre.x - halfWidth, centre.y - halfHeight, centre.x + halfWidth, centre.y + halfHeight);
        }

        /// <summary>Fits the floor to a visible rectangle in the view's local world units (the camera path of <see cref="UpdateFloor()"/>).</summary>
        public void UpdateFloor(float minX, float minY, float maxX, float maxY)
        {
            if (floorSprite == null)
            {
                return;
            }

            ApplyFloor(SlotGridLayout.FloorCellsFor(minX, minY, maxX, maxY));
        }

        private void ConfigureFloor()
        {
            if (floorRoot != null)
            {
                Destroy(floorRoot.gameObject);
            }

            floorRoot = null;
            floorTiles.Clear();
            floorEnabled = 0;
            floorApplied = false;
            floorRange = FloorRange.Empty;
            // A missing key is the supported fallback (no floor, no error): the camera background stays.
            floorSprite = sprites != null && sprites.TryGetEntry(PresentationSpriteCatalog.FloorTileKey, out var entry) ? entry.Sprite : null;
        }

        private void ApplyFloor(FloorRange range)
        {
            if (floorApplied && range.Equals(floorRange))
            {
                return;
            }

            floorApplied = true;
            floorRange = range;
            var needed = range.Count;
            if (needed > 0 && floorRoot == null)
            {
                var rootObject = new GameObject("Floor");
                rootObject.transform.SetParent(transform, false);
                floorRoot = rootObject.transform;
            }

            while (floorTiles.Count < needed)
            {
                var tileObject = new GameObject("Floor Tile");
                tileObject.transform.SetParent(floorRoot, false);
                var renderer = tileObject.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = FloorOrder;
                renderer.sprite = floorSprite;
                renderer.enabled = false;
                floorTiles.Add(renderer);
            }

            var index = 0;
            for (var row = range.MinRow; row <= range.MaxRow; row++)
            {
                for (var column = range.MinColumn; column <= range.MaxColumn; column++)
                {
                    var tile = floorTiles[index++];
                    var pivot = SlotGridLayout.FloorPivotOf(column, row);
                    tile.transform.localPosition = new Vector3(pivot.x, pivot.y, 0f);
                    tile.enabled = true;
                }
            }

            for (var i = needed; i < floorEnabled; i++)
            {
                floorTiles[i].enabled = false;
            }

            floorEnabled = needed;
        }

        /// <summary>Sets the column count (3 portrait, 4 landscape) and moves every slot.</summary>
        public void SetColumns(int newColumns)
        {
            if (newColumns != 3 && newColumns != 4)
            {
                throw new ArgumentOutOfRangeException(nameof(newColumns));
            }

            columns = newColumns;
            Reposition();
        }

        /// <summary>
        /// Scales the placeholder badges and markers for the projected size (Art Bible section 8.2): a tight badge is at least 27
        /// points wide, tight markers at least 10 points and hidden below 24 pixels per cell. Canvas-layer art (the F004 sprites) is
        /// authored at its final size and is never scaled. The lock-digit label follows the label threshold (cap height of at least 12 points).
        /// </summary>
        public void SetProjectedPixelsPerCell(float newPixelsPerCell)
        {
            if (float.IsNaN(newPixelsPerCell) || float.IsInfinity(newPixelsPerCell) || newPixelsPerCell <= 0f)
            {
                return;
            }

            if (Mathf.Approximately(pixelsPerCell, newPixelsPerCell))
            {
                return;
            }

            pixelsPerCell = newPixelsPerCell;
            ApplyOverlayScale();
        }

        public static float BadgeScale(float pixelsPerCell) => Mathf.Max(1f, MinimumBadgeWidthPoints / (BadgeNaturalWidth * pixelsPerCell));

        public static float MarkerScale(float pixelsPerCell) => Mathf.Max(1f, MinimumMarkerPoints / (MarkerNaturalSize * pixelsPerCell));

        // The 1 % tolerance of n_min (owner decision 2026-10-28) applies here too: the smallest allowed scale on a 2.0046 px/pt phone
        // projects 23.94 points per cell, which must not hide the markers.
        public static bool MarkersVisibleAt(float pixelsPerCell) => pixelsPerCell >= MarkerMinimumPixelsPerCell * 0.99f;

        /// <summary>
        /// The shared-clock seam (T431 plugs the presentation clock in here): one call per update advances every animated entry,
        /// <c>frame = floor(t * fps) mod frameCount</c> with <paramref name="presentationMilliseconds"/> in integer milliseconds of unscaled
        /// presentation time. Only a state-cue row marked <c>animated</c> (running) animates; <paramref name="frozen"/> (reduced motion or a
        /// suspended app) forces frame 0 from the next call. Allocation-free.
        /// </summary>
        public void AdvanceFrames(long presentationMilliseconds, bool frozen)
        {
            clockMilliseconds = presentationMilliseconds;
            clockFrozen = frozen;
            if (slots == null)
            {
                return;
            }

            foreach (var slot in slots)
            {
                slot.Advance(presentationMilliseconds, frozen);
            }
        }

        /// <summary><c>floor(t * fps) mod frameCount</c> for integer milliseconds; 0 for a still entry or a non-positive rate.</summary>
        public static int FrameAt(long milliseconds, float framesPerSecond, int frameCount)
        {
            if (frameCount <= 1 || framesPerSecond <= 0f || float.IsNaN(framesPerSecond) || float.IsInfinity(framesPerSecond) || milliseconds < 0)
            {
                return 0;
            }

            var ticks = (long)Math.Floor(milliseconds * (double)framesPerSecond / 1000.0);
            return (int)(ticks % frameCount);
        }

        public bool TryHitSlot(Vector2 world, out int slotIndex)
        {
            slotIndex = SlotGridLayout.HitTest(world, columns);
            return slotIndex >= 0;
        }

        public Rect WorldRectOf(int slotIndex) => SlotGridLayout.SlotRect(slotIndex, columns);

        /// <summary>The footprint pivot of a slot in world units: the origin of every overlay anchor.</summary>
        public Vector2 FootprintPivotOf(int slotIndex) => SlotGridLayout.PivotOf(slotIndex, columns);

        /// <summary>The sorting row of a slot: 0 for the top (back) row, increasing toward the front.</summary>
        public int RowOf(int slotIndex) => SlotGridLayout.RowOf(slotIndex, columns);

        /// <summary>The renderers of one slot, for tests and diagnostics.</summary>
        public IReadOnlyList<SpriteRenderer> RenderersOf(int slotIndex) => slots == null || slotIndex < 0 || slotIndex >= slots.Length
            ? Array.Empty<SpriteRenderer>()
            : slots[slotIndex].All;

        /// <summary>The renderer that draws a role in a slot, or null.</summary>
        public SpriteRenderer RendererOf(int slotIndex, SlotRendererRole role) => slots == null || slotIndex < 0 || slotIndex >= slots.Length
            ? null
            : slots[slotIndex].Get(role);

        public void Render(IReadOnlyList<SlotVisual> newVisuals)
        {
            if (sprites == null || slots == null)
            {
                throw new InvalidOperationException("The slot grid is not configured.");
            }

            if (newVisuals == null) throw new ArgumentNullException(nameof(newVisuals));
            // T431 (FR-026): the cue refresh reuses its own buffer; it allocates only when the number of slots changes.
            var count = newVisuals.Count;
            if (visuals.Length != count)
            {
                visuals = new SlotVisual[count];
            }

            for (var i = 0; i < count; i++)
            {
                visuals[i] = newVisuals[i];
            }
            for (var i = 0; i < slots.Length; i++)
            {
                if (i < visuals.Length)
                {
                    slots[i].Show(visuals[i], sprites, cueSet);
                }
                else
                {
                    slots[i].Hide();
                }
            }

            ApplyOverlayScale();
            AdvanceFrames(clockMilliseconds, clockFrozen);
        }

        private void Reposition()
        {
            if (slots == null)
            {
                return;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                slots[i].SetOrigin(SlotGridLayout.PivotOf(i, columns), SlotGridLayout.RowOf(i, columns));
            }
        }

        private void ApplyOverlayScale()
        {
            if (slots == null)
            {
                return;
            }

            var badge = BadgeScale(pixelsPerCell);
            var marker = MarkerScale(pixelsPerCell);
            var markersVisible = MarkersVisibleAt(pixelsPerCell);
            foreach (var slot in slots)
            {
                slot.ApplyOverlay(badge, marker, markersVisible, pixelsPerCell);
            }
        }

        private void OnDestroy()
        {
            if (slots == null)
            {
                return;
            }

            foreach (var slot in slots)
            {
                slot.Destroy();
            }

            slots = null;
        }
    }
}
