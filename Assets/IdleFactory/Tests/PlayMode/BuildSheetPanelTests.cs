using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

#if UNITY_EDITOR
using UnityEditor;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T113 (FR-009, FR-011, US1 scenario 1, UX-3, F-4): the build sheet of an empty slot. Normal, empty, disabled and error
    /// states are reached through the application seams (a real run session and its commands) without the scene; keyboard
    /// activation, 44 pt targets and the worst-case layout at every screen class, rotations and 200 % text scale.
    /// </summary>
    public sealed class BuildSheetPanelTests
    {
        private const string PanelRoot = "Assets/IdleFactory/Unity/UI/Panels/";
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;
        private RunSession session;
        private RunCommands commands;
        private BuildSheetPresenter panel;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        public static IEnumerable<int> Percents() => new[] { 100, 200 };

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

        private IEnumerator Open(ShellScreen screen, int percent, long cash, bool unlockEverything, int factoryLevel = 1)
        {
            session = RunShellFixtures.NewSession(out commands, factoryLevel, cash, unlockEverything);
            yield return OpenWith(screen, percent);
        }

        private IEnumerator OpenWith(ShellScreen screen, int percent)
        {
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelRoot + "BuildSheetPanel.uxml");
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(PanelRoot + "BuildSheetPanel.uss");
            Assert.That(tree, Is.Not.Null);
            Assert.That(styles, Is.Not.Null);
            panel = new BuildSheetPresenter(session, commands, tree, styles, ContractsPanelTestSupport.Icon);
            rig.Shell.Panels.Register(panel);
            yield return rig.Layout();
        }

        private IEnumerator ShowSlot(int slot)
        {
            Assert.That(rig.Shell.OpenSlot(slot, SlotCondition.Empty), Is.True);
            yield return ShellRig.Settle();
        }

        private static ShellScreen PhoneScreen() => ShellScreens.All().First(screen => screen.Name.StartsWith("phone-portrait 390", StringComparison.Ordinal));

        private int CardOf(string label) =>
            Enumerable.Range(0, panel.BuildButtons.Count).First(i => panel.Root.Q<Label>("build-name-" + i).text == label);

        private Button ButtonOf(string label) => panel.BuildButtons[CardOf(label)];

        private string Text(string name, int card) => panel.Root.Q<Label>(name + "-" + card).text;

        private bool Shown(string name, int card) => panel.Root.Q<Label>(name + "-" + card).style.display != DisplayStyle.None;

        private static void Send(VisualElement target, EventBase evt)
        {
            using (evt)
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }


        private static RunSession NewSessionWithoutUnlocks(out RunCommands runCommands)
        {
            // The Miner is the only type that is always unlocked; the variant content locks it behind factory level 5.
            var text = System.Text.Encoding.UTF8.GetString(RunSceneRun.IronBytes());
            var first = text.IndexOf("\"kind\": \"always\"", StringComparison.Ordinal);
            var second = text.IndexOf("\"kind\": \"always\"", first + 1, StringComparison.Ordinal);
            Assert.That(second, Is.GreaterThan(first));
            text = text.Substring(0, second) + "\"kind\": \"factoryLevelAtLeast\", \"level\": 5" + text.Substring(second + "\"kind\": \"always\"".Length);
            var loaded = GameplayContentDispatcher.Load(System.Text.Encoding.UTF8.GetBytes(text));
            Assert.That(loaded.IsSuccess, Is.True);
            var catalog = loaded.RunCatalog;
            var permanent = new PermanentState(catalog);
            var start = NextRunStart.Compute(catalog, permanent);
            var startMs = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            var state = new RunState(catalog, Guid.NewGuid(), 1, null, 25, 0, start.StorageLevel, 1, new string[0]);
            Assert.That(new RunSimulator(catalog).Advance(state, permanent, 0, startMs, null), Is.EqualTo(RunAdvanceResult.Ok));
            var created = new RunSession(catalog, state, permanent, startMs);
            runCommands = new RunCommands(created);
            return created;
        }

        private int MachineCount() => RunSnapshots.Factory(session).Machines.Count;

        // ---------------------------------------------------------------------------------------------------
        // Normal
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryMachineTypeShowsItsCostRateInputsAndOutputs()
        {
            yield return Open(PhoneScreen(), 100, RunShellFixtures.MinerCost, false);
            yield return ShowSlot(0);

            Assert.That(rig.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Build));
            Assert.That(panel.Slot, Is.EqualTo(0));
            Assert.That(panel.BuildButtons.Count, Is.EqualTo(4), "one card per machine type of the catalog");
            var miner = CardOf("Miner");
            Assert.That(Text("build-cost", miner), Is.EqualTo("Cost: " + RunShellFixtures.MinerCost + " Cash"));
            Assert.That(Text("build-makes", miner), Does.StartWith("Makes: Iron Ore ").And.Contain("/min"));
            Assert.That(Text("build-uses", miner), Is.EqualTo("Uses: nothing"));
            Assert.That(ButtonOf("Miner").text, Is.EqualTo("Build Miner"));
            Assert.That(RunShellDocument.IsSoftDisabled(ButtonOf("Miner")), Is.False, "the affordable, unlocked type can be built");
            Assert.That(Shown("build-reason", miner), Is.False);

            var smelter = CardOf("Smelter");
            Assert.That(Text("build-uses", smelter), Does.StartWith("Uses: Iron Ore ").And.Contain("/min"));
            Assert.That(Text("build-makes", smelter), Does.StartWith("Makes: Iron Bar "));
            Assert.That(panel.Root.Q<Label>("build-sheet-empty").style.display, Is.EqualTo(new StyleEnum<DisplayStyle>(DisplayStyle.None)));
        }

        [UnityTest]
        public IEnumerator ABuiltTypeStaysBuildableAndDuplicatesAreAllowed()
        {
            yield return Open(PhoneScreen(), 100, 100, false);
            yield return ShowSlot(0);
            Send(ButtonOf("Miner"), NavigationSubmitEvent.GetPooled());
            Assert.That(MachineCount(), Is.EqualTo(1));
            Assert.That(session.ReadCash(), Is.EqualTo(100 - RunShellFixtures.MinerCost));

            yield return ShowSlot(1);
            Assert.That(RunShellDocument.IsSoftDisabled(ButtonOf("Miner")), Is.False, "a second Miner is allowed");
            Send(ButtonOf("Miner"), NavigationSubmitEvent.GetPooled());
            var machines = RunSnapshots.Factory(session).Machines;
            Assert.That(machines.Count, Is.EqualTo(2));
            Assert.That(machines.Select(machine => machine.MachineId).Distinct().Count(), Is.EqualTo(1));
            Assert.That(machines.Select(machine => machine.SlotIndex), Is.EquivalentTo(new[] { 0, 1 }));
        }

        [UnityTest]
        public IEnumerator TheSheetFollowsExternalChangesToCashAndUnlocks()
        {
            yield return Open(PhoneScreen(), 100, RunShellFixtures.MinerCost, false);
            yield return ShowSlot(1);
            Assert.That(RunShellDocument.IsSoftDisabled(ButtonOf("Miner")), Is.False);

            Assert.That(commands.Build(0, "machine.miner").IsSuccess, Is.True);
            panel.Refresh();
            var miner = CardOf("Miner");
            Assert.That(RunShellDocument.IsSoftDisabled(ButtonOf("Miner")), Is.True, "no Cash left for another Miner");
            Assert.That(Text("build-reason", miner), Is.EqualTo("Not enough Cash. Need " + RunShellFixtures.MinerCost + " more."));
        }

        // ---------------------------------------------------------------------------------------------------
        // Disabled
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator LockedTypesAreDisabledWithTheirUnlockReasonVisible()
        {
            yield return Open(PhoneScreen(), 100, 25, false);
            yield return ShowSlot(0);

            var expected = new Dictionary<string, string>
            {
                { "Press", "Unlocks at factory level 2." },
                { "Assembler", "Unlocks at factory level 3." },
                { "Smelter", "Unlocks once Iron Ore has been produced." },
            };
            foreach (var pair in expected)
            {
                var card = CardOf(pair.Key);
                Assert.That(RunShellDocument.IsSoftDisabled(ButtonOf(pair.Key)), Is.True, pair.Key + " is disabled");
                Assert.That(Shown("build-reason", card), Is.True, pair.Key + " shows its reason");
                Assert.That(Text("build-reason", card), Is.EqualTo(pair.Value));
                Assert.That(ButtonOf(pair.Key).focusable, Is.True, "a disabled-looking control stays focusable (Art Bible 9.4)");
            }
        }

        [UnityTest]
        public IEnumerator LockedTypesShowTheLockAndRejectionsTheWarningBesideTheirTextAtEveryScreenClass(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent, 4, false);
            yield return ShowSlot(0);
            var where = $"{screen} @{percent}%";

            var press = CardOf("Press");
            ContractsPanelTestSupport.AssertIconBesideText(panel.Root.Q<Label>("build-reason-" + press), PanelIcons.Lock, where + " locked Press");
            var miner = CardOf("Miner");
            ContractsPanelTestSupport.AssertIconBesideText(panel.Root.Q<Label>("build-reason-" + miner), PanelIcons.Warning, where + " unaffordable Miner");

            Send(ButtonOf("Press"), NavigationSubmitEvent.GetPooled());
            yield return ShellRig.Settle();
            ContractsPanelTestSupport.AssertIconBesideText(panel.Root.Q<Label>("build-feedback-" + press), PanelIcons.Warning, where + " rejection");
            ContractsPanelTestSupport.AssertTextButtonsHaveNoChildren(panel.Root, where);
            AssertShellLayout(rig, "build sheet with lock and warning icons");
        }

        [UnityTest]
        public IEnumerator AnOccupiedSlotNoteShowsTheWarningBesideItsText()
        {
            yield return Open(PhoneScreen(), 100, 100, false);
            Assert.That(commands.Build(0, "machine.miner").IsSuccess, Is.True);
            yield return ShowSlot(0);
            ContractsPanelTestSupport.AssertIconBesideText(panel.Root.Q<Label>("build-sheet-note"), PanelIcons.Warning, "occupied slot note");
        }

        [UnityTest]
        public IEnumerator AnUnaffordableTypeIsDisabledWithTheMissingCash()
        {
            yield return Open(PhoneScreen(), 100, 4, false);
            yield return ShowSlot(0);
            var miner = CardOf("Miner");
            Assert.That(RunShellDocument.IsSoftDisabled(ButtonOf("Miner")), Is.True);
            Assert.That(Text("build-reason", miner), Is.EqualTo("Not enough Cash. Need " + (RunShellFixtures.MinerCost - 4) + " more."));
        }

        [UnityTest]
        public IEnumerator EverythingUnlockedAndAffordableIsEnabled()
        {
            yield return Open(PhoneScreen(), 100, 100_000, true, 3);
            yield return ShowSlot(0);
            for (var i = 0; i < panel.BuildButtons.Count; i++)
            {
                Assert.That(RunShellDocument.IsSoftDisabled(panel.BuildButtons[i]), Is.False, panel.Root.Q<Label>("build-name-" + i).text);
                Assert.That(Shown("build-reason", i), Is.False);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Error: the rejection copy sits next to the control used and the state is unchanged
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator PressingAnUnaffordableTypeShowsTheCashRejectionNextToItAndChangesNothing()
        {
            yield return Open(PhoneScreen(), 100, 4, false);
            yield return ShowSlot(0);
            var miner = CardOf("Miner");
            Send(ButtonOf("Miner"), NavigationSubmitEvent.GetPooled());

            Assert.That(Shown("build-feedback", miner), Is.True);
            Assert.That(Text("build-feedback", miner), Is.EqualTo(RunRejectionCopy.For(RunReasons.CashInsufficient)));
            Assert.That(MachineCount(), Is.EqualTo(0));
            Assert.That(session.ReadCash(), Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator PressingALockedTypeShowsTheLockedRejection()
        {
            yield return Open(PhoneScreen(), 100, 1_000, false);
            yield return ShowSlot(0);
            var press = CardOf("Press");
            Send(ButtonOf("Press"), NavigationSubmitEvent.GetPooled());
            Assert.That(Text("build-feedback", press), Is.EqualTo(RunRejectionCopy.For(RunReasons.MachineLocked)));
            Assert.That(MachineCount(), Is.EqualTo(0));
            Assert.That(session.ReadCash(), Is.EqualTo(1_000));

            // The next press elsewhere clears the stale rejection.
            Send(ButtonOf("Miner"), NavigationSubmitEvent.GetPooled());
            Assert.That(Shown("build-feedback", press), Is.False);
            Assert.That(MachineCount(), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AnOccupiedSlotShowsItsReasonAndRejectsTheBuild()
        {
            yield return Open(PhoneScreen(), 100, 100, false);
            Assert.That(commands.Build(0, "machine.miner").IsSuccess, Is.True);
            yield return ShowSlot(0);

            var note = panel.Root.Q<Label>("build-sheet-note");
            Assert.That(note.text, Is.EqualTo(RunRejectionCopy.For(RunReasons.SlotOccupied)));
            Assert.That(RunShellDocument.IsSoftDisabled(ButtonOf("Miner")), Is.True);
            var miner = CardOf("Miner");
            Send(ButtonOf("Miner"), NavigationSubmitEvent.GetPooled());
            Assert.That(Text("build-feedback", miner), Is.EqualTo(RunRejectionCopy.For(RunReasons.SlotOccupied)));
            Assert.That(MachineCount(), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AStaleSheetStillRevalidatesInTheCommand()
        {
            yield return Open(PhoneScreen(), 100, 100, false);
            yield return ShowSlot(2);
            // Another route builds the slot after the sheet drew; the sheet's button was enabled when it drew.
            Assert.That(commands.Build(2, "machine.miner").IsSuccess, Is.True);
            var miner = CardOf("Miner");
            Send(ButtonOf("Miner"), NavigationSubmitEvent.GetPooled());
            Assert.That(Text("build-feedback", miner), Is.EqualTo(RunRejectionCopy.For(RunReasons.SlotOccupied)));
            Assert.That(MachineCount(), Is.EqualTo(1), "the rejected command built nothing");
        }

        // ---------------------------------------------------------------------------------------------------
        // Empty
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WithNoUnlockedTypeTheSheetSaysSo()
        {
            // The defensive empty state: a run whose unlock set is empty (unreachable with valid content).
            session = NewSessionWithoutUnlocks(out commands);
            yield return OpenWith(PhoneScreen(), 100);
            yield return ShowSlot(0);
            Assert.That(panel.Root.Q<Label>("build-sheet-empty").style.display, Is.EqualTo(new StyleEnum<DisplayStyle>(DisplayStyle.Flex)));
            Assert.That(panel.Root.Q<Label>("build-sheet-empty").text, Is.EqualTo("No machine types unlocked."));
        }

        // ---------------------------------------------------------------------------------------------------
        // Keyboard
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryBuildButtonIsInTheFocusOrderAndActivatesWithSubmit()
        {
            yield return Open(PhoneScreen(), 100, 100, false);
            yield return ShowSlot(0);
            var order = rig.Shell.GetFocusOrder().ToList();
            foreach (var button in panel.BuildButtons)
            {
                Assert.That(order, Does.Contain(button), button.name + " is reachable by the keyboard, disabled-looking or not");
                button.Focus();
                yield return null;
                Assert.That(button.focusController.focusedElement, Is.SameAs(button));
            }

            var miner = ButtonOf("Miner");
            miner.Focus();
            yield return null;
            Send(miner.focusController.focusedElement as VisualElement, NavigationSubmitEvent.GetPooled());
            Assert.That(MachineCount(), Is.EqualTo(1), "Enter or Space (Submit) builds");
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout: worst-case content at every screen class (UX-3, F-4)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheWorstCaseSheetIsContainedAtEveryScreenClassAndTextScale([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent, 1_000_000_000_000L, true, 3);
            yield return ShowSlot(0);

            // Worst case: every type unlocked, one unaffordable type's reason and a rejection shown.
            AssertShellLayout(rig, "build sheet, every type unlocked");
            Assert.That(commands.Build(1, "machine.miner").IsSuccess, Is.True);
            foreach (var slot in new[] { 2, 3, 4, 5 })
            {
                Assert.That(commands.Build(slot, "machine.miner").IsSuccess, Is.True);
            }

            yield return ShowSlot(1);
            var assembler = CardOf("Assembler");
            Send(ButtonOf("Assembler"), NavigationSubmitEvent.GetPooled());
            yield return ShellRig.Settle();
            Assert.That(Shown("build-feedback", assembler), Is.True, "the occupied-slot rejection is shown");
            AssertShellLayout(rig, "build sheet with a rejection and the occupied-slot note");

            var scroll = rig.Shell.PanelScroll;
            var viewport = scroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport");
            var range = Mathf.Max(0f, scroll.contentContainer.layout.height - viewport.layout.height);
            foreach (var offset in new[] { 0f, 0.5f, 1f })
            {
                scroll.scrollOffset = new Vector2(scroll.scrollOffset.x, range * offset);
                yield return ShellRig.Settle();
                AssertScrollContentClipped(scroll, $"{screen} @{percent}% build sheet scrolled to {offset:P0}");
            }

            var safe = rig.SafeRect();
            foreach (var button in new[] { panel.BuildButtons.Last(), panel.BuildButtons.First(), panel.BuildButtons[panel.BuildButtons.Count / 2] })
            {
                scroll.scrollOffset = new Vector2(0f, button == panel.BuildButtons.Last() ? 0f : float.MaxValue);
                yield return ShellRig.Settle();
                button.Focus();
                yield return ShellRig.Settle();
                Assert.That(button.focusController.focusedElement, Is.SameAs(button), $"{screen} @{percent}%: '{button.name}' did not take focus");
                AssertWithin(viewport.worldBound, button.worldBound, $"{screen} @{percent}%: focused '{button.name}' is not scrolled into view");
                AssertWithin(safe, button.worldBound, $"{screen} @{percent}%: focused '{button.name}' leaves the safe area");
            }

            if (percent >= 200)
            {
                Assert.That(range, Is.GreaterThan(0f), $"{screen}: four cards at 200 % scroll");
            }
        }

        [UnityTest]
        public IEnumerator EveryActionableControlIsAtLeast44PointsAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen, 100, 25, false);
            yield return ShowSlot(0);
            foreach (var button in panel.BuildButtons)
            {
                Assert.That(button.worldBound.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{screen}: {button.name} width");
                Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{screen}: {button.name} height");
            }
        }

        [UnityTest]
        public IEnumerator TheSheetTextNeverUsesAFixedFontSizeAndGrowsWithTheScale()
        {
            yield return Open(PhoneScreen(), 100, 25, false);
            yield return ShowSlot(0);
            var name = panel.Root.Q<Label>("build-name-0");
            var line = panel.Root.Q<Label>("build-cost-0");
            var at100 = line.resolvedStyle.fontSize;
            rig.Shell.ApplyTextScale(2f);
            rig.SetTextScalePercent(200);
            yield return ShellRig.Settle();
            Assert.That(line.resolvedStyle.fontSize, Is.EqualTo(at100 * 2f).Within(0.51f));
            Assert.That(name.resolvedStyle.fontSize, Is.GreaterThan(line.resolvedStyle.fontSize));
        }
    }
}
#endif
