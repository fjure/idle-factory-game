using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.Rendering;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// Draws the slots of the world layout (12 today). Per slot there is one Sorting Group for the body art (plate, machine, accent; sorted by slot row, R04)
    /// and, outside that group, the overlays (selection frame, lock label, markers, state badge) on the dedicated
    /// <see cref="OverlaySortingLayer"/> above all machine art, with a row-based <c>sortingOrder</c>. At most 8 renderers per slot
    /// (Art Bible section 6.1 plus the accent and the lock label); the view owns no gameplay state: <see cref="Render"/> takes the pure
    /// <see cref="SlotVisual"/> values and the <see cref="StateCueSet"/> decides which cue sprite a state draws. Tapping resolves
    /// through <see cref="TryHitSlot"/>, which stays footprint-only.
    /// </summary>
    public sealed class SlotGridView : MonoBehaviour, IBuildPreview
    {
        /// <summary>The sorting layer of every overlay (frame, label, markers, badge); it is declared after Default in <c>TagManager</c>.</summary>
        public const string OverlaySortingLayer = "Overlay";

        // Sorting orders. Plate, machine and accent order inside the slot's Sorting Group; the group itself sorts by slot row.
        public const int PlateOrder = 10;
        public const int MachineOrder = 100;
        public const int AccentOrder = 110;

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
        private FactoryWorldLayout layout = LatticeLayoutBuilder.Shipped(WorldOrientation.Portrait);
        private float pixelsPerCell = 64f;
        private long clockMilliseconds;
        private bool clockFrozen;

        /// <summary>The world layout that places every slot, its hit test and its sorting rows (the F004 portrait lattice until <see cref="SetLayout"/>).</summary>
        public FactoryWorldLayout Layout => layout;

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
            UpgradeMarker = 9,
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

        /// <summary>The enabled accent renderers (<c>F004-ACCENT-*</c>); at most one per running occupied slot.</summary>
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

        /// <summary>Configures the view; <paramref name="slotCount"/> below 0 (the default) means one slot per location of the layout.</summary>
        public void Configure(PresentationSpriteCatalog catalog, int slotCount = -1) => Configure(catalog, null, slotCount);

        public void Configure(PresentationSpriteCatalog catalog, StateCueSet stateCues, int slotCount = -1)
        {
            sprites = catalog ?? throw new ArgumentNullException(nameof(catalog));
            cueSet = stateCues ?? StateCueSet.Default;
            BuildSlots(slotCount < 0 ? layout.LocationCount : slotCount);
            Reposition();
        }

        private void BuildSlots(int slotCount)
        {
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
        }

        /// <summary>
        /// Sets the world layout and moves every slot. A layout with a different number of locations rebuilds the slot renderers (the next
        /// <see cref="Render"/> shows them); the same count only repositions.
        /// </summary>
        public void SetLayout(FactoryWorldLayout newLayout)
        {
            layout = newLayout ?? throw new ArgumentNullException(nameof(newLayout));
            if (slots != null && sprites != null && slots.Length != layout.LocationCount)
            {
                BuildSlots(layout.LocationCount);
            }

            Reposition();
            LayoutChanged?.Invoke(layout);
        }

        /// <summary>Raised after <see cref="SetLayout"/> (the deck view re-plans on it, T527).</summary>
        public event Action<FactoryWorldLayout> LayoutChanged;

        /// <summary>Raised at the end of <see cref="Render"/> with the live visual buffer (the deck view reads each slot's condition, T527).</summary>
        public event Action<IReadOnlyList<SlotVisual>> VisualsRendered;

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
            if (layout.TryHit(world, out var id))
            {
                slotIndex = id.Value;
                return true;
            }

            slotIndex = -1;
            return false;
        }

        public Rect WorldRectOf(int slotIndex) => layout.FootprintOf(new BuildingLocationId(slotIndex));

        /// <summary>The footprint pivot of a slot in world units: the origin of every overlay anchor.</summary>
        public Vector2 FootprintPivotOf(int slotIndex) => layout.AnchorOf(new BuildingLocationId(slotIndex));

        /// <summary>The sorting row of a slot: 0 for the top (back) row, increasing toward the front.</summary>
        public int RowOf(int slotIndex) => layout.SortKeyOf(new BuildingLocationId(slotIndex));

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
                    slots[i].Show(visuals[i], sprites, cueSet, IsAvailable(i), i == previewSlot ? previewKey : null, IsMutedForBuild(i));
                }
                else
                {
                    slots[i].Hide();
                }
            }

            ApplyOverlayScale();
            AdvanceFrames(clockMilliseconds, clockFrozen);
            VisualsRendered?.Invoke(visuals);
        }

        // ------------------------------------------------------------------------------------------------------
        // Build card preview (T539, FR-032, FR-033, research R13)
        // ------------------------------------------------------------------------------------------------------

        /// <summary>The alpha of the ghost preview of the selected machine type (frame 0).</summary>
        public const float PreviewAlpha = 0.6f;

        /// <summary>The tint of an occupied or locked location while the build card is open (C-44).</summary>
        public static Color MutedTint => SlotRenderers.MutedTint;

        /// <summary>The alpha of the frame that outlines an available build location; the selected location keeps the full frame.</summary>
        public const float HighlightAlpha = 0.5f;

        private bool[] available = Array.Empty<bool>();
        private int previewSlot = -1;
        private string previewKey;

        /// <summary>The location the ghost shows at, or -1.</summary>
        public int PreviewSlot => previewSlot;

        /// <summary>The machine body key of the ghost, or null.</summary>
        public string PreviewKey => previewKey;

        /// <summary>
        /// C-44 (T560, FR-032): while the build card shows its outline or ghost, a location that cannot take a machine (occupied or locked) is muted to a dim tint, so it
        /// reads as "not here" by contrast with the framed available ones, not by the alpha of one frame alone. False when no build preview is active.
        /// </summary>
        public bool IsMutedForBuild(int slotIndex)
        {
            if (!BuildPreviewActive || slotIndex < 0 || slotIndex >= visuals.Length)
            {
                return false;
            }

            return visuals[slotIndex].Condition != SlotCondition.Empty;
        }

        /// <summary>True while the build card has outlined locations or a ghost.</summary>
        public bool BuildPreviewActive => availableCount > 0 || previewSlot >= 0;

        private int availableCount;

        /// <summary>True when <paramref name="slotIndex"/> is outlined as an available build location.</summary>
        public bool IsAvailable(int slotIndex) => slotIndex >= 0 && slotIndex < available.Length && available[slotIndex];

        /// <summary>Outlines the given locations as the available ones; an empty or null list clears the outline.</summary>
        public void ShowAvailable(IReadOnlyList<int> locations)
        {
            var count = locations == null ? 0 : locations.Count;
            var size = Math.Max(slots == null ? 0 : slots.Length, layout.LocationCount);
            if (available.Length != size)
            {
                available = new bool[size];
            }
            else
            {
                Array.Clear(available, 0, available.Length);
            }

            availableCount = 0;
            for (var i = 0; i < count; i++)
            {
                var location = locations[i];
                if (location >= 0 && location < available.Length)
                {
                    available[location] = true;
                    availableCount++;
                }
            }

            Reapply();
        }

        /// <summary>Shows the ghost of <paramref name="machineKey"/> at the empty <paramref name="location"/> (frame 0, <see cref="PreviewAlpha"/>); a null key or a negative location clears it.</summary>
        public void ShowPreview(int location, string machineKey)
        {
            var valid = machineKey != null && location >= 0;
            previewSlot = valid ? location : -1;
            previewKey = valid ? machineKey : null;
            Reapply();
        }

        /// <summary>Clears the outline and the ghost.</summary>
        public void ClearBuildPreview()
        {
            Array.Clear(available, 0, available.Length);
            availableCount = 0;
            previewSlot = -1;
            previewKey = null;
            Reapply();
        }

        // Redraws every slot from the last visuals with the preview state, without raising VisualsRendered (the deck view has nothing to re-plan).
        private void Reapply()
        {
            if (sprites == null || slots == null || visuals.Length == 0)
            {
                return;
            }

            for (var i = 0; i < slots.Length && i < visuals.Length; i++)
            {
                slots[i].Show(visuals[i], sprites, cueSet, IsAvailable(i), i == previewSlot ? previewKey : null, IsMutedForBuild(i));
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

            for (var i = 0; i < slots.Length && i < layout.LocationCount; i++)
            {
                var id = new BuildingLocationId(i);
                slots[i].SetOrigin(layout.AnchorOf(id), layout.SortKeyOf(id));
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
