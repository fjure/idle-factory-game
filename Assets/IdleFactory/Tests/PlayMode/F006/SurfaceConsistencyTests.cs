#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T536 (Feature 006, FR-015, FR-023, FR-024, SC-009, research R11, contracts/ui-surfaces-f006.md section 3): every panel the shell hosts is a
    /// scaffold surface. The Market (inventory, priorities, expansion), Contracts, Items, Research, Industry, Prestige, Settings and the hub carry the
    /// scaffold scope class; each of their buttons is a scaffold button of at least 48 pt; a soft-disabled button takes the scaffold's one disabled
    /// look (shape, text and contrast); the empty templates of Items and Contracts are the scaffold's; the dialog and the notice line are scaffold
    /// surfaces; and the Offline summary and the start-up status carry the scope and the button class. Together with <c>PanelScaffoldTests</c> (the
    /// scaffold itself) and <c>HubPanelTests</c> (the hub's states) this is the "scaffold classes and templates on every panel" check.
    /// </summary>
    public sealed class SurfaceConsistencyTests
    {
        private const float Target = 48f;
        private const float Tolerance = 0.51f;
        private const string Scope = "scaffold-scope";

        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private readonly List<OfflineSummaryPanelTestSupport.StandaloneRig> standaloneRigs = new List<OfflineSummaryPanelTestSupport.StandaloneRig>();
        private readonly List<IDisposable> disposables = new List<IDisposable>();
        private string settingsRoot;
        private string statusRoot;

        public static IEnumerable<ShellScreen> Screens()
        {
            var all = ShellScreens.All().ToList();
            yield return all.First(screen => screen.Name.StartsWith("phone-portrait 360", StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("phone-landscape 640", StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("desktop 1280", StringComparison.Ordinal));
        }

        public static IEnumerable<int> Percents() => new[] { 100, 200 };

        public static IEnumerable<string> PanelIds() => new[]
        {
            ShellPanelIds.Market, ShellPanelIds.Contracts, ShellPanelIds.Items, ShellPanelIds.Research, ShellPanelIds.Industry, ShellPanelIds.Prestige,
            ShellPanelIds.Settings,
        };

        [TearDown]
        public void TearDown()
        {
            foreach (var item in disposables)
            {
                item.Dispose();
            }

            foreach (var item in rigs)
            {
                item.Dispose();
            }

            foreach (var item in standaloneRigs)
            {
                item.Dispose();
            }

            disposables.Clear();
            rigs.Clear();
            standaloneRigs.Clear();
            if (settingsRoot != null)
            {
                SettingsTestHarness.DeleteRoot(settingsRoot);
                settingsRoot = null;
            }

            if (statusRoot != null)
            {
                RunSceneRun.DeleteRoot(statusRoot);
                statusRoot = null;
            }
        }

        private static VisualTreeAsset Tree(string path) => HubHarness.Load<VisualTreeAsset>(path);

        private static StyleSheet Styles(string path) => HubHarness.Load<StyleSheet>(path);

        private static Sprite Icon(string key) => ContractsPanelTestSupport.Icon(key);

        private IEnumerator OpenRig(ShellScreen screen, int percent, Action<ShellRig> result)
        {
            var rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            rig.Document.panelSettings.clearColor = true;
            rig.Document.panelSettings.colorClearValue = Color.black;
            yield return rig.Layout();
            result(rig);
        }

        /// <summary>Builds the panel of a registry id exactly as the bootstrap does (authored assets, shell services), registers it and opens it.</summary>
        private IEnumerator OpenPanel(string id, ShellRig rig, RunSession session, RunCommands commands, Action<IShellPanel> result)
        {
            IShellPanel panel;
            switch (id)
            {
                case ShellPanelIds.Market:
                    panel = new MarketPanelHost(session, commands, rig.Shell, Tree("Panels/InventoryRatesPanel.uxml"), Styles("Panels/InventoryRatesPanel.uss"),
                        Tree("Panels/PrioritiesPanel.uxml"), Styles("Panels/PrioritiesPanel.uss"), Tree("Panels/ExpansionPanel.uxml"), Styles("Panels/ExpansionPanel.uss"), Icon);
                    break;
                case ShellPanelIds.Contracts:
                    panel = new ContractsPanel(session, commands, Styles("Panels/ContractsPanel.uss"), Icon);
                    break;
                case ShellPanelIds.Items:
                    panel = new ItemsPanel(session, commands, Styles("Panels/ItemsPanel.uss"), Icon);
                    break;
                case ShellPanelIds.Research:
                    panel = new ResearchPanel(session, commands, Styles("Panels/ResearchPanel.uss"), Icon, null);
                    break;
                case ShellPanelIds.Industry:
                    panel = new IndustryPanel(session, commands, Styles("Panels/IndustryPanel.uss"), Icon, ShellPanelIds.Industry);
                    break;
                case ShellPanelIds.Prestige:
                    panel = new PrestigePresenter(session, commands, Tree("Panels/PrestigePanel.uxml"), Styles("Panels/PrestigePanel.uss"), Icon);
                    break;
                default:
                    settingsRoot = SettingsTestHarness.NewRoot();
                    var harness = new SettingsTestHarness(settingsRoot, false, true);
                    panel = new SettingsPanelPresenter(harness.Store, harness.Refresh, () => harness.Effective, () => harness.SystemAvailable,
                        Tree("Panels/Settings/SettingsPanel.uxml"), Styles("Panels/Settings/SettingsPanel.uss"), Icon);
                    break;
            }

            disposables.Add(rig.Shell.Panels.Register(panel));
            switch (id)
            {
                case ShellPanelIds.Market: rig.Shell.SelectTab(ShellTab.Market); break;
                case ShellPanelIds.Contracts: rig.Shell.SelectTab(ShellTab.Contracts); break;
                case ShellPanelIds.Items: rig.Shell.SelectTab(ShellTab.Items); break;
                case ShellPanelIds.Research: rig.Shell.SelectTab(ShellTab.Research); break;
                case ShellPanelIds.Prestige: rig.Shell.SelectTab(ShellTab.Prestige); break;
                case ShellPanelIds.Industry: rig.Shell.OpenIndustry(); break;
                default: rig.Shell.OpenSettings(); break;
            }

            yield return ShellRig.Settle();
            Assert.That(rig.Shell.ActivePanelId, Is.EqualTo(id));
            result(panel);
        }

        private static RunSession StockedSession(out RunCommands commands, bool unlockContracts)
        {
            var session = RunShellFixtures.NewSession(out commands, cash: 25, unlockEverything: true);
            if (unlockContracts)
            {
                ContractsPanelTestSupport.UnlockContracts(session);
            }

            ContractsPanelTestSupport.AddItems(session, session.Catalog.Items.Keys.First(), 2);
            return session;
        }

        private static bool InsideScope(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current.ClassListContains(Scope))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Context(ShellRig rig, string what) => $"{rig.Screen.Name} @{rig.TextScalePercent}% {what}";

        // ---------------------------------------------------------------------------------------------------
        // Every hosted panel
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryHostedPanelIsAScaffoldSurface(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent, [ValueSource(nameof(PanelIds))] string id)
        {
            ShellRig rig = null;
            yield return OpenRig(screen, percent, built => rig = built);
            var session = StockedSession(out var commands, true);
            IShellPanel panel = null;
            yield return OpenPanel(id, rig, session, commands, built => panel = built);

            var context = Context(rig, id);
            Assert.That(panel.Root.ClassListContains(Scope), Is.True, context + ": the panel root carries the scaffold scope class");
            Assert.That(panel.Root.parent, Is.SameAs(rig.Shell.PanelContent), context + ": the shell mounts it");

            var controls = panel.Root.Query<Button>().ToList().Where(button => button.ClassListContains("run-control")).ToList();
            Assert.That(controls, Is.Not.Empty, context + ": the panel has buttons");
            foreach (var button in controls)
            {
                Assert.That(button.ClassListContains("scaffold-button"), Is.True, $"{context}: button '{button.name}' is a scaffold button");
                Assert.That(button.ClassListContains("scaffold-button--disabled"), Is.EqualTo(RunShellDocument.IsSoftDisabled(button)),
                    $"{context}: button '{button.name}' disabled look follows its soft-disabled state");
                if (!IsDisplayed(button))
                {
                    continue;
                }

                Assert.That(button.worldBound.width, Is.GreaterThanOrEqualTo(Target - Tolerance), $"{context}: '{button.name}' width {button.worldBound.width}");
                Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(Target - Tolerance), $"{context}: '{button.name}' height {button.worldBound.height}");
                if (RunShellDocument.IsSoftDisabled(button))
                {
                    // The one disabled look reaches the control: the thick left bar and square corners of the scaffold, not the plain run-control fill alone.
                    Assert.That(button.resolvedStyle.borderLeftWidth, Is.EqualTo(6f).Within(0.01f), $"{context}: disabled '{button.name}' has the scaffold bar");
                    Assert.That(button.resolvedStyle.borderTopLeftRadius, Is.EqualTo(0f).Within(0.01f), $"{context}: disabled '{button.name}' has square corners");
                }
            }
        }

        [UnityTest]
        public IEnumerator ADisabledControlTakesTheScaffoldLookAndAnEnabledOneDropsIt()
        {
            ShellRig rig = null;
            yield return OpenRig(Screens().First(), 100, built => rig = built);
            var session = StockedSession(out var commands, true);
            IShellPanel panel = null;
            yield return OpenPanel(ShellPanelIds.Contracts, rig, session, commands, built => panel = built);
            var contracts = (ContractsPanel)panel;
            var accept = contracts.AcceptButton(contracts.OfferIds.First());
            RunShellDocument.SetSoftDisabled(accept, true);
            yield return ShellRig.Settle();
            Assert.That(accept.ClassListContains("scaffold-button--disabled"), Is.True);
            Assert.That(accept.resolvedStyle.borderLeftWidth, Is.EqualTo(6f).Within(0.01f));
            RunShellDocument.SetSoftDisabled(accept, false);
            yield return ShellRig.Settle();
            Assert.That(accept.ClassListContains("scaffold-button--disabled"), Is.False);
            Assert.That(accept.resolvedStyle.borderLeftWidth, Is.Not.EqualTo(6f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator TheMarketCarriesItsThreeSectionsInsideOneScaffoldSurface([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            ShellRig rig = null;
            yield return OpenRig(screen, 100, built => rig = built);
            var session = StockedSession(out var commands, true);
            IShellPanel panel = null;
            yield return OpenPanel(ShellPanelIds.Market, rig, session, commands, built => panel = built);
            var host = (MarketPanelHost)panel;
            var sections = new Dictionary<string, VisualElement>
            {
                { "inventory", host.Inventory.Root }, { "priorities", host.Priorities.Root }, { "expansion", host.Expansion.Root },
                { "session log", host.SessionLog.Root },
            };
            foreach (var pair in sections)
            {
                Assert.That(pair.Value.GetFirstAncestorOfType<VisualElement>() != null && InsideScope(pair.Value), Is.True, pair.Key + " sits inside the scope");
                var controls = pair.Value.Query<Button>().ToList().Where(button => button.ClassListContains("run-control")).ToList();
                Assert.That(controls.All(button => button.ClassListContains("scaffold-button")), Is.True, pair.Key + ": scaffold buttons");
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Templates
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ItemsAndContractsShowTheScaffoldEmptyTemplate([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            ShellRig rig = null;
            yield return OpenRig(screen, percent, built => rig = built);
            var session = RunShellFixtures.NewSession(out var commands, cash: 25, unlockEverything: true);
            IShellPanel items = null;
            yield return OpenPanel(ShellPanelIds.Items, rig, session, commands, built => items = built);
            AssertEmptyTemplate(rig, items.Root.Q<VisualElement>("items-empty-box"), "items", ((ItemsPanel)items).EmptyLabel);

            IShellPanel contracts = null;
            yield return OpenPanel(ShellPanelIds.Contracts, rig, session, commands, built => contracts = built);
            AssertEmptyTemplate(rig, contracts.Root.Q<VisualElement>("contracts-empty-box"), "contracts", ((ContractsPanel)contracts).EmptyLabel);
        }

        private static void AssertEmptyTemplate(ShellRig rig, VisualElement box, string prefix, Label message)
        {
            var context = Context(rig, prefix + " empty");
            Assert.That(box, Is.Not.Null, context);
            Assert.That(IsDisplayed(box), Is.True, context + ": shown");
            Assert.That(box.ClassListContains("scaffold__state"), Is.True, context);
            Assert.That(box.ClassListContains("scaffold__state--empty"), Is.True, context);
            Assert.That(box.ClassListContains("contracts-card") || box.ClassListContains("items-card"), Is.False, context + ": no panel-specific card look");
            var mark = box.Q<Label>(prefix + "-empty-mark");
            var title = box.Q<Label>(prefix + "-empty-title");
            Assert.That(mark?.ClassListContains("scaffold__mark"), Is.True, context + ": marker (a shape, not colour alone)");
            Assert.That(title?.text, Is.EqualTo(ScaffoldEmpty.TitleText), context + ": title");
            Assert.That(message.ClassListContains("scaffold__state-message"), Is.True, context + ": message");
            Assert.That(string.IsNullOrEmpty(message.text), Is.False, context + ": says what is missing");
            Assert.That(box.resolvedStyle.borderLeftWidth, Is.EqualTo(6f).Within(0.01f), context + ": the template's left bar reaches the box");
            // The panel scrolls vertically, so only the horizontal extent must stay inside the safe area.
            Assert.That(box.worldBound.xMin, Is.GreaterThanOrEqualTo(rig.SafeRect().xMin - Tolerance), context + ": inside the safe area (left)");
            Assert.That(box.worldBound.xMax, Is.LessThanOrEqualTo(rig.SafeRect().xMax + Tolerance), context + ": inside the safe area (right)");
        }

        [UnityTest]
        public IEnumerator TheHubKeepsTheScaffoldScopeAndItsStates([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            HubHarness harness = null;
            yield return HubHarness.Create(screen, 100, built => harness = built);
            disposables.Add(harness);
            harness.Shell.OpenHub();
            yield return ShellRig.Settle();
            Assert.That(harness.Hub.Root.ClassListContains("scaffold"), Is.True);
            Assert.That(harness.Hub.Root.ClassListContains(Scope), Is.True, "the shell marks the hub like every panel");
            Assert.That(harness.Hub.StateKind, Is.Null, "the cards show");
            harness.Hub.FailureProbe = () => true;
            harness.Shell.HandleBack();
            harness.Shell.OpenHub();
            yield return ShellRig.Settle();
            Assert.That(harness.Hub.StateKind, Is.EqualTo("error"));
            Assert.That(harness.Hub.Root.Q<VisualElement>("hub-state").ClassListContains("scaffold__state--error"), Is.True);
            Assert.That(harness.Hub.RetryButton.ClassListContains("scaffold-button"), Is.True);
        }

        [Test]
        public void TheHubAttentionFeedAllocatesNothingOnAFrameWhereTheRunDidNotChange()
        {
            var session = RunShellFixtures.NewSession(out var commands, cash: 25, unlockEverything: true);
            var attention = new HubAttention();
            var feed = new HubAttentionFeed(session, commands, attention);
            feed.Update(0f);
            var allocations = GcAllocationCounter.Count(() =>
            {
                for (var frame = 0; frame < 120; frame++)
                {
                    feed.Update(0.016f * frame);
                }
            });
            Assert.That(allocations, Is.EqualTo(0), "an unchanged run costs a revision compare per frame, nothing else");
        }

        // ---------------------------------------------------------------------------------------------------
        // Dialog, notice, Offline summary, start-up status
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheConfirmationDialogAndTheNoticeLineAreScaffoldSurfaces([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            ShellRig rig = null;
            yield return OpenRig(screen, percent, built => rig = built);
            var shell = rig.Shell;
            shell.ShowNotice("Saved on this device only.", ShellNoticeKind.Warning);
            yield return ShellRig.Settle();
            Assert.That(shell.NoticeBar.ClassListContains(Scope), Is.True, "the notice line is a scaffold surface");

            shell.RequestConfirmation(new ConfirmationRequest { Title = "Remove machine", Message = "This removes the machine and keeps no refund.", Destructive = true });
            yield return ShellRig.Settle();
            var card = shell.DialogCard;
            Assert.That(card.ClassListContains(Scope), Is.True, "the dialog is a scaffold surface");
            foreach (var button in new[] { shell.DialogCancelButton, shell.DialogConfirmButton })
            {
                Assert.That(button.ClassListContains("scaffold-button"), Is.True, button.name);
                Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(Target - Tolerance), button.name + " height " + button.worldBound.height);
                Assert.That(button.worldBound.width, Is.GreaterThanOrEqualTo(Target - Tolerance), button.name + " width " + button.worldBound.width);
            }

            Assert.That(shell.DialogConfirmButton.ClassListContains("run-button--destructive"), Is.True, "the destructive look of the confirm button is kept");
        }

        [UnityTest]
        public IEnumerator TheOfflineSummaryAndTheStartupStatusAreScaffoldSurfaces([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            ShellRig rig = null;
            yield return OpenRig(screen, percent, built => rig = built);
            var catalog = RunShellFixtures.NewSession(out _, unlockEverything: true).Catalog;
            var offline = new OfflineSummaryPanel(catalog, null, Styles("Panels/OfflineSummaryPanel.uss"));
            offline.Mount(rig.Root, rig.Shell);
            disposables.Add(new Unmounter(offline));
            offline.ShowSummary(OfflineSummaryPanelTestSupport.NormalReturn());
            yield return OfflineSummaryPanelTestSupport.Frames();
            AssertStatusSurface(rig, offline, "offline summary");
            Assert.That(offline.ContinueButton.worldBound.height, Is.GreaterThanOrEqualTo(Target - Tolerance), "Continue is a 48 pt target");
            offline.Unmount();

            statusRoot = OfflineSummaryPanelTestSupport.TempRoot();
            var run = StatusPersistenceRig.Create(statusRoot, new StatusTestClock());
            var startup = new StartupStatusPanel(run.Persistence, run.Legacy, Styles("Panels/StartupStatusPanel.uss"));
            startup.Mount(rig.Root, rig.Shell);
            disposables.Add(new Unmounter(startup));
            startup.ShowImporting();
            yield return OfflineSummaryPanelTestSupport.Frames();
            AssertStatusSurface(rig, startup, "start-up status");
        }

        // ---------------------------------------------------------------------------------------------------
        // Notices leave the primary action and Back usable (P2-F19)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ThreeNoticesLeaveBackAndThePrimaryActionUsable([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            ShellRig rig = null;
            yield return OpenRig(screen, percent, built => rig = built);
            var tree = HubHarness.Load<VisualTreeAsset>("PanelScaffold.uxml");
            var styles = HubHarness.Load<StyleSheet>("PanelScaffold.uss");
            var backs = 0;
            var retries = 0;
            var scaffold = new PanelScaffold(tree, styles, "Contracts", () => backs++);
            var host = new VisualElement { name = "scaffold-notice-host" };
            host.style.position = Position.Absolute;
            host.style.left = screen.Left;
            host.style.top = screen.Top;
            host.style.right = screen.Right;
            host.style.bottom = screen.Bottom;
            host.Add(scaffold.Root);
            rig.Root.Add(host);
            scaffold.ShowError("The saved factory could not be read.", () => retries++);
            scaffold.ShowNotice("Saved on this device only.", ScaffoldNoticeKind.Warning);
            scaffold.ShowNotice("Welcome back.");
            scaffold.ShowNotice("Cancelled by rotation.");
            yield return ShellRig.Settle();

            var context = Context(rig, "three notices");
            Assert.That(scaffold.Notices.Count, Is.EqualTo(PanelScaffold.MaxNotices));
            var stage = scaffold.Root.Q<VisualElement>("scaffold-stage").worldBound;
            var layer = scaffold.NoticeLayer.worldBound;
            Assert.That(layer.height, Is.LessThanOrEqualTo(stage.height * 0.7f + Tolerance), context + ": the notices take at most 70 % of the stage");
            Assert.That(Intersects(layer, scaffold.StateScroll.worldBound), Is.False, context + ": the notices never cover the state card");
            var picked = rig.Root.panel.Pick(scaffold.BackButton.worldBound.center);
            Assert.That(picked == scaffold.BackButton || scaffold.BackButton.Contains(picked), Is.True, context + ": Back is on top");
            Assert.That(scaffold.StateScroll.worldBound.height, Is.GreaterThanOrEqualTo(Target - Tolerance), context + ": room is left for the primary action");
            foreach (var notice in scaffold.Notices)
            {
                var dismiss = notice.Q<Button>("scaffold-notice-dismiss");
                Assert.That(dismiss.worldBound.height, Is.GreaterThanOrEqualTo(Target - Tolerance), context + ": a notice can be dismissed");
                AssertWithin(stage, dismiss.worldBound, context + ": the dismiss button stays in the stage");
            }

            // The primary action is reachable: scrolled into the state card's viewport it takes a click.
            scaffold.StateScroll.ScrollTo(scaffold.StatePrimaryButton);
            yield return ShellRig.Settle();
            var viewport = scaffold.StateScroll.contentViewport.worldBound;
            var button = scaffold.StatePrimaryButton.worldBound;
            Assert.That(button.yMin, Is.GreaterThanOrEqualTo(viewport.yMin - Tolerance), context + ": Retry scrolls into view (top)");
            Assert.That(button.yMax, Is.LessThanOrEqualTo(viewport.yMax + Tolerance), context + ": Retry scrolls into view (bottom)");
            scaffold.ClearNotices();
            host.RemoveFromHierarchy();
            Assert.That(backs + retries, Is.EqualTo(0));
        }

        private static void AssertStatusSurface(ShellRig rig, StatusSurface surface, string what)
        {
            var context = Context(rig, what);
            Assert.That(surface.Root.ClassListContains(Scope), Is.True, context + ": scope class");
            var buttons = surface.Root.Query<Button>().ToList().Where(button => button.ClassListContains("run-control")).ToList();
            Assert.That(buttons, Is.Not.Empty, context);
            Assert.That(buttons.All(button => button.ClassListContains("scaffold-button")), Is.True, context + ": every button is a scaffold button");
        }

        private sealed class Unmounter : IDisposable
        {
            private readonly StatusSurface surface;

            public Unmounter(StatusSurface surface) => this.surface = surface;

            public void Dispose() => surface.Unmount();
        }
    }
}
#endif
