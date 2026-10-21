using System;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Persistence;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>
    /// The session-log section, the fourth section of the Market tab (<c>session-log-section</c>, T340, FR-028 to FR-031, D-M, D-I;
    /// contracts/ui-surfaces-f005.md). It switches recording, exports the log, clears it after a confirmation and keeps a persistent
    /// "Last export" line. It is an adapter surface, not a run command: it reaches the log only through <see cref="ISessionLogControls"/>
    /// (the writer's own serialization is the single path) and holds no rule. The export path is shown and never logged, persisted or sent
    /// anywhere (SEC-2). Without a log (<c>null</c> controls) every control stays focusable but soft-disabled with the reason visible.
    /// </summary>
    public sealed class SessionLogPanel : IShellPanel
    {
        public const string Heading = "Session log";
        public const string StaysOnDeviceCopy = "The session log records this run's events and stays on this device. Nothing is sent anywhere.";
        public const string UnavailableReason = "The session log is not available.";
        public const string OffNote = "Recording is off. Nothing new is written.";
        public const string ClearTitle = "Clear session log";
        public const string ClearConfirmationMessage = "Clear the session log? This cannot be undone";
        public const string ClearConfirmLabel = "Clear log";
        public const string ClearCancelLabel = "Keep log";
        public const string ClearedStatus = "Session log cleared.";
        public const string ClearFailedReason = "The session log could not be cleared. Try again.";
        public const string NothingToExport = "Nothing to export. The log is empty.";
        public const string NoSpaceReason = "Not enough space on this device to export. Free some space, then retry.";
        public const string NoPermissionReason = "No permission to write the export. Check the storage access of the app, then retry.";
        public const string UnknownFailureReason = "The session log could not be exported. Retry.";
        public const string LastExportNone = "Last export: none yet";
        public const string LastExportPrefix = "Last export: ";
        public const string PullHint = "On this device the file is copied with adb pull.";

        private readonly ISessionLogControls log;
        private readonly RunShellDocument shell;
        private readonly VisualElement root;
        private readonly Label offNote;
        private readonly Label status;
        private readonly Label reason;
        private readonly Label pullHint;
        private bool confirmOpen;
        private bool exportFailed;
        private string lastExportText = LastExportNone;
        private bool lastExportKnown;

        /// <summary>Builds the section. <paramref name="log"/> may be null (the log failed to start): the section then explains it.</summary>
        public SessionLogPanel(ISessionLogControls log, RunShellDocument shell, StyleSheet styles, Func<string, Sprite> icons = null, string panelId = ShellPanelIds.Market)
        {
            this.log = log;
            this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
            Id = panelId;
            root = MarketPanelParts.CreateRoot(null, styles, "session-log-section", "exp-panel");
            MarketPanelParts.Ensure<Label>(root, "session-log-heading", "exp-heading").text = Heading;

            var card = new VisualElement { name = "session-log-card" };
            card.AddToClassList("exp-card");
            root.Add(card);

            card.Add(MarketPanelParts.NewLabel("session-log-copy", "exp-caption", StaysOnDeviceCopy));

            Toggle = RunShellDocument.CreateButton("session-log-toggle", "Session log: Off");
            Toggle.AddToClassList("exp-upgrade");
            Toggle.clicked += OnToggle;
            card.Add(Toggle);
            offNote = MarketPanelParts.NewLabel("session-log-off-note", "exp-caption", OffNote);
            card.Add(offNote);

            Export = RunShellDocument.CreateButton("session-log-export", "Export session log", true);
            Export.AddToClassList("exp-upgrade");
            Export.clicked += OnExport;
            card.Add(Export);

            Clear = RunShellDocument.CreateButton("session-log-clear", "Clear session log");
            Clear.AddToClassList("exp-upgrade");
            Clear.clicked += OnClear;
            card.Add(Clear);

            reason = MarketPanelParts.NewLabel("session-log-reason", "exp-caption");
            reason.AddToClassList("exp-caption--reason");
            PanelIcons.Mark(reason, icons, PanelIcons.Warning);
            card.Add(reason);
            status = MarketPanelParts.NewLabel("session-log-status", "exp-caption");
            card.Add(status);

            // A persistent line, never cleared by a refresh. On a desktop player its text can be selected and copied (D-M); it is plain text, so a
            // path is never interpreted as markup, and nothing here ever writes it to the Unity log.
            LastExport = new Label(LastExportNone) { name = "session-log-last-export", focusable = true };
            LastExport.AddToClassList("run-control");
            LastExport.AddToClassList("exp-caption");
            LastExport.style.whiteSpace = WhiteSpace.Normal;
            LastExport.enableRichText = false;
            card.Add(LastExport);
            pullHint = MarketPanelParts.NewLabel("session-log-pull-hint", "exp-caption", PullHint);
            card.Add(pullHint);
            Render();
        }

        public string Id { get; }
        public string Title => Heading;
        public VisualElement Root => root;

        /// <summary><c>session-log-toggle</c>: the recording Off/On toggle, accessible name "Session log, on/off".</summary>
        public Button Toggle { get; }

        /// <summary><c>session-log-export</c>: exports the log; reads "Retry export" after a failure.</summary>
        public Button Export { get; }

        /// <summary><c>session-log-clear</c>: asks for a confirmation, then clears the log.</summary>
        public Button Clear { get; }

        /// <summary><c>session-log-last-export</c>: "Last export: &lt;path&gt;", selectable on a desktop player.</summary>
        public Label LastExport { get; }

        public Label Status => status;
        public Label Reason => reason;
        public Label OffNoteLabel => offNote;
        public Label PullHintLabel => pullHint;
        public bool IsConfirmationOpen => confirmOpen;

        public void OnShown(ShellPanelContext context)
        {
            status.text = string.Empty;
            reason.text = string.Empty;
            exportFailed = false;
            Render();
        }

        public void Refresh() => Render();

        public void OnHidden()
        {
            status.text = string.Empty;
            reason.text = string.Empty;
        }

        /// <summary>Cancels an open Clear confirmation because of a rotation, without clearing (UX-9). Returns whether it was open.</summary>
        public bool CancelForRotation()
        {
            if (!confirmOpen)
            {
                return false;
            }

            return shell.CancelConfirmationForRotation();
        }

        private void Render()
        {
            var available = log != null;
            var enabled = available && log.Enabled;
            Toggle.text = "Session log: " + (enabled ? "On" : "Off");
            Toggle.tooltip = "Session log, " + (enabled ? "on" : "off");
            Export.text = exportFailed ? "Retry export" : "Export session log";
            Export.tooltip = available ? Export.text : "Export session log, unavailable: " + UnavailableReason;
            Clear.tooltip = available ? "Clear session log" : "Clear session log, unavailable: " + UnavailableReason;
            RunShellDocument.SetSoftDisabled(Toggle, !available);
            RunShellDocument.SetSoftDisabled(Export, !available);
            RunShellDocument.SetSoftDisabled(Clear, !available);
            MarketPanelParts.SetVisible(offNote, available && !enabled);

            if (!available)
            {
                reason.text = UnavailableReason;
            }

            MarketPanelParts.SetVisible(reason, !string.IsNullOrEmpty(reason.text));
            MarketPanelParts.SetVisible(status, !string.IsNullOrEmpty(status.text));

            // The last successful export of this process survives a rebuild of the panel (the controls hold it, in memory only).
            if (available && !lastExportKnown && !string.IsNullOrEmpty(log.LastExport.FilePath))
            {
                lastExportText = LastExportPrefix + log.LastExport.FilePath;
                lastExportKnown = true;
            }

            LastExport.text = lastExportText;
            LastExport.tooltip = lastExportKnown ? "Last export path" : "No export yet";
            var selectable = LifecyclePlatform.IsDesktop;
            LastExport.selection.isSelectable = selectable;
            // Selecting text makes the label a focus stop; set explicitly so the tab order is predictable on both platforms.
            LastExport.focusable = selectable;
            LastExport.tabIndex = 0;
            MarketPanelParts.SetVisible(pullHint, !LifecyclePlatform.IsDesktop && lastExportKnown);
        }

        private void OnToggle()
        {
            if (log == null)
            {
                Render();
                return;
            }

            reason.text = string.Empty;
            log.SetEnabled(!log.Enabled);
            status.text = log.Enabled ? "Session log is on." : "Session log is off.";
            Render();
        }

        private void OnExport()
        {
            if (log == null)
            {
                Render();
                return;
            }

            var result = log.Export();
            reason.text = string.Empty;
            status.text = string.Empty;
            exportFailed = false;
            switch (result.Status)
            {
                case SessionLogExportStatus.Exported:
                    lastExportText = LastExportPrefix + result.FilePath;
                    lastExportKnown = true;
                    status.text = "Exported " + result.Lines + (result.Lines == 1 ? " line." : " lines.") + " The file stays on this device.";
                    shell.ShowNotice("Session log exported.");
                    break;
                case SessionLogExportStatus.NothingToExport:
                    reason.text = NothingToExport;
                    break;
                default:
                    exportFailed = true;
                    reason.text = result.Failure == SessionLogExportFailure.NoSpace ? NoSpaceReason
                        : result.Failure == SessionLogExportFailure.NoPermission ? NoPermissionReason
                        : UnknownFailureReason;
                    break;
            }

            Render();
        }

        private void OnClear()
        {
            if (log == null)
            {
                Render();
                return;
            }

            confirmOpen = true;
            var opened = shell.RequestConfirmation(new ConfirmationRequest
            {
                Title = ClearTitle,
                Message = ClearConfirmationMessage,
                ConfirmLabel = ClearConfirmLabel,
                CancelLabel = ClearCancelLabel,
                Destructive = true,
                Invoker = Clear,
                OnConfirm = () =>
                {
                    confirmOpen = false;
                    ConfirmClear();
                },
                OnCancel = closeReason => confirmOpen = false,
            });
            if (!opened)
            {
                confirmOpen = false;
            }
        }

        private void ConfirmClear()
        {
            reason.text = string.Empty;
            status.text = string.Empty;
            if (log.Clear())
            {
                status.text = ClearedStatus;
            }
            else
            {
                reason.text = ClearFailedReason;
            }

            Render();
        }
    }
}
