#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.MarketPanelTestSupport;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T319 (US1, FR-001 to FR-003; contracts/ui-surfaces-f005.md, QA-12 "Sell selector and button"). The step selector and the Sell
    /// button of every inventory row. QA-12 rows covered here, each at the screen classes and text scales named per test:
    /// normal; nothing to sell; locked; "Need N"; stale tap re-render with focus kept; All confirmation (shortfall, then rotation
    /// cancel); success notice with the exact value; each rejection copy (resource.locked, sell.quantity-invalid, stock.insufficient,
    /// value.limit); plus the lifecycle block, recovery, accessible names, per-row focus order, keyboard and touch/pointer exact value.
    /// </summary>
    public sealed class MarketSellPanelTests
    {
        private const string Plate = "iron.plate";
        private const string Ore = "iron.ore";

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

        private IEnumerator Open(RunSession run, RunCommands runCommands, ShellScreen screen = null, int percent = 100, ShellDeviceProfile? profile = null)
        {
            session = run;
            commands = runCommands;
            screen = screen ?? ShellScreens.All().First();
            rig = ShellRig.Create(screen, profile, percent);
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

        /// <summary>A run with every resource unlocked and <paramref name="plates"/> Iron Plate in the store.</summary>
        private IEnumerator OpenWithPlates(long plates, long cash = 25, ShellScreen screen = null, int percent = 100, ShellDeviceProfile? profile = null)
        {
            var run = Fresh(out var runCommands, cash, true);
            if (plates > 0)
            {
                ContractsPanelTestSupport.AddStock(run, Plate, plates);
            }

            yield return Open(run, runCommands, screen, percent, profile);
        }

        private IEnumerator Sync()
        {
            panel.Render(true);
            yield return ShellRig.Settle();
        }

        private void SelectStep(string id, long step)
        {
            for (var i = 0; i < 6 && panel.SelectedStep(id) != step; i++)
            {
                Activate(panel.SellStepButton(id));
            }

            Assert.That(panel.SelectedStep(id), Is.EqualTo(step));
        }

        private long UnitPrice(string id) => RunSnapshots.Economy(session).Resources.First(item => item.ResourceId == id).UnitPrice;

        private static string Reason(string code) => RunRejectionCopy.For(code);

        // ---------------------------------------------------------------------------------------------------
        // Normal and selector
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryRowHasAStepSelectorAndASellButtonWithStableNames()
        {
            yield return OpenWithPlates(25);
            foreach (var id in panel.ListedResources)
            {
                Assert.That(panel.SellStepButton(id), Is.Not.Null, "inventory-sell-step-" + id);
                Assert.That(panel.SellStepButton(id).name, Is.EqualTo("inventory-sell-step-" + id));
                Assert.That(panel.SellButton(id).name, Is.EqualTo("inventory-sell-" + id));
            }
        }

        [UnityTest]
        public IEnumerator ASellableStepIsAvailableAndTheSelectorCyclesTheContentStepsThenAll()
        {
            yield return OpenWithPlates(250);
            var sell = panel.SellButton(Plate);
            Assert.That(panel.SelectedStep(Plate), Is.EqualTo(1L), "the first content step is the default");
            Assert.That(sell.text, Is.EqualTo("Sell 1"));
            Assert.That(RunShellDocument.IsSoftDisabled(sell), Is.False);
            Assert.That(panel.SellHint(Plate).text, Is.Empty);
            Assert.That(sell.tooltip, Is.EqualTo("Sell 1 Iron Plate, available"));
            Assert.That(panel.SellStepButton(Plate).tooltip, Is.EqualTo("Sell amount, 1"));

            var expected = new[] { 10L, 100L, ResourceEconomy.SellAllStep, 1L };
            var texts = new[] { "Sell 10", "Sell 100", "Sell all", "Sell 1" };
            for (var i = 0; i < expected.Length; i++)
            {
                Activate(panel.SellStepButton(Plate));
                Assert.That(panel.SelectedStep(Plate), Is.EqualTo(expected[i]));
                Assert.That(sell.text, Is.EqualTo(texts[i]));
            }

            SelectStep(Plate, ResourceEconomy.SellAllStep);
            Assert.That(panel.SellStepButton(Plate).text, Is.EqualTo("Amount: All"));
            Assert.That(panel.SellStepButton(Plate).tooltip, Is.EqualTo("Sell amount, all"));
            Assert.That(sell.tooltip, Is.EqualTo("Sell all 250 Iron Plate, available"));
        }

        [UnityTest]
        public IEnumerator TheStepIsRememberedPerResourceAcrossRenders()
        {
            var run = Fresh(out var runCommands, 25, true);
            ContractsPanelTestSupport.AddStock(run, Plate, 400);
            ContractsPanelTestSupport.AddStock(run, Ore, 400);
            yield return Open(run, runCommands);
            SelectStep(Plate, 100);
            yield return Sync();
            Assert.That(panel.SelectedStep(Plate), Is.EqualTo(100L));
            Assert.That(panel.SelectedStep(Ore), Is.EqualTo(1L), "another resource keeps its own step");
        }

        // ---------------------------------------------------------------------------------------------------
        // Success and the exact value
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator SellingTenSellsThroughTheCommandShowsTheNoticeAndTheExactValueAndKeepsFocus()
        {
            yield return OpenWithPlates(25);
            var unit = UnitPrice(Plate);
            SelectStep(Plate, 10);
            yield return ShellRig.Settle();
            var cash = session.ReadCash();
            var sell = panel.SellButton(Plate);
            sell.Focus();
            yield return ShellRig.Settle();
            Activate(sell);
            Assert.That(ContractsPanelTestSupport.Focused(sell), Is.SameAs(sell), "focus stays on Sell right after the tap");
            yield return ShellRig.Settle();

            Assert.That(session.State.Inventory.Units(Plate), Is.EqualTo(15));
            Assert.That(session.ReadCash(), Is.EqualTo(cash + 10 * unit));
            var text = "Sold 10 Iron Plate: +" + MarketPanelParts.Money(10 * unit);
            Assert.That(rig.Shell.NoticeText, Is.EqualTo(text));
            Assert.That(panel.SellResult(Plate).text, Is.EqualTo(text));
            Assert.That(IsDisplayed(panel.SellResult(Plate)), Is.True);
            Assert.That(panel.SellReason(Plate).text, Is.Empty);
            Assert.That(panel.SellButton(Plate), Is.SameAs(sell), "the row is updated in place");
            Assert.That(ContractsPanelTestSupport.Focused(sell), Is.SameAs(sell), "focus stays on Sell");
            Assert.That(panel.StockButton(Plate).text, Is.EqualTo("15"));

            panel.SellResult(Plate).Focus();
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.DetailText, Is.EqualTo("10 × $" + unit + " = $" + (10 * unit)), "the exact value is N × $p = $Y");
        }

        [UnityTest]
        public IEnumerator SellingAllSellsTheWholeStockAndNeedsNoConfirmationWithoutAContract()
        {
            yield return OpenWithPlates(37);
            var unit = UnitPrice(Plate);
            SelectStep(Plate, ResourceEconomy.SellAllStep);
            Activate(panel.SellButton(Plate));
            yield return ShellRig.Settle();

            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(session.State.Inventory.Units(Plate), Is.EqualTo(0));
            Assert.That(panel.SellResult(Plate).text, Is.EqualTo("Sold 37 Iron Plate: +" + MarketPanelParts.Money(37 * unit)));
            Assert.That(panel.SellHint(Plate).text, Is.EqualTo(InventoryRatesPanel.NothingToSellText), "the emptied row says so");
            Assert.That(RunShellDocument.IsSoftDisabled(panel.SellButton(Plate)), Is.True);
        }

        [Test]
        public void TheExactLineShowsTheUnitPriceTheTotalAndARoundingNoteOnlyWhenTheProductHasAFraction()
        {
            var run = Fresh(out _, 25, true);
            var economy = RunSnapshots.Economy(run).Resources.First(item => item.ResourceId == Plate);
            var total = 1234 * economy.UnitPrice;
            Assert.That(InventoryRatesPanel.ExactSaleLine(economy, 1234, total),
                Is.EqualTo("1,234 × $" + economy.UnitPrice + " = $" + QuantityFormatter.FormatExact(total)));
        }

        // ---------------------------------------------------------------------------------------------------
        // Nothing to sell, Need N, locked
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ARowWithNoStockSaysNothingToSellStaysFocusableAndRejectsATapWithTheCopy()
        {
            yield return OpenWithPlates(0);
            var sell = panel.SellButton(Plate);
            Assert.That(panel.SellHint(Plate).text, Is.EqualTo("Nothing to sell"));
            Assert.That(IsDisplayed(panel.SellHint(Plate)), Is.True);
            Assert.That(RunShellDocument.IsSoftDisabled(sell), Is.True);
            Assert.That(sell.tooltip, Is.EqualTo("Sell 1 Iron Plate, unavailable: nothing to sell"));
            Assert.That(rig.Shell.GetFocusOrder(), Does.Contain(sell), "a soft-disabled Sell stays in the focus order");

            var cash = session.ReadCash();
            sell.Focus();
            Activate(sell);
            yield return ShellRig.Settle();
            Assert.That(session.ReadCash(), Is.EqualTo(cash));
            Assert.That(panel.SellReason(Plate).text, Is.EqualTo(Reason(RunReasons.StockInsufficient)));
            Assert.That(IsDisplayed(panel.SellReason(Plate)), Is.True);
            Assert.That(PanelIcons.HasIcon(panel.SellReason(Plate)), Is.True, "the rejection carries the warning icon");
            Assert.That(ContractsPanelTestSupport.Focused(sell), Is.SameAs(sell), "focus stays on the soft-disabled Sell");
            Assert.That(IsDisplayed(panel.SellResult(Plate)), Is.False);
        }

        [UnityTest]
        public IEnumerator AStepAboveTheStockShowsNeedNWithoutChangingTheStepAndRecoversWhenStockArrives()
        {
            yield return OpenWithPlates(5);
            SelectStep(Plate, 10);
            Assert.That(panel.SellHint(Plate).text, Is.EqualTo("Need 10"));
            Assert.That(panel.SelectedStep(Plate), Is.EqualTo(10L), "the step is never changed silently");
            Assert.That(RunShellDocument.IsSoftDisabled(panel.SellButton(Plate)), Is.True);
            Assert.That(panel.SellButton(Plate).tooltip, Is.EqualTo("Sell 10 Iron Plate, unavailable: not enough stock"));

            Activate(panel.SellButton(Plate));
            yield return ShellRig.Settle();
            Assert.That(panel.SellReason(Plate).text, Is.EqualTo(Reason(RunReasons.StockInsufficient)));
            Assert.That(session.State.Inventory.Units(Plate), Is.EqualTo(5));
            Assert.That(panel.SelectedStep(Plate), Is.EqualTo(10L), "a rejected tap keeps the step");

            // Recovery: stock arrives and the row becomes available again, with the reason line cleared by the next action.
            ContractsPanelTestSupport.AddStock(session, Plate, 20);
            yield return Sync();
            Assert.That(panel.SellHint(Plate).text, Is.Empty);
            Assert.That(RunShellDocument.IsSoftDisabled(panel.SellButton(Plate)), Is.False);
            Assert.That(panel.SellButton(Plate).tooltip, Is.EqualTo("Sell 10 Iron Plate, available"));
            Activate(panel.SellButton(Plate));
            yield return ShellRig.Settle();
            Assert.That(session.State.Inventory.Units(Plate), Is.EqualTo(15));
            Assert.That(panel.SellReason(Plate).text, Is.Empty, "a successful sale clears the old rejection");
        }

        [UnityTest]
        public IEnumerator ALockedResourceHasNoRowAndTheLockedStateIsWhatTheViewShowsForIt()
        {
            // A fresh run has Iron Ore only; Iron Plate is locked and, like every locked resource, is not listed (FR-006, T114).
            var run = Fresh(out var runCommands);
            yield return Open(run, runCommands);
            Assert.That(panel.ListedResources, Does.Not.Contain(Plate));
            Assert.That(panel.SellButton(Plate), Is.Null);
            Assert.That(panel.SellStepButton(Plate), Is.Null);

            // The locked state the view would show if a row existed: "Locked" with the application's resource.locked reason.
            var locked = RunSnapshots.Economy(run).Resources.First(item => item.ResourceId == Plate);
            Assert.That(locked.Unlocked, Is.False);
            var view = InventoryRatesPanel.Evaluate(locked, locked.SellPreview(1));
            Assert.That(view.Available, Is.False);
            Assert.That(view.Hint, Is.EqualTo(InventoryRatesPanel.LockedText));
            Assert.That(view.IsLocked, Is.True);
            Assert.That(InventoryRatesPanel.SellName("Iron Plate", locked.SellPreview(1), view), Is.EqualTo("Sell 1 Iron Plate, unavailable: locked"));
            Assert.That(locked.SellPreview(1).UnavailableReason, Is.EqualTo(RunReasons.ResourceLocked));
            var direct = runCommands.Sell(Plate, 1);
            Assert.That(direct.ReasonCode, Is.EqualTo(RunReasons.ResourceLocked), "the command behind the view rejects with resource.locked");
            Assert.That(Reason(RunReasons.ResourceLocked), Is.EqualTo("That resource is not unlocked yet."));
        }

        [UnityTest]
        public IEnumerator ANonOfferedStepFallsBackToCommandInvalidCopyAndSellQuantityInvalidHasItsOwnCopy()
        {
            var run = Fresh(out _, 25, true);
            var economy = RunSnapshots.Economy(run).Resources.First(item => item.ResourceId == Plate);
            var preview = economy.SellPreview(7);
            Assert.That(preview.UnavailableReason, Is.EqualTo(RunReasons.CommandInvalid));
            var view = InventoryRatesPanel.Evaluate(economy, preview);
            Assert.That(view.Hint, Is.EqualTo(Reason(RunReasons.CommandInvalid)));
            Assert.That(view.Available, Is.False);
            // The step list comes from content and never offers a quantity below 1, so the UI cannot reach sell.quantity-invalid by a
            // tap; its copy is still the shared one if the application reports it.
            Assert.That(Reason(RunReasons.SellQuantityInvalid), Is.EqualTo("Choose at least one unit to sell."));
            yield return null;
        }

        // ---------------------------------------------------------------------------------------------------
        // Stale tap, value.limit, lifecycle
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AStaleTapRerendersTheRowKeepsFocusAndShowsStockInsufficient()
        {
            yield return OpenWithPlates(10);
            SelectStep(Plate, 10);
            var sell = panel.SellButton(Plate);
            sell.Focus();
            yield return ShellRig.Settle();
            Assert.That(panel.SellHint(Plate).text, Is.Empty, "the panel believes 10 are available");

            // The stock changes outside the panel (a machine, auto-sell, a delivery) before the tap reaches the command.
            Assert.That(commands.SellAll(Plate).IsSuccess, Is.True);
            Activate(sell);
            yield return ShellRig.Settle();

            Assert.That(panel.SellReason(Plate).text, Is.EqualTo(Reason(RunReasons.StockInsufficient)));
            Assert.That(panel.SellHint(Plate).text, Is.EqualTo("Nothing to sell"), "the row re-rendered");
            Assert.That(RunShellDocument.IsSoftDisabled(sell), Is.True);
            Assert.That(panel.SellButton(Plate), Is.SameAs(sell));
            Assert.That(ContractsPanelTestSupport.Focused(sell), Is.SameAs(sell), "focus is kept on the same Sell button");
            Assert.That(panel.StockButton(Plate).text, Is.EqualTo("0"));
        }

        [UnityTest]
        public IEnumerator ValueLimitIsShownAsHintAndAsTheRejectionOfATapAndNothingChanges()
        {
            // Cash sits just below the exact bound, so any sale of Iron Plate would overflow it (value.limit).
            yield return OpenWithPlates(10, ExactBound.Value - 1);
            var sell = panel.SellButton(Plate);
            Assert.That(panel.SellHint(Plate).text, Is.EqualTo(Reason(RunReasons.ValueLimit)));
            Assert.That(RunShellDocument.IsSoftDisabled(sell), Is.True);
            Assert.That(sell.tooltip, Is.EqualTo("Sell 1 Iron Plate, unavailable: number too large"));

            var cash = session.ReadCash();
            Activate(sell);
            yield return ShellRig.Settle();
            Assert.That(panel.SellReason(Plate).text, Is.EqualTo(Reason(RunReasons.ValueLimit)));
            Assert.That(PanelIcons.HasIcon(panel.SellReason(Plate)), Is.True);
            Assert.That(session.ReadCash(), Is.EqualTo(cash));
            Assert.That(session.State.Inventory.Units(Plate), Is.EqualTo(10));
            Assert.That(rig.Shell.NoticeText, Does.Not.StartWith("Sold"));
        }

        [UnityTest]
        public IEnumerator ABlockedLifecycleDisablesSellWithItsReasonAndRecovers()
        {
            yield return OpenWithPlates(10);
            session.SetLifecycleBlocked(true);
            yield return Sync();
            var sell = panel.SellButton(Plate);
            Assert.That(RunShellDocument.IsSoftDisabled(sell), Is.True);
            Assert.That(panel.SellHint(Plate).text, Is.EqualTo(Reason(RunReasons.LifecycleBlocked)));
            Activate(sell);
            yield return ShellRig.Settle();
            Assert.That(panel.SellReason(Plate).text, Is.EqualTo(Reason(RunReasons.LifecycleBlocked)));
            Assert.That(session.State.Inventory.Units(Plate), Is.EqualTo(10));

            session.SetLifecycleBlocked(false);
            yield return Sync();
            Assert.That(RunShellDocument.IsSoftDisabled(sell), Is.False);
            Assert.That(panel.SellHint(Plate).text, Is.Empty);
            panel.OnHidden();
            Assert.That(panel.SellReason(Plate).text, Is.Empty, "hiding the panel clears the rejection");
        }

        // ---------------------------------------------------------------------------------------------------
        // All confirmation
        // ---------------------------------------------------------------------------------------------------

        private void AcceptPlateContract(long stock)
        {
            ContractsPanelTestSupport.UnlockContracts(session, ContractsPanelTestSupport.Plate);
            Assert.That(commands.AcceptContract(ContractsPanelTestSupport.Plate).IsSuccess, Is.True);
            var have = session.State.Inventory.Units(Plate);
            if (stock > have)
            {
                ContractsPanelTestSupport.AddStock(session, Plate, stock - have);
            }
        }

        [UnityTest]
        public IEnumerator AllAsksForConfirmationWhenItWouldLeaveTheDeliveryContractShortAndCancelSellsNothing()
        {
            yield return OpenWithPlates(0);
            AcceptPlateContract(130);
            var required = RunSnapshots.Contracts(session).Active.RequiredUnits;
            yield return Sync();
            SelectStep(Plate, ResourceEconomy.SellAllStep);
            var sell = panel.SellButton(Plate);
            sell.Focus();
            Activate(sell);
            yield return ShellRig.Settle();

            Assert.That(rig.Shell.IsConfirmationOpen, Is.True);
            Assert.That(ContractsPanelTestSupport.DialogText(rig.Shell), Does.Contain("Contract will be " + QuantityFormatter.Format(required) + " short"));
            Assert.That(session.State.Inventory.Units(Plate), Is.EqualTo(130), "nothing is sold before the player confirms");
            ContractsPanelTestSupport.AssertDialogLayout(rig, "sell-all confirmation", 100);

            Activate(rig.Shell.DialogCancelButton);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(session.State.Inventory.Units(Plate), Is.EqualTo(130));
            Assert.That(panel.CancelForRotation(), Is.False, "a closed confirmation has nothing to cancel");
            Assert.That(ContractsPanelTestSupport.Focused(sell), Is.SameAs(sell), "focus returns to Sell");
        }

        [UnityTest]
        public IEnumerator ConfirmingAllSellsTheWholeStockAndTheContractFallsShort()
        {
            yield return OpenWithPlates(0);
            AcceptPlateContract(130);
            yield return Sync();
            SelectStep(Plate, ResourceEconomy.SellAllStep);
            var cash = session.ReadCash();
            var unit = UnitPrice(Plate);
            Activate(panel.SellButton(Plate));
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.True);

            Activate(rig.Shell.DialogConfirmButton);
            yield return ShellRig.Settle();
            Assert.That(session.State.Inventory.Units(Plate), Is.EqualTo(0));
            Assert.That(session.ReadCash(), Is.EqualTo(cash + 130 * unit));
            Assert.That(panel.SellResult(Plate).text, Is.EqualTo("Sold 130 Iron Plate: +" + MarketPanelParts.Money(130 * unit)));
            Assert.That(RunSnapshots.Contracts(session).Active.MissingUnits, Is.EqualTo(RunSnapshots.Contracts(session).Active.RequiredUnits));
        }

        [UnityTest]
        public IEnumerator ARotationCancelsTheSellAllConfirmationWithoutSellingAndTheHostRoutesIt()
        {
            var run = Fresh(out var runCommands, 25, true);
            session = run;
            commands = runCommands;
            rig = ShellRig.Create(ShellScreens.All().First(), null, 100);
            rigs.Add(rig);
            yield return rig.Layout();
            var host = new MarketPanelHost(session, commands, rig.Shell, Tree("InventoryRatesPanel"), Styles("InventoryRatesPanel"),
                Tree("PrioritiesPanel"), Styles("PrioritiesPanel"), Tree("ExpansionPanel"), Styles("ExpansionPanel"), Icon);
            rig.Shell.Panels.Register(host);
            panel = host.Inventory;
            panel.RefreshIntervalSeconds = 0f;
            rig.Shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
            AcceptPlateContract(130);
            panel.Render(true);
            SelectStep(Plate, ResourceEconomy.SellAllStep);
            var sell = panel.SellButton(Plate);
            sell.Focus();
            Activate(sell);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.True);

            Assert.That(host.CancelForRotation(), Is.True);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(rig.Shell.NoticeText, Is.EqualTo(RunShellDocument.RotationCancelledNotice));
            Assert.That(session.State.Inventory.Units(Plate), Is.EqualTo(130), "a rotation sells nothing");
            Assert.That(host.CancelForRotation(), Is.False);
        }

        [UnityTest]
        public IEnumerator AllNeedsNoConfirmationWhenTheContractIsAlreadyShortOrIsForAnotherResource()
        {
            // Stock below the requirement: the contract is already short, so selling all does not drop it below anything.
            yield return OpenWithPlates(0);
            AcceptPlateContract(40);
            yield return Sync();
            SelectStep(Plate, ResourceEconomy.SellAllStep);
            Activate(panel.SellButton(Plate));
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(session.State.Inventory.Units(Plate), Is.EqualTo(0));

            // A contract for another resource is not affected by selling Iron Ore.
            ContractsPanelTestSupport.AddStock(session, Ore, 50);
            yield return Sync();
            SelectStep(Ore, ResourceEconomy.SellAllStep);
            Activate(panel.SellButton(Ore));
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(session.State.Inventory.Units(Ore), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ASmallerStepNeverAsksForConfirmation()
        {
            yield return OpenWithPlates(0);
            AcceptPlateContract(130);
            yield return Sync();
            SelectStep(Plate, 10);
            Activate(panel.SellButton(Plate));
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(session.State.Inventory.Units(Plate), Is.EqualTo(120));
        }

        // ---------------------------------------------------------------------------------------------------
        // Focus, keyboard, exact value
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator PerRowFocusOrderIsStepThenSellThenAutoSell()
        {
            yield return OpenWithPlates(25);
            var order = rig.Shell.GetFocusOrder().ToList();
            foreach (var id in panel.ListedResources)
            {
                var step = order.IndexOf(panel.SellStepButton(id));
                var sell = order.IndexOf(panel.SellButton(id));
                var auto = order.IndexOf(panel.AutoSellButton(id));
                Assert.That(step, Is.GreaterThanOrEqualTo(0), id + " step selector is reachable");
                Assert.That(sell, Is.GreaterThan(step), id + ": Sell follows the step selector");
                Assert.That(auto, Is.GreaterThan(sell), id + ": Auto-sell follows Sell");
            }
        }

        [UnityTest]
        public IEnumerator EnterAndSpaceOperateTheSelectorAndSellWithTheFocusKept()
        {
            yield return OpenWithPlates(25);
            var step = panel.SellStepButton(Plate);
            step.Focus();
            Activate(step);
            Assert.That(panel.SelectedStep(Plate), Is.EqualTo(10L));
            Assert.That(ContractsPanelTestSupport.Focused(step), Is.SameAs(step), "the selector keeps focus after changing the step");

            var sell = panel.SellButton(Plate);
            sell.Focus();
            Activate(sell);
            Assert.That(session.State.Inventory.Units(Plate), Is.EqualTo(15));
            Assert.That(ContractsPanelTestSupport.Focused(sell), Is.SameAs(sell));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThePointerShowsTheExactSaleValueOnHover()
        {
            var screen = ShellScreens.All().First();
            yield return OpenWithPlates(25, 25, screen, 100, ShellDeviceProfile.Pointer(screen.Width, screen.Height, 96f));
            var unit = UnitPrice(Plate);
            SelectStep(Plate, 10);
            Activate(panel.SellButton(Plate));
            yield return ShellRig.Settle();

            Send(panel.SellResult(Plate), PointerEnterEvent.GetPooled(), UnityEngine.UIElements.PointerType.mouse);
            yield return null;
            Assert.That(rig.Shell.DetailText, Is.EqualTo("10 × $" + unit + " = $" + (10 * unit)));
        }

        [UnityTest]
        public IEnumerator ATouchTapOnTheResultPinsTheExactSaleValue()
        {
            var screen = ShellScreens.All().First();
            yield return OpenWithPlates(25, 25, screen, 100, ShellDeviceProfile.Touch(screen.Width, screen.Height, 400f));
            var unit = UnitPrice(Plate);
            SelectStep(Plate, 10);
            Activate(panel.SellButton(Plate));
            yield return ShellRig.Settle();

            Send(panel.SellResult(Plate), ClickEvent.GetPooled(), UnityEngine.UIElements.PointerType.touch);
            yield return null;
            Assert.That(rig.Shell.IsDetailPinned, Is.True);
            Assert.That(rig.Shell.DetailText, Is.EqualTo("10 × $" + unit + " = $" + (10 * unit)));
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout: every screen class at 100 and 200 percent text
        // ---------------------------------------------------------------------------------------------------

        /// <summary>Every screen class plus a 640x480 window (compact landscape), whose side panel is the narrowest column that review F1 broke.</summary>
        public static IEnumerable<ShellScreen> ScreensWithNarrowDesktop()
        {
            foreach (var screen in ShellScreens.All())
            {
                yield return screen;
            }

            yield return new ShellScreen("window 640x480", 640, 480, 0, 0, 0, 0, FactoryHudLayout.CompactLandscape);
        }

        /// <summary>
        /// No label line may start or end mid-word (review F1, F3). A wrapping text breaks only at spaces, so it breaks inside a word
        /// exactly when its content box is narrower than the widest single word. Each word is measured on its own with no width
        /// limit (its natural width) and compared with the element's content width. Words are split at spaces only, so "+$80" is one
        /// word and a split between "+" and "$" would fail.
        /// </summary>
        private static void AssertNoMidWordBreak(Button button, string where)
        {
            Assert.That(IsDisplayed(button), Is.True, where + " " + button.name + " is displayed");
            var width = button.contentRect.width;
            foreach (var word in button.text.Split(' ').Where(item => item.Length > 0))
            {
                var natural = button.MeasureTextSize(word, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).x;
                Assert.That(width, Is.GreaterThanOrEqualTo(natural - 1f),
                    where + " " + button.name + ": the word '" + word + "' (" + natural.ToString("0.#") + " px) does not fit the " + width.ToString("0.#") + " px content box, so it would break mid-word");
            }
        }

        [UnityTest]
        public IEnumerator SellControlsStayContainedAndTouchSizedWithAResultAndARejectionAtEveryScreenClassAndTextScale(
            [ValueSource(nameof(ScreensWithNarrowDesktop))] ShellScreen screen,
            [ValueSource(typeof(MarketPanelTestSupport), nameof(MarketPanelTestSupport.Percents))] int percent)
        {
            var run = WorstCase(out var runCommands);
            yield return Open(run, runCommands, screen, percent);
            var where = $"{screen} @{percent}%";
            var ids = panel.ListedResources;
            Assert.That(ids.Count, Is.EqualTo(4), where);

            // One row shows a result, the next a "Need N" hint, the next a rejection with its warning icon, the last stays normal.
            SelectStep(ids[0], 1);
            Activate(panel.SellButton(ids[0]));
            SelectStep(ids[1], 1);
            Activate(panel.SellButton(ids[1]));
            Assert.That(IsDisplayed(panel.SellResult(ids[1])), Is.True, where + ": a sale shows its result");
            SelectStep(ids[1], 100);
            Assert.That(commands.SellAll(ids[1]).IsSuccess, Is.True);
            panel.Render(true);
            Activate(panel.SellButton(ids[1]));
            yield return ShellRig.Settle();
            Assert.That(panel.SellHint(ids[1]).text, Is.EqualTo("Nothing to sell"), where);
            ContractsPanelTestSupport.AssertIconBesideText(panel.SellReason(ids[1]), PanelIcons.Warning, where + " stock.insufficient");
            ContractsPanelTestSupport.AssertTextButtonsHaveNoChildren(panel.Root, where);

            foreach (var id in ids)
            {
                AssertNoMidWordBreak(panel.SellStepButton(id), where);
                AssertNoMidWordBreak(panel.SellButton(id), where);
                foreach (var button in new[] { panel.SellStepButton(id), panel.SellButton(id) })
                {
                    Assert.That(IsDisplayed(button), Is.True, where + " " + button.name);
                    Assert.That(button.worldBound.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), where + " " + button.name + " width");
                    Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), where + " " + button.name + " height");
                }

                var step = panel.SellStepButton(id).worldBound;
                var sell = panel.SellButton(id).worldBound;
                Assert.That(Intersects(step, sell), Is.False, where + " " + id + ": the selector and Sell do not overlap");
            }

            var result = panel.SellResult(ids[1]);
            AssertNoMidWordBreak(result, where);
            Assert.That(result.worldBound.width, Is.GreaterThanOrEqualTo(panel.SellButton(ids[1]).worldBound.width - 1f), where + ": the result is as wide as the Sell button");

            yield return CheckLayout(rig, panel, "sell controls with result and rejection");
        }

        [UnityTest]
        public IEnumerator TheSellAllConfirmationFitsEveryScreenClassAtEveryTextScale(
            [ValueSource(typeof(MarketPanelTestSupport), nameof(MarketPanelTestSupport.Screens))] ShellScreen screen,
            [ValueSource(typeof(MarketPanelTestSupport), nameof(MarketPanelTestSupport.Percents))] int percent)
        {
            var run = Fresh(out var runCommands, 25, true);
            yield return Open(run, runCommands, screen, percent);
            AcceptPlateContract(130);
            yield return Sync();
            SelectStep(Plate, ResourceEconomy.SellAllStep);
            panel.SellButton(Plate).Focus();
            Activate(panel.SellButton(Plate));
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.True, $"{screen} @{percent}%");
            ContractsPanelTestSupport.AssertDialogLayout(rig, "sell-all confirmation", percent);
            Assert.That(rig.Shell.CancelConfirmationForRotation(), Is.True);
            yield return ShellRig.Settle();
            Assert.That(session.State.Inventory.Units(Plate), Is.EqualTo(130));
        }
    }
}
#endif
