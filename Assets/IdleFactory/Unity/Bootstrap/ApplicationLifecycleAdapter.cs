using System;
using IdleFactory.Core.Application;
using UnityEngine;

namespace IdleFactory.Unity.Bootstrap
{
    /// <summary>Normalizes Unity lifecycle callbacks into durable suspension and return commands.</summary>
    public sealed class ApplicationLifecycleAdapter : MonoBehaviour
    {
        private PersistenceCommands persistenceCommands;
        private Action prepareForDeparture;
        private bool configured;
        private bool startupReconciled;
        private bool applicationPaused;
        private bool applicationFocused = true;
        private bool applicationQuitting;
        private bool departureRecorded;

        public CommandResult<PersistenceOperationResult> LastResult { get; private set; }
        public event Action<CommandResult<PersistenceOperationResult>> ResultChanged;

        /// <summary>
        /// Binds the application-owned lifecycle commands and resolves the cold-start checkpoint before gameplay.
        /// A blocked result leaves lifecycle admission closed in the underlying session.
        /// </summary>
        public void Configure(PersistenceCommands commands, Action prepareForDeparture = null)
        {
            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            if (configured)
            {
                throw new InvalidOperationException("The application lifecycle adapter has already been configured.");
            }

            persistenceCommands = commands;
            this.prepareForDeparture = prepareForDeparture;
            configured = true;
            TryResume();
        }

        private void OnApplicationPause(bool paused)
        {
            if (applicationPaused == paused)
            {
                if (!paused)
                {
                    TryResume();
                }

                return;
            }

            applicationPaused = paused;
            if (paused)
            {
                RecordDeparture();
                return;
            }

            TryResume();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            NotifyFocusChanged(hasFocus);
        }

        /// <summary>Applies one normalized focus transition; exposed for deterministic lifecycle integration tests.</summary>
        public void NotifyFocusChanged(bool hasFocus)
        {
            if (applicationFocused == hasFocus)
            {
                if (hasFocus)
                {
                    TryResume();
                }

                return;
            }

            applicationFocused = hasFocus;
            if (!hasFocus)
            {
                RecordDeparture();
                return;
            }

            TryResume();
        }

        private void OnApplicationQuit()
        {
            if (applicationQuitting)
            {
                return;
            }

            applicationQuitting = true;
            RecordDeparture();
        }

        private void RecordDeparture()
        {
            if (!configured || !startupReconciled || departureRecorded)
            {
                return;
            }

            // The command retains the exact checkpoint ID only when the write succeeds. If it fails,
            // its next matching return applies the contract's explicit FR-037 fallback.
            departureRecorded = true;
            prepareForDeparture?.Invoke();
            Publish(persistenceCommands.RecordSuspension());
        }

        private void TryResume()
        {
            if (!configured || applicationQuitting || applicationPaused || !applicationFocused)
            {
                return;
            }

            if (!startupReconciled)
            {
                startupReconciled = true;
                Publish(persistenceCommands.ResumeAndReconcile());
                return;
            }

            if (!departureRecorded)
            {
                return;
            }

            // Mark the normalized return before calling into application commands so duplicate callbacks
            // cannot reconcile the same in-process return twice, even when reconciliation blocks.
            departureRecorded = false;
            Publish(persistenceCommands.ResumeAndReconcile());
        }

        private void Publish(CommandResult<PersistenceOperationResult> result)
        {
            LastResult = result;
            ResultChanged?.Invoke(result);
        }
    }
}
