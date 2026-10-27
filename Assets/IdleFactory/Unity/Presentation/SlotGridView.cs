using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>What a slot of the fixed 12-slot grid is (Art Bible section 5).</summary>
    public enum SlotCondition
    {
        Empty = 0,
        Locked = 1,
        Occupied = 2,
    }

    /// <summary>
    /// Everything the world draws for one slot: a pure value derived from the run read model (Art Bible sections 5 and
    /// 6). At most one state badge and two markers (ART-2); a locked or empty slot carries neither.
    /// </summary>
    public readonly struct SlotVisual
    {
        public SlotVisual(
            int index,
            SlotCondition condition,
            string baseKey,
            string machineKey,
            string badgeKey,
            string badgeText,
            bool boosted,
            bool overdrive,
            bool selected,
            int unlockLevel,
            string description)
        {
            Index = index;
            Condition = condition;
            BaseKey = baseKey;
            MachineKey = machineKey;
            BadgeKey = badgeKey;
            BadgeText = badgeText;
            Boosted = boosted;
            Overdrive = overdrive;
            Selected = selected;
            UnlockLevel = unlockLevel;
            Description = description;
        }

        public int Index { get; }
        public SlotCondition Condition { get; }

        /// <summary>The slot-family presentation key drawn under everything (empty, locked or occupied plate).</summary>
        public string BaseKey { get; }

        /// <summary>The machine body key, or null for an empty or locked slot.</summary>
        public string MachineKey { get; }

        /// <summary>The single state badge key, or null.</summary>
        public string BadgeKey { get; }

        /// <summary>The text cue of the badge (RUN, WAIT, IN!, FULL, PAUS, OFF), or null.</summary>
        public string BadgeText { get; }

        public bool Boosted { get; }
        public bool Overdrive { get; }
        public bool Selected { get; }

        /// <summary>The factory level that opens a locked slot, otherwise 0.</summary>
        public int UnlockLevel { get; }

        /// <summary>Plain-language description, so no state depends on color alone.</summary>
        public string Description { get; }

        public int MarkerCount => (Boosted ? 1 : 0) + (Overdrive ? 1 : 0);
    }

    /// <summary>The pure rules that turn the run read model into slot visuals (Art Bible sections 5 and 6).</summary>
    public static class SlotGridModel
    {
        public const string SlotEmptyKey = "F003-SLOT-EMPTY";
        public const string SlotOccupiedKey = "F003-SLOT-OCCUPIED-BASE";
        public const string SlotSelectedFrameKey = "F003-SLOT-SELECTED-FRAME";
        public const string BadgeRunningKey = "F003-BADGE-RUNNING";
        public const string BadgeWaitingKey = "F003-BADGE-WAITING";
        public const string BadgeMissingInputKey = "F003-BADGE-MISSING-INPUT";
        public const string BadgeStorageFullKey = "F003-BADGE-STORAGE-FULL";
        public const string BadgePausedKey = "F003-BADGE-PAUSED";
        public const string BadgeInactiveKey = "F003-BADGE-INACTIVE";
        public const string MarkerBoostedKey = "F003-MARKER-BOOSTED";
        public const string MarkerOverdriveKey = "F003-MARKER-OVERDRIVE";

        public static string LockedKey(int unlockLevel) => "F003-SLOT-LOCKED-L" + unlockLevel.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// The visual of slot <paramref name="index"/>: a pure function of the slot index and the read model. A slot index
        /// beyond the snapshot is treated as a locked slot of the highest level, so a grid that outgrows its data never throws.
        /// </summary>
        /// <param name="boostedMachineIds">Machine type IDs that an active Item effect targets; null for none.</param>
        public static SlotVisual Describe(
            int index,
            RunFactorySnapshot snapshot,
            Func<string, string> presentationKeyOf,
            bool overdriveActive,
            ISet<string> boostedMachineIds,
            int selectedSlot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (presentationKeyOf == null) throw new ArgumentNullException(nameof(presentationKeyOf));
            var selected = index == selectedSlot;
            var label = "Slot " + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (index < 0 || index >= snapshot.Slots.Count)
            {
                return new SlotVisual(index, SlotCondition.Locked, LockedKey(5), null, null, null, false, false, false, 5, label + ", locked");
            }

            var slot = snapshot.Slots[index];
            switch (slot.Status)
            {
                case SlotStatus.Locked:
                {
                    var level = Math.Max(2, Math.Min(5, slot.UnlocksAtFactoryLevel));
                    return new SlotVisual(index, SlotCondition.Locked, LockedKey(level), null, null, null, false, false, false,
                        slot.UnlocksAtFactoryLevel, label + ", locked until factory level " + slot.UnlocksAtFactoryLevel);
                }
                case SlotStatus.Occupied:
                {
                    var machine = snapshot.Machines[slot.MachineIndex];
                    var key = BadgeFor(machine, out var text, out var word);
                    var boosted = boostedMachineIds != null && boostedMachineIds.Contains(machine.MachineId);
                    var description = label + ", " + machine.Label + " level " + machine.Level + ", " + word +
                        (boosted ? ", boosted by an Item" : string.Empty) + (overdriveActive ? ", Overdrive" : string.Empty);
                    return new SlotVisual(index, SlotCondition.Occupied, SlotOccupiedKey, presentationKeyOf(machine.MachineId), key, text, boosted,
                        overdriveActive, selected, 0, description);
                }
                default:
                    return new SlotVisual(index, SlotCondition.Empty, SlotEmptyKey, null, null, null, false, false, selected, 0,
                        label + ", empty");
            }
        }

        /// <summary>Every slot of the snapshot in index order.</summary>
        public static SlotVisual[] DescribeAll(RunFactorySnapshot snapshot, Func<string, string> presentationKeyOf, bool overdriveActive, ISet<string> boostedMachineIds, int selectedSlot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var visuals = new SlotVisual[snapshot.Slots.Count];
            for (var i = 0; i < visuals.Length; i++)
            {
                visuals[i] = Describe(i, snapshot, presentationKeyOf, overdriveActive, boostedMachineIds, selectedSlot);
            }

            return visuals;
        }

        /// <summary>The badge of an occupied slot by the precedence table of Art Bible section 6.2.</summary>
        public static string BadgeFor(MachineSnapshot machine, out string text, out string word) =>
            BadgeFor(machine.IsActive, machine.Blocker, machine.Phase, out text, out word);

        /// <summary>The badge key, its text cue and a plain word for a machine state (Art Bible section 6.2, ranks 1 to 6).</summary>
        public static string BadgeFor(bool isActive, MachineBlocker blocker, MachinePhase phase, out string text, out string word)
        {
            if (!isActive || blocker == MachineBlocker.Inactive)
            {
                text = "OFF";
                word = "inactive";
                return BadgeInactiveKey;
            }

            switch (blocker)
            {
                case MachineBlocker.MissingInput:
                    text = "IN!";
                    word = "missing input";
                    return BadgeMissingInputKey;
                case MachineBlocker.StorageFull:
                    text = "FULL";
                    word = "storage full";
                    return BadgeStorageFullKey;
            }

            switch (phase)
            {
                case MachinePhase.Waiting:
                    text = "WAIT";
                    word = "waiting";
                    return BadgeWaitingKey;
                case MachinePhase.Paused:
                    text = "PAUS";
                    word = "paused";
                    return BadgePausedKey;
                default:
                    text = "RUN";
                    word = "running";
                    return BadgeRunningKey;
            }
        }
    }

    /// <summary>
    /// The fixed 12-slot grid geometry (Art Bible section 2): a slot is 2 x 2 cells at a pitch of 2.5 cells, the grid is
    /// 3 columns by 4 rows on a portrait screen and 4 by 3 on a landscape one. Pure; the world origin is the lower-left
    /// corner of slot column 0 in the bottom row.
    /// </summary>
    public static class SlotGridLayout
    {
        public const int SlotCount = 12;
        public const float SlotSize = 2f;
        public const float SlotPitch = 2.5f;
        public const float HalfCellMargin = 0.5f;

        /// <summary>The column count for a screen: 3 on a portrait screen, 4 on a landscape one.</summary>
        public static int ColumnsFor(float screenWidth, float screenHeight) => screenWidth > screenHeight ? 4 : 3;

        public static int RowsFor(int columns) => (SlotCount + columns - 1) / columns;

        public static Rect Extent(int columns)
        {
            var rows = RowsFor(columns);
            return new Rect(0f, 0f, (columns - 1) * SlotPitch + SlotSize, (rows - 1) * SlotPitch + SlotSize);
        }

        /// <summary>The slot's lower-left corner in world units; slot 0 is top-left in reading order.</summary>
        public static Vector2 SlotOrigin(int index, int columns)
        {
            var rows = RowsFor(columns);
            var column = index % columns;
            var rowFromTop = index / columns;
            return new Vector2(column * SlotPitch, (rows - 1 - rowFromTop) * SlotPitch);
        }

        public static Rect SlotRect(int index, int columns) => new Rect(SlotOrigin(index, columns), new Vector2(SlotSize, SlotSize));

        /// <summary>
        /// The slot under a world point, or -1. The gap between slots counts for the nearer slot, so a tap never falls
        /// through a 0.5 cell gap; a point beyond the grid extent plus half a gap is a miss.
        /// </summary>
        public static int HitTest(Vector2 world, int columns)
        {
            var tolerance = (SlotPitch - SlotSize) * 0.5f;
            var rows = RowsFor(columns);
            var column = Mathf.FloorToInt((world.x + tolerance) / SlotPitch);
            var row = Mathf.FloorToInt((world.y + tolerance) / SlotPitch);
            if (column < 0 || column >= columns || row < 0 || row >= rows)
            {
                return -1;
            }

            var index = (rows - 1 - row) * columns + column;
            return index < SlotCount ? index : -1;
        }

        /// <summary>The pixels-per-cell (screen points per cell) at which the whole grid plus a half-cell margin fits an area.</summary>
        public static float FitAllPixelsPerCell(float areaWidthPoints, float areaHeightPoints, int columns)
        {
            var extent = Extent(columns);
            var widthCells = extent.width + 2f * HalfCellMargin;
            var heightCells = extent.height + 2f * HalfCellMargin;
            return Mathf.Min(areaWidthPoints / widthCells, areaHeightPoints / heightCells);
        }
    }

    /// <summary>
    /// Replaceable zoom limits in pixels per cell (screen points per cell, Art Bible section 8.1). Feature 004 changes these
    /// values (for example 32 to 128 for integer pixel-perfect steps) without touching camera or layout code.
    /// </summary>
    [Serializable]
    public sealed class RunZoomSettings
    {
        [Min(1f)] public float minimumPixelsPerCell = 16f;
        [Min(1f)] public float maximumPixelsPerCell = 64f;

        /// <summary>The fit-all view never zooms out below this value (Art Bible section 8.2, the Z24 floor).</summary>
        [Min(1f)] public float fitAllFloorPixelsPerCell = 24f;

        public float Clamp(float pixelsPerCell) => Mathf.Clamp(pixelsPerCell, minimumPixelsPerCell, maximumPixelsPerCell);
    }

    /// <summary>
    /// Draws the 12 slots with the approved F003 sprites. It keeps at most six renderers per slot (plate, machine, selection
    /// frame, one badge, two markers; Art Bible section 6.1) and owns no gameplay state: <see cref="Render"/> takes the pure
    /// <see cref="SlotVisual"/> values. Tapping resolves through <see cref="TryHitSlot"/>.
    /// </summary>
    public sealed class SlotGridView : MonoBehaviour
    {
        // Sorting orders of the Art Bible (section 2).
        public const int PlateOrder = 10;
        public const int MachineOrder = 100;
        public const int FrameOrder = 150;
        public const int OverlayOrder = 300;

        private const float BadgeNaturalWidth = 72f / 64f;
        private const float MarkerNaturalSize = 28f / 64f;
        private const float MinimumBadgeWidthPoints = 27f;
        private const float MinimumMarkerPoints = 10f;
        private const float MarkerMinimumPixelsPerCell = 24f;

        private static readonly Vector2 BadgeCenter = new Vector2(1.0f, 1.75f);
        private static readonly Vector2 BoostedCenter = new Vector2(14f / 64f, 114f / 64f);
        private static readonly Vector2 OverdriveCenter = new Vector2(114f / 64f, 114f / 64f);

        private PresentationSpriteCatalog sprites;
        private SlotRenderers[] slots;
        private SlotVisual[] visuals = Array.Empty<SlotVisual>();
        private int columns = 3;
        private float pixelsPerCell = 64f;

        public int Columns => columns;
        public IReadOnlyList<SlotVisual> Visuals => visuals;

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

        public void Configure(PresentationSpriteCatalog catalog, int slotCount = SlotGridLayout.SlotCount)
        {
            sprites = catalog ?? throw new ArgumentNullException(nameof(catalog));
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
                slots[i] = new SlotRenderers(transform, i);
            }

            Reposition();
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
        /// Scales the badges and markers for the projected size (Art Bible section 8.2): a badge is at least 27 points wide,
        /// markers at least 10 points and hidden below 24 pixels per cell.
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

        public static bool MarkersVisibleAt(float pixelsPerCell) => pixelsPerCell >= MarkerMinimumPixelsPerCell;

        public bool TryHitSlot(Vector2 world, out int slotIndex)
        {
            slotIndex = SlotGridLayout.HitTest(world, columns);
            return slotIndex >= 0;
        }

        public Rect WorldRectOf(int slotIndex) => SlotGridLayout.SlotRect(slotIndex, columns);

        public void Render(IReadOnlyList<SlotVisual> newVisuals)
        {
            if (sprites == null || slots == null)
            {
                throw new InvalidOperationException("The slot grid is not configured.");
            }

            if (newVisuals == null) throw new ArgumentNullException(nameof(newVisuals));
            var copy = new SlotVisual[newVisuals.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = newVisuals[i];
            }

            visuals = copy;
            for (var i = 0; i < slots.Length; i++)
            {
                if (i < visuals.Length)
                {
                    slots[i].Show(visuals[i], sprites);
                }
                else
                {
                    slots[i].Hide();
                }
            }

            ApplyOverlayScale();
        }

        private void Reposition()
        {
            if (slots == null)
            {
                return;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                slots[i].SetOrigin(SlotGridLayout.SlotOrigin(i, columns));
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
                slot.ApplyOverlay(badge, marker, markersVisible);
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

        private sealed class SlotRenderers
        {
            private readonly Transform root;
            private readonly SpriteRenderer plate;
            private readonly SpriteRenderer machine;
            private readonly SpriteRenderer frame;
            private readonly SpriteRenderer badge;
            private readonly SpriteRenderer boosted;
            private readonly SpriteRenderer overdrive;
            private bool hasBoosted;
            private bool hasOverdrive;

            public SlotRenderers(Transform parent, int index)
            {
                var slotObject = new GameObject("Slot " + (index + 1));
                slotObject.transform.SetParent(parent, false);
                root = slotObject.transform;
                plate = Create("Plate", PlateOrder);
                machine = Create("Machine", MachineOrder);
                frame = Create("Selection", FrameOrder);
                badge = Create("Badge", OverlayOrder);
                boosted = Create("Boosted", OverlayOrder);
                overdrive = Create("Overdrive", OverlayOrder);
                badge.transform.localPosition = BadgeCenter;
                boosted.transform.localPosition = BoostedCenter;
                overdrive.transform.localPosition = OverdriveCenter;
            }

            public int ActiveCount =>
                (plate.enabled ? 1 : 0) + (machine.enabled ? 1 : 0) + (frame.enabled ? 1 : 0) +
                (badge.enabled ? 1 : 0) + (boosted.enabled ? 1 : 0) + (overdrive.enabled ? 1 : 0);

            public void SetOrigin(Vector2 origin) => root.localPosition = new Vector3(origin.x, origin.y, 0f);

            public void Show(SlotVisual visual, PresentationSpriteCatalog catalog)
            {
                Apply(plate, catalog, visual.BaseKey);
                Apply(machine, catalog, visual.MachineKey);
                Apply(frame, catalog, visual.Selected ? SlotGridModel.SlotSelectedFrameKey : null);
                Apply(badge, catalog, visual.BadgeKey);
                hasBoosted = Apply(boosted, catalog, visual.Boosted ? SlotGridModel.MarkerBoostedKey : null);
                hasOverdrive = Apply(overdrive, catalog, visual.Overdrive ? SlotGridModel.MarkerOverdriveKey : null);
            }

            public void Hide()
            {
                plate.enabled = machine.enabled = frame.enabled = badge.enabled = boosted.enabled = overdrive.enabled = false;
                hasBoosted = hasOverdrive = false;
            }

            public void ApplyOverlay(float badgeScale, float markerScale, bool markersVisible)
            {
                badge.transform.localScale = new Vector3(badgeScale, badgeScale, 1f);
                boosted.transform.localScale = new Vector3(markerScale, markerScale, 1f);
                overdrive.transform.localScale = new Vector3(markerScale, markerScale, 1f);
                boosted.enabled = hasBoosted && markersVisible;
                overdrive.enabled = hasOverdrive && markersVisible;
            }

            public void Destroy()
            {
                if (root != null)
                {
                    UnityEngine.Object.Destroy(root.gameObject);
                }
            }

            private SpriteRenderer Create(string name, int order)
            {
                var child = new GameObject(name);
                child.transform.SetParent(root, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = order;
                renderer.enabled = false;
                return renderer;
            }

            private static bool Apply(SpriteRenderer renderer, PresentationSpriteCatalog catalog, string key)
            {
                if (key != null && catalog.TryGet(key, out var sprite))
                {
                    renderer.sprite = sprite;
                    renderer.enabled = true;
                    return true;
                }

                renderer.sprite = null;
                renderer.enabled = false;
                return false;
            }
        }
    }
}
