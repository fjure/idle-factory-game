#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>Shared seams of the Contracts and Items panel tests (T115): content IDs, session setup through the application, and UI events.</summary>
    public static class ContractsPanelTestSupport
    {
        public const string Plate = "contract.plate";
        public const string Gear = "contract.gear";
        public const string TimedBar = "contract.timed-bar";
        public const string DiamondDrillHead = "item.diamond-drill-head";
        public const string FluxCatalyst = "item.flux-catalyst";
        public const string OverclockCore = "item.overclock-core";
        public const long MsToTicks = RunSession.TicksPerMs;

        private const string PanelRoot = "Assets/IdleFactory/Unity/UI/Panels/";
        private const string SpriteRoot = "Assets/IdleFactory/Art/Development/F003/Sprites/";

        public static StyleSheet Styles(string fileName) => AssetDatabase.LoadAssetAtPath<StyleSheet>(PanelRoot + fileName);

        public static Sprite Icon(string key) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + key.ToLowerInvariant() + "-v01.png");

        /// <summary>A run with every machine and resource unlocked and Cash 25; the contract offers are still locked.</summary>
        public static RunSession NewSession(out RunCommands commands) =>
            RunShellFixtures.NewSession(out commands, factoryLevel: 1, cash: 25, unlockEverything: true);

        public static void UnlockContracts(RunSession session, params string[] offerIds)
        {
            foreach (var id in offerIds.Length == 0 ? new[] { Gear, Plate, TimedBar } : offerIds)
            {
                Assert.That(session.State.TryUnlock(id), Is.EqualTo(RunStateResult.Ok), id);
            }
        }

        public static void AddStock(RunSession session, string resourceId, long units)
        {
            Assert.That(session.State.Inventory.TryImport(new[] { new ResourceAmount(resourceId, units) }), Is.EqualTo(RunStateResult.Ok));
        }

        public static void AddItems(RunSession session, string itemId, long count)
        {
            Assert.That(session.Permanent.TryAddItems(itemId, count), Is.EqualTo(RunStateResult.Ok));
        }

        /// <summary>Advances the run by active milliseconds (a real timeline step).</summary>
        public static void Advance(RunSession session, long milliseconds)
        {
            Assert.That(session.Advance(milliseconds * MsToTicks).IsSuccess, Is.True);
        }

        /// <summary>Enter or Space on a focused Button reaches it as the input system's Submit navigation event.</summary>
        public static void Submit(VisualElement target)
        {
            using (var evt = NavigationSubmitEvent.GetPooled())
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        public static VisualElement Focused(VisualElement any) => any.focusController?.focusedElement as VisualElement;

        /// <summary>All text of the shell's dialog card, for the message and the title.</summary>
        public static string DialogText(RunShellDocument shell) =>
            string.Join(" | ", shell.DialogCard.Query<Label>().ToList().Select(label => label.text));

        /// <summary>The panel's displayed buttons, in tree order.</summary>
        public static List<Button> DisplayedButtons(VisualElement panelRoot) =>
            panelRoot.Query<Button>().ToList().Where(button => IsDisplayed(button)).ToList();

        /// <summary>
        /// The worst-case layout checks of a mounted panel at one screen class and text scale: the shell layout, the clipped scroll
        /// content at the top, the middle and the bottom, and a focused first, middle and last control that is scrolled into view
        /// inside the sheet and the safe area. At 150 percent and more the content must scroll.
        /// </summary>
        public static IEnumerator AssertPanelLayout(ShellRig rig, VisualElement panelRoot, string state, int percent)
        {
            var shell = rig.Shell;
            var screen = rig.Screen;
            yield return ShellRig.Settle();
            AssertShellLayout(rig, state);

            var scroll = shell.PanelScroll;
            var viewport = scroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport");
            var range = Mathf.Max(0f, scroll.contentContainer.layout.height - viewport.layout.height);
            if (percent >= 150)
            {
                Assert.That(range, Is.GreaterThan(0f), $"{screen} @{percent}% [{state}]: the worst-case content scrolls");
            }

            foreach (var offset in new[] { 0f, 0.5f, 1f })
            {
                scroll.scrollOffset = new Vector2(scroll.scrollOffset.x, range * offset);
                yield return ShellRig.Settle();
                AssertScrollContentClipped(scroll, $"{screen} @{percent}% [{state}] scrolled to {offset:P0}");
            }

            var buttons = DisplayedButtons(panelRoot);
            Assert.That(buttons.Count, Is.GreaterThan(0), $"{screen} @{percent}% [{state}]: the panel has controls");
            var safe = rig.SafeRect();
            foreach (var button in new[] { buttons.Last(), buttons.First(), buttons[buttons.Count / 2] })
            {
                scroll.scrollOffset = new Vector2(0f, button == buttons.Last() ? 0f : float.MaxValue);
                yield return ShellRig.Settle();
                button.Focus();
                yield return ShellRig.Settle();
                Assert.That(Focused(button), Is.SameAs(button), $"{screen} @{percent}% [{state}]: '{button.name}' did not take focus");
                AssertWithin(viewport.worldBound, button.worldBound, $"{screen} @{percent}% [{state}]: focused '{button.name}' is not scrolled into view");
                AssertWithin(safe, button.worldBound, $"{screen} @{percent}% [{state}]: focused '{button.name}' leaves the safe area");
                Assert.That(button.ClassListContains("run-control--focused"), Is.True, $"{screen} @{percent}% [{state}]: '{button.name}' shows its focus ring");
            }

            AssertShellLayout(rig, state + " after focus changes");
        }

        /// <summary>The open confirmation dialog stays inside the safe area with its two buttons apart.</summary>
        public static void AssertDialogLayout(ShellRig rig, string state, int percent)
        {
            var shell = rig.Shell;
            var safe = rig.SafeRect();
            var card = shell.DialogCard.worldBound;
            AssertWithin(safe, card, $"{rig.Screen} @{percent}% [{state}]: the dialog card stays inside the safe area");
            AssertWithin(card, shell.DialogCancelButton.worldBound, $"{rig.Screen} @{percent}% [{state}]: Cancel stays in the card");
            AssertWithin(card, shell.DialogConfirmButton.worldBound, $"{rig.Screen} @{percent}% [{state}]: Confirm stays in the card");
            Assert.That(Intersects(shell.DialogCancelButton.worldBound, shell.DialogConfirmButton.worldBound), Is.False);
            AssertTextAndTargets(shell.DialogLayer, safe, $"{rig.Screen} @{percent}% [{state}] dialog", MinimumFontSize * percent / 100f);
        }
    }
}
#endif
