using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEngine.TestTools;

#if UNITY_EDITOR
namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T535 (FR-021, contracts/ui-surfaces-f006.md section 4, research R11): the back order of the run shell with the hub. Escape and Android Back are
    /// one path (<c>HandleBack</c>), which pops one frame per frame-tick: dialog, pinned value, card or build sheet or details, hub or system view or
    /// Settings, then the factory. The mixed sequences of the contract, plus rotation.
    /// </summary>
    public sealed class BackOrderTests
    {
        private readonly List<HubHarness> harnesses = new List<HubHarness>();
        private HubHarness h;

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

        private IEnumerator Make(ShellScreen screen, int percent = 100)
        {
            HubHarness made = null;
            yield return HubHarness.Create(screen, percent, built => made = built);
            harnesses.Add(made);
            h = made;
        }

        private static ShellScreen Phone() =>
            ShellScreens.All().First(screen => screen.Name.StartsWith("phone-portrait 390", StringComparison.Ordinal));

        private bool OpenDialog(List<ConfirmationCloseReason> closed)
        {
            return h.Shell.RequestConfirmation(new ConfirmationRequest
            {
                Title = "Confirm",
                Message = "Sure?",
                OnCancel = closed.Add,
            });
        }

        // One back press per frame: the frame guard answers a second press of the same frame without acting.
        private IEnumerator Back(Action<bool> result)
        {
            yield return null;
            result(h.Shell.HandleBack());
        }

        [UnityTest]
        public IEnumerator CardThenHubThenDialogPopsTheDialogThenTheHubThenNothing()
        {
            yield return Make(Phone());
            h.Card.Build(0, "machine.miner");
            yield return h.Card.OpenCard(0);
            h.Shell.OpenHub();
            Assert.That(h.Shell.IsCardOpen, Is.False, "the hub replaced the card");
            var closed = new List<ConfirmationCloseReason>();
            Assert.That(OpenDialog(closed), Is.True);

            var consumed = false;
            yield return Back(r => consumed = r);
            Assert.That(consumed, Is.True);
            Assert.That(h.Shell.IsConfirmationOpen, Is.False, "the dialog pops first");
            Assert.That(closed, Is.EqualTo(new[] { ConfirmationCloseReason.Back }));
            Assert.That(h.Shell.IsHubOpen, Is.True, "one frame per press");

            yield return Back(r => consumed = r);
            Assert.That(consumed, Is.True);
            Assert.That(h.Shell.IsSheetOpen, Is.False, "then the hub");

            yield return Back(r => consumed = r);
            Assert.That(consumed, Is.False, "the factory is the end of the stack");
        }

        [UnityTest]
        public IEnumerator HubThenSystemViewThenBackReturnsToTheFactoryInOnePress()
        {
            yield return Make(Phone());
            h.Shell.OpenHub();
            yield return ShellRig.Settle();
            CardHarness.Press(h.Hub.CardButton(HubCard.Market));
            Assert.That(h.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Market));

            var consumed = false;
            yield return Back(r => consumed = r);
            Assert.That(consumed, Is.True);
            Assert.That(h.Shell.IsSheetOpen, Is.False);
            Assert.That(h.Shell.ActivePanelId, Is.Null);
            yield return Back(r => consumed = r);
            Assert.That(consumed, Is.False);
        }

        [UnityTest]
        public IEnumerator IndustryAndSettingsFromTheHubEachPopInOnePress()
        {
            yield return Make(Phone());
            foreach (var card in new[] { HubCard.Industry, HubCard.Settings })
            {
                h.Shell.OpenHub();
                yield return ShellRig.Settle();
                CardHarness.Press(h.Hub.CardButton(card));
                Assert.That(h.Shell.IsSheetOpen, Is.True, card.ToString());
                var consumed = false;
                yield return Back(r => consumed = r);
                Assert.That(consumed, Is.True, card.ToString());
                Assert.That(h.Shell.IsSheetOpen, Is.False, card + ": one press to the factory");
            }
        }

        [UnityTest]
        public IEnumerator BuildSheetThenBackClosesItAndAnOpenCardClosesBeforeNothingElse()
        {
            yield return Make(Phone());
            Assert.That(h.Shell.OpenSlot(1, SlotCondition.Empty), Is.True);
            var consumed = false;
            yield return Back(r => consumed = r);
            Assert.That(consumed, Is.True);
            Assert.That(h.Shell.IsSheetOpen, Is.False);
            Assert.That(h.Shell.SelectedSlot, Is.EqualTo(-1));

            h.Card.Build(0, "machine.miner");
            yield return h.Card.OpenCard(0);
            yield return Back(r => consumed = r);
            Assert.That(consumed, Is.True);
            Assert.That(h.Shell.IsCardOpen, Is.False);
            yield return Back(r => consumed = r);
            Assert.That(consumed, Is.False);
        }

        [UnityTest]
        public IEnumerator DetailsThenBackReturnsToTheCardThenTheFactory()
        {
            yield return Make(Phone());
            h.Card.Build(0, "machine.miner");
            yield return h.Card.OpenCard(0);
            Assert.That(h.Shell.OpenDetails(0), Is.True);
            var consumed = false;
            yield return Back(r => consumed = r);
            Assert.That(h.Shell.IsCardOpen, Is.True, "Details returns to the card");
            yield return Back(r => consumed = r);
            Assert.That(h.Shell.IsCardOpen, Is.False);
            Assert.That(h.Shell.IsSheetOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator OneFramePopsOneFrameEvenWhenEscapeAndTheInputPollBothFire()
        {
            yield return Make(Phone());
            h.Shell.OpenHub();
            var closed = new List<ConfirmationCloseReason>();
            Assert.That(OpenDialog(closed), Is.True);
            yield return null;

            Assert.That(h.Shell.HandleBack(), Is.True);
            Assert.That(h.Shell.HandleBack(), Is.True, "the second press of the same frame is answered without acting");
            Assert.That(h.Shell.IsConfirmationOpen, Is.False);
            Assert.That(h.Shell.IsHubOpen, Is.True, "the hub did not pop in the same frame");
            yield return null;
            Assert.That(h.Shell.HandleBack(), Is.True);
            Assert.That(h.Shell.IsHubOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator RotationCancelsADialogAndLeavesTheHubOnTheStack([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Make(screen);
            h.Shell.OpenHub();
            var closed = new List<ConfirmationCloseReason>();
            Assert.That(OpenDialog(closed), Is.True);
            yield return ShellRig.Settle();

            Assert.That(h.Shell.CancelConfirmationForRotation(), Is.True);
            yield return h.Rig.Layout(ShellScreens.Rotated(screen));
            Assert.That(closed, Is.EqualTo(new[] { ConfirmationCloseReason.Rotation }), screen.ToString());
            Assert.That(h.Shell.IsConfirmationOpen, Is.False);
            Assert.That(h.Shell.IsHubOpen, Is.True, "the rotation did not pop the hub");
            Assert.That(h.Shell.NoticeText, Is.EqualTo(RunShellDocument.RotationCancelledNotice));

            var consumed = false;
            yield return Back(r => consumed = r);
            Assert.That(consumed, Is.True);
            Assert.That(h.Shell.IsSheetOpen, Is.False, "Back after the rotation pops the hub");
        }

        [UnityTest]
        public IEnumerator RotationKeepsTheCardAndBackStillClosesIt()
        {
            var portrait = Phone();
            yield return Make(portrait);
            h.Card.Build(0, "machine.miner");
            yield return h.Card.OpenCard(0);
            yield return h.Rig.Layout(ShellScreens.Rotated(portrait));
            Assert.That(h.Shell.IsCardOpen, Is.True);
            var consumed = false;
            yield return Back(r => consumed = r);
            Assert.That(consumed, Is.True);
            Assert.That(h.Shell.IsCardOpen, Is.False);
        }
    }
}
#endif
