using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.Rendering;

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

        /// <summary>
        /// The machine state by the precedence table of Art Bible section 6.2, ranks 1 to 6: inactive, missing input, storage full,
        /// waiting, paused, running. This is the only precedence rule; <see cref="BadgeFor(bool, MachineBlocker, MachinePhase, out string, out string)"/>
        /// and the state-cue set both follow it.
        /// </summary>
        public static MachineVisualState StateFor(bool isActive, MachineBlocker blocker, MachinePhase phase)
        {
            if (!isActive || blocker == MachineBlocker.Inactive)
            {
                return MachineVisualState.Inactive;
            }

            switch (blocker)
            {
                case MachineBlocker.MissingInput:
                    return MachineVisualState.MissingInput;
                case MachineBlocker.StorageFull:
                    return MachineVisualState.StorageFull;
            }

            switch (phase)
            {
                case MachinePhase.Waiting:
                    return MachineVisualState.Waiting;
                case MachinePhase.Paused:
                    return MachineVisualState.Paused;
                default:
                    return MachineVisualState.Running;
            }
        }

        /// <summary>The badge key of a machine state.</summary>
        public static string BadgeKeyOf(MachineVisualState state)
        {
            switch (state)
            {
                case MachineVisualState.Inactive: return BadgeInactiveKey;
                case MachineVisualState.MissingInput: return BadgeMissingInputKey;
                case MachineVisualState.StorageFull: return BadgeStorageFullKey;
                case MachineVisualState.Waiting: return BadgeWaitingKey;
                case MachineVisualState.Paused: return BadgePausedKey;
                default: return BadgeRunningKey;
            }
        }

        /// <summary>The badge key, its text cue and a plain word for a machine state (Art Bible section 6.2, ranks 1 to 6).</summary>
        public static string BadgeFor(bool isActive, MachineBlocker blocker, MachinePhase phase, out string text, out string word)
        {
            var state = StateFor(isActive, blocker, phase);
            switch (state)
            {
                case MachineVisualState.Inactive: text = "OFF"; word = "inactive"; break;
                case MachineVisualState.MissingInput: text = "IN!"; word = "missing input"; break;
                case MachineVisualState.StorageFull: text = "FULL"; word = "storage full"; break;
                case MachineVisualState.Waiting: text = "WAIT"; word = "waiting"; break;
                case MachineVisualState.Paused: text = "PAUS"; word = "paused"; break;
                default: text = "RUN"; word = "running"; break;
            }

            return BadgeKeyOf(state);
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
    /// The Art Bible inputs of the integer camera scale (Art Bible v1.0 section 8, <c>contracts/camera-scale.md</c>): the art density
    /// D (sprite pixels per unit), the smallest readable cell in UI points and the largest scale. The old pixels-per-cell limits
    /// (16 to 64, fit-all floor 24) are gone: the allowed scales are <c>[n_min, maxScale]</c> and <c>n_min</c> derives from the
    /// readable cell and the panel scale. The values change here only; the scene's older serialized block is dropped by its next
    /// re-save through Unity tooling (no hand edit).
    /// </summary>
    [Serializable]
    public sealed class RunZoomSettings
    {
        /// <summary>D: art pixels per cell, equal to the sprite pixels per unit (Art Bible v1.0: 24).</summary>
        [Min(1)] public int artPixelsPerCell = 24;

        /// <summary>The smallest cell size in UI points at which machines stay identifiable (the Z24 floor of Feature 003).</summary>
        [Min(1)] public int readableCellPoints = 24;

        /// <summary>The largest allowed integer scale.</summary>
        [Min(1)] public int maxScale = 6;

        public ArtScaleSettings ToScaleSettings() => new ArtScaleSettings(artPixelsPerCell, readableCellPoints, maxScale);
    }

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

        public int Columns => columns;
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
        public Vector2 FootprintPivotOf(int slotIndex) => SlotGridLayout.SlotOrigin(slotIndex, columns) + StateCueGeometry.FootprintPivotInSlot;

        /// <summary>The sorting row of a slot: 0 for the top (back) row, increasing toward the front.</summary>
        public int RowOf(int slotIndex) => slotIndex / columns;

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
                slots[i].SetOrigin(SlotGridLayout.SlotOrigin(i, columns), i / columns);
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

        private sealed class SlotRenderers
        {
            private readonly Transform root;
            private readonly SortingGroup bodyGroup;
            private readonly SpriteRenderer plate;
            private readonly SpriteRenderer machine;
            private readonly SpriteRenderer accent;
            private readonly SpriteRenderer frame;
            private readonly SpriteRenderer label;
            private readonly SpriteRenderer badge;
            private readonly SpriteRenderer boosted;
            private readonly SpriteRenderer overdrive;
            private readonly SpriteRenderer[] all;
            private bool hasBoosted;
            private bool hasOverdrive;
            private bool hasLabel;
            private LabelPolicy labelPolicy = LabelPolicy.Never;
            private PresentationSpriteEntry machineEntry;
            private PresentationSpriteEntry accentEntry;
            private bool animated;
            private int machineFrame;
            private int accentFrame;

            public SlotRenderers(Transform parent, int index, int overlayLayerId)
            {
                var slotObject = new GameObject("Slot " + (index + 1));
                slotObject.transform.SetParent(parent, false);
                root = slotObject.transform;

                // The machine's one Sorting Group: plate, machine and accent sort among themselves and, as a unit, by slot row.
                var body = new GameObject("Body");
                body.transform.SetParent(root, false);
                bodyGroup = body.AddComponent<SortingGroup>();
                plate = Create(body.transform, "Plate", PlateOrder, 0);
                machine = Create(body.transform, "Machine", MachineOrder, 0);
                accent = Create(body.transform, "Accent", AccentOrder, 0);

                // Overlays are outside the group, on the overlay layer, so no machine art of any row can cover them.
                frame = Create(root, "Selection", FrameOrder, overlayLayerId);
                label = Create(root, "Label", OverlayOrder, overlayLayerId);
                boosted = Create(root, "Boosted", OverlayOrder + 1, overlayLayerId);
                overdrive = Create(root, "Overdrive", OverlayOrder + 2, overlayLayerId);
                badge = Create(root, "Badge", OverlayOrder + 3, overlayLayerId);
                all = new[] { plate, machine, accent, frame, label, badge, boosted, overdrive };
            }

            public IReadOnlyList<SpriteRenderer> All => all;

            public bool AccentActive => accent.enabled;

            public int ActiveCount
            {
                get
                {
                    var count = 0;
                    foreach (var renderer in all)
                    {
                        count += renderer.enabled ? 1 : 0;
                    }

                    return count;
                }
            }

            public SpriteRenderer Get(SlotRendererRole role)
            {
                switch (role)
                {
                    case SlotRendererRole.Plate: return plate;
                    case SlotRendererRole.Machine: return machine;
                    case SlotRendererRole.Accent: return accent;
                    case SlotRendererRole.Frame: return frame;
                    case SlotRendererRole.Label: return label;
                    case SlotRendererRole.Badge: return badge;
                    case SlotRendererRole.Boosted: return boosted;
                    case SlotRendererRole.Overdrive: return overdrive;
                    default: return null;
                }
            }

            public void SetOrigin(Vector2 slotOrigin, int row)
            {
                root.localPosition = new Vector3(slotOrigin.x, slotOrigin.y, 0f);
                bodyGroup.sortingOrder = row;
                frame.sortingOrder = FrameOrder + row;
                label.sortingOrder = OverlayOrder + row * 4;
                boosted.sortingOrder = OverlayOrder + row * 4 + 1;
                overdrive.sortingOrder = OverlayOrder + row * 4 + 2;
                badge.sortingOrder = OverlayOrder + row * 4 + 3;
            }

            public void Show(SlotVisual visual, PresentationSpriteCatalog catalog, StateCueSet cues)
            {
                var hasRow = cues.TryGetRowForBadgeKey(visual.BadgeKey, out var cueRow);
                var running = hasRow && cueRow.State == MachineVisualState.Running;
                labelPolicy = StateCueSet.LabelPolicyFor(visual.Condition);

                Apply(plate, catalog, visual.BaseKey);
                machineEntry = Apply(machine, catalog, visual.MachineKey);
                accentEntry = running ? Apply(accent, catalog, StateCueSet.AccentKeyFor(visual.MachineKey)) : Apply(accent, catalog, null);
                animated = running;
                machineFrame = 0;
                accentFrame = 0;

                Apply(frame, catalog, visual.Selected ? StateCueSet.SelectionFrameKey : null);
                hasLabel = Apply(label, catalog, StateCueSet.LabelKeyFor(visual.Condition, visual.UnlockLevel)) != null;

                // One badge per occupied slot, from the cue row of the resolved state. The running row is intentionally empty
                // (owner decision R8): its key resolves to an entry without a sprite and the view draws nothing for it.
                Apply(badge, catalog, hasRow && !cueRow.IntentionallyEmpty ? cueRow.CueIconKey : null);
                hasBoosted = Apply(boosted, catalog, visual.Boosted ? StateCueSet.MarkerKeys[0] : null) != null;
                hasOverdrive = Apply(overdrive, catalog, visual.Overdrive ? StateCueSet.MarkerKeys[1] : null) != null;

                PlaceLayer(plate);
                PlaceLayer(machine);
                PlaceLayer(accent);
                PlaceLayer(frame);
                PlaceLayer(label);
            }

            public void Hide()
            {
                foreach (var renderer in all)
                {
                    renderer.enabled = false;
                }

                hasBoosted = hasOverdrive = hasLabel = false;
                machineEntry = accentEntry = null;
                animated = false;
            }

            public void Advance(long milliseconds, bool frozen)
            {
                if (!animated)
                {
                    return;
                }

                Step(machine, machineEntry, milliseconds, frozen, ref machineFrame);
                Step(accent, accentEntry, milliseconds, frozen, ref accentFrame);
            }

            public void ApplyOverlay(float badgeScale, float markerScale, bool markersVisible, float pointsPerCell)
            {
                // A canvas layer (the F004 art) is authored at its final size and keeps scale 1; only a tight placeholder scales.
                var badgeFactor = StateCueGeometry.IsCanvasLayer(badge.sprite) ? 1f : badgeScale;
                badge.transform.localScale = new Vector3(badgeFactor, badgeFactor, 1f);
                badge.transform.localPosition = BadgePosition(badge.sprite, badgeFactor);
                PlaceMarker(boosted, markerScale, StateCueGeometry.Marker1Offset);
                PlaceMarker(overdrive, markerScale, StateCueGeometry.Marker2Offset);
                boosted.enabled = hasBoosted && markersVisible;
                overdrive.enabled = hasOverdrive && markersVisible;
                label.enabled = hasLabel && labelPolicy.IsShown(pointsPerCell, StateCueGeometry.ArtPixelsPerCell);
            }

            public void Destroy()
            {
                if (root != null)
                {
                    UnityEngine.Object.Destroy(root.gameObject);
                }
            }

            // A canvas layer sits on the footprint pivot; the F003 placeholders keep their bottom-left pivot on the slot corner.
            private static void PlaceLayer(SpriteRenderer renderer)
            {
                var pivot = StateCueGeometry.IsCanvasLayer(renderer.sprite) ? StateCueGeometry.FootprintPivotInSlot : Vector2.zero;
                renderer.transform.localPosition = new Vector3(pivot.x, pivot.y, 0f);
            }

            private static Vector3 BadgePosition(Sprite sprite, float factor)
            {
                var pivot = StateCueGeometry.FootprintPivotInSlot;
                if (sprite == null || StateCueGeometry.IsCanvasLayer(sprite))
                {
                    return new Vector3(pivot.x, pivot.y, 0f);
                }

                // A tight (centre pivot) placeholder: its bottom-centre rests on the Art Bible anchor (59, 33).
                var height = sprite.rect.height / sprite.pixelsPerUnit * factor;
                var anchor = pivot + StateCueGeometry.BadgeAnchorOffset;
                return new Vector3(anchor.x, anchor.y + height * 0.5f, 0f);
            }

            private static void PlaceMarker(SpriteRenderer renderer, float markerScale, Vector2 offset)
            {
                var canvas = StateCueGeometry.IsCanvasLayer(renderer.sprite);
                var factor = canvas ? 1f : markerScale;
                renderer.transform.localScale = new Vector3(factor, factor, 1f);
                var center = StateCueGeometry.FootprintPivotInSlot + (canvas ? Vector2.zero : offset);
                renderer.transform.localPosition = new Vector3(center.x, center.y, 0f);
            }

            private static void Step(SpriteRenderer renderer, PresentationSpriteEntry entry, long milliseconds, bool frozen, ref int shown)
            {
                if (entry == null || !renderer.enabled)
                {
                    return;
                }

                var frameIndex = frozen ? 0 : FrameAt(milliseconds, entry.FramesPerSecond ?? 0f, entry.FrameCount);
                if (frameIndex == shown)
                {
                    return;
                }

                shown = frameIndex;
                renderer.sprite = entry.GetFrame(frameIndex);
            }

            private SpriteRenderer Create(Transform parent, string name, int order, int layerId)
            {
                var child = new GameObject(name);
                child.transform.SetParent(parent, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sortingLayerID = layerId;
                renderer.sortingOrder = order;
                renderer.enabled = false;
                return renderer;
            }

            private static PresentationSpriteEntry Apply(SpriteRenderer renderer, PresentationSpriteCatalog catalog, string key)
            {
                if (key != null && catalog.TryGetEntry(key, out var entry))
                {
                    renderer.sprite = entry.Sprite;
                    renderer.enabled = true;
                    return entry;
                }

                renderer.sprite = null;
                renderer.enabled = false;
                return null;
            }
        }
    }
}
