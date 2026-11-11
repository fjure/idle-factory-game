#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>The outcome of one scripted journey: the interactions it took, the steps, and whether its goal state was reached.</summary>
    public sealed class JourneyResult
    {
        public string Name { get; set; }
        public List<string> Steps { get; } = new List<string>();
        public bool GoalReached { get; set; }
        public string Evidence { get; set; } = string.Empty;

        /// <summary>Each tap, click or key press is one interaction; reading the screen is not.</summary>
        public int Interactions => Steps.Count;

        public string ToJson(string indent)
        {
            var builder = new StringBuilder();
            builder.Append(indent).Append("{\"name\": ").Append(ShellGeometryProbe.Quote(Name))
                .Append(", \"interactions\": ").Append(Interactions)
                .Append(", \"goalReached\": ").Append(GoalReached ? "true" : "false")
                .Append(", \"steps\": [");
            for (var i = 0; i < Steps.Count; i++)
            {
                builder.Append(i == 0 ? "" : ", ").Append(ShellGeometryProbe.Quote(Steps[i]));
            }

            builder.Append("], \"evidence\": ").Append(ShellGeometryProbe.Quote(Evidence)).Append('}');
            return builder.ToString();
        }
    }

    /// <summary>
    /// The deterministic interaction-count journey of the F006 baseline (T502, research R02, SC-003). It drives the real shell and its
    /// real panels with panel events only: a press is a <c>NavigationSubmitEvent</c> on the control, which is the event a click and the
    /// Enter key both reach a Button as (UI Toolkit drops device pointer events in the batch editor, workstation pitfalls). A tap on the
    /// factory floor is <see cref="RunShellDocument.OpenSlot"/>, the call the world tap makes. The scripts take the shortest path the
    /// current UI offers; a journey counts as done only when its goal state is verified.
    /// </summary>
    internal sealed class InteractionCountJourney
    {
        private readonly BaselineRig rig;

        public InteractionCountJourney(BaselineRig rig)
        {
            this.rig = rig;
        }

        private RunShellDocument Shell => rig.ShellDocument;

        private static void Press(VisualElement control)
        {
            Assert.That(control, Is.Not.Null);
            using (var evt = NavigationSubmitEvent.GetPooled())
            {
                evt.target = control;
                control.SendEvent(evt);
            }
        }

        private IEnumerator Tap(JourneyResult result, string description, Action action)
        {
            result.Steps.Add(description);
            action();
            yield return rig.Settle();

            // A confirmation dialog the action raised is one more press, never skipped.
            if (Shell.IsConfirmationOpen)
            {
                result.Steps.Add("confirm in the dialog");
                Press(Shell.DialogConfirmButton);
                yield return rig.Settle();
            }
        }

        private MachineSnapshot MachineAt(int slot) => RunSnapshots.Factory(rig.Session).Machines.FirstOrDefault(machine => machine.SlotIndex == slot);

        private string VisibleMachineText()
        {
            var safe = ShellGeometryProbe.SafeRectPoints(rig.Shell);
            return string.Join(" | ", ShellGeometryProbe.CollectTexts(new VisualElement[] { rig.Machine.Root }, safe).Select(item => item.Text));
        }

        /// <summary>Select a machine, upgrade it, and see the factory again (SC-003 baseline 3).</summary>
        public IEnumerator UpgradeAndReturn(Action<JourneyResult> done)
        {
            var result = new JourneyResult { Name = "upgrade a machine and see the factory again" };
            yield return rig.ShowNoPanel();
            const int slot = 0;
            var before = MachineAt(slot).Level;
            yield return Tap(result, "select the machine in slot 1", () => Assert.That(Shell.OpenSlot(slot, SlotCondition.Occupied), Is.True));
            yield return Tap(result, "press Upgrade", () => Press(rig.Machine.UpgradeButton));
            var after = MachineAt(slot).Level;
            if (Shell.IsSheetOpen)
            {
                yield return Tap(result, "press Close", () => Press(Shell.CloseButton));
            }

            result.GoalReached = after == before + 1 && !Shell.IsSheetOpen && Shell.SelectedSlot < 0;
            result.Evidence = "level " + before + " -> " + after + "; sheet open after: " + Shell.IsSheetOpen;
            done(result);
        }

        /// <summary>Learn the limiting cause of one stopped machine (SC-003 target 1 tap).</summary>
        public IEnumerator DiagnoseOneMachine(Action<JourneyResult> done)
        {
            var result = new JourneyResult { Name = "learn the limiting cause of one stopped machine" };
            yield return rig.ShowNoPanel();
            var stopped = BaselineFixture.StoppedSlots(rig.Session);
            Assert.That(stopped, Is.Not.Empty, "the baseline run has stopped machines");
            var slot = stopped[0];
            yield return Tap(result, "select the stopped machine in slot " + (slot + 1), () => Assert.That(Shell.OpenSlot(slot, SlotCondition.Occupied), Is.True));
            result.GoalReached = Shell.IsSheetOpen && rig.Machine.Slot == slot;
            result.Evidence = VisibleMachineText();
            done(result);
        }

        /// <summary>Learn the cause of every stopped machine: the machine sheet is the only place the cause is written (SC-003 target 0 taps for the category).</summary>
        public IEnumerator DiagnoseEveryStoppedMachine(Action<JourneyResult> done)
        {
            var result = new JourneyResult { Name = "learn the limiting cause of every stopped machine" };
            yield return rig.ShowNoPanel();
            var stopped = BaselineFixture.StoppedSlots(rig.Session);
            var seen = 0;
            foreach (var slot in stopped)
            {
                var target = slot;
                yield return Tap(result, "select the stopped machine in slot " + (target + 1), () => Assert.That(Shell.OpenSlot(target, SlotCondition.Occupied), Is.True));
                if (rig.Machine.Slot == target)
                {
                    seen++;
                }
            }

            yield return Tap(result, "press Close", () => Press(Shell.CloseButton));
            result.GoalReached = seen == stopped.Length && !Shell.IsSheetOpen;
            result.Evidence = stopped.Length + " stopped machines";
            done(result);
        }

        /// <summary>Open one system (a tab) and leave it again (SC-003: at most 2 to reach, 1 to return).</summary>
        public IEnumerator OpenAndLeaveSystem(ShellTab tab, Action<JourneyResult> done)
        {
            var result = new JourneyResult { Name = "open " + tab + " and return to the factory" };
            yield return rig.ShowNoPanel();
            yield return Tap(result, "press the " + tab + " tab", () => Press(Shell.TabButton(tab)));
            var opened = Shell.IsSheetOpen && Shell.ActiveTab == tab && Shell.ActivePanelId == ShellPanelIds.ForTab(tab);
            var openSteps = result.Steps.Count;
            yield return Tap(result, "press the Factory tab", () => Press(Shell.TabButton(ShellTab.Factory)));
            result.GoalReached = opened && !Shell.IsSheetOpen && Shell.ActiveTab == ShellTab.Factory;
            result.Evidence = "to reach " + openSteps + ", to return " + (result.Steps.Count - openSteps);
            done(result);
        }

        /// <summary>Build a machine in an empty slot (SC-003: at most 3).</summary>
        public IEnumerator BuildInEmptySlot(Action<JourneyResult> done)
        {
            var result = new JourneyResult { Name = "build a machine in an empty slot" };
            yield return rig.ShowNoPanel();
            var slot = BaselineFixture.EmptySlot;
            var before = RunSnapshots.Factory(rig.Session).Machines.Count;
            yield return Tap(result, "select the empty slot " + (slot + 1), () => Assert.That(Shell.OpenSlot(slot, SlotCondition.Empty), Is.True));
            var button = rig.Build.BuildButtons.FirstOrDefault(candidate => !RunShellDocument.IsSoftDisabled(candidate));
            Assert.That(button, Is.Not.Null, "an affordable build card exists");
            yield return Tap(result, "press a build card", () => Press(button));
            var built = RunSnapshots.Factory(rig.Session).Machines.Count == before + 1;
            var sheetAfterBuild = Shell.IsSheetOpen;
            if (Shell.IsSheetOpen)
            {
                yield return Tap(result, "press Close", () => Press(Shell.CloseButton));
            }

            result.GoalReached = built && !Shell.IsSheetOpen;
            result.Evidence = "machines " + before + " -> " + (before + (built ? 1 : 0)) + "; sheet still open after the build: " + sheetAfterBuild;
            done(result);
        }

        /// <summary>The four journeys of SC-003 plus one open-and-leave per system, in a fixed order.</summary>
        public IEnumerator RunAll(List<JourneyResult> into)
        {
            JourneyResult current = null;
            yield return DiagnoseOneMachine(r => current = r);
            into.Add(current);
            yield return DiagnoseEveryStoppedMachine(r => current = r);
            into.Add(current);
            foreach (var tab in new[] { ShellTab.Market, ShellTab.Contracts, ShellTab.Items, ShellTab.Research, ShellTab.Prestige })
            {
                yield return OpenAndLeaveSystem(tab, r => current = r);
                into.Add(current);
            }

            yield return UpgradeAndReturn(r => current = r);
            into.Add(current);
            yield return BuildInEmptySlot(r => current = r);
            into.Add(current);
        }
    }
}
#endif
