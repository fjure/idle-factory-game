using System;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>What the Overdrive control shows for one status: the caption, icon key, reason and look of its four stages.</summary>
    public readonly struct OverdriveControlView
    {
        public OverdriveControlView(OverdriveStage stage, string caption, string iconKey, string reason, bool active, bool softDisabled)
        {
            Stage = stage;
            Caption = caption;
            IconKey = iconKey;
            Reason = reason;
            Active = active;
            SoftDisabled = softDisabled;
        }

        public OverdriveStage Stage { get; }

        /// <summary>"Locked", "Ready", "On m:ss" (Active, remaining time) or "Cool m:ss" (Cooling down, remaining time).</summary>
        public string Caption { get; }

        public string IconKey { get; }

        /// <summary>The player-readable state, also the tooltip and the notice a press of a disabled control shows.</summary>
        public string Reason { get; }

        /// <summary>The boost is running: the control uses the accent fill.</summary>
        public bool Active { get; }

        /// <summary>Everything but Ready: the control looks disabled and stays focusable (Art Bible 9.4), and a press shows the reason.</summary>
        public bool SoftDisabled { get; }
    }

    /// <summary>
    /// The Overdrive HUD control (T116, FR-019, FR-032; contracts/ui-surfaces.md, Overdrive control): Ready, Active with the remaining
    /// time, Cooling down with the remaining time, and locked until its unlock. The control is self-contained: it reads
    /// <see cref="RunSnapshots.Overdrive"/> and activates only through <see cref="RunCommands.ActivateOverdrive"/>. A press while it is
    /// locked, running or cooling down shows the reason next to the control and activates nothing (the command also re-validates).
    /// <para>
    /// Composition: the shell already shows this state in its top strip through <c>RunShellDocument.SetOverdrive</c> and
    /// <c>RunShellPresenter</c>, with the same captions. This component is the same control for any other host, such as the side-panel
    /// header of a tablet or desktop layout: <see cref="MountInto"/> a host element, call <see cref="Refresh"/> each frame while the
    /// host is shown (it redraws on a structure change and once per timeline second), and call <see cref="Bind"/> with the shell to
    /// route the reason and the result to its notice line. <see cref="Describe"/> is the pure mapping, shared with tests.
    /// </para>
    /// </summary>
    public sealed class OverdriveControl
    {
        private readonly RunSession session;
        private readonly RunCommands commands;
        private readonly Func<string, Sprite> icons;
        private readonly VisualElement icon;
        private readonly Label caption;
        private RunShellDocument shell;
        private OverdriveStatus status;
        private OverdriveControlView view;
        private long keyStructure = -1;
        private long keySecond = long.MinValue;
        private bool forceRefresh = true;

        /// <summary>Builds the control. <paramref name="icons"/> resolves an icon key (<c>F003-UI-ICON-OVERDRIVE-*</c>) to a sprite (optional).</summary>
        public OverdriveControl(RunSession session, RunCommands commands, Func<string, Sprite> icons = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.icons = icons;
            Button = new Button { name = "overdrive-control", focusable = true };
            Button.AddToClassList("run-control");
            Button.AddToClassList("run-overdrive");
            icon = new VisualElement { name = "overdrive-control-icon", pickingMode = PickingMode.Ignore };
            icon.AddToClassList("run-icon");
            icon.AddToClassList("run-icon--tint-text");
            caption = new Label { name = "overdrive-control-caption", pickingMode = PickingMode.Ignore };
            caption.AddToClassList("run-caption");
            Button.Add(icon);
            Button.Add(caption);
            Button.clicked += OnPressed;
            Refresh();
        }

        /// <summary>The control's button (a <c>run-control</c>: 44 pt minimum, shell focus ring).</summary>
        public Button Button { get; }

        public Label Caption => caption;
        public VisualElement Icon => icon;

        /// <summary>The state the control currently shows.</summary>
        public OverdriveControlView View => view;

        /// <summary>The latest status the control drew from.</summary>
        public OverdriveStatus Status => status;

        /// <summary>The reason shown for the current state, or the message of the last rejected press.</summary>
        public string Reason => view.Reason;

        /// <summary>Raised with the text a press produced: the reason of a disabled control, the rejection copy, or the activation notice.</summary>
        public event Action<string, ShellNoticeKind> Notice;

        /// <summary>Routes the notices to the shell's notice line.</summary>
        public void Bind(RunShellDocument shell) => this.shell = shell;

        /// <summary>Adds the control to a host element (for example the side-panel header slot). A second call moves it.</summary>
        public void MountInto(VisualElement host)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            Button.RemoveFromHierarchy();
            host.Add(Button);
        }

        /// <summary>Forces the next <see cref="Refresh"/> to redraw.</summary>
        public void Invalidate() => forceRefresh = true;

        /// <summary>Redraws on a structure change and once per timeline second while a boost or a cooldown counts down.</summary>
        public void Refresh()
        {
            var second = session.TimelineUtcMs / 1000L;
            var timed = status != null && (status.State == OverdriveStage.Active || status.State == OverdriveStage.CoolingDown);
            if (!forceRefresh && session.StructureRevision == keyStructure && !(timed && second != keySecond))
            {
                return;
            }

            forceRefresh = false;
            keyStructure = session.StructureRevision;
            keySecond = second;
            Apply(RunSnapshots.Overdrive(session));
        }

        /// <summary>Presses the control as a tap or Enter does.</summary>
        public void Press() => OnPressed();

        private void Apply(OverdriveStatus next)
        {
            status = next;
            view = Describe(next);
            caption.text = view.Caption;
            var sprite = icons?.Invoke(view.IconKey);
            icon.style.backgroundImage = sprite == null ? new StyleBackground(StyleKeyword.None) : new StyleBackground(sprite);
            Button.EnableInClassList("run-overdrive--active", view.Active);
            RunShellDocument.SetSoftDisabled(Button, view.SoftDisabled);
            Button.tooltip = view.Reason;
        }

        private void OnPressed()
        {
            Refresh();
            if (view.SoftDisabled)
            {
                Raise(view.Reason, ShellNoticeKind.Warning);
                return;
            }

            var result = commands.ActivateOverdrive();
            Invalidate();
            if (!result.IsSuccess)
            {
                Refresh();
                Raise(RunRejectionCopy.For(result.ReasonCode), ShellNoticeKind.Warning);
                return;
            }

            Refresh();
            Raise("Overdrive is on for " + RunRejectionCopy.Countdown(result.Value.ActiveUntilUtcMs - result.Value.StartUtcMs) + ".", ShellNoticeKind.Info);
        }

        private void Raise(string text, ShellNoticeKind kind)
        {
            shell?.ShowNotice(text, kind);
            Notice?.Invoke(text, kind);
        }

        /// <summary>Maps a status to the control's look: the same captions and reasons as the shell's top-strip control.</summary>
        public static OverdriveControlView Describe(OverdriveStatus status)
        {
            if (status == null || status.State == OverdriveStage.Locked)
            {
                return new OverdriveControlView(OverdriveStage.Locked, "Locked", "F003-UI-ICON-OVERDRIVE-LOCKED",
                    RunRejectionCopy.OverdriveLocked(status), false, true);
            }

            switch (status.State)
            {
                case OverdriveStage.Active:
                    return new OverdriveControlView(OverdriveStage.Active, "On " + RunRejectionCopy.Countdown(status.RemainingMs),
                        "F003-UI-ICON-OVERDRIVE-ACTIVE",
                        RunRejectionCopy.For(RunReasons.OverdriveActive) + " " + RunRejectionCopy.Countdown(status.RemainingMs) + " left.", true, true);
                case OverdriveStage.CoolingDown:
                    return new OverdriveControlView(OverdriveStage.CoolingDown, "Cool " + RunRejectionCopy.Countdown(status.RemainingMs),
                        "F003-UI-ICON-OVERDRIVE-COOLDOWN",
                        RunRejectionCopy.For(RunReasons.OverdriveCoolingDown) + " Ready in " + RunRejectionCopy.Countdown(status.RemainingMs) + ".", false, true);
                default:
                    return new OverdriveControlView(OverdriveStage.Ready, "Ready", "F003-UI-ICON-OVERDRIVE-READY", "Overdrive is ready.", false, false);
            }
        }
    }
}
