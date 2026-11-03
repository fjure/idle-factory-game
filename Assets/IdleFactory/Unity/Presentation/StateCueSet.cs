using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// The machine state that <see cref="SlotGridModel.StateFor"/> resolves with the Art Bible section 6.2 precedence. It is the
    /// only machine-state vocabulary of the view: <see cref="SlotGridModel.BadgeFor(bool, IdleFactory.Run.Application.MachineBlocker, IdleFactory.Run.Application.MachinePhase, out string, out string)"/>
    /// maps the same value to its badge key, so there is exactly one precedence rule.
    /// </summary>
    public enum MachineVisualState
    {
        Running = 0,
        Waiting = 1,
        MissingInput = 2,
        StorageFull = 3,
        Paused = 4,
        Inactive = 5,
    }

    /// <summary>
    /// The per-scale rule of a text label in the world (research R06, Art Bible 8.4): the label is shown only when its cap height
    /// is at least <see cref="MinimumCapPoints"/> at the projected size. A policy with no cap height never shows a label (the machine
    /// panel always names the state in text).
    /// </summary>
    public readonly struct LabelPolicy
    {
        /// <summary>The label threshold of research R06 and Art Bible 8.4, in UI points.</summary>
        public const float MinimumCapPoints = 12f;

        private LabelPolicy(int capHeightArtPixels)
        {
            CapHeightArtPixels = capHeightArtPixels;
        }

        /// <summary>No world label at any scale.</summary>
        public static LabelPolicy Never => new LabelPolicy(0);

        /// <summary>A label of the given cap height in art pixels, shown from 12 points of cap height upward.</summary>
        public static LabelPolicy FromCapHeight(int capHeightArtPixels) => new LabelPolicy(Math.Max(0, capHeightArtPixels));

        public int CapHeightArtPixels { get; }

        public bool HasLabel => CapHeightArtPixels > 0;

        /// <summary>The cap height in UI points at a projected size: <c>cap_px * n / pxPerPoint</c>, written as <c>cap_px * pointsPerCell / D</c>.</summary>
        public float CapHeightPoints(float pointsPerCell, int artPixelsPerCell) =>
            artPixelsPerCell <= 0 ? 0f : CapHeightArtPixels * pointsPerCell / artPixelsPerCell;

        public bool IsShown(float pointsPerCell, int artPixelsPerCell) =>
            HasLabel && CapHeightPoints(pointsPerCell, artPixelsPerCell) >= MinimumCapPoints;
    }

    /// <summary>
    /// One row of the machine-state table of the state-cue set (data-model "State-cue set"): what the view draws for one
    /// <see cref="MachineVisualState"/>. Under cue variant b (owner decision S3) the body is unchanged for every state, so a row
    /// carries no body-variant key; a non-running state shows frame 0 of the same body entry and only running animates.
    /// </summary>
    public readonly struct StateCueRow
    {
        public StateCueRow(MachineVisualState state, string badgeKey, string cueIconKey, bool intentionallyEmpty, bool animated, LabelPolicy labelPolicy)
        {
            State = state;
            BadgeKey = badgeKey;
            CueIconKey = cueIconKey;
            IntentionallyEmpty = intentionallyEmpty;
            Animated = animated;
            LabelPolicy = labelPolicy;
        }

        public MachineVisualState State { get; }

        /// <summary>The key <c>SlotGridModel.BadgeFor</c> returns for the state (an F003 key; its primary frames are the F004 art).</summary>
        public string BadgeKey { get; }

        /// <summary>The key of the cue sprite the view draws, or null when the entry is intentionally empty.</summary>
        public string CueIconKey { get; }

        /// <summary>True for the running badge (owner decision R8): the key resolves to an entry without a sprite and nothing is drawn.</summary>
        public bool IntentionallyEmpty { get; }

        /// <summary>True only for running: the body (and its accent) play their loop on the shared clock.</summary>
        public bool Animated { get; }

        public LabelPolicy LabelPolicy { get; }
    }

    /// <summary>
    /// Overlay geometry of the Art Bible (section 3.2) for the art density D = 24. Everything is derived from art-pixel boxes of the
    /// 80 x 64 machine canvas, whose pivot is the footprint pivot (40, 15). Boxes use the Art Bible convention: pixel indices, y down,
    /// the pivot at pixel (40, 49 from the top), offsets in whole art pixels (P3) and world units equal to pixels / 24.
    /// </summary>
    public static class StateCueGeometry
    {
        /// <summary>The art density D: sprite pixels per cell (Art Bible v1.0).</summary>
        public const int ArtPixelsPerCell = 24;

        public const int CanvasWidthPixels = 80;
        public const int CanvasHeightPixels = 64;
        public const int PivotXPixels = 40;
        public const int PivotYFromBottomPixels = 15;

        public const int BadgeSizePixels = 23;
        public const int MarkerSizePixels = 11;

        // Pixel indices, y down (Art Bible 3.2 "Cue geometry"). The pivot sits at pixel (40, 49 from the top).
        private const int PivotRowFromTop = CanvasHeightPixels - PivotYFromBottomPixels;
        private const int BadgeAnchorX = 59;
        private const int BadgeAnchorRowFromTop = 33;
        private const int Marker1CenterX = 29;
        private const int Marker2CenterX = 41;
        private const int MarkerCenterRowFromTop = 21;

        /// <summary>The canvas size in world units: 80 x 64 art pixels at 24 pixels per unit.</summary>
        public static readonly Vector2 CanvasWorldSize = new Vector2(CanvasWidthPixels / (float)ArtPixelsPerCell, CanvasHeightPixels / (float)ArtPixelsPerCell);

        /// <summary>The normalized pivot of every canvas layer: (0.5, 0.234375).</summary>
        public static readonly Vector2 CanvasPivot = new Vector2(PivotXPixels / (float)CanvasWidthPixels, PivotYFromBottomPixels / (float)CanvasHeightPixels);

        /// <summary>
        /// The footprint pivot in slot-local world units (lower-left corner of the slot square = origin): centred on the slot and
        /// resting 15 art pixels above the slot bottom. This is the one constant that ties the overlay anchors to the lattice; the
        /// isometric lattice (T456) replaces it, and every overlay anchor follows because it is relative to this point.
        /// </summary>
        public static readonly Vector2 FootprintPivotInSlot = new Vector2(SlotGridLayout.SlotSize * 0.5f, PivotYFromBottomPixels / (float)ArtPixelsPerCell);

        /// <summary>The bottom-centre anchor of the badge relative to the footprint pivot: +19 px right, +16 px up = (0.7917, 0.6667) world units.</summary>
        public static readonly Vector2 BadgeAnchorOffset = Offset(BadgeAnchorX, BadgeAnchorRowFromTop);

        /// <summary>The centre of marker box M1 (x 24..34, rows 16..26) relative to the footprint pivot.</summary>
        public static readonly Vector2 Marker1Offset = Offset(Marker1CenterX, MarkerCenterRowFromTop);

        /// <summary>The centre of marker box M2 (x 36..46, rows 16..26) relative to the footprint pivot.</summary>
        public static readonly Vector2 Marker2Offset = Offset(Marker2CenterX, MarkerCenterRowFromTop);

        public static readonly float BadgeSizeWorld = BadgeSizePixels / (float)ArtPixelsPerCell;
        public static readonly float MarkerSizeWorld = MarkerSizePixels / (float)ArtPixelsPerCell;

        /// <summary>The badge box (23 x 23 art px, bottom-centre anchored) relative to the footprint pivot, in world units.</summary>
        public static Rect BadgeBox() => new Rect(
            BadgeAnchorOffset.x - BadgeSizeWorld * 0.5f, BadgeAnchorOffset.y, BadgeSizeWorld, BadgeSizeWorld);

        /// <summary>A marker box (11 x 11 art px) around its centre, relative to the footprint pivot, in world units.</summary>
        public static Rect MarkerBox(int marker) =>
            new Rect((marker == 0 ? Marker1Offset : Marker2Offset) - new Vector2(MarkerSizeWorld, MarkerSizeWorld) * 0.5f, new Vector2(MarkerSizeWorld, MarkerSizeWorld));

        private static Vector2 Offset(int pixelX, int rowFromTop) =>
            new Vector2((pixelX - PivotXPixels) / (float)ArtPixelsPerCell, (PivotRowFromTop - rowFromTop) / (float)ArtPixelsPerCell);

        /// <summary>
        /// True for a full machine-canvas layer (80 x 64 art px at 24 PPU): the anchor is baked into its pixels and it is placed at the
        /// footprint pivot with scale 1. Any other sprite (the F003 placeholders and the retained fallback frames) is a tight sprite that
        /// the view places on the derived anchor.
        /// </summary>
        public static bool IsCanvasLayer(Sprite sprite)
        {
            if (sprite == null)
            {
                return false;
            }

            var width = sprite.rect.width / sprite.pixelsPerUnit;
            var height = sprite.rect.height / sprite.pixelsPerUnit;
            return Mathf.Abs(width - CanvasWorldSize.x) < 0.01f && Mathf.Abs(height - CanvasWorldSize.y) < 0.01f;
        }
    }

    /// <summary>
    /// The state-cue set of Feature 004 (data-model "State-cue set", contracts/presentation-and-state-cues.md): authored presentation data
    /// that maps the machine state, which <see cref="SlotGridModel.BadgeFor(IdleFactory.Run.Application.MachineSnapshot, out string, out string)"/>
    /// has already resolved, to a cue row, and the slot condition (separate tables) to its plate, label and selection frame keys. It is a
    /// pure mapping with no precedence rule of its own and no gameplay reads. The active variant is b (owner decision S3, 2026-10-29): the
    /// body is unchanged for every state, a pixel badge carries the state, and a running machine shows no badge.
    /// </summary>
    public sealed class StateCueSet
    {
        private readonly StateCueRow[] rows;
        private readonly Dictionary<string, int> byBadgeKey = new Dictionary<string, int>(StringComparer.Ordinal);

        public StateCueSet(IEnumerable<StateCueRow> machineStateRows)
        {
            if (machineStateRows == null) throw new ArgumentNullException(nameof(machineStateRows));
            var list = new List<StateCueRow>(machineStateRows);
            rows = new StateCueRow[Enum.GetValues(typeof(MachineVisualState)).Length];
            var filled = new bool[rows.Length];
            foreach (var row in list)
            {
                var index = (int)row.State;
                if (index < 0 || index >= rows.Length || filled[index])
                {
                    throw new ArgumentException("The state-cue set needs exactly one row per machine state.", nameof(machineStateRows));
                }

                filled[index] = true;
                rows[index] = row;
                byBadgeKey[row.BadgeKey] = index;
            }

            foreach (var present in filled)
            {
                if (!present)
                {
                    throw new ArgumentException("The state-cue set needs exactly one row per machine state.", nameof(machineStateRows));
                }
            }
        }

        /// <summary>The Art Bible variant b table. Machine-state rows are keyed by what <c>SlotGridModel.BadgeFor</c> returns.</summary>
        public static StateCueSet Default { get; } = CreateDefault();

        /// <summary>The slot-condition key of the selection frame (an overlay; the frame is not part of a machine state).</summary>
        public const string SelectionFrameKey = SlotGridModel.SlotSelectedFrameKey;

        /// <summary>The keys of the two independent markers, in marker-box order (M1 boosted, M2 Overdrive).</summary>
        public static IReadOnlyList<string> MarkerKeys { get; } = new[] { SlotGridModel.MarkerBoostedKey, SlotGridModel.MarkerOverdriveKey };

        /// <summary>The label policy of the locked-plate digit: a 3 x 5 bitmap font at scale 3, cap height 15 art px (owner decision R4).</summary>
        public static LabelPolicy LockDigitPolicy { get; } = LabelPolicy.FromCapHeight(15);

        public IReadOnlyList<StateCueRow> Rows => rows;

        public StateCueRow RowFor(MachineVisualState state) => rows[(int)state];

        /// <summary>The row whose <c>BadgeFor</c> key is <paramref name="badgeKey"/>; false for an unknown key.</summary>
        public bool TryGetRowForBadgeKey(string badgeKey, out StateCueRow row)
        {
            if (badgeKey != null && byBadgeKey.TryGetValue(badgeKey, out var index))
            {
                row = rows[index];
                return true;
            }

            row = default;
            return false;
        }

        // ------------------------------------------------------------------------------------------------
        // Slot conditions (separate table; no body state)
        // ------------------------------------------------------------------------------------------------

        /// <summary>The plate key of a slot condition (empty, locked at a level, occupied). Matches <see cref="SlotGridModel.Describe"/>.</summary>
        public static string PlateKeyFor(SlotCondition condition, int unlockLevel)
        {
            switch (condition)
            {
                case SlotCondition.Locked:
                    return SlotGridModel.LockedKey(ClampLockLevel(unlockLevel));
                case SlotCondition.Occupied:
                    return SlotGridModel.SlotOccupiedKey;
                default:
                    return SlotGridModel.SlotEmptyKey;
            }
        }

        /// <summary>The overlay label key of a slot condition: <c>F004-LABEL-LOCK-L2</c> to <c>-L5</c> for a locked slot, otherwise null.</summary>
        public static string LabelKeyFor(SlotCondition condition, int unlockLevel) =>
            condition == SlotCondition.Locked
                ? PresentationSpriteCatalog.LockLabelKeyPrefix + ClampLockLevel(unlockLevel).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : null;

        /// <summary>The label policy of a slot condition; only a locked slot carries a world label.</summary>
        public static LabelPolicy LabelPolicyFor(SlotCondition condition) =>
            condition == SlotCondition.Locked ? LockDigitPolicy : LabelPolicy.Never;

        /// <summary>The accent layer key of a machine body key (<c>F003-MACHINE-MINER</c> gives <c>F004-ACCENT-MINER</c>), or null.</summary>
        public static string AccentKeyFor(string machineKey)
        {
            const string BodyPrefix = "F003-MACHINE-";
            return machineKey != null && machineKey.StartsWith(BodyPrefix, StringComparison.Ordinal) && machineKey.Length > BodyPrefix.Length
                ? PresentationSpriteCatalog.AccentKeyPrefix + machineKey.Substring(BodyPrefix.Length)
                : null;
        }

        private static int ClampLockLevel(int level) => Math.Max(2, Math.Min(5, level));

        private static StateCueSet CreateDefault()
        {
            StateCueRow Row(MachineVisualState state)
            {
                var key = SlotGridModel.BadgeKeyOf(state);
                var running = state == MachineVisualState.Running;
                return new StateCueRow(state, key, running ? null : key, running, running, LabelPolicy.Never);
            }

            return new StateCueSet(new[]
            {
                Row(MachineVisualState.Running),
                Row(MachineVisualState.Waiting),
                Row(MachineVisualState.MissingInput),
                Row(MachineVisualState.StorageFull),
                Row(MachineVisualState.Paused),
                Row(MachineVisualState.Inactive),
            });
        }
    }
}
