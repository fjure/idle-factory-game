using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

#if UNITY_EDITOR
namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T534 (FR-018, FR-019, FR-024): the Details view behind the card keeps every capability of the F003 to F005 machine panel, and Remove sits there
    /// behind the shared confirmation. A capability list names each one and proves it is displayed and reachable (focus scrolls it into view) from the
    /// card at every screen class at 200 % text.
    /// </summary>
    public sealed class MachineDetailsParityTests
    {
        private const long Millisecond = RunSession.TicksPerMs;
        private readonly List<CardHarness> harnesses = new List<CardHarness>();
        private CardHarness h;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in harnesses)
            {
                item.Dispose();
            }

            harnesses.Clear();
            h = null;
        }

        private IEnumerator Make(ShellScreen screen, int percent = 100, long cash = 0)
        {
            CardHarness made = null;
            yield return CardHarness.Create(
                screen, percent, cash == 0 ? RunShellFixtures.ComfortableCash * 6 : cash, built => made = built, unlockEverything: true, factoryLevel: 3);
            harnesses.Add(made);
            h = made;
        }

        /// <summary>A Smelter whose Iron Ore is sold as fast as a Miner makes it: a blocker with corrective actions (UX-10).</summary>
        private void BuildAutoSoldSmelterRun()
        {
            h.Build(0, "machine.miner");
            h.Build(1, "machine.smelter");
            Assert.That(h.Commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            Assert.That(h.Session.Advance(25_000L * Millisecond).IsSuccess, Is.True);
        }

        private IEnumerator OpenDetailsFromTheCard(int slot)
        {
            yield return h.OpenCard(slot);
            CardHarness.Press(h.Card.DetailsButton);
            yield return ShellRig.Settle();
            Assert.That(h.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Machine), "Details is the machine panel");
            Assert.That(h.Shell.IsDetailsFromCard, Is.True);
        }

        private static bool Shown(VisualElement panelRoot, string name)
        {
            var element = panelRoot.Q<VisualElement>(name);
            return element != null && IsDisplayed(element);
        }

        // ---------------------------------------------------------------------------------------------------
        // The capability list
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryCapabilityOfTheMachinePanelIsReachableFromTheCard([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Make(screen, 200);
            BuildAutoSoldSmelterRun();
            yield return OpenDetailsFromTheCard(1);
            var root = h.Details.Root;
            var context = $"{screen} @200%";

            // 1 Level and name, 2 the state, 3 the effective rates (out and in), 4 cycle progress, 5 the last batch line, 6 the next upgrade text,
            // 7 the blockers with corrective actions, 8 Upgrade with its reason, 9 switch on/off, 10 Remove. The batch note (11) is a
            // reserved line shown only beyond the event bound; it exists in the tree and has its own tests in MachinePanelTests.
            var capabilities = new[]
            {
                "machine-name", "machine-status", "machine-rate-out", "machine-rate-in", "machine-progress-text", "machine-last-batch",
                "machine-upgrade-text", "machine-blockers", "machine-upgrade", "machine-active", "machine-remove",
            };
            foreach (var name in capabilities)
            {
                Assert.That(Shown(root, name), Is.True, $"{context}: {name} is shown in Details");
            }

            Assert.That(root.Q<VisualElement>("machine-batch"), Is.Not.Null, $"{context}: the batch note line exists");
            Assert.That(root.Q<Label>("machine-name").text, Does.StartWith("Smelter - level 1 of "));
            Assert.That(root.Q<Label>("machine-status").text, Does.StartWith("Status: Paused: Missing Iron Ore"), $"{context}: the paused reason is in the status line");
            Assert.That(h.Details.BlockerButtons.Select(button => button.text), Has.Some.StartsWith("Stop auto-selling"), $"{context}: the corrective action is offered");

            // Reachable: focus scrolls each control into the sheet's visible area, at any text scale.
            var controls = new List<Button> { h.Details.UpgradeButton, h.Details.ActiveButton, h.Details.RemoveButton };
            controls.AddRange(h.Details.BlockerButtons);
            var viewport = h.Shell.PanelScroll.contentViewport;
            foreach (var control in controls)
            {
                control.Focus();
                yield return ShellRig.Settle();
                Assert.That(viewport.worldBound.Contains(control.worldBound.center), Is.True, $"{context}: '{control.text}' is reachable by focus");
            }
        }

        [UnityTest]
        public IEnumerator TheCorrectiveActionStillFixesTheBlockerFromDetails()
        {
            yield return Make(ShellScreens.All().First());
            BuildAutoSoldSmelterRun();
            yield return OpenDetailsFromTheCard(1);

            var fix = h.Details.BlockerButtons.First(button => button.text.StartsWith("Stop auto-selling", StringComparison.Ordinal));
            CardHarness.Press(fix);
            yield return ShellRig.Settle();

            var economy = RunSnapshots.Economy(h.Session).Resources.First(resource => resource.ResourceId == "iron.ore");
            Assert.That(economy.AutoSell, Is.False, "the fix went through RunCommands");
        }

        [UnityTest]
        public IEnumerator SwitchOffOnAndTheNextUpgradeWorkInDetails()
        {
            yield return Make(ShellScreens.All().First());
            h.Build(0, "machine.miner");
            yield return OpenDetailsFromTheCard(0);

            CardHarness.Press(h.Details.ActiveButton);
            Assert.That(h.MachineAt(0).IsActive, Is.False);
            Assert.That(h.Details.ActiveButton.text, Is.EqualTo("Switch on"));
            CardHarness.Press(h.Details.ActiveButton);
            Assert.That(h.MachineAt(0).IsActive, Is.True);

            CardHarness.Press(h.Details.UpgradeButton);
            Assert.That(h.MachineAt(0).Level, Is.EqualTo(2));
            Assert.That(h.Details.Root.Q<Label>("machine-upgrade-text").text, Does.StartWith("Level 3: cost $"));
        }

        [UnityTest]
        public IEnumerator ASoftDisabledUpgradeShowsItsReasonInDetailsAndKeepsFocus()
        {
            yield return Make(ShellScreens.All().First(), 100, RunShellFixtures.MinerCost);
            h.Build(0, "machine.miner");
            yield return OpenDetailsFromTheCard(0);

            Assert.That(RunShellDocument.IsSoftDisabled(h.Details.UpgradeButton), Is.True);
            Assert.That(h.Details.Root.Q<Label>("machine-upgrade-reason").text, Does.Contain("Not enough Cash"), "the reason is visible text, for touch users");
            h.Details.UpgradeButton.Focus();
            CardHarness.Press(h.Details.UpgradeButton);
            Assert.That(h.Details.Root.Q<Label>("machine-upgrade-feedback").text, Is.EqualTo(RunRejectionCopy.For(RunReasons.CashInsufficient)));
            Assert.That(h.MachineAt(0).Level, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------------------------------------------
        // Remove (FR-019)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator RemoveIsNeverOnTheCardAndNeedsDetailsAndTheConfirmation()
        {
            yield return Make(ShellScreens.All().First());
            h.Build(0, "machine.miner");
            yield return h.OpenCard(0);
            Assert.That(h.Card.Root.Query<Button>().ToList().Any(button => button.text.Contains("Remove")), Is.False, "interaction 0: the card offers no Remove");

            CardHarness.Press(h.Card.DetailsButton); // interaction 1
            yield return ShellRig.Settle();
            CardHarness.Press(h.Details.RemoveButton); // interaction 2: opens the shared confirmation, removes nothing
            yield return ShellRig.Settle();
            Assert.That(h.Shell.IsConfirmationOpen, Is.True);
            Assert.That(h.MachineAt(0), Is.Not.Null, "two interactions so far and the machine is still there");
            Assert.That(h.Shell.DialogConfirmButton.text, Is.EqualTo("Remove"));
            Assert.That(h.Shell.DialogCancelButton.text, Is.EqualTo("Keep machine"));

            CardHarness.Press(h.Shell.DialogCancelButton);
            yield return ShellRig.Settle();
            Assert.That(h.MachineAt(0), Is.Not.Null, "Keep machine changes nothing");
            Assert.That(FocusedNow(), Is.SameAs(h.Details.RemoveButton), "focus returns to the invoker (FR-024)");

            CardHarness.Press(h.Details.RemoveButton);
            yield return ShellRig.Settle();
            CardHarness.Press(h.Shell.DialogConfirmButton); // interaction 3: the removal
            yield return ShellRig.Settle();
            Assert.That(h.MachineAt(0), Is.Null, "the confirmation removed the machine");
            Assert.That(h.Shell.NoticeText, Does.StartWith("Removed Miner. Refunded"));
        }

        private VisualElement FocusedNow() => h.Shell.Root.panel.focusController.focusedElement as VisualElement;

        [UnityTest]
        public IEnumerator ARotationCancelsTheOpenConfirmationAndRemovesNothing()
        {
            yield return Make(ShellScreens.All().First());
            h.Build(0, "machine.miner");
            yield return OpenDetailsFromTheCard(0);
            CardHarness.Press(h.Details.RemoveButton);
            yield return ShellRig.Settle();
            Assert.That(h.Shell.IsConfirmationOpen, Is.True);

            Assert.That(h.Shell.CancelConfirmationForRotation(), Is.True);

            Assert.That(h.Shell.IsConfirmationOpen, Is.False);
            Assert.That(h.Shell.NoticeText, Is.EqualTo(RunShellDocument.RotationCancelledNotice));
            Assert.That(h.MachineAt(0), Is.Not.Null, "a rotation never confirms a removal");
        }

        [UnityTest]
        public IEnumerator TheDetailsTitleNamesTheLocation()
        {
            yield return Make(ShellScreens.All().First());
            h.Build(2, "machine.miner");
            yield return OpenDetailsFromTheCard(2);

            Assert.That(h.Shell.Root.Q<Label>("panel-title").text, Is.EqualTo("Details · Location 3"));
            Assert.That(h.Details.Title, Is.EqualTo("Details"));
        }
    }
}
#endif
