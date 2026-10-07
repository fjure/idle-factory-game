using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>
    /// The Priorities surface (FR-008; contracts/ui-surfaces.md): the machines in priority order with a move-up and a
    /// move-down button on every row (keyboard and touch), plus drag-to-reorder on pointer devices. The order is only ever
    /// changed through <see cref="RunCommands.ReorderPriority"/> with a full permutation built from the order that was shown;
    /// when the real order changed in the meantime (a machine was built or removed) the command rejects with
    /// <c>priority.not-permutation</c>, the panel says so and re-renders from the run. A rotation cancels an open drag
    /// without reordering anything (<see cref="CancelDragForRotation"/>, UX-9).
    /// </summary>
    public sealed class PrioritiesPanel : IShellPanel
    {
        public const string EmptyText = "Build machines to set priorities";

        private readonly RunSession session;
        private readonly RunCommands commands;
        private readonly RunShellDocument shell;
        private readonly Func<string, Sprite> icons;
        private readonly VisualElement root;
        private readonly Label emptyLabel;
        private readonly VisualElement list;
        private readonly Label statusLabel;
        private readonly Label reasonLabel;
        private readonly List<Guid> order = new List<Guid>();
        private readonly Dictionary<Guid, MachineLine> lines = new Dictionary<Guid, MachineLine>();
        private readonly Dictionary<Guid, VisualElement> rowElements = new Dictionary<Guid, VisualElement>();
        private long structureRevision = -1;
        private int dragIndex = -1;
        private int dragTarget = -1;
        private int dragPointer = -1;
        private bool releasing;

        private readonly struct MachineLine
        {
            public MachineLine(string label, int slot, int level)
            {
                Label = label;
                Slot = slot;
                Level = level;
            }

            public string Label { get; }
            public int Slot { get; }
            public int Level { get; }
        }

        public PrioritiesPanel(
            RunSession session,
            RunCommands commands,
            RunShellDocument shell,
            VisualTreeAsset tree,
            StyleSheet styles,
            Func<string, Sprite> icons = null,
            string panelId = ShellPanelIds.Market)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
            this.icons = icons;
            Id = panelId;
            root = MarketPanelParts.CreateRoot(tree, styles, "priorities-panel", "prio-panel");
            MarketPanelParts.Ensure<Label>(root, "priorities-heading", "prio-heading").text = "Priorities";
            MarketPanelParts.Ensure<Label>(root, "priorities-hint", "prio-caption").text =
                "Machines at the top get scarce inputs and store space first.";
            emptyLabel = MarketPanelParts.Ensure<Label>(root, "priorities-empty", "prio-text");
            emptyLabel.text = EmptyText;
            list = MarketPanelParts.Ensure<VisualElement>(root, "priorities-list", "prio-list");
            statusLabel = MarketPanelParts.Ensure<Label>(root, "priorities-status", "prio-caption");
            reasonLabel = MarketPanelParts.Ensure<Label>(root, "priorities-reason", "prio-caption");
            reasonLabel.AddToClassList("prio-caption--reason");
        }

        public string Id { get; }
        public string Title => "Priorities";
        public VisualElement Root => root;

        /// <summary>The instance IDs in the order the list shows them, highest priority first.</summary>
        public IReadOnlyList<Guid> DisplayedOrder => order;

        public bool IsEmptyShown => emptyLabel.style.display != DisplayStyle.None;
        public string StatusText => statusLabel.text;
        public string ReasonText => reasonLabel.text;
        public bool IsDragging => dragIndex >= 0;
        public int DragIndex => dragIndex;
        public int DragTargetIndex => dragTarget;

        /// <summary>The drag handle shows on pointer devices only (the device profile of the shell).</summary>
        public bool DragEnabled => shell.DeviceProfile.ShowsHover;

        public VisualElement RowOf(Guid id) => rowElements.TryGetValue(id, out var row) ? row : null;
        public Button UpButton(Guid id) => root.Q<Button>("priority-up-" + id.ToString("N"));
        public Button DownButton(Guid id) => root.Q<Button>("priority-down-" + id.ToString("N"));
        public VisualElement Handle(Guid id) => root.Q<VisualElement>("priority-handle-" + id.ToString("N"));

        public void OnShown(ShellPanelContext context)
        {
            reasonLabel.text = string.Empty;
            statusLabel.text = string.Empty;
            Render(true);
        }

        public void Refresh()
        {
            if (!IsDragging && session.StructureRevision != structureRevision)
            {
                Render(false);
            }
        }

        public void OnHidden()
        {
            CancelDrag();
            reasonLabel.text = string.Empty;
            statusLabel.text = string.Empty;
        }

        /// <summary>Re-reads the priority order and the machines from the run and rebuilds the rows.</summary>
        public void Render(bool force)
        {
            structureRevision = session.StructureRevision;
            var snapshot = RunSnapshots.Factory(session);
            order.Clear();
            lines.Clear();
            var byId = new Dictionary<Guid, MachineSnapshot>();
            for (var i = 0; i < snapshot.Machines.Count; i++)
            {
                byId[snapshot.Machines[i].InstanceId] = snapshot.Machines[i];
            }

            for (var i = 0; i < snapshot.Priority.Count; i++)
            {
                var id = snapshot.Priority[i];
                if (byId.TryGetValue(id, out var machine))
                {
                    order.Add(id);
                    lines[id] = new MachineLine(machine.Label, machine.SlotIndex, machine.Level);
                }
            }

            RebuildRows();
            MarketPanelParts.SetVisible(emptyLabel, order.Count == 0);
        }

        private void RebuildRows()
        {
            list.Clear();
            rowElements.Clear();
            var dragEnabled = DragEnabled;
            for (var i = 0; i < order.Count; i++)
            {
                var id = order[i];
                var key = id.ToString("N");
                var line = lines[id];
                var index = i;

                var row = new VisualElement { name = "priority-row-" + key };
                row.AddToClassList("prio-row");
                row.Add(MarketPanelParts.NewLabel("priority-title-" + key, "prio-row__title", (i + 1) + ". " + line.Label));
                row.Add(MarketPanelParts.NewLabel("priority-detail-" + key, "prio-row__detail", "Slot " + (line.Slot + 1) + " · Level " + line.Level));

                var actions = new VisualElement { name = "priority-actions-" + key };
                actions.AddToClassList("prio-actions");
                var up = RunShellDocument.CreateButton("priority-up-" + key, "Move up");
                up.AddToClassList("prio-move");
                RunShellDocument.SetSoftDisabled(up, i == 0);
                up.clicked += () => Move(index, -1);
                var down = RunShellDocument.CreateButton("priority-down-" + key, "Move down");
                down.AddToClassList("prio-move");
                RunShellDocument.SetSoftDisabled(down, i == order.Count - 1);
                down.clicked += () => Move(index, 1);
                actions.Add(up);
                actions.Add(down);

                var handle = new Label("Drag") { name = "priority-handle-" + key };
                handle.AddToClassList("prio-handle");
                MarketPanelParts.SetIcon(handle, icons, "F003-UI-ICON-DRAG-HANDLE");
                if (handle.style.backgroundImage.value.sprite != null)
                {
                    handle.AddToClassList("prio-handle--icon");
                }

                MarketPanelParts.SetVisible(handle, dragEnabled);
                RegisterDrag(handle, index);
                actions.Add(handle);

                row.Add(actions);
                list.Add(row);
                rowElements[id] = row;
            }
        }

        // ------------------------------------------------------------------------------------------------------
        // Buttons
        // ------------------------------------------------------------------------------------------------------

        /// <summary>Moves the machine at <paramref name="index"/> by one place (-1 up, +1 down) through the reorder command.</summary>
        public void Move(int index, int delta)
        {
            if (index < 0 || index >= order.Count)
            {
                return;
            }

            var target = index + delta;
            if (target < 0 || target >= order.Count)
            {
                reasonLabel.text = string.Empty;
                statusLabel.text = delta < 0 ? "That machine is already first." : "That machine is already last.";
                return;
            }

            var id = order[index];
            var permutation = new List<Guid>(order);
            permutation.RemoveAt(index);
            permutation.Insert(target, id);
            Commit(permutation, id, (delta < 0 ? "priority-up-" : "priority-down-") + id.ToString("N"));
        }

        private void Commit(List<Guid> permutation, Guid moved, string focusName)
        {
            var label = lines.TryGetValue(moved, out var line) ? line.Label : "Machine";
            var result = commands.ReorderPriority(permutation);
            if (result.IsSuccess)
            {
                reasonLabel.text = string.Empty;
                statusLabel.text = label + " is now number " + (permutation.IndexOf(moved) + 1) + " of " + permutation.Count + ".";
            }
            else
            {
                // The order that was shown is stale (or the run refused): say why, then show the real order.
                statusLabel.text = string.Empty;
                reasonLabel.text = RunRejectionCopy.For(result.ReasonCode);
            }

            Render(true);
            if (!string.IsNullOrEmpty(focusName))
            {
                var target = root.Q<VisualElement>(focusName);
                if (target != null)
                {
                    shell.FocusWhenReady(target);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------------
        // Drag to reorder (pointer devices)
        // ------------------------------------------------------------------------------------------------------

        private void RegisterDrag(VisualElement handle, int index)
        {
            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.pointerType == UnityEngine.UIElements.PointerType.touch || !BeginDrag(index))
                {
                    return;
                }

                dragPointer = evt.pointerId;
                if (evt.pointerId >= 0)
                {
                    handle.CapturePointer(evt.pointerId);
                }
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (IsDragging && evt.pointerId == dragPointer)
                {
                    UpdateDrag(evt.position.y);
                }
            });
            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (IsDragging && evt.pointerId == dragPointer)
                {
                    if (evt.pointerId >= 0 && handle.HasPointerCapture(evt.pointerId))
                    {
                        // Releasing the capture raises the capture-out event; it must not read as a cancel of this drop.
                        releasing = true;
                        handle.ReleasePointer(evt.pointerId);
                        releasing = false;
                    }

                    EndDrag(true);
                }
            });
            handle.RegisterCallback<PointerCancelEvent>(evt => CancelDrag());
            handle.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                // Losing the capture without a release by this panel (focus loss, a system gesture) cancels the drag.
                if (IsDragging && !releasing)
                {
                    CancelDrag();
                }
            });
            handle.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (IsDragging && evt.keyCode == KeyCode.Escape)
                {
                    CancelDrag();
                    evt.StopPropagation();
                }
            });
        }

        /// <summary>Starts dragging the row at <paramref name="index"/>. Only pointer devices drag; returns whether a drag started.</summary>
        public bool BeginDrag(int index)
        {
            if (!DragEnabled || index < 0 || index >= order.Count || IsDragging)
            {
                return false;
            }

            dragIndex = index;
            dragTarget = index;
            ApplyDragVisuals();
            return true;
        }

        /// <summary>Moves the drop position to the row under <paramref name="panelY"/> (panel coordinates, as <c>worldBound</c> reports).</summary>
        public void UpdateDrag(float panelY)
        {
            if (!IsDragging)
            {
                return;
            }

            var target = order.Count - 1;
            for (var i = 0; i < order.Count; i++)
            {
                var bound = rowElements[order[i]].worldBound;
                if (panelY < bound.yMax)
                {
                    target = i;
                    break;
                }
            }

            dragTarget = Mathf.Clamp(target, 0, order.Count - 1);
            ApplyDragVisuals();
        }

        /// <summary>Ends the drag. With <paramref name="commit"/> and a changed position it reorders through the command; otherwise nothing changes.</summary>
        public bool EndDrag(bool commit)
        {
            if (!IsDragging)
            {
                return false;
            }

            var from = dragIndex;
            var to = dragTarget;
            ClearDrag();
            if (!commit || from == to || from < 0 || from >= order.Count)
            {
                statusLabel.text = string.Empty;
                return false;
            }

            var id = order[from];
            var permutation = new List<Guid>(order);
            permutation.RemoveAt(from);
            permutation.Insert(Mathf.Clamp(to, 0, permutation.Count), id);
            Commit(permutation, id, "priority-up-" + id.ToString("N"));
            return true;
        }

        /// <summary>Cancels an open drag without reordering anything and without a command.</summary>
        public void CancelDrag()
        {
            if (!IsDragging)
            {
                return;
            }

            ClearDrag();
            statusLabel.text = string.Empty;
        }

        /// <summary>
        /// A rotation cancels an open drag: the order is unchanged and no command runs. Returns whether a drag was open, so the
        /// bootstrap shows the one-line "Cancelled by rotation" notice (UX-9) through its transient-action registry.
        /// </summary>
        public bool CancelDragForRotation()
        {
            if (!IsDragging)
            {
                return false;
            }

            CancelDrag();
            return true;
        }

        private void ClearDrag()
        {
            dragIndex = -1;
            dragTarget = -1;
            dragPointer = -1;
            ApplyDragVisuals();
        }

        private void ApplyDragVisuals()
        {
            for (var i = 0; i < order.Count; i++)
            {
                if (!rowElements.TryGetValue(order[i], out var row))
                {
                    continue;
                }

                row.EnableInClassList("prio-row--dragging", IsDragging && i == dragIndex);
                row.EnableInClassList("prio-row--drop-target", IsDragging && i == dragTarget && dragTarget != dragIndex);
            }

            if (IsDragging && dragIndex < order.Count)
            {
                var label = lines.TryGetValue(order[dragIndex], out var line) ? line.Label : "Machine";
                statusLabel.text = "Moving " + label + " to number " + (dragTarget + 1) + " of " + order.Count + ". Release to drop.";
            }
        }
    }
}
