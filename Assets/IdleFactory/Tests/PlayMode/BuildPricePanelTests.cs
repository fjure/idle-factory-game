using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleFactory.Run.Application;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

#if UNITY_EDITOR
using UnityEditor;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T324 (F005 US4, ui-surfaces-f005.md <c>build-price-&lt;machineId&gt;</c>, QA-12 row "Build price"): the build sheet shows the exact
    /// price, "Owned: N" and "Capped"; the UI always passes the price it shows as <c>expectedPrice</c>; a moved price recovers with
    /// "Price changed to $Y" and no charge; the Remove confirmation states the rebuild price. Growing prices come from variant content
    /// (the shipped Iron content is flat), reached through a real run session and its commands, without the scene.
    ///
    /// QA-12 rows covered here: affordable; "Need $X more"; capped (and flat content never capped); <c>build.price.changed</c> recovery;
    /// Remove confirmation with the rebuild price; each at every <c>ShellScreens.All()</c> entry at 100 % and 200 % text for the
    /// worst-case (6-digit price, capped, recovery line) fixture, with touch and pointer exact-value inspection, the accessible name
    /// per state, and focus on the first, middle and last card.
    /// </summary>
    public sealed class BuildPricePanelTests
    {
        private const string PanelRoot = "Assets/IdleFactory/Unity/UI/Panels/";
        private const string Miner = "machine.miner";
        private const float PhoneDpi = 420f;
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;
        private RunSession session;
        private RunCommands commands;
        private BuildSheetPresenter panel;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        public static IEnumerable<int> Percents() => new[] { 100, 200 };

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

        // ---------------------------------------------------------------------------------------------------
        // Fixtures
        // ---------------------------------------------------------------------------------------------------

        /// <summary>The Iron content with the Miner's build price growing: the shipped base x <paramref name="growthPpm"/> per owned Miner, capped at <paramref name="cap"/>.</summary>
        private static RunSession GrowingSession(out RunCommands runCommands, long cash, int factoryLevel, long growthPpm, long cap)
        {
            var text = System.Text.Encoding.UTF8.GetString(RunSceneRun.IronBytes());
            // Rewrite only the growth and the cap of the Miner's build price; the base stays the shipped one (T344: no content value is typed here).
            var pattern = new System.Text.RegularExpressions.Regex(
                "(\"id\":\\s*\"" + System.Text.RegularExpressions.Regex.Escape(Miner) + "\"[\\s\\S]*?\"buildPrice\":\\s*\\{[^}]*?\"ownedGrowthPpm\":\\s*)\\d+([^}]*?\"cap\":\\s*)\\d+");
            Assert.That(pattern.IsMatch(text), Is.True, "the Miner build price is found in the shipped content text");
            text = pattern.Replace(text, match => match.Groups[1].Value + growthPpm + match.Groups[2].Value + cap, 1);

            var loaded = GameplayContentDispatcher.Load(System.Text.Encoding.UTF8.GetBytes(text));
            Assert.That(loaded.IsSuccess, Is.True, "the variant content with a growing Miner price loads");
            var catalog = loaded.RunCatalog;
            var permanent = new PermanentState(catalog);
            var start = NextRunStart.Compute(catalog, permanent);
            var startMs = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            var state = new RunState(catalog, Guid.NewGuid(), 1, null, cash, 0, start.StorageLevel, factoryLevel, catalog.RunStart.InitialUnlocks);
            Assert.That(new RunSimulator(catalog).Advance(state, permanent, 0, startMs, null), Is.EqualTo(RunAdvanceResult.Ok));
            var created = new RunSession(catalog, state, permanent, startMs);
            runCommands = new RunCommands(created);
            return created;
        }

        // The shipped base price of a Miner; every expected price below is derived from it (T344), so a content retune moves the expectations with it.
        private static long Base => RunShellFixtures.ShippedCatalog.Machines[Miner].BuildPrice.Base;

        private const long GrowthPpm = 1_500_000;
        private const long WorstGrowthPpm = 30_000_000;

        // The price after <owned> Miners at 1.5x per Miner (ceil), uncapped: the base for 0 owned, then +50 percent each.
        private static long Grown(int owned) => (Base * Pow(3, owned) + Pow(2, owned) - 1) / Pow(2, owned);

        // The price after <owned> Miners at 30x per Miner, uncapped.
        private static long WorstGrown(int owned) => Base * Pow(30, owned);

        private static long Pow(long value, int exponent)
        {
            var result = 1L;
            for (var i = 0; i < exponent; i++)
            {
                result *= value;
            }

            return result;
        }

        // A cap that leaves two owned Miners uncapped and caps the third: one above the price at two owned (growth 1.5 gives 3.375 x base at three).
        private static long Cap => Grown(2) + 1;

        // A six-digit cap above the price at three owned in the 30x fixture, for the worst-case layout.
        private static long WorstCap => WorstGrown(3) + 177_654;

        private static string Cash(long amount) => amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        // Base, +50 percent (rounded up) per owned Miner, then the cap.
        private IEnumerator OpenGrowing(ShellScreen screen, int percent, long cash, ShellDeviceProfile? profile = null)
        {
            session = GrowingSession(out commands, cash, 3, GrowthPpm, Cap);
            yield return OpenWith(screen, percent, profile);
        }

        // Base x 30 per owned Miner (six digits at three owned), then the six-digit cap.
        private IEnumerator OpenWorstCase(ShellScreen screen, int percent)
        {
            session = GrowingSession(out commands, 5_000_000, 3, WorstGrowthPpm, WorstCap);
            yield return OpenWith(screen, percent, null);
        }

        private IEnumerator OpenWith(ShellScreen screen, int percent, ShellDeviceProfile? profile)
        {
            rig = ShellRig.Create(screen, profile, percent);
            rigs.Add(rig);
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelRoot + "BuildSheetPanel.uxml");
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(PanelRoot + "BuildSheetPanel.uss");
            Assert.That(tree, Is.Not.Null);
            Assert.That(styles, Is.Not.Null);
            panel = new BuildSheetPresenter(session, commands, tree, styles, ContractsPanelTestSupport.Icon);
            rig.Shell.Panels.Register(panel);
            yield return rig.Layout();
        }

        private IEnumerator ShowSlot(int slot)
        {
            Assert.That(rig.Shell.OpenSlot(slot, SlotCondition.Empty), Is.True);
            yield return ShellRig.Settle();
        }

        private static ShellScreen PhoneScreen() => ShellScreens.All().First(screen => screen.Name.StartsWith("phone-portrait 390", StringComparison.Ordinal));

        private int CardOf(string label) =>
            Enumerable.Range(0, panel.BuildButtons.Count).First(i => panel.Root.Q<Label>("build-name-" + i).text == label);

        private Button BuildButton(string label) => panel.BuildButtons[CardOf(label)];

        private Button Price(string machineId = Miner) => panel.Root.Q<Button>("build-price-" + machineId);

        private string OwnedText(string label) => panel.Root.Q<Label>("build-owned-" + CardOf(label)).text;

        private Label CappedLabel(string label) => panel.Root.Q<Label>("build-capped-" + CardOf(label));

        private bool CappedShown(string label) => CappedLabel(label).style.display != DisplayStyle.None;

        private string Feedback(string label) => panel.Root.Q<Label>("build-feedback-" + CardOf(label)).text;

        private bool FeedbackShown(string label) => panel.Root.Q<Label>("build-feedback-" + CardOf(label)).style.display != DisplayStyle.None;

        private MachineTypeSnapshot MinerType() => RunSnapshots.Factory(session).MachineTypes.First(type => type.MachineId == Miner);

        private void BuildExternally(params int[] slots)
        {
            foreach (var slot in slots)
            {
                var built = commands.Build(slot, Miner);
                Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
            }
        }

        private static void Press(VisualElement control) => Send(control, NavigationSubmitEvent.GetPooled());

        private static void Send(VisualElement target, EventBase evt)
        {
            using (evt)
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        private static void SendPointer(VisualElement target, EventBase evt, string pointerType)
        {
            using (evt)
            {
                for (var type = evt.GetType(); type != null; type = type.BaseType)
                {
                    var property = type.GetProperty("pointerType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    var setter = property?.GetSetMethod(true);
                    if (setter != null)
                    {
                        setter.Invoke(evt, new object[] { pointerType });
                        break;
                    }
                }

                evt.target = target;
                target.SendEvent(evt);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // QA-12 "Build price": affordable
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AnAffordablePriceShowsTheCostTheOwnedCountAndTheAccessibleName()
        {
            yield return OpenGrowing(PhoneScreen(), 100, 1_000);
            yield return ShowSlot(0);

            Assert.That(Price().text, Is.EqualTo("Cost: " + Cash(Grown(0)) + " Cash"));
            Assert.That(OwnedText("Miner"), Is.EqualTo("Owned: 0"));
            Assert.That(CappedShown("Miner"), Is.False, "far below the cap");
            Assert.That(RunShellDocument.IsSoftDisabled(BuildButton("Miner")), Is.False);
            Assert.That(BuildButton("Miner").tooltip, Is.EqualTo("Build Miner, $" + Cash(Grown(0)) + ", owned 0"));
        }

        [UnityTest]
        public IEnumerator BuildingChargesTheShownPriceAndTheNextPriceRises()
        {
            yield return OpenGrowing(PhoneScreen(), 100, 1_000);
            yield return ShowSlot(0);
            Press(BuildButton("Miner"));

            Assert.That(session.ReadCash(), Is.EqualTo(1_000 - Grown(0)), "the shown price is charged");
            yield return ShowSlot(1);
            Assert.That(Price().text, Is.EqualTo("Cost: " + Cash(Grown(1)) + " Cash"));
            Assert.That(OwnedText("Miner"), Is.EqualTo("Owned: 1"));
            Assert.That(BuildButton("Miner").tooltip, Is.EqualTo("Build Miner, $" + Cash(Grown(1)) + ", owned 1"));
            Press(BuildButton("Miner"));
            Assert.That(session.ReadCash(), Is.EqualTo(1_000 - Grown(0) - Grown(1)));
        }

        // ---------------------------------------------------------------------------------------------------
        // QA-12 "Build price": "Need $X more"
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AnUnaffordablePriceShowsTheMissingCashDisablesBuildAndKeepsItFocusable()
        {
            yield return OpenGrowing(PhoneScreen(), 100, Grown(0) + 10);
            yield return ShowSlot(0);
            Press(BuildButton("Miner"));
            Assert.That(session.ReadCash(), Is.EqualTo(10), "ten Cash are left after the first Miner");

            yield return ShowSlot(1);
            var card = CardOf("Miner");
            Assert.That(Price().text, Is.EqualTo("Cost: " + Cash(Grown(1)) + " Cash"));
            Assert.That(panel.Root.Q<Label>("build-reason-" + card).text, Is.EqualTo("Not enough Cash. Need " + (Grown(1) - 10) + " more."));
            Assert.That(RunShellDocument.IsSoftDisabled(BuildButton("Miner")), Is.True);
            Assert.That(BuildButton("Miner").focusable, Is.True, "a disabled-looking control stays focusable");
            Assert.That(BuildButton("Miner").tooltip, Is.EqualTo("Build Miner, $" + Cash(Grown(1)) + ", owned 1, unavailable: Not enough Cash. Need " + (Grown(1) - 10) + " more."));

            Press(BuildButton("Miner"));
            Assert.That(Feedback("Miner"), Is.EqualTo(RunRejectionCopy.For(RunReasons.CashInsufficient)));
            Assert.That(session.ReadCash(), Is.EqualTo(10), "nothing is charged");
            Assert.That(RunSnapshots.Factory(session).Machines.Count, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------------------------------------------
        // QA-12 "Build price": capped
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ACappedPriceShowsCappedAsTextAndFlatContentNeverDoes()
        {
            yield return OpenGrowing(PhoneScreen(), 100, 10_000);
            BuildExternally(0, 1);
            yield return ShowSlot(2);
            Assert.That(Price().text, Is.EqualTo("Cost: " + Cash(Grown(2)) + " Cash"));
            Assert.That(CappedShown("Miner"), Is.False, "the price at two owned is below the cap");

            BuildExternally(2);
            yield return ShowSlot(3);
            Assert.That(Price().text, Is.EqualTo("Cost: " + Cash(Cap) + " Cash"));
            Assert.That(OwnedText("Miner"), Is.EqualTo("Owned: 3"));
            Assert.That(CappedShown("Miner"), Is.True);
            Assert.That(CappedLabel("Miner").text, Does.StartWith("Capped"), "the cue is text, not colour alone");
            Assert.That(BuildButton("Miner").tooltip, Is.EqualTo("Build Miner, $" + Cash(Cap) + ", owned 3, price capped"));

            // Flat content (growth 1, cap equal to the base): never shown as capped. A flat variant, so a retuned shipped price cannot change the case.
            session = GrowingSession(out commands, 10_000, 3, 1_000_000, Base);
            yield return OpenWith(PhoneScreen(), 100, null);
            BuildExternally(0, 1, 2);
            yield return ShowSlot(3);
            Assert.That(OwnedText("Miner"), Is.EqualTo("Owned: 3"));
            Assert.That(CappedShown("Miner"), Is.False, "flat content reads not capped");
        }

        // ---------------------------------------------------------------------------------------------------
        // QA-12 "Build price": build.price.changed recovery
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AMovedPriceRecoversWithThePriceChangedLineAndChargesNothing()
        {
            yield return OpenGrowing(PhoneScreen(), 100, 1_000);
            yield return ShowSlot(1);
            Assert.That(Price().text, Is.EqualTo("Cost: " + Cash(Grown(0)) + " Cash"));

            // Another route builds a Miner after the sheet drew: the price the sheet shows is stale.
            BuildExternally(0);
            var cash = session.ReadCash();
            Press(BuildButton("Miner"));

            Assert.That(FeedbackShown("Miner"), Is.True);
            Assert.That(Feedback("Miner"), Is.EqualTo("Price changed to $" + Cash(Grown(1)) + ". You were not charged."));
            Assert.That(session.ReadCash(), Is.EqualTo(cash), "nothing is charged on a price change");
            Assert.That(RunSnapshots.Factory(session).Machines.Count, Is.EqualTo(1), "nothing was built");
            Assert.That(Price().text, Is.EqualTo("Cost: " + Cash(Grown(1)) + " Cash"), "the sheet re-rendered with the current price");
            Assert.That(BuildButton("Miner").tooltip, Is.EqualTo("Build Miner, $" + Cash(Grown(1)) + ", owned 1"));

            Press(BuildButton("Miner"));
            Assert.That(session.ReadCash(), Is.EqualTo(cash - Grown(1)), "the second press passes the refreshed price and builds");
            Assert.That(RunSnapshots.Factory(session).Machines.Count, Is.EqualTo(2));
        }

        // ---------------------------------------------------------------------------------------------------
        // QA-12 "Build price": Remove confirmation with the rebuild price
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheRemoveConfirmationStatesTheRebuildPrice()
        {
            session = GrowingSession(out commands, 10_000, 3, GrowthPpm, Cap);
            BuildExternally(0, 1, 2);
            rig = ShellRig.Create(PhoneScreen(), null, 100);
            rigs.Add(rig);
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelRoot + "MachinePanel.uxml");
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(PanelRoot + "MachinePanel.uss");
            var machinePanel = new MachinePanelPresenter(session, commands, tree, styles, ContractsPanelTestSupport.Icon);
            rig.Shell.Panels.Register(machinePanel);
            yield return rig.Layout();
            Assert.That(rig.Shell.OpenSlot(2, SlotCondition.Occupied), Is.True);
            yield return ShellRig.Settle();

            machinePanel.RemoveButton.Focus();
            yield return null;
            Press(machinePanel.RemoveButton);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.True);
            var message = rig.Shell.DialogLayer.Q<Label>(className: "run-dialog__message").text;
            Assert.That(message, Does.Contain("Building another Miner afterwards costs $" + Cash(Grown(2)) + "."), "three Miners owned: the price at two owned");
            Assert.That(MinerType().RebuildPrice, Is.EqualTo(Grown(2)));

            // The stated price is the price charged after the removal.
            Assert.That(commands.Remove(RunSnapshots.Factory(session).Machines.First(machine => machine.SlotIndex == 2).InstanceId, true).IsSuccess, Is.True);
            Assert.That(MinerType().BuildCost, Is.EqualTo(Grown(2)));
        }

        // ---------------------------------------------------------------------------------------------------
        // Exact value on inspect: keyboard focus, touch and pointer
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator FocusShowsTheExactPriceInTheDetailLine()
        {
            yield return OpenWorstCase(PhoneScreen(), 100);
            BuildExternally(0, 1, 2);
            yield return ShowSlot(3);
            Assert.That(Price().text, Does.StartWith("Cost: "));
            Price().Focus();
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.DetailText, Is.EqualTo("Miner costs " + Cash(WorstGrown(3)) + " Cash, owned 3"));
        }

        [UnityTest]
        public IEnumerator OnATouchDeviceATapPinsTheExactPrice()
        {
            var screen = PhoneScreen();
            yield return OpenGrowing(screen, 100, 1_000, ShellDeviceProfile.Touch(screen.Width, screen.Height, PhoneDpi));
            yield return ShowSlot(0);
            SendPointer(Price(), ClickEvent.GetPooled(), UnityEngine.UIElements.PointerType.touch);
            yield return null;
            Assert.That(rig.Shell.IsDetailPinned, Is.True);
            Assert.That(rig.Shell.DetailText, Is.EqualTo("Miner costs " + Cash(Grown(0)) + " Cash, owned 0"));
        }

        [UnityTest]
        public IEnumerator OnAPointerDeviceHoverShowsTheExactPrice()
        {
            var screen = PhoneScreen();
            yield return OpenGrowing(screen, 100, 1_000, ShellDeviceProfile.Pointer(screen.Width, screen.Height, 96f));
            yield return ShowSlot(0);
            SendPointer(Price(), PointerEnterEvent.GetPooled(), UnityEngine.UIElements.PointerType.mouse);
            yield return null;
            Assert.That(rig.Shell.DetailText, Is.EqualTo("Miner costs " + Cash(Grown(0)) + " Cash, owned 0"));
        }

        // ---------------------------------------------------------------------------------------------------
        // Every screen class at 100 % and 200 %: worst-case content stays contained and reachable
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheWorstCasePriceSheetIsContainedAndReachableAtEveryScreenClassAndTextScale(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return OpenWorstCase(screen, percent);
            BuildExternally(0, 1, 2);
            yield return ShowSlot(3);
            var where = $"{screen} @{percent}%";
            Assert.That(Price().text, Is.EqualTo("Cost: " + QuantityFormatter.Format(WorstGrown(3)) + " Cash"), where);
            AssertShellLayout(rig, "build price sheet, six-digit price");

            // A price change to the capped price: the recovery line (with the 6-digit price) and the Capped line are both shown.
            BuildExternally(4);
            Press(BuildButton("Miner"));
            yield return ShellRig.Settle();
            Assert.That(Feedback("Miner"), Is.EqualTo("Price changed to $" + Cash(WorstCap) + ". You were not charged."), where);
            Assert.That(CappedShown("Miner"), Is.True, where);
            Assert.That(session.ReadCash(), Is.GreaterThan(0));
            AssertShellLayout(rig, "build price sheet with the recovery line and Capped");

            var scroll = rig.Shell.PanelScroll;
            var viewport = scroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport");
            var range = Mathf.Max(0f, scroll.contentContainer.layout.height - viewport.layout.height);
            foreach (var offset in new[] { 0f, 0.5f, 1f })
            {
                scroll.scrollOffset = new Vector2(scroll.scrollOffset.x, range * offset);
                yield return ShellRig.Settle();
                AssertScrollContentClipped(scroll, $"{where} price sheet scrolled to {offset:P0}");
            }

            var safe = rig.SafeRect();
            var controls = new List<Button>();
            foreach (var card in new[] { 0, panel.BuildButtons.Count / 2, panel.BuildButtons.Count - 1 })
            {
                controls.Add(panel.Root.Q<Button>("build-price-" + panel.Root.Q<Label>("build-name-" + card).text.ToLowerInvariant().Insert(0, "machine.")));
                controls.Add(panel.BuildButtons[card]);
            }

            foreach (var control in controls.Where(control => control != null))
            {
                Assert.That(control.worldBound.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{where}: {control.name} width");
                Assert.That(control.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{where}: {control.name} height");
                scroll.scrollOffset = new Vector2(0f, control == controls.Last() ? 0f : float.MaxValue);
                yield return ShellRig.Settle();
                control.Focus();
                yield return ShellRig.Settle();
                Assert.That(control.focusController.focusedElement, Is.SameAs(control), $"{where}: '{control.name}' did not take focus");
                AssertWithin(viewport.worldBound, control.worldBound, $"{where}: focused '{control.name}' is not scrolled into view");
                AssertWithin(safe, control.worldBound, $"{where}: focused '{control.name}' leaves the safe area");
            }
        }
    }
}
#endif
