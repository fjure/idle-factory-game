#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.MarketPanelTestSupport;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T114 (FR-007, FR-035, FR-032): the Expansion surface. Storage and factory level with the next capacity or slots and the
    /// cost, an unaffordable upgrade that is disabled with its reason visible and still re-validated, a successful upgrade, the
    /// maximum level (disabled), exact values in the detail line, keyboard activation, and a worst-case layout at every screen
    /// class (rotations included) at 100 and 200 percent text scale.
    /// </summary>
    public sealed class ExpansionPanelTests
    {
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;
        private RunSession session;
        private RunCommands commands;
        private ExpansionPanel panel;

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

        // "<capacity now> > <capacity of the next level>", derived from the shipped storage levels (T344).
        private static string CapacityStep(int level) =>
            QuantityFormatter.Format(RunShellFixtures.StorageCapacity(level)) + " > " + QuantityFormatter.Format(RunShellFixtures.StorageCapacity(level + 1));

        private static string SlotStep(int level) =>
            QuantityFormatter.Format(RunShellFixtures.FactorySlots(level)) + " > " + QuantityFormatter.Format(RunShellFixtures.FactorySlots(level + 1));

        private IEnumerator Open(RunSession run, RunCommands runCommands, ShellScreen screen = null, int percent = 100)
        {
            session = run;
            commands = runCommands;
            rig = ShellRig.Create(screen ?? ShellScreens.All().First(), null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            panel = new ExpansionPanel(session, commands, rig.Shell, Tree("ExpansionPanel"), Styles("ExpansionPanel"), Icon);
            rig.Shell.Panels.Register(panel);
            rig.Shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
        }

        // ---------------------------------------------------------------------------------------------------
        // Normal and disabled states
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AFreshRunShowsBothTracksWithTheNextLevelAndItsCost()
        {
            var run = Fresh(out var runCommands);
            yield return Open(run, runCommands);

            Assert.That(panel.StorageLevel.text, Is.EqualTo("Level 1 of " + RunShellFixtures.StorageLevelCount));
            Assert.That(panel.StorageValue.text, Is.EqualTo(CapacityStep(1)));
            Assert.That(panel.StorageCost.text, Is.EqualTo(MarketPanelParts.Money(RunShellFixtures.StorageLevelCost(2))));
            Assert.That(panel.FactoryLevel.text, Is.EqualTo("Level 1 of " + RunShellFixtures.FactoryLevelCount));
            Assert.That(panel.FactoryValue.text, Is.EqualTo(SlotStep(1)));
            Assert.That(panel.FactoryCost.text, Is.EqualTo(MarketPanelParts.Money(RunShellFixtures.FactoryLevelCost(2))), "the price of factory level 2 is shown as Cash");

            var snapshot = RunSnapshots.Factory(session);
            Assert.That(snapshot.Storage.NextCost, Is.EqualTo(RunShellFixtures.StorageLevelCost(2)));
            Assert.That(snapshot.Factory.NextCost, Is.EqualTo(RunShellFixtures.FactoryLevelCost(2)));
        }

        [UnityTest]
        public IEnumerator AnUnaffordableUpgradeIsDisabledWithItsReasonVisibleAndStillRevalidates()
        {
            var run = Fresh(out var runCommands);
            yield return Open(run, runCommands);
            var cash = run.ReadCash();

            Assert.That(RunShellDocument.IsSoftDisabled(panel.StorageUpgrade), Is.True);
            Assert.That(panel.StorageReason.text, Is.EqualTo(RunRejectionCopy.For(RunReasons.CashInsufficient)), "the reason shows before the tap");
            Assert.That(panel.StorageReason.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            Activate(panel.StorageUpgrade);
            yield return ShellRig.Settle();

            Assert.That(run.State.StorageLevel, Is.EqualTo(1), "a rejected upgrade changes nothing");
            Assert.That(run.ReadCash(), Is.EqualTo(cash));
            Assert.That(panel.StorageReason.text, Is.EqualTo(RunRejectionCopy.For(RunReasons.CashInsufficient)));
            Assert.That(panel.StorageStatus.text, Is.Empty);
        }

        [UnityTest]
        public IEnumerator TheReasonShowsTheWarningBesideItsTextAtEveryScreenClass(
            [ValueSource(typeof(MarketPanelTestSupport), nameof(MarketPanelTestSupport.Screens))] ShellScreen screen,
            [ValueSource(typeof(MarketPanelTestSupport), nameof(MarketPanelTestSupport.Percents))] int percent)
        {
            var run = Fresh(out var runCommands);
            yield return Open(run, runCommands, screen, percent);
            var where = $"{screen} @{percent}%";
            ContractsPanelTestSupport.AssertIconBesideText(panel.StorageReason, PanelIcons.Warning, where + " storage");
            ContractsPanelTestSupport.AssertIconBesideText(panel.FactoryReason, PanelIcons.Warning, where + " factory");
            Activate(panel.StorageUpgrade);
            yield return ShellRig.Settle();
            ContractsPanelTestSupport.AssertIconBesideText(panel.StorageReason, PanelIcons.Warning, where + " rejection");
            ContractsPanelTestSupport.AssertTextButtonsHaveNoChildren(panel.Root, where);
        }

        [UnityTest]
        public IEnumerator AnAffordableUpgradeRaisesTheLevelAndTheNextCapacity()
        {
            var run = Fresh(out var runCommands, 1_000_000);
            yield return Open(run, runCommands);
            Assert.That(RunShellDocument.IsSoftDisabled(panel.StorageUpgrade), Is.False);
            Assert.That(panel.StorageReason.style.display.value, Is.EqualTo(DisplayStyle.None));

            var upgrade = panel.StorageUpgrade;
            upgrade.Focus();
            Activate(upgrade);
            yield return ShellRig.Settle();

            Assert.That(run.State.StorageLevel, Is.EqualTo(2));
            Assert.That(run.ReadCash(), Is.EqualTo(1_000_000 - RunShellFixtures.StorageLevelCost(2)));
            Assert.That(panel.StorageLevel.text, Is.EqualTo("Level 2 of " + RunShellFixtures.StorageLevelCount));
            Assert.That(panel.StorageValue.text, Is.EqualTo(CapacityStep(2)));
            Assert.That(panel.StorageCost.text, Is.EqualTo(MarketPanelParts.Money(RunShellFixtures.StorageLevelCost(3))));
            Assert.That(panel.StorageStatus.text, Does.Contain("level 2"));
            Assert.That(panel.StorageUpgrade, Is.SameAs(upgrade), "the control is updated in place and keeps its focus");
            Assert.That(upgrade.focusController.focusedElement, Is.SameAs(upgrade));

            Activate(panel.FactoryUpgrade);
            Assert.That(run.State.FactoryLevel, Is.EqualTo(2));
            Assert.That(panel.FactoryValue.text, Is.EqualTo(SlotStep(2)));
        }

        [UnityTest]
        public IEnumerator TheMaximumLevelIsDisabledWithItsReasonAndNoCost()
        {
            var run = Fresh(out var runCommands, 5_000_000);
            for (var i = 1; i < RunShellFixtures.StorageLevelCount; i++)
            {
                Assert.That(runCommands.UpgradeStorage().IsSuccess, Is.True);
            }

            for (var i = 1; i < RunShellFixtures.FactoryLevelCount; i++)
            {
                Assert.That(runCommands.UpgradeFactory().IsSuccess, Is.True);
            }

            yield return Open(run, runCommands);
            var maxStorage = RunShellFixtures.StorageLevelCount;
            var maxFactory = RunShellFixtures.FactoryLevelCount;
            Assert.That(panel.StorageLevel.text, Is.EqualTo("Level " + maxStorage + " of " + maxStorage));
            Assert.That(panel.StorageValue.text, Is.EqualTo(QuantityFormatter.Format(RunShellFixtures.StorageCapacity(maxStorage)) + " (max)"));
            Assert.That(panel.StorageCost.text, Is.EqualTo("-"));
            Assert.That(RunShellDocument.IsSoftDisabled(panel.StorageUpgrade), Is.True);
            Assert.That(panel.StorageReason.text, Is.EqualTo(RunRejectionCopy.For(RunReasons.LevelMax)));
            Assert.That(panel.FactoryValue.text, Is.EqualTo(QuantityFormatter.Format(RunShellFixtures.FactorySlots(maxFactory)) + " (max)"));
            Assert.That(RunShellDocument.IsSoftDisabled(panel.FactoryUpgrade), Is.True);

            var cash = run.ReadCash();
            Activate(panel.StorageUpgrade);
            Activate(panel.FactoryUpgrade);
            Assert.That(run.State.StorageLevel, Is.EqualTo(maxStorage));
            Assert.That(run.State.FactoryLevel, Is.EqualTo(maxFactory));
            Assert.That(run.ReadCash(), Is.EqualTo(cash), "the maximum level cannot be bought");
            Assert.That(panel.StorageReason.text, Is.EqualTo(RunRejectionCopy.For(RunReasons.LevelMax)));
        }

        [UnityTest]
        public IEnumerator RefreshFollowsCashSoAnUpgradeBecomesAvailableWhenItIsAffordable()
        {
            var factoryCost = RunShellFixtures.FactoryLevelCost(2);
            var run = Fresh(out var runCommands, factoryCost + RunShellFixtures.MinerCost - 10);
            Build(runCommands, 0, "machine.miner");
            Assert.That(runCommands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            yield return Open(run, runCommands);
            Assert.That(RunShellDocument.IsSoftDisabled(panel.FactoryUpgrade), Is.True, factoryCost - 10 + " Cash does not buy the " + factoryCost + " Cash factory level");

            Advance(run, 3 * 3600);
            Assert.That(run.ReadCash(), Is.GreaterThanOrEqualTo(factoryCost));
            panel.Refresh();
            Assert.That(RunShellDocument.IsSoftDisabled(panel.FactoryUpgrade), Is.False);
            Assert.That(panel.FactoryReason.text, Is.Empty);
        }

        // ---------------------------------------------------------------------------------------------------
        // Exact values and keyboard
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheExactValuesShowInTheDetailLineOnFocus()
        {
            var run = Fresh(out var runCommands, 1_000_000);
            yield return Open(run, runCommands);
            var shell = rig.Shell;

            panel.StorageValue.Focus();
            yield return ShellRig.Settle();
            Assert.That(shell.DetailText, Is.EqualTo("Capacity " + QuantityFormatter.FormatExact(RunShellFixtures.StorageCapacity(1)) + " units, next level " + QuantityFormatter.FormatExact(RunShellFixtures.StorageCapacity(2)) + " units"));

            panel.StorageCost.Focus();
            yield return ShellRig.Settle();
            Assert.That(shell.DetailText, Is.EqualTo(MarketPanelParts.MoneyExact(RunShellFixtures.StorageLevelCost(2)) + " Cash"));

            panel.FactoryValue.Focus();
            yield return ShellRig.Settle();
            Assert.That(shell.DetailText, Is.EqualTo("Slots " + QuantityFormatter.FormatExact(RunShellFixtures.FactorySlots(1)) + " slots, next level " + QuantityFormatter.FormatExact(RunShellFixtures.FactorySlots(2)) + " slots"));
        }

        [UnityTest]
        public IEnumerator EveryControlIsReachableFromTheKeyboard()
        {
            var run = Fresh(out var runCommands);
            yield return Open(run, runCommands);
            var order = rig.Shell.GetFocusOrder();
            foreach (var control in new VisualElement[]
                     {
                         panel.StorageValue, panel.StorageCost, panel.StorageUpgrade,
                         panel.FactoryValue, panel.FactoryCost, panel.FactoryUpgrade,
                     })
            {
                Assert.That(order, Does.Contain(control), control.name + " is reachable from the keyboard");
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout (UX-3, F-4)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WorstCaseContentStaysContainedAtEveryScreenClassAndTextScale(
            [ValueSource(typeof(MarketPanelTestSupport), nameof(MarketPanelTestSupport.Screens))] ShellScreen screen,
            [ValueSource(typeof(MarketPanelTestSupport), nameof(MarketPanelTestSupport.Percents))] int percent)
        {
            // Worst case: both tracks at the maximum level (the longest value text and a visible reason) and the biggest numbers.
            var run = WorstCase(out var runCommands);
            yield return Open(run, runCommands, screen, percent);
            Assert.That(panel.StorageValue.text, Does.Contain("(max)"));
            yield return CheckLayout(rig, panel, "expansion worst case");
        }

        [UnityTest]
        public IEnumerator TheUnaffordableCaseWithItsReasonStaysContainedAtEveryScreenClassAndTextScale(
            [ValueSource(typeof(MarketPanelTestSupport), nameof(MarketPanelTestSupport.Screens))] ShellScreen screen,
            [ValueSource(typeof(MarketPanelTestSupport), nameof(MarketPanelTestSupport.Percents))] int percent)
        {
            var run = Fresh(out var runCommands, RunShellFixtures.MinerCost);
            Assert.That(runCommands.Build(0, "machine.miner").IsSuccess, Is.True);
            yield return Open(run, runCommands, screen, percent);
            Activate(panel.StorageUpgrade);
            Assert.That(panel.StorageReason.text, Is.Not.Empty);
            yield return CheckLayout(rig, panel, "expansion unaffordable with a rejection");
        }
    }
}
#endif
