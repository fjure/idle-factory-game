#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.ContractsPanelTestSupport;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T367 (UI/UX review findings R-01, R-06, R-07, R-08, R-10, R-24, R-25, R-27 and the Q5 Item copy): the Industry pointer on the Prestige panel and
    /// the Research tab dot, the offer order and the one state label per offer, the distinct titles, the grouped lock text, the next-level
    /// Industry copy, the single Research rejection message and the clean stylesheets. The Machine panel (R-20, R-23), Offline (R-31) and the
    /// per-surface touch and pointer cases live beside the suites of those surfaces.
    /// </summary>
    public sealed class UiReviewFixesTests
    {
        private const string BonusPlate = "contract.bonus-plate";
        private const string ProducePlate = "contract.produce-plate";
        private const string PanelRoot = "Assets/IdleFactory/Unity/UI/Panels/";

        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;
        private RunSession session;
        private RunCommands commands;
        private ContractsPanel contracts;
        private PrestigePresenter prestige;
        private ResearchPanel research;
        private IndustryPanel industry;

        private static ShellScreen Phone => ShellScreens.All().First(screen => screen.Name.StartsWith("phone-portrait 360", StringComparison.Ordinal));

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
            contracts = null;
            prestige = null;
            research = null;
            industry = null;
        }

        private static bool Shown(VisualElement element) => IsDisplayed(element);

        private IEnumerator OpenContracts(ShellScreen screen, int percent, int factoryLevel, params string[] offers)
        {
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            session = RunShellFixtures.NewSession(out commands, factoryLevel: factoryLevel, cash: 25, unlockEverything: true);
            if (offers.Length > 0)
            {
                UnlockContracts(session, offers);
            }

            contracts = new ContractsPanel(session, commands, Styles("ContractsPanel.uss"), Icon);
            rig.Shell.Panels.Register(contracts);
            rig.Shell.SelectTab(ShellTab.Contracts);
            yield return ShellRig.Settle();
        }

        private IEnumerator SyncContracts()
        {
            contracts.Invalidate();
            contracts.Refresh();
            yield return ShellRig.Settle();
        }

        private string ResourceLabel(string id) => session.Catalog.Resources[id].Label;

        // ---------------------------------------------------------------------------------------------------
        // R-06: order
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator OffersAreOrderedLiveFirstThenCoolingDownThenEndedUsedOrLocked([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return OpenContracts(screen, 100, 1, Gear, Plate, TimedBar);
            Assert.That(contracts.OfferDisplayOrder, Is.EqualTo(new[] { Gear, Plate, TimedBar, BonusPlate, ProducePlate }),
                "live offers keep the content order, the locked ones follow");

            // Plate is delivered and cools down: it moves behind the live offers and in front of the locked ones.
            Submit(contracts.AcceptButton(Plate));
            AddStock(session, "iron.plate", RunShellFixtures.OfferQuantity(Plate));
            yield return SyncContracts();
            Submit(contracts.DeliverButton);
            yield return SyncContracts();
            Assert.That(session.State.Contracts.Active, Is.Null, "the delivery ended the contract");
            Assert.That(contracts.OfferDisplayOrder, Is.EqualTo(new[] { Gear, TimedBar, Plate, BonusPlate, ProducePlate }), "cooling down sorts behind live");

            // The once-per-second tick re-renders the countdown and never moves a card.
            var before = contracts.OfferCard(Gear).worldBound.y;
            Advance(session, 1_000);
            yield return SyncContracts();
            Advance(session, 1_000);
            yield return SyncContracts();
            Assert.That(contracts.OfferDisplayOrder, Is.EqualTo(new[] { Gear, TimedBar, Plate, BonusPlate, ProducePlate }), "a tick does not re-sort");
            Assert.That(contracts.OfferCard(Gear).worldBound.y, Is.EqualTo(before).Within(0.5f), "no layout jump on the tick");

            // When the cooldown ends the offer is live again and takes its content place.
            Advance(session, RunShellFixtures.OfferCooldownMs(Plate));
            yield return SyncContracts();
            Assert.That(contracts.OfferDisplayOrder, Is.EqualTo(new[] { Gear, Plate, TimedBar, BonusPlate, ProducePlate }));
        }

        [UnityTest]
        public IEnumerator CoolingDownUsesTheNeutralIconAndNeverTheWarningIcon([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return OpenContracts(screen, 100, 1, Gear, Plate, TimedBar);
            Submit(contracts.AcceptButton(Plate));
            AddStock(session, "iron.plate", RunShellFixtures.OfferQuantity(Plate));
            yield return SyncContracts();
            Submit(contracts.DeliverButton);
            yield return SyncContracts();

            var state = contracts.OfferCooldown(Plate);
            Assert.That(state.text, Does.StartWith("Available in "));
            AssertIconBesideText(state, PanelIcons.Info, screen + " cooldown state");
            Assert.That(Shown(contracts.OfferReason(Plate)), Is.False, "the state label is the only line: no duplicate reason, no warning icon");
            Assert.That(Shown(contracts.OfferFeedback(Plate)), Is.False, "no rejection happened");

            // A rejected tap is the only place the warning icon appears.
            Submit(contracts.AcceptButton(Plate));
            yield return ShellRig.Settle();
            AssertIconBesideText(contracts.OfferFeedback(Plate), PanelIcons.Warning, screen + " rejection after a tap");
        }

        // ---------------------------------------------------------------------------------------------------
        // R-07: one state label
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ABonusOfferKeepsOneStateLabelWhileAnotherContractIsActive()
        {
            yield return OpenContracts(Phone, 100, 2, Gear, BonusPlate, TimedBar);
            Advance(session, 1_000);
            yield return SyncContracts();
            var line = contracts.OfferCooldown(BonusPlate);
            Assert.That(line.text, Does.StartWith("Bonus, ends in "));
            Assert.That(Shown(contracts.OfferReason(BonusPlate)), Is.False, "nothing blocks it yet");

            // Another contract is accepted: the bonus label is unchanged and the block is its own, separate line.
            Submit(contracts.AcceptButton(TimedBar));
            yield return ShellRig.Settle();
            Assert.That(session.State.Contracts.Active?.OfferId, Is.EqualTo(TimedBar));
            Assert.That(line.text, Does.StartWith("Bonus, ends in "), "the label does not flip to 'used'");
            Assert.That(contracts.OfferReason(BonusPlate).text, Is.EqualTo(RunRejectionCopy.For(RunReasons.ContractActiveExists)));
            Assert.That(Shown(contracts.OfferReason(BonusPlate)), Is.True);

            // Spend the bonus: afterwards it reads 'used' with and without an active contract.
            AddStock(session, "iron.bar", RunShellFixtures.OfferQuantity(TimedBar));
            yield return SyncContracts();
            Submit(contracts.DeliverButton);
            yield return SyncContracts();
            Submit(contracts.AcceptButton(BonusPlate));
            AddStock(session, "iron.plate", RunShellFixtures.OfferQuantity(BonusPlate));
            yield return SyncContracts();
            Submit(contracts.DeliverButton);
            yield return SyncContracts();
            Assert.That(session.State.Contracts.Active, Is.Null);
            Assert.That(line.text, Is.EqualTo("Bonus used"));
            AssertIconBesideText(line, PanelIcons.Info, "used state");

            Submit(contracts.AcceptButton(Gear));
            yield return ShellRig.Settle();
            Assert.That(session.State.Contracts.Active?.OfferId, Is.EqualTo(Gear));
            Assert.That(line.text, Is.EqualTo("Bonus used"), "still 'used' while another contract is active");
            Assert.That(Shown(contracts.OfferReason(BonusPlate)), Is.False, "the blocked-by-active line is not repeated on a used offer");
            Assert.That(contracts.OfferDisplayOrder.Last(), Is.EqualTo(BonusPlate).Or.EqualTo(ProducePlate), "used sorts with the locked ones");
        }

        // ---------------------------------------------------------------------------------------------------
        // R-08 / Q5: titles and the Item note
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator StandingBonusAndProductionOffersHaveDistinctTitles([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return OpenContracts(screen, 100, 2, Gear, Plate, TimedBar, BonusPlate, ProducePlate);
            var plate = ResourceLabel("iron.plate");
            Assert.That(contracts.OfferTitleLabel(Plate).text, Is.EqualTo("Plate contract"));
            Assert.That(contracts.OfferTitleLabel(BonusPlate).text, Is.EqualTo("Bonus: Plate contract"));
            Assert.That(contracts.OfferTitleLabel(ProducePlate).text, Is.EqualTo("Produce " + plate));
            Assert.That(contracts.OfferTitleLabel(Gear).text, Is.EqualTo("Gear contract"));
            var titles = new[] { Plate, BonusPlate, ProducePlate }.Select(id => contracts.OfferTitleLabel(id).text).ToList();
            Assert.That(titles.Distinct().Count(), Is.EqualTo(3), "the three Plate offers no longer share a title");

            Submit(contracts.AcceptButton(BonusPlate));
            yield return ShellRig.Settle();
            Assert.That(session.State.Contracts.Active?.OfferId, Is.EqualTo(BonusPlate).Or.Null);
            if (session.State.Contracts.Active != null)
            {
                Assert.That(contracts.ActiveCard.Q<Label>("contracts-active-title").text, Is.EqualTo("Bonus: Plate contract"), "the active card uses the same title");
            }
        }

        [UnityTest]
        public IEnumerator AWithheldItemRewardSaysTheLimitWasReachedAndNothingElseDoes()
        {
            // The shipped cap equals the ladder size, so nothing is withheld; the fixture lowers the cap in the content text to 3.
            var text = System.Text.Encoding.UTF8.GetString(RunSceneRun.IronBytes());
            Assert.That(text, Does.Contain("\"perRunCap\": 10"));
            var lowered = System.Text.Encoding.UTF8.GetBytes(text.Replace("\"perRunCap\": 10", "\"perRunCap\": 3"));
            rig = ShellRig.Create(Phone, null, 100);
            rigs.Add(rig);
            yield return rig.Layout();
            session = RunShellFixtures.NewSession(out commands, factoryLevel: 1, cash: 25, unlockEverything: true, content: lowered);
            UnlockContracts(session, Gear, Plate, TimedBar);
            contracts = new ContractsPanel(session, commands, Styles("ContractsPanel.uss"), Icon);
            rig.Shell.Panels.Register(contracts);
            rig.Shell.SelectTab(ShellTab.Contracts);
            yield return ShellRig.Settle();
            Assert.That(Shown(contracts.OfferItemNote(Plate)), Is.False, "no Item is due at the first completion index");

            var ladder = session.Catalog.ContractItemLadder;
            Assert.That(ladder.PerRunCap, Is.EqualTo(3));
            Assert.That(ladder.Entries.Count, Is.GreaterThan(ladder.PerRunCap), "fixture: the ladder is wider than its cap");
            var capped = ladder.Entries.OrderBy(entry => entry.Index).ThenBy(entry => entry.ItemId, StringComparer.Ordinal).ElementAt((int)ladder.PerRunCap);
            Assert.That(session.State.Contracts.TryRestoreRunCompletionTotal(capped.Index), Is.EqualTo(IdleFactory.Run.Domain.RunStateResult.Ok));
            yield return SyncContracts();
            Assert.That(Shown(contracts.OfferItemNote(Plate)), Is.True);
            Assert.That(contracts.OfferItemNote(Plate).text, Is.EqualTo("Item reward limit reached this run"));
            Assert.That(contracts.OfferItemNote(Plate).text, Is.EqualTo(ContractsPanel.ItemLimitText));
        }

        // ---------------------------------------------------------------------------------------------------
        // R-10: grouped lock text
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void ConditionsOfOneKindAreGroupedAndMixedKindsJoinWithOr()
        {
            var catalog = RunShellFixtures.ShippedCatalog;
            var plate = catalog.Resources["iron.plate"].Label;
            var gear = catalog.Resources["iron.gear"].Label;
            var bar = catalog.Resources["iron.bar"].Label;
            var produced = new[] { "iron.plate", "iron.gear", "iron.bar" }.Select(id => new UnlockCondition("resourceProduced", id)).ToList();
            Assert.That(UnlockCopy.AfterAny(catalog, produced), Is.EqualTo("you produce " + plate + ", " + gear + " or " + bar));
            Assert.That(UnlockCopy.AfterAny(catalog, produced.Take(2)), Is.EqualTo("you produce " + plate + " or " + gear));
            Assert.That(UnlockCopy.AfterAny(catalog, produced.Take(1).Concat(produced.Take(1))), Is.EqualTo("you produce " + plate), "duplicates collapse");

            var mixed = produced.Take(2).Concat(new[] { new UnlockCondition("factoryLevelAtLeast", null, 3) }).ToList();
            Assert.That(UnlockCopy.AfterAny(catalog, mixed), Is.EqualTo("you produce " + plate + " or " + gear + " or the factory reaches level 3"));
        }

        [UnityTest]
        public IEnumerator TheLockedContractsTextGroupsTheConditionsOfOneKind([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return OpenContracts(screen, 100, 1);
            Assert.That(Shown(contracts.EmptyLabel), Is.True);
            var conditions = contracts.Snapshot.Offers.Select(offer => offer.Unlock).ToList();
            Assert.That(contracts.EmptyLabel.text, Is.EqualTo("Contracts unlock after " + UnlockCopy.AfterAny(session.Catalog, conditions) + "."));
            Assert.That(contracts.EmptyLabel.text, Does.Not.Contain(" or you produce"), "one 'you produce' for all produced resources");
            Assert.That(contracts.EmptyLabel.text, Does.Contain(", "), "the labels of one kind are comma separated");
        }

        // ---------------------------------------------------------------------------------------------------
        // R-01: Industry pointer and Research tab dot
        // ---------------------------------------------------------------------------------------------------

        private IEnumerator OpenPrestige(ShellScreen screen, int percent, long cash)
        {
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            session = RunShellFixtures.NewSession(out commands, factoryLevel: 2, cash: cash, unlockEverything: true);
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelRoot + "PrestigePanel.uxml");
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(PanelRoot + "PrestigePanel.uss");
            prestige = new PrestigePresenter(session, commands, tree, styles, Icon);
            industry = new IndustryPanel(session, commands, ResearchPanelTestSupport.Styles("IndustryPanel.uss"), Icon);
            research = new ResearchPanel(session, commands, ResearchPanelTestSupport.Styles("ResearchPanel.uss"), Icon, industry);
            rig.Shell.Panels.Register(prestige);
            rig.Shell.Panels.Register(research);
            rig.Shell.SelectTab(ShellTab.Prestige);
            yield return ShellRig.Settle();
        }

        [UnityTest]
        public IEnumerator ThePrestigePanelOffersToSpendUnspentIpAndTheButtonOpensResearchAtIndustry([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return OpenPrestige(screen, 100, 25);
            Assert.That(Shown(prestige.IndustryCard), Is.False, "no IP, no pointer");

            ResearchPanelTestSupport.GrantIp(session, 25);
            prestige.Invalidate();
            prestige.Refresh();
            yield return ShellRig.Settle();
            Assert.That(Shown(prestige.IndustryCard), Is.True);
            Assert.That(prestige.IndustryLineLabel.text, Does.Contain("25 IP to spend"));
            Assert.That(prestige.IndustryButton.text, Is.EqualTo("Spend 25 IP in Industry"));
            Assert.That(prestige.IndustryButton.worldBound.height, Is.GreaterThanOrEqualTo(43.5f), "44 pt target");

            Submit(prestige.IndustryButton);
            yield return ShellRig.Settle();
            yield return new WaitForSeconds(0.4f);
            yield return ShellRig.Settle();

            Assert.That(rig.Shell.ActiveTab, Is.EqualTo(ShellTab.Research));
            Assert.That(rig.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Research));
            var viewport = rig.Shell.PanelScroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport").worldBound;
            var top = industry.Root.worldBound;
            Assert.That(top.y, Is.GreaterThanOrEqualTo(viewport.y - 1f), "the Industry section starts in the sheet, not above it");
            Assert.That(top.y, Is.LessThan(viewport.yMax), "and it is on screen");
            var focused = Focused(rig.Root);
            Assert.That(focused, Is.Not.Null, "focus did not stay on the hidden Prestige button");
            Assert.That(focused, Is.SameAs(rig.Shell.TabButton(ShellTab.Research)), "focus moved to the Research tab, a control that stays visible");
        }

        [UnityTest]
        public IEnumerator AfterASaleTheBannerAndTheButtonAppearAndTheBannerLeavesWithThePanel()
        {
            var cash = RunShellFixtures.ShippedCatalog.Prestige.Threshold?.Base ?? RunShellFixtures.ShippedCatalog.Prestige.ThresholdFactoryValue;
            yield return OpenPrestige(Phone, 100, cash + cash / 4 + 345_678);
            var award = commands.GetPrestigePreview().Value.IpAward;
            Submit(prestige.ConfirmButton);
            yield return ShellRig.Settle();
            Submit(rig.Shell.DialogConfirmButton);
            for (var i = 0; i < 300 && prestige.State == PrestigePanelState.Saving; i++)
            {
                yield return null;
            }

            yield return ShellRig.Settle();
            Assert.That(session.Permanent.Ip, Is.EqualTo(award));
            Assert.That(Shown(prestige.IndustryCard), Is.True);
            Assert.That(Shown(prestige.SaleBannerLabel), Is.True);
            Assert.That(prestige.SaleBannerLabel.text, Is.EqualTo("Factory Sale complete: +" + QuantityFormatter.Format(award) + " IP."));
            Assert.That(prestige.IndustryButton.text, Is.EqualTo("Spend " + QuantityFormatter.Format(award) + " IP in Industry"));

            rig.Shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
            rig.Shell.SelectTab(ShellTab.Prestige);
            yield return ShellRig.Settle();
            Assert.That(Shown(prestige.SaleBannerLabel), Is.False, "the banner belongs to the moment after the sale");
            Assert.That(Shown(prestige.IndustryCard), Is.True, "the pointer stays while IP is unspent");
        }

        [UnityTest]
        public IEnumerator TheResearchTabShowsADotAndAnAccessibleNameWhileIpIsUnspent([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            rig = ShellRig.Create(screen, null, 100);
            rigs.Add(rig);
            yield return rig.Layout();
            var shell = rig.Shell;
            var tab = shell.TabButton(ShellTab.Research);
            Assert.That(Shown(shell.ResearchBadge), Is.False, "no IP, no dot");
            Assert.That(tab.tooltip, Is.Not.StartWith("Research, "));

            shell.SetIndustryPoints(25);
            yield return ShellRig.Settle();
            Assert.That(Shown(shell.ResearchBadge), Is.True);
            Assert.That(shell.ResearchBadge.text, Is.EqualTo(RunShellDocument.DotGlyph));
            Assert.That(tab.tooltip, Is.EqualTo("Research, 25 IP to spend in Industry"));
            Assert.That(shell.IndustryPoints, Is.EqualTo(25));
            Assert.That(tab.worldBound.height, Is.GreaterThanOrEqualTo(43.5f), "the dot does not change the 44 pt tab");

            shell.SetIndustryPoints(0);
            yield return ShellRig.Settle();
            Assert.That(Shown(shell.ResearchBadge), Is.False, "spending the IP clears the dot");
            Assert.That(tab.tooltip, Is.EqualTo("Research"));
        }

        // ---------------------------------------------------------------------------------------------------
        // R-24: Industry next-level copy
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ADecayingUpgradeNamesTheGainOfItsNextLevelAndNotPerLevel()
        {
            rig = ShellRig.Create(Phone, null, 100);
            rigs.Add(rig);
            yield return rig.Layout();
            session = ResearchPanelTestSupport.NewSession(out commands);
            industry = new IndustryPanel(session, commands, ResearchPanelTestSupport.Styles("IndustryPanel.uss"), Icon);
            rig.Shell.Panels.Register(industry);
            rig.Shell.SelectTab(ShellTab.Research);
            yield return ShellRig.Settle();

            var decayed = session.Catalog.IndustryUpgrades.Values.Where(def => def.IsDecayed && def.MaxLevel > 1).ToList();
            Assert.That(decayed, Is.Not.Empty, "fixture: the shipped Industry upgrades decay");
            foreach (var def in decayed)
            {
                var effect = def.Effect;
                var first = ResearchEffectCopy.Describe(session.Catalog, effect.Kind, effect.TargetKind, effect.TargetId, def.ValueTable[0], false);
                Assert.That(industry.EffectLabel(def.Id).text, Is.EqualTo("Next level: " + first), def.Id);
                Assert.That(industry.EffectLabel(def.Id).text, Does.Not.Contain("per level"), def.Id);
            }

            var sample = decayed[0];
            ResearchPanelTestSupport.SetIndustryLevel(session, sample.Id, 1);
            industry.Invalidate();
            industry.Refresh();
            yield return ShellRig.Settle();
            var second = ResearchEffectCopy.Describe(session.Catalog, sample.Effect.Kind, sample.Effect.TargetKind, sample.Effect.TargetId, sample.ValueTable[1], false);
            Assert.That(industry.EffectLabel(sample.Id).text, Is.EqualTo("Next level: " + second), "the next level has its own, smaller gain");

            ResearchPanelTestSupport.SetIndustryLevel(session, sample.Id, sample.MaxLevel);
            industry.Invalidate();
            industry.Refresh();
            yield return ShellRig.Settle();
            var last = ResearchEffectCopy.Describe(session.Catalog, sample.Effect.Kind, sample.Effect.TargetKind, sample.Effect.TargetId, sample.ValueTable[sample.ValueTable.Count - 1], false);
            Assert.That(industry.EffectLabel(sample.Id).text, Is.EqualTo("Last level: " + last));
        }

        // ---------------------------------------------------------------------------------------------------
        // R-25 / R-27: Research
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ARejectedResearchPurchaseShowsOneMessageAndOneIcon([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            rig = ShellRig.Create(screen, null, 100);
            rigs.Add(rig);
            yield return rig.Layout();
            session = ResearchPanelTestSupport.NewSession(out commands);
            research = new ResearchPanel(session, commands, ResearchPanelTestSupport.Styles("ResearchPanel.uss"), Icon);
            rig.Shell.Panels.Register(research);
            rig.Shell.SelectTab(ShellTab.Research);
            yield return ShellRig.Settle();

            var node = research.NodeButton(ResearchPanelTestSupport.Bargaining);
            Submit(node);
            yield return ShellRig.Settle();
            Assert.That(session.State.ResearchLevel(ResearchPanelTestSupport.Bargaining), Is.Zero, "no RP: nothing was bought");
            Assert.That(Shown(research.DetailReason), Is.True, "the standing reason is on the card");
            AssertIconBesideText(research.DetailReason, PanelIcons.Warning, screen + " standing reason");
            Assert.That(Shown(research.Feedback), Is.False, "the same message is not repeated with a second icon");

            var visibleWarnings = research.Detail.Query<Label>().ToList().Count(label => label != research.DetailEffect && Shown(label) && PanelIcons.HasIcon(label)); // T441: the effect line carries its own effect icon, not a message icon
            Assert.That(visibleWarnings, Is.EqualTo(1), "one message, one icon");
        }

        [Test]
        public void NoStyleSheetUsesAPseudoClassUnityDoesNotSupport()
        {
            var directory = Path.Combine(UnityEngine.Application.dataPath, "IdleFactory", "Unity", "UI");
            var sheets = Directory.GetFiles(directory, "*.uss", SearchOption.AllDirectories);
            Assert.That(sheets, Is.Not.Empty);
            foreach (var sheet in sheets)
            {
                var text = File.ReadAllText(sheet);
                Assert.That(text, Does.Not.Contain(":last-child"), sheet);
                Assert.That(text, Does.Not.Contain(":first-child"), sheet);
                Assert.That(text, Does.Not.Contain(":nth-child"), sheet);
            }
        }
    }
}
#endif
