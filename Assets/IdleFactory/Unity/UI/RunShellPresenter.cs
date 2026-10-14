using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using UnityEngine;

namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// Binds the live <see cref="RunSession"/> to the shell and the slot grid. It reads the run only through the application
    /// contracts (hot reads every frame, snapshots only when the structure or a machine state changed) and mutates only
    /// through <see cref="RunCommands"/>. It holds no economy rule: Overdrive activation, Cash, RP and the store usage are
    /// shown exactly as the application reports them.
    /// </summary>
    public sealed class RunShellPresenter
    {
        private readonly RunSession session;
        private readonly RunCommands commands;
        private readonly RunShellDocument shell;
        private readonly SlotGridView grid;
        private readonly Func<string, string> presentationKeyOf;
        private readonly List<MachineStateView> machineBuffer = new List<MachineStateView>();
        private MachineStateView[] views = new MachineStateView[16];
        private MachineStateView[] previousViews = new MachineStateView[16];
        private int previousViewCount = -1;
        private long structureRevision = -1;
        private long lastCash = long.MinValue;
        private long lastRp = long.MinValue;
        private long lastUsed = long.MinValue;
        private long lastCapacity = long.MinValue;
        private long overdriveSecond = long.MinValue;
        private bool researchStarted;
        private bool factoryDirty = true;
        private RunFactorySnapshot snapshot;
        private HashSet<string> boosted = new HashSet<string>(StringComparer.Ordinal);
        private bool overdriveActive;
        private bool disposed;

        public RunShellPresenter(
            RunSession session,
            RunCommands commands,
            RunShellDocument shell,
            SlotGridView grid,
            Func<string, string> presentationKeyOf)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            this.presentationKeyOf = presentationKeyOf ?? throw new ArgumentNullException(nameof(presentationKeyOf));
            shell.SlotSelectionChanged += HandleSelectionChanged;
            shell.OverdriveRequested += HandleOverdriveRequested;

            // The ore vein mines only through RunCommands.Mine, which owns the rate cap (T359).
            shell.BindOreVein(new RunOreVeinSource(session, commands));
        }

        /// <summary>The latest factory snapshot the grid was drawn from, or null before the first refresh.</summary>
        public RunFactorySnapshot Snapshot => snapshot;

        /// <summary>The slot visuals last rendered.</summary>
        public IReadOnlyList<SlotVisual> Visuals => grid.Visuals;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            shell.SlotSelectionChanged -= HandleSelectionChanged;
            shell.OverdriveRequested -= HandleOverdriveRequested;
            shell.UnbindOreVein();
        }

        /// <summary>Forces the next <see cref="Refresh"/> to rebuild the snapshot, for example after a layout change.</summary>
        public void Invalidate()
        {
            factoryDirty = true;
            structureRevision = -1;
        }

        /// <summary>Per-frame update: hot reads for the strip, a snapshot only when something structural or a machine state changed.</summary>
        public void Refresh()
        {
            if (disposed || !shell.IsReady)
            {
                return;
            }

            var cash = session.ReadCash();
            var rp = session.ReadRp();
            var inventory = session.State.Inventory;
            var used = inventory.Total;
            var capacity = inventory.Capacity;
            var structureChanged = session.StructureRevision != structureRevision;
            if (structureChanged)
            {
                structureRevision = session.StructureRevision;
                factoryDirty = true;
                RefreshStructureDependents();
            }

            if (cash != lastCash)
            {
                lastCash = cash;
                shell.SetCash(cash);
            }

            if (rp != lastRp || structureChanged)
            {
                lastRp = rp;
                shell.SetRp(rp, rp > 0 || researchStarted);
            }

            if (used != lastUsed || capacity != lastCapacity)
            {
                lastUsed = used;
                lastCapacity = capacity;
                shell.SetStore(used, capacity);
            }

            if (MachineStatesChanged())
            {
                factoryDirty = true;
            }

            var second = session.TimelineUtcMs / 1000L;
            if (structureChanged || (second != overdriveSecond && (overdriveActive || OverdriveTimed())))
            {
                overdriveSecond = second;
                var status = RunSnapshots.Overdrive(session);
                overdriveActive = status.State == OverdriveStage.Active;
                shell.SetOverdrive(status);
            }

            if (factoryDirty)
            {
                factoryDirty = false;
                RenderFactory();
            }

            if (shell.IsSheetOpen)
            {
                shell.RefreshActivePanel();
            }
        }

        /// <summary>Resolves a world tap to a slot and opens its sheet. Returns whether the tap hit a slot.</summary>
        public bool HandleWorldTap(Vector2 world)
        {
            if (disposed || snapshot == null || !grid.TryHitSlot(world, out var slot))
            {
                return false;
            }

            return OpenSlot(slot);
        }

        /// <summary>Opens the build sheet, machine panel or the locked-slot notice for a slot.</summary>
        public bool OpenSlot(int slot)
        {
            if (snapshot == null || slot < 0 || slot >= snapshot.Slots.Count)
            {
                return false;
            }

            var visual = grid.Visuals.Count > slot ? grid.Visuals[slot] : SlotGridModel.Describe(slot, snapshot, presentationKeyOf, overdriveActive, boosted, -1);
            return shell.OpenSlot(slot, visual.Condition, visual.UnlockLevel);
        }

        private void RefreshStructureDependents()
        {
            var effects = RunSnapshots.Items(session).Active;
            boosted = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < effects.Count; i++)
            {
                if (!string.IsNullOrEmpty(effects[i].TargetKey))
                {
                    boosted.Add(effects[i].TargetKey);
                }
            }

            researchStarted = false;
            var nodes = RunSnapshots.Research(session).Nodes;
            for (var i = 0; i < nodes.Count && !researchStarted; i++)
            {
                researchStarted = nodes[i].Level > 0;
            }
        }

        private bool OverdriveTimed()
        {
            var overdrive = session.State.Overdrive;
            var now = session.TimelineUtcMs;
            return overdrive.IsActive(now) || overdrive.IsCoolingDown(now);
        }

        private bool MachineStatesChanged()
        {
            var count = session.CopyMachineStates(views);
            if (count > views.Length)
            {
                views = new MachineStateView[count];
                previousViews = new MachineStateView[count];
                previousViewCount = -1;
                session.CopyMachineStates(views);
            }

            var changed = count != previousViewCount;
            for (var i = 0; !changed && i < count; i++)
            {
                var a = views[i];
                var b = previousViews[i];
                changed = a.SlotIndex != b.SlotIndex || a.Level != b.Level || a.IsActive != b.IsActive ||
                    a.Phase != b.Phase || a.Blocker != b.Blocker || a.MachineTypeIndex != b.MachineTypeIndex;
            }

            if (changed)
            {
                Array.Copy(views, previousViews, Math.Min(count, views.Length));
                previousViewCount = count;
            }

            return changed;
        }

        private void RenderFactory()
        {
            snapshot = RunSnapshots.Factory(session);
            var visuals = SlotGridModel.DescribeAll(snapshot, presentationKeyOf, overdriveActive, boosted, shell.SelectedSlot);
            grid.Render(visuals);
            var anyOccupied = false;
            for (var i = 0; i < visuals.Length; i++)
            {
                anyOccupied |= visuals[i].Condition == SlotCondition.Occupied;
            }

            shell.SetEmptyHint(!anyOccupied);
            if (shell.LockNoticeSlot >= 0 && shell.LockNoticeSlot < visuals.Length)
            {
                shell.ReconcileLockNotice(shell.LockNoticeSlot, visuals[shell.LockNoticeSlot].Condition);
            }

            if (shell.SelectedSlot >= 0 && shell.SelectedSlot < visuals.Length)
            {
                shell.UpdateSlotRoute(shell.SelectedSlot, visuals[shell.SelectedSlot].Condition);
            }
        }

        private void HandleSelectionChanged(int slot)
        {
            if (snapshot == null)
            {
                return;
            }

            grid.Render(SlotGridModel.DescribeAll(snapshot, presentationKeyOf, overdriveActive, boosted, slot));
        }

        private void HandleOverdriveRequested()
        {
            var result = commands.ActivateOverdrive();
            if (result.IsSuccess)
            {
                shell.ClearNotice();
            }
            else
            {
                shell.ShowNotice(RunRejectionCopy.For(result.ReasonCode), ShellNoticeKind.Error);
            }

            Invalidate();
        }
    }
}
