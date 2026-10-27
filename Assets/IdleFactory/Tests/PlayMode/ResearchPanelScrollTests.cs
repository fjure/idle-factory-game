#if UNITY_EDITOR
using System;
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
using static IdleFactory.Tests.PlayMode.ResearchPanelTestSupport;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T248 (OD-8, FR-032, SC-007): on pointer devices the Research graph scrolls with the mouse wheel (vertically), Shift plus wheel and a
    /// drag on empty canvas (sideways), without focusing a node, at the desktop screen classes. The keyboard order and the focus reveal are
    /// covered by ResearchPanelTests.
    /// </summary>
    public sealed class ResearchPanelScrollTests
    {
        private const string RightMost = AssemblerTuning;
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;
        private ResearchPanel panel;

        public static IEnumerable<ShellScreen> Desktops() =>
            ShellScreens.All().Where(screen => screen.Name.StartsWith("desktop", StringComparison.Ordinal));

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

        private IEnumerator Open(ShellScreen screen, int percent = 100, bool withRp = true)
        {
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            var session = NewSession(out var commands);
            if (withRp)
            {
                GrantRp(session, Huge / 1000L);
            }

            panel = new ResearchPanel(session, commands, Styles("ResearchPanel.uss"), Icon, null);
            rig.Shell.Panels.Register(panel);
            rig.Shell.SelectTab(ShellTab.Research);
            yield return ShellRig.Settle();
        }

        private static void Wheel(VisualElement target, Vector2 delta, EventModifiers modifiers = EventModifiers.None)
        {
            var evt = WheelEvent.GetPooled(new Event { type = EventType.ScrollWheel, delta = delta, mousePosition = target.worldBound.center, modifiers = modifiers });
            Send(target, evt);
        }

        private static readonly string[] Order =
        {
            Bargaining, MinerTuning, DeepVeins, MarketAnalysis, SmelterTuning, ToolDiscount, PressTuning, WarehousePlanning, AssemblerTuning,
        };

        private static Rect GraphViewport(ResearchPanel p) => p.GraphScroll.contentViewport.worldBound;

        private Rect SheetViewport() => rig.Shell.PanelScroll.contentViewport.worldBound;

        private void ResetScroll()
        {
            panel.GraphScroll.scrollOffset = Vector2.zero;
            rig.Shell.PanelScroll.scrollOffset = Vector2.zero;
        }

        private static void Pointer<T>(VisualElement target, EventType type, Vector2 position) where T : PointerEventBase<T>, new()
        {
            var systemEvent = new Event { type = type, mousePosition = position, button = 0 };
            Send(target, PointerEventBase<T>.GetPooled(systemEvent));
        }

        [UnityTest]
        public IEnumerator TheWheelAndShiftWheelBringTheRightMostAndBottomMostNodeIntoViewWithoutFocus(
            [ValueSource(nameof(Desktops))] ShellScreen screen, [Values(100, 200)] int percent)
        {
            yield return Open(screen, percent);
            ResetScroll();
            yield return ShellRig.Settle();
            var right = panel.NodeButton(AssemblerTuning);
            var anchor = panel.NodeButton(Bargaining);
            Assert.That(right.worldBound.xMax, Is.GreaterThan(GraphViewport(panel).xMax), "the right-most node starts out of view: the graph overflows sideways");

            for (var i = 0; i < 80 && right.worldBound.xMax > GraphViewport(panel).xMax + 0.5f; i++)
            {
                Wheel(anchor, new Vector2(0f, 3f), EventModifiers.Shift);
                yield return null;
            }

            yield return ShellRig.Settle();
            Assert.That(right.worldBound.xMax, Is.LessThanOrEqualTo(GraphViewport(panel).xMax + 0.5f), $"{screen} @{percent}%: Shift+wheel reaches the right-most node");
            Assert.That(panel.GraphScroll.scrollOffset.x, Is.GreaterThan(0f));

            for (var i = 0; i < 80; i++)
            {
                Wheel(anchor, new Vector2(0f, -3f), EventModifiers.Shift);
                yield return null;
            }

            Assert.That(panel.GraphScroll.scrollOffset.x, Is.EqualTo(0f).Within(0.5f), "Shift+wheel upward returns to the first column");

            var sheet = rig.Shell.PanelScroll;
            var range = Mathf.Max(0f, sheet.contentContainer.layout.height - sheet.contentViewport.layout.height);
            var focusBefore = Focused(rig.Root);
            for (var i = 0; i < 120; i++)
            {
                Wheel(anchor, new Vector2(0f, 3f));
                yield return null;
            }

            yield return ShellRig.Settle();
            Assert.That(sheet.scrollOffset.y, Is.EqualTo(range).Within(0.5f), $"{screen} @{percent}%: the wheel scrolls the sheet to its end (range {range})");
            var bottom = Order.Select(id => panel.NodeButton(id)).Max(node => node.worldBound.yMax);
            Assert.That(bottom, Is.LessThanOrEqualTo(SheetViewport().yMax + 0.5f), "the bottom-most node is in view after the wheel");
            if (percent == 200)
            {
                Assert.That(range, Is.GreaterThan(0f), "at 200 percent the wheel had something to scroll");
            }

            Assert.That(Focused(rig.Root), Is.SameAs(focusBefore), "scrolling did not move focus");
        }

        [UnityTest]
        public IEnumerator ADragOnEmptyCanvasPansTheGraphAndAPressOnANodeDoesNotStartAPan([ValueSource(nameof(Desktops))] ShellScreen screen)
        {
            yield return Open(screen);
            ResetScroll();
            yield return ShellRig.Settle();
            var empty = panel.Canvas;
            var start = new Vector2(panel.Canvas.worldBound.xMin + 2f, panel.Canvas.worldBound.yMin + 2f);

            Pointer<PointerDownEvent>(empty, EventType.MouseDown, start);
            Pointer<PointerMoveEvent>(empty, EventType.MouseMove, start + new Vector2(-120f, 0f));
            Pointer<PointerUpEvent>(empty, EventType.MouseUp, start + new Vector2(-120f, 0f));
            yield return ShellRig.Settle();
            Assert.That(panel.GraphScroll.scrollOffset.x, Is.EqualTo(120f).Within(0.5f), "dragging left on empty canvas pans the graph right");

            var node = panel.NodeButton(Bargaining);
            Pointer<PointerDownEvent>(node, EventType.MouseDown, node.worldBound.center);
            Assert.That(panel.Canvas.HasPointerCapture(0), Is.False, "a press on a node is a press, not the start of a pan");
            Pointer<PointerUpEvent>(node, EventType.MouseUp, node.worldBound.center);
            yield return ShellRig.Settle();
        }

        [UnityTest]
        public IEnumerator WithoutRpTheGraphStillScrolls([ValueSource(nameof(Desktops))] ShellScreen screen)
        {
            yield return Open(screen, 100, withRp: false);
            ResetScroll();
            yield return ShellRig.Settle();
            Wheel(panel.NodeButton(Bargaining), new Vector2(0f, 3f), EventModifiers.Shift);
            yield return ShellRig.Settle();
            Assert.That(panel.GraphScroll.scrollOffset.x, Is.GreaterThan(0f), "an empty graph (nothing affordable) still scrolls sideways");
        }

        [UnityTest]
        public IEnumerator TheFocusRevealAndFocusRingStayAfterPointerScrolling([ValueSource(nameof(Desktops))] ShellScreen screen)
        {
            yield return Open(screen);
            ResetScroll();
            Wheel(panel.NodeButton(Bargaining), new Vector2(0f, 3f), EventModifiers.Shift);
            yield return ShellRig.Settle();
            var last = panel.NodeButton(AssemblerTuning);
            last.Focus();
            yield return ShellRig.Settle();
            Assert.That(Focused(rig.Root), Is.SameAs(last));
            Assert.That(last.worldBound.xMax, Is.LessThanOrEqualTo(GraphViewport(panel).xMax + 0.5f), "a focused node is scrolled into view");
            Assert.That(last.ClassListContains("run-control--focused"), Is.True, "focus stays visible");
        }
    }
}
#endif
