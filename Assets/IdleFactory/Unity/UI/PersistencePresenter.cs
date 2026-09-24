using System;
using System.Globalization;
using System.Text;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI
{
    /// <summary>Projects durable checkpoint and offline-return outcomes into the shared development HUD.</summary>
    public sealed class PersistencePresenter : MonoBehaviour
    {
        private const long CoreTicksPerSecond = TimeSpan.TicksPerSecond;

        private PersistenceCommands commands;
        private Func<LifecycleState> lifecycleState;
        private Func<PersistenceOperationResult> latestLifecycleResult;
        private Action cancelPlacementPreview;
        private Action cancelPendingPointerState;
        private Action<FactorySnapshot> refreshLoadedFactory;
        private VisualElement panel;
        private Label statusLabel;
        private Label feedbackLabel;
        private Label summaryLabel;
        private Button saveButton;
        private Button loadButton;
        private Button resetButton;
        private Button retryButton;
        private Button reloadButton;
        private Button recoverButton;
        private bool configured;
        private bool busy;

        public bool IsConfigured => configured;
        public string Error { get; private set; } = string.Empty;

        public void Configure(
            PersistenceCommands persistenceCommands,
            VisualElement hudRoot,
            Func<LifecycleState> readLifecycleState,
            Action cancelCurrentPlacementPreview,
            Action cancelPendingPointerState,
            Action<FactorySnapshot> refreshFactoryAndSelection,
            Func<PersistenceOperationResult> readLatestLifecycleResult = null)
        {
            if (configured)
            {
                throw new InvalidOperationException("The persistence presenter has already been configured.");
            }

            commands = persistenceCommands ?? throw new ArgumentNullException(nameof(persistenceCommands));
            if (hudRoot == null) throw new ArgumentNullException(nameof(hudRoot));
            lifecycleState = readLifecycleState ?? throw new ArgumentNullException(nameof(readLifecycleState));
            cancelPlacementPreview = cancelCurrentPlacementPreview ?? throw new ArgumentNullException(nameof(cancelCurrentPlacementPreview));
            this.cancelPendingPointerState = cancelPendingPointerState ?? throw new ArgumentNullException(nameof(cancelPendingPointerState));
            refreshLoadedFactory = refreshFactoryAndSelection ?? throw new ArgumentNullException(nameof(refreshFactoryAndSelection));
            latestLifecycleResult = readLatestLifecycleResult;

            panel = hudRoot.Q<VisualElement>("persistence-panel");
            statusLabel = hudRoot.Q<Label>("persistence-status");
            feedbackLabel = hudRoot.Q<Label>("persistence-feedback");
            summaryLabel = hudRoot.Q<Label>("persistence-summary");
            saveButton = hudRoot.Q<Button>("persistence-save");
            loadButton = hudRoot.Q<Button>("persistence-load");
            resetButton = hudRoot.Q<Button>("persistence-reset");
            retryButton = hudRoot.Q<Button>("persistence-retry");
            reloadButton = hudRoot.Q<Button>("persistence-reload");
            recoverButton = hudRoot.Q<Button>("persistence-recover");

            if (panel == null || statusLabel == null || feedbackLabel == null || summaryLabel == null ||
                saveButton == null || loadButton == null || resetButton == null || retryButton == null ||
                reloadButton == null || recoverButton == null)
            {
                Error = "Persistence controls are unavailable because the HUD layout is incomplete.";
                RenderUnconfigured(Error);
                return;
            }

            saveButton.clicked += BeginSaveFromHud;
            loadButton.clicked += LoadDevelopmentCheckpoint;
            resetButton.clicked += ResetDevelopmentScenario;
            retryButton.clicked += RetryReturn;
            reloadButton.clicked += ReloadAuthoritativeGeneration;
            recoverButton.clicked += RecoverPreviousCheckpoint;
            BindFocusOutline(saveButton);
            BindFocusOutline(loadButton);
            BindFocusOutline(resetButton);
            BindFocusOutline(retryButton);
            BindFocusOutline(reloadButton);
            BindFocusOutline(recoverButton);

            configured = true;
            feedbackLabel.text = "No recent persistence action.";
            Refresh();
        }

        public void Refresh()
        {
            if (!configured || commands == null)
            {
                return;
            }

            var status = commands.GetPersistenceStatus();
            var state = lifecycleState();
            statusLabel.text = busy
                ? "Status: SAVING — writing and verifying a complete checkpoint."
                : StatusText(status.Status, state);
            panel.EnableInClassList("factory-persistence--error", status.Status == PersistenceStatusKind.Failure);
            panel.EnableInClassList("factory-persistence--warning", status.Status == PersistenceStatusKind.NoCheckpoint ||
                state == LifecycleState.ReturnBlocked);

            var running = state == LifecycleState.Running;
            var blocked = state == LifecycleState.ReturnBlocked;
            saveButton.SetEnabled(!busy && running);
            loadButton.SetEnabled(!busy && running);
            resetButton.SetEnabled(!busy && running);
            retryButton.SetEnabled(!busy && blocked);
            reloadButton.SetEnabled(!busy && blocked);
            recoverButton.SetEnabled(!busy && status.PreviousCheckpointAvailable && (running || blocked));

            var summary = state == LifecycleState.Running ? commands.GetLastOfflineSummary() : null;
            var fallback = false;
            if (latestLifecycleResult != null)
            {
                try
                {
                    var result = latestLifecycleResult();
                    fallback = result != null && result.UsedFallback;
                }
                catch (Exception)
                {
                    // Lifecycle feedback is optional. The durable summary remains usable if its owner is unavailable.
                }
            }

            RenderOfflineSummary(summary, fallback);
        }

        public void SaveNow()
        {
            Execute(() => commands.SaveNow(), "Checkpoint saved.", OperationKind.Save);
        }

        public void LoadDevelopmentCheckpoint()
        {
            Execute(() => commands.LoadDevelopmentCheckpoint(), "Saved factory loaded.", OperationKind.ReplaceWorld);
        }

        public void ResetDevelopmentScenario()
        {
            Execute(() => commands.ResetDevelopmentScenario(),
                "Live D001 scenario reset. The saved checkpoint is unchanged.",
                OperationKind.ReplaceWorld);
        }

        public void RetryReturn()
        {
            Execute(() => commands.RetryReturn(), "Offline return completed and saved.", OperationKind.ReplaceWorld);
        }

        public void ReloadAuthoritativeGeneration()
        {
            Execute(() => commands.ReloadAuthoritativeGeneration(), "Saved factory reloaded.", OperationKind.ReplaceWorld);
        }

        public void RecoverPreviousCheckpoint()
        {
            Execute(() => commands.RecoverPreviousCheckpoint(), "Previous saved factory recovered.", OperationKind.ReplaceWorld);
        }

        private void OnDestroy()
        {
            if (saveButton == null)
            {
                return;
            }

            saveButton.clicked -= BeginSaveFromHud;
            loadButton.clicked -= LoadDevelopmentCheckpoint;
            resetButton.clicked -= ResetDevelopmentScenario;
            retryButton.clicked -= RetryReturn;
            reloadButton.clicked -= ReloadAuthoritativeGeneration;
            recoverButton.clicked -= RecoverPreviousCheckpoint;
            UnbindFocusOutline(saveButton);
            UnbindFocusOutline(loadButton);
            UnbindFocusOutline(resetButton);
            UnbindFocusOutline(retryButton);
            UnbindFocusOutline(reloadButton);
            UnbindFocusOutline(recoverButton);
        }

        private void Execute(
            Func<CommandResult<PersistenceOperationResult>> operation,
            string successMessage,
            OperationKind operationKind)
        {
            if (!configured || busy)
            {
                return;
            }

            busy = true;
            feedbackLabel.text = operationKind == OperationKind.Save
                ? "Saving checkpoint…"
                : operationKind == OperationKind.ReplaceWorld
                    ? "Applying saved factory…"
                    : "Working…";
            panel.RemoveFromClassList("factory-persistence--error");
            SetControlsEnabled(false);

            CommandResult<PersistenceOperationResult> result;
            try
            {
                result = operation();
            }
            catch (Exception)
            {
                // Never send paths, exception text or serialized save data to the HUD.
                busy = false;
                feedbackLabel.text = "The persistence action could not be completed. Your current factory is unchanged.";
                panel.AddToClassList("factory-persistence--error");
                Refresh();
                return;
            }

            busy = false;
            if (!result.Succeeded)
            {
                feedbackLabel.text = FailureText(result.Outcome, operationKind);
                panel.AddToClassList("factory-persistence--error");
                Refresh();
                return;
            }

            feedbackLabel.text = result.Value != null && result.Value.UsedFallback
                ? "Return completed using the saved-checkpoint fallback. Later unsaved changes were not restored."
                : successMessage;
            panel.RemoveFromClassList("factory-persistence--error");

            var value = result.Value;
            if (operationKind == OperationKind.ReplaceWorld && value != null && !value.WasNoOp && value.Factory != null)
            {
                RefreshReplacedWorld(value.Factory);
            }

            Refresh();
            if (value != null && value.OfflineSummary != null)
            {
                RenderOfflineSummary(value.OfflineSummary, value.UsedFallback);
            }
        }

        private void BeginSaveFromHud()
        {
            if (!configured || busy)
            {
                return;
            }

            // Give UI Toolkit one frame to paint the in-progress state before the synchronous
            // application command performs its durable write and validation.
            busy = true;
            feedbackLabel.text = "Saving checkpoint…";
            statusLabel.text = "Status: SAVING — writing and verifying a complete checkpoint.";
            SetControlsEnabled(false);
            StartCoroutine(RunSaveAfterProgressFrame());
        }

        private System.Collections.IEnumerator RunSaveAfterProgressFrame()
        {
            yield return null;
            busy = false;
            SaveNow();
        }

        private void RefreshReplacedWorld(FactorySnapshot snapshot)
        {
            TryCancel(cancelPlacementPreview, "Factory loaded; placement preview could not be cleared.");
            TryCancel(cancelPendingPointerState, "Factory loaded; pending pointer input could not be cleared.");

            try
            {
                // The composition owner refreshes the FactoryView and lets its selection event refresh inspection.
                refreshLoadedFactory(snapshot);
            }
            catch (Exception)
            {
                feedbackLabel.text = "Factory loaded; the view could not be refreshed.";
                panel.AddToClassList("factory-persistence--error");
            }
        }

        private void TryCancel(Action cancel, string failureMessage)
        {
            try
            {
                cancel();
            }
            catch (Exception)
            {
                feedbackLabel.text = failureMessage;
                panel.AddToClassList("factory-persistence--warning");
            }
        }

        private void RenderOfflineSummary(OfflineReturnSummary summary, bool usedFallback)
        {
            if (summary == null || summary.Outcome == OfflineReturnOutcome.NoCheckpoint)
            {
                summaryLabel.text = string.Empty;
                summaryLabel.style.display = DisplayStyle.None;
                return;
            }

            var text = new StringBuilder();
            text.Append("Durable offline return").AppendLine();
            text.Append("Measured: ").Append(FormatDuration(summary.MeasuredTicks)).AppendLine();
            text.Append("Credited: ").Append(FormatDuration(summary.CreditedTicks)).AppendLine();
            text.Append("Effective: ").Append(FormatDuration(summary.EffectiveTicks)).AppendLine();

            if (summary.ResourceDeltas.Count == 0)
            {
                text.Append("Actual resource changes: none").AppendLine();
            }
            else
            {
                text.AppendLine("Actual resource changes:");
                foreach (var delta in summary.ResourceDeltas)
                {
                    text.Append("• ").Append(delta.OwnerId.Value).Append(" · ").Append(delta.ResourceId.Value)
                        .Append(' ').Append(delta.Delta > 0 ? "+" : string.Empty)
                        .Append(delta.Delta.ToString(CultureInfo.InvariantCulture)).AppendLine();
                }
            }

            if (summary.Limitations.Count == 0)
            {
                text.Append("Limiting reasons: none");
            }
            else
            {
                text.AppendLine("Limiting reasons:");
                foreach (var limitation in summary.Limitations)
                {
                    text.Append("• ").Append(limitation.OwnerId.Value).Append(" · ")
                        .Append(DescribeLimitation(limitation)).AppendLine();
                }
            }

            if (summary.Outcome == OfflineReturnOutcome.InvalidTime)
            {
                text.AppendLine("The device time was backward; no absence time was credited.");
            }

            if (usedFallback)
            {
                text.AppendLine("Warning: fallback used; later unsaved changes were not restored.");
            }

            summaryLabel.text = text.ToString().TrimEnd();
            summaryLabel.style.display = DisplayStyle.Flex;
        }

        private static string StatusText(PersistenceStatusKind status, LifecycleState state)
        {
            if (state == LifecycleState.ReturnBlocked)
            {
                return "Status: RETURN BLOCKED — no return result has been applied. Choose Retry return or Reload saved factory.";
            }

            if (state == LifecycleState.Bootstrapping || state == LifecycleState.Loading ||
                state == LifecycleState.Reconciling)
            {
                return "Status: RETURN PENDING — the saved factory is being checked; no offline result is available yet.";
            }

            if (state == LifecycleState.Checkpointing || state == LifecycleState.Suspended)
            {
                return "Status: WAITING — the factory is suspended; no offline result has been applied.";
            }

            switch (status)
            {
                case PersistenceStatusKind.NoCheckpoint:
                    return "Status: NO SAVE — the development factory remains available.";
                case PersistenceStatusKind.Ready:
                    return "Status: READY — a saved factory is available.";
                case PersistenceStatusKind.Writing:
                    return "Status: SAVING — verifying a complete checkpoint.";
                case PersistenceStatusKind.Recovered:
                    return "Status: RECOVERED — the previous saved factory is now active.";
                default:
                    return "Status: ACTION FAILED — the current factory was not replaced.";
            }
        }

        private static string FailureText(CommandOutcome outcome, OperationKind operationKind)
        {
            switch (outcome)
            {
                case CommandOutcome.NoCheckpoint:
                    return "No saved factory is available. Your live factory and selection are unchanged.";
                case CommandOutcome.UnsupportedSaveVersion:
                    return "This saved factory version is not supported. Your live factory is unchanged.";
                case CommandOutcome.InvalidCheckpoint:
                    return "The saved factory could not be validated. Your live factory is unchanged.";
                case CommandOutcome.ReturnBlocked:
                    return "Return is still blocked. No offline result has been applied.";
                case CommandOutcome.PersistenceFailed:
                    return operationKind == OperationKind.Save
                        ? "Save failed. The current factory and previous checkpoint are unchanged."
                        : "The checkpoint could not be confirmed. Your current factory is unchanged.";
                case CommandOutcome.LifecycleNoOp:
                    return "This action is unavailable while the factory is waiting for lifecycle recovery.";
                default:
                    return "The action was not applied. Your current factory is unchanged.";
            }
        }

        private static string DescribeLimitation(OfflineLimitationSnapshot limitation)
        {
            switch (limitation.Kind)
            {
                case ProductionBlockerKind.Inactive:
                    return "machine inactive";
                case ProductionBlockerKind.MissingInput:
                    return limitation.ResourceId.HasValue
                        ? $"missing input {limitation.ResourceId.Value.Value}"
                        : "missing input";
                case ProductionBlockerKind.InsufficientOutputCapacity:
                    return limitation.ResourceId.HasValue
                        ? $"output capacity reached for {limitation.ResourceId.Value.Value}"
                        : "output capacity reached";
                default:
                    return "production limit reached";
            }
        }

        private static string FormatDuration(long ticks)
        {
            var duration = TimeSpan.FromTicks(ticks);
            if (duration.Days > 0)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0} d {1:00} h {2:00} m {3:00} s",
                    duration.Days, duration.Hours, duration.Minutes, duration.Seconds);
            }

            if (duration.Hours > 0)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0} h {1:00} m {2:00} s",
                    duration.Hours, duration.Minutes, duration.Seconds);
            }

            if (duration.Minutes > 0)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0} m {1:00} s",
                    duration.Minutes, duration.Seconds);
            }

            return (ticks / (double)CoreTicksPerSecond).ToString("0.##", CultureInfo.InvariantCulture) + " s";
        }

        private void SetControlsEnabled(bool enabled)
        {
            saveButton.SetEnabled(enabled);
            loadButton.SetEnabled(enabled);
            resetButton.SetEnabled(enabled);
            retryButton.SetEnabled(enabled);
            reloadButton.SetEnabled(enabled);
            recoverButton.SetEnabled(enabled);
        }

        private void RenderUnconfigured(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = "Status: UNCONFIGURED — " + message;
            }

            if (feedbackLabel != null)
            {
                feedbackLabel.text = "Persistence controls are unavailable.";
            }

            if (saveButton != null) saveButton.SetEnabled(false);
            if (loadButton != null) loadButton.SetEnabled(false);
            if (resetButton != null) resetButton.SetEnabled(false);
            if (retryButton != null) retryButton.SetEnabled(false);
            if (reloadButton != null) reloadButton.SetEnabled(false);
            if (recoverButton != null) recoverButton.SetEnabled(false);
        }

        private static void BindFocusOutline(Button button)
        {
            button.RegisterCallback<FocusInEvent>(HandleControlFocusIn);
            button.RegisterCallback<FocusOutEvent>(HandleControlFocusOut);
        }

        private static void UnbindFocusOutline(Button button)
        {
            button.UnregisterCallback<FocusInEvent>(HandleControlFocusIn);
            button.UnregisterCallback<FocusOutEvent>(HandleControlFocusOut);
        }

        private static void HandleControlFocusIn(FocusInEvent evt)
        {
            (evt.target as VisualElement)?.AddToClassList("factory-persistence__control--focused");
        }

        private static void HandleControlFocusOut(FocusOutEvent evt)
        {
            (evt.target as VisualElement)?.RemoveFromClassList("factory-persistence__control--focused");
        }

        private enum OperationKind
        {
            Save,
            ReplaceWorld,
        }
    }
}
