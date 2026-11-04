#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Run.Domain;
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
    /// T116 (FR-019, FR-032, US3, UX-3): the Overdrive HUD control in its four stages (locked, ready, active with the remaining time, cooling
    /// down with the remaining time), its activation through the application, the reason of a disabled press, the countdown, the match with the
    /// shell's own top-strip control, and the layout in the shell at every screen class (rotations included) at 100 and 200 percent text scale.
    /// </summary>
    public sealed class OverdriveControlTests
    {
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;
        private RunSession session;
        private RunCommands commands;
        private OverdriveControl control;
        private readonly List<string> notices = new List<string>();

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        public static IEnumerable<int> Percents() => new[] { 100, 200 };

        public static IEnumerable<OverdriveStage> Stages() =>
            new[] { OverdriveStage.Locked, OverdriveStage.Ready, OverdriveStage.Active, OverdriveStage.CoolingDown };

        [TearDown]
        public void TearDown()
        {
            foreach (var item in rigs)
            {
                item.Dispose();
            }

            rigs.Clear();
            rig = null;
            control = null;
            notices.Clear();
        }

        private IEnumerator Open(ShellScreen screen, int percent = 100, bool unlocked = true, bool bindShell = true,
            IdleFactory.Unity.Presentation.ShellDeviceProfile? profile = null)
        {
            rig = ShellRig.Create(screen, profile, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            session = NewSession(out commands, unlocked);
            control = new OverdriveControl(session, commands, Icon);
            if (bindShell)
            {
                control.Bind(rig.Shell);
            }

            control.Notice += (text, kind) => notices.Add(text);
            // The control sits in the open sheet's header, where the tablet and desktop layouts put the Overdrive control (UX-1).
            rig.Shell.Panels.Register(new TestShellPanel(ShellPanelIds.Market, "Market", 2, false, true));
            rig.Shell.SelectTab(ShellTab.Market);
            MountControl();
            rig.Shell.OverdriveButton.style.display = DisplayStyle.None;
            yield return ShellRig.Settle();
        }

        /// <summary>Replaces the mounted control with a new one over a session, so the next stage is drawn from scratch.</summary>
        private void UseControl(RunSession forSession, RunCommands forCommands)
        {
            foreach (var other in rig.Shell.Root.Query<Button>("overdrive-control").ToList())
            {
                other.RemoveFromHierarchy();
            }

            control = new OverdriveControl(forSession, forCommands, Icon);
            control.Bind(rig.Shell);
            MountControl();
        }

        /// <summary>Mounts the control into its host. In the top strip it sits before the gear (T433: the gear is the last strip element).</summary>
        private void MountControl()
        {
            control.MountInto(HostFor(rig));
            if (!rig.Shell.IsSidePanelLayout && rig.Shell.SettingsButton != null)
            {
                control.Button.PlaceBehind(rig.Shell.SettingsButton);
            }
        }

        /// <summary>The side-panel header on tablet and desktop, the top strip on a phone (UX-1): the header slot is not shown on a phone.</summary>
        private static VisualElement HostFor(ShellRig forRig) =>
            forRig.Shell.IsSidePanelLayout ? forRig.Shell.Root.Q<VisualElement>("panel-header-slot") : forRig.Shell.TopStrip;

        // Content values come from the shipped catalog (T344), never from a typed revision.
        private static long BoostMs => RunShellFixtures.OverdriveDurationMs;
        private static long CooldownMs => RunShellFixtures.OverdriveCooldownMs;
        private static string UnlockCopy => "Overdrive unlocks at factory level " + RunShellFixtures.OverdriveUnlockLevel + ".";
        private static string Clock(long ms) => RunShellFixtures.Clock(ms);

        private static string StateName(OverdriveStage stage) => stage.ToString();

        // ---------------------------------------------------------------------------------------------------
        // The four stages
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void EachStageHasItsCaptionIconAndReason()
        {
            var locked = OverdriveControl.Describe(LockedStatus());
            Assert.That(locked.Caption, Is.EqualTo("Locked"));
            Assert.That(locked.IconKey, Is.EqualTo("F003-UI-ICON-OVERDRIVE-LOCKED"));
            Assert.That(locked.Reason, Is.EqualTo(UnlockCopy));
            Assert.That(locked.SoftDisabled, Is.True);
            Assert.That(locked.Active, Is.False);

            var ready = OverdriveControl.Describe(RunShellFixtures.OverdriveStatusIn(OverdriveStage.Ready));
            Assert.That(ready.Caption, Is.EqualTo("Ready"));
            Assert.That(ready.IconKey, Is.EqualTo("F003-UI-ICON-OVERDRIVE-READY"));
            Assert.That(ready.SoftDisabled, Is.False);

            var active = OverdriveControl.Describe(RunShellFixtures.OverdriveStatusIn(OverdriveStage.Active));
            Assert.That(active.Caption, Is.EqualTo("On " + Clock(BoostMs)), "active with the remaining time");
            Assert.That(active.IconKey, Is.EqualTo("F003-UI-ICON-OVERDRIVE-ACTIVE"));
            Assert.That(active.Active, Is.True);
            Assert.That(active.SoftDisabled, Is.True);
            Assert.That(active.Reason, Is.EqualTo("Overdrive is already running. " + Clock(BoostMs) + " left."));

            var cooling = OverdriveControl.Describe(RunShellFixtures.OverdriveStatusIn(OverdriveStage.CoolingDown));
            Assert.That(cooling.Caption, Is.EqualTo("Cool " + Clock(CooldownMs - 1_000)), "cooling down with the remaining time");
            Assert.That(cooling.IconKey, Is.EqualTo("F003-UI-ICON-OVERDRIVE-COOLDOWN"));
            Assert.That(cooling.SoftDisabled, Is.True);
            Assert.That(cooling.Reason, Is.EqualTo("Overdrive is cooling down. Ready in " + Clock(CooldownMs - 1_000) + "."));

            Assert.That(OverdriveControl.Describe(null).Stage, Is.EqualTo(OverdriveStage.Locked));
        }

        [UnityTest]
        public IEnumerator TheControlShowsTheSameStateAsTheShellsTopStripControl([ValueSource(nameof(Stages))] OverdriveStage stage)
        {
            yield return Open(ShellScreens.All().First(), unlocked: stage != OverdriveStage.Locked);
            var status = stage == OverdriveStage.Locked ? LockedStatus() : RunShellFixtures.OverdriveStatusIn(stage);
            rig.Shell.SetOverdrive(status);
            var shellCaption = rig.Shell.Root.Q<Label>("overdrive-caption").text;
            var view = OverdriveControl.Describe(status);
            Assert.That(view.Caption, Is.EqualTo(shellCaption), StateName(stage));
            Assert.That(view.Reason, Is.EqualTo(rig.Shell.OverdriveReason), StateName(stage));
            Assert.That(view.SoftDisabled, Is.EqualTo(RunShellDocument.IsSoftDisabled(rig.Shell.OverdriveButton)), StateName(stage));
            Assert.That(view.Active, Is.EqualTo(rig.Shell.OverdriveButton.ClassListContains("run-overdrive--active")), StateName(stage));
        }

        [UnityTest]
        public IEnumerator ALockedControlShowsLockedAndUnlocksWhenTheConditionIsMet()
        {
            yield return Open(ShellScreens.All().First(), unlocked: false);
            Assert.That(control.View.Stage, Is.EqualTo(OverdriveStage.Locked));
            Assert.That(control.Caption.text, Is.EqualTo("Locked"));
            Assert.That(RunShellDocument.IsSoftDisabled(control.Button), Is.True);
            Assert.That(control.Button.tooltip, Is.EqualTo(UnlockCopy));

            Assert.That(session.State.TryUnlock(RunState.OverdriveUnlockId), Is.EqualTo(RunStateResult.Ok));
            control.Invalidate();
            control.Refresh();
            Assert.That(control.View.Stage, Is.EqualTo(OverdriveStage.Ready));
            Assert.That(control.Caption.text, Is.EqualTo("Ready"));
            Assert.That(RunShellDocument.IsSoftDisabled(control.Button), Is.False);
        }

        [UnityTest]
        public IEnumerator EveryStageDrawsItsIconAndTheActiveStageUsesTheAccentFill()
        {
            yield return Open(ShellScreens.All().First());
            Assert.That(control.Icon.resolvedStyle.backgroundImage.sprite, Is.Not.Null, "ready icon");
            Assert.That(control.Button.ClassListContains("run-overdrive--active"), Is.False);
            control.Press();
            yield return ShellRig.Settle();
            Assert.That(control.View.Stage, Is.EqualTo(OverdriveStage.Active));
            Assert.That(control.Icon.resolvedStyle.backgroundImage.sprite, Is.Not.Null, "active icon");
            Assert.That(control.Button.ClassListContains("run-overdrive--active"), Is.True);
            Assert.That(control.Caption.text, Is.EqualTo("On " + Clock(BoostMs)), "the caption also says it, so the fill is never the only cue");
        }

        // ---------------------------------------------------------------------------------------------------
        // Activation, reasons and the countdown
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator PressingAReadyControlActivatesOverdriveThroughTheApplication([Values(false, true)] bool viaSubmit)
        {
            yield return Open(ShellScreens.All().First());
            Assert.That(session.State.Overdrive.IsActive(session.TimelineUtcMs), Is.False);
            control.Button.Focus();
            if (viaSubmit)
            {
                Submit(control.Button);
            }
            else
            {
                control.Press();
            }

            yield return ShellRig.Settle();
            Assert.That(session.State.Overdrive.IsActive(session.TimelineUtcMs), Is.True);
            Assert.That(control.View.Stage, Is.EqualTo(OverdriveStage.Active));
            Assert.That(control.Caption.text, Is.EqualTo("On " + Clock(BoostMs)));
            Assert.That(notices, Is.EqualTo(new[] { "Overdrive is on for " + Clock(BoostMs) + "." }));
            Assert.That(rig.Shell.NoticeText, Is.EqualTo("Overdrive is on for " + Clock(BoostMs) + "."));
        }

        [UnityTest]
        public IEnumerator OverdriveWorksWithATouchTapAndWithAPointerClick([Values(true, false)] bool touch)
        {
            var screen = ShellScreens.All().First();
            yield return Open(screen, 100, true, true, InputModeSupport.ProfileFor(screen, touch));
            var where = touch ? "touch" : "pointer";
            InputModeSupport.AssertTargets(new VisualElement[] { control.Button }, where);
            Assert.That(control.Button.tooltip, Is.Not.Empty, where + ": the state is in the accessible name, not in a hover");

            yield return InputModeSupport.Tap(control.Button, touch);
            yield return ShellRig.Settle();
            Assert.That(session.State.Overdrive.IsActive(session.TimelineUtcMs), Is.True, where + ": the press started Overdrive");
            Assert.That(control.View.Stage, Is.EqualTo(OverdriveStage.Active));
            Assert.That(rig.Shell.NoticeText, Is.EqualTo("Overdrive is on for " + Clock(BoostMs) + "."));

            // A second press while it runs states the reason and restarts nothing.
            var until = session.State.Overdrive.ActiveUntilUtcMs;
            yield return InputModeSupport.Tap(control.Button, touch);
            yield return ShellRig.Settle();
            Assert.That(session.State.Overdrive.ActiveUntilUtcMs, Is.EqualTo(until), where + ": not restarted");
            Assert.That(notices.Last(), Does.StartWith("Overdrive is already running."));
        }

        [UnityTest]
        public IEnumerator APressWhileActiveCoolingDownOrLockedShowsTheReasonAndActivatesNothing()
        {
            yield return Open(ShellScreens.All().First(), unlocked: false);
            control.Press();
            Assert.That(notices.Last(), Is.EqualTo(UnlockCopy));
            Assert.That(session.State.Overdrive.ActiveUntilUtcMs.HasValue, Is.False, "locked: nothing started");

            Assert.That(session.State.TryUnlock(RunState.OverdriveUnlockId), Is.EqualTo(RunStateResult.Ok));
            control.Invalidate();
            control.Press();
            var activeUntil = session.State.Overdrive.ActiveUntilUtcMs;
            Assert.That(activeUntil.HasValue, Is.True);

            Advance(session, 4_000);
            control.Refresh();
            control.Press();
            Assert.That(notices.Last(), Is.EqualTo("Overdrive is already running. " + Clock(BoostMs - 4_000) + " left."));
            Assert.That(session.State.Overdrive.ActiveUntilUtcMs, Is.EqualTo(activeUntil), "an active Overdrive is not restarted");

            Advance(session, BoostMs - 4_000);
            control.Refresh();
            Assert.That(control.View.Stage, Is.EqualTo(OverdriveStage.CoolingDown));
            control.Press();
            Assert.That(notices.Last(), Is.EqualTo("Overdrive is cooling down. Ready in " + Clock(CooldownMs) + "."));
            Assert.That(session.State.Overdrive.ActiveUntilUtcMs, Is.EqualTo(activeUntil));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheRemainingTimeCountsDownOncePerSecondThroughActiveCooldownAndReady()
        {
            yield return Open(ShellScreens.All().First());
            control.Press();
            Assert.That(control.Caption.text, Is.EqualTo("On " + Clock(BoostMs)));

            Advance(session, 1_000);
            control.Refresh();
            Assert.That(control.Caption.text, Is.EqualTo("On " + Clock(BoostMs - 1_000)));

            Advance(session, 600);
            control.Refresh();
            Assert.That(control.Caption.text, Is.EqualTo("On " + Clock(BoostMs - 1_000)), "the same timeline second keeps the same caption");

            Advance(session, BoostMs - 1_600);
            control.Refresh();
            Assert.That(control.Caption.text, Is.EqualTo("Cool " + Clock(CooldownMs)), "the boost ended and the full cooldown starts");

            var partOfCooldown = CooldownMs * 2 / 5;
            Advance(session, partOfCooldown);
            control.Refresh();
            Assert.That(control.Caption.text, Is.EqualTo("Cool " + Clock(CooldownMs - partOfCooldown)));

            Advance(session, CooldownMs - partOfCooldown);
            control.Refresh();
            Assert.That(control.View.Stage, Is.EqualTo(OverdriveStage.Ready));
            Assert.That(control.Caption.text, Is.EqualTo("Ready"));
            Assert.That(RunShellDocument.IsSoftDisabled(control.Button), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheControlIsFocusableAndTheCommandStillRevalidatesAStalePress()
        {
            yield return Open(ShellScreens.All().First(), bindShell: false);
            Assert.That(control.Button.focusable, Is.True);
            Assert.That(control.Button.ClassListContains("run-control"), Is.True);

            // The control last drew Ready; the run activated Overdrive behind its back. The press re-reads the state and rejects.
            Assert.That(commands.ActivateOverdrive().IsSuccess, Is.True);
            control.Press();
            Assert.That(notices.Last(), Does.StartWith("Overdrive is already running."));
            Assert.That(rig.Shell.NoticeText, Is.Not.EqualTo(notices.Last()), "unbound: the shell notice line is not used");
            yield return null;
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout (UX-3, F-4): every stage at every screen class, rotations included, at 100 and 200 percent
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryStageStaysContainedAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            foreach (var stage in Stages())
            {
                switch (stage)
                {
                    case OverdriveStage.Locked:
                        // A fresh locked run drawn through the same control.
                        var locked = RunShellFixtures.NewSession(out var lockedCommands, factoryLevel: 1, cash: 25, unlockEverything: false);
                        UseControl(locked, lockedCommands);
                        break;
                    case OverdriveStage.Ready:
                        UseControl(session, commands);
                        break;
                    case OverdriveStage.Active:
                        UseControl(session, commands);
                        Assert.That(commands.ActivateOverdrive().IsSuccess, Is.True);
                        break;
                    default:
                        Advance(session, BoostMs + 1_000);
                        UseControl(session, commands);
                        break;
                }

                control.Refresh();
                yield return ShellRig.Settle();
                Assert.That(control.View.Stage, Is.EqualTo(stage));
                AssertShellLayout(rig, "overdrive " + stage);
                var button = control.Button;
                Assert.That(IsDisplayed(button), Is.True);
                var safe = rig.SafeRect();
                AssertWithin(safe, button.worldBound, $"{screen} @{percent}%: the {stage} control stays in the safe area");
                AssertWithin((rig.Shell.IsSidePanelLayout ? rig.Shell.PanelHost : rig.Shell.TopStrip).worldBound, button.worldBound, $"{screen} @{percent}%: the {stage} control stays in its host");
                Assert.That(button.worldBound.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance));
                Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance));
                button.Focus();
                yield return ShellRig.Settle();
                Assert.That(Focused(button), Is.SameAs(button), $"{screen} @{percent}%: {stage} takes focus");
                Assert.That(button.ClassListContains("run-control--focused"), Is.True);
                AssertShellLayout(rig, "overdrive " + stage + " focused");
            }
        }
    }
}
#endif
