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
        public const string BadgeRunningKey = PresentationSpriteCatalog.RunningBadgeKey;
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
}
