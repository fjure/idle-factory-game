#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>Shared seams of the Research, Overdrive and Industry panel tests (T116): content IDs, session setup through the application, and UI events.</summary>
    public static class ResearchPanelTestSupport
    {
        public const string Bargaining = "research.bargaining";
        public const string MinerTuning = "research.miner-tuning";
        public const string DeepVeins = "research.deep-veins";
        public const string MarketAnalysis = "research.market-analysis";
        public const string SmelterTuning = "research.smelter-tuning";
        public const string ToolDiscount = "research.tool-discount";
        public const string PressTuning = "research.press-tuning";
        public const string WarehousePlanning = "research.warehouse-planning";
        public const string AssemblerTuning = "research.assembler-tuning";
        public const string Calibration = "industry.calibration";
        public const string MarketContacts = "industry.market-contacts";
        public const string SeedCapital = "industry.seed-capital";
        public const string Toolmaker = "industry.toolmaker";
        public const string Warehouse = "industry.warehouse";
        public const long Huge = 999_999_999_999_999_999L;

        private const string PanelRoot = "Assets/IdleFactory/Unity/UI/Panels/";

        public static StyleSheet Styles(string fileName) => AssetDatabase.LoadAssetAtPath<StyleSheet>(PanelRoot + fileName);

        public static Sprite Icon(string key) =>
            UiIconSprites.Load(key);

        /// <summary>A run at factory level 2 with Overdrive unlocked, no RP and no IP.</summary>
        public static RunSession NewSession(out RunCommands commands, bool unlockOverdrive = true) =>
            RunShellFixtures.NewSession(out commands, factoryLevel: unlockOverdrive ? 2 : 1, cash: 25, unlockEverything: unlockOverdrive);

        /// <summary>The Overdrive status of a run below the unlock level (the shared fixture cannot build it: its level 2 unlocks Overdrive).</summary>
        public static OverdriveStatus LockedStatus() => RunSnapshots.Overdrive(NewSession(out _, false));

        public static void GrantRp(RunSession session, long amount) =>
            Assert.That(session.State.TryAddRp(amount), Is.EqualTo(IdleFactory.Run.Domain.RunStateResult.Ok));

        public static void GrantIp(RunSession session, long amount) =>
            Assert.That(session.Permanent.TryAddIp(amount), Is.EqualTo(IdleFactory.Run.Domain.RunStateResult.Ok));

        public static void SetResearchLevel(RunSession session, string researchId, long level) =>
            Assert.That(session.State.TrySetResearchLevel(researchId, level), Is.EqualTo(IdleFactory.Run.Domain.RunStateResult.Ok), researchId);

        public static void SetIndustryLevel(RunSession session, string upgradeId, long level) =>
            Assert.That(session.Permanent.TrySetIndustryLevel(upgradeId, level), Is.EqualTo(IdleFactory.Run.Domain.RunStateResult.Ok), upgradeId);

        /// <summary>Advances the run by active milliseconds (a real timeline step).</summary>
        public static void Advance(RunSession session, long milliseconds) =>
            Assert.That(session.Advance(milliseconds * RunSession.TicksPerMs).IsSuccess, Is.True);

        public static void Send(VisualElement target, EventBase evt)
        {
            using (evt)
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        /// <summary>Enter or Space on a focused Button reaches it as the input system's Submit navigation event.</summary>
        public static void Submit(VisualElement target) => Send(target, NavigationSubmitEvent.GetPooled());

        public static void Move(VisualElement target, NavigationMoveEvent.Direction direction) =>
            Send(target, NavigationMoveEvent.GetPooled(direction));

        public static void Arrow(VisualElement target, KeyCode key) => Send(target, KeyDownEvent.GetPooled('\0', key, EventModifiers.None));

        public static VisualElement Focused(VisualElement any) => any.focusController?.focusedElement as VisualElement;

        /// <summary>The panel's displayed buttons, in tree order.</summary>
        public static List<Button> DisplayedButtons(VisualElement panelRoot) =>
            panelRoot.Query<Button>().ToList().Where(button => IsDisplayed(button)).ToList();

        /// <summary>
        /// The worst-case layout checks of a mounted panel at one screen class and text scale: the shell layout, clipped scroll content at the
        /// top, the middle and the bottom, and a focused first, middle and last control that is scrolled into view inside the sheet and the
        /// safe area. At 150 percent and more the content must scroll.
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
                AssertInView(viewport.worldBound, button.worldBound, $"{screen} @{percent}% [{state}]: focused '{button.name}' is not scrolled into view");
                AssertInView(safe, button.worldBound, $"{screen} @{percent}% [{state}]: focused '{button.name}' leaves the safe area", button.ClassListContains("research-node"));
                Assert.That(button.ClassListContains("run-control--focused"), Is.True, $"{screen} @{percent}% [{state}]: '{button.name}' shows its focus ring");
            }

            AssertShellLayout(rig, state + " after focus changes");
        }

        /// <summary>
        /// A control inside a viewport: whole when it fits; when it is larger than the viewport (a node at 200 % text on a short sheet) its
        /// top-left corner starts in view, aligned with the viewport edge.
        /// </summary>
        public static void AssertInView(Rect viewport, Rect control, string message, bool startOnly = false)
        {
            if (startOnly || control.height > viewport.height + Tolerance || control.width > viewport.width + Tolerance)
            {
                Assert.That(control.yMin, Is.GreaterThanOrEqualTo(viewport.yMin - 4f).And.LessThan(viewport.yMax), message + " (oversized, top) " + control + " in " + viewport);
                Assert.That(control.xMin, Is.GreaterThanOrEqualTo(viewport.xMin - 4f).And.LessThan(viewport.xMax), message + " (oversized, left) " + control + " in " + viewport);
                return;
            }

            AssertWithin(viewport, control, message);
        }
    }
}
#endif
