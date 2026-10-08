#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.MarketPanelTestSupport;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T114 (FR-008, FR-032): the Priorities surface. Empty, ordered, move up and down by button and keyboard, a stale order that
    /// rejects and re-renders, drag-to-reorder on a pointer profile only, a rotation that cancels a drag without reordering, and a
    /// worst-case layout at every screen class (rotations included) at 100 and 200 percent text scale.
    /// </summary>
    public sealed class PrioritiesPanelTests
    {
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;
        private RunSession session;
        private RunCommands commands;
        private PrioritiesPanel panel;

        [TearDown]
        public void TearDown()
        {
            foreach (var item in rigs)
            {
                item.Dispose();
            }

            rigs.Clear();
            rig = null;
            panel = null;
        }

        private IEnumerator Open(RunSession run, RunCommands runCommands, ShellDeviceProfile? profile = null, ShellScreen screen = null, int percent = 100)
        {
            session = run;
            commands = runCommands;
            rig = ShellRig.Create(screen ?? ShellScreens.All().First(), profile, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            panel = new PrioritiesPanel(session, commands, rig.Shell, Tree("PrioritiesPanel"), Styles("PrioritiesPanel"), Icon);
            rig.Shell.Panels.Register(panel);
            rig.Shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
        }

        /// <summary>A run with three machines (miner, smelter, press) and the order they were built in.</summary>
        private IEnumerator ThreeMachines(ShellDeviceProfile? profile = null)
        {
            var run = Fresh(out var runCommands, 100_000, true);
            Build(runCommands, 0, "machine.miner");
            Build(runCommands, 1, "machine.smelter");
            Build(runCommands, 2, "machine.press");
            yield return Open(run, runCommands, profile);
        }

        private static ShellDeviceProfile PointerProfile => ShellDeviceProfile.Pointer(640, 360, 0f);

        private Guid[] Priority() => session.State.Priority.ToArray();

        // ---------------------------------------------------------------------------------------------------
        // Empty and ordered states
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WithoutMachinesTheListSaysToBuildMachines()
        {
            var run = Fresh(out var runCommands);
            yield return Open(run, runCommands);
            Assert.That(panel.IsEmptyShown, Is.True);
            Assert.That(panel.Root.Q<Label>("priorities-empty").text, Is.EqualTo("Build machines to set priorities"));
            Assert.That(panel.DisplayedOrder, Is.Empty);
            Assert.That(panel.Root.Query<Button>(className: "prio-move").ToList(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator MachinesAppearInTheOrderTheRunReports()
        {
            yield return ThreeMachines();
            Assert.That(panel.IsEmptyShown, Is.False);
            Assert.That(panel.DisplayedOrder, Is.EqualTo(Priority()));
            var snapshot = RunSnapshots.Factory(session);
            for (var i = 0; i < snapshot.Priority.Count; i++)
            {
                var machine = snapshot.Machines.First(item => item.InstanceId == snapshot.Priority[i]);
                var title = panel.Root.Q<Label>("priority-title-" + snapshot.Priority[i].ToString("N"));
                Assert.That(title.text, Is.EqualTo((i + 1) + ". " + machine.Label));
            }

            Assert.That(RunShellDocument.IsSoftDisabled(panel.UpButton(panel.DisplayedOrder[0])), Is.True, "the first machine cannot move up");
            Assert.That(RunShellDocument.IsSoftDisabled(panel.DownButton(panel.DisplayedOrder[2])), Is.True, "the last machine cannot move down");
            Assert.That(RunShellDocument.IsSoftDisabled(panel.DownButton(panel.DisplayedOrder[0])), Is.False);
        }

        // ---------------------------------------------------------------------------------------------------
        // Buttons and keyboard
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator MoveDownReordersThroughTheCommandAndKeepsFocusOnTheSameButton()
        {
            yield return ThreeMachines();
            var before = Priority();
            var down = panel.DownButton(before[0]);
            down.Focus();
            Activate(down);
            yield return ShellRig.Settle();

            Assert.That(Priority(), Is.EqualTo(new[] { before[1], before[0], before[2] }));
            Assert.That(panel.DisplayedOrder, Is.EqualTo(Priority()));
            Assert.That(panel.ReasonText, Is.Empty);
            Assert.That(panel.StatusText, Does.Contain("number 2 of 3"));
            var moved = panel.DownButton(before[0]);
            Assert.That(moved.focusController.focusedElement, Is.SameAs(moved), "focus stays on the control that was used, so repeated Enter keeps moving it");
        }

        [UnityTest]
        public IEnumerator MoveUpOfTheLastMachineWorksByKeyboardSubmit()
        {
            yield return ThreeMachines();
            var before = Priority();
            Activate(panel.UpButton(before[2]));
            Assert.That(Priority(), Is.EqualTo(new[] { before[0], before[2], before[1] }));
            var order = rig.Shell.GetFocusOrder();
            foreach (var id in panel.DisplayedOrder)
            {
                Assert.That(order, Does.Contain(panel.UpButton(id)));
                Assert.That(order, Does.Contain(panel.DownButton(id)));
            }
        }

        [UnityTest]
        public IEnumerator MovingPastTheEndChangesNothingAndSaysSo()
        {
            yield return ThreeMachines();
            var before = Priority();
            var revision = session.StructureRevision;
            Activate(panel.UpButton(before[0]));
            Assert.That(Priority(), Is.EqualTo(before));
            Assert.That(session.StructureRevision, Is.EqualTo(revision), "no command ran");
            Assert.That(panel.StatusText, Is.EqualTo("That machine is already first."));
            Activate(panel.DownButton(before[2]));
            Assert.That(panel.StatusText, Is.EqualTo("That machine is already last."));
        }

        [UnityTest]
        public IEnumerator AStaleOrderRejectsAndReRendersFromTheRun()
        {
            yield return ThreeMachines();
            var before = Priority();

            // A machine is built while the list still shows the old order: the shown permutation is no longer one.
            var added = Build(commands, 3, "machine.assembler");
            Assert.That(panel.DisplayedOrder.Count, Is.EqualTo(3), "the panel has not refreshed yet");
            Activate(panel.DownButton(before[0]));
            yield return ShellRig.Settle();

            Assert.That(panel.ReasonText, Is.EqualTo(RunRejectionCopy.For(RunReasons.PriorityNotPermutation)));
            Assert.That(Priority(), Is.EqualTo(before.Concat(new[] { added }).ToArray()), "the rejected move changed nothing");
            Assert.That(panel.DisplayedOrder, Is.EqualTo(Priority()), "the list re-rendered with the new machine");
            Assert.That(panel.Root.Query<Button>(className: "prio-move").ToList().Count, Is.EqualTo(8));

            // The next move works from the fresh list.
            Activate(panel.DownButton(before[0]));
            Assert.That(Priority()[1], Is.EqualTo(before[0]));
            Assert.That(panel.ReasonText, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ARefreshAfterABuildOrRemoveRendersTheNewList()
        {
            yield return ThreeMachines();
            Build(commands, 3, "machine.assembler");
            panel.Refresh();
            Assert.That(panel.DisplayedOrder.Count, Is.EqualTo(4));
            var removed = Priority()[0];
            Assert.That(commands.Remove(removed, true).IsSuccess, Is.True);
            panel.Refresh();
            Assert.That(panel.DisplayedOrder, Is.EqualTo(Priority()));
            Assert.That(panel.DisplayedOrder.Contains(removed), Is.False);
        }

        // ---------------------------------------------------------------------------------------------------
        // Drag (pointer devices) and rotation
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator OnAPointerDeviceDraggingARowReordersThroughTheCommand()
        {
            yield return ThreeMachines(PointerProfile);
            var before = Priority();
            Assert.That(panel.DragEnabled, Is.True);
            Assert.That(panel.Handle(before[0]).style.display.value, Is.EqualTo(DisplayStyle.Flex), "the handle shows on a pointer device");

            Assert.That(panel.BeginDrag(0), Is.True);
            Assert.That(panel.IsDragging, Is.True);
            var lastRow = panel.RowOf(before[2]).worldBound;
            panel.UpdateDrag(lastRow.center.y);
            Assert.That(panel.DragTargetIndex, Is.EqualTo(2));
            Assert.That(panel.RowOf(before[0]).ClassListContains("prio-row--dragging"), Is.True);
            Assert.That(panel.RowOf(before[2]).ClassListContains("prio-row--drop-target"), Is.True);
            Assert.That(panel.StatusText, Does.Contain("number 3 of 3"), "the drop position is also said in text");
            Assert.That(Priority(), Is.EqualTo(before), "nothing is reordered before the drop");

            Assert.That(panel.EndDrag(true), Is.True);
            Assert.That(Priority(), Is.EqualTo(new[] { before[1], before[2], before[0] }));
            Assert.That(panel.IsDragging, Is.False);
            Assert.That(panel.DisplayedOrder, Is.EqualTo(Priority()));
        }

        [UnityTest]
        public IEnumerator DroppingWhereItStartedOrCancellingChangesNothing()
        {
            yield return ThreeMachines(PointerProfile);
            var before = Priority();
            var revision = session.StructureRevision;
            Assert.That(panel.BeginDrag(1), Is.True);
            Assert.That(panel.EndDrag(true), Is.False, "no position change, no command");
            Assert.That(panel.BeginDrag(1), Is.True);
            panel.UpdateDrag(panel.RowOf(before[0]).worldBound.center.y);
            panel.CancelDrag();
            Assert.That(panel.IsDragging, Is.False);
            Assert.That(Priority(), Is.EqualTo(before));
            Assert.That(session.StructureRevision, Is.EqualTo(revision));
        }

        [UnityTest]
        public IEnumerator OnATouchDeviceThereIsNoDragHandleAndTheButtonsRemain()
        {
            yield return ThreeMachines(ShellDeviceProfile.Touch(1080, 2400, 0f));
            var first = Priority()[0];
            Assert.That(panel.DragEnabled, Is.False);
            Assert.That(panel.Handle(first).style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(panel.BeginDrag(0), Is.False, "a touch device does not drag");
            Assert.That(panel.UpButton(first), Is.Not.Null);
            Assert.That(panel.DownButton(first), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator PointerEventsOnTheHandleStartAndCancelADragButATouchPressDoesNot()
        {
            yield return ThreeMachines(PointerProfile);
            var handle = panel.Handle(Priority()[0]);

            Send(handle, PointerDownEvent.GetPooled(), UnityEngine.UIElements.PointerType.touch);
            Assert.That(panel.IsDragging, Is.False, "a touch press never starts a drag");

            Send(handle, PointerDownEvent.GetPooled(), UnityEngine.UIElements.PointerType.mouse);
            Assert.That(panel.IsDragging, Is.True, "a mouse press on the handle starts a drag");
            Send(handle, PointerCancelEvent.GetPooled());
            Assert.That(panel.IsDragging, Is.False, "a cancelled pointer ends the drag without reordering");
        }

        [UnityTest]
        public IEnumerator ARotationCancelsAnOpenDragWithoutReorderingAnything()
        {
            yield return ThreeMachines(PointerProfile);
            var before = Priority();
            var revision = session.StructureRevision;
            Assert.That(panel.CancelDragForRotation(), Is.False, "nothing to cancel when no drag is open");

            Assert.That(panel.BeginDrag(0), Is.True);
            panel.UpdateDrag(panel.RowOf(before[2]).worldBound.center.y);
            Assert.That(panel.IsDragging, Is.True);

            Assert.That(panel.CancelDragForRotation(), Is.True);
            Assert.That(panel.IsDragging, Is.False);
            Assert.That(panel.DragTargetIndex, Is.EqualTo(-1));
            Assert.That(Priority(), Is.EqualTo(before), "a rotation never reorders");
            Assert.That(session.StructureRevision, Is.EqualTo(revision), "no command ran");
            Assert.That(panel.RowOf(before[0]).ClassListContains("prio-row--dragging"), Is.False);
            Assert.That(panel.EndDrag(true), Is.False, "the cancelled drag cannot be dropped later");
            Assert.That(Priority(), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator ClosingTheSheetCancelsAnOpenDrag()
        {
            yield return ThreeMachines(PointerProfile);
            Assert.That(panel.BeginDrag(0), Is.True);
            rig.Shell.CloseSheet();
            Assert.That(panel.IsDragging, Is.False);
        }

        [UnityTest]
        public IEnumerator TheRejectionShowsTheWarningBesideItsTextAtEveryScreenClassAndClearsWithIt(
            [ValueSource(typeof(MarketPanelTestSupport), nameof(MarketPanelTestSupport.Screens))] ShellScreen screen,
            [ValueSource(typeof(MarketPanelTestSupport), nameof(MarketPanelTestSupport.Percents))] int percent)
        {
            var run = Fresh(out var runCommands, 100_000, true);
            Build(runCommands, 0, "machine.miner");
            Build(runCommands, 1, "machine.smelter");
            Build(runCommands, 2, "machine.press");
            yield return Open(run, runCommands, ShellDeviceProfile.Pointer(screen.Width, screen.Height, 0f), screen, percent);
            var where = $"{screen} @{percent}%";
            var before = Priority();
            Assert.That(PanelIcons.HasIcon(panel.Root.Q<Label>("priorities-reason")), Is.False, where + ": no icon without a rejection text");

            Build(commands, 3, "machine.assembler");
            Activate(panel.DownButton(before[0]));
            yield return ShellRig.Settle();
            ContractsPanelTestSupport.AssertIconBesideText(panel.Root.Q<Label>("priorities-reason"), PanelIcons.Warning, where + " rejection");
            ContractsPanelTestSupport.AssertTextButtonsHaveNoChildren(panel.Root, where);

            panel.OnHidden();
            yield return ShellRig.Settle();
            Assert.That(panel.ReasonText, Is.Empty, where);
            Assert.That(PanelIcons.HasIcon(panel.Root.Q<Label>("priorities-reason")), Is.False, where + ": the icon goes with its text");
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout (UX-3, F-4)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WorstCaseContentStaysContainedAtEveryScreenClassAndTextScale(
            [ValueSource(typeof(MarketPanelTestSupport), nameof(MarketPanelTestSupport.Screens))] ShellScreen screen,
            [ValueSource(typeof(MarketPanelTestSupport), nameof(MarketPanelTestSupport.Percents))] int percent)
        {
            var run = WorstCase(out var runCommands);
            // A pointer profile shows the drag handle too, the widest row.
            yield return Open(run, runCommands, ShellDeviceProfile.Pointer(screen.Width, screen.Height, 0f), screen, percent);
            Assert.That(panel.DisplayedOrder.Count, Is.EqualTo(12));
            Assert.That(panel.DragEnabled, Is.True);
            yield return CheckLayout(rig, panel, "priorities worst case");
        }
    }
}
#endif
