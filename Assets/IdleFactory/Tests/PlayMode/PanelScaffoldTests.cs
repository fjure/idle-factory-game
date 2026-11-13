using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T533 (Feature 006, FR-005, FR-023, research R11, Art Bible section 11, OD-9b UI/UX C4 to C6): the shared panel scaffold. At every
    /// screen class and at 100, 150 and 200 percent text, the header, the primary and secondary buttons, the disabled state, the
    /// empty, loading and error templates and the notices stay inside the safe area, clip and squeeze nothing, are 48 pt targets, keep the
    /// focus order (Back first) and keep the header chrome within its share of the screen. Governance tests read the USS for tokens only.
    /// </summary>
    public sealed class PanelScaffoldTests
    {
        private const float Target = 48f;
        private const float Tolerance = 0.51f;

        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;
        private PanelScaffold scaffold;
        private VisualElement host;
        private int backCount;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        public static IEnumerable<int> Percents() => new[] { 100, 150, 200 };

        [TearDown]
        public void TearDown()
        {
            foreach (var item in rigs)
            {
                item.Dispose();
            }

            rigs.Clear();
            rig = null;
            scaffold = null;
            host = null;
        }

        private IEnumerator Open(ShellScreen screen, int percent)
        {
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            // A UI Toolkit render texture keeps earlier frames where the final frame is transparent (workstation pitfalls); clear it.
            rig.Document.panelSettings.clearColor = true;
            rig.Document.panelSettings.colorClearValue = Color.black;
            yield return rig.Layout();
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelScaffold.UxmlPath);
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(PanelScaffold.StylePath);
            Assert.That(tree, Is.Not.Null);
            Assert.That(styles, Is.Not.Null);
            backCount = 0;
            scaffold = new PanelScaffold(tree, styles, "Office", () => backCount++);
            // The shell's panel host keeps the safe area; the test host does the same.
            host = new VisualElement { name = "scaffold-test-host" };
            host.style.position = Position.Absolute;
            host.style.left = screen.Left;
            host.style.top = screen.Top;
            host.style.right = screen.Right;
            host.style.bottom = screen.Bottom;
            host.Add(scaffold.Root);
            rig.Root.Add(host);
            yield return ShellRig.Settle();
        }

        private static float Expected(int percent, float at100) => at100 * percent / 100f;

        private static void AssertTarget(VisualElement element, string context)
        {
            Assert.That(IsDisplayed(element), Is.True, $"{context}: '{element.name}' is displayed");
            Assert.That(element.worldBound.width, Is.GreaterThanOrEqualTo(Target - Tolerance), $"{context}: '{element.name}' width {element.worldBound.width}");
            Assert.That(element.worldBound.height, Is.GreaterThanOrEqualTo(Target - Tolerance), $"{context}: '{element.name}' height {element.worldBound.height}");
        }

        private string Context(string state) => $"{rig.Screen} [{state}] @{rig.TextScalePercent}%";

        private void AssertSurface(string state)
        {
            var context = Context(state);
            var safe = host.worldBound;
            AssertTextAndTargets(scaffold.Root, safe, context, 12f * rig.TextScalePercent / 100f);
            AssertWithin(safe, scaffold.Header.worldBound, context + ": header");
            foreach (var button in scaffold.Root.Query<Button>().ToList().Where(item => IsDisplayed(item)))
            {
                AssertTarget(button, context);
                if (button.GetFirstAncestorOfType<ScrollView>() == null)
                {
                    AssertWithin(safe, button.worldBound, context + ": button " + button.name);
                }
            }

            var chrome = scaffold.Header.worldBound.height / safe.height;
            var limit = rig.TextScalePercent <= 100 ? 0.15f : 0.20f;
            Assert.That(chrome, Is.LessThanOrEqualTo(limit), $"{context}: header chrome {chrome:P1} of the screen (C6)");
            Assert.That(scaffold.TitleLabel.resolvedStyle.fontSize, Is.EqualTo(Expected(rig.TextScalePercent, 16f)).Within(Tolerance), context + ": title uses the value token");
        }

        // ---------------------------------------------------------------------------------------------------
        // Header, buttons, disabled state
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator HeaderButtonsAndTheDisabledStateHoldAtEveryClassAndScale(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            var primary = PanelScaffold.CreateButton("probe-primary", "Upgrade for $1,000,000,000", true);
            var secondary = PanelScaffold.CreateButton("probe-secondary", "Details", false);
            var disabled = PanelScaffold.CreateButton("probe-disabled", "Unlock a very long named machine", true);
            var reason = new Label { name = "probe-reason" };
            scaffold.Body.Add(primary);
            scaffold.Body.Add(secondary);
            scaffold.Body.Add(disabled);
            scaffold.Body.Add(reason);
            PanelScaffold.SetDisabled(disabled, true, "Needs 12 more Iron plate before this can be unlocked", reason);
            yield return ShellRig.Settle();

            var context = Context("header");
            AssertSurface("header");
            Assert.That(scaffold.BackButton.worldBound.xMin, Is.LessThan(scaffold.TitleLabel.worldBound.xMin), context + ": Back sits before the title");
            AssertTarget(scaffold.BackButton, context);
            AssertTarget(primary, context);
            AssertTarget(secondary, context);
            AssertTarget(disabled, context);
            Assert.That(primary.resolvedStyle.fontSize, Is.EqualTo(Expected(percent, 14f)).Within(Tolerance), context + ": button text is the body token");
            Assert.That(reason.resolvedStyle.fontSize, Is.EqualTo(Expected(percent, 12f)).Within(Tolerance), context + ": reason is the caption token");
            Assert.That(reason.text, Does.Contain("Iron plate"), context + ": the reason is visible text");

            // Scroll content is clipped in its viewport and the disabled control is still reachable (soft-disabled).
            AssertScrollContentClipped(scaffold.BodyScroll, context);
            Assert.That(disabled.focusable, Is.True, context + ": soft-disabled stays focusable");
            Assert.That(disabled.enabledSelf, Is.True, context + ": soft-disabled is not the engine's disabled state");
        }

        [UnityTest]
        public IEnumerator TheDisabledStateIsConveyedByShapeTextAndContrastNotByDimming()
        {
            yield return Open(ShellScreens.All().First(), 100);
            var normal = PanelScaffold.CreateButton("normal", "Upgrade", false);
            var disabled = PanelScaffold.CreateButton("disabled", "Upgrade", false);
            var reason = new Label();
            scaffold.Body.Add(normal);
            scaffold.Body.Add(disabled);
            scaffold.Body.Add(reason);
            PanelScaffold.SetDisabled(disabled, true, "Not enough cash", reason);
            yield return ShellRig.Settle();

            Assert.That(PanelScaffold.IsDisabled(disabled), Is.True);
            Assert.That(PanelScaffold.IsDisabled(normal), Is.False);
            // Not by dimming alone: opacity stays 1 on the control, its text colour and every ancestor.
            for (var current = (VisualElement)disabled; current != null; current = current.parent)
            {
                Assert.That(current.resolvedStyle.opacity, Is.EqualTo(1f), "opacity of " + current.name);
            }

            // Shape: square corners and a thick left bar instead of the rounded outline.
            Assert.That(normal.resolvedStyle.borderTopLeftRadius, Is.GreaterThan(0f));
            Assert.That(disabled.resolvedStyle.borderTopLeftRadius, Is.EqualTo(0f));
            Assert.That(disabled.resolvedStyle.borderLeftWidth, Is.GreaterThan(normal.resolvedStyle.borderLeftWidth + 2f));
            // Text: the reason is shown as text and named in the tooltip.
            Assert.That(reason.text, Is.EqualTo("Not enough cash"));
            Assert.That(disabled.tooltip, Does.Contain("Not enough cash"));
            // Contrast: the label against the control fill is at least 4.5:1, and the disabled fill differs from the enabled one.
            var ratio = Contrast(disabled.resolvedStyle.color, disabled.resolvedStyle.backgroundColor);
            Assert.That(ratio, Is.GreaterThanOrEqualTo(4.5f), "disabled label contrast " + ratio);
            Assert.That(disabled.resolvedStyle.backgroundColor, Is.Not.EqualTo(normal.resolvedStyle.backgroundColor));

            PanelScaffold.SetDisabled(disabled, false);
            Assert.That(PanelScaffold.IsDisabled(disabled), Is.False);
            Assert.That(disabled.tooltip, Is.Empty);
        }

        [UnityTest]
        public IEnumerator PrimaryAndSecondaryButtonsDifferByFillAndMeetContrast()
        {
            yield return Open(ShellScreens.All().First(), 100);
            var primary = PanelScaffold.CreateButton("p", "Go", true);
            var secondary = PanelScaffold.CreateButton("s", "Cancel", false);
            scaffold.Body.Add(primary);
            scaffold.Body.Add(secondary);
            yield return ShellRig.Settle();
            Assert.That(primary.resolvedStyle.backgroundColor, Is.Not.EqualTo(secondary.resolvedStyle.backgroundColor));
            Assert.That(Contrast(primary.resolvedStyle.color, primary.resolvedStyle.backgroundColor), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(Contrast(secondary.resolvedStyle.color, secondary.resolvedStyle.backgroundColor), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(primary.ClassListContains("run-control"), Is.True, "the shell focus ring applies to scaffold buttons");
        }

        // ---------------------------------------------------------------------------------------------------
        // Empty, loading and error templates
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EmptyLoadingAndErrorTemplatesHoldAtEveryClassAndScale(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            var longText = "Nothing has been delivered to this system yet, so there is nothing to show; come back after the first delivery.";

            scaffold.ShowEmpty("Nothing here yet", longText, "Open the factory", () => { });
            yield return ShellRig.Settle();
            AssertSurface("empty");
            Assert.That(IsDisplayed(scaffold.StateCard), Is.True);
            AssertTarget(scaffold.StatePrimaryButton, Context("empty"));
            AssertTarget(scaffold.StateSecondaryButton, Context("empty"));

            scaffold.ShowLoading("Reading your save");
            yield return ShellRig.Settle();
            AssertSurface("loading");
            Assert.That(scaffold.StatePrimaryButton.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "loading offers no primary action");
            AssertTarget(scaffold.StateSecondaryButton, Context("loading"));
            Assert.That(scaffold.StateCard.Q<VisualElement>("scaffold-state-bar").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));

            scaffold.ShowError(longText, () => { });
            yield return ShellRig.Settle();
            AssertSurface("error");
            AssertTarget(scaffold.StatePrimaryButton, Context("error"));
            AssertTarget(scaffold.StateSecondaryButton, Context("error"));
            Assert.That(scaffold.StatePrimaryButton.text, Is.EqualTo("Retry"));
            Assert.That(scaffold.StateSecondaryButton.text, Is.EqualTo("Back"));
            Assert.That(scaffold.BodyScroll.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "the state replaces the body");
            AssertScrollContentClipped(scaffold.StateScroll, Context("error"));

            scaffold.ShowContent();
            yield return ShellRig.Settle();
            Assert.That(scaffold.StateCard.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(scaffold.BodyScroll.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
        }

        [UnityTest]
        public IEnumerator EachStateCarriesAShapeAndWordCueNotColourAlone()
        {
            yield return Open(ShellScreens.All().First(), 100);
            var marks = new Dictionary<ScaffoldViewState, (string text, float radius)>();
            var mark = scaffold.StateCard.Q<Label>("scaffold-state-mark");

            scaffold.ShowEmpty("Nothing here yet", "Nothing to show.");
            yield return ShellRig.Settle();
            marks[ScaffoldViewState.Empty] = (mark.text, mark.resolvedStyle.borderTopLeftRadius);
            scaffold.ShowLoading("Reading");
            yield return ShellRig.Settle();
            marks[ScaffoldViewState.Loading] = (mark.text, mark.resolvedStyle.borderTopLeftRadius);
            scaffold.ShowError("Failed", null);
            yield return ShellRig.Settle();
            marks[ScaffoldViewState.Error] = (mark.text, mark.resolvedStyle.borderTopLeftRadius);

            Assert.That(marks.Values.Select(item => item.text).Distinct().Count(), Is.EqualTo(3), "a distinct glyph per state");
            Assert.That(marks.Values.Select(item => item.radius).Distinct().Count(), Is.EqualTo(3), "a distinct corner shape per state");
            Assert.That(scaffold.StateTitle.text, Is.Not.Empty, "a heading names the state");
            Assert.That(scaffold.StatePrimaryButton.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "an error without a retry offers only Back");
        }

        [UnityTest]
        public IEnumerator TheStateActionsAndBackRunTheirCallbacks()
        {
            yield return Open(ShellScreens.All().First(), 100);
            var retries = 0;
            scaffold.ShowError("Failed to load", () => retries++);
            yield return ShellRig.Settle();

            Click(scaffold.StatePrimaryButton);
            Assert.That(retries, Is.EqualTo(1), "Retry runs the retry callback");
            Click(scaffold.StateSecondaryButton);
            Assert.That(backCount, Is.EqualTo(1), "the state's Back runs the back callback");
            Click(scaffold.BackButton);
            Assert.That(backCount, Is.EqualTo(2), "the header Back runs the back callback");

            var acted = 0;
            scaffold.ShowEmpty("Empty", "Nothing.", "Do it", () => acted++);
            Click(scaffold.StatePrimaryButton);
            Assert.That(acted, Is.EqualTo(1));

            scaffold.BackVisible = false;
            yield return ShellRig.Settle();
            Assert.That(scaffold.BackButton.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(scaffold.BackButton.focusable, Is.False, "a hidden Back leaves the focus order");
            scaffold.BackVisible = true;
            Assert.That(scaffold.BackButton.focusable, Is.True);
        }

        // ---------------------------------------------------------------------------------------------------
        // Notices layered above sheets
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator NoticesLayerAboveTheBodyAndHoldAtEveryClassAndScale(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            for (var i = 0; i < 24; i++)
            {
                scaffold.Body.Add(new Label("Body line " + i) { name = "body-line-" + i });
            }

            var text = scaffold.ShowNotice("Saved to this device only, so another phone will not see this factory.", ScaffoldNoticeKind.Warning);
            scaffold.ShowNotice("Welcome back.");
            yield return ShellRig.Settle();

            var context = Context("notices");
            var notices = scaffold.Notices;
            Assert.That(notices.Count, Is.EqualTo(2));
            var layer = scaffold.NoticeLayer;
            var stage = layer.parent;
            Assert.That(stage.IndexOf(layer), Is.EqualTo(stage.childCount - 1), context + ": the layer is the last sibling, so it draws above the body");
            Assert.That(layer.pickingMode, Is.EqualTo(PickingMode.Ignore), context + ": the layer takes no pointer");
            var host = scaffold.Root.Q<VisualElement>("scaffold-stage").worldBound;
            foreach (var notice in notices)
            {
                AssertWithin(host, notice.worldBound, context + ": notice stays in the stage");
                Assert.That(Intersects(notice.worldBound, scaffold.BodyScroll.worldBound), Is.True, context + ": the notice overlaps the body");
                var dismiss = notice.Q<Button>("scaffold-notice-dismiss");
                AssertTarget(dismiss, context);
                // A notice wins hit testing over the body beneath it.
                var picked = rig.Root.panel.Pick(dismiss.worldBound.center);
                Assert.That(picked == dismiss || dismiss.Contains(picked), Is.True, context + ": the dismiss button is on top");
            }

            Assert.That(text.text, Does.StartWith("Warning:"), context + ": the kind is a word");
            AssertTextAndTargets(layer, this.host.worldBound, context, 12f * percent / 100f);
            Assert.That(text.resolvedStyle.fontSize, Is.EqualTo(Expected(percent, 14f)).Within(Tolerance));

            Click(notices[0].Q<Button>("scaffold-notice-dismiss"));
            yield return ShellRig.Settle();
            Assert.That(scaffold.Notices.Count, Is.EqualTo(1), "Dismiss removes its notice");
            scaffold.ClearNotices();
            Assert.That(scaffold.Notices.Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator OnlyTheNewestNoticesStay()
        {
            yield return Open(ShellScreens.All().First(), 100);
            for (var i = 0; i < PanelScaffold.MaxNotices + 2; i++)
            {
                scaffold.ShowNotice("n" + i);
            }

            Assert.That(scaffold.Notices.Count, Is.EqualTo(PanelScaffold.MaxNotices));
            Assert.That(scaffold.Notices.Last().Q<Label>("scaffold-notice-text").text, Does.EndWith("n" + (PanelScaffold.MaxNotices + 1)));
            yield break;
        }

        // ---------------------------------------------------------------------------------------------------
        // Focus, accessibility, reduced motion
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator FocusStartsAtBackAndShowsTheShellFocusRingAtEveryScale([ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(ShellScreens.All().First(), percent);
            scaffold.Body.Add(PanelScaffold.CreateButton("body-button", "Body action", true));
            scaffold.ShowNotice("A notice");
            yield return ShellRig.Settle();

            // Document order is the keyboard and gamepad focus order: Back, then the body, then the notices.
            var focusable = scaffold.Root.Query<Button>().Where(item => item.focusable && IsDisplayed(item)).ToList();
            Assert.That(focusable[0], Is.SameAs(scaffold.BackButton), "Back is first in the focus order");
            Assert.That(focusable.Select(item => item.name).ToArray(), Is.EqualTo(new[] { "scaffold-back", "body-button", "scaffold-notice-dismiss" }));

            scaffold.BackButton.Focus();
            yield return ShellRig.Settle();
            Assert.That(rig.Root.panel.focusController.focusedElement, Is.SameAs(scaffold.BackButton));
            Assert.That(scaffold.BackButton.ClassListContains("run-control--focused"), Is.True, "the shell marks the focused scaffold button");
            Assert.That(rig.Shell.FocusRingTarget, Is.SameAs(scaffold.BackButton));
            Assert.That(rig.Shell.FocusRing.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "the focus ring is visible");
            var ring = rig.Shell.FocusRing.worldBound;
            Assert.That(ring.width, Is.GreaterThanOrEqualTo(scaffold.BackButton.worldBound.width - Tolerance));
        }

        [UnityTest]
        public IEnumerator ControlsHaveAccessibleNames()
        {
            yield return Open(ShellScreens.All().First(), 100);
            scaffold.ShowError("It failed", () => { });
            scaffold.ShowNotice("Heads up");
            yield return ShellRig.Settle();
            foreach (var button in scaffold.Root.Query<Button>().ToList())
            {
                Assert.That(string.IsNullOrEmpty(button.text) && string.IsNullOrEmpty(button.tooltip) && IsDisplayed(button), Is.False,
                    "button '" + button.name + "' has a visible text or a tooltip name");
            }

            Assert.That(scaffold.BackButton.tooltip, Is.EqualTo("Back"));
            Assert.That(scaffold.TitleLabel.text, Is.EqualTo("Office"));
            scaffold.Title = "Contracts";
            Assert.That(scaffold.TitleLabel.text, Is.EqualTo("Contracts"));
        }

        [UnityTest]
        public IEnumerator NothingAnimatesSoReducedMotionHasTheSameStaticForm()
        {
            yield return Open(ShellScreens.All().First(), 100);
            scaffold.ShowLoading("Reading");
            scaffold.ShowNotice("A notice");
            yield return ShellRig.Settle();
            foreach (var element in scaffold.Root.Query<VisualElement>().ToList())
            {
                var transitions = element.resolvedStyle.transitionDuration;
                Assert.That(transitions.All(item => item.value <= 0f), Is.True, "no transition on " + Describe(element));
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Governance (Art Bible section 11): tokens only
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void TheScaffoldStyleSheetUsesTokensOnly()
        {
            var css = File.ReadAllText(PanelScaffold.StylePath);
            css = Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            Assert.That(Regex.IsMatch(css, @"#[0-9a-fA-F]{3,8}\b"), Is.False, "no literal hex colour");
            Assert.That(Regex.IsMatch(css, @"\brgba?\s*\("), Is.False, "no literal rgb colour");
            Assert.That(css, Does.Not.Contain("opacity"), "the disabled and every other state avoid dimming");
            Assert.That(css, Does.Not.Contain("transition"), "no animation (C5)");
            Assert.That(css, Does.Not.Contain("nowrap"));

            var fontSizes = Regex.Matches(css, @"font-size\s*:\s*([^;]+);").Cast<Match>().Select(match => match.Groups[1].Value.Trim()).ToList();
            Assert.That(fontSizes, Is.Not.Empty);
            Assert.That(fontSizes.All(value => value.StartsWith("var(--run-fs-")), Is.True, "font sizes only through --run-fs-* tokens: " + string.Join(", ", fontSizes));

            var colours = Regex.Matches(css, @"(?<![-\w])(?:color|background-color|border-[a-z]+-color|border-color)\s*:\s*([^;]+);")
                .Cast<Match>().Select(match => match.Groups[1].Value.Trim()).ToList();
            Assert.That(colours, Is.Not.Empty);
            var allowed = new HashSet<string>
            {
                "var(--bg)", "var(--surface)", "var(--surface-raised)", "var(--text-primary)", "var(--text-strong)", "var(--text-secondary)",
                "var(--text-muted)", "var(--accent)", "var(--on-accent)", "var(--positive)", "var(--warning)", "var(--danger)", "var(--border)",
            };
            Assert.That(colours.Where(value => !allowed.Contains(value)).ToList(), Is.Empty, "colours only through the chrome tokens");
        }

        [Test]
        public void TheScaffoldDeclaresNoTokenOfItsOwn()
        {
            var css = File.ReadAllText(PanelScaffold.StylePath);
            Assert.That(Regex.IsMatch(css, @"^\s*--[a-z-]+\s*:", RegexOptions.Multiline), Is.False, "tokens live in FactoryRun.uss only");
            Assert.That(File.ReadAllText(PanelScaffold.UxmlPath), Does.Not.Contain("font-size"));
        }

        // ---------------------------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------------------------

        private static void Click(Button button)
        {
            using (var evt = NavigationSubmitEvent.GetPooled())
            {
                evt.target = button;
                button.SendEvent(evt);
            }
        }

        private static float Luminance(Color color)
        {
            static float Channel(float value) => value <= 0.03928f ? value / 12.92f : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Channel(color.r) + 0.7152f * Channel(color.g) + 0.0722f * Channel(color.b);
        }

        private static float Contrast(Color foreground, Color background)
        {
            var a = Luminance(foreground);
            var b = Luminance(background);
            return (Mathf.Max(a, b) + 0.05f) / (Mathf.Min(a, b) + 0.05f);
        }
    }
}
#endif
