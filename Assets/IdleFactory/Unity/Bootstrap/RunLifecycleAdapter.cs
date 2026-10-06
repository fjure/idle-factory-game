using System;
using IdleFactory.Run.Application;
using UnityEngine;

namespace IdleFactory.Unity.Bootstrap
{
    /// <summary>
    /// Normalizes Unity focus, pause and quit callbacks into the Save V2 suspension and return commands of
    /// <see cref="RunPersistence"/>. It is the Feature 003 counterpart of <see cref="ApplicationLifecycleAdapter"/> and
    /// classifies a return exactly the same way (a focus-only departure that coincides with a screen configuration change
    /// is a <see cref="LifecycleReturnKind.ConfigurationChange"/>, FR-036): the checkpoint and the exactly-once offline
    /// accounting are unchanged, only the feedback differs. The cold start is not performed here; the bootstrap runs
    /// <see cref="RunPersistence.ColdStart"/> before it configures this adapter.
    /// </summary>
    public sealed class RunLifecycleAdapter : MonoBehaviour
    {
        /// <summary>See <see cref="ApplicationLifecycleAdapter.ConfigurationChangeWindowSeconds"/>.</summary>
        public const double ConfigurationChangeWindowSeconds = ApplicationLifecycleAdapter.ConfigurationChangeWindowSeconds;

        private RunPersistence persistence;
        private Action prepareForDeparture;
        private Func<Func<RunCommandResult<RunPersistenceOutcome>>, RunCommandResult<RunPersistenceOutcome>> measureResume;
        private Func<LifecycleScreenConfiguration> readScreenConfiguration = () => LifecycleScreenConfiguration.Current;
        private Func<double> readRealtimeSeconds = () => Time.realtimeSinceStartupAsDouble;
        private bool configured;
        private bool applicationPaused;
        private bool applicationFocused = true;
        private bool applicationQuitting;
        private bool departureRecorded;
        private bool departureIncludedPause;
        private bool departureFollowedConfigurationChange;
        private LifecycleScreenConfiguration departureConfiguration;
        private LifecycleScreenConfiguration observedConfiguration;
        private double lastConfigurationChangeSeconds = double.NegativeInfinity;

        public RunCommandResult<RunPersistenceOutcome> LastResult { get; private set; }

        /// <summary>Classifies <see cref="LastResult"/>; presentation withholds the offline summary of a configuration-only return.</summary>
        public LifecycleReturnKind LastReturnKind { get; private set; }

        public event Action<RunCommandResult<RunPersistenceOutcome>, LifecycleReturnKind> ResultChanged;

        /// <summary>
        /// Binds the lifecycle commands. <paramref name="prepareForDeparture"/> runs synchronously before the suspension is
        /// recorded (the host flushes its last active interval); <paramref name="measureResume"/> wraps every return so the
        /// host can log its elapsed time.
        /// </summary>
        public void Configure(
            RunPersistence runPersistence,
            Action prepareForDeparture,
            Func<Func<RunCommandResult<RunPersistenceOutcome>>, RunCommandResult<RunPersistenceOutcome>> measureResume = null)
        {
            if (runPersistence == null)
            {
                throw new ArgumentNullException(nameof(runPersistence));
            }

            if (configured)
            {
                throw new InvalidOperationException("The run lifecycle adapter has already been configured.");
            }

            persistence = runPersistence;
            this.prepareForDeparture = prepareForDeparture;
            this.measureResume = measureResume;
            observedConfiguration = readScreenConfiguration();
            configured = true;
        }

        /// <summary>
        /// Replaces the screen and realtime readers for deterministic tests. The bootstrap configures this adapter during
        /// start-up, so unlike the Feature 001 adapter the readers may be replaced afterwards; the observed configuration
        /// is then re-read from the new source.
        /// </summary>
        public void UseScreenConfigurationSource(Func<LifecycleScreenConfiguration> screenConfiguration, Func<double> realtimeSeconds)
        {
            readScreenConfiguration = screenConfiguration ?? throw new ArgumentNullException(nameof(screenConfiguration));
            readRealtimeSeconds = realtimeSeconds ?? throw new ArgumentNullException(nameof(realtimeSeconds));
            observedConfiguration = readScreenConfiguration();
            lastConfigurationChangeSeconds = double.NegativeInfinity;
        }

        private void Update()
        {
            ObserveScreenConfiguration();
        }

        /// <summary>Records the screen configuration the running frame used; exposed for lifecycle integration tests.</summary>
        public void ObserveScreenConfiguration()
        {
            if (!configured)
            {
                return;
            }

            var current = readScreenConfiguration();
            if (current.Equals(observedConfiguration))
            {
                return;
            }

            observedConfiguration = current;
            lastConfigurationChangeSeconds = readRealtimeSeconds();
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
                departureIncludedPause = true;
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

        /// <summary>Applies one normalized pause transition; exposed for deterministic lifecycle integration tests.</summary>
        public void NotifyPauseChanged(bool paused) => OnApplicationPause(paused);

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
            if (!configured || departureRecorded)
            {
                return;
            }

            // The command retains the exact checkpoint ID only when the write succeeds; if it fails, the next
            // matching return applies the contract's explicit fallback.
            departureRecorded = true;
            departureIncludedPause = applicationPaused;
            departureConfiguration = readScreenConfiguration();
            departureFollowedConfigurationChange = !departureConfiguration.Equals(observedConfiguration) ||
                readRealtimeSeconds() - lastConfigurationChangeSeconds <= ConfigurationChangeWindowSeconds;
            prepareForDeparture?.Invoke();
            Publish(persistence.RecordSuspension(), LifecycleReturnKind.None);
        }

        private void TryResume()
        {
            if (!configured || applicationQuitting || applicationPaused || !applicationFocused || !departureRecorded)
            {
                return;
            }

            // Mark the normalized return before calling into the application so duplicate callbacks cannot
            // reconcile the same in-process return twice, even when the reconciliation blocks.
            departureRecorded = false;
            var returnKind = ClassifyReturn();
            var result = measureResume != null
                ? measureResume(() => persistence.ResumeAndReconcile())
                : persistence.ResumeAndReconcile();
            Publish(result, returnKind);
        }

        private LifecycleReturnKind ClassifyReturn()
        {
            if (departureIncludedPause)
            {
                return LifecycleReturnKind.Return;
            }

            var returnConfiguration = readScreenConfiguration();
            var configurationChanged = departureFollowedConfigurationChange ||
                !returnConfiguration.Equals(departureConfiguration) ||
                !returnConfiguration.Equals(observedConfiguration);
            return configurationChanged ? LifecycleReturnKind.ConfigurationChange : LifecycleReturnKind.Return;
        }

        private void Publish(RunCommandResult<RunPersistenceOutcome> result, LifecycleReturnKind returnKind)
        {
            LastResult = result;
            LastReturnKind = returnKind;
            ResultChanged?.Invoke(result, returnKind);
        }
    }
}
