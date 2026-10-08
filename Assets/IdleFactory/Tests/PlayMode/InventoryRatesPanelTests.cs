#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.MarketPanelTestSupport;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T114 (FR-006, FR-011, FR-012, FR-032, FR-035): the Inventory and rates surface. Normal, empty, locked-hidden, full-store,
    /// auto-sell, rejection (value.limit), exact-value and keyboard states, and a worst-case layout at every screen class
    /// (rotations included) at 100 and 200 percent text scale.
    /// </summary>
    public sealed class InventoryRatesPanelTests
    {
        private const string Ore = "iron.ore";
        private const string Bar = "iron.bar";

        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;
        private RunSession session;
        private RunCommands commands;
        private InventoryRatesPanel panel;

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

        private IEnumerator Open(RunSession run, RunCommands runCommands, ShellScreen screen = null, int percent = 100)
        {
            session = run;
            commands = runCommands;
            rig = ShellRig.Create(screen ?? ShellScreens.All().First(), null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            panel = new InventoryRatesPanel(session, commands, rig.Shell, Tree("InventoryRatesPanel"), Styles("InventoryRatesPanel"), Icon)
            {
                RefreshIntervalSeconds = 0f,
            };
            rig.Shell.Panels.Register(panel);
            rig.Shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
        }

        private IEnumerator MiningRun()
        {
            var run = Fresh(out var runCommands, 1_000);
            Build(runCommands, 0, "machine.miner");
            Advance(run, 120);
            yield return Open(run, runCommands);
        }

        // ---------------------------------------------------------------------------------------------------
        // Normal, empty and locked states
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AFreshRunShowsNothingStoredYetAndOnlyTheUnlockedResource()
        {
            var run = Fresh(out var runCommands);
            yield return Open(run, runCommands);

            Assert.That(panel.IsEmptyShown, Is.True);
            Assert.That(panel.Root.Q<Label>("inventory-empty").text, Is.EqualTo("Nothing stored yet"));
            Assert.That(panel.ListedResources, Is.EqualTo(new[] { Ore }), "locked resources are hidden");
            Assert.That(panel.StoreValue.text, Is.EqualTo("Store 0 / 1,000"));
            Assert.That(panel.StockButton(Ore).text, Is.EqualTo("0"));
            Assert.That(panel.PriceButton(Ore).text, Is.EqualTo("$1 each"));
            Assert.That(panel.AutoSellButton(Ore).text, Is.EqualTo("Auto-sell: Off"));
            Assert.That(panel.Root.Q<VisualElement>("inventory-row-" + Bar), Is.Null, "the locked Iron Bar has no row");
            Assert.That(panel.Root.Q<VisualElement>("inventory-store-fill").resolvedStyle.width, Is.EqualTo(0f).Within(0.5f));
        }

        [UnityTest]
        public IEnumerator AllUnlockedResourcesAreListedInStockOrder()
        {
            var run = Fresh(out var runCommands, 25, true);
            yield return Open(run, runCommands);
            Assert.That(panel.ListedResources.Count, Is.EqualTo(4));
            Assert.That(panel.ListedResources, Is.SupersetOf(new[] { Ore, Bar, "iron.plate", "iron.gear" }));
            Assert.That(panel.IsEmptyShown, Is.True, "nothing stored yet, although every resource is listed");
        }

        [UnityTest]
        public IEnumerator AMiningFactoryShowsStockNetRatePriceAndTheStoreBar()
        {
            yield return MiningRun();
            var state = session.State;
            var units = state.Inventory.Units(Ore);
            Assert.That(units, Is.GreaterThan(0));

            Assert.That(panel.IsEmptyShown, Is.False);
            Assert.That(panel.StockButton(Ore).text, Is.EqualTo(QuantityFormatter.Format(units)));
            Assert.That(panel.NetButton(Ore).text, Does.StartWith("+").And.EndsWith(" /min"));
            Assert.That(panel.StoreValue.text, Is.EqualTo("Store " + QuantityFormatter.Format(state.Inventory.Total) + " / 1,000"));
            var fill = panel.Root.Q<VisualElement>("inventory-store-fill");
            Assert.That(fill.resolvedStyle.width, Is.GreaterThan(0f), "the store bar fills with the stock");
            Assert.That(panel.Root.Q<VisualElement>("inventory-store").ClassListContains("inv-store--full"), Is.False);
            // The rate agrees with the application read model.
            var rates = RunSnapshots.Rates(session).Resources.First(item => item.ResourceId == Ore);
            Assert.That(panel.NetButton(Ore).text, Is.EqualTo("+" + rates.NetText + " /min"));
        }

        [UnityTest]
        public IEnumerator AFullStoreSaysSoWithTextAndNotOnlyColor()
        {
            var run = Fresh(out var runCommands, 1_000);
            Build(runCommands, 0, "machine.miner");
            Advance(run, 4 * 3600);
            yield return Open(run, runCommands);

            Assert.That(run.State.Inventory.Total, Is.EqualTo(run.State.Inventory.Capacity));
            var section = panel.Root.Q<VisualElement>("inventory-store");
            Assert.That(section.ClassListContains("inv-store--full"), Is.True);
            var full = panel.Root.Q<Label>("inventory-store-full");
            Assert.That(full.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(full.text, Does.Contain("store is full"));
            Assert.That(panel.StoreValue.text, Is.EqualTo("Store 1,000 / 1,000"));
        }

        [UnityTest]
        public IEnumerator ARefreshAfterTheRunChangesUpdatesTheRowsInPlace()
        {
            yield return MiningRun();
            var stock = panel.StockButton(Ore);
            var before = stock.text;
            Advance(session, 60);
            panel.Refresh();
            Assert.That(panel.StockButton(Ore), Is.SameAs(stock), "the row is updated in place, so a focused control keeps its focus");
            Assert.That(stock.text, Is.Not.EqualTo(before));
        }

        // ---------------------------------------------------------------------------------------------------
        // Auto-sell
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TurningAutoSellOnSellsTheStockThroughTheCommand()
        {
            yield return MiningRun();
            var cash = session.ReadCash();
            var toggle = panel.AutoSellButton(Ore);
            toggle.Focus();
            Activate(toggle);
            yield return ShellRig.Settle();

            Assert.That(session.State.IsAutoSell(Ore), Is.True);
            Assert.That(session.ReadCash(), Is.GreaterThan(cash), "enabling auto-sell sells the stock at once");
            Assert.That(session.State.Inventory.Units(Ore), Is.EqualTo(0));
            Assert.That(panel.AutoSellButton(Ore), Is.SameAs(toggle));
            Assert.That(toggle.text, Is.EqualTo("Auto-sell: On"));
            Assert.That(toggle.ClassListContains("inv-autosell--on"), Is.True);
            Assert.That(panel.StockButton(Ore).text, Is.EqualTo("0"));
            Assert.That(panel.RowReason(Ore).text, Is.Empty);

            Activate(toggle);
            Assert.That(session.State.IsAutoSell(Ore), Is.False);
            Assert.That(toggle.text, Is.EqualTo("Auto-sell: Off"));
        }

        [UnityTest]
        public IEnumerator AnOverflowingAutoSellIsRejectedWithValueLimitAndNothingChanges()
        {
            // Cash sits just below the exact bound, so selling the stock would overflow it (value.limit).
            var run = Fresh(out var runCommands, ExactBound.Value - 20);
            Build(runCommands, 0, "machine.miner");
            Advance(run, 600);
            yield return Open(run, runCommands);
            var cash = run.ReadCash();
            var units = run.State.Inventory.Units(Ore);
            Assert.That(units, Is.GreaterThan(10));

            var toggle = panel.AutoSellButton(Ore);
            Activate(toggle);
            yield return ShellRig.Settle();

            Assert.That(run.State.IsAutoSell(Ore), Is.False, "a rejection changes no setting");
            Assert.That(run.ReadCash(), Is.EqualTo(cash));
            Assert.That(run.State.Inventory.Units(Ore), Is.EqualTo(units));
            Assert.That(panel.RowReason(Ore).text, Is.EqualTo(RunRejectionCopy.For(RunReasons.ValueLimit)));
            Assert.That(panel.RowReason(Ore).style.display.value, Is.EqualTo(DisplayStyle.Flex), "the reason shows next to the toggle");
            Assert.That(toggle.text, Is.EqualTo("Auto-sell: Off"));
        }

        [UnityTest]
        public IEnumerator TheRejectionShowsTheWarningBesideItsTextAtEveryScreenClassAndClearsWithIt(
            [ValueSource(typeof(MarketPanelTestSupport), nameof(MarketPanelTestSupport.Screens))] ShellScreen screen,
            [ValueSource(typeof(MarketPanelTestSupport), nameof(MarketPanelTestSupport.Percents))] int percent)
        {
            var run = Fresh(out var runCommands, ExactBound.Value - 20);
            Build(runCommands, 0, "machine.miner");
            Advance(run, 600);
            yield return Open(run, runCommands, screen, percent);
            var where = $"{screen} @{percent}%";
            Assert.That(PanelIcons.HasIcon(panel.RowReason(Ore)), Is.False, where + ": no icon without a rejection text");

            Activate(panel.AutoSellButton(Ore));
            yield return ShellRig.Settle();
            ContractsPanelTestSupport.AssertIconBesideText(panel.RowReason(Ore), PanelIcons.Warning, where + " value.limit");
            ContractsPanelTestSupport.AssertTextButtonsHaveNoChildren(panel.Root, where);

            panel.OnHidden();
            yield return ShellRig.Settle();
            Assert.That(panel.RowReason(Ore).text, Is.Empty, where);
            Assert.That(PanelIcons.HasIcon(panel.RowReason(Ore)), Is.False, where + ": the icon goes with its text");
        }

        [UnityTest]
        public IEnumerator EnterAndSpaceActivateTheToggleAndEveryControlIsInTheFocusOrder()
        {
            var run = Fresh(out var runCommands, 25, true);
            yield return Open(run, runCommands);
            var order = rig.Shell.GetFocusOrder();
            foreach (var id in panel.ListedResources)
            {
                foreach (var control in new VisualElement[] { panel.StockButton(id), panel.NetButton(id), panel.PriceButton(id), panel.AutoSellButton(id) })
                {
                    Assert.That(order, Does.Contain(control), control.name + " is reachable from the keyboard");
                }
            }

            Assert.That(order, Does.Contain(panel.StoreValue));

            var toggle = panel.AutoSellButton(Bar);
            toggle.Focus();
            Activate(toggle);
            Assert.That(run.State.IsAutoSell(Bar), Is.True, "Enter or Space (navigation submit) toggles auto-sell");
        }

        // ---------------------------------------------------------------------------------------------------
        // Exact values (UX-8)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheExactValueShowsInTheDetailLineOnFocus()
        {
            yield return MiningRun();
            var shell = rig.Shell;
            var units = session.State.Inventory.Units(Ore);

            panel.StockButton(Ore).Focus();
            yield return ShellRig.Settle();
            Assert.That(shell.DetailText, Is.EqualTo(QuantityFormatter.FormatExact(units) + " units in the store"));

            panel.NetButton(Ore).Focus();
            yield return ShellRig.Settle();
            Assert.That(shell.DetailText, Does.Contain("per minute").And.Contain("production"));

            panel.PriceButton(Ore).Focus();
            yield return ShellRig.Settle();
            Assert.That(shell.DetailText, Does.Contain("$1 per unit"));

            panel.StoreValue.Focus();
            yield return ShellRig.Settle();
            Assert.That(shell.DetailText, Is.EqualTo(QuantityFormatter.FormatExact(session.State.Inventory.Total) + " of 1,000 units stored"));
        }

        [TestCase(0L, "0")]
        [TestCase(62_500L, "0.0625")]
        [TestCase(1_000_000L, "1")]
        [TestCase(1_234_500_000L, "1,234.5")]
        [TestCase(-4_680_000L, "-4.68")]
        public void ExactRatesKeepEverySignificantDigit(long micro, string expected)
        {
            Assert.That(MarketPanelParts.FormatExactRate(micro, false), Is.EqualTo(expected));
        }


        // ---------------------------------------------------------------------------------------------------
        // Layout (UX-3, F-4)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WorstCaseContentStaysContainedAtEveryScreenClassAndTextScale(
            [ValueSource(typeof(MarketPanelTestSupport), nameof(MarketPanelTestSupport.Screens))] ShellScreen screen,
            [ValueSource(typeof(MarketPanelTestSupport), nameof(MarketPanelTestSupport.Percents))] int percent)
        {
            var run = WorstCase(out var runCommands);
            yield return Open(run, runCommands, screen, percent);
            Assert.That(panel.ListedResources.Count, Is.EqualTo(4));
            Assert.That(panel.IsEmptyShown, Is.False);
            yield return CheckLayout(rig, panel, "inventory worst case");
        }
    }
}
#endif
