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
    /// T116 (FR-023, FR-032, UX-3): the Industry surface. The IP balance, the upgrades with level, cost and effect, the "Applies next run" tag of
    /// the starting effects, the unaffordable and maxed states with their reason, the purchase through the application, and the worst-case layout
    /// at every screen class (rotations included) at 100 and 200 percent text scale.
    /// </summary>
    public sealed class IndustryPanelTests
    {
        private static readonly string[] ExpectedOrder = { Calibration, MarketContacts, SeedCapital, Toolmaker, Warehouse };

        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;
        private RunSession session;
        private RunCommands commands;
        private IndustryPanel panel;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        public static IEnumerable<int> Percents() => new[] { 100, 200 };

        public static IEnumerable<ShellScreen> Representative()
        {
            var all = ShellScreens.All().ToList();
            yield return all.First(screen => screen.Name.StartsWith("phone-portrait 360", StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("phone-landscape 640", StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("tablet-portrait", StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("desktop 1280", StringComparison.Ordinal));
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

        private IEnumerator Open(ShellScreen screen, int percent = 100, IdleFactory.Unity.Presentation.ShellDeviceProfile? profile = null)
        {
            rig = ShellRig.Create(screen, profile, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            session = NewSession(out commands);
            // Standing alone, the surface registers as the Research panel (the shell has no Industry tab).
            panel = new IndustryPanel(session, commands, Styles("IndustryPanel.uss"), Icon);
            rig.Shell.Panels.Register(panel);
            rig.Shell.SelectTab(ShellTab.Research);
            yield return ShellRig.Settle();
        }

        private IEnumerator Sync()
        {
            panel.Invalidate();
            panel.Refresh();
            yield return ShellRig.Settle();
        }

        private static bool Shown(VisualElement element) => IsDisplayed(element);

        private long StartCash() => session.Catalog.RunStart.StartingCash;

        // The effect copy of an upgrade, from the shipped catalog's effect (T344), never from a typed revision.
        private string EffectCopy(string id)
        {
            var def = session.Catalog.IndustryUpgrades[id];
            var effect = def.Effect;
            return def.IsDecayed
                ? "Next level: " + ResearchEffectCopy.Describe(session.Catalog, effect.Kind, effect.TargetKind, effect.TargetId, def.ValueTable[0], false)
                : ResearchEffectCopy.Describe(session.Catalog, effect.Kind, effect.TargetKind, effect.TargetId, effect.Value);
        }

        private int MaxLevelOf(string id) => session.Catalog.IndustryUpgrades[id].MaxLevel;

        private long SeedValue() => session.Catalog.IndustryUpgrades[SeedCapital].Effect.Value;

        /// <summary>The Cash the first <paramref name="levels"/> levels of Seed Capital add: the sum of the decayed value table (revision 3 decays the amount per level).</summary>
        private long SeedTotal(int levels) => session.Catalog.IndustryUpgrades[SeedCapital].ValueTable.Take(levels).Sum();

        // ---------------------------------------------------------------------------------------------------
        // Content
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheShellMountsTheSurface([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            Assert.That(rig.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Research));
            Assert.That(panel.Root.parent, Is.SameAs(rig.Shell.PanelContent));
            Assert.That(panel.Title, Is.EqualTo("Industry"));
        }

        [UnityTest]
        public IEnumerator TheIpBalanceAndEveryUpgradeShowLevelCostAndEffect()
        {
            yield return Open(ShellScreens.All().First());
            GrantIp(session, 1_250);
            yield return Sync();
            Assert.That(panel.UpgradeOrder, Is.EqualTo(ExpectedOrder));
            Assert.That(panel.IpLabel.text, Is.EqualTo("Industry points: 1,250 IP"));

            var calibrationCost = session.Catalog.IndustryUpgrades[Calibration].IpCostTable[0];
            Assert.That(panel.TitleLabel(Calibration).text, Is.EqualTo("Machine Calibration"));
            Assert.That(panel.LevelLabel(Calibration).text, Is.EqualTo("Level 0 of " + session.Catalog.IndustryUpgrades[Calibration].MaxLevel));
            Assert.That(panel.EffectLabel(Calibration).text, Is.EqualTo(EffectCopy(Calibration)));
            Assert.That(panel.CostLabel(Calibration).text, Is.EqualTo("Next level: " + QuantityFormatter.Format(calibrationCost) + " IP"));
            Assert.That(panel.BuyButton(Calibration).text, Is.EqualTo("Upgrade for " + QuantityFormatter.Format(calibrationCost) + " IP"));

            Assert.That(panel.EffectLabel(MarketContacts).text, Is.EqualTo(EffectCopy(MarketContacts)));
            Assert.That(panel.EffectLabel(Toolmaker).text, Is.EqualTo(EffectCopy(Toolmaker)));
            Assert.That(panel.EffectLabel(SeedCapital).text, Is.EqualTo(EffectCopy(SeedCapital)));
            Assert.That(panel.EffectLabel(Warehouse).text, Is.EqualTo(EffectCopy(Warehouse)));
        }

        [UnityTest]
        public IEnumerator IndustryWorksWithATouchTapAndWithAPointerClick([Values(true, false)] bool touch)
        {
            var screen = ShellScreens.All().First();
            yield return Open(screen, 100, InputModeSupport.ProfileFor(screen, touch));
            var where = touch ? "touch" : "pointer";
            var cost = session.Catalog.IndustryUpgrades[Calibration].IpCostTable[0];
            GrantIp(session, cost - 1);
            yield return Sync();

            // Too little IP: the reason is visible text, the press changes nothing and the feedback sits next to the control.
            Assert.That(Shown(panel.ReasonLabel(Calibration)), Is.True, where + ": the reason needs no hover");
            yield return InputModeSupport.Tap(panel.BuyButton(Calibration), touch);
            yield return ShellRig.Settle();
            Assert.That(session.Permanent.IndustryLevel(Calibration), Is.Zero, where + ": nothing was bought");
            Assert.That(Shown(panel.FeedbackLabel(Calibration)), Is.True);

            GrantIp(session, 1);
            yield return Sync();
            yield return InputModeSupport.Tap(panel.BuyButton(Calibration), touch);
            yield return ShellRig.Settle();
            Assert.That(session.Permanent.IndustryLevel(Calibration), Is.EqualTo(1), where + ": the press bought the level");
            InputModeSupport.AssertTargets(new VisualElement[] { panel.BuyButton(Calibration) }, where);
        }

        [UnityTest]
        public IEnumerator OnlyTheStartingEffectsCarryTheAppliesNextRunTag([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            foreach (var id in ExpectedOrder)
            {
                var starting = id == SeedCapital || id == Warehouse;
                Assert.That(Shown(panel.TagLabel(id)), Is.EqualTo(starting), id);
                Assert.That(panel.TagLabel(id).text, Is.EqualTo("Applies next run"));
            }
        }

        [UnityTest]
        public IEnumerator TheNextRunStartIsShownFromTheApplication()
        {
            yield return Open(ShellScreens.All().First());
            Assert.That(panel.NextRunLabel.text, Is.EqualTo("Next run starts with $" + StartCash() + " and storage level 1."));

            SetIndustryLevel(session, SeedCapital, 3);
            SetIndustryLevel(session, Warehouse, 1);
            yield return Sync();
            Assert.That(panel.NextRunLabel.text, Is.EqualTo("Next run starts with $" + (StartCash() + SeedTotal(3)).ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " and storage level 2."));
        }

        // ---------------------------------------------------------------------------------------------------
        // Disabled with the reason, and the purchase
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ReasonAndRejectionShowTheWarningBesideTheirTextAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            var where = $"{screen} @{percent}%";
            ContractsPanelTestSupport.AssertIconBesideText(panel.ReasonLabel(Calibration), PanelIcons.Warning, where + " reason");
            Submit(panel.BuyButton(Calibration));
            yield return ShellRig.Settle();
            ContractsPanelTestSupport.AssertIconBesideText(panel.FeedbackLabel(Calibration), PanelIcons.Warning, where + " rejection");
            ContractsPanelTestSupport.AssertTextButtonsHaveNoChildren(panel.Root, where);
            AssertShellLayout(rig, "industry with warning icons");
        }

        [UnityTest]
        public IEnumerator AnUnaffordableUpgradeIsDisabledWithItsReasonBeforeTheTap()
        {
            yield return Open(ShellScreens.All().First());
            Assert.That(RunShellDocument.IsSoftDisabled(panel.BuyButton(Calibration)), Is.True);
            Assert.That(Shown(panel.ReasonLabel(Calibration)), Is.True);
            Assert.That(panel.ReasonLabel(Calibration).text, Is.EqualTo("Not enough Industry points."));

            Submit(panel.BuyButton(Calibration));
            yield return ShellRig.Settle();
            Assert.That(Shown(panel.FeedbackLabel(Calibration)), Is.True);
            Assert.That(panel.FeedbackLabel(Calibration).text, Is.EqualTo("Not enough Industry points."));
            Assert.That(session.Permanent.IndustryLevel(Calibration), Is.Zero);
            Assert.That(session.Permanent.Ip, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AnAffordableUpgradeIsBoughtOnceAndASpentBalanceDisablesIt([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            var cost = session.Catalog.IndustryUpgrades[Calibration].IpCostTable[0];
            GrantIp(session, cost);
            yield return Sync();
            Assert.That(RunShellDocument.IsSoftDisabled(panel.BuyButton(Calibration)), Is.False);
            Assert.That(Shown(panel.ReasonLabel(Calibration)), Is.False);

            var button = panel.BuyButton(Calibration);
            button.Focus();
            Submit(button);
            yield return ShellRig.Settle();
            Assert.That(session.Permanent.IndustryLevel(Calibration), Is.EqualTo(1));
            Assert.That(session.Permanent.Ip, Is.Zero);
            Assert.That(panel.LevelLabel(Calibration).text, Is.EqualTo("Level 1 of " + session.Catalog.IndustryUpgrades[Calibration].MaxLevel));
            Assert.That(panel.IpLabel.text, Is.EqualTo("Industry points: 0 IP"));
            Assert.That(RunShellDocument.IsSoftDisabled(button), Is.True, "the balance is spent");
            Assert.That(rig.Shell.NoticeText, Is.EqualTo(session.Catalog.IndustryUpgrades[Calibration].Label + " is now level 1."));
            Assert.That(Focused(rig.Root), Is.SameAs(button), "focus stays on the control");

            Submit(button);
            yield return ShellRig.Settle();
            Assert.That(session.Permanent.IndustryLevel(Calibration), Is.EqualTo(1), "a second press buys nothing");
        }

        [UnityTest]
        public IEnumerator BuyingAStartingEffectTellsThePlayerItAppliesNextRun()
        {
            yield return Open(ShellScreens.All().First());
            GrantIp(session, 1_000);
            yield return Sync();
            var cashBefore = session.State.Cash;
            var button = panel.BuyButton(SeedCapital);
            button.Focus();
            Submit(button);
            yield return ShellRig.Settle();
            Assert.That(session.Permanent.IndustryLevel(SeedCapital), Is.EqualTo(1));
            Assert.That(rig.Shell.NoticeText, Is.EqualTo(session.Catalog.IndustryUpgrades[SeedCapital].Label + " is now level 1. It applies next run."));
            Assert.That(panel.NextRunLabel.text, Is.EqualTo("Next run starts with $" + (StartCash() + SeedValue()).ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " and storage level 1."));
            Assert.That(session.State.Cash, Is.EqualTo(cashBefore), "the running factory is unchanged");
        }

        [UnityTest]
        public IEnumerator AMaxedUpgradeSaysSoAndIsDisabled()
        {
            yield return Open(ShellScreens.All().First());
            SetIndustryLevel(session, Warehouse, MaxLevelOf(Warehouse));
            GrantIp(session, 100_000);
            yield return Sync();
            Assert.That(panel.CostLabel(Warehouse).text, Is.EqualTo("Fully upgraded."));
            Assert.That(panel.BuyButton(Warehouse).text, Is.EqualTo("Max level"));
            Assert.That(RunShellDocument.IsSoftDisabled(panel.BuyButton(Warehouse)), Is.True);
            Assert.That(panel.ReasonLabel(Warehouse).text, Is.EqualTo("Already at the maximum level."));
            Submit(panel.BuyButton(Warehouse));
            yield return ShellRig.Settle();
            Assert.That(session.Permanent.IndustryLevel(Warehouse), Is.EqualTo(MaxLevelOf(Warehouse)));
            Assert.That(session.Permanent.Ip, Is.EqualTo(100_000));
        }

        [UnityTest]
        public IEnumerator EveryBuyButtonIsFocusableAndAtLeast44Points([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            foreach (var id in ExpectedOrder)
            {
                var button = panel.BuyButton(id);
                Assert.That(button.focusable, Is.True);
                Assert.That(button.layout.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), id);
                Assert.That(button.layout.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), id);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout (UX-3, F-4): worst-case content at every screen class, rotations included, at 100 and 200 percent
        // ---------------------------------------------------------------------------------------------------

        /// <summary>The worst case: a huge IP balance, upgrades at every state (maxed, owned, fresh), both next-run tags, and a rejection shown.</summary>
        private IEnumerator BuildWorstCase()
        {
            GrantIp(session, Huge / 1000L);
            SetIndustryLevel(session, Warehouse, 2);
            SetIndustryLevel(session, SeedCapital, 9);
            SetIndustryLevel(session, Calibration, Math.Max(1, session.Catalog.IndustryUpgrades[Calibration].MaxLevel - 1));
            yield return Sync();
            Submit(panel.BuyButton(Warehouse));
            yield return ShellRig.Settle();
            Assert.That(Shown(panel.FeedbackLabel(Warehouse)), Is.True);
        }

        [UnityTest]
        public IEnumerator TheWorstCaseSurfaceStaysContainedAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            yield return BuildWorstCase();
            yield return AssertPanelLayout(rig, panel.Root, "worst-case industry", percent);
        }

        [UnityTest]
        public IEnumerator TheFreshSurfaceStaysContainedAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            yield return ShellRig.Settle();
            AssertShellLayout(rig, "fresh industry");
        }

        [UnityTest]
        public IEnumerator PanelTextFollowsTheScaleVariablesAndNeverFallsBelowTheMinimum([ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(ShellScreens.All().First(), percent);
            yield return BuildWorstCase();
            var expected = percent / 100f;
            foreach (var label in panel.Root.Query<TextElement>().ToList().Where(item => IsDisplayed(item) && !string.IsNullOrEmpty(item.text)))
            {
                Assert.That(label.resolvedStyle.fontSize, Is.GreaterThanOrEqualTo(12f * expected - 0.51f), label.name);
                Assert.That(label.resolvedStyle.whiteSpace, Is.Not.EqualTo(WhiteSpace.NoWrap), label.name + " wraps");
            }
        }

        [UnityTest]
        public IEnumerator ARotationHasNothingToCancel()
        {
            yield return Open(ShellScreens.All().First());
            Assert.That(panel.CancelForRotation(), Is.False);
            yield return rig.Layout(ShellScreens.Rotated(rig.Screen));
            Assert.That(panel.Snapshot, Is.Not.Null);
        }
    }
}
#endif
