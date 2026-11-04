using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using IdleFactory.Unity.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

namespace IdleFactory.Tests.PlayMode
{
#if UNITY_EDITOR
    /// <summary>
    /// T433 (FR-018, FR-020, FR-020a, OD-8): the gear and the Settings sheet. Focus order, focus return to the gear, Escape and Android
    /// Back, 44 x 44 pt targets, 200 percent text, the one sheet host and the state table, at every supported screen class.
    /// </summary>
    public sealed class SettingsSheetTests
    {
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private string root;
        private SettingsTestHarness harness;
        private ShellRig rig;
        private SettingsPanelPresenter presenter;
        private IDisposable registration;

        [SetUp]
        public void SetUp()
        {
            root = SettingsTestHarness.NewRoot();
        }

        [TearDown]
        public void TearDown()
        {
            registration?.Dispose();
            registration = null;
            foreach (var item in rigs)
            {
                item.Dispose();
            }

            rigs.Clear();
            rig = null;
            SettingsTestHarness.DeleteRoot(root);
        }

        private IEnumerator Open(ShellScreen screen, int percent = 100, bool systemReduces = false, bool systemAvailable = true)
        {
            harness = new SettingsTestHarness(root, systemReduces, systemAvailable);
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/IdleFactory/Unity/UI/Panels/Settings/SettingsPanel.uxml");
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/IdleFactory/Unity/UI/Panels/Settings/SettingsPanel.uss");
            Assert.That(tree, Is.Not.Null);
            Assert.That(styles, Is.Not.Null);
            presenter = harness.NewPresenter(tree, styles);
            registration = rig.Shell.Panels.Register(presenter);
            yield return rig.Layout();
        }

        private static VisualElement Focused(VisualElement any) => any.focusController?.focusedElement as VisualElement;

        private static void Key(VisualElement target, KeyCode key)
        {
            using (var evt = KeyDownEvent.GetPooled('\0', key, EventModifiers.None))
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        private static void Click(Button button)
        {
            using (var evt = NavigationSubmitEvent.GetPooled())
            {
                evt.target = button;
                button.SendEvent(evt);
            }
        }

        [UnityTest]
        public IEnumerator TheGearIsTheLastStripElementFixedAtFortyFourPointsAndNamedSettingsAtEveryScreenClass()
        {
            foreach (var screen in ShellScreens.All())
            {
                foreach (var percent in new[] { 100, 200 })
                {
                    yield return Open(screen, percent);
                    var gear = rig.Shell.SettingsButton;
                    var strip = rig.Shell.TopStrip;
                    var context = screen + " @" + percent;
                    Assert.That(gear, Is.Not.Null, context);
                    Assert.That(strip.Children().Last(), Is.SameAs(gear), context + ": last element of the top strip");
                    Assert.That(gear.tooltip, Is.EqualTo("Settings"), context + ": accessible name and tooltip");
                    Assert.That(gear.worldBound.width, Is.EqualTo(44f).Within(0.5f), context);
                    Assert.That(gear.worldBound.height, Is.EqualTo(44f).Within(0.5f), context);
                    AssertWithin(rig.SafeRect(), gear.worldBound, context + ": gear inside the safe area");
                    AssertWithin(strip.worldBound, gear.worldBound, context + ": gear inside the strip");
                    foreach (var other in new VisualElement[] { rig.Shell.CashButton, rig.Shell.RpButton, rig.Shell.StoreButton })
                    {
                        Assert.That(Intersects(gear.worldBound, other.worldBound), Is.False, context + ": gear overlaps " + other.name);
                    }

                    if (screen.IsPhone)
                    {
                        Assert.That(Intersects(gear.worldBound, rig.Shell.OverdriveButton.worldBound), Is.False, context + ": gear overlaps Overdrive");
                    }

                    rig.Dispose();
                    rigs.Remove(rig);
                    registration?.Dispose();
                    registration = null;
                }
            }
        }

        [UnityTest]
        public IEnumerator TheGearIsLastInFocusOrderAfterOverdriveInTheStrip()
        {
            yield return Open(ShellScreens.All().First());
            var order = rig.Shell.GetFocusOrder().ToList();
            var gear = order.IndexOf(rig.Shell.SettingsButton);
            Assert.That(gear, Is.GreaterThanOrEqualTo(0));
            Assert.That(order.IndexOf(rig.Shell.OverdriveButton), Is.LessThan(gear), "after Overdrive");
            Assert.That(order.IndexOf(rig.Shell.StoreButton), Is.LessThan(gear));
            Assert.That(order.Where(control => rig.Shell.TopStrip.Contains(control)).Last(), Is.SameAs(rig.Shell.SettingsButton));
        }

        [UnityTest]
        public IEnumerator TheGearOpensAndClosesTheSheetAndSettingsHasItsOwnFlag()
        {
            yield return Open(ShellScreens.All().First());
            Assert.That(rig.Shell.IsSettingsOpen, Is.False);
            Click(rig.Shell.SettingsButton);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsSettingsOpen, Is.True);
            Assert.That(rig.Shell.IsSheetOpen, Is.True);
            Assert.That(rig.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Settings));
            Assert.That(rig.Root.Q<Label>("panel-title").text, Is.EqualTo("Settings"));
            Click(rig.Shell.SettingsButton);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsSettingsOpen, Is.False);
            Assert.That(rig.Shell.IsSheetOpen, Is.False);
            Assert.That(ShellPanelIds.IsKnown(ShellPanelIds.Settings), Is.True);
        }

        [UnityTest]
        public IEnumerator OpeningSettingsReplacesAnOpenTabOrSlotSheetAndATabReplacesSettings()
        {
            yield return Open(ShellScreens.All().First());
            rig.Shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Market));
            rig.Shell.OpenSettings();
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Settings));
            Assert.That(rig.Shell.IsSettingsOpen, Is.True);

            rig.Shell.OpenSlot(0, SlotCondition.Empty);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsSettingsOpen, Is.False, "a slot sheet replaces Settings");
            Assert.That(rig.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Build));

            rig.Shell.OpenSettings();
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Settings), "Settings replaces the slot sheet");
            Assert.That(rig.Shell.HandleBack(), Is.True);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsSheetOpen, Is.False, "one Back closes Settings and the whole sheet");
        }

        [UnityTest]
        public IEnumerator TheRadioGroupFollowsTheGearInFocusOrderInThreeLabelledChoices()
        {
            yield return Open(ShellScreens.All().First());
            rig.Shell.OpenSettings();
            yield return ShellRig.Settle();
            var order = rig.Shell.GetFocusOrder().ToList();
            var gear = order.IndexOf(rig.Shell.SettingsButton);
            var follow = order.IndexOf(presenter.FollowRadio);
            var on = order.IndexOf(presenter.OnRadio);
            var off = order.IndexOf(presenter.OffRadio);
            Assert.That(new[] { gear, follow, on, off }, Is.Ordered.Ascending.And.All.GreaterThanOrEqualTo(0), "gear, then Follow system, On, Off");
            Assert.That(presenter.GroupLabel.text, Is.EqualTo("Reduced motion"));
            Assert.That(presenter.FollowRadio.Q<Label>().text, Does.StartWith("Follow system"));
            Assert.That(presenter.OnRadio.Q<Label>().text, Does.StartWith("On"));
            Assert.That(presenter.OffRadio.Q<Label>().text, Does.StartWith("Off"));
            Assert.That(presenter.FollowRadio.ClassListContains("set-radio--selected"), Is.True, "no file: Follow system");
            Assert.That(presenter.FollowRadio.Q<Label>().text, Does.Contain("selected"), "the selected state is text, not colour alone");
            Assert.That(presenter.RadioGroup.tooltip, Is.EqualTo("Reduced motion"));
        }

        [UnityTest]
        public IEnumerator EscapeAndAndroidBackCloseSettingsAndFocusReturnsToTheGear()
        {
            yield return Open(ShellScreens.All().First());
            foreach (var round in new[] { 1, 2 })
            {
                rig.Shell.OpenSettings();
                yield return ShellRig.Settle();
                presenter.OnRadio.Focus();
                yield return null;
                Assert.That(Focused(rig.Root), Is.SameAs(presenter.OnRadio));
                // Escape and Android Back are one input (the Input System reports Back as Escape); the navigation adapter calls HandleBack.
                Assert.That(rig.Shell.HandleBack(), Is.True);

                yield return ShellRig.Settle();
                Assert.That(rig.Shell.IsSettingsOpen, Is.False, "Escape / Android Back");
                Assert.That(Focused(rig.Root), Is.SameAs(rig.Shell.SettingsButton), "focus returns to the gear");
            }
        }

        [UnityTest]
        public IEnumerator ThePanelCloseButtonAlsoReturnsFocusToTheGear()
        {
            yield return Open(ShellScreens.All().First());
            rig.Shell.OpenSettings();
            yield return ShellRig.Settle();
            presenter.OffRadio.Focus();
            yield return null;
            Click(rig.Shell.CloseButton);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsSettingsOpen, Is.False);
            Assert.That(Focused(rig.Root), Is.SameAs(rig.Shell.SettingsButton));
        }

        [UnityTest]
        public IEnumerator ADialogAndAPinnedValueCloseBeforeSettingsDoes()
        {
            yield return Open(ShellScreens.All().First());
            rig.Shell.OpenSettings();
            yield return ShellRig.Settle();
            var closed = ConfirmationCloseReason.Confirmed;
            Assert.That(rig.Shell.RequestConfirmation(new ConfirmationRequest { Title = "Test", Message = "m", OnCancel = reason => closed = reason }), Is.True);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.HandleBack(), Is.True);
            yield return ShellRig.Settle();
            Assert.That(closed, Is.EqualTo(ConfirmationCloseReason.Back));
            Assert.That(rig.Shell.IsSettingsOpen, Is.True, "Back closed the dialog first");
            Assert.That(rig.Shell.HandleBack(), Is.True);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsSettingsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator EveryControlIsAtLeastFortyFourPointsAndLaysOutInsideTheSheetAtEveryClassAndTextScale()
        {
            foreach (var screen in ShellScreens.All())
            {
                foreach (var percent in new[] { 100, 200 })
                {
                    yield return Open(screen, percent);
                    rig.Shell.OpenSettings();
                    yield return ShellRig.Settle();
                    var context = screen + " @" + percent;
                    AssertShellLayout(rig, "settings open " + context);
                    foreach (var radio in new[] { presenter.FollowRadio, presenter.OnRadio, presenter.OffRadio })
                    {
                        Assert.That(radio.worldBound.height, Is.GreaterThanOrEqualTo(43.5f), context + " " + radio.name);
                        Assert.That(radio.worldBound.width, Is.GreaterThanOrEqualTo(43.5f), context + " " + radio.name);
                    }

                    var scroll = rig.Root.Q<ScrollView>("panel-scroll");
                    AssertScrollContentClipped(scroll, context);
                    AssertTextAndTargets(rig.Root, rig.SafeRect(), context);
                    rig.Dispose();
                    rigs.Remove(rig);
                    registration?.Dispose();
                    registration = null;
                }
            }
        }

        [UnityTest]
        public IEnumerator TheLiveLineFollowsTheStateTable()
        {
            // No stored file, system reduces motion.
            yield return Open(ShellScreens.All().First(), 100, systemReduces: true);
            rig.Shell.OpenSettings();
            yield return ShellRig.Settle();
            Assert.That(presenter.EffectiveLine.text, Is.EqualTo("Following system: reduced motion on"));
            Assert.That(presenter.NotSavedNote.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));

            harness.SystemAvailable = false;
            harness.Signal.Reduce = false;
            rig.Shell.CloseSettings();
            rig.Shell.OpenSettings();
            yield return ShellRig.Settle();
            Assert.That(presenter.EffectiveLine.text, Is.EqualTo("Following system: not available, motion on"));

            Click(presenter.OnRadio);
            yield return ShellRig.Settle();
            Assert.That(presenter.EffectiveLine.text, Is.EqualTo("Reduced motion on (your choice)"));
            Assert.That(harness.Clock.ReducedMotion, Is.True);
            Assert.That(presenter.OnRadio.ClassListContains("set-radio--selected"), Is.True);
            Assert.That(presenter.FollowRadio.ClassListContains("set-radio--selected"), Is.False);

            Click(presenter.OffRadio);
            yield return ShellRig.Settle();
            Assert.That(presenter.EffectiveLine.text, Is.EqualTo("Reduced motion off (your choice)"));
            Assert.That(harness.Clock.ReducedMotion, Is.False);
            Assert.That(File.ReadAllText(harness.Store.FilePath), Does.Contain("\"off\""));
        }

        [UnityTest]
        public IEnumerator AFailedWriteShowsTheInlineNotSavedNoteUntilTheNextSuccessfulWrite()
        {
            yield return Open(ShellScreens.All().First());
            // A file where the Preferences directory belongs makes the write fail while the choice still applies.
            var preferences = Path.GetDirectoryName(harness.Store.FilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(preferences));
            File.WriteAllText(preferences, "blocker");
            rig.Shell.OpenSettings();
            yield return ShellRig.Settle();

            Click(presenter.OnRadio);
            yield return ShellRig.Settle();
            Assert.That(harness.Store.HasUnsavedChoice, Is.True);
            Assert.That(presenter.NotSavedNote.text, Does.StartWith("Not saved"));
            Assert.That(presenter.NotSavedNote.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(presenter.EffectiveLine.text, Is.EqualTo("Reduced motion on (your choice)"), "the choice applies in memory");
            Assert.That(harness.Clock.ReducedMotion, Is.True);
            Assert.That(presenter.OnRadio.ClassListContains("set-radio--selected"), Is.True);

            rig.Shell.CloseSettings();
            rig.Shell.OpenSettings();
            yield return ShellRig.Settle();
            Assert.That(presenter.NotSavedNote.text, Does.StartWith("Not saved"), "the note stays until a write succeeds");
            AssertTextAndTargets(rig.Root, rig.SafeRect(), "not saved");

            File.Delete(preferences);
            Click(presenter.OffRadio);
            yield return ShellRig.Settle();
            Assert.That(harness.Store.HasUnsavedChoice, Is.False, "the next change retries the write");
            Assert.That(presenter.NotSavedNote.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        [UnityTest]
        public IEnumerator ArrowKeysMoveTheChoiceAndTheFocusWithinTheGroup()
        {
            yield return Open(ShellScreens.All().First());
            rig.Shell.OpenSettings();
            yield return ShellRig.Settle();
            presenter.FollowRadio.Focus();
            yield return null;
            Key(presenter.FollowRadio, KeyCode.DownArrow);
            yield return null;
            Assert.That(harness.Store.Preference, Is.EqualTo(ReducedMotionPreference.On));
            Assert.That(Focused(rig.Root), Is.SameAs(presenter.OnRadio));
            Key(presenter.OnRadio, KeyCode.UpArrow);
            yield return null;
            Assert.That(harness.Store.Preference, Is.EqualTo(ReducedMotionPreference.FollowSystem));
        }
    }
#endif
}
