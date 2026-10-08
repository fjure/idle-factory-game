using System;
using IdleFactory.Core.Application;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>What a <see cref="StartupStatusPanel"/> currently shows.</summary>
    public enum StartupStatusKind
    {
        /// <summary>Nothing: the run is live, or the surface was not shown.</summary>
        Hidden = 0,

        /// <summary>"Importing your development save…" with a spinner (FR-028).</summary>
        Importing = 1,

        /// <summary>The V1 import failed a rule: the failed rule's copy with Retry and a confirmed Start new run (OD-3).</summary>
        ImportBlocked = 2,

        /// <summary>A transient store failure interrupted the cold start before anything was decided; Retry runs the whole start again.</summary>
        StartRetry = 3,

        /// <summary>
        /// The V2 checkpoint could not be loaded (damaged, another build's save, a failed return) and the lifecycle is <c>ReturnBlocked</c>:
        /// the Feature 001 recovery, Retry, the previous generation, and the confirmed discard of a damaged sole save.
        /// </summary>
        ReturnBlocked = 4,
    }

    /// <summary>
    /// The start-up and import status surface (T118, FR-028, FR-032, US5 scenarios 3 and 4; contracts/ui-surfaces.md, Startup / import
    /// status; contracts/save-v2-and-v1-import.md). It covers the screen until <c>RunPersistence</c> reaches <c>Running</c> and shows
    /// the state the application reports, never anything taken from a file: the importing message with a spinner; a blocked import with
    /// the failed rule's copy, <b>Retry</b> (<c>ColdStart</c> again) and a confirmed <b>Start new run</b>
    /// (<c>StartNewRunAfterBlockedImport</c>, OD-3); and, for a V2 checkpoint that could not be loaded, the Feature 001 recovery:
    /// Retry, <b>Use previous save</b> (<c>RecoverPreviousCheckpoint</c>) and a confirmed discard of a damaged sole save
    /// (<c>DiscardDamagedSaveAndRestart</c>), which is offered only when the application says it is eligible.
    ///
    /// The V1 files are never touched: the panel only calls the application commands, and after a confirmed Start new run the V1 save is
    /// no longer offered because a V2 checkpoint exists. A rotation cancels a pending confirmation without running its command and V1
    /// stays offered (UX-6, UX-9): <see cref="CancelForRotation"/> is the callback the composition registers with
    /// <c>CoreGameFoundationBootstrap.RegisterTransientAction</c>.
    ///
    /// Confirmations follow the shell's rules (UX-2): the dialog traps focus, opens with focus on Cancel, returns focus to the control that
    /// opened it, and treats Escape and Android Back as Cancel. Under a shell the surface uses the shell's shared dialog; before the shell
    /// exists (a blocked start), which is the normal case here, it draws the same dialog itself.
    /// </summary>
    public sealed class StartupStatusPanel : StatusSurface
    {
        public const string SurfaceName = "startup-status";
        public const string ImportingText = "Importing your development save…";
        public const string ImportBlockedTitle = "Your development save could not be imported";
        public const string StartRetryTitle = "The saved factory could not be checked";
        public const string ReturnBlockedTitle = "The saved factory could not be loaded";
        public const string RetryLabel = "Retry";
        public const string StartNewRunLabel = "Start new run";
        public const string UsePreviousLabel = "Use previous save";
        public const string DiscardLabel = "Discard damaged save";

        public const string StartNewRunConfirmTitle = "Start a new run?";
        public const string StartNewRunConfirmMessage =
            "A fresh first run starts and is saved. Your development save files stay unchanged, but they are no longer offered for import.";
        public const string DiscardConfirmTitle = "Discard the damaged save?";
        public const string DiscardConfirmMessage =
            "The damaged saved factory is discarded and a new run starts. If a development save exists it is imported once; your development save files stay unchanged.";

        public const string ImportBlockedHelp =
            "Retry tries the import again. Start new run begins a fresh run; your development save files stay unchanged and are no longer offered for import.";
        public const string StartRetryText = "Checkpoint storage was not available, so nothing was decided yet and nothing was changed. Retry checks the saved factory again.";
        public const string StillBlockedText = "Still blocked. Nothing was changed.";
        public const string SelectedDurableText = "A saved factory was found and loaded instead.";

        private readonly RunPersistence persistence;
        private readonly ILegacyCheckpointSource legacySource;
        private readonly Label message;
        private readonly Label help;
        private readonly Label feedback;
        private readonly VisualElement spinner;
        private readonly VisualElement confirmLayer;
        private readonly VisualElement confirmCard;
        private readonly Label confirmTitle;
        private readonly Label confirmMessage;
        private readonly ScrollView confirmScroll;
        private readonly IVisualElementScheduledItem spinnerTimer;
        private StartupStatusKind kind;
        private string blockedCode;
        private bool canDiscard;
        private bool inlineOpen;
        private bool shellDialogOpen;
        private ConfirmationRequest inlineRequest;
        private VisualElement inlineInvoker;
        private float spinnerAngle;

        /// <summary>
        /// Builds the surface. <paramref name="persistence"/> is the run's persistence (its lifecycle and recovery commands are the only seams
        /// used); <paramref name="legacySource"/> is the V1 source the retry and the discard hand to it (null: no V1); <paramref name="styles"/> is
        /// <c>StartupStatusPanel.uss</c>.
        /// </summary>
        public StartupStatusPanel(RunPersistence persistence, ILegacyCheckpointSource legacySource, StyleSheet styles)
            : base(SurfaceName, styles)
        {
            this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            this.legacySource = legacySource;

            spinner = new VisualElement { name = "startup-spinner", pickingMode = PickingMode.Ignore };
            spinner.AddToClassList("status-spinner");
            message = MakeLabel("startup-message", "status-line--lead");
            message.AddToClassList("status-line");
            help = MakeLabel("startup-help", "status-line");
            feedback = MakeLabel("startup-feedback", "status-line");
            feedback.AddToClassList("status-line--warning");
            var spinnerRow = new VisualElement { name = "startup-spinner-row" };
            spinnerRow.AddToClassList("status-spinner-row");
            spinnerRow.Add(spinner);
            Content.Add(spinnerRow);
            Content.Add(message);
            Content.Add(help);
            Content.Add(feedback);
            SpinnerRow = spinnerRow;

            RetryButton = RunShellDocument.CreateButton("startup-retry", RetryLabel, true);
            StartNewRunButton = RunShellDocument.CreateButton("startup-start-new-run", StartNewRunLabel);
            UsePreviousButton = RunShellDocument.CreateButton("startup-use-previous", UsePreviousLabel);
            DiscardButton = RunShellDocument.CreateButton("startup-discard", DiscardLabel);
            RetryButton.clicked += OnRetry;
            StartNewRunButton.clicked += OnStartNewRun;
            UsePreviousButton.clicked += OnUsePrevious;
            DiscardButton.clicked += OnDiscard;
            foreach (var button in new[] { RetryButton, UsePreviousButton, StartNewRunButton, DiscardButton })
            {
                Actions.Add(button);
                SetDisplayed(button, false);
            }

            SetDisplayed(SpinnerRow, false);
            SetDisplayed(message, false);
            SetDisplayed(help, false);
            SetDisplayed(feedback, false);

            // The surface's own confirmation dialog, used while there is no shell (UX-2 behaviour, identical to the shell's).
            confirmLayer = new VisualElement { name = "startup-confirm-layer" };
            confirmLayer.AddToClassList("status-confirm-layer");
            confirmLayer.style.display = DisplayStyle.None;
            confirmCard = new VisualElement { name = "startup-confirm" };
            confirmCard.AddToClassList("status-confirm");
            confirmTitle = MakeLabel("startup-confirm-title", "status-title");
            confirmScroll = new ScrollView(ScrollViewMode.Vertical) { name = "startup-confirm-scroll" };
            confirmScroll.AddToClassList("status-scroll");
            confirmMessage = MakeLabel("startup-confirm-message", "status-line");
            var confirmBody = new VisualElement { name = "startup-confirm-body" };
            confirmBody.AddToClassList("status-content");
            confirmBody.Add(confirmMessage);
            confirmScroll.Add(confirmBody);
            var confirmActions = new VisualElement { name = "startup-confirm-actions" };
            confirmActions.AddToClassList("status-actions");
            ConfirmCancelButton = RunShellDocument.CreateButton("startup-confirm-cancel", "Cancel");
            ConfirmButton = RunShellDocument.CreateButton("startup-confirm-confirm", "Confirm", true);
            ConfirmCancelButton.clicked += () => CloseInline(ConfirmationCloseReason.Cancelled);
            ConfirmButton.clicked += () => CloseInline(ConfirmationCloseReason.Confirmed);
            confirmActions.Add(ConfirmCancelButton);
            confirmActions.Add(ConfirmButton);
            confirmCard.Add(confirmTitle);
            confirmCard.Add(confirmScroll);
            confirmCard.Add(confirmActions);
            confirmLayer.Add(confirmCard);
            Root.Add(confirmLayer);
            confirmLayer.RegisterCallback<KeyDownEvent>(OnConfirmKeyDown, TrickleDown.TrickleDown);
            confirmLayer.RegisterCallback<NavigationMoveEvent>(OnConfirmNavigationMove, TrickleDown.TrickleDown);
            confirmLayer.RegisterCallback<NavigationCancelEvent>(OnConfirmNavigationCancel, TrickleDown.TrickleDown);
            confirmLayer.RegisterCallback<FocusOutEvent>(OnConfirmFocusOut);

            spinnerTimer = spinner.schedule.Execute(TickSpinner).Every(60);
            spinnerTimer.Pause();
        }

        public Button RetryButton { get; }
        public Button StartNewRunButton { get; }
        public Button UsePreviousButton { get; }
        public Button DiscardButton { get; }

        /// <summary>The surface's own confirmation dialog buttons (used while there is no shell).</summary>
        public Button ConfirmCancelButton { get; }

        public Button ConfirmButton { get; }
        public VisualElement ConfirmLayer => confirmLayer;
        public VisualElement ConfirmCard => confirmCard;
        public Label ConfirmTitleLabel => confirmTitle;
        public Label ConfirmMessageLabel => confirmMessage;
        public VisualElement Spinner => spinner;
        public VisualElement SpinnerRow { get; }
        public Label MessageLabel => message;
        public Label HelpLabel => help;
        public Label FeedbackLabel => feedback;

        /// <summary>What the surface shows now.</summary>
        public StartupStatusKind Kind => kind;

        /// <summary>The failed rule of a blocked import that is shown (one of <see cref="V1ImportCodes"/>), otherwise null.</summary>
        public string BlockedCode => kind == StartupStatusKind.ImportBlocked ? blockedCode : null;

        /// <summary>True while the spinner animates.</summary>
        public bool IsSpinning => spinnerTimer != null && spinnerTimer.isActive && spinner.resolvedStyle.display != DisplayStyle.None;

        /// <summary>True while a confirmation is open, the surface's own or the shell's.</summary>
        public bool IsConfirmationOpen => inlineOpen || shellDialogOpen;

        /// <summary>
        /// Raised when a recovery command completed and the lifecycle is <c>Running</c>. The composition then finishes the start
        /// (<c>CoreGameFoundationBootstrap.ResumeStartupAfterRecovery</c>), which composes the shell and the runtime.
        /// </summary>
        public event Action<RunCommandResult<RunPersistenceOutcome>> Completed;

        // ------------------------------------------------------------------------------------------------------
        // State
        // ------------------------------------------------------------------------------------------------------

        /// <summary>Shows "Importing your development save…" with the spinner (call before the cold start runs).</summary>
        public void ShowImporting()
        {
            Apply(StartupStatusKind.Importing, null);
        }

        /// <summary>
        /// Shows what the persistence layer reports now: nothing when the run is live, otherwise the importing, blocked-import, retry or
        /// recovery state. Call it after the cold start and after every recovery command.
        /// </summary>
        public StartupStatusKind Refresh()
        {
            var lifecycle = persistence.Lifecycle;
            if (lifecycle == LifecycleState.Running)
            {
                Hide();
            }
            else if (persistence.BlockedImportCode != null)
            {
                Apply(StartupStatusKind.ImportBlocked, persistence.BlockedImportCode);
            }
            else if (persistence.ColdStartRetryRequired)
            {
                Apply(StartupStatusKind.StartRetry, null);
            }
            else if (lifecycle == LifecycleState.ReturnBlocked)
            {
                Apply(StartupStatusKind.ReturnBlocked, null);
            }
            else
            {
                Apply(StartupStatusKind.Importing, null);
            }

            return kind;
        }

        /// <summary>Closes the surface. An open confirmation is closed first without running its command.</summary>
        public void Hide()
        {
            CancelConfirmation(ConfirmationCloseReason.Replaced, restoreFocus: false);
            spinnerTimer.Pause();
            kind = StartupStatusKind.Hidden;
            blockedCode = null;
            CloseSurface();
        }

        private void Apply(StartupStatusKind next, string code)
        {
            var changed = next != kind || code != blockedCode;
            kind = next;
            blockedCode = code;
            SetFeedback(string.Empty);

            if (changed)
            {
                CancelConfirmation(ConfirmationCloseReason.Replaced, restoreFocus: false);
            }

            switch (next)
            {
                case StartupStatusKind.Importing:
                    SetText(TitleLabel, ImportingText);
                    ShowBody(string.Empty, string.Empty);
                    SetDisplayed(SpinnerRow, true);
                    spinnerTimer.Resume();
                    ShowButtons(false, false, false, false);
                    break;
                case StartupStatusKind.ImportBlocked:
                    SetText(TitleLabel, ImportBlockedTitle);
                    ShowBody(ImportCopy(code), ImportBlockedHelp);
                    ShowButtons(true, true, false, false);
                    break;
                case StartupStatusKind.StartRetry:
                    SetText(TitleLabel, StartRetryTitle);
                    ShowBody(StartRetryText, string.Empty);
                    ShowButtons(true, false, false, false);
                    break;
                case StartupStatusKind.ReturnBlocked:
                    canDiscard = persistence.CanDiscardDamagedSave;
                    SetText(TitleLabel, ReturnBlockedTitle);
                    ShowBody(ReturnMessage(), ReturnHelp());
                    ShowButtons(true, false, true, canDiscard);
                    break;
            }

            if (next != StartupStatusKind.Importing)
            {
                SetDisplayed(SpinnerRow, false);
                spinnerTimer.Pause();
            }

            if (changed || !IsOpen)
            {
                OpenSurface(FirstVisibleButton());
            }
        }

        private void ShowBody(string primary, string secondary)
        {
            SetText(message, primary);
            SetDisplayed(message, primary.Length > 0);
            SetText(help, secondary);
            SetDisplayed(help, secondary.Length > 0);
        }

        private void ShowButtons(bool retry, bool startNewRun, bool usePrevious, bool discard)
        {
            SetDisplayed(RetryButton, retry);
            SetDisplayed(StartNewRunButton, startNewRun);
            SetDisplayed(UsePreviousButton, usePrevious);
            SetDisplayed(DiscardButton, discard);
        }

        private Button FirstVisibleButton()
        {
            foreach (var button in new[] { RetryButton, UsePreviousButton, StartNewRunButton, DiscardButton })
            {
                if (button.style.display != DisplayStyle.None)
                {
                    return button;
                }
            }

            return null;
        }

        private void SetFeedback(string text)
        {
            SetText(feedback, text);
            SetDisplayed(feedback, text.Length > 0);
        }

        private static void SetText(Label label, string text)
        {
            label.text = text ?? string.Empty;
            label.style.minHeight = StyleKeyword.Null;
        }

        private void TickSpinner()
        {
            spinnerAngle = (spinnerAngle + 30f) % 360f;
            spinner.style.rotate = new Rotate(new Angle(spinnerAngle, AngleUnit.Degree));
        }

        // ------------------------------------------------------------------------------------------------------
        // Commands
        // ------------------------------------------------------------------------------------------------------

        private void OnRetry()
        {
            if (kind == StartupStatusKind.ReturnBlocked)
            {
                Finish(persistence.RetryReturn());
            }
            else if (kind == StartupStatusKind.ImportBlocked || kind == StartupStatusKind.StartRetry)
            {
                Finish(persistence.ColdStart(legacySource));
            }
        }

        private void OnUsePrevious()
        {
            if (kind == StartupStatusKind.ReturnBlocked)
            {
                Finish(persistence.RecoverPreviousCheckpoint());
            }
        }

        private void OnStartNewRun()
        {
            if (kind != StartupStatusKind.ImportBlocked)
            {
                return;
            }

            Confirm(new ConfirmationRequest
            {
                Title = StartNewRunConfirmTitle,
                Message = StartNewRunConfirmMessage,
                ConfirmLabel = StartNewRunLabel,
                Destructive = true,
                Invoker = StartNewRunButton,
                OnConfirm = () => Finish(persistence.StartNewRunAfterBlockedImport()),
            });
        }

        private void OnDiscard()
        {
            if (kind != StartupStatusKind.ReturnBlocked)
            {
                return;
            }

            Confirm(new ConfirmationRequest
            {
                Title = DiscardConfirmTitle,
                Message = DiscardConfirmMessage,
                ConfirmLabel = "Discard and start",
                Destructive = true,
                Invoker = DiscardButton,
                OnConfirm = () => Finish(persistence.DiscardDamagedSaveAndRestart(legacySource)),
            });
        }

        private void Finish(RunCommandResult<RunPersistenceOutcome> result)
        {
            if (result.IsSuccess)
            {
                var durable = result.Value != null && result.Value.SelectedDurableCheckpoint;
                Refresh();
                if (kind != StartupStatusKind.Hidden)
                {
                    // The command succeeded but the run is not live (it should not happen): keep the state honest.
                    SetFeedback(StillBlockedText);
                    return;
                }

                Completed?.Invoke(result);
                if (durable)
                {
                    // The player's choice was replaced by a durable checkpoint; the composition can show this once the shell exists.
                    LastCompletionNote = SelectedDurableText;
                }
                else
                {
                    LastCompletionNote = string.Empty;
                }

                return;
            }

            Refresh();
            SetFeedback(RejectionCopy(result.ReasonCode));
            FocusWhenReady(FirstVisibleButton());
        }

        /// <summary>A note for the shell notice once the start completed after a durable checkpoint was selected instead of the player's choice.</summary>
        public string LastCompletionNote { get; private set; } = string.Empty;

        // ------------------------------------------------------------------------------------------------------
        // Confirmation (UX-2, UX-6, UX-9)
        // ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Cancels an open confirmation because of a rotation, without running its command, and shows the one-line notice (UX-9). Returns
        /// whether one was open. V1 stays offered: nothing was promoted and the blocked import is unchanged. Register this with
        /// <c>CoreGameFoundationBootstrap.RegisterTransientAction</c>.
        /// </summary>
        public bool CancelForRotation()
        {
            if (!IsConfirmationOpen)
            {
                return false;
            }

            CancelConfirmation(ConfirmationCloseReason.Rotation, restoreFocus: true);
            ShowNotice(RunShellDocument.RotationCancelledNotice);
            return true;
        }

        private void Confirm(ConfirmationRequest request)
        {
            ClearNotice();
            var useShell = Shell != null && Shell.IsReady;
            if (useShell)
            {
                var original = request;
                shellDialogOpen = true;
                var wrapped = new ConfirmationRequest
                {
                    Title = original.Title,
                    Message = original.Message,
                    ConfirmLabel = original.ConfirmLabel,
                    CancelLabel = original.CancelLabel,
                    Destructive = original.Destructive,
                    Invoker = original.Invoker,
                    OnConfirm = () =>
                    {
                        shellDialogOpen = false;
                        TrapSuspended = false;
                        original.OnConfirm?.Invoke();
                    },
                    OnCancel = reason =>
                    {
                        shellDialogOpen = false;
                        TrapSuspended = false;
                        original.OnCancel?.Invoke(reason);
                    },
                };
                TrapSuspended = true;
                if (!Shell.RequestConfirmation(wrapped))
                {
                    shellDialogOpen = false;
                    TrapSuspended = false;
                }

                return;
            }

            OpenInline(request);
        }

        private void OpenInline(ConfirmationRequest request)
        {
            if (inlineOpen)
            {
                CloseInline(ConfirmationCloseReason.Replaced, restoreFocus: false);
            }

            inlineRequest = request;
            inlineInvoker = request.Invoker ?? Root.panel?.focusController?.focusedElement as VisualElement;
            inlineOpen = true;
            SetText(confirmTitle, request.Title);
            SetText(confirmMessage, request.Message);
            ConfirmButton.text = request.ConfirmLabel ?? "Confirm";
            ConfirmCancelButton.text = request.CancelLabel ?? "Cancel";
            ConfirmButton.EnableInClassList("run-button--destructive", request.Destructive);
            ConfirmButton.EnableInClassList("run-button--primary", !request.Destructive);
            confirmLayer.style.display = DisplayStyle.Flex;
            confirmScroll.scrollOffset = Vector2.zero;

            // The card behind is inert while the dialog is open (focus cannot leave it by Tab) and gets focus back on close.
            Card.SetEnabled(false);
            FocusWhenReady(ConfirmCancelButton);
        }

        private void CloseInline(ConfirmationCloseReason reason, bool restoreFocus = true)
        {
            if (!inlineOpen)
            {
                return;
            }

            var request = inlineRequest;
            var invoker = inlineInvoker;
            inlineOpen = false;
            inlineRequest = null;
            inlineInvoker = null;
            confirmLayer.style.display = DisplayStyle.None;
            Card.SetEnabled(true);
            if (restoreFocus && invoker != null && invoker.panel == Root.panel && invoker.enabledInHierarchy && invoker.resolvedStyle.display != DisplayStyle.None)
            {
                FocusWhenReady(invoker);
            }
            else if (restoreFocus)
            {
                FocusWhenReady(FirstVisibleButton());
            }

            if (reason == ConfirmationCloseReason.Confirmed)
            {
                request?.OnConfirm?.Invoke();
            }
            else
            {
                request?.OnCancel?.Invoke(reason);
            }
        }

        private void CancelConfirmation(ConfirmationCloseReason reason, bool restoreFocus)
        {
            if (inlineOpen)
            {
                CloseInline(reason, restoreFocus);
            }
            else if (shellDialogOpen && Shell != null)
            {
                if (reason == ConfirmationCloseReason.Rotation)
                {
                    Shell.CancelConfirmationForRotation();
                }
                else
                {
                    Shell.HandleBack();
                }

                shellDialogOpen = false;
                TrapSuspended = false;
            }
        }

        private void OnConfirmKeyDown(KeyDownEvent evt)
        {
            if (!inlineOpen)
            {
                return;
            }

            if (evt.keyCode == KeyCode.Escape)
            {
                // Escape is Android Back: both are Cancel.
                evt.StopPropagation();
                CloseInline(ConfirmationCloseReason.Back);
            }
            else if (evt.keyCode == KeyCode.Tab)
            {
                MoveConfirmFocus(evt.shiftKey ? -1 : 1);
                evt.StopPropagation();
                Root.panel?.focusController?.IgnoreEvent(evt);
            }
        }

        private void OnConfirmNavigationMove(NavigationMoveEvent evt)
        {
            if (!inlineOpen)
            {
                return;
            }

            switch (evt.direction)
            {
                case NavigationMoveEvent.Direction.Next:
                case NavigationMoveEvent.Direction.Right:
                case NavigationMoveEvent.Direction.Down:
                    MoveConfirmFocus(1);
                    break;
                case NavigationMoveEvent.Direction.Previous:
                case NavigationMoveEvent.Direction.Left:
                case NavigationMoveEvent.Direction.Up:
                    MoveConfirmFocus(-1);
                    break;
            }

            evt.StopPropagation();
            Root.panel?.focusController?.IgnoreEvent(evt);
        }

        private void OnConfirmNavigationCancel(NavigationCancelEvent evt)
        {
            if (inlineOpen)
            {
                evt.StopPropagation();
                CloseInline(ConfirmationCloseReason.Back);
            }
        }

        private void MoveConfirmFocus(int step)
        {
            var focused = Root.panel?.focusController?.focusedElement;
            var next = focused == ConfirmCancelButton ? ConfirmButton : ConfirmCancelButton;
            if (step < 0 && focused == ConfirmButton)
            {
                next = ConfirmCancelButton;
            }

            next.Focus();
        }

        private void OnConfirmFocusOut(FocusOutEvent evt)
        {
            if (!inlineOpen)
            {
                return;
            }

            // The trap: focus that moves outside the open dialog goes back into it.
            if (evt.relatedTarget is VisualElement next && (confirmLayer.Contains(next) || next == confirmLayer))
            {
                return;
            }

            confirmLayer.schedule.Execute(() =>
            {
                if (inlineOpen)
                {
                    var focused = Root.panel?.focusController?.focusedElement as VisualElement;
                    if (focused == null || !confirmLayer.Contains(focused))
                    {
                        ConfirmCancelButton.Focus();
                    }
                }
            });
        }

        // ------------------------------------------------------------------------------------------------------
        // Copy (static, never taken from a file: SEC-5)
        // ------------------------------------------------------------------------------------------------------

        /// <summary>The failed rule's copy of a blocked import.</summary>
        public static string ImportCopy(string code)
        {
            switch (code)
            {
                case V1ImportCodes.Invalid: return "The saved development factory could not be read.";
                case V1ImportCodes.Clock: return "The device clock is unavailable, so the saved development factory cannot be imported yet. Check the device date and time, then retry.";
                case V1ImportCodes.IdUnsupported: return "The saved development factory contains content that cannot be imported.";
                case V1ImportCodes.Arithmetic: return "The saved development factory is too large to import.";
                case V1ImportCodes.SlotsExceeded: return "The saved development factory has more machines than the first factory level holds.";
                case V1ImportCodes.CapacityExceeded: return "The saved development factory holds more stock than the first store level holds.";
                case V1ImportCodes.Persistence: return "The imported factory could not be saved. Your development save is unchanged; retry.";
                default: return "The saved development factory could not be imported.";
            }
        }

        private string ReturnMessage()
        {
            var status = persistence.Status;
            var text = status == null || string.IsNullOrEmpty(status.Message) ? "The saved factory could not be loaded." : status.Message;
            return text;
        }

        private string ReturnHelp()
        {
            var parts = new System.Text.StringBuilder("Retry loads it again. Use previous save restores the generation before it, if one exists; progress made after that save is lost.");
            if (canDiscard)
            {
                parts.Append(" Discard damaged save throws the damaged save away and starts a new run; a development save is imported once if one exists.");
            }
            else
            {
                parts.Append(" The save is kept exactly as it is and is never overwritten.");
            }

            return parts.ToString();
        }

        private static string RejectionCopy(string reason)
        {
            switch (reason)
            {
                case RunPersistenceReasons.PersistenceFailed: return "Checkpoint storage is unavailable. Nothing was changed.";
                case RunPersistenceReasons.DiscardUnavailable: return "The damaged save cannot be discarded here. Nothing was changed.";
                case RunPersistenceReasons.LifecycleNoOp: return "That action is not available now. Nothing was changed.";
                case RunPersistenceReasons.NoCheckpoint: return "There is no previous save to use. Nothing was changed.";
                case RunPersistenceReasons.InvalidCheckpoint: return "The previous save could not be used. Nothing was changed.";
                case RunPersistenceReasons.UnsupportedVersion:
                case RunPersistenceReasons.UnresolvedContent: return "The saved factory comes from another version of the game. Nothing was changed.";
                default: return StillBlockedText;
            }
        }
    }
}
