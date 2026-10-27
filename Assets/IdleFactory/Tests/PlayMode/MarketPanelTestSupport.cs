#if UNITY_EDITOR
using System;
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
    /// <summary>
    /// Helpers of the T114 Market panel suites (Inventory and rates, Priorities, Expansion): the authored UXML and USS, the
    /// worst-case run, key and pointer input, and the shared layout check (UX-3, F-4) that every panel suite runs at every screen
    /// class, including the rotated forms, at 100 and 200 percent text scale. Editor-only (AssetDatabase), so the whole file sits
    /// inside <c>UNITY_EDITOR</c> for the player build of the Play Mode assembly.
    /// </summary>
    internal static class MarketPanelTestSupport
    {
        public const string PanelRoot = "Assets/IdleFactory/Unity/UI/Panels/";
        private const string SpriteRoot = "Assets/IdleFactory/Art/Development/F003/Sprites/";

        public static readonly string[] MachineTypes = { "machine.miner", "machine.smelter", "machine.press", "machine.assembler" };

        public static VisualTreeAsset Tree(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelRoot + name + ".uxml");
            Assert.That(asset, Is.Not.Null, name + ".uxml");
            return asset;
        }

        public static StyleSheet Styles(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<StyleSheet>(PanelRoot + name + ".uss");
            Assert.That(asset, Is.Not.Null, name + ".uss");
            return asset;
        }

        public static Sprite Icon(string key) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + key.ToLowerInvariant() + "-v01.png");

        /// <summary>Sends a navigation submit to a control, as Enter or Space does on a focused button.</summary>
        public static void Activate(VisualElement target)
        {
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = target;
                target.SendEvent(submit);
            }
        }

        public static void Send(VisualElement target, EventBase evt, string pointerType = null)
        {
            using (evt)
            {
                if (pointerType != null)
                {
                    SetPointerType(evt, pointerType);
                }

                evt.target = target;
                target.SendEvent(evt);
            }
        }

        private static void SetPointerType(EventBase evt, string pointerType)
        {
            for (var type = evt.GetType(); type != null; type = type.BaseType)
            {
                var property = type.GetProperty("pointerType", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly);
                var setter = property?.GetSetMethod(true);
                if (setter != null)
                {
                    setter.Invoke(evt, new object[] { pointerType });
                    return;
                }
            }

            Assert.Fail("The event has no settable pointerType.");
        }

        public static RunSession Fresh(out RunCommands commands, long cash = 25, bool unlockEverything = false, int factoryLevel = 1) =>
            RunShellFixtures.NewSession(out commands, factoryLevel, cash, unlockEverything);

        public static Guid Build(RunCommands commands, int slot, string machineId)
        {
            var result = commands.Build(slot, machineId);
            Assert.That(result.IsSuccess, Is.True, "Build " + machineId + " in slot " + slot + ": " + result.ReasonCode);
            return result.Value.InstanceId;
        }

        public static void Advance(RunSession session, long seconds)
        {
            Assert.That(session.Advance(seconds * 1000L * RunSession.TicksPerMs).IsSuccess, Is.True);
        }

        /// <summary>
        /// The worst-case run of the layout tests: factory level 5 (12 slots), storage level 5, every resource and machine
        /// unlocked, twelve machines of the four types and a full store after a long advance.
        /// </summary>
        public static RunSession WorstCase(out RunCommands commands)
        {
            // The top factory level, every storage upgrade and every slot filled: the largest run the shipped content allows (T344).
            var session = Fresh(out commands, 50_000_000, true, RunShellFixtures.FactoryLevelCount);
            for (var i = 1; i < RunShellFixtures.StorageLevelCount; i++)
            {
                Assert.That(commands.UpgradeStorage().IsSuccess, Is.True);
            }

            for (var slot = 0; slot < RunShellFixtures.FactorySlots(RunShellFixtures.FactoryLevelCount); slot++)
            {
                Build(commands, slot, MachineTypes[slot % MachineTypes.Length]);
            }

            Advance(session, 3 * 3600);
            return session;
        }

        /// <summary>
        /// The layout check every Market panel suite runs: the shell layout holds with the panel open, the sheet scrolls and keeps
        /// its content clipped, and the first, middle and last control of the panel each scroll into view on focus.
        /// </summary>
        public static IEnumerator CheckLayout(ShellRig rig, IShellPanel panel, string state)
        {
            var shell = rig.Shell;
            var screen = rig.Screen;
            var percent = rig.TextScalePercent;
            shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
            AssertShellLayout(rig, state);

            var scroll = shell.PanelScroll;
            var viewport = scroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport");
            var range = Mathf.Max(0f, scroll.contentContainer.layout.height - viewport.layout.height);
            foreach (var offset in new[] { 0f, 0.5f, 1f })
            {
                scroll.scrollOffset = new Vector2(scroll.scrollOffset.x, range * offset);
                yield return ShellRig.Settle();
                AssertScrollContentClipped(scroll, $"{screen} @{percent}% {state} scrolled to {offset:P0}");
            }

            var controls = shell.GetFocusOrder().Where(control => panel.Root.Contains(control)).ToList();
            Assert.That(controls, Is.Not.Empty, $"{screen} @{percent}% {state}: the panel has focusable controls");
            var safe = rig.SafeRect();
            foreach (var control in new[] { controls.Last(), controls.First(), controls[controls.Count / 2] })
            {
                scroll.scrollOffset = new Vector2(0f, control == controls.Last() ? 0f : float.MaxValue);
                yield return ShellRig.Settle();
                control.Focus();
                yield return ShellRig.Settle();
                Assert.That(control.focusController.focusedElement, Is.SameAs(control), $"{screen} @{percent}% {state}: '{control.name}' did not take focus");
                AssertWithin(viewport.worldBound, control.worldBound, $"{screen} @{percent}% {state}: focused '{control.name}' is not scrolled into view");
                AssertWithin(safe, control.worldBound, $"{screen} @{percent}% {state}: focused '{control.name}' leaves the safe area");
            }

            AssertShellLayout(rig, state + " after focus changes");
        }

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        public static IEnumerable<int> Percents() => new[] { 100, 200 };
    }
}
#endif
