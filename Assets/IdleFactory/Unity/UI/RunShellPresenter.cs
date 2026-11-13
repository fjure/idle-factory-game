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
        private readonly ContractsShellFeed contractsFeed;
        private readonly Func<string, string> presentationKeyOf;
        private readonly List<MachineStateView> machineBuffer = new List<MachineStateView>();
        private MachineStateView[] views = new MachineStateView[16];
        private MachineStateView[] previousViews = new MachineStateView[16];
        private int previousViewCount = -1;
        private long structureRevision = -1;
        private long lastCash = long.MinValue;
        private long lastRp = long.MinValue;
        private long lastIp = long.MinValue;
        private long lastUsed = long.MinValue;
        private long lastCapacity = long.MinValue;
        private long overdriveSecond = long.MinValue;
        private bool researchStarted;
        private bool factoryDirty = true;
        private RunFactorySnapshot snapshot;
        private HashSet<string> boosted = new HashSet<string>(StringComparer.Ordinal);
        private bool overdriveActive;
        private bool disposed;
        private FactoryWorldLayout orderedFor;

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
            contractsFeed = new ContractsShellFeed(session, shell);
            shell.SlotSelectionChanged += HandleSelectionChanged;
            shell.OverdriveRequested += HandleOverdriveRequested;
            shell.LocationActivated += HandleLocationActivated;

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
            shell.LocationActivated -= HandleLocationActivated;
            shell.UnbindOreVein();
        }

        /// <summary>Forces the next <see cref="Refresh"/> to rebuild the snapshot, for example after a layout change.</summary>
        public void Invalidate()
        {
            factoryDirty = true;
            structureRevision = -1;
            contractsFeed.Invalidate();
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

            // The unspent Influence Points mark the Research tab until they are spent in Industry (R-01).
            var ip = session.Permanent == null ? 0L : session.Permanent.Ip;
            if (ip != lastIp)
            {
                lastIp = ip;
                shell.SetIndustryPoints(ip);
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

            // The Contracts tab badge and the auto-delivered notice show on every tab (T330).
            contractsFeed.Update();

            if (shell.IsSheetOpen)
            {
                shell.RefreshActivePanel();
            }

            // T534: the card refreshes its model every frame (P2-F10) and the keyboard stop follows the camera.
            shell.RefreshCard();
            UpdateLocationOrder();
            shell.RefreshLocationStop();
        }

        /// <summary>
        /// The keyboard order of the locations (R10): by <see cref="FactoryWorldLayout.SortKeyOf"/> (the row from the back), then by column from
        /// the left, then by id. Plain data, no UI.
        /// </summary>
        public static IReadOnlyList<int> KeyboardOrder(FactoryWorldLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var ids = new List<int>(layout.LocationCount);
            for (var i = 0; i < layout.LocationCount; i++)
            {
                ids.Add(i);
            }

            ids.Sort((a, b) =>
            {
                var rowA = layout.SortKeyOf(new BuildingLocationId(a));
                var rowB = layout.SortKeyOf(new BuildingLocationId(b));
                if (rowA != rowB)
                {
                    return rowA.CompareTo(rowB);
                }

                var columnA = layout.CellOf(new BuildingLocationId(a)).x;
                var columnB = layout.CellOf(new BuildingLocationId(b)).x;
                return columnA != columnB ? columnA.CompareTo(columnB) : a.CompareTo(b);
            });
            return ids;
        }

        private void UpdateLocationOrder()
        {
            var layout = grid.Layout;
            if (layout == null || ReferenceEquals(layout, orderedFor))
            {
                return;
            }

            orderedFor = layout;
            shell.SetLocationOrder(KeyboardOrder(layout));
        }

        private void HandleLocationActivated(int slot) => OpenSlot(slot, true);

        /// <summary>Resolves a world tap to a slot and opens its sheet. Returns whether the tap hit a slot.</summary>
        public bool HandleWorldTap(Vector2 world)
        {
            if (disposed || snapshot == null)
            {
                return false;
            }

            if (!grid.TryHitSlot(world, out var slot))
            {
                // A tap on empty world closes the card (US3 scenario 5). The marker-versus-machine tap priority belongs to T528 (P2-F13): no
                // attention marker is a hit target yet, so a tap that hits no machine shape is empty world.
                if (shell.IsDetailsFromCard)
                {
                    // T553 (U-A2): with the Details view open the card is hidden, so IsCardOpen is false. The tap still closes both, through the
                    // existing close path (Details returns to the card, the card closes and the camera restores) and focus returns to the location.
                    var detailsSlot = shell.SelectedSlot;
                    shell.CloseSheetOrDetails();
                    shell.CloseMachineCard(false);
                    shell.FocusLocation(detailsSlot);
                }
                else if (shell.IsCardOpen)
                {
                    shell.CloseMachineCard(false);
                }

                return false;
            }

            return OpenSlot(slot);
        }

        /// <summary>Opens the build sheet, the machine card or the locked-slot notice for a slot; <paramref name="focusCard"/> moves focus into the card (keyboard).</summary>
        public bool OpenSlot(int slot, bool focusCard = false)
        {
            if (snapshot == null || slot < 0 || slot >= snapshot.Slots.Count)
            {
                return false;
            }

            var visual = grid.Visuals.Count > slot ? grid.Visuals[slot] : SlotGridModel.Describe(slot, snapshot, presentationKeyOf, overdriveActive, boosted, -1);
            return shell.OpenSlot(slot, visual.Condition, visual.UnlockLevel, focusCard);
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
