using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Tests.PlayMode.F006;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

#if UNITY_EDITOR
namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T534 (R10, FR-005, FR-024): the keyboard order is the persistent layer, then the world locations in <see cref="FactoryWorldLayout.SortKeyOf"/>
    /// order, then the card. Closing the card returns focus to the location that opened it. The location stop is one focusable element that stands for
    /// the world locations: arrows move its cursor, Enter or Space opens the location as a tap does.
    /// </summary>
    public sealed class KeyboardTraversalTests
    {
        private readonly List<CardHarness> harnesses = new List<CardHarness>();
        private CardHarness h;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        public static IEnumerable<string> LayoutNames() => TestLayouts.Names;

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
                screen, percent, cash == 0 ? RunShellFixtures.ComfortableCash * 4 : cash, built => made = built, unlockEverything: true, factoryLevel: 3);
            harnesses.Add(made);
            h = made;
        }

        private VisualElement Focused => h.Shell.Root.panel.focusController.focusedElement as VisualElement;

        private static void Key(VisualElement target, KeyCode key) =>
            CardHarness.Send(target, KeyDownEvent.GetPooled(key == KeyCode.Space ? ' ' : '\0', key, EventModifiers.None));

        // ---------------------------------------------------------------------------------------------------
        // The order itself
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void TheLocationOrderIsSortKeyThenColumnForEveryLayout()
        {
            var layouts = new List<FactoryWorldLayout> { LatticeLayoutBuilder.ShippedForColumns(3), LatticeLayoutBuilder.ShippedForColumns(4) };
            layouts.AddRange(TestLayouts.Names.Select(name => TestLayouts.ByName(name)));
            foreach (var layout in layouts)
            {
                var order = RunShellPresenter.KeyboardOrder(layout);
                Assert.That(order.Count, Is.EqualTo(layout.LocationCount), layout.LayoutId);
                Assert.That(order.Distinct().Count(), Is.EqualTo(layout.LocationCount), layout.LayoutId + ": every location exactly once");
                for (var i = 1; i < order.Count; i++)
                {
                    var previous = new BuildingLocationId(order[i - 1]);
                    var current = new BuildingLocationId(order[i]);
                    var rowDelta = layout.SortKeyOf(current) - layout.SortKeyOf(previous);
                    Assert.That(rowDelta, Is.GreaterThanOrEqualTo(0), layout.LayoutId + ": rows run from the back to the front");
                    if (rowDelta == 0)
                    {
                        Assert.That(layout.CellOf(current).x, Is.GreaterThan(layout.CellOf(previous).x), layout.LayoutId + ": columns run left to right inside a row");
                    }
                }
            }
        }

        [Test]
        public void TheShippedThreeByFourLatticeStartsAtTheBackLeft()
        {
            var layout = LatticeLayoutBuilder.ShippedForColumns(3);
            var order = RunShellPresenter.KeyboardOrder(layout);
            var first = new BuildingLocationId(order[0]);
            Assert.That(layout.SortKeyOf(first), Is.EqualTo(0), "the first stop is in the back row");
            Assert.That(layout.CellOf(first).x, Is.EqualTo(0), "and at the left");
            var last = new BuildingLocationId(order[order.Count - 1]);
            Assert.That(layout.SortKeyOf(last), Is.EqualTo(layout.RowCount - 1), "the last stop is in the front row");
        }

        // ---------------------------------------------------------------------------------------------------
        // Focus order in the shell
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator FocusOrderIsThePersistentLayerThenTheLocationsThenTheCard([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Make(screen);
            h.Build(0, "machine.miner");
            yield return h.OpenCard(0);

            var order = h.Shell.Root.Query<VisualElement>().Where(element => element.focusable && element.tabIndex >= 0 && RunShellLayoutAssertions.IsDisplayed(element)).ToList();
            int IndexOf(VisualElement element)
            {
                var index = order.IndexOf(element);
                Assert.That(index, Is.GreaterThanOrEqualTo(0), $"{screen}: '{element.name}' is in the focus order");
                return index;
            }

            var stop = IndexOf(h.Shell.LocationStop);
            // On the side-panel classes Overdrive sits in the panel header (after the world); T537 rebuilds the persistent layer.
            foreach (var persistent in new VisualElement[] { h.Shell.CashButton, h.Shell.StoreButton, h.Shell.SettingsButton })
            {
                Assert.That(IndexOf(persistent), Is.LessThan(stop), $"{screen}: '{persistent.name}' of the persistent layer comes before the locations");
            }

            if (RunShellLayoutAssertions.IsDisplayed(h.Shell.ZoomInButton))
            {
                Assert.That(IndexOf(h.Shell.ZoomInButton), Is.LessThan(stop), $"{screen}: the zoom controls come before the locations");
            }

            var close = IndexOf(h.Card.CloseButton);
            var upgrade = IndexOf(h.Card.UpgradeButton);
            var details = IndexOf(h.Card.DetailsButton);
            Assert.That(stop, Is.LessThan(close), $"{screen}: the locations come before the card");
            Assert.That(close, Is.LessThan(upgrade), $"{screen}: inside the card the header comes first");
            Assert.That(upgrade, Is.LessThan(details), $"{screen}: then Upgrade, then Details");
            Assert.That(h.Card.UpgradeButton.focusable, Is.True);
        }

        [UnityTest]
        public IEnumerator TheLocationStopHasAnAccessibleNameAndIsReachableByFocus()
        {
            yield return Make(ShellScreens.All().First());
            var stop = h.Shell.LocationStop;
            Assert.That(stop.focusable, Is.True);
            Assert.That(stop.tooltip, Is.EqualTo(RunShellDocument.LocationStopName));
            stop.Focus();
            yield return ShellRig.Settle();
            Assert.That(Focused, Is.SameAs(stop));
            Assert.That(h.Shell.FocusRingTarget, Is.SameAs(stop), "the shell focus ring surrounds the stop");
            Assert.That(h.Shell.FocusRing.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(stop.worldBound.width, Is.GreaterThanOrEqualTo(48f - 0.5f), "a 48 pt ring even with no camera");
        }

        [UnityTest]
        public IEnumerator ArrowKeysMoveTheCursorInOrderAndEnterOpensTheLocation()
        {
            yield return Make(ShellScreens.All().First());
            var layout = LatticeLayoutBuilder.ShippedForColumns(3);
            var order = RunShellPresenter.KeyboardOrder(layout);
            h.Shell.SetLocationOrder(order);
            var activated = new List<int>();
            h.Shell.LocationActivated += activated.Add;
            var stop = h.Shell.LocationStop;
            stop.Focus();

            Assert.That(h.Shell.LocationCursorSlot, Is.EqualTo(order[0]));
            Key(stop, KeyCode.RightArrow);
            Assert.That(h.Shell.LocationCursorSlot, Is.EqualTo(order[1]));
            Key(stop, KeyCode.DownArrow);
            Assert.That(h.Shell.LocationCursorSlot, Is.EqualTo(order[1]), "one arrow press raised a key event and a navigation event in the same frame: it moved once");
            yield return null;
            Key(stop, KeyCode.DownArrow);
            Assert.That(h.Shell.LocationCursorSlot, Is.EqualTo(order[2]));
            yield return null;
            Key(stop, KeyCode.End);
            Assert.That(h.Shell.LocationCursorSlot, Is.EqualTo(order[order.Count - 1]));
            yield return null;
            Key(stop, KeyCode.RightArrow);
            Assert.That(h.Shell.LocationCursorSlot, Is.EqualTo(order[order.Count - 1]), "the cursor stops at the last location");
            yield return null;
            Key(stop, KeyCode.Home);
            Assert.That(h.Shell.LocationCursorSlot, Is.EqualTo(order[0]));
            yield return null;
            Key(stop, KeyCode.LeftArrow);
            Assert.That(h.Shell.LocationCursorSlot, Is.EqualTo(order[0]), "and at the first");

            Key(stop, KeyCode.Return);
            Assert.That(activated, Is.EqualTo(new[] { order[0] }), "Enter activates the location under the cursor");
            CardHarness.Send(stop, NavigationSubmitEvent.GetPooled());
            Assert.That(activated.Count, Is.EqualTo(1), "a key event and a submit event in one frame activate once");
            yield return null;
            Key(stop, KeyCode.Space);
            Assert.That(activated.Count, Is.EqualTo(2), "Space activates too");
        }

        [UnityTest]
        public IEnumerator EnterOpensTheCardWithFocusInsideAndClosingReturnsFocusToTheLocation([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Make(screen);
            var layout = LatticeLayoutBuilder.ShippedForColumns(3);
            var order = RunShellPresenter.KeyboardOrder(layout);
            h.Build(order[2], "machine.miner");
            h.Shell.SetLocationOrder(order);
            h.Shell.LocationActivated += slot => h.Shell.OpenSlot(slot, SlotCondition.Occupied, 0, true);
            var stop = h.Shell.LocationStop;
            stop.Focus();
            yield return null;
            Key(stop, KeyCode.RightArrow);
            yield return null;
            Key(stop, KeyCode.RightArrow);
            Assert.That(h.Shell.LocationCursorSlot, Is.EqualTo(order[2]));

            yield return null;
            Key(stop, KeyCode.Return);
            yield return ShellRig.Settle();
            Assert.That(h.Shell.IsCardOpen, Is.True, $"{screen}: Enter opened the card of the location");
            Assert.That(h.Shell.CardSlot, Is.EqualTo(order[2]));
            Assert.That(Focused, Is.SameAs(h.Card.UpgradeButton), $"{screen}: focus moved into the card, onto Upgrade");

            // Closing by Back, by Escape, or by the card's own control returns focus to the location, with the cursor where it was.
            Assert.That(h.Shell.HandleBack(), Is.True);
            yield return ShellRig.Settle();
            Assert.That(h.Shell.IsCardOpen, Is.False);
            Assert.That(Focused, Is.SameAs(stop), $"{screen}: focus returns to the location stop");
            Assert.That(h.Shell.LocationCursorSlot, Is.EqualTo(order[2]), $"{screen}: on the same location");

            h.Shell.OpenSlot(order[2], SlotCondition.Occupied, 0, true);
            yield return ShellRig.Settle();
            CardHarness.Press(h.Card.CloseButton);
            yield return ShellRig.Settle();
            Assert.That(h.Shell.IsCardOpen, Is.False);
            Assert.That(Focused, Is.SameAs(stop), $"{screen}: the card's close control returns focus to the location too");
        }

        [UnityTest]
        public IEnumerator TheRingFollowsTheLocationRectangleTheCameraProvides()
        {
            yield return Make(ShellScreens.All().First());
            h.Shell.SetLocationOrder(new[] { 3, 4, 5 });
            var provided = new Rect(120f, 160f, 70f, 80f);
            h.Shell.LocationRectProvider = slot => slot == 3 ? provided : (Rect?)null;
            h.Shell.LocationStop.Focus();
            yield return ShellRig.Settle();

            var bound = h.Shell.LocationStop.worldBound;
            Assert.That(bound.center.x, Is.EqualTo(provided.center.x).Within(1f), "the stop sits over the location");
            Assert.That(bound.center.y, Is.EqualTo(provided.center.y).Within(1f));
            Assert.That(bound.width, Is.GreaterThanOrEqualTo(provided.width - 0.5f));
            Assert.That(h.Shell.FocusRingTarget, Is.SameAs(h.Shell.LocationStop));
        }

        [UnityTest]
        public IEnumerator TheBackStackPopsTheCardOnlyAfterDetailsAndTheDialog()
        {
            yield return Make(ShellScreens.All().First());
            h.Build(0, "machine.miner");
            yield return h.OpenCard(0);
            CardHarness.Press(h.Card.DetailsButton);
            yield return ShellRig.Settle();
            CardHarness.Press(h.Details.RemoveButton);
            yield return ShellRig.Settle();
            Assert.That(h.Shell.IsConfirmationOpen, Is.True, "Remove asks first (FR-019)");

            Assert.That(h.Shell.HandleBack(), Is.True);
            yield return null;
            Assert.That(h.Shell.IsConfirmationOpen, Is.False, "1: the dialog pops first");
            Assert.That(h.Shell.IsDetailsFromCard, Is.True);

            Assert.That(h.Shell.HandleBack(), Is.True);
            yield return null;
            Assert.That(h.Shell.IsCardOpen, Is.True, "2: Details pops back to the card");

            Assert.That(h.Shell.HandleBack(), Is.True);
            yield return null;
            Assert.That(h.Shell.IsCardOpen, Is.False, "3: the card pops last");
            Assert.That(h.Shell.HandleBack(), Is.False, "nothing is left to pop");
        }
    }
}
#endif
