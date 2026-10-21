#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.ContractsPanelTestSupport;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T115 (FR-017, FR-018, FR-032, US3 scenarios 1 and 2, UX-2, UX-3, UX-5): the Items surface in its normal, empty, disabled and error
    /// states, the activation flow (Activate, target by machine type, confirm), the countdown, a conflicting or invalid activation that
    /// keeps the Item, the focus behaviour of the activation confirmation, the rotation cancel, and the worst-case layout at every
    /// screen class and at 100 and 200 percent text scale.
    /// </summary>
    public sealed class ItemsPanelTests
    {
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;
        private RunSession session;
        private RunCommands commands;
        private ItemsPanel panel;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        public static IEnumerable<int> Percents() => new[] { 100, 200 };

        public static IEnumerable<ShellScreen> Representative()
        {
            var all = ShellScreens.All().ToList();
            yield return all.First(screen => screen.Name.StartsWith("phone-portrait 360", System.StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("phone-landscape 640", System.StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("tablet-portrait", System.StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("desktop 1280", System.StringComparison.Ordinal));
        }

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

        private IEnumerator Open(ShellScreen screen, int percent = 100)
        {
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            session = NewSession(out commands);
            panel = new ItemsPanel(session, commands, Styles("ItemsPanel.uss"), Icon);
            rig.Shell.Panels.Register(panel);
            rig.Shell.SelectTab(ShellTab.Items);
            yield return ShellRig.Settle();
        }

        private IEnumerator Sync()
        {
            panel.Invalidate();
            panel.Refresh();
            yield return ShellRig.Settle();
        }

        private static bool Shown(VisualElement element) => IsDisplayed(element);

        private long Owned(string itemId) => session.Permanent.ItemCount(itemId);

        // ---------------------------------------------------------------------------------------------------
        // Normal and empty
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheShellMountsThePanelUnderTheItemsTab([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            Assert.That(rig.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Items));
            Assert.That(panel.Id, Is.EqualTo(ShellPanelIds.Items));
            Assert.That(panel.Root.parent, Is.SameAs(rig.Shell.PanelContent));
        }

        [UnityTest]
        public IEnumerator WithoutItemsTheEmptyStateSendsThePlayerToContracts([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            Assert.That(Shown(panel.EmptyLabel), Is.True);
            Assert.That(panel.EmptyLabel.text, Is.EqualTo("No Items yet — earn them from contracts."));
            Assert.That(Shown(panel.StacksSection), Is.False);
            Assert.That(Shown(panel.EffectsSection), Is.False);
        }

        // Content values come from the shipped catalog (T344), never from a typed revision.
        private string EffectText(string itemId)
        {
            var item = session.Catalog.Items[itemId];
            return ItemsPanel.EffectDescription(item.Effect.Kind, item.Effect.Value);
        }

        private long DurationMs(string itemId) => session.Catalog.Items[itemId].DurationMs;

        private string Label(string itemId) => session.Catalog.Items[itemId].Label;

        [UnityTest]
        public IEnumerator StacksAreShownByTierWithTheirCounts()
        {
            yield return Open(ShellScreens.All().First());
            AddItems(session, OverclockCore, 2);
            AddItems(session, DiamondDrillHead, 3);
            AddItems(session, FluxCatalyst, 1);
            yield return Sync();

            Assert.That(Shown(panel.EmptyLabel), Is.False);
            var shown = panel.StacksSection.Children().Where(child => child.name.StartsWith("items-stack-", System.StringComparison.Ordinal) && Shown(child))
                .Select(child => child.name.Substring("items-stack-".Length)).ToList();
            Assert.That(shown, Is.EqualTo(new[] { DiamondDrillHead, FluxCatalyst, OverclockCore }), "tier 1, 2, 3");
            Assert.That(panel.StackTitle(DiamondDrillHead).text, Is.EqualTo(Label(DiamondDrillHead) + " x3"));
            Assert.That(panel.StackTitle(OverclockCore).text, Is.EqualTo(Label(OverclockCore) + " x2"));
            Assert.That(panel.StackCard(FluxCatalyst).Q<Label>("items-tier-" + FluxCatalyst).text, Is.EqualTo("Tier " + session.Catalog.Items[FluxCatalyst].Tier));
            Assert.That(panel.StackEffect(DiamondDrillHead).text, Is.EqualTo(EffectText(DiamondDrillHead) + " for " + RunShellFixtures.Clock(DurationMs(DiamondDrillHead))));
            Assert.That(panel.StackEffect(FluxCatalyst).text, Is.EqualTo(EffectText(FluxCatalyst) + " for " + RunShellFixtures.Clock(DurationMs(FluxCatalyst))));
            Assert.That(panel.StackEffect(OverclockCore).text, Is.EqualTo(EffectText(OverclockCore) + " for " + RunShellFixtures.Clock(DurationMs(OverclockCore))));
        }

        [UnityTest]
        public IEnumerator ActivateOpensATargetChoiceByMachineTypeAndTheFactoryItemOffersTheWholeFactory()
        {
            yield return Open(ShellScreens.All().First());
            AddItems(session, FluxCatalyst, 1);
            AddItems(session, OverclockCore, 1);
            yield return Sync();

            Submit(panel.ActivateButton(FluxCatalyst));
            yield return ShellRig.Settle();
            Assert.That(panel.PendingItemId, Is.EqualTo(FluxCatalyst));
            Assert.That(Shown(panel.ChoiceContainer(FluxCatalyst)), Is.True);
            Assert.That(panel.TargetButtons(FluxCatalyst).Select(button => button.text), Is.EqualTo(new[] { "Use on Press", "Use on Smelter" }));
            Assert.That(Focused(rig.Root), Is.SameAs(panel.TargetButtons(FluxCatalyst)[0]), "focus moves into the choice");
            Assert.That(Owned(FluxCatalyst), Is.EqualTo(1), "choosing a target consumes nothing");

            Submit(panel.ActivateButton(OverclockCore));
            yield return ShellRig.Settle();
            Assert.That(panel.PendingItemId, Is.EqualTo(OverclockCore), "one choice is open at a time");
            Assert.That(Shown(panel.ChoiceContainer(FluxCatalyst)), Is.False);
            Assert.That(panel.TargetButtons(OverclockCore).Select(button => button.text), Is.EqualTo(new[] { "Use on the whole factory" }));

            Submit(panel.CancelChoiceButton(OverclockCore));
            yield return ShellRig.Settle();
            Assert.That(panel.PendingItemId, Is.Null);
            Assert.That(Focused(rig.Root), Is.SameAs(panel.ActivateButton(OverclockCore)));
        }

        [UnityTest]
        public IEnumerator ChoosingATargetAsksForAConfirmationAndConfirmingActivatesTheItemOnce([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            AddItems(session, FluxCatalyst, 1);
            yield return Sync();
            Submit(panel.ActivateButton(FluxCatalyst));
            yield return ShellRig.Settle();
            var press = panel.TargetButtons(FluxCatalyst)[0];
            press.Focus();
            Submit(press);
            yield return ShellRig.Settle();

            var shell = rig.Shell;
            Assert.That(shell.IsConfirmationOpen, Is.True);
            Assert.That(panel.IsConfirmationOpen, Is.True);
            Assert.That(DialogText(shell), Does.Contain(Label(FluxCatalyst)).And.Contain("Press").And.Contain(EffectText(FluxCatalyst)).And.Contain(RunShellFixtures.Clock(DurationMs(FluxCatalyst))));
            Assert.That(Owned(FluxCatalyst), Is.EqualTo(1), "nothing is consumed before the confirmation");
            Assert.That(Focused(rig.Root), Is.SameAs(shell.DialogCancelButton), "the dialog opens with focus on Cancel (UX-2)");

            Submit(shell.DialogConfirmButton);
            yield return ShellRig.Settle();
            Assert.That(Owned(FluxCatalyst), Is.Zero, "exactly one Item is used");
            Assert.That(session.State.Effects.Effects.Count, Is.EqualTo(1));
            Assert.That(session.State.Effects.Effects[0].ConflictKey.TargetKey, Is.EqualTo("machine.press"));
            Assert.That(panel.PendingItemId, Is.Null);
            Assert.That(Shown(panel.EffectsSection), Is.True);
            Assert.That(panel.EffectLabels.First(label => Shown(label)).text, Is.EqualTo(Label(FluxCatalyst) + " on Press: " + EffectText(FluxCatalyst) + ", " + RunShellFixtures.Clock(DurationMs(FluxCatalyst)) + " left"));
            Assert.That(shell.NoticeText, Does.Contain("Flux Catalyst is active on Press"));
            Assert.That(Shown(panel.EmptyLabel), Is.True, "no Item is left");
            var focused = Focused(rig.Root);
            Assert.That(focused, Is.Not.Null, "focus is not lost when the stack disappears");
            Assert.That(Shown(focused), Is.True);

            Submit(shell.DialogConfirmButton);
            yield return null;
            Assert.That(session.State.Effects.Effects.Count, Is.EqualTo(1), "a closed dialog cannot confirm again");
        }

        [UnityTest]
        public IEnumerator CancellingTheConfirmationKeepsTheItemAndReturnsFocusToTheTargetControl()
        {
            yield return Open(ShellScreens.All().First());
            AddItems(session, FluxCatalyst, 1);
            yield return Sync();
            Submit(panel.ActivateButton(FluxCatalyst));
            yield return ShellRig.Settle();
            var smelter = panel.TargetButtons(FluxCatalyst)[1];
            smelter.Focus();
            Submit(smelter);
            yield return ShellRig.Settle();

            Submit(rig.Shell.DialogCancelButton);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(panel.IsConfirmationOpen, Is.False);
            Assert.That(Owned(FluxCatalyst), Is.EqualTo(1), "Cancel keeps the Item");
            Assert.That(session.State.Effects.Effects.Count, Is.Zero);
            Assert.That(Focused(rig.Root), Is.SameAs(smelter), "focus returns to the invoking control (UX-2)");
            Assert.That(panel.PendingItemId, Is.EqualTo(FluxCatalyst), "the choice stays open");

            // Escape and Android Back are Cancel as well.
            Submit(smelter);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.HandleBack(), Is.True);
            yield return ShellRig.Settle();
            Assert.That(Owned(FluxCatalyst), Is.EqualTo(1));
            Assert.That(Focused(rig.Root), Is.SameAs(smelter));
        }

        [UnityTest]
        public IEnumerator ActiveEffectsCountDownOncePerSecond()
        {
            yield return Open(ShellScreens.All().First());
            AddItems(session, DiamondDrillHead, 1);
            Assert.That(commands.ActivateItem(DiamondDrillHead, "machine.miner").IsSuccess, Is.True);
            yield return Sync();
            var label = panel.EffectLabels.First(item => Shown(item));
            Assert.That(label.text, Is.EqualTo(Label(DiamondDrillHead) + " on Miner: " + EffectText(DiamondDrillHead) + ", " + RunShellFixtures.Clock(DurationMs(DiamondDrillHead)) + " left"));

            Advance(session, 1000);
            panel.Refresh();
            Assert.That(label.text, Is.EqualTo(Label(DiamondDrillHead) + " on Miner: " + EffectText(DiamondDrillHead) + ", " + RunShellFixtures.Clock(DurationMs(DiamondDrillHead) - 1000) + " left"));
            label.text = "marker";
            Advance(session, 100);
            panel.Refresh();
            Assert.That(label.text, Is.EqualTo("marker"), "the countdown updates at 1 Hz, not every frame");
            Advance(session, 900);
            panel.Refresh();
            Assert.That(label.text, Does.EndWith(RunShellFixtures.Clock(DurationMs(DiamondDrillHead) - 2000) + " left"));

            Advance(session, DurationMs(DiamondDrillHead) + 10_000);
            yield return Sync();
            Assert.That(panel.EffectLabels.Count(item => Shown(item)), Is.EqualTo(RunSnapshots.Items(session).Active.Count), "an over effect is no longer listed");
        }

        // ---------------------------------------------------------------------------------------------------
        // Disabled and error: invalid or conflicting activation, the Item is kept
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AConflictingTargetIsShownDisabledWithItsReasonAndATapKeepsTheItem()
        {
            yield return Open(ShellScreens.All().First());
            AddItems(session, FluxCatalyst, 2);
            Assert.That(commands.ActivateItem(FluxCatalyst, "machine.press").IsSuccess, Is.True);
            yield return Sync();
            Assert.That(Owned(FluxCatalyst), Is.EqualTo(1));

            Submit(panel.ActivateButton(FluxCatalyst));
            yield return ShellRig.Settle();
            var buttons = panel.TargetButtons(FluxCatalyst);
            Assert.That(RunShellDocument.IsSoftDisabled(buttons[0]), Is.True, "Press already has this effect");
            Assert.That(Shown(panel.TargetReason(FluxCatalyst, 0)), Is.True);
            Assert.That(panel.TargetReason(FluxCatalyst, 0).text, Is.EqualTo(RunRejectionCopy.For(RunReasons.EffectConflict)));
            Assert.That(RunShellDocument.IsSoftDisabled(buttons[1]), Is.False, "Smelter is free");
            Assert.That(Focused(rig.Root), Is.SameAs(buttons[1]), "focus goes to the first available target");

            Submit(buttons[0]);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False, "an invalid choice does not reach the confirmation");
            Assert.That(Shown(panel.ChoiceFeedback(FluxCatalyst)), Is.True);
            Assert.That(panel.ChoiceFeedback(FluxCatalyst).text, Is.EqualTo(RunRejectionCopy.For(RunReasons.EffectConflict)));
            Assert.That(Owned(FluxCatalyst), Is.EqualTo(1), "the Item is kept");
            Assert.That(session.State.Effects.Effects.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RejectionTextsShowTheWarningBesideThemAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            AddItems(session, FluxCatalyst, 2);
            AddItems(session, OverclockCore, 2);
            Assert.That(commands.ActivateItem(FluxCatalyst, "machine.press").IsSuccess, Is.True);
            Assert.That(commands.ActivateItem(OverclockCore, null).IsSuccess, Is.True);
            yield return Sync();
            var where = $"{screen} @{percent}%";

            AssertIconBesideText(panel.StackReason(OverclockCore), PanelIcons.Warning, where + " stack reason");
            Submit(panel.ActivateButton(FluxCatalyst));
            yield return ShellRig.Settle();
            AssertIconBesideText(panel.TargetReason(FluxCatalyst, 0), PanelIcons.Warning, where + " target reason");
            Submit(panel.TargetButtons(FluxCatalyst)[0]);
            yield return ShellRig.Settle();
            AssertIconBesideText(panel.ChoiceFeedback(FluxCatalyst), PanelIcons.Warning, where + " rejection");
            AssertTextButtonsHaveNoChildren(panel.Root, where);
            AssertShellLayout(rig, "items with warning icons");
        }

        [UnityTest]
        public IEnumerator AStaleUnavailableTargetNeitherActivatesNorConsumesTheItem()
        {
            yield return Open(ShellScreens.All().First());
            AddItems(session, FluxCatalyst, 2);
            Assert.That(commands.ActivateItem(FluxCatalyst, "machine.press").IsSuccess, Is.True);
            yield return Sync();
            Submit(panel.ActivateButton(FluxCatalyst));
            yield return ShellRig.Settle();
            var press = panel.TargetButtons(FluxCatalyst)[0];
            Assert.That(RunShellDocument.IsSoftDisabled(press), Is.True, "the panel shows the Press target as unavailable");

            // The effect ends before the panel redraws: the stale button is still shown as unavailable, the command would now succeed.
            Advance(session, DurationMs(FluxCatalyst) + 10_000);
            Assert.That(session.State.Effects.Effects.Count, Is.Zero);
            Submit(press);
            yield return ShellRig.Settle();

            Assert.That(Owned(FluxCatalyst), Is.EqualTo(1), "the Item is not consumed without the confirmation");
            Assert.That(session.State.Effects.Effects.Count, Is.Zero, "nothing was activated");
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(RunShellDocument.IsSoftDisabled(panel.TargetButtons(FluxCatalyst)[0]), Is.False, "the target re-rendered as available");
            Assert.That(panel.PendingItemId, Is.EqualTo(FluxCatalyst), "the choice stays open for a deliberate choice");

            Submit(panel.TargetButtons(FluxCatalyst)[0]);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.True, "now the confirmation asks first");
            Assert.That(Owned(FluxCatalyst), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AnUnavailableTargetNeverCallsActivateItem()
        {
            yield return Open(ShellScreens.All().First());
            AddItems(session, FluxCatalyst, 2);
            Assert.That(commands.ActivateItem(FluxCatalyst, "machine.press").IsSuccess, Is.True);
            yield return Sync();
            Submit(panel.ActivateButton(FluxCatalyst));
            yield return ShellRig.Settle();
            var effects = session.State.Effects.Effects.Count;
            var structure = session.StructureRevision;

            Submit(panel.TargetButtons(FluxCatalyst)[0]);
            yield return ShellRig.Settle();

            Assert.That(Owned(FluxCatalyst), Is.EqualTo(1));
            Assert.That(session.State.Effects.Effects.Count, Is.EqualTo(effects));
            Assert.That(session.StructureRevision, Is.EqualTo(structure), "no command ran");
            Assert.That(panel.ChoiceFeedback(FluxCatalyst).text, Is.EqualTo(RunRejectionCopy.For(RunReasons.EffectConflict)));
        }

        [UnityTest]
        public IEnumerator AFactoryItemWithItsEffectRunningIsDisabledWithTheReasonWhileTheItemIsKept()
        {
            yield return Open(ShellScreens.All().First());
            AddItems(session, OverclockCore, 2);
            yield return Sync();
            Submit(panel.ActivateButton(OverclockCore));
            yield return ShellRig.Settle();
            Submit(panel.TargetButtons(OverclockCore)[0]);
            yield return ShellRig.Settle();
            Submit(rig.Shell.DialogConfirmButton);
            yield return ShellRig.Settle();

            Assert.That(Owned(OverclockCore), Is.EqualTo(1));
            Assert.That(RunShellDocument.IsSoftDisabled(panel.ActivateButton(OverclockCore)), Is.True);
            Assert.That(Shown(panel.StackReason(OverclockCore)), Is.True);
            Assert.That(panel.StackReason(OverclockCore).text, Is.EqualTo(RunRejectionCopy.For(RunReasons.EffectConflict)));
            Assert.That(session.State.Effects.Effects[0].ConflictKey.TargetKey, Is.EqualTo(RunCommands.FactoryTargetKey));
        }

        [UnityTest]
        public IEnumerator ARejectedActivationAfterTheConfirmationShowsItsReasonAndKeepsTheItem()
        {
            yield return Open(ShellScreens.All().First());
            AddItems(session, DiamondDrillHead, 1);
            yield return Sync();
            Submit(panel.ActivateButton(DiamondDrillHead));
            yield return ShellRig.Settle();
            Submit(panel.TargetButtons(DiamondDrillHead)[0]);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.True);

            session.SetLifecycleBlocked(true);
            Submit(rig.Shell.DialogConfirmButton);
            yield return ShellRig.Settle();
            session.SetLifecycleBlocked(false);

            Assert.That(Owned(DiamondDrillHead), Is.EqualTo(1), "the Item is kept");
            Assert.That(session.State.Effects.Effects.Count, Is.Zero);
            Assert.That(panel.PendingItemId, Is.EqualTo(DiamondDrillHead), "the choice stays open so the player can retry");
            Assert.That(Shown(panel.ChoiceFeedback(DiamondDrillHead)), Is.True);
            Assert.That(panel.ChoiceFeedback(DiamondDrillHead).text, Is.EqualTo(RunRejectionCopy.For(RunReasons.LifecycleBlocked)));
        }

        // ---------------------------------------------------------------------------------------------------
        // Rotation (UX-5, UX-9)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ARotationCancelsTheTargetChoiceWithoutConsumingTheItem([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            AddItems(session, FluxCatalyst, 1);
            yield return Sync();
            Submit(panel.ActivateButton(FluxCatalyst));
            yield return ShellRig.Settle();
            Assert.That(panel.PendingItemId, Is.EqualTo(FluxCatalyst));

            Assert.That(panel.CancelForRotation(), Is.True);
            yield return rig.Layout(ShellScreens.Rotated(screen));
            Assert.That(panel.PendingItemId, Is.Null);
            Assert.That(Shown(panel.ChoiceContainer(FluxCatalyst)), Is.False);
            Assert.That(Owned(FluxCatalyst), Is.EqualTo(1), "a rotation never consumes an Item");
            Assert.That(session.State.Effects.Effects.Count, Is.Zero);
            Assert.That(rig.Shell.NoticeText, Is.EqualTo(RunShellDocument.RotationCancelledNotice));
            Assert.That(Focused(rig.Root), Is.SameAs(panel.ActivateButton(FluxCatalyst)), "focus returns to the control that started the flow");
            Assert.That(panel.CancelForRotation(), Is.False, "nothing is left to cancel");
        }

        [UnityTest]
        public IEnumerator ARotationCancelsAnOpenActivationConfirmationAsWell([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            AddItems(session, FluxCatalyst, 1);
            yield return Sync();
            Submit(panel.ActivateButton(FluxCatalyst));
            yield return ShellRig.Settle();
            var press = panel.TargetButtons(FluxCatalyst)[0];
            press.Focus();
            Submit(press);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.True);

            Assert.That(panel.CancelForRotation(), Is.True);
            yield return rig.Layout(ShellScreens.Rotated(screen));
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(panel.IsConfirmationOpen, Is.False);
            Assert.That(panel.PendingItemId, Is.Null);
            Assert.That(Owned(FluxCatalyst), Is.EqualTo(1));
            Assert.That(session.State.Effects.Effects.Count, Is.Zero);
            Assert.That(rig.Shell.NoticeText, Is.EqualTo(RunShellDocument.RotationCancelledNotice));
            Assert.That(Focused(rig.Root), Is.SameAs(panel.ActivateButton(FluxCatalyst)));
        }

        [UnityTest]
        public IEnumerator LeavingTheTabClosesAPendingChoiceWithoutConsuming()
        {
            yield return Open(ShellScreens.All().First());
            AddItems(session, FluxCatalyst, 1);
            yield return Sync();
            Submit(panel.ActivateButton(FluxCatalyst));
            yield return ShellRig.Settle();
            rig.Shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
            Assert.That(panel.PendingItemId, Is.Null);
            Assert.That(Owned(FluxCatalyst), Is.EqualTo(1));
        }

        // ---------------------------------------------------------------------------------------------------
        // Keyboard
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryPanelControlIsReachableByFocusAndHasATargetOfAtLeast44Points([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            AddItems(session, FluxCatalyst, 1);
            AddItems(session, OverclockCore, 1);
            yield return Sync();
            Submit(panel.ActivateButton(FluxCatalyst));
            yield return ShellRig.Settle();

            var order = rig.Shell.GetFocusOrder().Select(element => element.name).ToList();
            foreach (var expected in new[] { "items-activate-" + FluxCatalyst, "items-activate-" + OverclockCore, "items-target-" + FluxCatalyst + "-0", "items-target-" + FluxCatalyst + "-1", "items-cancel-" + FluxCatalyst })
            {
                Assert.That(order, Does.Contain(expected), $"{screen}: '{expected}' is in the focus order");
            }

            foreach (var button in DisplayedButtons(panel.Root))
            {
                Assert.That(button.focusable, Is.True, button.name);
                Assert.That(button.worldBound.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), button.name);
                Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), button.name);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout (UX-3, F-4): worst-case content at every screen class, rotations included, at 100 and 200 percent
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// The worst case of the panel: every Item owned, three running effects with their countdowns, and an open target choice with
        /// a conflicting target, its reason and a rejection.
        /// </summary>
        private IEnumerator BuildWorstCase()
        {
            foreach (var id in new[] { DiamondDrillHead, FluxCatalyst, OverclockCore })
            {
                AddItems(session, id, 5);
            }

            Assert.That(commands.ActivateItem(DiamondDrillHead, "machine.miner").IsSuccess, Is.True);
            Assert.That(commands.ActivateItem(FluxCatalyst, "machine.press").IsSuccess, Is.True);
            Assert.That(commands.ActivateItem(OverclockCore, null).IsSuccess, Is.True);
            yield return Sync();
            Submit(panel.ActivateButton(FluxCatalyst));
            yield return ShellRig.Settle();
            Submit(panel.TargetButtons(FluxCatalyst)[0]);
            yield return ShellRig.Settle();
            Assert.That(Shown(panel.ChoiceFeedback(FluxCatalyst)), Is.True);
        }

        [UnityTest]
        public IEnumerator TheWorstCasePanelStaysContainedAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            yield return BuildWorstCase();
            yield return AssertPanelLayout(rig, panel.Root, "worst-case items", percent);
        }

        [UnityTest]
        public IEnumerator TheEmptyStateStaysContainedAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            yield return ShellRig.Settle();
            AssertShellLayout(rig, "empty items");
        }

        [UnityTest]
        public IEnumerator TheActivationConfirmationStaysContainedAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            AddItems(session, FluxCatalyst, 1);
            yield return Sync();
            Submit(panel.ActivateButton(FluxCatalyst));
            yield return ShellRig.Settle();
            var smelter = panel.TargetButtons(FluxCatalyst)[1];
            smelter.Focus();
            Submit(smelter);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.True);
            AssertDialogLayout(rig, "activate item", percent);
            Assert.That(Focused(rig.Root), Is.SameAs(rig.Shell.DialogCancelButton));
        }

        [UnityTest]
        public IEnumerator PanelTextFollowsTheScaleVariablesAndNeverFallsBelowTheMinimum([ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(ShellScreens.All().First(), percent);
            yield return BuildWorstCase();
            var expected = percent / 100f;
            foreach (var label in panel.Root.Query<Label>().ToList().Where(item => IsDisplayed(item) && !string.IsNullOrEmpty(item.text)))
            {
                Assert.That(label.resolvedStyle.fontSize, Is.GreaterThanOrEqualTo(12f * expected - 0.51f), label.name);
                Assert.That(label.resolvedStyle.whiteSpace, Is.Not.EqualTo(WhiteSpace.NoWrap), label.name + " wraps");
            }
        }
    }
}
#endif
